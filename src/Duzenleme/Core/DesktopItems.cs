using System.IO;

namespace Duzenleme.Core;

/// <summary>Masaüstü öğelerini "Klasörler / Kısayollar / Dosyalar" bölmeleri için sınıflandırır.</summary>
public static class DesktopItems
{
    /// <summary>Masaüstünde uygulama başlatan öğeler: kısayollar ve doğrudan duran programlar.</summary>
    public static bool IsShortcut(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".lnk" or ".url" or ".appref-ms" or ".exe";

    public static bool Matches(DesktopFilter filter, FileSystemInfo item) =>
        Matches(filter, item.Name, item is DirectoryInfo, item.Attributes);

    /// <summary>Masaüstü anlık görüntüsündeki kayıt için (diske yeniden bakmadan).</summary>
    public static bool Matches(DesktopFilter filter, DirEntry entry) =>
        Matches(filter, entry.Name, entry.IsDirectory, entry.Attributes);

    public static bool Matches(DesktopFilter filter, string name, bool isDirectory, FileAttributes attributes)
    {
        if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) return false;
        if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return false;
        return filter switch
        {
            DesktopFilter.All => true,
            DesktopFilter.Folders => isDirectory,
            DesktopFilter.Shortcuts => !isDirectory && IsShortcut(name),
            DesktopFilter.Files => !isDirectory && !IsShortcut(name),
            _ => false,
        };
    }

    /// <summary>Tarayıcının/indiricinin henüz bitmemiş dosyası (adı bitince değişir): izlemede olayları gürültü sayılır.</summary>
    public static bool IsPartialDownload(string? name) =>
        name is not null && Path.GetExtension(name).ToLowerInvariant() is ".crdownload" or ".part" or ".partial" or ".download" or ".opdownload";

    /// <summary>Türün bölme başlığı (kaydedilmez: başlık boşsa her gösterişte arayüz dilinde).</summary>
    public static string Label(DesktopFilter filter) => filter switch
    {
        DesktopFilter.All => L.T("Masaüstü"),
        DesktopFilter.Folders => L.T("Klasörler"),
        DesktopFilter.Shortcuts => L.T("Kısayollar"),
        DesktopFilter.Files => L.T("Dosyalar"),
        _ => "",
    };

    /// <summary>"Ne gösterilsin?" ve "Yeni bölme…"deki açıklama.</summary>
    public static string Description(DesktopFilter filter) => filter switch
    {
        DesktopFilter.All => L.T("Masaüstündeki her şey"),
        DesktopFilter.Folders => L.T("Masaüstündeki klasörler"),
        DesktopFilter.Shortcuts => L.T("Uygulama ve web kısayolları"),
        DesktopFilter.Files => L.T("Masaüstünde kalan dosyalar"),
        _ => "",
    };

    public static readonly DesktopFilter[] Filters = [DesktopFilter.Folders, DesktopFilter.Shortcuts, DesktopFilter.Files, DesktopFilter.All];
}
