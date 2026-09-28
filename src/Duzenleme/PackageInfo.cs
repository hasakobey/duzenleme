using System.Runtime.InteropServices;
using System.Text;

namespace Duzenleme;

/// <summary>
/// Uygulama MSIX paketi içinde (Microsoft Store sürümü) mi çalışıyor? Paketliyken Windows %AppData% ve HKCU
/// yazmalarını pakete özel yere yönlendirir ve Run kaydı çalışmaz; paketsiz (kurulum/taşınabilir) davranış değişmez.
/// WinRT projeksiyonu (~25 MB) eklenmesin diye her şey doğrudan Win32/COM çağrısıyla yapılır.
/// </summary>
public static class PackageInfo
{
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    // Sonuç süreç boyunca değişmez: bir kez sorulur.
    private static readonly Lazy<string?> FullName = new(() => Query(GetCurrentPackageFullName));
    private static readonly Lazy<string?> Family = new(() => Query(GetCurrentPackageFamilyName));
    private static readonly Lazy<string?> Aumid = new(() => Query(GetCurrentApplicationUserModelId));

    /// <summary>Paket kimliğiyle mi çalışıyor (Store/MSIX sürümü)?</summary>
    public static bool IsPackaged => FullName.Value is not null;

    /// <summary>Paket aile adı (ör. "Yayinci.NestDesk_1a2b3c4d5e6f7"); paketsizken null.</summary>
    public static string? FamilyName => IsPackaged ? Family.Value : null;

    /// <summary>Sürecin uygulama kimliği (ör. "Yayinci.NestDesk_1a2b3c4d5e6f7!AddWidget"); paketsizken null.</summary>
    public static string? ApplicationUserModelId => IsPackaged ? Aumid.Value : null;

    private delegate int NameQuery(ref uint length, StringBuilder? name);

    private static string? Query(NameQuery query)
    {
        // Paketsiz süreçte APPMODEL_ERROR_NO_PACKAGE (15700) döner; beklenmeyen bir hata da paketsiz sayılır
        // (böylece var olan davranış korunur).
        uint length = 0;
        if (query(ref length, null) != ERROR_INSUFFICIENT_BUFFER) return null;
        var name = new StringBuilder((int)length);
        return query(ref length, name) == ERROR_SUCCESS ? name.ToString() : null;
    }

    /// <summary>
    /// Uygulamayı Windows başlangıç görevi mi açtı? Görev exe'yi argümansız başlatır (--minimized gelmez);
    /// oturum açılışında ana pencere belirmesin diye etkinleştirme türüne bakılır. Windows bu bilgiyi yalnızca
    /// ilk çağrıda verir: açılışta bir kez çağır. Bir şey ters giderse false (pencere normal açılışta olduğu gibi gösterilir).
    /// </summary>
    public static bool LaunchedByStartupTask()
    {
        if (!IsPackaged) return false;
        const int ActivationKindStartupTask = 1020;
        try
        {
            if (WinRt.Factory<IAppInstanceStatics>("Windows.ApplicationModel.AppInstance") is not { } statics) return false;
            if (statics.GetActivatedEventArgs(out var args) < 0 || args is null) return false;
            return args.GetKind(out var kind) >= 0 && kind == ActivationKindStartupTask;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"etkinleştirme türü okunamadı: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Store güncellemesi çalışan uygulamayı kapatır; kayıtlı süreç güncelleme bitince yeniden başlatılır. Tepside
    /// sessizce dönsün. Çökme/donma sonrası ve Windows güncellemesiyle yeniden başlayan bilgisayarda yeniden başlatılmaz
    /// (orada başlangıç görevi, kullanıcının seçimine göre, açar).
    /// </summary>
    public static void RegisterRestartAfterUpdate()
    {
        if (!IsPackaged) return;
        const int RESTART_NO_CRASH = 1, RESTART_NO_HANG = 2, RESTART_NO_REBOOT = 8;
        var hr = RegisterApplicationRestart("--minimized", RESTART_NO_CRASH | RESTART_NO_HANG | RESTART_NO_REBOOT);
        if (hr < 0) DebugLog.Write($"RegisterApplicationRestart başarısız: 0x{hr:X8}");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint length, StringBuilder? name);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref uint length, StringBuilder? name);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentApplicationUserModelId(ref uint length, StringBuilder? id);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterApplicationRestart(string commandLine, int flags);

    /// <summary>
    /// Aynı paketin başka bir uygulamasını (AUMID) verilen argümanlarla başlatır (IApplicationActivationManager). Olmazsa false.
    /// </summary>
    public static bool ActivateApplication(string aumid, string arguments)
    {
        try
        {
            var manager = (IApplicationActivationManager)new ApplicationActivationManager();
            try
            {
                const int AO_NONE = 0;
                var hr = manager.ActivateApplication(aumid, arguments, AO_NONE, out var processId);
                DebugLog.Write($"{aumid} etkinleştirildi: 0x{hr:X8}, süreç {processId}");
                return hr >= 0;
            }
            finally { Marshal.ReleaseComObject(manager); }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            DebugLog.Write($"{aumid} etkinleştirilemedi: {ex.Message}");
            return false;
        }
    }

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManager { }

    // shobjidl_core.h; yalnızca ilk yöntem kullanılır (sonrakiler sanal tabloda ondan sonra gelir).
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments, int options, out uint processId);
    }

    // WinRT arayüzleri IInspectable'dan türer: ilk üç yuva (GetIids, GetRuntimeClassName, GetTrustLevel) hiç çağrılmaz,
    // yalnızca sanal tablo sırası tutsun diye yer tutar. GUID'ler ve yöntem sırası Windows.ApplicationModel.winmd'den.

    [ComImport, Guid("9d11e77f-9ea6-47af-a6ec-46784c5ba254"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAppInstanceStatics
    {
        void GetIids();
        void GetRuntimeClassName();
        void GetTrustLevel();
        [PreserveSig] int GetRecommendedInstance(out IntPtr instance);
        [PreserveSig] int GetActivatedEventArgs([MarshalAs(UnmanagedType.Interface)] out IActivatedEventArgs? args);
    }

    [ComImport, Guid("cf651713-cd08-4fd8-b697-a281b6544e2e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivatedEventArgs
    {
        void GetIids();
        void GetRuntimeClassName();
        void GetTrustLevel();
        [PreserveSig] int GetKind(out int kind);
    }
}
