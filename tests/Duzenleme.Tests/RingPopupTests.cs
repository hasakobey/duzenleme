using System.Text.RegularExpressions;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Localization;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// Kısayolla açılan halkanın saf bölümleri: "Kısayolla imleçte açılsın" akışı (öneri döngüsü), NESTDESK_TEST_RING'in yalnızca
/// test masaüstünde geçerli olması, açılır pencerenin yerleşimi, hedef halka, NestDesk eylemlerinin eşlemesi, tuş/metin
/// sonuçlarının bildirimleri ve pencerenin Store koşulları (kaynak taraması: etkinleşebilir, masaüstüne sahipli değil, kanca yok).
/// </summary>
public class RingPopupTests
{
    // ---------------------------------------------------------------- "Kısayolla imleçte açılsın"

    /// <summary>Sahte kayıt: <paramref name="taken"/> içindeki kısayollar "başka bir uygulama kullanıyor" der.</summary>
    private static Func<HotkeySettings, IReadOnlyDictionary<HotkeyAction, string>> FakeApply(List<string?> applied, params string[] taken) =>
        settings =>
        {
            applied.Add(settings.Ring);
            var failures = new Dictionary<HotkeyAction, string>();
            foreach (var action in Enum.GetValues<HotkeyAction>())
                if (taken.Contains(settings.Get(action), StringComparer.OrdinalIgnoreCase)) failures[action] = "başka uygulama";
            return failures;
        };

    [Fact]
    public void Turning_on_takes_the_first_free_suggestion()
    {
        var settings = new HotkeySettings();
        var applied = new List<string?>();
        var result = RingHotkey.TurnOn(settings, FakeApply(applied));
        Assert.Equal("Ctrl+Alt+R", result.Hotkey);
        Assert.Equal("Ctrl+Alt+R", settings.Ring);
        Assert.Null(result.Failure);
        Assert.Empty(result.Tried);
        Assert.Equal(new string?[] { "Ctrl+Alt+R" }, applied);
    }

    [Fact]
    public void A_suggestion_another_app_holds_is_skipped()
    {
        var settings = new HotkeySettings();
        var applied = new List<string?>();
        var result = RingHotkey.TurnOn(settings, FakeApply(applied, "Ctrl+Alt+R", "Ctrl+Alt+K"));
        Assert.Equal("Ctrl+Alt+J", result.Hotkey);
        Assert.Equal("Ctrl+Alt+J", settings.Ring);
        Assert.Equal(new[] { "Ctrl+Alt+R", "Ctrl+Alt+K" }, result.Tried);
        Assert.Equal(new string?[] { "Ctrl+Alt+R", "Ctrl+Alt+K", "Ctrl+Alt+J" }, applied);
    }

    [Fact]
    public void Other_actions_failing_do_not_block_the_ring()
    {
        // Ctrl+Alt+O (Şimdi düzenle) başka bir uygulamada: halka yine ilk öneriyi alır.
        var settings = new HotkeySettings();
        var result = RingHotkey.TurnOn(settings, FakeApply([], "Ctrl+Alt+O"));
        Assert.Equal("Ctrl+Alt+R", result.Hotkey);
    }

    [Fact]
    public void A_suggestion_one_of_our_own_shortcuts_uses_is_never_tried()
    {
        var settings = new HotkeySettings { QuickAdd = "Ctrl+Alt+R" };
        var applied = new List<string?>();
        var result = RingHotkey.TurnOn(settings, FakeApply(applied));
        Assert.Equal("Ctrl+Alt+K", result.Hotkey);
        Assert.DoesNotContain("Ctrl+Alt+R", applied);
    }

    [Fact]
    public void When_every_suggestion_fails_the_ring_stays_off_and_says_why()
    {
        var settings = new HotkeySettings();
        var applied = new List<string?>();
        var result = RingHotkey.TurnOn(settings, FakeApply(applied, [.. RingHotkey.Candidates]));
        Assert.Null(result.Hotkey);
        Assert.Equal("başka uygulama", result.Failure);
        Assert.Null(settings.Ring);
        Assert.Equal(RingHotkey.Candidates.Count, result.Tried.Count);
        // Son kayıt halkasız yapılır (kaydedilemeyen kısayol ayarda kalmaz).
        Assert.Null(applied[^1]);
        Assert.Equal(RingHotkey.Candidates.Count + 1, applied.Count);
    }

    [Fact]
    public void Test_ring_is_honoured_only_on_a_test_desktop()
    {
        Assert.True(RingHotkey.TestOverride(isTestDesktop: true, "1"));
        Assert.True(RingHotkey.TestOverride(isTestDesktop: true, " 1 "));
        Assert.False(RingHotkey.TestOverride(isTestDesktop: false, "1"));
        Assert.False(RingHotkey.TestOverride(isTestDesktop: true, null));
        Assert.False(RingHotkey.TestOverride(isTestDesktop: true, "0"));
        Assert.False(RingHotkey.TestOverride(isTestDesktop: true, "true"));
    }

    [Fact]
    public void Test_ring_routes_id_8_without_a_registered_shortcut()
    {
        var off = new HotkeySettings();
        Assert.Equal((true, (HotkeyAction?)null), HotkeyManager.Route(8, off));
        Assert.Equal((true, (HotkeyAction?)HotkeyAction.Ring), HotkeyManager.Route(8, off, testRing: true));
        Assert.Equal((true, (HotkeyAction?)HotkeyAction.Ring), HotkeyManager.Route(8, null, testRing: true));
        // Öteki eylemler değişmez.
        Assert.Equal((true, (HotkeyAction?)HotkeyAction.ToggleDesktop), HotkeyManager.Route(1, off, testRing: true));
        Assert.Equal((false, (HotkeyAction?)null), HotkeyManager.Route(99, off, testRing: true));
    }

    [Fact]
    public void Test_ring_is_wired_only_through_the_test_desktop_check()
    {
        var app = Source("App.xaml.cs");
        // Test masaüstü hiçbir genel kısayolu kaydetmez (NESTDESK_TEST_HOTKEYS=1 değilse); halka testi iletiyle açılır.
        Assert.Matches(@"new HotkeyManager\(OnHotkey\)\s*\{\s*TestDesktop = AppHost\.IsTestDesktop,\s*" +
            @"TestHotkeys = HotkeyManager\.TestOverride\(AppHost\.IsTestDesktop, AppEnvironment\.Get\(""TEST_HOTKEYS""\)\),\s*" +
            @"TestRing = Views\.RingPopup\.TestRing,\s*\}", app);
        var popup = Source("Views", "RingPopup.cs");
        Assert.Matches(@"TestRing => RingHotkey\.TestOverride\(AppHost\.IsTestDesktop, AppEnvironment\.Get\(""TEST_RING""\)\)", popup);
        // Ortam değişkenleri başka yerden okunmaz.
        foreach (var file in SourceFiles().Where(f => !f.EndsWith("RingPopup.cs", StringComparison.Ordinal)))
            Assert.DoesNotContain("\"TEST_RING\"", File.ReadAllText(file));
        foreach (var file in SourceFiles().Where(f => !f.EndsWith("App.xaml.cs", StringComparison.Ordinal)))
            Assert.DoesNotContain("\"TEST_HOTKEYS\"", File.ReadAllText(file));
        // Kayıt kararı tek yerde: RegistersWithWindows test masaüstünü ve isteği birlikte alır.
        Assert.Contains("RegistersWithWindows(action, TestDesktop, TestHotkeys, TestRing)", Source("Desktop", "HotkeyManager.cs"));
    }

    // ---------------------------------------------------------------- yerleşim ve hedef

    [Fact]
    public void Popup_window_is_twice_ring_plus_aim_zone_in_physical_pixels()
    {
        var monitor = new Box(-1920, 0, 0, 1080);
        var work = new Box(-1920, 0, 0, 1040);
        var p = RingPlacement.ForPopup(monitor, work, -960, 540, outerRadiusDip: 120, aimDip: 72, scale: 1.25);
        Assert.Equal(2 * (150 + 90), p.SidePx);
        Assert.Equal((-960, 540), (p.Place.CenterX, p.Place.CenterY));
        Assert.Equal((-960 - 240, 540 - 240), (p.Place.Left, p.Place.Top));
        // Halkanın ortası pencerede, pencerenin ölçeğiyle DIP'e çevrilmiş.
        Assert.Equal(240 / 1.25, p.CenterXDip, 6);
        Assert.Equal(240 / 1.25, p.CenterYDip, 6);
    }

    [Theory]
    [InlineData(-1915, 5)]       // sol üst köşe
    [InlineData(-3, 1078)]       // sağ alt (görev çubuğu)
    [InlineData(-960, 540)]
    public void Popup_keeps_the_ring_in_the_work_area_and_the_window_on_the_monitor(int x, int y)
    {
        var monitor = new Box(-1920, 0, 0, 1080);
        var work = new Box(-1920, 0, 0, 1040);
        var p = RingPlacement.ForPopup(monitor, work, x, y, 120, 72, 1.25);
        var radius = (int)Math.Ceiling(120 * 1.25);
        Assert.InRange(p.Place.CenterX - radius - RingPlacement.MarginPx, work.Left, work.Right);
        Assert.InRange(p.Place.CenterX + radius + RingPlacement.MarginPx, work.Left, work.Right);
        Assert.InRange(p.Place.CenterY + radius + RingPlacement.MarginPx, work.Top, work.Bottom);
        Assert.InRange(p.Place.Left, monitor.Left, monitor.Right - p.SidePx);
        Assert.InRange(p.Place.Top, monitor.Top, monitor.Bottom - p.SidePx);
        // Çizim yeri pencere yeriyle tutarlı.
        Assert.Equal(p.Place.CenterX - p.Place.Left, p.CenterXDip * 1.25, 6);
        Assert.Equal(p.Place.CenterY - p.Place.Top, p.CenterYDip * 1.25, 6);
    }

    [Fact]
    public void Popup_layout_survives_a_bad_scale()
    {
        var box = new Box(0, 0, 1920, 1080);
        var p = RingPlacement.ForPopup(box, box, 500, 500, 100, 72, 0);
        Assert.Equal(2 * (100 + 72), p.SidePx);
        Assert.Equal(p.Place.OffsetX, p.CenterXDip, 6);
    }

    [Fact]
    public void Target_is_the_chosen_ring_else_the_first_ring()
    {
        var note = new WidgetConfig { Kind = WidgetKind.Note };
        var box = new WidgetConfig { Kind = WidgetKind.Launcher };
        var first = new WidgetConfig { Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring };
        var second = new WidgetConfig { Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring };
        var widgets = new[] { note, box, first, second };
        Assert.Equal(new[] { first, second }, RingTargets.All(widgets));
        Assert.Same(second, RingTargets.Pick(widgets, second.Id));
        Assert.Same(first, RingTargets.Pick(widgets, null));
        Assert.Same(first, RingTargets.Pick(widgets, "silinmiş"));
        Assert.Same(first, RingTargets.Pick(widgets, box.Id));   // halka olmayan widget hedef olmaz
        Assert.Null(RingTargets.Pick([note, box], null));
    }

    [Fact]
    public void Widget_menu_option_is_checked_only_on_the_ring_the_shortcut_opens()
    {
        var first = new WidgetConfig { Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring };
        var second = new WidgetConfig { Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring };
        var box = new WidgetConfig { Kind = WidgetKind.Launcher };
        var widgets = new[] { box, first, second };
        Assert.True(RingTargets.Opens(widgets, null, first));            // seçilmemişse ilk halka
        Assert.False(RingTargets.Opens(widgets, null, second));
        Assert.True(RingTargets.Opens(widgets, second.Id, second));
        Assert.False(RingTargets.Opens(widgets, second.Id, first));
        Assert.False(RingTargets.Opens(widgets, box.Id, box));           // halka olmayan widget hiç
        // Menüdeki seçenek bu halkaya bakar; başka halkayı açan kısayolu kapatmaz, bu halkayı hedef yapar.
        var shortcut = Code("Widgets", "RingShortcut.cs");
        Assert.Matches(@"public static bool Opens\(WidgetConfig config\) =>\s*IsOn && RingTargets\.Opens\(", shortcut);
        Assert.Matches(@"if \(Opens\(config\)\)\s*\{\s*TurnOff\(\);", shortcut);
        Assert.Contains("() => RingShortcut.Opens(_config)", Code("Widgets", "RingWidgetView.cs"));
    }

    // ---------------------------------------------------------------- NestDesk eylemleri

    [Fact]
    public void Ring_app_actions_map_to_run_action_cases_and_never_to_the_ring_itself()
    {
        var app = Source("App.xaml.cs");
        var runAction = Regex.Match(app, @"internal void RunAction\(HotkeyAction action\)\s*\{(?<body>[\s\S]*?)\n    \}").Groups["body"].Value;
        Assert.NotEmpty(runAction);
        foreach (var id in RingAppIds.All)
        {
            var action = RingAppIds.ToHotkeyAction(id);
            Assert.NotNull(action);
            Assert.NotEqual(HotkeyAction.Ring, action);
            Assert.NotEqual(HotkeyAction.OrganizeNow, action);   // Paused'u atlardı
            Assert.Contains($"case HotkeyAction.{action}:", runAction);
            Assert.Equal(id, RingAppIds.FromHotkeyAction(action!.Value));
        }
        Assert.DoesNotContain("HotkeyAction.Ring", runAction);
        // Kısayol halkayı açar; öbür eylemler RunAction'a gider.
        Assert.Matches(@"private void OnHotkey\(HotkeyAction action\)\s*\{\s*if \(action == HotkeyAction\.Ring\)[\s\S]*?RingPopup\.Toggle\(\)[\s\S]*?RunAction\(action\);", app);
    }

    // ---------------------------------------------------------------- tuş ve metin bildirimleri

    [Fact]
    public void Successful_sends_and_pastes_are_silent()
    {
        Assert.Null(RingRunner.KeysNotice(SendResult.Sent, null).Text);
        Assert.Null(RingRunner.TextNotice(SnippetPaste.Outcome.Restored, null).Text);
        Assert.Null(RingRunner.TextNotice(SnippetPaste.Outcome.ClipboardChanged, null).Text);
    }

    [Fact]
    public void Every_other_result_says_what_happened_in_both_languages()
    {
        foreach (var lang in new[] { Lang.Tr, Lang.En })
        {
            using var _ = L.Use(lang);
            foreach (var result in Enum.GetValues<SendResult>().Where(r => r != SendResult.Sent))
                Assert.False(string.IsNullOrWhiteSpace(RingRunner.KeysNotice(result, null).Text), $"{lang} {result}");
            foreach (var outcome in Enum.GetValues<SnippetPaste.Outcome>()
                         .Where(o => o is not SnippetPaste.Outcome.Restored and not SnippetPaste.Outcome.ClipboardChanged))
                Assert.False(string.IsNullOrWhiteSpace(RingRunner.TextNotice(outcome, null).Text), $"{lang} {outcome}");
        }
    }

    [Fact]
    public void Elevated_target_is_named_only_when_it_really_runs_as_administrator()
    {
        Assert.Equal("Yönetici olarak çalışan pencereye tuş gönderilemez.", RingRunner.KeysNotice(SendResult.Elevated, true).Text);
        Assert.NotEqual(RingRunner.KeysNotice(SendResult.Elevated, true).Text, RingRunner.KeysNotice(SendResult.Elevated, null).Text);
        Assert.NotEqual(RingRunner.TextNotice(SnippetPaste.Outcome.Elevated, true).Text, RingRunner.TextNotice(SnippetPaste.Outcome.Elevated, false).Text);
        Assert.Equal(Duzenleme.Views.NoticeKind.Warning, RingRunner.KeysNotice(SendResult.Elevated, true).Kind);
    }

    [Theory]
    [InlineData(@"C:\Araçlar\not.exe", true)]
    [InlineData(@"C:\Users\x\Desktop\Kısayol.lnk", true)]
    [InlineData(@"C:\betik.cmd", true)]
    [InlineData(@"C:\belge.pdf", false)]
    [InlineData(@"C:\Klasör", false)]
    [InlineData("::{645FF040-5081-101B-9F08-00AA002F954E}", false)]
    public void Only_programs_and_shortcuts_run_as_administrator(string path, bool expected) =>
        Assert.Equal(expected, RingRunner.CanRunAsAdmin(path));

    // ---------------------------------------------------------------- pencere (Store koşulları; kaynak taraması)

    [Fact]
    public void Popup_window_activates_and_is_not_desktop_owned_and_has_no_effects()
    {
        var popup = Code("Views", "RingPopup.cs");
        Assert.Contains("WS_EX_TOOLWINDOW", popup);
        Assert.DoesNotContain("NOACTIVATE =", popup);           // etkinleşebilir (klavye 1–9, Esc)
        Assert.DoesNotContain("0x08000000", popup);
        Assert.DoesNotContain("GWLP_HWNDPARENT", popup);        // masaüstüne sahipli değil
        Assert.DoesNotContain("DesktopOwner", popup);
        Assert.DoesNotContain("QuietShow", popup);
        Assert.Contains("Topmost = true", popup);
        Assert.Contains("SC_KEYMENU", popup);
        Assert.DoesNotMatch(@"\b(DropShadowEffect|BlurEffect|ShaderEffect)\b|\.Effect\s*=", popup);
        // Hareket yalnızca Motion'la.
        Assert.Contains("Motion.RingOpen(", popup);
        Assert.Contains("Motion.RingClose(", popup);
        Assert.Contains("Motion.RingSwap(", popup);
        // Kanca yok; tuş yoklaması yalnızca kısayolun tuşları, sınırlı süre.
        Assert.DoesNotMatch(@"SetWindowsHookEx|WH_KEYBOARD|WH_MOUSE|RegisterRawInput", popup);
        Assert.Contains("TimeSpan.FromSeconds(10)", popup);
        Assert.Matches(@"AllReleased\(\) => _hotkeyKeys\.All\(vk => NativeMethods\.GetAsyncKeyState\(vk\) >= 0\)", popup);
        Assert.Single(Regex.Matches(popup, @"GetAsyncKeyState"));
        // Nişan alanı fareyi alır (tamamen saydam piksel alttaki pencereye geçirirdi).
        Assert.Contains("Color.FromArgb(0x01, 0, 0, 0)", popup);
    }

    [Fact]
    public void Keys_and_text_go_out_only_after_the_ring_has_closed()
    {
        var popup = Code("Views", "RingPopup.cs");
        // Tuş/metin yalnızca kapanış geri çağırmasında (HideNow → after) gönderilir.
        Assert.Matches(@"CloseWith\(restore: false, after: \(\) => _ = RingRunner\.SendToTargetAsync\(", popup);
        Assert.Matches(@"Motion\.RingClose\(_root, HideNow", popup);
        Assert.Matches(@"private void HideNow\(\)[\s\S]*?Hide\(\);[\s\S]*?after\?\.Invoke\(\);", popup);
        var runner = Code("Widgets", "RingRunner.cs");
        Assert.Matches(@"SetForegroundWindow\(target\);[\s\S]*?await KeySender\.WaitForKeysReleasedAsync\(hotkeyKeys, KeySender\.MaxWait\);[\s\S]*?KeySender\.SendToAsync\(", runner);
        // Öğe çalışamasa da odak önceki pencereye döner (önce öne getirme, sonra denetim); hata çağırana (beklenmeyen görev) kaçmaz.
        Assert.Matches(@"SetForegroundWindow\(target\);\s*if \(!Runnable\(action\)\) return;\s*try", runner);
    }

    [Fact]
    public void Clicking_the_invisible_aim_zone_closes_instead_of_running_an_item()
    {
        var popup = Code("Views", "RingPopup.cs");
        // Diskin dışındaki tıklama (nişan alanı) Dismiss; vurgulanan öğeyi seçmez.
        Assert.Matches(@"_root\.MouseLeftButtonUp \+= \(_, e\) =>\s*\{[^}]*Dismiss\(\);\s*\};", popup);
        Assert.DoesNotMatch(@"_root\.MouseLeftButtonUp[^}]*CommitHighlight", popup);
        // Nişan alanında yalnızca "tuşu bırakınca seç" ya da basıp sürükleme vurgular.
        Assert.Matches(@"private void OnPointerMove\(\)[\s\S]*?if \(_armed\)[\s\S]*?else if \(_ring\.IsMouseCaptured\)", popup);
    }

    [Fact]
    public void A_ring_that_could_not_come_to_the_front_closes_by_itself()
    {
        var popup = Code("Views", "RingPopup.cs");
        // Öne gelemezse öndeki pencere izlenir: başka pencere öne gelince ya da süre dolunca (imleç üstünde değilse) kapanır.
        Assert.Matches(@"if \(ForegroundInfo\.Window\(\) != hwnd\) StartWatch\(\);", popup);
        Assert.Matches(@"foreground != IntPtr\.Zero && foreground != _previous\) CloseWith\(restore: false", popup);
        Assert.Matches(@"expired && !IsMouseOver\) CloseWith\(restore: false", popup);
        Assert.Matches(@"Activated \+= [\s\S]*?StopWatch\(\);", popup);
        // Gizli pencere ölçeğe geçmezse yeniden kurulur (bir kez).
        Assert.Matches(@"if \(!Place\(requireScale: mayRecreate\)\)\s*\{\s*Recreate\(", popup);
        Assert.Contains("fresh.Open(pressedAt, origin, _anchor, previous, mayRecreate: false);", popup);
        // Kısayol (ve çift tıklama) halka açıkken kapatıldıysa kapanınca bütünüyle kapanır.
        Assert.Matches(@"if \(!Prebuild\)\s*\{\s*Sync\(\);", popup);
    }

    [Fact]
    public void Ctrl_shift_from_a_held_shortcut_does_not_run_as_administrator()
    {
        var popup = Code("Views", "RingPopup.cs");
        Assert.Contains("var admin = Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && !_hotkeyHeld;", popup);
        Assert.Matches(@"_hotkeyHeld = _hotkeyKeys\.Count > 0 && !AllReleased\(\);", popup);
    }

    /// <summary>
    /// Açılır halka widget'ın temasına uyar: tema ya da özel vurgu rengi değişince (Style/Accent aynı kalsa da, ör. Okyanus →
    /// Otomatik → Klasik) önceden kurulmuş halka eskir ve yeniden kurulur; Windows'un açık/koyu kipi değişince de (açılışta
    /// denetlenir). Arayüz denemesinde widget Klasik/Koyu iken açılır halka Okyanus renklerinde kalıyordu.
    /// </summary>
    [Fact]
    public void Popup_rebuilds_when_the_theme_or_custom_accent_changes()
    {
        var config = WidgetSeeds.Create(WidgetSeeds.Ring)!;
        var before = Views.RingPopup.Signature(config);
        Assert.Equal(before, Views.RingPopup.Signature(config));
        config.LookTheme = ThemePresets.Okyanus;
        var themed = Views.RingPopup.Signature(config);
        Assert.NotEqual(before, themed);
        config.LookAccentColor = "#12A594";
        Assert.NotEqual(themed, Views.RingPopup.Signature(config));
        config.Left = 120; // konum görünüşü değiştirmez
        var moved = Views.RingPopup.Signature(config);
        config.Left = 340;
        Assert.Equal(moved, Views.RingPopup.Signature(config));

        var popup = Code("Views", "RingPopup.cs");
        Assert.Contains("_builtLook = LookNow();", popup);
        Assert.Matches(@"\|\| _builtLook != LookNow\(\)\) Build\(config\);", popup);
        Assert.Contains("WidgetPalette.SystemLook.Current(WidgetPalette.SystemDark, SystemParameters.HighContrast)", popup);
        Assert.DoesNotContain("_builtHighContrast", popup);
        Assert.DoesNotContain("_builtSystemDark", popup);
    }

    /// <summary>
    /// Windows'un görünüşü değişince (açık/koyu kip, yüksek karşıtlık, bir yüksek karşıtlık temasından ötekine: Gece gökyüzü →
    /// Çöl'de HighContrast hep true kalır, yalnızca renkler değişir) önceden kurulmuş açılır halka boşta yeniden kurulur:
    /// App.ApplyTheme widget'ları yeniden çizdiği dalda halkaya da bildirir. Eskiden yalnızca iki bayrağa açılışta (kısayol
    /// yolunda) bakılıyordu; YK temaları arasında halka ve zemini eski Windows renklerinde kalıyordu.
    /// </summary>
    [Fact]
    public void Popup_rebuilds_at_idle_when_the_windows_look_changes()
    {
        var app = Code("App.xaml.cs");
        Assert.Matches(@"if \(_widgetLook != look\)\s*\{[\s\S]*?if \(restyle\)\s*\{\s*AppHost\.Widgets\?\.RestyleAll\(all\);\s*Views\.RingPopup\.SystemLookChanged\(\);\s*\}", app);
        var popup = Code("Views", "RingPopup.cs");
        Assert.Matches(@"public static void SystemLookChanged\(\)\s*\{[\s\S]*?popup\.MarkDirty\(\);\s*\}", popup);
        // MarkDirty gizli pencereyi boşta kurar, açık pencereyi kapanınca.
        Assert.Matches(@"private void MarkDirty\(\)[\s\S]*?if \(IsVisible \|\| _rebuildQueued \|\| _forReal\) return;[\s\S]*?DispatcherPriority\.ApplicationIdle", popup);
        Assert.Matches(@"if \(_dirty\) MarkDirty\(\);", popup);
    }

    [Fact]
    public void Settings_list_the_ring_shortcut_and_reset_turns_it_off()
    {
        var page = Source("Views", "SettingsPage.xaml.cs");
        Assert.Contains("(HotkeyAction.Ring, L.T(\"Halka menüyü imleçte aç\"), SymbolRegular.DataPie24)", page);
        Assert.Matches(@"ResetHotkeys_Click[\s\S]*?var ringWasOn = RingShortcut\.IsOn;[\s\S]*?if \(ringWasOn\)", page);
        // Varsayılan ayarda halka kapalı (Store: varsayılan kapalı ve atanmamış).
        Assert.Null(new HotkeySettings().Ring);
        var xaml = Source("Views", "SettingsPage.xaml");
        Assert.Contains("Checked=\"RingToggle_Changed\" Unchecked=\"RingToggle_Changed\"", xaml);
        Assert.Contains("Checked=\"RingHoldToggle_Changed\" Unchecked=\"RingHoldToggle_Changed\"", xaml);
        // Halka adı ComboBox'ta düz metin (DisplayMemberPath → TextBlock; "_" erişim tuşu olmaz).
        Assert.Contains("DisplayMemberPath=\"Name\"", xaml);
        // Kısayol listesinden açılan halka da ne yaptığını anlatır (Store matrisi 2b; widget menüsüyle aynı metin).
        // Ayarlar'da kapatınca açılıştaki "… ile imlecin yanında açılır" şeridi ortada kalmaz: kapatıldığı söylenir.
        Assert.Matches(@"RingToggle_Changed[\s\S]*?if \(wasOn\) RingShortcut\.TurnOff\(\);[\s\S]*?if \(wasOn\) Notice\.Show\(L\.T\(""Halka menü kısayolu kapatıldı\.""\)", page);
        // Satır sonu kaçışla (\n): kaynağın satır sonları (CRLF/LF) sonucu değiştirmesin.
        Assert.Matches(@"row\.Action == HotkeyAction\.Ring && !ringWasOn[^\n]*\n\s*Notice\.Show\(RingShortcut\.TurnedOnText\(", page);
        Assert.Contains("TurnedOnText(hotkey)", Code("Widgets", "RingShortcut.cs"));
    }

    // ---------------------------------------------------------------- yardımcılar

    private static string Code(params string[] parts) =>
        string.Join("\n", File.ReadAllLines(Path.Combine([SourceRoot, .. parts])).Select(line =>
        {
            var index = line.IndexOf("//", StringComparison.Ordinal);
            return index >= 0 && !line[..index].Contains('"') ? line[..index] : line;
        }));

    private static string Source(params string[] parts) => File.ReadAllText(Path.Combine([SourceRoot, .. parts]));

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(SourceRoot, "*.cs", SearchOption.AllDirectories).Where(f =>
        {
            var rel = Path.GetRelativePath(SourceRoot, f);
            return !rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) && !rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase);
        });

    private static string SourceRoot
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return Path.Combine(dir.FullName, "src", "Duzenleme");
            throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
        }
    }
}
