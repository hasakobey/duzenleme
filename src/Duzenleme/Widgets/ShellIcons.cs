using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Duzenleme.Widgets;

/// <summary>Dosyaların Windows gezginindeki simgelerini döner (uzantı bazında önbellekli).</summary>
public static class ShellIcons
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PerFile = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".lnk", ".ico", ".url", ".appref-ms" };

    public static ImageSource? For(string path)
    {
        var ext = Path.GetExtension(path);
        var isDir = Directory.Exists(path);
        // Klasörler kendi (özel olabilecek) simgeleriyle, yol bazında önbelleğe alınır.
        var key = isDir || PerFile.Contains(ext) ? path : ext;
        return Cache.GetOrAdd(key, _ => Load(path, isDir, usePathOnly: !PerFile.Contains(ext) && !isDir));
    }

    /// <summary>"::{CLSID}" gibi kabuk nesnelerinin (Bu Bilgisayar, Geri Dönüşüm Kutusu…) simgesi.</summary>
    public static ImageSource? ForShellObject(string parsingName) =>
        Cache.GetOrAdd("shell|" + parsingName, _ => LoadFromPidl(parsingName));

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW")]
    private static extern IntPtr SHGetFileInfoPidl(IntPtr pidl, uint attrs, ref NativeMethods.SHFILEINFO info, uint size, uint flags);

    private static ImageSource? LoadFromPidl(string parsingName)
    {
        const uint SHGFI_PIDL = 0x000000008;
        if (SHParseDisplayName(parsingName, IntPtr.Zero, out var pidl, 0, out _) != 0 || pidl == IntPtr.Zero) return null;
        try
        {
            var info = new NativeMethods.SHFILEINFO();
            SHGetFileInfoPidl(pidl, 0, ref info, (uint)Marshal.SizeOf(info), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON | SHGFI_PIDL);
            if (info.hIcon == IntPtr.Zero) return null;
            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally { NativeMethods.DestroyIcon(info.hIcon); }
        }
        finally { Marshal.FreeCoTaskMem(pidl); }
    }

    /// <summary>Klasör simgesi değişince önbellekteki eski görüntüyü bırak.</summary>
    public static void Forget(string path) => Cache.TryRemove(path, out _);

    private static ImageSource? Load(string path, bool isDir, bool usePathOnly)
    {
        var info = new NativeMethods.SHFILEINFO();
        var flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;
        if (usePathOnly) flags |= NativeMethods.SHGFI_USEFILEATTRIBUTES;
        var attrs = isDir ? 0x10u : NativeMethods.FILE_ATTRIBUTE_NORMAL;

        NativeMethods.SHGetFileInfo(path, attrs, ref info, (uint)Marshal.SizeOf(info), flags);
        if (info.hIcon == IntPtr.Zero) return null;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DestroyIcon(info.hIcon);
        }
    }
}
