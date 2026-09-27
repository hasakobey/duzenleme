namespace Duzenleme.Core;

/// <summary>Görünen ad ve ad değişikliğinde (Düzenleme → NestDesk, 2.0.0) BİLEREK eski adla bırakılan kimlikler.
/// DEĞİŞMEZ değerler mevcut kurulumları bozar: Duzenleme.iss, AppxManifest.xml ve diskteki veriler bunlara bağlı (IdentityTests sabitler).</summary>
public static class AppInfo
{
    public const string Name = "NestDesk";
    public const string FormerName = "Düzenleme";
    public const string Tagline = "Masaüstün için derli toplu bir yuva.";
    public const string ReleasesUrl = "https://github.com/hasakobey/duzenleme/releases";
    public static string Version => typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "";

    public const string InstanceIdPrefix = "Duzenleme.";      // DEĞİŞMEZ: mutex + .show/.add/.exit; Duzenleme.iss InstanceId
    public const string DataFolderName = "Duzenleme";         // DEĞİŞMEZ: %AppData% altı
    public const string RunValueName = "Duzenleme";           // DEĞİŞMEZ: HKCU\...\Run değer adı
    public const string StartupTaskId = "DuzenlemeStartup";   // DEĞİŞMEZ: AppxManifest StartupTask TaskId
}
