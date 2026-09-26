using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

public sealed record WidgetPalette(Brush Background, Brush BorderBrush, Brush Foreground, Brush Secondary, Brush Accent, Brush AccentForeground, bool TextShadow)
{
    private static SolidColorBrush Solid(uint argb)
    {
        var b = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        b.Freeze();
        return b;
    }

    private static LinearGradientBrush Gradient(uint from, uint to)
    {
        var b = new LinearGradientBrush(
            Color.FromArgb((byte)(from >> 24), (byte)(from >> 16), (byte)(from >> 8), (byte)from),
            Color.FromArgb((byte)(to >> 24), (byte)(to >> 16), (byte)(to >> 8), (byte)to),
            new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
        b.Freeze();
        return b;
    }

    /// <summary>Koyu, yarı saydam cam: her duvar kağıdında okunur.</summary>
    public static readonly WidgetPalette Glass = new(
        Gradient(0x70141420, 0x4A0C0C14), Solid(0x38FFFFFF), Solid(0xFFFFFFFF), Solid(0xC8FFFFFF),
        Solid(0xFFC4B5FD), Solid(0xFF1E1B2E), TextShadow: true);

    public static readonly WidgetPalette Dark = new(
        Gradient(0xF2202029, 0xF218181F), Solid(0x1FFFFFFF), Solid(0xFFF4F4F6), Solid(0xFF9D9DA8),
        Solid(0xFFA78BFA), Solid(0xFF16131F), TextShadow: false);

    public static readonly WidgetPalette Light = new(
        Gradient(0xF7FFFFFF, 0xF2F5F3FF), Solid(0x18000000), Solid(0xFF1C1C21), Solid(0xFF6B6B76),
        Solid(0xFF7C3AED), Solid(0xFFFFFFFF), TextShadow: false);

    public static WidgetPalette For(WidgetStyle style) => style switch
    {
        WidgetStyle.Dark => Dark,
        WidgetStyle.Light => Light,
        _ => Glass,
    };
}
