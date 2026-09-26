using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Duzenleme.Widgets;

/// <summary>Dosyaların Windows gezginindeki simgelerini döner (uzantı bazında önbellekli).</summary>
public static class ShellIcons
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PerFile = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".lnk", ".ico", ".url", ".appref-ms" };

    /// <summary>
    /// Dosyanın/klasörün simgesi. <paramref name="pixels"/> verilirse o boyutta keskin simge istenir
    /// (bölmede büyük simgeler bulanık görünmesin); alınamazsa sistemin 32 piksellik simgesine düşer.
    /// </summary>
    public static ImageSource? For(string path, int pixels = 0, bool preview = false) =>
        Cache.GetOrAdd(Key(path, pixels, preview, out var isDir), _ =>
            (pixels > 0 ? LoadSized(path, pixels, preview) : null) ?? Load(path, isDir, usePathOnly: !isDir && !PerFile.Contains(Path.GetExtension(path))));

    // Klasörler kendi (özel olabilecek) simgeleriyle, program/kısayollar ve önizlemeler dosya bazında,
    // diğerleri uzantı bazında önbelleğe alınır.
    private static string Key(string path, int pixels, bool preview, out bool isDir)
    {
        var ext = Path.GetExtension(path);
        isDir = Directory.Exists(path);
        if (preview) return "önizleme|" + path + "|" + pixels;
        return (isDir || PerFile.Contains(ext) ? path : ext) + "|" + pixels;
    }

    public static bool TryCached(string path, int pixels, bool preview, out ImageSource? icon) =>
        Cache.TryGetValue(Key(path, pixels, preview, out _), out icon);

    private static readonly HashSet<string> Previewable = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic", ".tif", ".tiff",
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".pdf",
    };

    /// <summary>
    /// Önizlemesi çıkarılabilecek yerel bir dosya mı? Bulut yer tutucuları (OneDrive "isteğe bağlı" dosyalar)
    /// önizleme için indirilmesin diye hariç tutulur.
    /// </summary>
    public static bool CanPreview(string path)
    {
        if (!Previewable.Contains(Path.GetExtension(path))) return false;
        try
        {
            const FileAttributes Cloud = (FileAttributes)0x1000 | (FileAttributes)0x40000 | (FileAttributes)0x400000;
            return (File.GetAttributes(path) & Cloud) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static readonly BlockingCollection<(string Path, int Pixels, bool Preview, Action<ImageSource?> Done, Dispatcher Dispatcher)> Queue = new();
    private static Thread? _worker;

    /// <summary>
    /// Simgeyi arka plandaki STA iş parçacığında yükler, sonucu çağıranın iş parçacığında bildirir.
    /// Kabuk simgesi çıkarmak (özellikle ağ/OneDrive yollarında) yavaş olabilir; arayüz beklememeli.
    /// </summary>
    public static void Request(string path, int pixels, bool preview, Action<ImageSource?> done)
    {
        EnsureWorker();
        Queue.Add((path, pixels, preview, done, Dispatcher.CurrentDispatcher));
    }

    private static void EnsureWorker()
    {
        if (_worker is not null) return;
        lock (Queue)
        {
            if (_worker is not null) return;
            var thread = new Thread(() =>
            {
                foreach (var (path, pixels, preview, done, dispatcher) in Queue.GetConsumingEnumerable())
                {
                    ImageSource? icon = null;
                    try { icon = For(path, pixels, preview); }
                    catch (Exception ex) { DebugLog.Write($"simge yüklenemedi {path}: {ex.Message}"); }
                    dispatcher.BeginInvoke(done, DispatcherPriority.Background, icon);
                }
            })
            { IsBackground = true, Name = "Duzenleme simge yükleyici", Priority = ThreadPriority.BelowNormal };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            _worker = thread;
        }
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(NativeSize size, int flags, out IntPtr bitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize { public int Width, Height; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DIBSECTION
    {
        public BITMAP dsBm;
        public BITMAPINFOHEADER dsBmih;
        public uint dsBitfield0, dsBitfield1, dsBitfield2;
        public IntPtr dshSection;
        public uint dsOffset;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? factory);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr handle, int size, out DIBSECTION section);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    private static ImageSource? LoadSized(string path, int pixels, bool preview)
    {
        const int SIIGBF_RESIZETOFIT = 0x0, SIIGBF_ICONONLY = 0x4;
        try
        {
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var factory) != 0 || factory is null) return null;
            try
            {
                var flags = preview ? SIIGBF_RESIZETOFIT : SIIGBF_ICONONLY;
                if (factory.GetImage(new NativeSize { Width = pixels, Height = pixels }, flags, out var bitmap) != 0 || bitmap == IntPtr.Zero) return null;
                try { return FromDib(bitmap); }
                finally { DeleteObject(bitmap); }
            }
            finally { Marshal.ReleaseComObject(factory); }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Kabuğun verdiği 32 bitlik DIB'i saydamlığıyla kopyalar (CreateBitmapSourceFromHBitmap alfa kanalını kaybeder).
    /// </summary>
    private static BitmapSource? FromDib(IntPtr bitmap)
    {
        if (GetObject(bitmap, Marshal.SizeOf<DIBSECTION>(), out var dib) == 0) return null;
        var bm = dib.dsBm;
        if (bm.bmBitsPixel != 32 || bm.bmBits == IntPtr.Zero || bm.bmWidth <= 0 || bm.bmHeight <= 0) return null;

        var height = bm.bmHeight;
        var stride = bm.bmWidthBytes;
        var pixels = new byte[stride * height];
        Marshal.Copy(bm.bmBits, pixels, 0, pixels.Length);

        // Pozitif yükseklik: satırlar alttan üste.
        if (dib.dsBmih.biHeight > 0)
        {
            var row = new byte[stride];
            for (var top = 0; top < height / 2; top++)
            {
                var bottom = height - 1 - top;
                Buffer.BlockCopy(pixels, top * stride, row, 0, stride);
                Buffer.BlockCopy(pixels, bottom * stride, pixels, top * stride, stride);
                Buffer.BlockCopy(row, 0, pixels, bottom * stride, stride);
            }
        }

        // Eski (alfasız) simgelerde alfa hep 0 gelir; o zaman görüntüyü opak say.
        var hasAlpha = false;
        for (var i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 0) { hasAlpha = true; break; }

        var source = BitmapSource.Create(bm.bmWidth, height, 96, 96, hasAlpha ? PixelFormats.Pbgra32 : PixelFormats.Bgr32, null, pixels, stride);
        source.Freeze();
        return source;
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
    public static void Forget(string path)
    {
        foreach (var key in Cache.Keys.Where(k => k.StartsWith(path + "|", StringComparison.OrdinalIgnoreCase)).ToList())
            Cache.TryRemove(key, out _);
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
