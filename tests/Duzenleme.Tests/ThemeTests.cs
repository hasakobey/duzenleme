using System.Text.Json;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// 2.2 widget temaları: renk hesapları (ThemeMath), paketlenen temaların okunurluğu (ThemePresets) ve görünüş seçimlerinin
/// öncelik kuralları (WidgetLooks).
/// </summary>
public class ThemeTests
{
    // ---------------------------------------------------------------- renk metni

    [Theory]
    [InlineData("#22D3EE", 0xFF22D3EEu)]
    [InlineData("22d3ee", 0xFF22D3EEu)]
    [InlineData("  #a78bfa ", 0xFFA78BFAu)]
    [InlineData("#8022D3EE", 0x8022D3EEu)]
    public void Hex_parses(string text, uint expected)
    {
        Assert.True(ThemeMath.TryParse(text, out var argb));
        Assert.Equal(expected, argb);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    [InlineData("red")]
    [InlineData("#1234567")]
    public void Bad_hex_is_rejected(string? text)
    {
        Assert.False(ThemeMath.TryParse(text, out _));
        Assert.Null(ThemeMath.NormalizeHex(text));
    }

    [Fact]
    public void Hex_is_stored_as_opaque_upper_case_rrggbb()
    {
        Assert.Equal("#22D3EE", ThemeMath.NormalizeHex("22d3ee"));
        Assert.Equal("#22D3EE", ThemeMath.ToHex(0xFF22D3EE));
        Assert.Equal("#22D3EE", ThemeMath.ToHex(0x0022D3EE)); // saydamlık yazılmaz
        Assert.Null(ThemeMath.NormalizeHex("#8022D3EE")); // saydam özel renk kaydedilmez
        Assert.Equal("#FFFFFF", ThemeMath.NormalizeHex("#FFFFFFFF"));
    }

    // ---------------------------------------------------------------- karşıtlık

    [Fact]
    public void Contrast_matches_wcag()
    {
        Assert.Equal(21, ThemeMath.Contrast(0xFF000000, 0xFFFFFFFF), 2);
        Assert.Equal(1, ThemeMath.Contrast(0xFF777777, 0xFF777777), 6);
        Assert.Equal(4.48, ThemeMath.Contrast(0xFF777777, 0xFFFFFFFF), 2); // WCAG'nin bilinen örneği
        Assert.Equal(ThemeMath.Contrast(0xFF2563EB, 0xFFFFFFFF), ThemeMath.Contrast(0xFFFFFFFF, 0xFF2563EB), 9);
    }

    [Fact]
    public void Composite_blends_by_alpha()
    {
        Assert.Equal(0xFF808080u, ThemeMath.Composite(0x80FFFFFF, 0xFF000000));
        Assert.Equal(0xFF123456u, ThemeMath.Composite(0xFF123456, 0xFFFFFFFF));
        Assert.Equal(0xFFABCDEFu, ThemeMath.Composite(0x00000000, 0xFFABCDEF));
    }

    [Theory]
    [InlineData(0xFF3E63DDu, 0xFF0F172Au)]   // koyu mavi, lacivert zeminde: açılmalı
    [InlineData(0xFFFFE066u, 0xFFFFFFFFu)]   // açık sarı, beyaz zeminde: koyulaşmalı
    [InlineData(0xFF777777u, 0xFF777777u)]   // zeminle aynı gri
    [InlineData(0xFF000000u, 0xFF000000u)]   // siyah zeminde siyah
    public void Ensure_contrast_reaches_the_minimum(uint color, uint background)
    {
        var shown = ThemeMath.EnsureContrast(color, [background], ThemeMath.AccentContrast);
        Assert.True(ThemeMath.Contrast(shown, background) >= ThemeMath.AccentContrast,
            $"{ThemeMath.ToHex(shown)} / {ThemeMath.ToHex(background)}: {ThemeMath.Contrast(shown, background):0.00}");
    }

    [Fact]
    public void Ensure_contrast_keeps_a_readable_color_and_its_hue()
    {
        Assert.Equal(0xFF22D3EEu, ThemeMath.EnsureContrast(0xFF22D3EE, [0xFF0B0816], ThemeMath.AccentContrast));
        var shown = ThemeMath.EnsureContrast(0xFF3E63DD, [0xFF0F172A, 0xFF1A2642], ThemeMath.AccentContrast);
        Assert.NotEqual(0xFF3E63DDu, shown);
        Assert.InRange(Math.Abs(ThemeMath.ToHsl(shown).H - ThemeMath.ToHsl(0xFF3E63DD).H), 0, 3);
        Assert.Equal(ThemeMath.NearestAccent(0xFF3E63DD), ThemeMath.NearestAccent(shown));
    }

    [Fact]
    public void Text_on_accent_is_the_more_readable_ink()
    {
        Assert.Equal(ThemeMath.DarkInk, ThemeMath.TextOn(0xFFFCD34D));
        Assert.Equal(ThemeMath.WhiteInk, ThemeMath.TextOn(0xFF1D4ED8));
    }

    [Fact]
    public void Hsl_round_trips()
    {
        foreach (var color in new uint[] { 0xFF22D3EE, 0xFFB45309, 0xFF0D9488, 0xFF7C3AED, 0xFF808080, 0xFFFFFFFF, 0xFF000000 })
        {
            var (h, s, l) = ThemeMath.ToHsl(color);
            var back = ThemeMath.FromHsl(h, s, l);
            foreach (var shift in new[] { 16, 8, 0 })
                Assert.InRange(Math.Abs((int)((back >> shift) & 0xFF) - (int)((color >> shift) & 0xFF)), 0, 1);
        }
    }

    // ---------------------------------------------------------------- en yakın vurgu rengi

    [Theory]
    [InlineData(0xFF22D3EEu, WidgetAccent.Blue)]     // camgöbeği
    [InlineData(0xFF2563EBu, WidgetAccent.Blue)]
    [InlineData(0xFF0D9488u, WidgetAccent.Green)]    // deniz yeşili
    [InlineData(0xFF16A34Au, WidgetAccent.Green)]
    [InlineData(0xFFB45309u, WidgetAccent.Orange)]   // kehribar
    [InlineData(0xFFFCD34Du, WidgetAccent.Orange)]   // ay sarısı
    [InlineData(0xFFE5484Du, WidgetAccent.Orange)]   // kırmızı: turuncuya pembeden yakın
    [InlineData(0xFFDB2777u, WidgetAccent.Pink)]
    [InlineData(0xFF8B5CF6u, WidgetAccent.Violet)]
    [InlineData(0xFF808080u, WidgetAccent.Violet)]   // gri: varsayılan
    public void Nearest_accent_by_hue(uint color, WidgetAccent expected) =>
        Assert.Equal(expected, ThemeMath.NearestAccent(color));

    [Fact]
    public void Every_named_accent_is_its_own_nearest()
    {
        foreach (var accent in Enum.GetValues<WidgetAccent>())
        {
            Assert.Equal(accent, ThemeMath.NearestAccent(ThemeMath.AccentArgb(accent, onLight: true)));
            Assert.Equal(accent, ThemeMath.NearestAccent(ThemeMath.AccentArgb(accent, onLight: false)));
        }
    }

    // ---------------------------------------------------------------- paketlenen temalar

    public static TheoryData<string> PresetIds()
    {
        var data = new TheoryData<string>();
        foreach (var preset in ThemePresets.All) data.Add(preset.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(PresetIds))]
    public void Every_bundled_preset_and_variant_is_readable(string id)
    {
        var preset = ThemePresets.Find(id)!;
        Assert.Empty(ThemePresets.Validate(preset));
        foreach (var variant in preset.Variants)
        {
            // Paketlenen temalar opaktır (yazı ClearType ile çizilsin) ve kendi kurallarını sayılarla da sağlar.
            Assert.True(variant.IsOpaque);
            foreach (var stop in variant.Stops)
            {
                Assert.True(ThemeMath.Contrast(variant.Text, stop) >= 4.5, $"{id}: yazı {ThemeMath.Contrast(variant.Text, stop):0.00}");
                Assert.True(ThemeMath.Contrast(variant.Secondary, stop) >= 3.0, $"{id}: ikincil {ThemeMath.Contrast(variant.Secondary, stop):0.00}");
                Assert.True(ThemeMath.Contrast(preset.Accent, stop) >= 3.0, $"{id}: vurgu {ThemeMath.Contrast(preset.Accent, stop):0.00}");
            }
        }
        Assert.True(ThemeMath.Contrast(ThemeMath.TextOn(preset.Accent), preset.Accent) >= 4.5);
    }

    [Fact]
    public void Validation_catches_unreadable_themes()
    {
        var gray = new ThemeColors(0xFF777777, 0xFF777777, 0x20FFFFFF, 0xFF888888, 0xFF808080);
        Assert.NotEmpty(ThemePresets.Validate(new ThemePreset("x", "x", 0xFF7A7A7A, null, gray)));
        // Saydam zemin: yazı açık ve gölgeli olmalı (Cam gibi).
        var glass = new ThemeColors(0x70141420, 0x4A0C0C14, 0x38FFFFFF, 0xFFFFFFFF, 0xC8FFFFFF, TextShadow: false);
        Assert.Contains(ThemePresets.Validate(new ThemePreset("y", "y", 0xFFC4B5FD, null, glass)), p => p.Contains("translucent"));
        Assert.NotEmpty(ThemePresets.Validate(new ThemePreset("z", "z", 0xFF22D3EE, null, null)));
    }

    [Fact]
    public void Preset_ids_names_and_variants()
    {
        Assert.Equal([ThemePresets.Neon, ThemePresets.Gece, ThemePresets.Kagit, ThemePresets.Okyanus], ThemePresets.All.Select(p => p.Id));
        Assert.Equal(ThemePresets.All.Count, ThemePresets.All.Select(p => p.Id).Distinct().Count());
        Assert.All(ThemePresets.All, p => Assert.Equal(p.Id, p.Id.ToLowerInvariant()));
        Assert.Equal("Kâğıt", ThemePresets.Find("kagit")!.Label); // testler Türkçe
        Assert.True(ThemePresets.Find(ThemePresets.Okyanus)!.FollowsWindows);
        Assert.False(ThemePresets.Find(ThemePresets.Neon)!.FollowsWindows);
        var ocean = ThemePresets.Find("okyanus")!;
        Assert.False(ocean.Variant(systemDark: true).IsLight);
        Assert.True(ocean.Variant(systemDark: false).IsLight);
        Assert.False(ThemePresets.Find("neon")!.Variant(systemDark: false).IsLight); // tek hâlli tema hep aynı
    }

    [Fact]
    public void Unknown_theme_is_classic()
    {
        Assert.Null(ThemePresets.Known(null));
        Assert.Null(ThemePresets.Known(""));
        Assert.Null(ThemePresets.Known("gelecek-tema"));
        Assert.Equal("auto", ThemePresets.Known(" AUTO "));
        Assert.Equal("neon", ThemePresets.Known("Neon"));
        Assert.Null(ThemePresets.Find("auto"));
        Assert.True(ThemePresets.FollowsWindows("auto"));
        Assert.True(ThemePresets.FollowsWindows("okyanus"));
        Assert.False(ThemePresets.FollowsWindows("gece"));
        Assert.False(ThemePresets.FollowsWindows("gelecek-tema"));
        Assert.False(ThemePresets.FollowsWindows(null));
    }

    // ---------------------------------------------------------------- öncelik kuralları

    [Fact]
    public void Picking_a_preset_writes_its_accent_and_the_nearest_21_look()
    {
        var c = new WidgetConfig { Kind = WidgetKind.Fence, Style = WidgetStyle.Glass, Accent = WidgetAccent.Pink };
        WidgetLooks.SetTheme(c, "neon", systemDark: false);
        Assert.Equal("neon", c.LookTheme);
        Assert.Equal("#22D3EE", c.LookAccentColor);
        Assert.Equal(WidgetStyle.Dark, c.Style);      // 2.1: opak koyu zemin
        Assert.Equal(WidgetAccent.Blue, c.Accent);    // 2.1: camgöbeğine en yakın

        WidgetLooks.SetTheme(c, "kagit", systemDark: true);
        Assert.Equal(WidgetStyle.Light, c.Style);
        Assert.Equal(WidgetAccent.Orange, c.Accent);

        WidgetLooks.SetTheme(c, "okyanus", systemDark: true);
        Assert.Equal(WidgetStyle.Dark, c.Style);
        WidgetLooks.SetTheme(c, "okyanus", systemDark: false);
        Assert.Equal(WidgetStyle.Light, c.Style);
    }

    [Fact]
    public void Classic_and_auto_clear_the_theme_accent()
    {
        var c = new WidgetConfig { Kind = WidgetKind.Clock };
        WidgetLooks.SetTheme(c, "gece", systemDark: true);
        Assert.NotNull(c.LookAccentColor);

        WidgetLooks.SetTheme(c, ThemePresets.Auto, systemDark: true);
        Assert.Equal("auto", c.LookTheme);
        Assert.Null(c.LookAccentColor);
        Assert.Equal(WidgetStyle.Dark, c.Style);
        WidgetLooks.SetTheme(c, ThemePresets.Auto, systemDark: false);
        Assert.Equal(WidgetStyle.Light, c.Style);

        WidgetLooks.SetTheme(c, "gece", systemDark: true);
        WidgetLooks.SetTheme(c, "", systemDark: true);
        Assert.Null(c.LookTheme);
        Assert.Null(c.LookAccentColor);
        WidgetLooks.SetTheme(c, "bilinmeyen", systemDark: true);
        Assert.Null(c.LookTheme);
    }

    [Fact]
    public void Classic_background_clears_the_theme_and_shows_unchecked_while_a_theme_is_on()
    {
        var c = new WidgetConfig { Kind = WidgetKind.Fence, Style = WidgetStyle.Glass };
        Assert.Equal(WidgetStyle.Glass, WidgetLooks.ShownStyle(c));
        WidgetLooks.SetTheme(c, "neon", systemDark: true);
        Assert.Null(WidgetLooks.ShownStyle(c));
        WidgetLooks.SetStyle(c, WidgetStyle.Light);
        Assert.Null(c.LookTheme);
        Assert.Equal(WidgetStyle.Light, WidgetLooks.ShownStyle(c));
        Assert.Equal("#22D3EE", c.LookAccentColor); // vurgu rengi olduğu gibi kalır

        // Daha yeni sürümün teması bu sürümde klasik çizilir: Arka plan işaretli görünür, tema adı korunur.
        c.LookTheme = "gelecek";
        Assert.Equal(WidgetStyle.Light, WidgetLooks.ShownStyle(c));
        Assert.Equal("gelecek", c.LookTheme);
    }

    [Fact]
    public void Named_accent_clears_the_custom_color_and_custom_color_sets_the_nearest_name()
    {
        var c = new WidgetConfig { Kind = WidgetKind.Fence, Accent = WidgetAccent.Violet };
        Assert.True(WidgetLooks.SetAccentColor(c, "#0d9488"));
        Assert.Equal("#0D9488", c.LookAccentColor);
        Assert.Equal(WidgetAccent.Green, c.Accent);
        Assert.Null(WidgetLooks.ShownAccent(c));
        Assert.Equal("#0D9488", WidgetLooks.CustomAccent(c));

        Assert.False(WidgetLooks.SetAccentColor(c, "turkuaz"));
        Assert.Equal("#0D9488", c.LookAccentColor);

        WidgetLooks.SetAccent(c, WidgetAccent.Pink);
        Assert.Null(c.LookAccentColor);
        Assert.Equal(WidgetAccent.Pink, WidgetLooks.ShownAccent(c));

        // Elle bozulmuş özel renk yok sayılır: adlı renk geçerli.
        c.LookAccentColor = "mavi";
        Assert.Null(WidgetLooks.CustomAccent(c));
        Assert.Equal(WidgetAccent.Pink, WidgetLooks.ShownAccent(c));
    }

    [Fact]
    public void Theme_accent_is_not_shown_as_a_chosen_custom_color()
    {
        // Hazır tema kendi rengini LookAccentColor'a yazar; menüde "Özel renk…" işaretli görünmemeli (kullanıcı seçmedi).
        var c = new WidgetConfig { Kind = WidgetKind.Fence };
        Assert.False(WidgetLooks.CustomAccentChosen(c));
        WidgetLooks.SetTheme(c, "neon", systemDark: true);
        Assert.NotNull(WidgetLooks.CustomAccent(c));
        Assert.False(WidgetLooks.CustomAccentChosen(c));
        Assert.Null(WidgetLooks.ShownAccent(c)); // renk temanın: adlı renklerin hiçbiri işaretli değil

        Assert.True(WidgetLooks.SetAccentColor(c, "#123456"));
        Assert.True(WidgetLooks.CustomAccentChosen(c));
        // Elle temanın rengini yazmak da (küçük harfle) temanın rengi sayılır.
        Assert.True(WidgetLooks.SetAccentColor(c, "#22d3ee"));
        Assert.False(WidgetLooks.CustomAccentChosen(c));

        // Tema kaldırılınca (klasik arka plan) aynı renk artık kullanıcının özel rengidir.
        WidgetLooks.SetStyle(c, WidgetStyle.Dark);
        Assert.True(WidgetLooks.CustomAccentChosen(c));
        WidgetLooks.SetTheme(c, "", systemDark: true);
        Assert.False(WidgetLooks.CustomAccentChosen(c));
    }

    [Fact]
    public void Only_auto_and_two_variant_themes_follow_windows()
    {
        Assert.False(WidgetLooks.FollowsWindows(new WidgetConfig()));
        Assert.True(WidgetLooks.FollowsWindows(new WidgetConfig { LookTheme = "auto" }));
        Assert.True(WidgetLooks.FollowsWindows(new WidgetConfig { LookTheme = "okyanus" }));
        Assert.False(WidgetLooks.FollowsWindows(new WidgetConfig { LookTheme = "neon" }));
    }

    [Fact]
    public void Theme_fields_round_trip_through_settings()
    {
        var c = new WidgetConfig { Kind = WidgetKind.Launcher };
        WidgetLooks.SetTheme(c, "okyanus", systemDark: true);
        var copy = c.Clone();
        Assert.Equal("okyanus", copy.LookTheme);
        Assert.Equal("#0D9488", copy.LookAccentColor);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(c, JsonFile.Options));
        Assert.Equal("okyanus", doc.RootElement.GetProperty("LookTheme").GetString());
    }
}
