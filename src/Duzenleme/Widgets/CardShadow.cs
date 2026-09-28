using System.Windows;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Widget kartının gölgesi, efektsiz: 4 köşede dairesel, 4 kenarda doğrusal degrade ve ortada düz dolgu
/// (<see cref="ShadowGradient"/>). Kartın arkasında, kartın kendisinden bağımsız çizilir: kartın içinde bir şey değişince
/// (fare üstüne gelme, saat, yazı imleci) gölge yeniden hesaplanmaz. Eski DropShadowEffect yazılım çiziminde her
/// değişiklikte bütün kartı yeniden bulanıklaştırıyor, fare gezinirken bir işlemci çekirdeğini dolduruyordu.
/// Fırçalar dondurulur ve yalnızca köşe yarıçapı ya da koyuluk değişince yeniden kurulur; bit eşlem yok, her ölçekte keskin.
/// </summary>
internal sealed class CardShadow : FrameworkElement
{
    /// <summary>Gölgenin kart kenarından taşma payı (eski efektin BlurRadius'u), DIP.</summary>
    public const double Pad = 26;

    /// <summary>Gölgenin aşağı kayması (eski efektin ShadowDepth'i), DIP.</summary>
    public const double Depth = 4;

    /// <summary>Tam opak kartın gölge koyuluğu; yarı saydam kartta zeminin ortalama opaklığıyla çarpılır.</summary>
    public const double BaseOpacity = 0.32;

    private Brush? _topLeft, _topRight, _bottomLeft, _bottomRight, _top, _bottom, _left, _right, _fill;
    private double _corner = -1, _opacity = -1;

    public CardShadow()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>
    /// Kenar payları: kart pencerede her yandan <paramref name="cardMargin"/> içeride durur; gölge onun
    /// <see cref="Pad"/> dışına taşar ve <see cref="Depth"/> kadar aşağı kayar (pencerenin dışında kalan kısmı kırpılır).
    /// </summary>
    public static Thickness MarginFor(double cardMargin) =>
        new(cardMargin - Pad, cardMargin - Pad + Depth, cardMargin - Pad, cardMargin - Pad - Depth);

    /// <summary>Kartın (ölçeklenmiş) köşe yarıçapına ve zeminin opaklığına göre fırçaları kurar; değişmediyse bir şey yapmaz.</summary>
    public void Update(double cardRadius, double opacity)
    {
        var corner = Math.Round(Pad + ShadowGradient.ShadowRadius(cardRadius, Pad), 2);
        opacity = Math.Round(Math.Clamp(opacity, 0, 1), 3);
        if (corner == _corner && opacity == _opacity) return;
        _corner = corner;
        _opacity = opacity;

        var radius = corner - Pad;
        var radial = ToStops(ShadowGradient.Stops(opacity, Pad, radius, radial: true));
        var linear = ToStops(ShadowGradient.Stops(opacity, Pad, radius, radial: false));
        _topLeft = Radial(radial, 1, 1);
        _topRight = Radial(radial, 0, 1);
        _bottomLeft = Radial(radial, 1, 0);
        _bottomRight = Radial(radial, 0, 0);
        _top = Linear(linear, new Point(0, 0), new Point(0, 1));
        _bottom = Linear(linear, new Point(0, 1), new Point(0, 0));
        _left = Linear(linear, new Point(0, 0), new Point(1, 0));
        _right = Linear(linear, new Point(1, 0), new Point(0, 0));
        var fill = new SolidColorBrush(Color.FromArgb(ToByte(opacity), 0, 0, 0));
        fill.Freeze();
        _fill = fill;
        InvalidateVisual();
    }

    /// <summary>Gölge pencerenin boyutunu etkilemez (saat/tarih içeriğe göre boyutlanır); kartın boyutunu alır.</summary>
    protected override Size MeasureOverride(Size availableSize) => default;

    protected override void OnRender(DrawingContext dc)
    {
        if (_fill is null || _corner <= 0) return;
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        // Hücre sınırları tam piksele oturur: yan yana iki yarı kaplanmış kenar, gölgede açık renkli bir çizgi bırakırdı.
        // Çok küçük kartta köşe hücreleri üst üste binmez (degrade sıkışır; olağan boyutlarda hiç olmaz).
        var dpi = VisualTreeHelper.GetDpi(this);
        var cx = Math.Min(Snap(_corner, dpi.DpiScaleX), Math.Floor(w * dpi.DpiScaleX / 2) / dpi.DpiScaleX);
        var cy = Math.Min(Snap(_corner, dpi.DpiScaleY), Math.Floor(h * dpi.DpiScaleY / 2) / dpi.DpiScaleY);
        double midW = w - 2 * cx, midH = h - 2 * cy;

        dc.DrawRectangle(_topLeft, null, new Rect(0, 0, cx, cy));
        dc.DrawRectangle(_topRight, null, new Rect(w - cx, 0, cx, cy));
        dc.DrawRectangle(_bottomLeft, null, new Rect(0, h - cy, cx, cy));
        dc.DrawRectangle(_bottomRight, null, new Rect(w - cx, h - cy, cx, cy));
        if (midW > 0)
        {
            dc.DrawRectangle(_top, null, new Rect(cx, 0, midW, cy));
            dc.DrawRectangle(_bottom, null, new Rect(cx, h - cy, midW, cy));
        }
        if (midH > 0)
        {
            dc.DrawRectangle(_left, null, new Rect(0, cy, cx, midH));
            dc.DrawRectangle(_right, null, new Rect(w - cx, cy, cx, midH));
        }
        if (midW > 0 && midH > 0) dc.DrawRectangle(_fill, null, new Rect(cx, cy, midW, midH));
    }

    private static GradientStopCollection ToStops(IReadOnlyList<(double Offset, double Alpha)> stops)
    {
        var collection = new GradientStopCollection(stops.Count);
        foreach (var (offset, alpha) in stops) collection.Add(new GradientStop(Color.FromArgb(ToByte(alpha), 0, 0, 0), offset));
        collection.Freeze();
        return collection;
    }

    private static Brush Radial(GradientStopCollection stops, double cx, double cy)
    {
        var brush = new RadialGradientBrush(stops)
        {
            Center = new Point(cx, cy), GradientOrigin = new Point(cx, cy), RadiusX = 1, RadiusY = 1,
        };
        brush.Freeze();
        return brush;
    }

    private static Brush Linear(GradientStopCollection stops, Point start, Point end)
    {
        var brush = new LinearGradientBrush(stops, start, end);
        brush.Freeze();
        return brush;
    }

    private static byte ToByte(double alpha) => (byte)Math.Round(255 * Math.Clamp(alpha, 0, 1));

    private static double Snap(double value, double scale) => Math.Round(value * scale) / scale;

    /// <summary>Zeminin ortalama opaklığı (degrade duraklarının ortalaması × fırça opaklığı); bilinmiyorsa 1.</summary>
    public static double MeanAlpha(Brush? brush) => brush switch
    {
        SolidColorBrush s => s.Color.A / 255.0 * s.Opacity,
        GradientBrush g when g.GradientStops.Count > 0 => g.GradientStops.Average(x => x.Color.A) / 255.0 * g.Opacity,
        null => 0,
        _ => 1,
    };
}
