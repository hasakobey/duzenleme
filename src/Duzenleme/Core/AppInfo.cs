namespace Duzenleme.Core;

/// <summary>
/// Görünen ad ve kimlikler. 2.0.0'da görünen ad Düzenleme → NestDesk oldu; 2.1.0'da program dosyası, veri klasörü, Run
/// değeri, klasör simgesi öneki ve Store uygulama kimlikleri de yeni ada geçti. Eski adlar Legacy* olarak tanınır ve açılışta
/// taşınır (Core/DataFolderLocator, Core/RunValueMigration, kurulumdaki [Code]). DEĞİŞMEZ işaretliler mevcut kurulumları
/// bozar (NestDesk.iss, AppxManifest.xml ve diskteki veriler bunlara bağlı; IdentityTests sabitler).
/// </summary>
public static class AppInfo
{
    public const string Name = "NestDesk";
    public const string FormerName = "Düzenleme";
    /// <summary>Kısa tanıtım cümlesi (Ana sayfa, Ayarlar → Hakkında), arayüz dilinde.</summary>
    public static string Tagline => L.T("Masaüstün için derli toplu bir yuva.");

    /// <summary>Depo adresi (uygulamanın eski adını taşır). Depo yeniden adlandırılırsa yalnızca burası değişir:
    /// GitHub eski adresleri yeni depoya yönlendirir, eski sürümlerdeki bağlantılar da çalışmaya devam eder.</summary>
    public const string RepoUrl = "https://github.com/hasakobey/duzenleme";
    public const string ReleasesUrl = RepoUrl + "/releases";
    public const string IssuesUrl = RepoUrl + "/issues";
    public const string PrivacyUrl = RepoUrl + "/blob/main/PRIVACY.md";

    public static string Version => typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "";

    /// <summary>Program dosyası (csproj AssemblyName + ".exe"); 2.0 ve öncesi <see cref="LegacyExeName"/>.</summary>
    public const string ExeName = "NestDesk.exe";
    public const string LegacyExeName = "Duzenleme.exe";

    /// <summary>DEĞİŞMEZ: mutex + .show/.add/.exit. 2.0 (Duzenleme.exe) ile 2.1 (NestDesk.exe) birbirini görür; kurulum
    /// ikisini de aynı sinyalle kapatır (NestDesk.iss InstanceId).</summary>
    public const string InstanceIdPrefix = "Duzenleme.";

    /// <summary>%AppData% altındaki veri klasörü; 2.0 ve öncesininki açılışta yeniden adlandırılır.</summary>
    public const string DataFolderName = "NestDesk";
    public const string LegacyDataFolderName = "Duzenleme";

    /// <summary>HKCU\...\Run değer adı; eskisi açılışta ve kurulumda yeni ada taşınır.</summary>
    public const string RunValueName = "NestDesk";
    public const string LegacyRunValueName = "Duzenleme";

    /// <summary>Store (MSIX) kimlikleri: AppxManifest.xml'deki Application Id'ler ve StartupTask TaskId.</summary>
    public const string PackageAppId = "NestDesk";
    public const string PackageAddWidgetAppId = "AddWidget";
    public const string StartupTaskId = "NestDeskStartup";
}
