using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>
/// Düzenle panelinin görünüm galerisi (<see cref="LookGallery"/>): sekiz kart, her biri <see cref="WidgetLooks"/>'un kurallarıyla
/// yazılır (2.1 için en yakın Style/Accent hep yazılır), seçili kart ayardan bulunur, "tüm widget'lara uygula" geri alınabilir.
/// </summary>
public class LookGalleryTests
{
    [Fact]
    public void Gallery_has_the_eight_looks_in_order()
    {
        Assert.Equal(["auto", "cam", "koyu", "acik", "neon", "gece", "kagit", "okyanus"], LookGallery.All.Select(e => e.Id));
        Assert.Equal(["Otomatik", "Cam", "Koyu", "Açık", "Neon", "Gece", "Kâğıt", "Okyanus"], LookGallery.All.Select(e => e.Label));
        using var _ = L.Use(Lang.En);
        Assert.Equal(["Auto", "Glass", "Dark", "Light", "Neon", "Night", "Paper", "Ocean"], LookGallery.All.Select(e => e.Label));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_look_round_trips_through_widget_looks(bool systemDark)
    {
        foreach (var entry in LookGallery.All)
        {
            var c = new WidgetConfig { Kind = WidgetKind.Fence, Style = WidgetStyle.Glass, Accent = WidgetAccent.Pink };
            Assert.True(LookGallery.Apply(c, entry.Id, systemDark));
            Assert.Equal(entry.Id, LookGallery.Current(c));

            if (LookGallery.StyleOf(entry.Id) is { } style)
            {
                Assert.Null(c.LookTheme);
                Assert.Equal(style, c.Style);
                Assert.Equal(WidgetAccent.Pink, c.Accent); // klasik arka plan rengi değiştirmez
            }
            else if (entry.Id == ThemePresets.Auto)
            {
                Assert.Equal("auto", c.LookTheme);
                Assert.Equal(systemDark ? WidgetStyle.Dark : WidgetStyle.Light, c.Style); // 2.1 için en yakın
                Assert.Null(c.LookAccentColor);
            }
            else
            {
                var preset = ThemePresets.Find(entry.Id)!;
                Assert.Equal(entry.Id, c.LookTheme);
                Assert.Equal(ThemePresets.NearestStyle(preset.Variant(systemDark)), c.Style);
                Assert.Equal(ThemeMath.NearestAccent(preset.Accent), c.Accent);
                Assert.Equal(ThemeMath.ToHex(preset.Accent), c.LookAccentColor);
            }
        }
    }

    [Fact]
    public void Leaving_a_theme_for_a_classic_look_drops_the_themes_own_color_but_keeps_a_chosen_one()
    {
        var c = new WidgetConfig { Kind = WidgetKind.Fence };
        LookGallery.Apply(c, "neon", systemDark: true);
        LookGallery.Apply(c, "koyu", systemDark: true);
        Assert.Null(c.LookTheme);
        Assert.Null(c.LookAccentColor);                                       // temanın rengi temayla gitti
        Assert.Equal(ThemeMath.NearestAccent(0xFF22D3EE), c.Accent);          // en yakın adlı renk kaldı
        Assert.Equal(c.Accent, WidgetLooks.ShownAccent(c));                    // renk noktası işaretli

        // Kullanıcının seçtiği özel renk kalır.
        Assert.True(WidgetLooks.SetAccentColor(c, "#123456"));
        LookGallery.Apply(c, "acik", systemDark: true);
        Assert.Equal("#123456", c.LookAccentColor);
        Assert.Equal(WidgetStyle.Light, c.Style);

        // Temada seçilen özel renk de kalır.
        LookGallery.Apply(c, "gece", systemDark: true);
        Assert.True(WidgetLooks.SetAccentColor(c, "#654321"));
        LookGallery.Apply(c, "cam", systemDark: true);
        Assert.Equal("#654321", c.LookAccentColor);
    }

    [Fact]
    public void Unknown_ids_change_nothing_and_unknown_theme_names_show_the_classic_card()
    {
        var c = new WidgetConfig { Kind = WidgetKind.Clock, Style = WidgetStyle.Light, LookTheme = "gelecek" };
        Assert.False(LookGallery.Apply(c, "bilinmeyen", systemDark: false));
        Assert.Equal("gelecek", c.LookTheme);              // daha yeni sürümün teması korunur
        Assert.Equal(WidgetStyle.Light, c.Style);
        Assert.Equal("acik", LookGallery.Current(c));      // klasik çizildiği gibi görünür
        Assert.Equal("cam", LookGallery.Current(new WidgetConfig()));
    }

    [Fact]
    public void Probes_paint_cards_without_touching_the_widget()
    {
        var c = new WidgetConfig { Kind = WidgetKind.Fence, Style = WidgetStyle.Dark, Accent = WidgetAccent.Green, NoteText = "x" };
        var probe = LookGallery.Probe(c, "okyanus", systemDark: false);
        Assert.Equal("okyanus", probe.LookTheme);
        Assert.Null(c.LookTheme);
        Assert.Equal(WidgetStyle.Dark, c.Style);
        Assert.NotSame(c, probe);

        var dot = LookGallery.ProbeAccent(c, WidgetAccent.Orange);
        Assert.Equal(WidgetAccent.Orange, dot.Accent);
        Assert.Equal(WidgetAccent.Green, c.Accent);
    }

    [Fact]
    public void Look_for_all_copies_theme_and_colors_and_undo_puts_each_back()
    {
        var source = new WidgetConfig { Kind = WidgetKind.Clock };
        LookGallery.Apply(source, "gece", systemDark: true);
        var fence = new WidgetConfig { Kind = WidgetKind.Fence, Style = WidgetStyle.Light, Accent = WidgetAccent.Orange, Scale = 1.5 };
        var box = new WidgetConfig { Kind = WidgetKind.Launcher, LookTheme = "neon", LookAccentColor = "#22D3EE" };

        var before = LookGallery.CopyToAll(source, [source, fence, box]);
        Assert.Equal([fence.Id, box.Id], before.Select(b => b.Id));
        foreach (var target in new[] { fence, box })
        {
            Assert.Equal(LookFields.Of(source), LookFields.Of(target));
        }
        Assert.Equal(1.5, fence.Scale); // boyut görünüm değildir

        var restored = LookGallery.Undo(before, [fence]); // kutu bu arada kaldırıldı
        Assert.Equal([fence.Id], restored);
        Assert.Equal(WidgetStyle.Light, fence.Style);
        Assert.Equal(WidgetAccent.Orange, fence.Accent);
        Assert.Null(fence.LookTheme);
        Assert.Null(fence.LookAccentColor);
    }
}
