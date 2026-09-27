using System.Runtime.InteropServices;
using Duzenleme.Core;
using Microsoft.Win32;

namespace Duzenleme;

/// <summary>
/// "Windows ile başlat" — paketsizken HKCU\...\Run kaydı. Store (MSIX) sürümünde Run kaydı çalışmaz (HKCU yazmaları
/// pakete özel kopyaya gider); orada manifestteki başlangıç görevi WinRT StartupTask ile açılıp kapatılır. Kullanıcı
/// görevi Windows'tan kapattıysa ya da ilke yönetiyorsa değişiklik yalnızca Windows'un Başlangıç uygulamaları sayfasından.
/// </summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Duzenleme";

    /// <summary>Paket manifestindeki desktop:StartupTask kimliği (manifestle aynı olmalı).</summary>
    public const string TaskId = "DuzenlemeStartup";

    /// <summary>Windows'un Başlangıç uygulamaları ayar sayfası (görev kullanıcı ya da ilke nedeniyle kilitliyse).</summary>
    public const string StartupAppsSettingsUri = "ms-settings:startupapps";

    private static readonly TimeSpan TaskTimeout = TimeSpan.FromSeconds(10);

    private static string Command => $"\"{Environment.ProcessPath}\" --minimized";

    /// <summary>Paketsiz sürümde Run kaydı bu exe'yi mi gösteriyor? (Store sürümünde <see cref="GetTaskStateAsync"/>.)</summary>
    public static bool IsEnabled
    {
        get
        {
            if (PackageInfo.IsPackaged) return false;
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value && value.Contains(Environment.ProcessPath ?? "\0", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void Set(bool enabled)
    {
        // Paketliyken yazılan Run değeri yalnızca pakete özel kopyada kalır ve oturum açılışında okunmaz.
        if (PackageInfo.IsPackaged) return;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Store sürümünde başlangıç görevinin durumu; paketsizken ya da okunamazsa null.</summary>
    public static Task<StartupTaskState?> GetTaskStateAsync() => RunTask(enable: null);

    /// <summary>
    /// Store sürümünde görevi açar ya da kapatır ve sonraki durumu döndürür (paketsizken null). Paketli masaüstü
    /// uygulamasında Windows onay sormaz; kullanıcı görevi Windows'tan kapattıysa ya da ilke varsa durum değişmez.
    /// </summary>
    public static Task<StartupTaskState?> SetTaskEnabledAsync(bool enabled) => RunTask(enabled);

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
