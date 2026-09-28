using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Dosyaların, klasörlerin ve kabuk nesnelerinin ("::{CLSID}": Bu Bilgisayar, Geri Dönüşüm Kutusu…) Windows gezginindeki
/// simgeleri. Simgeler ekranda çizilecekleri piksel boyutunda istenir (1:1 çizilsin, bulanıklaşmasın; bkz.
/// <see cref="IconSizing"/>). Önbellek sınırlıdır: en son kullanılan simgeler tutulur (tür bazında; program, kısayol,
/// klasör ve önizlemeler dosya bazında), uzun süre açık kalan uygulamada bellek sınırsız büyümez.
/// </summary>
public static class ShellIcons
{
    private const int CacheEntries = 1000;
    private const long CacheBytes = 48L << 20;

    private static readonly LruCache<string, ImageSource?> Cache = new(CacheEntries, CacheBytes, CostOf, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> PerFile = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".lnk", ".ico", ".url", ".appref-ms" };

    private static long CostOf(ImageSource? icon) => icon is BitmapSource b ? (long)b.PixelWidth * b.PixelHeight * 4 : 64;

    /// <summary>
    /// Dosyanın/klasörün ya da kabuk nesnesinin simgesi. <paramref name="pixels"/> verilirse o boyutta keskin simge istenir;
    /// alınamazsa sistemin simge listesinden en yakın boyuta düşer. Kabuk çağrısı yapar: arayüz iş parçacığında değil,
    /// <see cref="Request"/> ile arka planda kullanılmalı.
    /// </summary>
    public static ImageSource? For(string path, int pixels = 0, bool preview = false)
    {
        // Arka plandaki yükleyiciden çağrılır (klasör mü diye diske burada bakılır, arayüzde değil).
        var isDir = !TileItem.IsShellObject(path) && Directory.Exists(path);
        return Cache.GetOrAdd(Key(path, pixels, preview, isDir), _ => Load(path, pixels, preview, isDir));
    }

    private static ImageSource? Load(string path, int pixels, bool preview, bool isDir)
    {
        if (TileItem.IsShellObject(path))
            return (pixels > 0 ? LoadSized(path, pixels, preview: false) : null) ?? LoadFromPidl(path, pixels);
        return (pixels > 0 ? LoadSized(path, pixels, preview) : null)
            ?? LoadFromInfo(path, isDir, usePathOnly: !isDir && !PerFile.Contains(Path.GetExtension(path)), pixels);
    }

    // Klasörler kendi (özel olabilecek) simgeleriyle, program/kısayollar ve önizlemeler dosya bazında,
    // diğerleri uzantı bazında önbelleğe alınır.
    private static string Key(string path, int pixels, bool preview, bool isDir)
    {
        if (TileItem.IsShellObject(path)) return "kabuk|" + path + "|" + pixels;
        var ext = Path.GetExtension(path);
        if (preview) return "önizleme|" + path + "|" + pixels;
        return (isDir || PerFile.Contains(ext) ? path : ext) + "|" + pixels;
    }

    /// <summary>
    /// Önbellekte varsa simgeyi verir. Diske dokunmaz: klasör mü olduğunu çağıran bilir (anlık görüntüden; kabuk nesnesi
    /// için false).
    /// </summary>
    public static bool TryCached(string path, int pixels, bool preview, bool isDirectory, out ImageSource? icon) =>
        Cache.TryGetValue(Key(path, pixels, preview, isDirectory), out icon);

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
        try { return CanPreview(path, File.GetAttributes(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Öznitelikleri zaten bilinen dosya için (diske dokunmaz).</summary>
    public static bool CanPreview(string path, FileAttributes attributes)
    {
        const FileAttributes Cloud = (FileAttributes)0x1000 | (FileAttributes)0x40000 | (FileAttributes)0x400000;
        return Previewable.Contains(Path.GetExtension(path)) && (attributes & (Cloud | FileAttributes.Directory)) == 0;
    }

    private static readonly BlockingCollection<(Func<ImageSource?> Load, Action<ImageSource?> Done, Dispatcher Dispatcher)> Queue = new();
    private static Thread? _worker;

    /// <summary>
    /// Simgeyi arka plandaki STA iş parçacığında yükler, sonucu çağıranın iş parçacığında bildirir.
    /// Kabuk simgesi çıkarmak (özellikle ağ/OneDrive yollarında) yavaş olabilir; arayüz beklememeli.
    /// Art arda biten simgeler tek seferde bildirilir: 300 öğelik bölme 300 ayrı çizim yerine birkaç çizimle dolar.
    /// </summary>
    public static void Request(string path, int pixels, bool preview, Action<ImageSource?> done)
    {
        EnsureWorker();
        // "::{CLSID}" kabuk nesneleri (Geri Dönüşüm Kutusu…) de burada, istenen boyutta yüklenir: arayüzde yavaş sürücüyü beklemesin.
        Queue.Add((() => For(path, pixels, preview), done, Dispatcher.CurrentDispatcher));
    }

    // --- Kullanıcının seçtiği simgeler (kısayol kutusu öğesi: "res:" ve "img:", bkz. Core.IconRef) ---

    /// <summary>
    /// .ico/.exe/.dll içindeki bir simgeyi (Windows'un "Simge Değiştir" penceresinde seçilen) istenen piksel boyutunda arka
    /// planda yükler. Yol ortam değişkeni taşıyabilir. Yüklenemezse null bildirilir (çağıran dosyanın kendi simgesine döner).
    /// </summary>
    public static void RequestResource(string file, int index, int pixels, Action<ImageSource?> done)
    {
        EnsureWorker();
        Queue.Add((() => Cache.GetOrAdd(ResourceKey(file, index, pixels),
            _ => LoadResource(Environment.ExpandEnvironmentVariables(file), index, pixels)), done, Dispatcher.CurrentDispatcher));
    }

    /// <summary>Kullanıcının seçtiği resmi (veri klasöründeki kopyası) istenen boyuta yakın çözünürlükte arka planda yükler.</summary>
    public static void RequestImage(string file, int pixels, Action<ImageSource?> done)
    {
        EnsureWorker();
        Queue.Add((() => Cache.GetOrAdd(ImageKey(file, pixels), _ => LoadImage(file, pixels)), done, Dispatcher.CurrentDispatcher));
    }

    /// <summary>Önbellekteki kaynak simgesi (diske dokunmaz).</summary>
    public static bool TryCachedResource(string file, int index, int pixels, out ImageSource? icon) =>
        Cache.TryGetValue(ResourceKey(file, index, pixels), out icon);

    /// <summary>Önbellekteki resim (diske dokunmaz).</summary>
    public static bool TryCachedImage(string file, int pixels, out ImageSource? icon) =>
        Cache.TryGetValue(ImageKey(file, pixels), out icon);

    private static string ResourceKey(string file, int index, int pixels) => $"kaynak|{file}|{index}|{pixels}";

    private static string ImageKey(string file, int pixels) => $"resim|{file}|{pixels}";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIcon(string iconFile, int index, uint flags, out IntPtr large, out IntPtr small, uint iconSize);

    private static ImageSource? LoadResource(string file, int index, int pixels)
    {
        var size = Math.Clamp(pixels, 16, 256);
        // Düşük sözcük büyük simgenin, yüksek sözcük küçük simgenin boyutu.
        if (SHDefExtractIcon(file, index, 0, out var large, out var small, (uint)(size | (16 << 16))) != 0) return null;
        if (small != IntPtr.Zero) NativeMethods.DestroyIcon(small);
        return FromIcon(large);
    }

    private static ImageSource? LoadImage(string file, int pixels)
    {
        try
        {
            if (!File.Exists(file)) return null;
            if (string.Equals(Path.GetExtension(file), ".ico", StringComparison.OrdinalIgnoreCase))
            {
                // .ico: istenen boyuta en yakın (tercihen büyük) kare; WPF Image kendi başına ilk kareyi gösterirdi.
                using var stream = File.OpenRead(file);
                var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames
                    .OrderBy(f => f.PixelWidth >= pixels ? f.PixelWidth - pixels : 10000 + pixels - f.PixelWidth).FirstOrDefault();
                frame?.Freeze();
                return frame;
            }
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.UriSource = new Uri(file, UriKind.Absolute);
            // Büyük fotoğraf simge boyutunda çözülür (bellek ve süre).
            bitmap.DecodePixelWidth = Math.Clamp(pixels * 2, 16, 512);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or
                                       InvalidOperationException or COMException)
        {
            DebugLog.Write($"simge resmi yüklenemedi {file}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Bir toplu bildirim en çok bu kadar bekletilir (sıra boşalınca hemen gider).</summary>
    private const int BatchMilliseconds = 80;

    private static void EnsureWorker()
    {
        if (_worker is not null) return;
        lock (Queue)
        {
            if (_worker is not null) return;
            var thread = new Thread(() =>
            {
                var batch = new List<(Action<ImageSource?> Done, ImageSource? Icon, Dispatcher Dispatcher)>();
                var batchStarted = 0L;
                foreach (var (load, done, dispatcher) in Queue.GetConsumingEnumerable())
                {
                    ImageSource? icon = null;
                    try { icon = load(); }
                    catch (Exception ex) { DebugLog.Write($"simge yüklenemedi: {ex.Message}"); }
                    if (batch.Count == 0) batchStarted = Environment.TickCount64;
                    batch.Add((done, icon, dispatcher));
                    if (Queue.Count == 0 || Environment.TickCount64 - batchStarted >= BatchMilliseconds) Deliver(batch);
                }
            })
            { IsBackground = true, Name = "Duzenleme simge yükleyici", Priority = ThreadPriority.BelowNormal };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            _worker = thread;
        }
    }

    /// <summary>Biten simgeleri, isteyen her arayüz iş parçacığına tek bir işlemle bildirir (tek yerleşim, tek çizim).</summary>
    private static void Deliver(List<(Action<ImageSource?> Done, ImageSource? Icon, Dispatcher Dispatcher)> batch)
    {
        foreach (var group in batch.GroupBy(b => b.Dispatcher))
        {
            var items = group.ToArray();
            group.Key.BeginInvoke(() =>
            {
                foreach (var (done, icon, _) in items)
                {
                    try { done(icon); }
                    catch (Exception ex) { DebugLog.Write($"simge bildirimi: {ex}"); }
                }
            }, DispatcherPriority.Background);
        }
        batch.Clear();
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

    /// <summary>
    /// İstenen boyutta simge ya da önizleme (IShellItemImageFactory). Kabuk en yakın simge karesini tam bu boyuta kendisi
    /// getirir. "::{CLSID}" ayrıştırma adları (Bu Bilgisayar…) da çalışır.
    /// </summary>
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

    /// <summary>"::{CLSID}" gibi kabuk nesnelerinin (Bu Bilgisayar, Geri Dönüşüm Kutusu…) simgesi. Kabuk çağrısı yapar.</summary>
    public static ImageSource? ForShellObject(string parsingName, int pixels = 0) => For(parsingName, pixels);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW")]
    private static extern IntPtr SHGetFileInfoPidl(IntPtr pidl, uint attrs, ref NativeMethods.SHFILEINFO info, uint size, uint flags);

    private const uint SHGFI_PIDL = 0x000000008, SHGFI_SYSICONINDEX = 0x000004000;

    /// <summary>Yedek yol: kabuk nesnesinin simgesi SHGetFileInfo ile (büyük boyutta sistemin 48 piksellik listesinden).</summary>
    private static ImageSource? LoadFromPidl(string parsingName, int pixels)
    {
        if (SHParseDisplayName(parsingName, IntPtr.Zero, out var pidl, 0, out _) != 0 || pidl == IntPtr.Zero) return null;
        try
        {
            var info = new NativeMethods.SHFILEINFO();
            if (pixels > 32 && SHGetFileInfoPidl(pidl, 0, ref info, (uint)Marshal.SizeOf(info), SHGFI_SYSICONINDEX | SHGFI_PIDL) != IntPtr.Zero &&
                FromSystemImageList(info.iIcon) is { } large)
                return large;
            SHGetFileInfoPidl(pidl, 0, ref info, (uint)Marshal.SizeOf(info), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON | SHGFI_PIDL);
            return FromIcon(info.hIcon);
        }
        finally { Marshal.FreeCoTaskMem(pidl); }
    }

    /// <summary>Klasör simgesi değişince önbellekteki eski görüntüyü bırak.</summary>
    public static void Forget(string path) =>
        Cache.RemoveWhere(k => k.StartsWith(path + "|", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Yedek yol (boyutlu simge alınamadıysa): SHGetFileInfo. 32 pikselden büyük istenince sistemin 48 piksellik simge
    /// listesinden alınır; sistemin 32 piksellik simgesini büyütmek bulanık görünür.
    /// </summary>
    private static ImageSource? LoadFromInfo(string path, bool isDir, bool usePathOnly, int pixels)
    {
        var info = new NativeMethods.SHFILEINFO();
        var pathFlags = usePathOnly ? NativeMethods.SHGFI_USEFILEATTRIBUTES : 0;
        var attrs = isDir ? 0x10u : NativeMethods.FILE_ATTRIBUTE_NORMAL;

        if (pixels > 32 && NativeMethods.SHGetFileInfo(path, attrs, ref info, (uint)Marshal.SizeOf(info), SHGFI_SYSICONINDEX | pathFlags) != IntPtr.Zero &&
            FromSystemImageList(info.iIcon) is { } large)
            return large;
        NativeMethods.SHGetFileInfo(path, attrs, ref info, (uint)Marshal.SizeOf(info), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON | pathFlags);
        return FromIcon(info.hIcon);
    }

    /// <summary>HICON'u saydamlığıyla kopyalar ve bırakır.</summary>
    private static ImageSource? FromIcon(IntPtr icon)
    {
        if (icon == IntPtr.Zero) return null;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally { NativeMethods.DestroyIcon(icon); }
    }

    // Sistem simge listesi (IImageList). Yalnızca GetIcon kullanılır; öncekiler sanal tablo sırası için bildirilir.
    [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr image, IntPtr mask, out int index);
        [PreserveSig] int ReplaceIcon(int i, IntPtr icon, out int index);
        [PreserveSig] int SetOverlayImage(int image, int overlay);
        [PreserveSig] int Replace(int i, IntPtr image, IntPtr mask);
        [PreserveSig] int AddMasked(IntPtr image, int maskColor, out int index);
        [PreserveSig] int Draw(IntPtr drawParams);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out IntPtr icon);
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetImageList(int list, [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IImageList? imageList);

    /// <summary>Sistemin 48 piksellik (SHIL_EXTRALARGE) simge listesinden bir simge; alınamazsa null.</summary>
    private static ImageSource? FromSystemImageList(int index)
    {
        const int SHIL_EXTRALARGE = 2, ILD_TRANSPARENT = 1;
        if (index < 0) return null;
        try
        {
            if (SHGetImageList(SHIL_EXTRALARGE, typeof(IImageList).GUID, out var list) != 0 || list is null) return null;
            try { return list.GetIcon(index, ILD_TRANSPARENT, out var icon) == 0 ? FromIcon(icon) : null; }
            finally { Marshal.ReleaseComObject(list); }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or EntryPointNotFoundException)
        {
            return null;
        }
    }
}
