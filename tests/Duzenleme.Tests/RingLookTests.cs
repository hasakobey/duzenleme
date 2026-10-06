using System.Windows;
using System.Windows.Media;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// Halkanın görünüşü (RingColors ve öğe/orta ölçüleri): simgeler her temada halkanın gerçek katmanlarının üstünde okunur,
/// vurgulanan ve basılan daireler nötr kalır, yüksek karşıtlıkta vurgulanan öğenin simgesi kaybolmaz, gölge dairenin içine
/// düşmez, odak halkası yerinde kalır, fırçalar dondurulmuştur (efekt yok).
/// </summary>
public class RingLookTests
{
    /// <summary>
    /// Fluent simgeler (vurgu renginde; alt halkanın geri oku ve ortadaki simge de) altlarında gerçekten görünen rengin karşısında
    /// en az 3:1 okunur. Katmanlar ekrandaki gibi kurulur: kartın zemini (Cam'da açılır halkanın koyu zemini de) → zemin diski
    /// (RingView'in kendi degradesi, simgenin her noktasında) → boştaki dilim ya da vurgulanan dilimin degradesi → öğe dairesi
    /// (boşta, vurgulu, basılı); ortada disk → orta daire. Bütün hazır temalar (açık/koyu), klasik arka planlar × beş vurgu rengi
    /// ve özel vurgu renkleri; birkaç simge boyu ve öğe sayısı. Daireyi yalnızca kartın üstüne koyan eski hesap Okyanus'un koyu
    /// hâlinde ve klasik Koyu'da vurgulanan öğede 3:1'in altında kalıyordu (vurgu degradesi yarı saydam daireden sızıyordu).
    /// </summary>
    [Fact]
    public void Glyphs_read_on_the_real_ring_layers_in_every_look() => OnSta(() =>
    {
        foreach (var (name, palette) in Palettes())
        {
            var colors = RingColors.From(palette, highContrast: false);
            var glyph = Argb(colors.Glyph);
            var text = Argb(((SolidColorBrush)colors.Text).Color);
            foreach (var ground in CardGrounds(palette))
                foreach (var icon in new[] { 24.0, 32, 48, 64 })
                    foreach (var places in new[] { 1, 3, 8, 12, 16 })
                    {
                        var m = RingMetrics.For(places, icon);
                        var (disc, hot) = RingView.LayerBrushes(colors, m);
                        var c = new Point(m.OuterRadius, m.OuterRadius);
                        var r = m.ItemDip / 2;
                        foreach (var angle in RingGeometry.Angles(places))
                        {
                            var at = RingView.At(c, m.Radius, angle);
                            foreach (var (dx, dy) in Grid(icon))
                            {
                                var point = new Point(at.X + dx, at.Y + dy);
                                var under = ThemeMath.Composite(Sample(disc, point), ground);
                                var idle = ThemeMath.Composite(Sample(colors.Wedge, point), under);
                                var highlighted = ThemeMath.Composite(Sample(hot, point), under);
                                var fy = (dy + r) / (2 * r);
                                Check($"{name} öğe boşta {icon}/{places}", glyph, ThemeMath.Composite(Vertical(colors.Chip, fy), idle));
                                Check($"{name} öğe vurgulu {icon}/{places}", glyph, ThemeMath.Composite(Vertical(colors.ChipHot, fy), highlighted));
                                foreach (var below in new[] { idle, highlighted })
                                    Check($"{name} öğe basılı {icon}/{places}", glyph, ThemeMath.Composite(Vertical(colors.ChipPressed, fy), below));
                            }
                        }
                        // Orta: simge (dairenin %85'i) ve alt halkanın geri oku; yazı da en az 4,5:1.
                        foreach (var (dx, dy) in Grid(m.CenterRadius * 0.85))
                        {
                            var under = ThemeMath.Composite(Sample(disc, new Point(c.X + dx, c.Y + dy)), ground);
                            foreach (var fill in new[] { colors.Center, colors.CenterHot, colors.ChipPressed })
                            {
                                var shown = ThemeMath.Composite(Vertical(fill, 0.5), under);
                                Check($"{name} orta {icon}/{places}", glyph, shown);
                                Assert.True(ThemeMath.Contrast(text, shown) >= ThemeMath.TextContrast - 0.01,
                                    $"{name} orta yazı: {ThemeMath.Contrast(text, shown):0.00}");
                            }
                        }
                    }
        }
    });

    /// <summary>
    /// Her halka biçiminde (<see cref="RingStyles"/>) × her görünümde (temalar açık/koyu, klasik arka planlar, özel vurgu renkleri)
    /// simgeler altlarındaki gerçek katmanların karşısında en az 3:1: dilimsiz biçimde (Daire, Etiketli) öğe dairesi doğrudan
    /// zemin diskinin üstündedir, vurgulanınca da; Işıltı'nın dış bandı, halesi ve kenarları öğe dairesinin dışında kalır (bkz.
    /// <see cref="Glow_ornaments_stay_outside_the_circles"/>). Etiketin adı etiket zemininde (boşta ve vurgulu) en az 4,5:1.
    /// </summary>
    [Fact]
    public void Glyphs_and_labels_read_in_every_style_and_look() => OnSta(() =>
    {
        foreach (var style in RingStyles.All)
            foreach (var (name, palette) in Palettes())
            {
                var colors = RingColors.From(palette, highContrast: false, style);
                var glyph = Argb(colors.Glyph);
                foreach (var ground in CardGrounds(palette))
                    foreach (var icon in new[] { 24.0, 48 })
                        foreach (var places in new[] { 3, 8, 12 })
                        {
                            var m = RingMetrics.For(places, icon, style);
                            var (disc, hot) = RingView.LayerBrushes(colors, m);
                            var c = new Point(m.OuterRadius, m.OuterRadius);
                            var r = m.ItemDip / 2;
                            foreach (var angle in RingGeometry.Angles(places))
                            {
                                var at = RingView.At(c, m.Radius, angle);
                                foreach (var (dx, dy) in Grid(icon))
                                {
                                    var point = new Point(at.X + dx, at.Y + dy);
                                    var under = ThemeMath.Composite(Sample(disc, point), ground);
                                    var idle = style.Wedges ? ThemeMath.Composite(Sample(colors.Wedge, point), under) : under;
                                    var highlighted = style.Wedges ? ThemeMath.Composite(Sample(hot, point), under) : under;
                                    var fy = (dy + r) / (2 * r);
                                    var what = $"{style.Id ?? "classic"} {name} {icon}/{places}";
                                    Check(what + " boşta", glyph, ThemeMath.Composite(Vertical(colors.Chip, fy), idle));
                                    Check(what + " vurgulu", glyph, ThemeMath.Composite(Vertical(colors.ChipHot, fy), highlighted));
                                    foreach (var below in new[] { idle, highlighted })
                                        Check(what + " basılı", glyph, ThemeMath.Composite(Vertical(colors.ChipPressed, fy), below));
                                }
                                if (!style.Labels) continue;
                                // Etiket: zemini diskin o yerindeki rengin üstünde; ad boşta yazı renginde, vurgulanınca vurgu yazısı.
                                var label = RingItemVisual.LabelRect(at, m, angle);
                                foreach (var corner in new[] { label.TopLeft, label.BottomRight, new Point(label.X + label.Width / 2, label.Y + label.Height / 2) })
                                {
                                    var under = ThemeMath.Composite(Sample(disc, corner), ground);
                                    var idleFill = ThemeMath.Composite(Argb(((SolidColorBrush)colors.Label).Color), under);
                                    var hotFill = ThemeMath.Composite(Argb(((SolidColorBrush)colors.LabelHot).Color), under);
                                    var text = Argb(((SolidColorBrush)colors.LabelText).Color);
                                    var hotText = Argb(((SolidColorBrush)colors.LabelTextHot).Color);
                                    Assert.True(ThemeMath.Contrast(text, idleFill) >= ThemeMath.TextContrast - 0.01,
                                        $"{name} etiket: {ThemeMath.Contrast(text, idleFill):0.00}");
                                    Assert.True(ThemeMath.Contrast(hotText, hotFill) >= ThemeMath.TextContrast - 0.01,
                                        $"{name} vurgulu etiket: {ThemeMath.Contrast(hotText, hotFill):0.00}");
                                }
                            }
                        }
            }
    });

    /// <summary>
    /// Işıltı'nın süsleri efekt değil, dondurulmuş degrade ve biçimdir ve simgenin altına düşmez: vurgulanan dairenin halesi
    /// yalnızca dairenin dışında ve öğe kutusunun içinde; dış bant öğe dairelerinin dışında başlar. Yüksek karşıtlıkta hiçbiri yok.
    /// </summary>
    [Fact]
    public void Glow_ornaments_stay_outside_the_circles() => OnSta(() =>
    {
        foreach (var icon in new[] { 16.0, 32, 64 })
            foreach (var places in new[] { 1, 6, 12 })
            {
                var m = RingMetrics.For(places, icon, RingStyles.For(RingStyles.Glow));
                var (brush, shape) = RingItemVisual.Halo(m, Colors.MediumPurple)!.Value;
                Assert.True(brush.IsFrozen && shape.IsFrozen);
                var box = RingItemVisual.BoxSize(m);
                var c = new Point(box / 2, box / 2);
                var r = m.ItemDip / 2;
                for (var angle = 0; angle < 360; angle += 20)
                    for (var d = 0.0; d <= r - RingItemVisual.ShadowHoleInset - 0.05; d += 1)
                        Assert.False(shape.FillContains(RingView.At(c, d, angle)));
                var bounds = shape.Bounds;
                Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= box && bounds.Bottom <= box, $"{icon}: {bounds}");

                var colors = RingColors.From(WidgetPalette.Glass, highContrast: false, RingStyles.For(RingStyles.Glow));
                var (band, halo) = RingView.GlowBrushes(colors, m);
                Assert.True(band.IsFrozen && halo.IsFrozen);
                var ring = new Point(m.OuterRadius, m.OuterRadius);
                // Bandın saydam olmayan kısmı öğe dairelerinin dış kenarından sonra başlar.
                Assert.Equal(0u, Sample(band, RingView.At(ring, m.Radius + r, 45)) >> 24);
                Assert.True(Sample(band, RingView.At(ring, m.OuterRadius - 4, 45)) >> 24 >= RingView.GlowBandAlpha - 1);
            }
        var hc = RingColors.From(WidgetPalette.HighContrast(), highContrast: true, RingStyles.For(RingStyles.Glow));
        Assert.True(hc.HighContrast);
        Assert.Same(RingStyles.Classic, RingStyles.Effective(RingStyles.Glow, highContrast: true));
    });

    /// <summary>
    /// Vurgulanan ve basılan daireler (öğe ve orta) nötrdür ve vurgu rengi kenarda (2 DIP) durur; orta daire öğe dairesiyle
    /// aynı. Koyu temada neredeyse opak (altındaki vurgu degradesi sızmaz), açık temada opak. Ortanın vurgusu eskiden vurgu
    /// renginin yarı saydamıydı: üstündeki vurgu renginde simge ve geri oku açık temalarda 2,3:1'e iniyordu.
    /// </summary>
    [Fact]
    public void Highlighted_and_pressed_circles_are_neutral_with_an_accent_border() => OnSta(() =>
    {
        foreach (var (name, palette) in Palettes())
        {
            var c = RingColors.From(palette, highContrast: false);
            Assert.Same(c.ChipHot, c.CenterHot);
            foreach (var border in new[] { c.ChipBorderHot, c.CenterBorderHot })
            {
                Assert.Equal(c.Accent, ((SolidColorBrush)border.Brush).Color);
                Assert.Equal(2, border.Thickness);
            }
            foreach (var fill in new[] { c.ChipHot, c.ChipPressed })
            {
                var color = ((SolidColorBrush)fill).Color;
                Assert.True(color.A >= (palette.IsLight ? 0xFF : 0xE6), $"{name}: {color}");
            }
        }
        // Nötr: dolgu vurgu renginden bağımsızdır (aynı zeminde her vurgu renginde aynı).
        static (Color Hot, Color Pressed) Fills(WidgetPalette palette)
        {
            var c = RingColors.From(palette, highContrast: false);
            return (((SolidColorBrush)c.ChipHot).Color, ((SolidColorBrush)c.ChipPressed).Color);
        }
        foreach (var style in new[] { WidgetStyle.Glass, WidgetStyle.Dark, WidgetStyle.Light })
            Assert.Single(Enum.GetValues<WidgetAccent>().Select(a => Fills(WidgetPalette.For(style, a))).Distinct());
        foreach (var preset in ThemePresets.All)
            foreach (var variant in preset.Variants)
                Assert.Equal(Fills(WidgetPalette.FromColors(variant, preset.Accent)), Fills(WidgetPalette.FromColors(variant, 0xFFDB2777)));
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
        Assert.Equal(0, c.ChipShadowAlpha);
        Assert.Null(RingItemVisual.Shadow(RingMetrics.For(8, 32), c.ChipShadowAlpha));
    });

    /// <summary>Bütün fırça, kalem ve biçimler dondurulmuştur (yeniden çizimde kopyalanmaz); gölge yalnızca degradedir, efekt değil.</summary>
    [Fact]
    public void Ring_colors_are_frozen() => OnSta(() =>
    {
        var m = RingMetrics.For(8, 32);
        foreach (var colors in new[] { RingColors.From(WidgetPalette.Glass, false), RingColors.From(WidgetPalette.For(WidgetStyle.Light), false),
                     RingColors.From(WidgetPalette.HighContrast(), true) })
        {
            foreach (var property in typeof(RingColors).GetProperties())
                if (property.GetValue(colors) is Freezable freezable)
                    Assert.True(freezable.IsFrozen, property.Name);
            var (disc, hot) = RingView.LayerBrushes(colors, m);
            Assert.True(disc.IsFrozen);
            Assert.True(hot.IsFrozen);
        }
        var (brush, shape) = RingItemVisual.Shadow(m, 0x50)!.Value;
        Assert.IsType<RadialGradientBrush>(brush);
        Assert.True(brush.IsFrozen);
        Assert.True(shape.IsFrozen);
    });

    /// <summary>
    /// Öğe gölgesi dairenin içine hiç düşmez (koyu temada yarı saydam daire ve simge koyulaşmıyordu: degrade gölge dairesinin
    /// yarıçapına göre kurulduğundan 16 DIP'ten büyük her dairede içeride başlıyordu); dairenin dışında, özellikle altında
    /// görünür ve öğenin kutusundan taşmaz.
    /// </summary>
    [Fact]
    public void Chip_shadow_never_falls_inside_the_circle() => OnSta(() =>
    {
        foreach (var icon in new[] { 16.0, 24, 32, 48, 64, 96 })
        {
            var m = RingMetrics.For(8, icon);
            var (brush, shape) = RingItemVisual.Shadow(m, 0x50)!.Value;
            var box = RingItemVisual.BoxSize(m);
            var c = new Point(box / 2, box / 2);
            var r = m.ItemDip / 2;
            for (var angle = 0; angle < 360; angle += 15)
            {
                for (var d = 0.0; d <= r - RingItemVisual.ShadowHoleInset - 0.05; d += 0.5)
                {
                    var p = RingView.At(c, d, angle);
                    Assert.False(shape.FillContains(p), $"{icon}: içeride {d:0.0}/{angle}");
                }
            }
            // Altında görünür (dairenin 1,5 DIP altı en koyu), yanlarda da var; kutudan taşmaz.
            var below = new Point(c.X, c.Y + r + 1.5);
            Assert.True(shape.FillContains(below));
            Assert.True(Sample(brush, below) >> 24 >= 0x40, $"{icon}: alt");
            Assert.True(Sample(brush, new Point(c.X + r + 0.8, c.Y)) >> 24 > 0, $"{icon}: yan");
            var bounds = shape.Bounds;
            Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= box && bounds.Bottom <= box, $"{icon}: {bounds}");
            // Gölgenin ortası daireden aşağıda: kenarın altında, üstündekinden koyu.
            Assert.True(Sample(brush, below) >> 24 > Sample(brush, new Point(c.X, c.Y - r - 1.5)) >> 24, $"{icon}: üst");
        }
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
        Assert.True(RingView.HotInnerAlpha < RingView.HotMidAlpha && RingView.HotMidAlpha < RingView.HotOuterAlpha);
        Assert.True(RingView.HotOuterAlpha <= 0x50);
    }

    // ---------------------------------------------------------------- yardımcılar

    private static IEnumerable<(string Name, WidgetPalette Palette)> Palettes()
    {
        foreach (var preset in ThemePresets.All)
            foreach (var variant in preset.Variants)
                yield return (preset.Id + (variant.IsLight ? "/açık" : "/koyu"), WidgetPalette.FromColors(variant, preset.Accent));
        foreach (var style in new[] { WidgetStyle.Glass, WidgetStyle.Dark, WidgetStyle.Light })
        {
            foreach (var accent in Enum.GetValues<WidgetAccent>())
                yield return ($"{style}/{accent}", WidgetPalette.For(style, accent));
            // Özel vurgu renkleri (zemine göre açılıp koyulaşır): deniz yeşili, sarı, lacivert.
            foreach (var hex in new[] { "#0D9488", "#FACC15", "#1E3A8A" })
                yield return ($"{style}/{hex}", WidgetPalette.Resolve(new WidgetConfig { Style = style, LookAccentColor = hex }, null, false, false));
        }
    }

    /// <summary>
    /// Halkanın altındaki zemin (opak): kartın degradesinin iki ucu ve ortası; Cam'da (yarı saydam) açılır halkanın koyu zemini de
    /// (RingBackdrop), kartın kendisi siyahın üstünde.
    /// </summary>
    private static IEnumerable<uint> CardGrounds(WidgetPalette palette)
    {
        var stops = Stops(palette.Background).ToList();
        var grounds = stops.Concat(stops.Count > 1 ? [Lerp(stops[0], stops[^1], 0.5)] : []).Select(ThemeMath.Opaque).ToList();
        if (!palette.IsOpaqueBackground)
            grounds.AddRange(new[] { RingColors.PopupPlateInner, Lerp(RingColors.PopupPlateInner, RingColors.PopupPlateOuter, 0.5), RingColors.PopupPlateOuter }
                .Select(ThemeMath.Opaque));
        return grounds;
    }

    /// <summary>Simgenin kutusunda 5×5 nokta (ortasına göre).</summary>
    private static IEnumerable<(double X, double Y)> Grid(double size)
    {
        for (var i = 0; i < 5; i++)
            for (var j = 0; j < 5; j++)
                yield return (-size / 2 + size * i / 4, -size / 2 + size * j / 4);
    }

    private static void Check(string what, uint glyph, uint under)
    {
        var contrast = ThemeMath.Contrast(glyph, under);
        Assert.True(contrast >= RingColors.GlyphContrast - 0.01, $"{what}: {contrast:0.00} (simge #{glyph & 0xFFFFFF:X6}, zemin #{under & 0xFFFFFF:X6})");
    }

    /// <summary>Fırçanın noktadaki rengi: düz renk ya da (mutlak eşlemeli, dairesel) degrade, durakları arasında doğrusal.</summary>
    private static uint Sample(Brush brush, Point point)
    {
        if (brush is SolidColorBrush s) return Argb(s.Color);
        var g = (RadialGradientBrush)brush;
        Assert.Equal(BrushMappingMode.Absolute, g.MappingMode);
        Assert.Equal(g.RadiusX, g.RadiusY);
        return AtOffset(g, (point - g.Center).Length / g.RadiusX);
    }

    /// <summary>Daireyi dolduran fırçanın, dairenin yukarıdan aşağı <paramref name="fy"/> (0–1) yerindeki rengi.</summary>
    private static uint Vertical(Brush brush, double fy) => brush switch
    {
        SolidColorBrush s => Argb(s.Color),
        LinearGradientBrush g => AtOffset(g, Math.Clamp(fy, 0, 1)),
        _ => throw new InvalidOperationException(brush.GetType().Name),
    };

    private static uint AtOffset(GradientBrush g, double t)
    {
        var stops = g.GradientStops.OrderBy(x => x.Offset).ToList();
        if (t <= stops[0].Offset) return Argb(stops[0].Color);
        for (var i = 1; i < stops.Count; i++)
            if (t <= stops[i].Offset)
            {
                var (a, b) = (stops[i - 1], stops[i]);
                var span = b.Offset - a.Offset;
                return Lerp(Argb(a.Color), Argb(b.Color), span <= 0 ? 1 : (t - a.Offset) / span);
            }
        return Argb(stops[^1].Color);
    }

    private static uint Lerp(uint a, uint b, double f)
    {
        uint Channel(int shift) => (uint)Math.Round(((a >> shift) & 0xFF) + ((double)((b >> shift) & 0xFF) - ((a >> shift) & 0xFF)) * f);
        return Channel(24) << 24 | Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }

    private static uint Argb(Color c) => (uint)c.A << 24 | (uint)c.R << 16 | (uint)c.G << 8 | c.B;

    private static IEnumerable<uint> Stops(Brush background) => background switch
    {
        SolidColorBrush s => [Argb(s.Color)],
        GradientBrush g => g.GradientStops.Select(x => Argb(x.Color)),
        _ => [],
    };

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA testi zaman aşımına uğradı");
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
