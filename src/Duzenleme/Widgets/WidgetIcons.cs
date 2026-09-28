using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Duzenleme.Core;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Duzenleme.Widgets;

/// <summary>
/// Widget simgesinin tek kaynağı: başlıktaki simge, Widget'lar listesi ve ekleme kutucukları aynı eşlemeyi kullanır
/// (kullanıcı simge seçince her yerde aynı anda değişir). Kullanıcının seçtiği <see cref="WidgetConfig.Icon"/> ("sym:") önce
/// gelir; yoksa ya da tanınmıyorsa türün varsayılanı. Yeni widget türleri <see cref="DefaultFor"/>'a kendi satırını ekler.
/// </summary>
internal static class WidgetIcons
{
    /// <summary>Başlıkta ve listede gösterilecek simge.</summary>
    public static SymbolRegular For(WidgetConfig config) => Symbol(config.Icon) ?? DefaultFor(config);

    /// <summary>
    /// Türün (alt türün; bölmede kaynağının) varsayılan simgesi; ekleme kutucuklarıyla aynı (<c>WidgetCatalogTests</c>
    /// denetler). Yeni alt tür (<see cref="WidgetVariants"/>) buraya kendi satırını ekler.
    /// </summary>
    public static SymbolRegular DefaultFor(WidgetConfig config) => WidgetVariants.Of(config) switch
    {
        WidgetVariants.Month => SymbolRegular.CalendarMonth24,
        WidgetVariants.Countdown => SymbolRegular.CalendarStar24,
        WidgetVariants.Timer => TimerModes.Normalize(config.Timer?.Mode) == TimerModes.Pomodoro ? SymbolRegular.ClockAlarm24 : SymbolRegular.Timer24,
        WidgetVariants.World => SymbolRegular.GlobeClock24,
        WidgetVariants.System => SymbolRegular.Gauge24,
        WidgetVariants.Recycle => SymbolRegular.Delete24,
        _ => config.Kind switch
        {
            WidgetKind.Clock => SymbolRegular.Clock24,
            WidgetKind.Date => SymbolRegular.CalendarLtr24,
            WidgetKind.Note => config.NoteChecklist ? SymbolRegular.TaskListLtr24 : SymbolRegular.Note24,
            WidgetKind.Launcher => SymbolRegular.AppsAddIn24,
            _ => config.Filter switch
            {
                DesktopFilter.Folders => SymbolRegular.Folder24,
                DesktopFilter.Shortcuts => SymbolRegular.Apps24,
                DesktopFilter.Files => SymbolRegular.DocumentMultiple24,
                DesktopFilter.All => SymbolRegular.Desktop24,
                _ when WidgetVariants.IsPortal(config) => ForKnownFolder(config.FolderKnownId),
                _ => string.Equals(config.FolderName, "PDF", StringComparison.OrdinalIgnoreCase)
                    ? SymbolRegular.DocumentPdf24 : SymbolRegular.FolderOpen24,
            },
        },
    };

    /// <summary>Klasör portalının simgesi: Windows'un bilinen klasörü (İndirilenler…) ya da herhangi bir klasör.</summary>
    public static SymbolRegular ForKnownFolder(string? knownId) => knownId switch
    {
        FolderPortal.Downloads => SymbolRegular.ArrowDownload24,
        FolderPortal.Documents => SymbolRegular.Document24,
        FolderPortal.Pictures => SymbolRegular.Image24,
        FolderPortal.Music => SymbolRegular.MusicNote224,
        FolderPortal.Videos => SymbolRegular.Video24,
        FolderPortal.Screenshots => SymbolRegular.Screenshot24,
        _ => SymbolRegular.FolderLink24,
    };

    /// <summary>
    /// "sym:Ad" başvurusunun Fluent simgesi; başvuru yoksa, simge değilse, WPF-UI'de bulunmuyorsa ya da çizilemiyorsa
    /// (U+FFFF üstü: SymbolIcon "X" gösterir) null.
    /// </summary>
    public static SymbolRegular? Symbol(string? iconRef) =>
        IconRef.Parse(iconRef) is { Kind: IconRefKind.Symbol } r && Enum.TryParse<SymbolRegular>(r.Value, out var symbol) &&
        Enum.IsDefined(symbol) && IsDrawable(symbol)
            ? symbol
            : null;

    /// <summary>Simge (dolu biçimiyle de) WPF-UI yazı tipinin BMP aralığında mı?</summary>
    public static bool IsDrawable(SymbolRegular symbol)
    {
        if ((int)symbol is <= 0 or > 0xFFFF) return false;
        try { return (int)Wpf.Ui.Extensions.SymbolExtensions.Swap(symbol) is > 0 and <= 0xFFFF; }
        catch (ArgumentException) { return false; } // dolu karşılığı yok
    }
}

/// <summary>
/// Fluent simgesinin (WPF-UI yazı tipi) vektör resmi: kutucuk şablonu yalnızca Image taşır; kısayol kutusu öğesine seçilen
/// simge de öteki simgeler gibi Image ile, her boyutta keskin çizilir. Önbellekli (simge, renk, dolu).
/// </summary>
internal static class GlyphImage
{
    private static readonly Dictionary<(SymbolRegular, uint, bool), ImageSource> Cache = [];
    private static FontFamily? _regular, _filled;

    public static ImageSource? For(SymbolRegular symbol, Color color, bool filled = true)
    {
        var key = (symbol, (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B), filled);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var family = filled ? _filled ??= Font("FluentSystemIconsFilled", "FluentSystemIcons-Filled")
                            : _regular ??= Font("FluentSystemIcons", "FluentSystemIcons-Regular");
        var glyph = filled ? Wpf.Ui.Extensions.SymbolExtensions.GetString(Wpf.Ui.Extensions.SymbolExtensions.Swap(symbol))
                           : Wpf.Ui.Extensions.SymbolExtensions.GetString(symbol);
        if (string.IsNullOrEmpty(glyph)) return null;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        // 100 birimlik tuvalde çizilir, Image kutucuğun boyutuna ölçekler (vektör: bulanıklaşmaz).
        var text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 100, brush, 1.0);
        var geometry = text.BuildGeometry(new Point(0, 0));
        if (geometry.IsEmpty()) return null;
        var group = new DrawingGroup();
        // Saydam kare: simge ortalı dursun, en-boy oranı korunsun.
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 100, 100))));
        var bounds = geometry.Bounds;
        var scale = 88 / Math.Max(bounds.Width, bounds.Height);
        var transform = new TransformGroup();
        transform.Children.Add(new TranslateTransform(-bounds.Left - bounds.Width / 2, -bounds.Top - bounds.Height / 2));
        transform.Children.Add(new ScaleTransform(scale, scale));
        transform.Children.Add(new TranslateTransform(50, 50));
        geometry.Transform = transform;
        group.Children.Add(new GeometryDrawing(brush, null, geometry));
        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        Cache[key] = image;
        return image;
    }

    private static FontFamily Font(string resourceKey, string familyName) =>
        Application.Current?.TryFindResource(resourceKey) as FontFamily
        ?? new FontFamily(new Uri("pack://application:,,,/Wpf.Ui;component/Resources/Fonts/"), "./#" + familyName);
}
