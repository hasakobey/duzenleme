using System.Windows;
using System.Windows.Media;

namespace Duzenleme.Widgets;

/// <summary>
/// Widget içi küçük yüzeylerin renkleri (TileStyles.xaml: kaldırma ×, kutucuk seçimi ve fare zemini, widget düğmeleri):
/// şablonlar bunları <c>DynamicResource</c> ile alır, <see cref="WidgetWindow"/> kartın kaynaklarına paletten yazar.
/// Varsayılanları TileStyles.xaml'dadır (2.1 renkleri). Kutucuk seçimi vurgu rengini izler. Yüksek karşıtlıkta Windows'un
/// renkleri kullanılır: zemin katmanları kalkar, seçim ve fare Vurgulama/Bağlantı rengiyle çerçevelenir, kaldırma ×'i
/// Vurgulama zemininde Vurgulanan yazı rengindedir.
/// </summary>
internal static class WidgetChrome
{
    public const string TileHover = "WidgetTileHoverBrush";
    public const string TileSelected = "WidgetTileSelectedBrush";
    public const string TileHoverBorder = "WidgetTileHoverBorderBrush";
    public const string TileSelectedBorder = "WidgetTileSelectedBorderBrush";
    public const string CloseHover = "WidgetCloseHoverColor";
    public const string CloseGlyphHover = "WidgetCloseGlyphHoverColor";
    public const string GlyphHover = "WidgetGlyphHoverColor";
    public const string PillHover = "WidgetPillHoverColor";
    public const string TextHover = "WidgetTextHoverColor";
    public const string Pressed = "WidgetPressedBrush";
    public const string ButtonFill = "WidgetButtonFillBrush";
    public const string PillFill = "WidgetPillFillBrush";
    public const string ButtonOutline = "WidgetButtonOutlineBrush";

    /// <summary>Bütün anahtarlar (TileStyles.xaml'da her birinin varsayılanı olmalı; RenderingTests denetler).</summary>
    public static IReadOnlyList<string> Keys =>
        [TileHover, TileSelected, TileHoverBorder, TileSelectedBorder, CloseHover, CloseGlyphHover, GlyphHover, PillHover, TextHover,
         Pressed, ButtonFill, PillFill, ButtonOutline];

    /// <summary>Paletin kaynak değerleri (fırçalar dondurulmuş; renkler <see cref="Color"/>).</summary>
    public static IReadOnlyList<(string Key, object Value)> For(WidgetPalette palette)
    {
        if (palette.IsHighContrast)
        {
            var text = SystemColors.WindowTextColor;
            return
            [
                (TileHover, Clear), (TileSelected, Clear),
                (TileHoverBorder, Brush(SystemColors.HotTrackColor)), (TileSelectedBorder, Brush(SystemColors.HighlightColor)),
                (CloseHover, SystemColors.HighlightColor), (CloseGlyphHover, SystemColors.HighlightTextColor),
                (GlyphHover, WithAlpha(text, 0x33)), (PillHover, WithAlpha(text, 0x33)), (TextHover, WithAlpha(text, 0x33)),
                (Pressed, Brush(WithAlpha(text, 0x4D))), (ButtonFill, Clear), (PillFill, Clear), (ButtonOutline, Brush(text)),
            ];
        }
        var accent = palette.Accent is SolidColorBrush solid ? solid.Color : Color.FromRgb(0xA7, 0x8B, 0xFA);
        return
        [
            (TileHover, Brush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF))), (TileSelected, Brush(WithAlpha(accent, 0x40))),
            (TileHoverBorder, Clear), (TileSelectedBorder, Clear),
            (CloseHover, Color.FromRgb(0xE5, 0x48, 0x4D)), (CloseGlyphHover, Colors.White),
            (GlyphHover, Color.FromArgb(0x26, 0x80, 0x80, 0x80)), (PillHover, Color.FromArgb(0x1F, 0x80, 0x80, 0x80)),
            (TextHover, Color.FromArgb(0x10, 0x80, 0x80, 0x80)),
            (Pressed, Brush(Color.FromArgb(0x1A, 0x80, 0x80, 0x80))), (ButtonFill, Brush(Color.FromArgb(0x1A, 0x80, 0x80, 0x80))),
            (PillFill, Brush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF))), (ButtonOutline, Clear),
        ];
    }

    /// <summary>Kartın kaynaklarına yazar; yalnızca değişen değer (aynı renkte fırça yeniden atanmaz: şablonlar boşuna tazelenmez).</summary>
    public static void Apply(ResourceDictionary resources, WidgetPalette palette)
    {
        foreach (var (key, value) in For(palette))
            if (!Same(resources.Contains(key) ? resources[key] : null, value)) resources[key] = value;
    }

    private static bool Same(object? a, object b) => (a, b) switch
    {
        (SolidColorBrush x, SolidColorBrush y) => x.Color == y.Color,
        (Color x, Color y) => x == y,
        _ => false,
    };

    private static readonly SolidColorBrush Clear = Brush(Colors.Transparent);

    private static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
