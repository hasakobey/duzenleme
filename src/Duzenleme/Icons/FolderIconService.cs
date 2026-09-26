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
    private const string IconPrefix = ".duzenleme-";
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

    public static bool HasCustomIcon(string folder)
    {
        var ini = Path.Combine(folder, "desktop.ini");
        try
        {
            return File.Exists(ini) && File.ReadAllText(ini).Contains("Icon", StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
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

        var settings = new SHFOLDERCUSTOMSETTINGS
        {
            dwSize = (uint)Marshal.SizeOf<SHFOLDERCUSTOMSETTINGS>(),
            dwMask = FCSM_ICONFILE,
            pszIconFile = iconPath,
            cchIconFile = 0,
            iIconIndex = 0,
        };
        var hr = SHGetSetFolderCustomSettings(ref settings, folder, FCS_FORCEWRITE);
        if (hr != 0) Marshal.ThrowExceptionForHR(hr);
        Refresh(folder);
    }

    /// <summary>Klasörü varsayılan Windows simgesine döndürür.</summary>
    public static void Reset(string folder)
    {
        var ini = Path.Combine(folder, "desktop.ini");
        if (File.Exists(ini))
        {
            var attrs = File.GetAttributes(ini);
            File.SetAttributes(ini, FileAttributes.Normal);
            // Yalnızca simge satırlarını sil; desktop.ini'deki diğer ayarlar (ör. yerelleştirilmiş ad) kalsın.
            var lines = File.ReadAllLines(ini)
                .Where(l => !l.TrimStart().StartsWith("IconResource", StringComparison.OrdinalIgnoreCase)
                            && !l.TrimStart().StartsWith("IconFile", StringComparison.OrdinalIgnoreCase)
                            && !l.TrimStart().StartsWith("IconIndex", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var meaningful = lines.Any(l => l.Trim().Length > 0 && !l.Trim().StartsWith('[') && !l.Trim().StartsWith(';'));
            if (meaningful)
            {
                File.WriteAllLines(ini, lines, Encoding.Unicode);
                File.SetAttributes(ini, attrs);
            }
            else
            {
                File.Delete(ini);
            }
        }
        RemoveOwnIconFiles(folder);
        Refresh(folder);
    }

    private static void RemoveOwnIconFiles(string folder)
    {
        foreach (var old in Directory.EnumerateFiles(folder, IconPrefix + "*.ico"))
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

    private static void Refresh(string folder)
    {
        SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, folder, IntPtr.Zero);
        SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, Path.GetDirectoryName(folder), IntPtr.Zero);
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, null, IntPtr.Zero);
    }
}
