using System.IO;

namespace Duzenleme.Core;

/// <summary>Windows başlangıç görevinin durumu (WinRT StartupTaskState ile aynı değerler).</summary>
public enum StartupTaskState
{
    Disabled = 0,
    DisabledByUser = 1,
    Enabled = 2,
    DisabledByPolicy = 3,
    EnabledByPolicy = 4,
}

/// <summary>Store (MSIX) sürümüne özgü saf kurallar: dosya yönlendirmesi ve başlangıç görevi.</summary>
public static class PackagedApp
{
    /// <summary>
    /// Paketli uygulamanın %AppData% (Roaming) altında yeni oluşturduğu dosya ve klasörler
    /// %LOCALAPPDATA%\Packages\&lt;aile adı&gt;\LocalCache\Roaming\... altına yazılır; uygulama onları eski yerinde
    /// görür ama paket dışındaki Gezgin yalnızca burada. Yol Roaming altında değilse null.
    /// </summary>
    public static string? RedirectedRoamingPath(string path, string roamingAppData, string localAppData, string familyName)
    {
        var root = Path.TrimEndingDirectorySeparator(roamingAppData);
        path = Path.TrimEndingDirectorySeparator(path);
        if (root.Length == 0 || familyName.Length == 0) return null;
        string relative;
        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) relative = "";
        else if (path.Length > root.Length && path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && path[root.Length] is '\\' or '/')
            relative = path[(root.Length + 1)..];
        else return null;
        return Path.Combine(localAppData, "Packages", familyName, "LocalCache", "Roaming", relative);
    }

    /// <summary>
    /// Gezgin'de gösterilecek veri klasörü. Paketli uygulama <paramref name="dataDirectory"/>'yi birleşik görür
    /// (önce yönlendirilen kopya, yoksa gerçek konum). Ayarlar yönlendirilen klasördeyse orası; eski (paketsiz)
    /// sürümden kalan ayarlar hâlâ gerçek %AppData%'dan okunuyorsa gerçek konum; hiçbiri yoksa var olan klasör.
    /// </summary>
    public static string DataFolderToShow(string dataDirectory, string? redirected, Func<string, bool> fileExists,
        Func<string, bool> directoryExists, string settingsFile = "settings.json")
    {
        if (redirected is null) return dataDirectory;
        if (fileExists(Path.Combine(redirected, settingsFile))) return redirected;
        // Yönlendirilen kopyada yoksa uygulamanın gördüğü dosya gerçek konumdadır.
        if (fileExists(Path.Combine(dataDirectory, settingsFile))) return dataDirectory;
        return directoryExists(redirected) ? redirected : dataDirectory;
    }

    /// <summary>WinRT'den gelen durumu çözer; tanınmıyorsa null (yanlış durum göstermektense bilinmez).</summary>
    public static StartupTaskState? ParseStartupTaskState(int value) =>
        Enum.IsDefined((StartupTaskState)value) ? (StartupTaskState)value : null;

    /// <summary>
    /// Store sürümünde "Windows ile başlat" kartı: uygulama görevi yalnızca Disabled/Enabled iken açıp kapatabilir.
    /// Kullanıcı görevi Windows'tan kapattıysa (RequestEnableAsync bu seçimi ezmez), ilke yönetiyorsa ya da durum
    /// okunamadıysa anahtar yerine Windows'un Başlangıç uygulamaları sayfasına giden düğme gösterilir.
    /// </summary>
    public static StartupTaskView DescribeStartupTask(StartupTaskState? state) => state switch
    {
        StartupTaskState.Enabled => new(ShowToggle: true, IsOn: true, Note: null),
        StartupTaskState.Disabled => new(ShowToggle: true, IsOn: false, Note: null),
        StartupTaskState.DisabledByUser => new(ShowToggle: false, IsOn: false,
            Note: "Windows'un Başlangıç ayarlarından ya da Görev Yöneticisi'nden kapatılmış; Windows yeniden açmaya yalnızca oradan izin verir."),
        StartupTaskState.DisabledByPolicy => new(ShowToggle: false, IsOn: false, Note: "Yönetici ilkesiyle kapalı; buradan değiştirilemez."),
        StartupTaskState.EnabledByPolicy => new(ShowToggle: false, IsOn: true, Note: "Yönetici ilkesiyle açık; buradan değiştirilemez."),
        _ => new(ShowToggle: false, IsOn: false, Note: "Durum okunamadı; ayar Windows'un Başlangıç uygulamaları sayfasında."),
    };
}

/// <summary>Başlangıç görevi kartının görünümü: anahtar (açık/kapalı) ya da Windows ayarına giden düğme ve açıklama.</summary>
public sealed record StartupTaskView(bool ShowToggle, bool IsOn, string? Note);
