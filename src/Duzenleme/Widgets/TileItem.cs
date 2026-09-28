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
/// Simge önbellekte yoksa arka planda yüklenir ve gelince görünür (büyük klasörlerde arayüz donmasın).
/// </summary>
public sealed class TileItem : INotifyPropertyChanged
{
    private ImageSource? _icon;

    public required string Name { get; init; }
    public required string Path { get; init; }
    public bool Missing { get; init; }

    public ImageSource? Icon
    {
        get => _icon;
        private set
        {
            _icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
    /// Dosya/klasör kutucuğu. <paramref name="pixelsPerDip"/> kutucuğu gösterecek görünümün ekran ölçeğidir
    /// (VisualTreeHelper.GetDpi(görünüm).PixelsPerDip); verilmezse sistemin ölçeği sayılır. Görünüm, ekran ya da widget
    /// ölçeği değişince <see cref="UpdateIconSize"/> çağırmalıdır.
    /// </summary>
    public static TileItem Create(string path, WidgetConfig config, string? name = null, double pixelsPerDip = 0)
    {
        var native = NativePath(path);
        var exists = File.Exists(native) || Directory.Exists(native);
        var item = Layout(config, path, name ?? DisplayName(path), missing: !exists);
        if (exists)
        {
            item._iconPath = native;
            item._preview = config.ShowPreviews && File.Exists(native) && ShellIcons.CanPreview(native);
            item.UpdateIconSize(pixelsPerDip, config.Scale);
        }
        return item;
    }

    // Simgesi istenen yol (32-bit'te Sysnative'e çevrilmiş ya da "::{CLSID}"), önizleme mi, en son istenen piksel boyutu.
    private string? _iconPath;
    private bool _preview;
    private int _pixels;

    /// <summary>
    /// Simgeyi ekranda çizileceği gerçek piksel boyutunda (simge boyutu × ekran ölçeği × widget ölçeği) ister ve 1:1
    /// çizdirir; iki kat büyük istenip küçültülen ya da küçük istenip büyütülen simge %125/%150'de bulanık görünürdü.
    /// Boyut değişmediyse bir şey yapmaz. Yeni boyut gelene dek eski simge görünür.
    /// </summary>
    public void UpdateIconSize(double pixelsPerDip, double widgetScale)
    {
        if (_iconPath is not { } path) return;
        var pixels = IconSizing.DevicePixels(IconPx, pixelsPerDip > 0 ? pixelsPerDip : NativeMethods.SystemPixelsPerDip, widgetScale);
        if (pixels == _pixels) return;
        _pixels = pixels;
        var preview = _preview;
        if (ShellIcons.TryCached(path, pixels, preview, out var cached))
        {
            Icon = cached;
            return;
        }
        // Önizleme gelene kadar (varsa) türün simgesi görünsün.
        if (preview && Icon is null && ShellIcons.TryCached(path, pixels, false, out var typeIcon)) Icon = typeIcon;
        // Arada başka bir boyut istendiyse (ekran değişti) geç gelen eski boyut yenisinin üstüne yazılmaz.
        ShellIcons.Request(path, pixels, preview, icon => { if (_pixels == pixels) Icon = icon ?? Icon; });
    }

    /// <summary>Kutucuğun yerleşimi (simge boyutu, yazı, aralık, liste/ızgara); simgesi yok.</summary>
    private static TileItem Layout(WidgetConfig config, string path, string name, bool missing)
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

    /// <summary>Simge paneli: ızgara ya da liste; ızgarada satırlar sola, ortaya ya da sağa yaslanır.</summary>
    public static ItemsPanelTemplate Panel(WidgetConfig config)
    {
        var list = config.View == ItemView.List;
        var panel = new FrameworkElementFactory(list ? typeof(StackPanel) : typeof(WrapPanel));
        if (!list && config.Align == TileAlign.Right)
            panel.SetValue(FrameworkElement.FlowDirectionProperty, FlowDirection.RightToLeft);
        else if (!list && config.Align == TileAlign.Center)
            panel.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        return new ItemsPanelTemplate(panel);
    }

    /// <summary>
    /// Bu Bilgisayar, Geri Dönüşüm Kutusu gibi kabuk nesnesi ("::{CLSID}") kutucuğu. Simgesi de dosyalarınki gibi arka
    /// planda ve gerçek piksel boyutunda yüklenir (kabuk çağrısı arayüzü bekletmez).
    /// </summary>
    public static TileItem CreateShell(Desktop.SystemIcon icon, WidgetConfig config, double pixelsPerDip = 0)
    {
        var item = Layout(config, "::" + icon.Clsid, icon.Name, missing: false);
        item._iconPath = item.Path;
        item.UpdateIconSize(pixelsPerDip, config.Scale);
        return item;
    }

    public static bool IsShellObject(string path) => path.StartsWith("::", StringComparison.Ordinal);

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

    public static void Reveal(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        AllowSetForegroundWindow(-1);
        Process.Start("explorer.exe", $"/select,\"{path}\"")?.Dispose();
    }
}
