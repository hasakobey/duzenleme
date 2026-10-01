using System.Xml.Linq;
using Duzenleme.Core;
using Duzenleme.Icons;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>Çizim ve ölçek (DPI) kuralları: efektsiz gölge, başlık sığdırma, simge boyutu, önbellek, çalışma alanı.</summary>
public class RenderingTests
{
    // --- Efektsiz gölge ---

    [Fact]
    public void Shadow_tail_is_a_gaussian_edge()
    {
        Assert.Equal(0.5, ShadowGradient.Tail(0), 6);
        Assert.True(ShadowGradient.Tail(3) < 0.002);
        Assert.True(ShadowGradient.Tail(-3) > 0.998);
        foreach (var z in new[] { 0.3, 1.0, 1.7, 2.5 })
            Assert.Equal(1, ShadowGradient.Tail(z) + ShadowGradient.Tail(-z), 6);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Shadow_stops_fade_from_full_inside_to_nothing_outside(bool radial)
    {
        const double pad = 26, opacity = 0.32;
        var radius = ShadowGradient.ShadowRadius(20, pad);
        var stops = ShadowGradient.Stops(opacity, pad, radius, radial);

        Assert.All(stops, s => Assert.InRange(s.Offset, 0, 1));
        Assert.All(stops, s => Assert.InRange(s.Alpha, 0, opacity));
        // Konuma göre sıralı; köşede merkezden dışa, kenarda dış sınırdan içe.
        Assert.Equal(stops.OrderBy(s => s.Offset).Select(s => s.Offset), stops.Select(s => s.Offset));
        var (outer, inner) = radial ? (stops[^1], stops[0]) : (stops[0], stops[^1]);
        Assert.Equal(1, radial ? outer.Offset : 1 - outer.Offset, 6);
        Assert.True(outer.Alpha < 0.002, "gölgenin dış sınırı saydam olmalı");
        Assert.True(inner.Alpha > opacity * 0.99, "iç sınır orta dolguyla aynı koyulukta olmalı (basamak kalmasın)");
        // Dışa doğru hiç koyulaşmaz.
        var outward = radial ? stops : stops.Reverse().ToList();
        for (var i = 1; i < outward.Count; i++) Assert.True(outward[i].Alpha <= outward[i - 1].Alpha + 1e-9);
    }

    [Fact]
    public void Shadow_corner_is_never_sharper_than_the_blur()
    {
        const double pad = 26;
        var sigma = ShadowGradient.Sigma(pad);
        Assert.Equal(ShadowGradient.MinRadiusInSigmas * sigma, ShadowGradient.ShadowRadius(3, pad), 6); // Köşeli
        Assert.Equal(ShadowGradient.MinRadiusInSigmas * sigma, ShadowGradient.ShadowRadius(0, pad), 6);
        Assert.Equal(50, ShadowGradient.ShadowRadius(50, pad), 6); // büyük ölçekte yuvarlak köşe olduğu gibi
        Assert.Throws<ArgumentOutOfRangeException>(() => ShadowGradient.Stops(0.3, 0, 10, true));
    }

    // --- Başlık sığdırma ---

    [Fact]
    public void Header_keeps_everything_when_the_title_fits()
    {
        // 300 genişlik: × 24 + sayı 42 + arama 28 + simge 30 = 124, başlığa 176 kalır.
        Assert.Equal(0, HeaderFit.PartsToHide(300, 24, 150, [42, 28, 30]));
    }

    [Fact]
    public void Narrow_header_hides_parts_in_order_until_the_title_gets_its_minimum()
    {
        // En dar bölme (190 DIP pencere → 132 DIP başlık): gizlenmeden başlığa 8 DIP kalırdı ("…").
        IReadOnlyList<double> parts = [42, 28, 28, 30]; // sayı, klasörü aç, arama, simge
        var hide = HeaderFit.PartsToHide(132, 24, 200, parts);
        Assert.Equal(3, hide);
        Assert.True(132 - 24 - parts.Skip(hide).Sum() >= HeaderFit.TitleMin);
        // Bir eksiği yetmezdi.
        Assert.True(132 - 24 - parts.Skip(hide - 1).Sum() < HeaderFit.TitleMin);
    }

    [Fact]
    public void Short_title_needs_only_its_own_width()
    {
        // "PDF" (30 DIP) sığıyorsa hiçbir şey gizlenmez; 72 DIP'lik pay yalnızca uzun başlıklar için.
        Assert.Equal(0, HeaderFit.PartsToHide(160, 24, 30, [42, 28, 30]));
        Assert.Equal(1, HeaderFit.PartsToHide(160, 24, 80, [42, 28, 30]));
    }

    [Fact]
    public void Header_hides_everything_optional_when_even_that_is_not_enough()
    {
        Assert.Equal(2, HeaderFit.PartsToHide(60, 24, 200, [30, 30]));
        Assert.Equal(0, HeaderFit.PartsToHide(60, 24, 200, []));
    }

    // --- Simge boyutu ---

    [Theory]
    [InlineData(36, 1.0, 1.0, 36)]
    [InlineData(36, 1.25, 1.0, 45)]  // %125: 72 isteyip küçültmek yerine tam 45
    [InlineData(36, 1.5, 1.0, 54)]
    [InlineData(36, 1.75, 1.0, 63)]
    [InlineData(24, 1.25, 1.0, 30)]
    [InlineData(28, 1.25, 1.0, 35)]  // ana penceredeki liste simgesi
    [InlineData(36, 1.25, 2.0, 90)]  // widget ölçeği %200
    [InlineData(36, 1.0, 9.0, 90)]   // ölçek 2,5'te kesilir
    [InlineData(64, 2.0, 2.5, 256)]  // en büyük kabuk simgesi
    [InlineData(36, 0, 1.0, 36)]     // bilinmeyen ekran ölçeği → %100
    [InlineData(36, double.NaN, double.NaN, 36)]
    public void Icons_are_requested_at_device_size(double dip, double pixelsPerDip, double scale, int expected) =>
        Assert.Equal(expected, IconSizing.DevicePixels(dip, pixelsPerDip, scale));

    // --- Sınırlı önbellek ---

    [Fact]
    public void Lru_cache_evicts_the_least_recently_used()
    {
        var cache = new LruCache<string, int>(3);
        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3);
        Assert.True(cache.TryGetValue("a", out _)); // a yeniden kullanıldı: en eski artık b
        cache.Set("d", 4);

        Assert.Equal(3, cache.Count);
        Assert.False(cache.TryGetValue("b", out _));
        Assert.True(cache.TryGetValue("a", out var a) && a == 1);
        Assert.True(cache.TryGetValue("c", out _));
        Assert.True(cache.TryGetValue("d", out _));
    }

    [Fact]
    public void Lru_cache_limits_total_cost_but_keeps_the_newest()
    {
        var cache = new LruCache<string, int[]>(100, maxCost: 10, cost: v => v.Length);
        cache.Set("a", new int[4]);
        cache.Set("b", new int[4]);
        cache.Set("c", new int[4]); // 12 > 10: a atılır
        Assert.Equal(8, cache.TotalCost);
        Assert.False(cache.TryGetValue("a", out _));

        cache.Set("b", new int[1]); // değiştirmek maliyeti günceller
        Assert.Equal(5, cache.TotalCost);

        cache.Set("huge", new int[50]); // tek başına sınırı aşsa da en yenisi kalır
        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGetValue("huge", out _));
    }

    [Fact]
    public void Lru_cache_removes_by_predicate_and_uses_the_comparer()
    {
        var cache = new LruCache<string, int>(10, comparer: StringComparer.OrdinalIgnoreCase);
        cache.Set(@"C:\Masaüstü\PDF|45", 1);
        cache.Set(@"C:\Masaüstü\PDF|36", 2);
        cache.Set(".pdf|45", 3);
        Assert.True(cache.TryGetValue(@"c:\MASAÜSTÜ\pdf|45", out var hit) && hit == 1);

        Assert.Equal(2, cache.RemoveWhere(k => k.StartsWith(@"C:\Masaüstü\PDF|", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(1, cache.Count);
        Assert.Equal(3, cache.GetOrAdd(".PDF|45", _ => 99));
        Assert.Equal(7, cache.GetOrAdd(".txt|45", _ => 7));
    }

    [Fact]
    public void Lru_cache_is_safe_across_threads()
    {
        var cache = new LruCache<int, int>(64, maxCost: 500, cost: v => v % 10);
        Parallel.For(0, 20_000, i =>
        {
            cache.Set(i % 200, i);
            cache.TryGetValue((i * 7) % 200, out _);
            if (i % 997 == 0) cache.RemoveWhere(k => k % 50 == 0);
        });
        Assert.InRange(cache.Count, 1, 64);
        Assert.InRange(cache.TotalCost, 0, 500);
    }

    // --- Çalışma alanına sığdırma (fiziksel piksel; 1920×1080 %125, görev çubuğu 60 px) ---

    private static readonly Box Work = new(-1920, 0, 0, 1020);
    private const int Margin = 18; // 14 DIP gölge payı × 1,25

    [Fact]
    public void Tallest_window_lets_the_card_fill_the_work_area()
    {
        Assert.Equal(1020 + 2 * Margin, WorkAreaFit.MaxWindowHeight(Work, Margin));
    }

    [Fact]
    public void Card_hanging_below_the_taskbar_moves_up_but_never_down()
    {
        // Kartın altı 1100'de (çalışma alanı 1020'de bitiyor): 80 px yukarı.
        Assert.Equal(420, WorkAreaFit.KeepBottomInside(500, 618, Work, Margin));
        // Sığan kart yerinde kalır, yukarıdaki kart aşağı çekilmez.
        Assert.Equal(100, WorkAreaFit.KeepBottomInside(100, 618, Work, Margin));
        Assert.Equal(-40, WorkAreaFit.KeepBottomInside(-40, 300, Work, Margin));
    }

    [Fact]
    public void Card_taller_than_the_work_area_is_pinned_to_its_top()
    {
        // Kısılamamış (ör. yükseklik henüz uygulanmadı) çok uzun kart: üstü çalışma alanının üstünde durur.
        Assert.Equal(-Margin, WorkAreaFit.KeepBottomInside(300, 2000, Work, Margin));
    }

    [Fact]
    public void Resizing_stops_at_the_work_area_edges()
    {
        var start = new Box(-900, 200, -500, 700);
        var grown = WorkAreaFit.ClampResize(start, new Box(-900, -50, -500, 1300), top: true, bottom: true, Work);
        Assert.Equal(new Box(-900, 0, -500, 1020), grown);
        // Yalnızca çekilen kenar sınırlanır.
        var onlyBottom = WorkAreaFit.ClampResize(start, new Box(-900, -50, -500, 1300), top: false, bottom: true, Work);
        Assert.Equal(-50, onlyBottom.Top);
    }

    [Fact]
    public void Resizing_a_card_that_is_already_outside_does_not_snap_it_back()
    {
        var hanging = new Box(-900, 800, -500, 1150); // başka ekranda seçilmiş boy: altı taşıyor
        var shrunk = WorkAreaFit.ClampResize(hanging, new Box(-900, 800, -500, 1100), top: false, bottom: true, Work);
        Assert.Equal(1100, shrunk.Bottom); // küçültmeye izin var
        var grown = WorkAreaFit.ClampResize(hanging, new Box(-900, 800, -500, 1200), top: false, bottom: true, Work);
        Assert.Equal(1150, grown.Bottom); // daha da dışarı büyümez
    }

    // --- Simge dosyaları ---

    [Fact]
    public void Folder_icons_have_a_frame_for_every_common_scale()
    {
        byte[]? ico = null;
        Exception? error = null;
        // WPF çizimi STA iş parçacığında yapılır.
        var thread = new Thread(() =>
        {
            try { ico = FolderIconRenderer.ToIco(FolderIconRenderer.FolderDrawing(FolderIconCatalog.Colors[0], '\uE8B7')); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));
        Assert.Null(error);
        Assert.NotNull(ico);
        Assert.Equal(FolderIconRenderer.IcoSizes.Order(), IcoFrameSizes(ico!).Order());
        // %100–%200'de Gezgin'in istediği boyutlar (%125: 20, 40; %200: 32, 64, 96…).
        Assert.Superset(new HashSet<int> { 16, 20, 24, 32, 40, 48, 64, 96, 128, 256 }, IcoFrameSizes(ico!).ToHashSet());
    }

    [Fact]
    public void App_icon_has_a_frame_for_every_common_scale()
    {
        var sizes = IcoFrameSizes(File.ReadAllBytes(Path.Combine(RepoRoot(), "src", "Duzenleme", "Assets", "app.ico")));
        // Tepsi (%100–%200: 16/20/24/28/32), başlık/görev çubuğu (24–48), büyük simgeler (64/96/128/256).
        Assert.Superset(new HashSet<int> { 16, 20, 24, 28, 32, 36, 40, 48, 64, 96, 128, 256 }, sizes.ToHashSet());
    }

    [Fact]
    public void Opaque_palettes_can_use_cleartype_but_glass_cannot()
    {
        bool? glass = null, dark = null, light = null, note = null, graphite = null;
        var thread = new Thread(() =>
        {
            glass = WidgetPalette.Glass.IsOpaqueBackground;
            dark = WidgetPalette.Dark.IsOpaqueBackground;
            light = WidgetPalette.Light.IsOpaqueBackground;
            note = WidgetPalette.ForNote(NoteColor.Yellow).IsOpaqueBackground;
            graphite = WidgetPalette.ForNote(NoteColor.Graphite).IsOpaqueBackground;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));
        Assert.Equal(false, glass);
        Assert.Equal(true, dark);
        Assert.Equal(true, light);
        Assert.Equal(true, note);
        Assert.Equal(true, graphite);
    }

    // --- 2.2 Tema ve yüksek karşıtlık ---

    [Fact]
    public void Themed_palettes_are_opaque_so_text_can_use_cleartype() => OnSta(() =>
    {
        foreach (var preset in ThemePresets.All)
            foreach (var dark in new[] { true, false })
            {
                var config = new WidgetConfig { Kind = WidgetKind.Fence };
                WidgetLooks.SetTheme(config, preset.Id, dark);
                var palette = WidgetPalette.Resolve(config, null, dark, highContrast: false);
                Assert.True(palette.IsOpaqueBackground, preset.Id);
                Assert.False(palette.TextShadow);
                Assert.Equal(preset.Variant(dark).IsLight, palette.IsLight);
                Assert.Equal(ThemeMath.ToHex(preset.Accent), Hex(palette.Accent));
            }
        var auto = new WidgetConfig { LookTheme = ThemePresets.Auto, Style = WidgetStyle.Glass };
        Assert.True(WidgetPalette.Resolve(auto, null, systemDark: true, highContrast: false).IsOpaqueBackground);
        Assert.False(WidgetPalette.Resolve(auto, null, systemDark: true, highContrast: false).IsLight);
        Assert.True(WidgetPalette.Resolve(auto, null, systemDark: false, highContrast: false).IsLight);
    });

    [Fact]
    public void Classic_palettes_look_exactly_as_before() => OnSta(() =>
    {
        // Tema ve özel renk yokken 2.1'in renkleri (WidgetPalette.For) değişmez; tanınmayan tema da klasik çizilir.
        foreach (var style in Enum.GetValues<WidgetStyle>())
            foreach (var accent in Enum.GetValues<WidgetAccent>())
                foreach (var theme in new string?[] { null, "gelecek-tema" })
                {
                    var before = WidgetPalette.For(style, accent);
                    var now = WidgetPalette.Resolve(new WidgetConfig { Style = style, Accent = accent, LookTheme = theme }, null, true, false);
                    Assert.Equal(Hex(before.Accent), Hex(now.Accent));
                    Assert.Equal(Hex(before.Foreground), Hex(now.Foreground));
                    Assert.Equal(Hex(before.AccentForeground), Hex(now.AccentForeground));
                    Assert.Same(before.Background, now.Background);
                    Assert.Equal(before.TextShadow, now.TextShadow);
                }
    });

    [Fact]
    public void Custom_accent_stays_readable_on_every_background() => OnSta(() =>
    {
        foreach (var hex in new[] { "#FFFFFF", "#000000", "#3E63DD", "#FFE066", "#0D9488" })
            foreach (var style in Enum.GetValues<WidgetStyle>())
                foreach (var theme in new string?[] { null, "neon", "gece", "kagit", "okyanus" })
                {
                    var config = new WidgetConfig { Style = style, LookTheme = theme, LookAccentColor = hex };
                    var palette = WidgetPalette.Resolve(config, null, systemDark: true, highContrast: false);
                    var accent = Argb(palette.Accent);
                    foreach (var stop in Stops(palette.Background))
                        Assert.True(ThemeMath.Contrast(accent, ThemeMath.Opaque(stop)) >= ThemeMath.AccentContrast - 0.05,
                            $"{hex} {style} {theme}: {ThemeMath.Contrast(accent, ThemeMath.Opaque(stop)):0.00}");
                    Assert.True(ThemeMath.Contrast(Argb(palette.AccentForeground), accent) >= 4.5);
                }
    });

    [Fact]
    public void Preview_of_a_background_or_named_accent_wins_over_theme_and_custom_color() => OnSta(() =>
    {
        var config = new WidgetConfig { Kind = WidgetKind.Fence };
        WidgetLooks.SetTheme(config, "kagit", systemDark: false);
        var themed = WidgetPalette.Resolve(config, null, false, false);
        Assert.True(themed.IsLight);

        // "Arka plan ▸ Koyu" üstüne gelince: seçilince olacağı gibi tema kalkar (vurgu rengi kalır).
        var dark = WidgetPalette.Resolve(config, new LookOverride(Style: WidgetStyle.Dark), false, false);
        Assert.False(dark.IsLight);
        Assert.Same(WidgetPalette.Dark.Background, dark.Background);

        // "Vurgu rengi ▸ Pembe": özel renk (temanın kehribarı) yerine pembe.
        var pink = WidgetPalette.Resolve(config, new LookOverride(Accent: WidgetAccent.Pink), false, false);
        Assert.Equal(WidgetAccent.Pink, ThemeMath.NearestAccent(Argb(pink.Accent)));

        // "Tema ▸ Klasik": tema ve onunla gelen renk kalkar.
        var classic = WidgetPalette.Resolve(config, LookMenus.ThemePreview(""), false, false);
        Assert.Equal(Hex(WidgetPalette.For(config.Style, config.Accent).Accent), Hex(classic.Accent));

        // Özel renk önizlemesi.
        var custom = WidgetPalette.Resolve(config, new LookOverride(AccentColor: "#0D9488"), false, false);
        Assert.Equal(ThemeMath.EnsureContrast(0xFF0D9488, Stops(themed.Background), ThemeMath.AccentContrast), Argb(custom.Accent));

        // Önizleme kaydedilmez.
        Assert.Equal("kagit", config.LookTheme);
        Assert.Equal("#B45309", config.LookAccentColor);
    });

    [Fact]
    public void High_contrast_palette_is_opaque_plain_and_from_windows() => OnSta(() =>
    {
        var palette = WidgetPalette.HighContrast();
        Assert.True(palette.IsHighContrast);
        Assert.True(palette.IsOpaqueBackground);
        Assert.False(palette.TextShadow);
        Assert.Equal(Hex(System.Windows.SystemColors.WindowBrush), Hex(palette.Background));
        Assert.Equal(Hex(System.Windows.SystemColors.WindowTextBrush), Hex(palette.Foreground));
        Assert.Equal(Hex(System.Windows.SystemColors.HighlightBrush), Hex(palette.Accent));
        Assert.Equal(Hex(System.Windows.SystemColors.HighlightTextBrush), Hex(palette.AccentForeground));
        // Yarı saydam katmanlar yok: hafif dolgular saydam, alanlar ve "Bırak" katmanı kartın opak zemini.
        Assert.Equal(0, ((System.Windows.Media.SolidColorBrush)palette.Tint(0x26)).Color.A);
        Assert.Same(palette.Background, palette.Field(0x22));
        Assert.Same(palette.Background, palette.Overlay);
        // Yüksek karşıtlık dışındaki paletlerde eski katmanlar.
        Assert.Equal(0x26, ((System.Windows.Media.SolidColorBrush)WidgetPalette.Glass.Tint(0x26)).Color.A);
    });

    [Fact]
    public void Windows_look_changes_decide_which_widgets_are_redrawn()
    {
        WidgetPalette.SystemLook light = new(false, false, ""), dark = new(true, false, "");
        WidgetPalette.SystemLook nightSky = new(false, true, "#FF000000,#FFFFFFFF"), desert = new(false, true, "#FFFFFAEF,#FF3D3D3D");
        Assert.Equal((false, false), WidgetPalette.SystemLook.Change(null, light));     // ilk bakış: pencereler zaten böyle açılır
        Assert.Equal((false, false), WidgetPalette.SystemLook.Change(light, light));
        Assert.Equal((true, false), WidgetPalette.SystemLook.Change(light, dark));      // yalnızca Windows'a uyanlar
        Assert.Equal((true, true), WidgetPalette.SystemLook.Change(dark, nightSky));    // yüksek karşıtlık açıldı
        Assert.Equal((true, true), WidgetPalette.SystemLook.Change(nightSky, light));   // kapandı
        // Bir yüksek karşıtlık temasından ötekine: açık/koyu ve yüksek karşıtlık değişmez, yalnızca renkler; hepsi yeniden çizilir.
        Assert.Equal((true, true), WidgetPalette.SystemLook.Change(nightSky, desert));
        Assert.Equal((false, false), WidgetPalette.SystemLook.Change(desert, desert));

        // Renkler yalnızca yüksek karşıtlıkta okunur (Windows'un sıradan renk değişikliği widget'ları yeniden çizdirmez).
        Assert.Equal("", WidgetPalette.SystemLook.Current(true, false).HighContrastColors);
        Assert.NotEqual("", WidgetPalette.SystemLook.Current(false, true).HighContrastColors);
    }

    [Fact]
    public void Widget_chrome_is_swapped_as_one_dictionary_and_only_when_it_changed() => OnSta(() =>
    {
        var card = new System.Windows.Controls.Border();
        var green = WidgetPalette.For(WidgetStyle.Dark, WidgetAccent.Green);
        WidgetChrome.Apply(card.Resources, green);
        var first = Assert.Single(card.Resources.MergedDictionaries);
        Assert.Equal(WidgetChrome.Keys.Order(), first.Keys.Cast<string>().Order());
        Assert.Empty(card.Resources.Keys); // kartın kendi kaynaklarına tek tek yazılmaz

        WidgetChrome.Apply(card.Resources, WidgetPalette.For(WidgetStyle.Dark, WidgetAccent.Green));
        Assert.Same(first, Assert.Single(card.Resources.MergedDictionaries)); // aynı değerler: dokunulmaz

        WidgetChrome.Apply(card.Resources, WidgetPalette.HighContrast());
        var hc = Assert.Single(card.Resources.MergedDictionaries);
        Assert.NotSame(first, hc);
        Assert.Equal(1.0, (double)card.Resources[WidgetChrome.GlyphRestOpacity]); // sönük × ve "+" yüksek karşıtlıkta tam görünür
        WidgetChrome.Apply(card.Resources, green);
        Assert.Equal(0.55, (double)card.Resources[WidgetChrome.GlyphRestOpacity]);

        // Sönük × (TileStyles) ve "Yeni madde ekle" artısı (NoteView) saydamlığını buradan alır.
        foreach (var file in new[] { "TileStyles.xaml", "NoteView.xaml" })
            Assert.Contains("{DynamicResource WidgetGlyphRestOpacity}",
                File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "Widgets", file)));
    });

    [Fact]
    public void High_contrast_overrides_themes_and_notes() => OnSta(() =>
    {
        var note = new WidgetConfig { Kind = WidgetKind.Note, NoteColor = NoteColor.Pink };
        Func<WidgetPalette, WidgetPalette> paper = _ => WidgetPalette.ForNote(note.NoteColor); // NoteView.AdjustPalette gibi
        Assert.False(WidgetPalette.ForWidget(note, null, false, highContrast: false, paper).IsHighContrast);
        Assert.True(WidgetPalette.ForWidget(note, null, false, highContrast: true, paper).IsHighContrast);
        Assert.True(WidgetPalette.ForWidget(note, new LookOverride(Note: NoteColor.Green), false, highContrast: true, paper).IsHighContrast);

        var themed = new WidgetConfig { Kind = WidgetKind.Fence, Style = WidgetStyle.Glass };
        WidgetLooks.SetTheme(themed, "neon", systemDark: true);
        WidgetLooks.SetAccentColor(themed, "#E5484D");
        var hc = WidgetPalette.ForWidget(themed, LookMenus.ThemePreview("okyanus"), true, highContrast: true, p => p);
        Assert.True(hc.IsHighContrast);
        Assert.True(hc.IsOpaqueBackground);
        Assert.False(hc.TextShadow);
        Assert.Equal(Hex(System.Windows.SystemColors.HighlightBrush), Hex(hc.Accent));
    });

    [Fact]
    public void Widget_chrome_resources_have_defaults_and_no_translucent_fill_in_high_contrast() => OnSta(() =>
    {
        var styles = XDocument.Load(Path.Combine(RepoRoot(), "src", "Duzenleme", "Widgets", "TileStyles.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var defaults = styles.Root!.Elements().Select(e => (string?)e.Attribute(x + "Key")).OfType<string>().ToHashSet();
        Assert.All(WidgetChrome.Keys, key => Assert.Contains(key, defaults));
        Assert.Equal(WidgetChrome.Keys.Order(), WidgetChrome.For(WidgetPalette.Dark).Select(r => r.Key).Order());

        var hc = WidgetChrome.For(WidgetPalette.HighContrast()).ToDictionary(r => r.Key, r => r.Value);
        Assert.Equal(WidgetChrome.Keys.Order(), hc.Keys.Order());
        foreach (var fill in new[] { WidgetChrome.TileHover, WidgetChrome.TileSelected, WidgetChrome.ButtonFill, WidgetChrome.PillFill })
            Assert.Equal(0, ((System.Windows.Media.SolidColorBrush)hc[fill]).Color.A);
        foreach (var line in new[] { WidgetChrome.TileSelectedBorder, WidgetChrome.TileHoverBorder, WidgetChrome.ButtonOutline })
            Assert.Equal(255, ((System.Windows.Media.SolidColorBrush)hc[line]).Color.A);
        Assert.Equal(System.Windows.SystemColors.HighlightColor, (System.Windows.Media.Color)hc[WidgetChrome.CloseHover]);
        Assert.Equal(System.Windows.SystemColors.HighlightTextColor, (System.Windows.Media.Color)hc[WidgetChrome.CloseGlyphHover]);

        // Kutucuk seçimi vurgu rengini izler.
        var selected = (System.Windows.Media.SolidColorBrush)WidgetChrome.For(WidgetPalette.For(WidgetStyle.Dark, WidgetAccent.Green))
            .Single(r => r.Key == WidgetChrome.TileSelected).Value;
        Assert.Equal(ThemeMath.AccentArgb(WidgetAccent.Green, onLight: false) & 0xFFFFFF, Argb(selected) & 0xFFFFFF);
    });

    [Fact]
    public void Tile_styles_take_their_colors_from_the_card() => OnSta(() =>
    {
        // TileStyles.xaml gerçekten yüklenir (BAML): şablonlar renklerini DynamicResource ile kartın kaynaklarından alır.
        var styles = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri("/NestDesk;component/Widgets/TileStyles.xaml", UriKind.Relative));
        foreach (var palette in new[] { WidgetPalette.For(WidgetStyle.Dark, WidgetAccent.Green), WidgetPalette.HighContrast() })
        {
            var card = new System.Windows.Controls.Border();
            card.Resources.MergedDictionaries.Add(styles);
            WidgetChrome.Apply(card.Resources, palette);
            var chrome = WidgetChrome.For(palette).ToDictionary(r => r.Key, r => r.Value);

            var list = new System.Windows.Controls.ListBox { ItemContainerStyle = (System.Windows.Style)styles["TileContainer"] };
            var item = new System.Windows.Controls.ListBoxItem { Content = "PDF", Style = (System.Windows.Style)styles["TileContainer"] };
            list.Items.Add(item);
            var close = new System.Windows.Controls.Button { Style = (System.Windows.Style)styles["WidgetCloseButton"] };
            var text = new System.Windows.Controls.Button { Style = (System.Windows.Style)styles["WidgetTextButton"], Content = "+1" };
            card.Child = new System.Windows.Controls.StackPanel { Children = { list, close, text } };
            card.Measure(new System.Windows.Size(400, 400));
            card.Arrange(new System.Windows.Rect(0, 0, 400, 400));
            card.UpdateLayout();

            item.IsSelected = true;
            var bg = (System.Windows.Controls.Border)item.Template.FindName("Bg", item);
            Assert.Equal(Hex((System.Windows.Media.Brush)chrome[WidgetChrome.TileSelected]), Hex(bg.Background));
            Assert.Equal(Hex((System.Windows.Media.Brush)chrome[WidgetChrome.TileSelectedBorder]), Hex(bg.BorderBrush));

            var fills = Descendants(close).Select(d => Views.Motion.GetHoverFill(d)).ToList();
            Assert.Contains((System.Windows.Media.Color)chrome[WidgetChrome.CloseHover], fills);
            Assert.Contains((System.Windows.Media.Color)chrome[WidgetChrome.CloseGlyphHover], fills);
            Assert.Equal(Hex((System.Windows.Media.Brush)chrome[WidgetChrome.ButtonFill]), Hex(text.Background));
        }
    });

    private static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var below in Descendants(child)) yield return below;
        }
    }

    [Fact]
    public void Audited_widget_views_take_colors_from_the_palette()
    {
        // Yüksek karşıtlık denetiminden geçen dosyalarda sabit renk kalmaz (renkler paletten ya da WidgetChrome kaynaklarından).
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme", "Widgets");
        var offenders = new List<string>();
        foreach (var name in new[] { "FenceView.xaml.cs", "LauncherView.xaml.cs", "TimerView.xaml.cs", "TitleEditor.cs" })
        {
            var lines = File.ReadAllLines(Path.Combine(src, name));
            for (var i = 0; i < lines.Length; i++)
                if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"Color\.From(A?)Rgb\(|Brushes\.(White|Black|MediumPurple)\b"))
                    offenders.Add($"{name}:{i + 1}: {lines[i].Trim()}");
        }
        // XAML: kök kaynaklar (varsayılanlar) dışında "#RRGGBB" yok.
        foreach (var name in new[] { "TileStyles.xaml", "TimerView.xaml" })
        {
            var document = XDocument.Load(Path.Combine(src, name), LoadOptions.SetLineInfo);
            var resources = document.Root!.Elements().Where(e => e.Name.LocalName is "SolidColorBrush" or "Color").ToHashSet();
            foreach (var element in document.Root.Descendants().Where(e => !resources.Contains(e)))
                foreach (var attribute in element.Attributes())
                    if (System.Text.RegularExpressions.Regex.IsMatch(attribute.Value, "^#[0-9A-Fa-f]{6,8}$"))
                        offenders.Add($"{name}:{((System.Xml.IXmlLineInfo)element).LineNumber}: {attribute}");
        }
        Assert.True(offenders.Count == 0, "Sabit renk:\n" + string.Join("\n", offenders));
    }

    private static string Hex(System.Windows.Media.Brush brush) => ThemeMath.ToHex(Argb(brush));

    private static uint Argb(System.Windows.Media.Brush brush) => brush is System.Windows.Media.SolidColorBrush s
        ? (uint)s.Color.A << 24 | (uint)s.Color.R << 16 | (uint)s.Color.G << 8 | s.Color.B
        : throw new InvalidOperationException("düz renk değil");

    private static uint[] Stops(System.Windows.Media.Brush brush) => brush switch
    {
        System.Windows.Media.GradientBrush g => g.GradientStops
            .Select(x => (uint)x.Color.A << 24 | (uint)x.Color.R << 16 | (uint)x.Color.G << 8 | x.Color.B).ToArray(),
        _ => [Argb(brush)],
    };

    /// <summary>WPF nesneleri STA iş parçacığında kurulur (uygulama nesnesi yok).</summary>
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

    /// <summary>.ico dizinindeki karelerin boyutları (0 = 256).</summary>
    private static List<int> IcoFrameSizes(byte[] ico)
    {
        Assert.Equal(1, BitConverter.ToUInt16(ico, 2)); // simge türü
        var count = BitConverter.ToUInt16(ico, 4);
        return Enumerable.Range(0, count).Select(i => ico[6 + 16 * i] is 0 ? 256 : (int)ico[6 + 16 * i]).ToList();
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
    }
}
