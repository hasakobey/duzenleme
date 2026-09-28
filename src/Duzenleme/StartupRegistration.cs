using System.IO;
using System.Runtime.InteropServices;
using Duzenleme.Core;
using Microsoft.Win32;

namespace Duzenleme;

/// <summary>
/// "Windows ile başlat" — paketsizken HKCU\...\Run kaydı ("NestDesk"; 2.0 ve öncesinde "Duzenleme", açılışta taşınır:
/// <see cref="MigrateLegacyRunValue"/>). Store (MSIX) sürümünde Run kaydı çalışmaz (HKCU yazmaları pakete özel kopyaya
/// gider); orada manifestteki başlangıç görevi WinRT StartupTask ile açılıp kapatılır. Kullanıcı görevi Windows'tan
/// kapattıysa ya da ilke yönetiyorsa değişiklik yalnızca Windows'un Başlangıç uygulamaları sayfasından.
/// </summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Görev Yöneticisi'nin Başlangıç sekmesindeki açık/kapalı seçimi (Run değeriyle aynı adlı ikili değer).</summary>
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = AppInfo.RunValueName;
    private const string LegacyValueName = AppInfo.LegacyRunValueName;

    /// <summary>Paket manifestindeki desktop:StartupTask kimliği (manifestle aynı olmalı).</summary>
    public const string TaskId = AppInfo.StartupTaskId;

    /// <summary>Windows'un Başlangıç uygulamaları ayar sayfası (görev kullanıcı ya da ilke nedeniyle kilitliyse).</summary>
    public const string StartupAppsSettingsUri = "ms-settings:startupapps";

    private static readonly TimeSpan TaskTimeout = TimeSpan.FromSeconds(10);

    private static string ExePath => Environment.ProcessPath ?? "";
    private static string ExeDirectory => Path.GetDirectoryName(ExePath) ?? "";

    /// <summary>
    /// Paketsiz sürümde Run kaydı bu programın klasörünü gösteriyor ve Görev Yöneticisi'nde kapatılmamış mı? Yeni ya da
    /// (geçiş henüz yapılmadıysa) eski adlı değer sayılır. (Store sürümünde <see cref="GetTaskStateAsync"/>.)
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            if (PackageInfo.IsPackaged) return false;
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            bool Active(string name) =>
                RunValueMigration.PointsInto(key?.GetValue(name) as string, ExeDirectory) &&
                !RunValueMigration.ApprovalDisabled(approved?.GetValue(name) as byte[]);
            return Active(ValueName) || Active(LegacyValueName);
        }
    }

    public static void Set(bool enabled)
    {
        // Test örneği (--desktop) gerçek Run değerini test exe'sine çevirmesin.
        if (AppHost.IsTestDesktop) { DebugLog.Write("başlangıç kaydı değiştirilmedi (test masaüstü)"); return; }
        // Paketliyken yazılan Run değeri yalnızca pakete özel kopyada kalır ve oturum açılışında okunmaz.
        if (PackageInfo.IsPackaged) return;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, RunValueMigration.Command(ExePath));
            // Görev Yöneticisi'nde kapatılmışsa o seçim de kalkar: anahtar açık görünüp Windows başlatmıyor olmasın.
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
            if (RunValueMigration.ApprovalDisabled(approved?.GetValue(ValueName) as byte[]))
                approved?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        else DeleteIfOurs(key, ValueName);
        // Bu kopyanın eski adlı değeri artık gereksiz: açıkken ikinci kez başlatmasın, kapalıyken hiç başlatmasın.
        DeleteIfOurs(key, LegacyValueName);
    }

    /// <summary>Değer bu programın klasörünü gösteriyorsa onu ve Görev Yöneticisi'ndeki seçim kaydını siler.</summary>
    private static void DeleteIfOurs(RegistryKey run, string name)
    {
        if (!RunValueMigration.PointsInto(run.GetValue(name) as string, ExeDirectory)) return;
        run.DeleteValue(name, throwOnMissingValue: false);
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        approved?.DeleteValue(name, throwOnMissingValue: false);
    }

    /// <summary>
    /// 2.0 → 2.1: bu programın klasörünü gösteren "Duzenleme" Run değeri "NestDesk" olur (yeni exe'yle); Görev Yöneticisi'ndeki
    /// açık/kapalı seçimi de taşınır (bkz. <see cref="RunValueMigration.Plan"/>). Paketsiz sürümde, test örneği dışında, açılışta
    /// bir kez ve arka planda çağrılır; hata fırlatmaz.
    /// </summary>
    public static void MigrateLegacyRunValue()
    {
        if (PackageInfo.IsPackaged || AppHost.IsTestDesktop || ExePath.Length == 0) return;
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKey);
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
            var legacyApproval = approved?.GetValue(LegacyValueName) as byte[];
            var plan = RunValueMigration.Plan(run.GetValue(ValueName) as string, run.GetValue(LegacyValueName) as string,
                approved?.GetValue(ValueName) is not null, legacyApproval is not null, ExePath);
            if (plan.IsEmpty) return;
            DebugLog.Write($"başlangıç kaydı geçişi: {plan}");
            var command = RunValueMigration.Command(ExePath);
            if (plan.WriteCurrent) run.SetValue(ValueName, command);
            if (plan.RewriteLegacy) run.SetValue(LegacyValueName, command);
            // Önce yeni değer ve seçim yazılır, sonra eskiler silinir: yarıda kalırsa en kötü ihtimalle ikisi birden durur
            // (tek örnek olduğundan zararsız), hiçbiri kaybolmaz.
            if (plan.CopyApprovalToCurrent && legacyApproval is not null) approved?.SetValue(ValueName, legacyApproval, RegistryValueKind.Binary);
            if (plan.DeleteLegacyApproval) approved?.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            if (plan.DeleteLegacy) run.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            DebugLog.Write($"başlangıç kaydı taşınamadı: {ex.Message}");
        }
    }

    /// <summary>Store sürümünde başlangıç görevinin durumu; paketsizken ya da okunamazsa null.</summary>
    public static Task<StartupTaskState?> GetTaskStateAsync() => RunTask(enable: null);

    /// <summary>
    /// Store sürümünde görevi açar ya da kapatır ve sonraki durumu döndürür (paketsizken null). Paketli masaüstü
    /// uygulamasında Windows onay sormaz; kullanıcı görevi Windows'tan kapattıysa ya da ilke varsa durum değişmez.
    /// Test örneğinde (--desktop) değiştirmez, yalnızca durumu okur.
    /// </summary>
    public static Task<StartupTaskState?> SetTaskEnabledAsync(bool enabled) =>
        AppHost.IsTestDesktop ? GetTaskStateAsync() : RunTask(enabled);

    // WinRT çağrıları bekleyerek yapılır (bkz. WinRt): arayüz donmasın diye arka planda.
    private static Task<StartupTaskState?> RunTask(bool? enable) =>
        PackageInfo.IsPackaged ? Task.Run(() => QueryTask(enable)) : Task.FromResult<StartupTaskState?>(null);

    /// <summary>
    /// StartupTask.GetAsync(TaskId); istenirse RequestEnableAsync/Disable; sonra State. Bir şey ters giderse null.
    /// Paket kimliği denetlenmez (testler paketsiz süreçte hatanın yutulduğunu doğrular).
    /// </summary>
    internal static StartupTaskState? QueryTask(bool? enable)
    {
        try
        {
            if (WinRt.Factory<IStartupTaskStatics>("Windows.ApplicationModel.StartupTask") is not { } statics) return null;
            IntPtr pointer;
            using (var id = new WinRt.HString(TaskId))
                pointer = WinRt.AwaitObject((out IntPtr operation) => statics.GetAsync(id.Handle, out operation), TaskTimeout);
            try
            {
                var task = (IStartupTask)Marshal.GetObjectForIUnknown(pointer);
                if (enable == true) return PackagedApp.ParseStartupTaskState(WinRt.AwaitInt32(task.RequestEnableAsync, TaskTimeout));
                if (enable == false) Marshal.ThrowExceptionForHR(task.Disable());
                Marshal.ThrowExceptionForHR(task.GetState(out var state));
                return PackagedApp.ParseStartupTaskState(state);
            }
            finally
            {
                Marshal.Release(pointer);
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"başlangıç görevi ({enable?.ToString() ?? "durum"}): {ex.GetType().Name} {ex.Message}");
            return null;
        }
    }

    // WinRT arayüzleri IInspectable'dan türer: ilk üç yuva hiç çağrılmaz, yalnızca sanal tablo sırası tutsun diye yer
    // tutar. GUID'ler ve yöntem sırası Windows.ApplicationModel.winmd'den.

    [ComImport, Guid("ee5b60bd-a148-41a7-b26e-e8b88a1e62f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IStartupTaskStatics
    {
        void GetIids();
        void GetRuntimeClassName();
        void GetTrustLevel();
        [PreserveSig] int GetForCurrentPackageAsync(out IntPtr operation);
        [PreserveSig] int GetAsync(IntPtr taskId, out IntPtr operation);
    }

    [ComImport, Guid("f75c23c8-b5f2-4f6c-88dd-36cb1d599d17"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IStartupTask
    {
        void GetIids();
        void GetRuntimeClassName();
        void GetTrustLevel();
        [PreserveSig] int RequestEnableAsync(out IntPtr operation);
        [PreserveSig] int Disable();
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetTaskId(out IntPtr taskId);
    }
}
