using System.Windows;
using System.Windows.Media;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// Halkanın görünüşü (RingColors ve öğe/orta ölçüleri): simgeler her temada öğe dairesinin üstünde okunur, yüksek karşıtlıkta
/// vurgulanan öğenin simgesi kaybolmaz, gölge ve odak halkası yerinde kalır, fırçalar dondurulmuştur (efekt yok).
/// </summary>
public class RingLookTests
{
    /// <summary>
    /// Fluent simgeler (vurgu renginde) boştaki ve vurgulanan öğe dairesinin karşısında en az 3:1 okunur: bütün hazır temalar
    /// (açık/koyu), klasik arka planlar ve beş vurgu rengi. Okyanus'un koyu hâlinde deniz yeşili simge deniz yeşili dairede
    /// kayboluyordu.
    /// </summary>
    [Fact]
    public void Glyphs_read_on_idle_and_highlighted_items_in_every_look() => OnSta(() =>
    {
        var palettes = new List<(string Name, WidgetPalette Palette)>();
        foreach (var preset in ThemePresets.All)
            foreach (var variant in preset.Variants)
                palettes.Add((preset.Id + (variant.IsLight ? "/açık" : "/koyu"), WidgetPalette.FromColors(variant, preset.Accent)));
        foreach (var style in new[] { WidgetStyle.Glass, WidgetStyle.Dark, WidgetStyle.Light })
            foreach (var accent in Enum.GetValues<WidgetAccent>())
                palettes.Add(($"{style}/{accent}", WidgetPalette.For(style, accent)));

        foreach (var (name, palette) in palettes)
        {
            var colors = RingColors.From(palette, highContrast: false);
            var glyph = Argb(colors.Glyph);
            foreach (var fill in new[] { colors.Chip, colors.ChipHot })
                foreach (var chip in Fills(fill))
                    foreach (var stop in Stops(palette.Background))
                    {
                        var under = ThemeMath.Composite(chip, ThemeMath.Opaque(stop));
                        Assert.True(ThemeMath.Contrast(glyph, under) >= RingColors.GlyphContrast - 0.01,
                            $"{name}: {ThemeMath.Contrast(glyph, under):0.00}");
                    }
        }
    });

    /// <summary>
    /// Yüksek karşıtlık: vurgulanan ve basılan öğe ile orta daire pencere renginde kalır (simge ve yazı pencere yazısı renginde
    /// okunur; Vurgulama dolgusunda simge görünmüyordu), vurgu kalın Vurgulama kenarıyla; rozet Vurgulama / Vurgulanan yazı
    /// çifti; gölge yok.
    /// </summary>
    [Fact]
    public void High_contrast_keeps_highlighted_items_legible() => OnSta(() =>
    {
        var c = RingColors.From(WidgetPalette.HighContrast(), highContrast: true);
        Assert.True(c.HighContrast);
        Assert.Equal(SystemColors.WindowTextColor, c.Glyph);
        foreach (var fill in new[] { c.Chip, c.ChipHot, c.ChipPressed, c.Center, c.CenterHot })
            Assert.Equal(SystemColors.WindowColor, ((SolidColorBrush)fill).Color);
        Assert.Equal(SystemColors.WindowTextColor, ((SolidColorBrush)c.TextHot).Color);
        Assert.Equal(SystemColors.HighlightColor, ((SolidColorBrush)c.ChipBorderHot.Brush).Color);
        Assert.Equal(SystemColors.HighlightColor, ((SolidColorBrush)c.CenterBorderHot.Brush).Color);
        Assert.True(c.ChipBorderHot.Thickness >= 2);
        Assert.Equal(SystemColors.HighlightColor, ((SolidColorBrush)c.BadgeHot).Color);
        Assert.Equal(SystemColors.HighlightTextColor, ((SolidColorBrush)c.BadgeTextHot).Color);
        Assert.Equal(SystemColors.HighlightColor, ((SolidColorBrush)c.HotFill).Color);
        Assert.Null(c.ChipShadow);
    });

    /// <summary>Bütün fırça ve kalemler dondurulmuştur (yeniden çizimde kopyalanmaz); gölge yalnızca degradedir, efekt değil.</summary>
    [Fact]
    public void Ring_colors_are_frozen() => OnSta(() =>
    {
        foreach (var colors in new[] { RingColors.From(WidgetPalette.Glass, false), RingColors.From(WidgetPalette.For(WidgetStyle.Light), false),
                     RingColors.From(WidgetPalette.HighContrast(), true) })
            foreach (var property in typeof(RingColors).GetProperties())
                if (property.GetValue(colors) is Freezable freezable)
                    Assert.True(freezable.IsFrozen, property.Name);
        Assert.IsType<RadialGradientBrush>(RingColors.From(WidgetPalette.Glass, false).ChipShadow);
    });

    /// <summary>
    /// Ölçüler: öğe gölgesi öğenin kutusundan taşmaz; odak halkası orta dairenin dışında ve öğelerin içinde kalır; rozet öğe
    /// dairesinden küçük; vurgulanan dilimin degradesi ölçülü (dış kenarda en çok ~%30).
    /// </summary>
    [Fact]
    public void Shadow_focus_ring_and_badge_stay_in_place()
    {
        foreach (var icon in new[] { 24.0, 36, 48, 64 })
            foreach (var places in new[] { 1, 4, 8, 12, 16 })
            {
                var m = RingMetrics.For(places, icon);
                Assert.True(m.ItemDip / 2 + RingItemVisual.ShadowReach + RingItemVisual.ShadowDrop <= RingItemVisual.BoxSize(m) / 2, $"{icon}/{places}");
                Assert.True(m.CenterRadius + RingCenterVisual.FocusGap + 1 < m.Radius - m.ItemDip / 2, $"{icon}/{places}");
                Assert.InRange(RingItemVisual.BadgeRadius(m), 7.5, m.ItemDip / 4);
            }
        Assert.True(RingView.HotInnerAlpha <= 0x10);
        Assert.True(RingView.HotOuterAlpha <= 0x50);
    }

    private static uint Argb(Color c) => (uint)c.A << 24 | (uint)c.R << 16 | (uint)c.G << 8 | c.B;

    private static IEnumerable<uint> Fills(Brush brush) => brush switch
    {
        SolidColorBrush s => [Argb(s.Color)],
        GradientBrush g => g.GradientStops.Select(x => Argb(x.Color)),
        _ => [],
    };

    private static IEnumerable<uint> Stops(Brush background) => Fills(background);

    private static void OnSta(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA testi zaman aşımına uğradı");
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
