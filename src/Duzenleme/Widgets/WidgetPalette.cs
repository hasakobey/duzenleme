using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

public sealed record WidgetPalette(Brush Background, Brush BorderBrush, Brush Foreground, Brush Secondary, Brush Accent, Brush AccentForeground, bool TextShadow, bool IsLight)
{
    public static SolidColorBrush Solid(uint argb)
    {
        var b = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        b.Freeze();
        return b;
    }

    public static LinearGradientBrush Gradient(uint from, uint to)
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
        Solid(0xFFC4B5FD), Solid(0xFF1E1B2E), TextShadow: true, IsLight: false);

    // Koyu, Açık ve Grafit tam opaktır: katmanlı pencerede yazı ancak opak zemin üstünde ClearType ile (renkli alt
    // piksellerle) çizilebilir; %95 opak zemin gözle fark edilmiyordu ama yazıyı gri tonlamalı (daha bulanık) bırakıyordu.
    public static readonly WidgetPalette Dark = new(
        Gradient(0xFF202029, 0xFF18181F), Solid(0x1FFFFFFF), Solid(0xFFF4F4F6), Solid(0xFF9D9DA8),
        Solid(0xFFA78BFA), Solid(0xFF16131F), TextShadow: false, IsLight: false);

    public static readonly WidgetPalette Light = new(
        Gradient(0xFFFFFFFF, 0xFFF5F3FF), Solid(0x18000000), Solid(0xFF1C1C21), Solid(0xFF6B6B76),
        Solid(0xFF7C3AED), Solid(0xFFFFFFFF), TextShadow: false, IsLight: true);

    /// <summary>Vurgu renkleri: koyu zeminde açık ton, açık zeminde koyu ton.</summary>
    private static (uint OnDark, uint OnLight) AccentColors(WidgetAccent accent) => accent switch
    {
        WidgetAccent.Blue => (0xFF93C5FD, 0xFF2563EB),
        WidgetAccent.Green => (0xFF86EFAC, 0xFF16A34A),
        WidgetAccent.Orange => (0xFFFDBA74, 0xFFEA580C),
        WidgetAccent.Pink => (0xFFF9A8D4, 0xFFDB2777),
        _ => (0xFFC4B5FD, 0xFF7C3AED),
    };

    public static uint AccentArgb(WidgetAccent accent, bool light) =>
        light ? AccentColors(accent).OnLight : AccentColors(accent).OnDark;

    public static WidgetPalette For(WidgetStyle style, WidgetAccent accent = WidgetAccent.Violet)
    {
        var basePalette = style switch
        {
            WidgetStyle.Dark => Dark,
            WidgetStyle.Light => Light,
            _ => Glass,
        };
        return basePalette with { Accent = Solid(AccentArgb(accent, basePalette.IsLight)) };
    }

    /// <summary>Yapışkan not renkleri (SlideSlide'daki yüzen notlar gibi).</summary>
    public static WidgetPalette ForNote(NoteColor color) => color switch
    {
        NoteColor.Pink => NotePalette(0xFFFFD6E7, 0xFFFFC2DA, 0xFFBE185D),
        NoteColor.Green => NotePalette(0xFFD9F7D2, 0xFFC4EFBA, 0xFF15803D),
        NoteColor.Blue => NotePalette(0xFFD6E8FF, 0xFFC0DBFF, 0xFF1D4ED8),
        NoteColor.Purple => NotePalette(0xFFE9DEFF, 0xFFDACBFF, 0xFF6D28D9),
        NoteColor.Graphite => new(Gradient(0xFF2A2A33, 0xFF20202A), Solid(0x24FFFFFF), Solid(0xFFF4F4F6), Solid(0xFFA1A1AA),
            Solid(0xFFFDE68A), Solid(0xFF1C1917), TextShadow: false, IsLight: false),
        _ => NotePalette(0xFFFFF4B8, 0xFFFFEC99, 0xFFA16207),
    };

    private static WidgetPalette NotePalette(uint top, uint bottom, uint accent) =>
        new(Gradient(top, bottom), Solid(0x14000000), Solid(0xFF26221C), Solid(0x99302A20),
            Solid(accent), Solid(0xFFFFFFFF), TextShadow: false, IsLight: true);

    /// <summary>Zemin tamamen opak mı (ClearType yalnızca opak zeminde çalışır)? Cam yarı saydamdır.</summary>
    public bool IsOpaqueBackground => Background switch
    {
        SolidColorBrush s => s.Color.A == 255 && s.Opacity >= 1,
        GradientBrush g => g.Opacity >= 1 && g.GradientStops.Count > 0 && g.GradientStops.All(x => x.Color.A == 255),
        _ => false,
    };
}
