using System.Text.RegularExpressions;
using System.Xml.Linq;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Hareket 2.0 (2.2): düzey (Kapalı / Sakin / Canlı), planların sayıları ve sınırları (<see cref="MotionPolicy"/>) ve kaynak
/// taramaları. Animasyon yalnızca Views/Motion.cs'ten açılır; döngü, tekrar, pencere sınırı oynatma, widget'larda efekt ve
/// fareyle görünürlük değişimi yok.
/// </summary>
public class MotionTests
{
    private static readonly MotionKind[] Kinds = Enum.GetValues<MotionKind>();
    private static readonly MotionSurface[] Surfaces = Enum.GetValues<MotionSurface>();
    private static readonly MotionLevel[] Levels = Enum.GetValues<MotionLevel>();

    /// <summary>Bütün düzey × tür × yüzey × kalabalık × piksel bileşimlerinin planları (null olmayanlar).</summary>
    private static IEnumerable<(MotionLevel Level, MotionKind Kind, MotionSurface Surface, int Active, long Pixels, MotionPlan Plan)> AllPlans()
    {
        foreach (var level in Levels)
        foreach (var kind in Kinds)
        foreach (var surface in Surfaces)
        foreach (var active in new[] { 0, 1, 3, 4, 8, 9, 12, 13, 40 })
        foreach (var pixels in new[] { 0L, 400_000L, MotionPolicy.MaxPopPixels, MotionPolicy.MaxPopPixels + 1, 4_000_000L })
            if (MotionPolicy.Plan(level, kind, surface, active, pixels) is { } plan)
                yield return (level, kind, surface, active, pixels, plan);
    }

    // ---------------------------------------------------------------- düzey

    [Theory]
    //          windows appOff calm  saver  remote  beklenen
    [InlineData(true,  false, false, false, false, MotionLevel.Lively)]
    [InlineData(true,  false, true,  false, false, MotionLevel.Calm)]
    [InlineData(true,  false, false, true,  false, MotionLevel.Calm)]    // enerji tasarrufu Sakin'e indirir
    [InlineData(true,  false, false, false, true,  MotionLevel.Calm)]    // uzak masaüstü Sakin'e indirir
    [InlineData(true,  true,  false, false, false, MotionLevel.Off)]     // uygulamada Kapalı
    [InlineData(true,  true,  true,  true,  true,  MotionLevel.Off)]     // AnimationsOff, CalmMotion'dan baskın
    [InlineData(false, false, false, false, false, MotionLevel.Off)]     // Windows'ta "Animasyon efektleri" kapalı: seçim ne olursa olsun
    [InlineData(false, false, true,  false, false, MotionLevel.Off)]
    [InlineData(false, true,  false, true,  true,  MotionLevel.Off)]
    public void Level_truth_table(bool windows, bool appOff, bool calm, bool saver, bool remote, MotionLevel expected) =>
        Assert.Equal(expected, MotionPolicy.Level(windows, appOff, calm, saver, remote));

    [Fact]
    public void Windows_animation_effects_off_always_means_off()
    {
        foreach (var appOff in new[] { false, true })
        foreach (var calm in new[] { false, true })
        foreach (var saver in new[] { false, true })
        foreach (var remote in new[] { false, true })
            Assert.Equal(MotionLevel.Off, MotionPolicy.Level(false, appOff, calm, saver, remote));
    }

    [Theory]
    [InlineData(false, false, MotionLevel.Lively)]
    [InlineData(false, true, MotionLevel.Calm)]
    [InlineData(true, false, MotionLevel.Off)]
    [InlineData(true, true, MotionLevel.Off)]
    public void Settings_choice_reads_flags(bool animationsOff, bool calmMotion, MotionLevel expected) =>
        Assert.Equal(expected, MotionPolicy.Chosen(animationsOff, calmMotion));

    [Fact]
    public void Settings_choice_writes_flags_and_round_trips()
    {
        Assert.Equal((false, false), MotionPolicy.Flags(MotionLevel.Lively, calmNow: true));
        Assert.Equal((false, true), MotionPolicy.Flags(MotionLevel.Calm, calmNow: false));
        // Kapalı yalnızca AnimationsOff'u açar; Sakin seçimi korunur (Kapalı'dan çıkınca 2.1 ile gidip gelen kullanıcı kaybetmez).
        Assert.Equal((true, true), MotionPolicy.Flags(MotionLevel.Off, calmNow: true));
        Assert.Equal((true, false), MotionPolicy.Flags(MotionLevel.Off, calmNow: false));
        foreach (var choice in Levels)
        foreach (var calmNow in new[] { false, true })
        {
            var (off, calm) = MotionPolicy.Flags(choice, calmNow);
            Assert.Equal(choice, MotionPolicy.Chosen(off, calm));
        }
    }

    // ---------------------------------------------------------------- planlar

    [Fact]
    public void Off_is_instant_everywhere()
    {
        foreach (var kind in Kinds)
        foreach (var surface in Surfaces)
        {
            Assert.Null(MotionPolicy.Plan(MotionLevel.Off, kind, surface));
            Assert.Null(MotionPolicy.Plan(MotionLevel.Off, kind, surface, 1, 1000, out var note));
            Assert.Equal("Off", note);
        }
    }

    [Fact]
    public void Calm_is_a_150ms_fade_and_never_hover_press_attention_or_tile_enter()
    {
        foreach (var kind in Kinds)
        foreach (var surface in Surfaces)
        {
            var plan = MotionPolicy.Plan(MotionLevel.Calm, kind, surface, 1);
            if (kind is MotionKind.Hover or MotionKind.Press or MotionKind.Attention or MotionKind.TileEnter)
            {
                Assert.Null(plan);
                continue;
            }
            Assert.NotNull(plan);
            Assert.Equal(MotionKind.Fade, plan.Kind);
            Assert.Equal(MotionPolicy.FadeMs, plan.Ms);
            Assert.Equal(150, plan.Ms);
            Assert.True(plan.Fade);
            Assert.False(plan.Moves);
            Assert.NotEqual(MotionEase.Overshoot, plan.Ease);
        }
    }

    [Fact]
    public void Lively_kinds_have_their_numbers()
    {
        var pop = MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Pop, MotionSurface.LayeredWidget, 1, 200_000)!;
        Assert.Equal((MotionKind.Pop, MotionPolicy.EnterMs, MotionPolicy.PopScale, true), (pop.Kind, pop.Ms, pop.Scale, pop.Fade));
        var exit = MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Exit, MotionSurface.LayeredWidget, 1, 200_000)!;
        Assert.Equal((MotionKind.Exit, MotionPolicy.ExitMs, MotionPolicy.ExitScale), (exit.Kind, exit.Ms, exit.Scale));
        var page = MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Page, MotionSurface.Window)!;
        Assert.Equal((MotionPolicy.PageMs, MotionPolicy.PageDip, 1.0), (page.Ms, page.OffsetY, page.Scale));
        Assert.True(page.OffsetY > 0);                                                     // tek yön: aşağıdan
        Assert.True(MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Rise, MotionSurface.Window)!.OffsetY > 0);
        Assert.True(MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Drop, MotionSurface.Window)!.OffsetY < 0);
        var press = MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Press, MotionSurface.Window)!;
        Assert.Equal((MotionPolicy.PressDownMs, MotionPolicy.PressUpMs, MotionPolicy.PressScale, false), (press.Ms, press.ReturnMs, press.Scale, press.Fade));
        var tile = MotionPolicy.Plan(MotionLevel.Lively, MotionKind.TileEnter, MotionSurface.LayeredWidget, 1, 200_000)!;
        Assert.False(tile.Fade);                                                           // ClearType'lı yazı solmaz: yalnızca ölçek
        Assert.Equal(MotionPolicy.TileEnterScale, tile.Scale);
        var hover = MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Hover, MotionSurface.LayeredWidget, 1)!;
        Assert.False(hover.Moves);
        Assert.False(hover.Fade);
        var layeredHover = MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Hover, MotionSurface.LayeredWidget, 1) is not null;
        Assert.True(layeredHover == MotionPolicy.LayeredHoverFade);                       // kapatma düğmesi yalnızca widget'larda
    }

    [Fact]
    public void Pop_downgrades_by_count_and_pixels_on_layered_widgets()
    {
        MotionPlan? Pop(int active, long pixels = 200_000) =>
            MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Pop, MotionSurface.LayeredWidget, active, pixels);

        for (var n = 1; n <= MotionPolicy.MaxPoppedWindows; n++) Assert.Equal(MotionKind.Pop, Pop(n)!.Kind);
        var crowded = Pop(MotionPolicy.MaxPoppedWindows + 1)!;
        Assert.Equal(MotionKind.Fade, crowded.Kind);                                       // fazlası yalnızca solar
        Assert.False(crowded.Moves);
        Assert.Equal(MotionKind.Fade, Pop(MotionPolicy.MaxFadedWindows)!.Kind);
        Assert.Null(Pop(MotionPolicy.MaxFadedWindows + 1));                                 // daha çoğu hemen
        Assert.Null(MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Pop, MotionSurface.LayeredWidget, MotionPolicy.MaxFadedWindows + 1, 0, out var note));
        Assert.Equal("Crowd", note);
        Assert.Null(MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Hover, MotionSurface.LayeredWidget, MotionPolicy.MaxActiveLayered + 1, 0, out note));
        Assert.Equal("Busy", note);

        Assert.Equal(MotionKind.Pop, Pop(1, MotionPolicy.MaxPopPixels)!.Kind);
        var large = MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Pop, MotionSurface.LayeredWidget, 1, MotionPolicy.MaxPopPixels + 1, out note)!;
        Assert.Equal(MotionKind.Fade, large.Kind);                                         // çok büyük kart yalnızca solar
        Assert.Equal("Large", note);
        Assert.Null(MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Attention, MotionSurface.LayeredWidget, 1, MotionPolicy.MaxPopPixels + 1));
        Assert.Null(MotionPolicy.Plan(MotionLevel.Lively, MotionKind.TileEnter, MotionSurface.LayeredWidget, 1, MotionPolicy.MaxPopPixels + 1));
        Assert.False(MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Exit, MotionSurface.LayeredWidget, 1, MotionPolicy.MaxPopPixels + 1)!.Moves);

        // Donanımla çizilen pencerede sayı ve piksel sınırı yok.
        Assert.Equal(MotionKind.Pop, MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Pop, MotionSurface.Window, 40, 40_000_000)!.Kind);
    }

    [Fact]
    public void Overshoot_only_on_the_ring()
    {
        foreach (var (_, _, surface, _, _, plan) in AllPlans())
            if (plan.Ease == MotionEase.Overshoot) Assert.Equal(MotionSurface.Ring, surface);
        Assert.Equal(MotionEase.Overshoot, MotionPolicy.Plan(MotionLevel.Lively, MotionKind.RingOpen, MotionSurface.Ring)!.Ease);
        Assert.Equal(MotionEase.Overshoot, MotionPolicy.Plan(MotionLevel.Lively, MotionKind.RingSwap, MotionSurface.Ring)!.Ease);
        Assert.NotEqual(MotionEase.Overshoot, MotionPolicy.Plan(MotionLevel.Lively, MotionKind.RingOpen, MotionSurface.LayeredWidget, 1)!.Ease);
        Assert.NotEqual(MotionEase.Overshoot, MotionPolicy.Plan(MotionLevel.Calm, MotionKind.RingOpen, MotionSurface.Ring)!.Ease);
        Assert.InRange(MotionPolicy.OvershootAmplitude, 0.1, 0.5);
    }

    [Fact]
    public void Scales_stay_in_each_surface_range()
    {
        Assert.Equal(0.9, MotionPolicy.MinScale(MotionSurface.Window));
        Assert.Equal(0.9, MotionPolicy.MinScale(MotionSurface.LayeredWidget));
        Assert.Equal(0.8, MotionPolicy.MinScale(MotionSurface.Ring));
        foreach (var (_, _, surface, _, _, plan) in AllPlans())
            Assert.InRange(plan.Scale, MotionPolicy.MinScale(surface), 1.0);
        foreach (var scale in new[] { MotionPolicy.PopScale, MotionPolicy.ExitScale, MotionPolicy.PressScale, MotionPolicy.AttentionScale, MotionPolicy.TileEnterScale })
            Assert.InRange(scale, MotionPolicy.MinWindowScale, 1.0);
        foreach (var scale in new[] { MotionPolicy.RingItemScale, MotionPolicy.RingCenterScale, MotionPolicy.RingCloseScale })
            Assert.InRange(scale, MotionPolicy.MinRingScale, 1.0);
        foreach (var offset in new[] { MotionPolicy.RiseDip, MotionPolicy.PageDip, MotionPolicy.DropDip })
            Assert.InRange(offset, 1, 16);
        Assert.InRange(MotionPolicy.RingTravel, 0, 0.5);
    }

    [Fact]
    public void Every_duration_is_short()
    {
        Assert.Equal(300, MotionPolicy.MaxMs);
        foreach (var ms in new[] { MotionPolicy.FadeMs, MotionPolicy.EnterMs, MotionPolicy.ExitMs, MotionPolicy.PageMs, MotionPolicy.HoverMs,
                     MotionPolicy.PressDownMs, MotionPolicy.PressUpMs, MotionPolicy.AttentionMs, MotionPolicy.GlowMs, MotionPolicy.RingOpenMs,
                     MotionPolicy.RingSwapMs, MotionPolicy.RingCloseMs })
            Assert.InRange(ms, 1, MotionPolicy.MaxMs);
        Assert.InRange(MotionPolicy.HoverMs, 1, 120);
        Assert.True(MotionPolicy.ExitMs < MotionPolicy.EnterMs);
        Assert.True(MotionPolicy.RingCloseMs < MotionPolicy.RingOpenMs);
        Assert.True(MotionPolicy.AttentionGapMs >= 1000);                                  // en çok saniyede bir nabız
        foreach (var (_, _, _, _, _, plan) in AllPlans())
        {
            Assert.InRange(plan.TotalMs, 1, MotionPolicy.MaxMs);
            Assert.InRange(plan.ReturnMs, 0, MotionPolicy.MaxMs);
        }
        Assert.Equal(MotionPolicy.MaxMs, MotionPolicy.Duration(1000));
        Assert.Equal(0, MotionPolicy.Duration(-5));
        Assert.Equal(120, MotionPolicy.Duration(120));
    }

    [Fact]
    public void Cascades_fit_in_max_duration()
    {
        foreach (var kind in new[] { MotionKind.Pop, MotionKind.Rise, MotionKind.TileEnter, MotionKind.Page, MotionKind.RingOpen, MotionKind.RingSwap })
        foreach (var surface in Surfaces)
        {
            var plan = MotionPolicy.Plan(MotionLevel.Lively, kind, surface, 1);
            if (plan is null) continue;
            var previousDelay = -1;
            for (var i = 0; i < 40; i++)
            {
                var item = MotionPolicy.Stagger(MotionLevel.Lively, plan, i);
                Assert.InRange(item.TotalMs, 1, MotionPolicy.MaxMs);
                Assert.True(item.DelayMs >= previousDelay);
                previousDelay = item.DelayMs;
            }
            var ring = plan.Kind is MotionKind.RingOpen or MotionKind.RingSwap;
            var last = MotionPolicy.Stagger(MotionLevel.Lively, plan, 1000);
            Assert.Equal(ring ? (MotionPolicy.RingStaggerMax - 1) * MotionPolicy.RingStaggerMs : (MotionPolicy.CascadeMax - 1) * MotionPolicy.CascadeStepMs, last.DelayMs);
        }
        // Sakin: hepsi birlikte solar (kademe yok).
        var calm = MotionPolicy.Plan(MotionLevel.Calm, MotionKind.Rise, MotionSurface.Window)!;
        Assert.Equal(0, MotionPolicy.Stagger(MotionLevel.Calm, calm, 7).DelayMs);
        // Elle verilen bekleme de sınırın içinde.
        var delayed = MotionPolicy.Delay(MotionPolicy.Plan(MotionLevel.Lively, MotionKind.Page, MotionSurface.Window)!, 5000);
        Assert.InRange(delayed.TotalMs, 1, MotionPolicy.MaxMs);
    }

    [Fact]
    public void Layered_widgets_run_at_30_fps_or_less()
    {
        Assert.InRange(MotionPolicy.WidgetFrameRate, 15, 30);
        Assert.InRange(MotionPolicy.WindowFrameRate, 30, 60);
        foreach (var (_, _, surface, _, _, plan) in AllPlans())
        {
            if (surface == MotionSurface.LayeredWidget) Assert.InRange(plan.Fps, 1, 30);
            else Assert.InRange(plan.Fps, 1, 60);
        }
        Assert.Equal(3, MotionPolicy.MaxPoppedWindows);
        Assert.Equal(8, MotionPolicy.MaxFadedWindows);
        Assert.Equal(12, MotionPolicy.MaxActiveLayered);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]   // Saydamlık efektleri kapalı
    [InlineData(true, true, false)]     // yüksek karşıtlık
    [InlineData(false, true, false)]
    public void Glow_needs_transparency_and_no_high_contrast(bool transparency, bool highContrast, bool expected) =>
        Assert.Equal(expected, MotionPolicy.GlowAllowed(transparency, highContrast));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Menus_fade_only_when_windows_menu_animation_is_on(bool levelAtLeastCalm, bool menuAnimation, bool expected) =>
        Assert.Equal(expected, MotionPolicy.MenuFade(levelAtLeastCalm, menuAnimation));

    [Fact]
    public void Only_a_few_widget_windows_fade_at_once()
    {
        Assert.False(MotionPolicy.FadeWindows(true, 0));
        Assert.True(MotionPolicy.FadeWindows(true, 1));
        Assert.True(MotionPolicy.FadeWindows(true, MotionPolicy.MaxFadedWindows));
        Assert.False(MotionPolicy.FadeWindows(true, MotionPolicy.MaxFadedWindows + 1));
        Assert.False(MotionPolicy.FadeWindows(false, 1));
    }

    // ---------------------------------------------------------------- ayar

    [Fact]
    public void Motion_settings_are_not_written_when_default()
    {
        var settings = new AppSettings();
        Assert.False(settings.AnimationsOff);
        Assert.False(settings.CalmMotion);
        var json = System.Text.Encoding.UTF8.GetString(JsonFile.Serialize(settings));
        Assert.DoesNotContain("AnimationsOff", json);
        Assert.DoesNotContain("CalmMotion", json);
        settings.AnimationsOff = true;
        settings.CalmMotion = true;
        json = System.Text.Encoding.UTF8.GetString(JsonFile.Serialize(settings));
        Assert.Contains("\"AnimationsOff\": true", json);
        Assert.Contains("\"CalmMotion\": true", json);
    }

    [Fact]
    public void Settings_from_21_with_animations_off_load_as_off()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-motion-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, """{ "FirstRunDone": true, "AnimationsOff": true }""");
            var s = JsonFile.Load(path, () => new AppSettings());
            Assert.True(s.AnimationsOff);
            Assert.False(s.CalmMotion);
            Assert.Equal(MotionLevel.Off, MotionPolicy.Chosen(s.AnimationsOff, s.CalmMotion));
            Assert.Equal(MotionLevel.Off, MotionPolicy.Level(true, s.AnimationsOff, s.CalmMotion, false, false));
            Assert.Empty(Directory.GetFiles(dir, "*.bozuk-*"));

            File.WriteAllText(path, """{ "FirstRunDone": true }""");
            s = JsonFile.Load(path, () => new AppSettings());
            Assert.Equal(MotionLevel.Lively, MotionPolicy.Chosen(s.AnimationsOff, s.CalmMotion));   // 2.1 varsayılanı → Canlı
        }
        finally
        {
            try { Directory.Delete(dir, true); }
            catch (IOException) { }
        }
    }

    // ---------------------------------------------------------------- kaynak taraması

    private static readonly Regex CodeAnimation =
        new(@"\bBeginAnimation\b|\bBeginStoryboard\b|\bStoryboard\b|\b\w+Animation(UsingKeyFrames|UsingPath)?\s*[({]|new\s+\w+Animation\b|\bTransitionAnimationProvider\b|\bVisualTransition\b");

    [Fact]
    public void Animations_only_through_Motion()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.cs"))
        {
            var relative = Path.GetRelativePath(SourceRoot, file);
            if (relative == Path.Combine("Views", "Motion.cs")) continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = StripComment(lines[i]);
                if (CodeAnimation.IsMatch(code)) offenders.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
            }
        }
        Assert.True(offenders.Count == 0, "Animasyon yalnızca Views/Motion.cs'ten açılır:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Xaml_has_no_storyboards_or_sliding_popups()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.xaml"))
        {
            var relative = Path.GetRelativePath(SourceRoot, file);
            var text = XamlWithoutComments(file);
            if (Regex.IsMatch(text, @"<(\w+:)?(Storyboard|BeginStoryboard|VisualTransition|(?!Popup)\w+Animation(UsingKeyFrames)?)\b"))
                offenders.Add($"{relative}: Storyboard/VisualTransition/Animation");
            // Açılır pencerelerde kayma (Slide/Scroll) yok; solmayı yalnızca Motion kodla açar.
            foreach (Match m in Regex.Matches(text, @"PopupAnimation(=""|"">|\"" Value=\"")(?<v>\w+)"))
                if (m.Groups["v"].Value != "None") offenders.Add($"{relative}: PopupAnimation {m.Groups["v"].Value}");
            foreach (Match m in Regex.Matches(text, @">(?<v>\w+)</PopupAnimation>"))
                if (m.Groups["v"].Value != "None") offenders.Add($"{relative}: PopupAnimation {m.Groups["v"].Value}");
            // Sayfa geçişi Motion.ApplyTo ile koddan: XAML'de yalnızca "None" başlangıç değeri.
            foreach (Match m in Regex.Matches(text, @"\bTransition=""(?<v>\w+)"""))
                if (m.Groups["v"].Value != "None") offenders.Add($"{relative}: Transition {m.Groups["v"].Value}");
        }
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    [Fact]
    public void Motion_never_touches_window_bounds()
    {
        // Yalnızca saydamlık, RenderTransform ve fırça oynar: pencere sınırı, LayoutTransform, kenar boşluğu asla.
        var path = Path.Combine(SourceRoot, "Views", "Motion.cs");
        var code = string.Join("\n", File.ReadAllLines(path).Select(StripComment));
        var offenders = Regex.Matches(code, @"\b(Left|Top|Width|Height|MinWidth|MinHeight|MaxWidth|MaxHeight|Margin|Padding)Property\b|\bLayoutTransform\b|\.(Left|Top|Width|Height|Margin)\s*=[^=]")
            .Select(m => m.Value).ToList();
        Assert.True(offenders.Count == 0, "Motion.cs pencere sınırına/yerleşime dokunuyor: " + string.Join(", ", offenders));
        Assert.DoesNotContain("IsHitTestVisible", code);                                    // girdi hiç beklemez
        Assert.DoesNotContain("IsEnabled =", code);
    }

    [Fact]
    public void No_endless_or_reversing_animations()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                if (Regex.IsMatch(StripComment(lines[i]), @"\b(RepeatBehavior|AutoReverse|Forever)\b"))
                    offenders.Add($"{Path.GetRelativePath(SourceRoot, file)}:{i + 1}: {lines[i].Trim()}");
        }
        foreach (var file in SourceFiles("*.xaml"))
            if (Regex.IsMatch(XamlWithoutComments(file), @"\b(RepeatBehavior|AutoReverse|Forever)\b"))
                offenders.Add(Path.GetRelativePath(SourceRoot, file));
        Assert.True(offenders.Count == 0, "Döngü/tekrar yok:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Widget_windows_have_no_effects()
    {
        // Katmanlı widget penceresi yazılımla çizilir: Effect her değişiklikte bütün kartı yeniden işler (gölge CardShadow).
        var offenders = new List<string>();
        var widgets = Path.Combine(SourceRoot, "Widgets");
        foreach (var file in SourceFiles("*.cs").Where(f => f.StartsWith(widgets, StringComparison.OrdinalIgnoreCase)))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                if (Regex.IsMatch(StripComment(lines[i]), @"\b(DropShadowEffect|BlurEffect|ShaderEffect)\b|\.Effect\s*=[^=]"))
                    offenders.Add($"{Path.GetRelativePath(SourceRoot, file)}:{i + 1}: {lines[i].Trim()}");
        }
        foreach (var file in SourceFiles("*.xaml").Where(f => f.StartsWith(widgets, StringComparison.OrdinalIgnoreCase)))
            if (Regex.IsMatch(XamlWithoutComments(file), @"\b(DropShadowEffect|BlurEffect)\b|<\w+\.Effect>|\sEffect="""))
                offenders.Add(Path.GetRelativePath(SourceRoot, file));
        Assert.True(offenders.Count == 0, "Widget'larda Effect yok:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Hover_never_changes_visibility()
    {
        // Fareyle yalnızca zemin rengi değişir: IsMouseOver tetikleyicisi hiçbir öğeyi gösterip gizlemez.
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.xaml"))
        {
            var document = XDocument.Parse(XamlWithoutComments(file), LoadOptions.SetLineInfo);
            foreach (var trigger in document.Descendants().Where(e => e.Name.LocalName is "Trigger" or "MultiTrigger" or "DataTrigger" or "MultiDataTrigger"))
            {
                var onHover = trigger.Attributes().Any(a => a.Value.Contains("IsMouseOver", StringComparison.Ordinal))
                              || trigger.Elements().Where(e => e.Name.LocalName.EndsWith(".Conditions", StringComparison.Ordinal))
                                  .Elements().Any(c => c.Attributes().Any(a => a.Value.Contains("IsMouseOver", StringComparison.Ordinal)));
                if (!onHover) continue;
                foreach (var setter in trigger.Descendants().Where(e => e.Name.LocalName == "Setter"))
                {
                    var property = (string?)setter.Attribute("Property") ?? "";
                    if (property.EndsWith("Visibility", StringComparison.Ordinal))
                        offenders.Add($"{Path.GetRelativePath(SourceRoot, file)}:{((System.Xml.IXmlLineInfo)setter).LineNumber}: {setter}");
                }
            }
        }
        Assert.True(offenders.Count == 0, "Fareyle görünürlük değişmez:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Widget_buttons_change_on_hover_only_through_Motion()
    {
        // Widget düğmelerinde fareyle yalnızca zemin katmanı solar (Motion.HoverFill; Kapalı'da hemen) ve basınca içerik küçülür
        // (Motion.Press): şablonda IsMouseOver tetikleyicisi (hemen değişen renk ya da saydamlık) kalmaz. Bölme kutucukları
        // (TileContainer) bilerek hemen değişir.
        var document = XDocument.Parse(XamlWithoutComments(Path.Combine(SourceRoot, "Widgets", "TileStyles.xaml")));
        XElement Keyed(string key) => document.Root!.Elements().Single(e => (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Key") == key);
        foreach (var key in new[] { "WidgetCloseButton", "PillButton", "WidgetGlyphButtonTemplate", "WidgetTextButtonTemplate" })
        {
            var element = Keyed(key);
            Assert.DoesNotContain(element.Descendants(), e => e.Name.LocalName is "Trigger" or "MultiTrigger" &&
                e.Attributes().Any(a => a.Value.Contains("IsMouseOver", StringComparison.Ordinal)));
            Assert.Contains(element.Descendants(), e => e.Attributes().Any(a => a.Name.LocalName == "Motion.HoverFill"));
        }
        foreach (var key in new[] { "PillButton", "WidgetGlyphButton" })
            Assert.Contains(Keyed(key).Elements(), e => (string?)e.Attribute("Property") == "views:Motion.Press" && (string?)e.Attribute("Value") == "True");
        Assert.Contains(Keyed("TileContainer").Descendants(), e => e.Name.LocalName == "Trigger" && (string?)e.Attribute("Property") == "IsMouseOver");
    }

    [Fact]
    public void Exit_plays_only_when_the_user_removes_one_thing()
    {
        // Çıkış (küçülüp solma) yalnızca kullanıcının tek bir şeyi kaldırdığı yerde: widget'ı kaldırma (RemoveWithUndo →
        // Leave) ve bildirim şeridinin ×'i. Toplu ve iç kaldırmalar (Remove, düzen uygulama) hemen kapanır.
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.cs"))
        {
            var relative = Path.GetRelativePath(SourceRoot, file);
            if (relative == Path.Combine("Views", "Motion.cs")) continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
                if (Regex.IsMatch(StripComment(lines[i]), @"\bMotion\.Exit\(") &&
                    relative != Path.Combine("Widgets", "WidgetManager.cs") && relative != "MainWindow.xaml.cs")
                    offenders.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
        }
        Assert.True(offenders.Count == 0, "Çıkış yalnızca kullanıcının kaldırmasında:\n" + string.Join("\n", offenders));

        var manager = File.ReadAllText(Path.Combine(SourceRoot, "Widgets", "WidgetManager.cs"));
        Assert.Matches(@"public bool Remove\(string id, bool notify = true\) => RemoveCore\(id, notify, leave: false\);", manager);
        Assert.Single(Regex.Matches(manager, @"leave: true"));
        Assert.Matches(@"RemoveWithUndo[\s\S]*RemoveCore\(id, notify: false, leave: true\)", manager);
    }

    [Fact]
    public void Size_to_content_dialogs_fit_their_chrome()
    {
        // WPF-UI FluentWindow çerçevesini ilk ölçümden sonra kaldırır: içeriğe göre boyutlanan her pencere yeniden ölçülmeli
        // (yoksa altında ~70 piksel boşluk kalır; bkz. WindowFit.FitChromeToContent).
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.cs"))
        {
            var text = File.ReadAllText(file);
            var fluent = text.Contains("new FluentWindow", StringComparison.Ordinal) || text.Contains(": FluentWindow", StringComparison.Ordinal);
            if (fluent && text.Contains("SizeToContent.WidthAndHeight", StringComparison.Ordinal) &&
                !text.Contains("FitChromeToContent(", StringComparison.Ordinal))
                offenders.Add(Path.GetRelativePath(SourceRoot, file));
        }
        Assert.True(offenders.Count == 0, "FitChromeToContent çağrılmıyor:\n" + string.Join("\n", offenders));
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 && !line[..index].Contains('"') ? line[..index] : line;
    }

    private static string XamlWithoutComments(string file) =>
        Regex.Replace(File.ReadAllText(file), "<!--.*?-->", "", RegexOptions.Singleline);

    private static string SourceRoot => Path.Combine(RepoRoot(), "src", "Duzenleme");

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(SourceRoot, pattern, SearchOption.AllDirectories)
            .Where(f =>
            {
                var rel = Path.GetRelativePath(SourceRoot, f);
                return !rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) && !rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase);
            });

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
    }
}
