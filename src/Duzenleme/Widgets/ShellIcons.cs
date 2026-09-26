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
        var key = isDir ? "<dir>" : PerFile.Contains(ext) ? path : ext;
        return Cache.GetOrAdd(key, _ => Load(path, isDir, usePathOnly: !PerFile.Contains(ext) && !isDir));
    }

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
