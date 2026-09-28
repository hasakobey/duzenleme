using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Bölme ve kısayol kutusundaki bir öğe; simge boyutu ve görünüm moduna göre yerleşimini taşır.
/// Öğe diske bakmaz: bölmede bilgiler masaüstü anlık görüntüsünden gelir, kısayol kutusunda yol arka planda denetlenir
/// (<see cref="PathProbe"/>). Simge önbellekte yoksa arka planda yüklenir ve gelince görünür (arayüz donmasın).
/// </summary>
public sealed class TileItem : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private bool _missing;

    public required string Name { get; init; }
    public required string Path { get; init; }

    /// <summary>Klasör mü? (Bilinmiyorsa false; kısayol kutusunda denetim bitince güncellenir.)</summary>
    public bool IsDirectory { get; private set; }

    /// <summary>Yol bulunamadı ya da (ağ yolu) şu an ulaşılamıyor: öğe soluk görünür ve tek tıkla açılmaz.</summary>
    public bool Missing
    {
        get => _missing;
        private set
        {
            if (_missing == value) return;
            _missing = value;
            Raise(nameof(Missing));
            Raise(nameof(Dim));
            Raise(nameof(Tooltip));
        }
    }

    public ImageSource? Icon
    {
        get => _icon;
        private set
        {
            _icon = value;
            Raise(nameof(Icon));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public double IconPx { get; init; }
    public double TileWidth { get; init; }
    public Orientation Orientation { get; init; }
    public HorizontalAlignment IconAlign { get; init; }
    public TextAlignment TextAlign { get; init; }
    public TextWrapping Wrap { get; init; }
    public Thickness TextMargin { get; init; }
    public double TextMaxHeight { get; init; }
    public double LabelFont { get; init; }
    public Visibility LabelVisibility { get; init; }
    public Thickness Pad { get; init; }
    public double Dim => Missing ? 0.45 : 1;
    public string Tooltip => Missing ? $"{Name}\n{Path}\n(bulunamadı)" : $"{Name}\n{Path}";

    /// <summary>
    /// 32-bit sürüm 64-bit Windows'ta çalışırken System32 yolları SysWOW64'e yönlenir; kısayol kutusundaki
    /// 64-bit sistem araçları "bulunamadı" görünmesin diye gerçek System32'ye (Sysnative) çevrilir.
    /// </summary>
    public static string NativePath(string path)
    {
        if (Environment.Is64BitProcess || !Environment.Is64BitOperatingSystem) return path;
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System).TrimEnd('\\') + "\\";
        return path.StartsWith(system32, StringComparison.OrdinalIgnoreCase)
            ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative", path[system32.Length..])
            : path;
    }

    public static double IconPixels(IconSize size) => size switch
    {
        IconSize.Small => 24, IconSize.Large => 48, IconSize.ExtraLarge => 64, _ => 36,
    };

    /// <summary>
    /// Durumu zaten bilinen (anlık görüntüden gelen, var olan) öğe: diske hiç dokunulmadan kurulur, simgesi istenir.
    /// <paramref name="pixelsPerDip"/> kutucuğu gösterecek görünümün ekran ölçeğidir (VisualTreeHelper.GetDpi(görünüm)
    /// .PixelsPerDip); verilmezse sistemin ölçeği sayılır. Görünüm, ekran ya da widget ölçeği değişince
    /// <see cref="UpdateIconSize"/> çağırmalıdır.
    /// </summary>
    public static TileItem Create(string path, WidgetConfig config, bool isDirectory, FileAttributes attributes, string? name = null,
        double pixelsPerDip = 0)
    {
        var item = Layout(config, path, name ?? DisplayName(path), missing: false, pixelsPerDip);
        item.ApplyState(new PathState(true, isDirectory, attributes), config);
        return item;
    }

    /// <summary>
    /// Kısayol kutusundaki gibi durumu bilinmeyen yol: öğe hemen gösterilir, var mı/klasör mü arka planda (ağ yollarında
    /// süre sınırıyla) öğrenilir; sonuç gelince soluklaşır ya da simgesi yüklenir. Arayüz iş parçacığı diske bakmaz.
    /// </summary>
    public static TileItem CreateUnchecked(string path, WidgetConfig config, string? name = null, double pixelsPerDip = 0)
    {
        // Bu Bilgisayar, Geri Dönüşüm Kutusu gibi kabuk nesneleri diskte yoktur: "bulunamadı" sayılmaz, kabuk simgesiyle gelir.
        if (IsShellObject(path)) return CreateShell(path, name ?? ShellName(path), config, pixelsPerDip);
        var item = Layout(config, path, name ?? DisplayName(path), missing: false, pixelsPerDip);
        var native = NativePath(path);
        if (PathProbe.Shared.TryGetCached(native, out var known))
        {
            item.ApplyState(known, config);
            return item;
        }
        var dispatcher = Application.Current?.Dispatcher;
        PathProbe.Shared.CheckAsync(native).ContinueWith(t =>
        {
            var state = t.IsCompletedSuccessfully ? t.Result : null;
            dispatcher?.BeginInvoke(() => item.ApplyState(state, config), System.Windows.Threading.DispatcherPriority.Background);
        }, TaskScheduler.Default);
        return item;
    }

    // Simgesi istenen yol (32-bit'te Sysnative'e çevrilmiş ya da "::{CLSID}"), önizleme mi, en son istenen piksel boyutu;
    // görünümün son bildirdiği ekran ve widget ölçeği (durum arka planda sonradan öğrenilince simge bu boyutta istenir).
    private string? _iconPath;
    private bool _preview;
    private int _pixels;
    private double _pixelsPerDip;
    private double _widgetScale = 1;

    /// <summary>Yolun durumu öğrenildi (null: ulaşılamıyor): soluk gösterir ya da simgesini ister.</summary>
    private void ApplyState(PathState? state, WidgetConfig config)
    {
        if (state is not { Exists: true } known)
        {
            Missing = true;
            return;
        }
        Missing = false;
        IsDirectory = known.IsDirectory;
        _iconPath = NativePath(Path);
        _preview = config.ShowPreviews && !known.IsDirectory && ShellIcons.CanPreview(_iconPath, known.Attributes);
        _pixels = 0;
        UpdateIconSize(_pixelsPerDip, _widgetScale);
    }

    /// <summary>
    /// Simgeyi ekranda çizileceği gerçek piksel boyutunda (simge boyutu × ekran ölçeği × widget ölçeği) ister ve 1:1
    /// çizdirir; iki kat büyük istenip küçültülen ya da küçük istenip büyütülen simge %125/%150'de bulanık görünürdü.
    /// Boyut değişmediyse bir şey yapmaz. Yeni boyut gelene dek eski simge görünür. Diske dokunmaz; durumu henüz
    /// bilinmeyen öğede ölçek saklanır, simge durum gelince istenir.
    /// </summary>
    public void UpdateIconSize(double pixelsPerDip, double widgetScale)
    {
        _pixelsPerDip = pixelsPerDip;
        _widgetScale = widgetScale;
        if (_iconPath is not { } path) return;
        var pixels = IconSizing.DevicePixels(IconPx, pixelsPerDip > 0 ? pixelsPerDip : NativeMethods.SystemPixelsPerDip, widgetScale);
        if (pixels == _pixels) return;
        _pixels = pixels;
        var preview = _preview;
        var isDirectory = IsDirectory;
        if (ShellIcons.TryCached(path, pixels, preview, isDirectory, out var cached))
        {
            Icon = cached;
            return;
        }
        // Önizleme gelene kadar (varsa) türün simgesi görünsün.
        if (preview && Icon is null && ShellIcons.TryCached(path, pixels, false, isDirectory, out var typeIcon)) Icon = typeIcon;
        // Arada başka bir boyut istendiyse (ekran değişti) geç gelen eski boyut yenisinin üstüne yazılmaz.
        ShellIcons.Request(path, pixels, preview, icon => { if (_pixels == pixels) Icon = icon ?? Icon; });
    }

    /// <summary>Kutucuğun yerleşimi (simge boyutu, yazı, aralık, liste/ızgara); simgesi yok.</summary>
    private static TileItem Layout(WidgetConfig config, string path, string name, bool missing, double pixelsPerDip)
    {
        var list = config.View == ItemView.List;
        var px = list ? Math.Min(IconPixels(config.IconSize), 32) : IconPixels(config.IconSize);
        var font = config.LabelSize switch { LabelSize.Small => 10.5, LabelSize.Large => 13, _ => 11.5 };
        var labels = !config.HideLabels;
        return new TileItem
        {
            Name = name,
            Path = path,
            Missing = missing,
            _pixelsPerDip = pixelsPerDip,
            _widgetScale = config.Scale,
            IconPx = px,
            TileWidth = list ? double.NaN : labels ? px + Math.Max(40, font * 4) : px + 4,
            Orientation = list ? Orientation.Horizontal : Orientation.Vertical,
            IconAlign = list ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            TextAlign = list ? TextAlignment.Left : TextAlignment.Center,
            Wrap = list ? TextWrapping.NoWrap : TextWrapping.Wrap,
            TextMargin = list ? new Thickness(10, 0, 0, 0) : new Thickness(0, 5, 0, 0),
            TextMaxHeight = list ? font * 1.8 : font * 2.8,
            LabelFont = font,
            LabelVisibility = labels ? Visibility.Visible : Visibility.Collapsed,
            Pad = config.Spacing switch
            {
                TileSpacing.Compact => list ? new Thickness(4, 2, 4, 2) : new Thickness(3, 4, 3, 3),
                TileSpacing.Wide => list ? new Thickness(10, 7, 10, 7) : new Thickness(10, 12, 10, 10),
                _ => list ? new Thickness(6, 4, 6, 4) : new Thickness(6, 7, 6, 6),
            },
        };
    }

    /// <summary>
    /// Simge paneli: ızgara ya da liste; ızgarada satırlar sola, ortaya ya da sağa yaslanır. Liste sanallaştırılır (yalnızca
    /// görünen satırların kutusu kurulur; çok öğeli klasör bölmesi hızlı açılır ve kayar).
    /// </summary>
    public static ItemsPanelTemplate Panel(WidgetConfig config)
    {
        var list = config.View == ItemView.List;
        var panel = new FrameworkElementFactory(list ? typeof(VirtualizingStackPanel) : typeof(WrapPanel));
        if (!list && config.Align == TileAlign.Right)
            panel.SetValue(FrameworkElement.FlowDirectionProperty, FlowDirection.RightToLeft);
        else if (!list && config.Align == TileAlign.Center)
            panel.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        return new ItemsPanelTemplate(panel);
    }

    /// <summary>Paneli belirleyen ayarlar (değişmedikçe panel yeniden kurulmaz: bütün kutucuklar yeniden üretilirdi).</summary>
    public static string PanelKey(WidgetConfig config) => $"{config.View}|{config.Align}";

    /// <summary>
    /// Bu Bilgisayar, Geri Dönüşüm Kutusu gibi kabuk nesnesi ("::{CLSID}") kutucuğu. Simgesi de dosyalarınki gibi arka
    /// planda ve gerçek piksel boyutunda yüklenir (kabuk çağrısı arayüzü bekletmez).
    /// </summary>
    public static TileItem CreateShell(Desktop.SystemIcon icon, WidgetConfig config, double pixelsPerDip = 0) =>
        CreateShell("::" + icon.Clsid, icon.Name, config, pixelsPerDip);

    /// <summary>Kabuk nesnesi kutucuğu, ayrıştırma adıyla ("::{CLSID}"; kısayol kutusundaki Geri Dönüşüm Kutusu gibi).</summary>
    public static TileItem CreateShell(string parsingName, string name, WidgetConfig config, double pixelsPerDip = 0)
    {
        var item = Layout(config, parsingName, name, missing: false, pixelsPerDip);
        item._iconPath = item.Path;
        item.UpdateIconSize(pixelsPerDip, config.Scale);
        return item;
    }

    /// <summary>Kabuk nesnesinin görünen adı (bilinen masaüstü simgelerinden); bilinmiyorsa ayrıştırma adı.</summary>
    public static string ShellName(string parsingName) =>
        Desktop.DesktopSystemIcons.All.FirstOrDefault(i => string.Equals("::" + i.Clsid, parsingName, StringComparison.OrdinalIgnoreCase))?.Name
        ?? parsingName;

    /// <summary>Simgeyi yeniden ister (önbellekten düşürüldükten sonra; ör. Geri Dönüşüm Kutusu doldu/boşaldı).</summary>
    public void ReloadIcon()
    {
        _pixels = 0;
        UpdateIconSize(_pixelsPerDip, _widgetScale);
    }

    public static bool IsShellObject(string path) => path.StartsWith("::", StringComparison.Ordinal);

    /// <summary>Geri Dönüşüm Kutusu öğesi mi?</summary>
    public static bool IsRecycleBin(string path) => string.Equals(path, Core.WidgetSeeds.RecycleBinItem, StringComparison.OrdinalIgnoreCase);

    public static string DisplayName(string path)
    {
        var name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(name)) return path;
        var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        return ext is ".lnk" or ".url" or ".exe" or ".appref-ms" ? System.IO.Path.GetFileNameWithoutExtension(name) : name;
    }

    /// <summary>
    /// Öğeyi açar. Kabuk açılışı (klasör, ağ yolu, OneDrive dosyası) Explorer meşgulken saniyeler sürebilir: arka planda
    /// yapılır, widget'lar o sırada donmaz. Açılan pencere öne gelebilsin diye ön plan izni önceden verilir.
    /// </summary>
    public static void Launch(string path, bool asAdmin = false)
    {
        const int ASFW_ANY = -1;
        AllowSetForegroundWindow(ASFW_ANY);
        var dispatcher = Application.Current?.Dispatcher;
        Task.Run(() =>
        {
            try
            {
                if (IsShellObject(path))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", "shell:" + path) { UseShellExecute = true })?.Dispose();
                    return;
                }
                var native = NativePath(path);
                var info = new ProcessStartInfo(native) { UseShellExecute = true };
                // Çalışma klasörü asıl yoldan alınır: "Sysnative" yalnızca 32-bit süreçlerde vardır, başlatılan 64-bit program onu bulamaz.
                if (File.Exists(native)) info.WorkingDirectory = System.IO.Path.GetDirectoryName(path);
                if (asAdmin) info.Verb = "runas";
                Process.Start(info)?.Dispose();
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // Kullanıcı yönetici onayını iptal etti.
            }
            catch (Exception ex)
            {
                dispatcher?.BeginInvoke(() => MessageBox.Show(ex.Message, AppInfo.Name));
            }
        });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    /// <summary>Öğeyi Gezgin'de seçili gösterir. Yolun varlığı arka planda denetlenir (ulaşılamayan ağ yolu beklenmez).</summary>
    public static void Reveal(string path)
    {
        AllowSetForegroundWindow(-1);
        Task.Run(() =>
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            Process.Start("explorer.exe", $"/select,\"{path}\"")?.Dispose();
        });
    }
}
