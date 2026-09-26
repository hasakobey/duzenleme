using System.IO;

namespace Duzenleme.Core;

/// <summary>Masaüstü öğelerini "Klasörler / Kısayollar / Dosyalar" bölmeleri için sınıflandırır.</summary>
public static class DesktopItems
{
    /// <summary>Masaüstünde uygulama başlatan öğeler: kısayollar ve doğrudan duran programlar.</summary>
    public static bool IsShortcut(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".lnk" or ".url" or ".appref-ms" or ".exe";

    public static bool Matches(DesktopFilter filter, FileSystemInfo item)
    {
        if ((item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) return false;
        if (item.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return false;
        return filter switch
        {
            DesktopFilter.All => true,
            DesktopFilter.Folders => item is DirectoryInfo,
            DesktopFilter.Shortcuts => item is FileInfo && IsShortcut(item.Name),
            DesktopFilter.Files => item is FileInfo && !IsShortcut(item.Name),
            _ => false,
        };
    }

    public static string Label(DesktopFilter filter) => filter switch
    {
        DesktopFilter.All => "Masaüstü",
        DesktopFilter.Folders => "Klasörler",
        DesktopFilter.Shortcuts => "Kısayollar",
        DesktopFilter.Files => "Dosyalar",
        _ => "",
    };

    public static string Description(DesktopFilter filter) => filter switch
    {
        DesktopFilter.All => "Masaüstündeki her şey",
        DesktopFilter.Folders => "Masaüstündeki klasörler",
        DesktopFilter.Shortcuts => "Uygulama ve web kısayolları",
        DesktopFilter.Files => "Masaüstünde kalan dosyalar",
        _ => "",
    };

    public static readonly DesktopFilter[] Filters = [DesktopFilter.Folders, DesktopFilter.Shortcuts, DesktopFilter.Files, DesktopFilter.All];
}
