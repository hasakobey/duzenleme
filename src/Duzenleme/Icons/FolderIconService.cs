using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;

namespace Duzenleme.Icons;

/// <summary>
/// Klasöre özel simge atar/kaldırır. Simge dosyası klasörün içine gizli olarak konur;
/// böylece klasör taşınsa da simgesi korunur.
/// </summary>
public static class FolderIconService
{
    /// <summary>Yeni yazılan simge dosyalarının öneki (gizli + sistem dosyası, klasörün içinde).</summary>
    internal const string IconPrefix = ".nestdesk-";

    /// <summary>DEĞİŞMEZ: 2.0 ve öncesinin simge dosyaları bu önekle tanınır (onarma, kaldırma, "hepsini kaldır").</summary>
    internal const string LegacyIconPrefix = ".duzenleme-";

    internal static readonly string[] OwnPrefixes = [IconPrefix, LegacyIconPrefix];
    private const uint FCSM_ICONFILE = 0x00000010;
    private const uint FCS_FORCEWRITE = 0x00000002;
    private const int SHCNE_UPDATEDIR = 0x00001000;
    private const int SHCNE_UPDATEITEM = 0x00002000;
    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_PATHW = 0x0005;
    private const uint SHCNF_IDLIST = 0x0000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFOLDERCUSTOMSETTINGS
    {
        public uint dwSize;
        public uint dwMask;
        public IntPtr pvid;
        public string? pszWebViewTemplate;
        public uint cchWebViewTemplate;
        public string? pszWebViewTemplateVersion;
        public string? pszInfoTip;
        public uint cchInfoTip;
        public IntPtr pclsid;
        public uint dwFlags;
        public string? pszIconFile;
        public uint cchIconFile;
        public int iIconIndex;
        public string? pszLogo;
        public uint cchLogo;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetSetFolderCustomSettings(ref SHFOLDERCUSTOMSETTINGS pfcs, string pszPath, uint dwReadWrite);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, string? dwItem1, IntPtr dwItem2);

    static FolderIconService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>
    /// Bir klasöre simge verildi (true) ya da simgesi kaldırıldı (false). Çağıranın iş parçacığında tetiklenir (toplu
    /// kaldırmada arka planda); AppHost verilen simgeleri kaydeder (<see cref="Core.AppSettings.IconFolders"/>).
    /// </summary>
    public static event Action<string, bool>? Changed;

    /// <summary>Dosya adı bu uygulamanın yazdığı bir klasör simgesi mi (yeni ya da eski önekli .ico)?</summary>
    public static bool IsOwnIconFile(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return name.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
            && OwnPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>desktop.ini'yi kodlamasını algılayarak okur (UTF-16/UTF-8 BOM'lu ya da sistemin ANSI kod sayfası).</summary>
    private static (string[] Lines, Encoding Encoding) ReadIni(string ini)
    {
        var bytes = File.ReadAllBytes(ini);
        Encoding encoding =
            bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE ? Encoding.Unicode :
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? new UTF8Encoding(true) :
            Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        var text = encoding.GetString(bytes).TrimStart('﻿');
        return (text.Split(["\r\n", "\n"], StringSplitOptions.None), encoding);
    }

    /// <summary>desktop.ini'deki IconResource (ya da eski IconFile) değeri, ",indeks" kısmı olmadan.</summary>
    private static string? IconValue(string[] lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            foreach (var key in new[] { "IconResource", "IconFile" })
            {
                if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;
                var eq = line.IndexOf('=');
                if (eq < 0) continue;
                var value = line[(eq + 1)..].Trim();
                var comma = value.LastIndexOf(',');
                if (comma > 1 && int.TryParse(value[(comma + 1)..].Trim(), out _)) value = value[..comma];
                return value.Trim().Trim('"');
            }
        }
        return null;
    }

    private static string Resolve(string folder, string value)
    {
        var expanded = Environment.ExpandEnvironmentVariables(value);
        return Path.IsPathRooted(expanded) ? expanded : Path.Combine(folder, expanded);
    }

    /// <summary>Klasörün özel simgesi var ve simge dosyası gerçekten duruyor mu? (Bozuk/eski simge "yok" sayılır.)</summary>
    public static bool HasCustomIcon(string folder)
    {
        var ini = Path.Combine(folder, "desktop.ini");
        try
        {
            if (!File.Exists(ini)) return false;
            var value = IconValue(ReadIni(ini).Lines);
            return value is { Length: > 0 } && File.Exists(Resolve(folder, value));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>
    /// Eski sürümlerin tam yol yazdığı desktop.ini'leri onarır: simge dosyası klasörün içindeyse göreli yola çevirir,
    /// hiç yoksa klasörü varsayılan simgeye döndürür. Böylece taşınan/yeniden adlandırılan klasörlerin simgesi bozulmaz.
    /// </summary>
    public static void RepairDesktopFolders(string desktop)
    {
        if (!Directory.Exists(desktop)) return;
        foreach (var folder in Directory.EnumerateDirectories(desktop))
        {
            try
            {
                var ini = Path.Combine(folder, "desktop.ini");
                if (!File.Exists(ini)) continue;
                var (lines, encoding) = ReadIni(ini);
                var value = IconValue(lines);
                if (value is null || !Path.IsPathRooted(value)) continue;
                var name = Path.GetFileName(value);
                if (!IsOwnIconFile(name)) continue; // başka programın simgesine dokunma

                if (File.Exists(Path.Combine(folder, name)))
                {
                    var fixedLines = lines.Select(l => l.TrimStart().StartsWith("IconResource", StringComparison.OrdinalIgnoreCase)
                        ? $"IconResource={name},0" : l).ToArray();
                    var attrs = File.GetAttributes(ini);
                    File.SetAttributes(ini, FileAttributes.Normal);
                    File.WriteAllText(ini, string.Join("\r\n", fixedLines), encoding);
                    File.SetAttributes(ini, attrs);
                    Refresh(folder);
                }
                else if (!File.Exists(value))
                {
                    Reset(folder);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Çizimi .ico'ya çevirip klasöre uygular.</summary>
    public static void Apply(string folder, Drawing drawing)
    {
        var bytes = FolderIconRenderer.ToIco(drawing);
        RemoveOwnIconFiles(folder);

        // Her seferinde yeni ad: Explorer'ın simge önbelleği eski görüntüyü göstermesin.
        var iconPath = Path.Combine(folder, $"{IconPrefix}{DateTime.Now:yyyyMMddHHmmss}.ico");
        File.WriteAllBytes(iconPath, bytes);
        File.SetAttributes(iconPath, FileAttributes.Hidden | FileAttributes.System);

        // desktop.ini yoksa UTF-16 olarak başlat: Windows sonraki yazmalarda Unicode'u korur (Türkçe/Çince klasör adları vb.).
        var ini = Path.Combine(folder, "desktop.ini");
        if (!File.Exists(ini)) File.WriteAllText(ini, "[.ShellClassInfo]\r\n", Encoding.Unicode);

        var settings = new SHFOLDERCUSTOMSETTINGS
        {
            dwSize = (uint)Marshal.SizeOf<SHFOLDERCUSTOMSETTINGS>(),
            dwMask = FCSM_ICONFILE,
            // Göreli yol: klasör taşınsa, yeniden adlandırılsa ya da başka dilde Windows'ta açılsa da simge bulunur.
            pszIconFile = Path.GetFileName(iconPath),
            cchIconFile = 0,
            iIconIndex = 0,
        };
        var hr = SHGetSetFolderCustomSettings(ref settings, folder, FCS_FORCEWRITE);
        if (hr != 0) Marshal.ThrowExceptionForHR(hr);
        Refresh(folder);
        Changed?.Invoke(folder, true);
    }

    /// <summary>Klasörü varsayılan Windows simgesine döndürür.</summary>
    /// <param name="iconCache">false: Windows'un simge önbelleği tazelenmez (toplu kaldırmada en sonda bir kez yapılır).</param>
    public static void Reset(string folder, bool iconCache = true)
    {
        var ini = Path.Combine(folder, "desktop.ini");
        if (File.Exists(ini))
        {
            var attrs = File.GetAttributes(ini);
            File.SetAttributes(ini, FileAttributes.Normal);
            // Yalnızca simge satırlarını sil; desktop.ini'deki diğer ayarlar (ör. yerelleştirilmiş ad) kalsın.
            var (all, encoding) = ReadIni(ini);
            var lines = all
                .Where(l => !l.TrimStart().StartsWith("IconResource", StringComparison.OrdinalIgnoreCase)
                            && !l.TrimStart().StartsWith("IconFile", StringComparison.OrdinalIgnoreCase)
                            && !l.TrimStart().StartsWith("IconIndex", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var meaningful = lines.Any(l => l.Trim().Length > 0 && !l.Trim().StartsWith('[') && !l.Trim().StartsWith(';'));
            if (meaningful)
            {
                File.WriteAllText(ini, string.Join("\r\n", lines), encoding);
                File.SetAttributes(ini, attrs);
            }
            else
            {
                File.Delete(ini);
            }
        }
        RemoveOwnIconFiles(folder);
        Refresh(folder, iconCache);
        Changed?.Invoke(folder, false);
    }

    private static void RemoveOwnIconFiles(string folder)
    {
        foreach (var old in OwnPrefixes.SelectMany(prefix => Directory.EnumerateFiles(folder, prefix + "*.ico")).ToList())
        {
            try
            {
                File.SetAttributes(old, FileAttributes.Normal);
                File.Delete(old);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// "Klasör simgelerinin hepsini kaldır" (Ayarlar → Gelişmiş; Store sürümünde kaldırma programı olmadığı için): verilen
    /// kökler altındaki klasörlerden (kökün kendisi değil; en çok <paramref name="depth"/> düzey) bu uygulamanın verdiği
    /// simgeleri kaldırır. desktop.ini simgesi bizimse klasör varsayılan simgeye döner; başka programın simgesi olan klasörde
    /// yalnızca bizim artık dosyalarımız silinir. Bağlantı (junction) klasörlere girilmez. Arka planda çağır.
    /// </summary>
    /// <param name="includeRoots">Köklerin kendisine de bakılır (klasör portalının gösterdiği klasör; 2.1 P6).</param>
    /// <param name="failures">Verilirse erişilemediği için dokunulamayan klasörler buraya eklenir.</param>
    public static (List<string> Removed, int Failed) RemoveAllOwnIcons(IEnumerable<string> roots, int depth = 2, int maxFolders = 20000,
        bool includeRoots = false, List<string>? failures = null)
    {
        List<string> removed = [];
        int failed = 0, visited = 0;
        var queue = new Queue<(string Folder, int Level)>();
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase)) queue.Enqueue((root, 0));
        while (queue.Count > 0 && visited < maxFolders)
        {
            var (folder, level) = queue.Dequeue();
            if (level > 0 || includeRoots)
            {
                visited++;
                try
                {
                    if (RemoveOwnIcon(folder)) removed.Add(folder);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failed++;
                    failures?.Add(folder);
                }
            }
            if (level >= depth) continue;
            try
            {
                foreach (var child in new DirectoryInfo(folder).EnumerateDirectories())
                    if (!child.Attributes.HasFlag(FileAttributes.ReparsePoint)) queue.Enqueue((child.FullName, level + 1));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        // Simge önbelleği klasör başına değil, bir kez tazelenir.
        if (removed.Count > 0) SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, null, IntPtr.Zero);
        return (removed, failed);
    }

    /// <summary>Klasördeki bu uygulamaya ait simgeyi kaldırır; bir şey değiştiyse true.</summary>
    internal static bool RemoveOwnIcon(string folder)
    {
        var ini = Path.Combine(folder, "desktop.ini");
        if (File.Exists(ini) && IconValue(ReadIni(ini).Lines) is { } value && IsOwnIconFile(value))
        {
            Reset(folder, iconCache: false);
            return true;
        }
        // desktop.ini başka bir simgeyi gösteriyor (ya da yok): yalnızca artık kalmış simge dosyalarımız silinir, görünüm değişmez.
        if (!OwnPrefixes.Any(prefix => Directory.EnumerateFiles(folder, prefix + "*.ico").Any())) return false;
        RemoveOwnIconFiles(folder);
        return true;
    }

    private static void Refresh(string folder, bool iconCache = true)
    {
        SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, folder, IntPtr.Zero);
        SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, Path.GetDirectoryName(folder), IntPtr.Zero);
        if (iconCache) SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, null, IntPtr.Zero);
    }
}
