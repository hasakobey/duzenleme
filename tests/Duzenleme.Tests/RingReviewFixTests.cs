using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// 2.2 halka incelemesinin düzeltmeleri: "Halkayı düzenle" kimlikle bulur (kaldırılan widget'ın kopyası düzenlenmez), çift
/// tıklamanın halkadan önceki seçimi ayarda kalır, halka olmayan merkez ezilmez, vazgeçince yalnızca kısayolla açılan halka
/// önceki pencereye döner, tanınmayan halka biçimi galeride Klasik görünmez, parmak izi bütün alanları görür; arayüz
/// tesisatı (düzenleyicinin bırakılması, gereksiz yeniden kurmalar, ölçek, odak çerçevesi) kaynak taramasıyla.
/// </summary>
public class RingReviewFixTests
{
    private static WidgetConfig Ring(string id) => new()
    {
        Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring, Id = id, Tabs = [new LauncherTab { Items = [@"C:\a.lnk"] }],
    };

    // ---------------------------------------------------------------- 1. RingEditor.Open

    [Fact]
    public void Edit_place_resolves_by_id_and_never_edits_a_removed_copy()
    {
        var live = Ring("halka000000000000000000000000001");
        var hub = Ring("merkez00000000000000000000000001");
        var settings = new AppSettings { Widgets = [live], Hub = hub };

        Assert.Equal((RingEditPlace.Hub, hub), RingTargets.EditPlace(settings, hub));
        Assert.Equal((RingEditPlace.Widget, live), RingTargets.EditPlace(settings, live));

        // "Geri al" kaldırılan widget'ı yeni bir nesneyle geri getirir: eski kopya şimdiki nesneye çözülür.
        var stale = Ring(live.Id);
        Assert.Equal((RingEditPlace.Widget, live), RingTargets.EditPlace(settings, stale));

        // Kaldırıldı: hiçbir yer (kopuk kopyanın kendi penceresi açılmaz).
        settings.Widgets.Remove(live);
        Assert.Equal((RingEditPlace.Gone, (WidgetConfig?)null), RingTargets.EditPlace(settings, live));

        // Aynı kimlikli halka olmayan widget da halka değildir.
        settings.Widgets.Add(new WidgetConfig { Kind = WidgetKind.Note, Id = live.Id });
        Assert.Equal(RingEditPlace.Gone, RingTargets.EditPlace(settings, live).Place);
    }

    [Fact]
    public void Ring_editor_open_resolves_by_id_and_honours_the_tab()
    {
        var editor = Code("Views", "RingEditor.cs");
        Assert.DoesNotContain("Settings.Widgets.Contains(config)", editor);
        Assert.Contains("RingTargets.EditPlace(AppHost.Settings, config)", editor);
        Assert.Contains("case RingEditPlace.Hub:", editor);
        Assert.Contains("case RingEditPlace.Gone:", editor);
        Assert.Contains("AppHost.EnsureWidgetsShown();", editor);
        Assert.Contains("view.EditorTab = tab;", editor);
        Assert.Contains("view.EditorTab = null", editor);
        // Bağımsız pencere yalnızca NestDesk halkasında.
        Assert.Single(Regex.Matches(editor, @"RingEditorWindow\.ShowFor\("));
        var view = Code("Widgets", "RingWidgetView.cs");
        Assert.Contains("var tab = EditorTab ?? _ring.TabIndex;", view);
    }

    // ---------------------------------------------------------------- 2. çift tıklamanın önceki seçimi

    [Theory]
    [InlineData(DesktopState.DoubleClickNone)]
    [InlineData(DesktopState.DoubleClickPeek)]
    [InlineData(DesktopState.DoubleClickToggle)]
    public void Ring_double_click_off_restores_the_choice_saved_before_it_even_after_a_restart(string before)
    {
        var settings = new AppSettings { DoubleClickAction = before, DoubleClickHidesDesktop = before != DesktopState.DoubleClickNone };
        Assert.Equal(DesktopState.DoubleClickRing, DesktopState.RingDoubleClickOn(settings));
        settings.DoubleClickAction = DesktopState.DoubleClickRing;
        Assert.Equal(before, settings.DoubleClickBeforeRing);

        // Yeniden başlatma: ayar dosyasından okunur.
        var again = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, JsonFile.Options), JsonFile.Options)!;
        Assert.Equal(before, DesktopState.RingDoubleClickOff(again));
        Assert.Null(again.DoubleClickBeforeRing); // geri konunca silinir
        // Bilinmiyorsa "otomatik".
        Assert.Equal(DesktopState.DoubleClickAuto, DesktopState.RingDoubleClickOff(again));
    }

    [Fact]
    public void Turning_ring_on_twice_keeps_the_original_choice_and_empty_is_not_written()
    {
        var settings = new AppSettings { DoubleClickAction = DesktopState.DoubleClickNone };
        DesktopState.RingDoubleClickOn(settings);
        settings.DoubleClickAction = DesktopState.DoubleClickRing;
        DesktopState.RingDoubleClickOn(settings);
        Assert.Equal(DesktopState.DoubleClickNone, settings.DoubleClickBeforeRing);
        // Tanınmayan kayıt "otomatik"e döner.
        settings.DoubleClickBeforeRing = "gelecek";
        Assert.Equal(DesktopState.DoubleClickAuto, DesktopState.RingDoubleClickOff(settings));
        Assert.DoesNotContain("DoubleClickBeforeRing", JsonSerializer.Serialize(new AppSettings(), JsonFile.Options));
    }

    [Fact]
    public void Welcome_restores_the_choice_saved_before_the_ring()
    {
        var settings = new AppSettings { DoubleClickAction = DesktopState.DoubleClickNone, DoubleClickHidesDesktop = false };
        Onboarding.ApplyRingChoices(settings, new WelcomeChoices { DoubleClickRing = true });
        Assert.Equal(DesktopState.DoubleClickRing, settings.DoubleClickAction);
        Assert.Equal(DesktopState.DoubleClickNone, settings.DoubleClickBeforeRing);

        Onboarding.ApplyRingChoices(settings, new WelcomeChoices { DoubleClickRing = false });
        Assert.Equal(DesktopState.DoubleClickNone, settings.DoubleClickAction);
        Assert.False(settings.DoubleClickHidesDesktop);
        Assert.Null(settings.DoubleClickBeforeRing);

        var page = Code("Views", "SettingsPage.xaml.cs");
        Assert.DoesNotContain("_doubleClickBeforeRing", page);
        Assert.Contains("DesktopState.RingDoubleClickOn(AppHost.Settings) : DesktopState.RingDoubleClickOff(AppHost.Settings)", page);
    }

    // ---------------------------------------------------------------- 3, 4. düzenleyicinin bırakılması ve tazeleme

    [Fact]
    public void Embedded_ring_editor_is_released_whenever_the_panel_drops_its_content()
    {
        var editor = Code("Views", "WidgetEditor.cs");
        // Build ve Release içeriği atmadan önce bırakma işlerini çalıştırır.
        Assert.Equal(2, Regex.Matches(editor, @"ReleaseContent\(\);\s*_body\.Children\.Clear\(\);").Count);
        Assert.Contains("new EditorContent(content, _refreshers, Changed, ShowNotice, _releasers)", editor);
        var view = Code("Widgets", "RingWidgetView.cs");
        Assert.Contains("private readonly List<Action> _editorChanged = [];", view);
        Assert.Contains("_editorChanged.Remove(changed);", view);
        Assert.Contains("editor.Release();", view);
        Assert.Contains("content.Custom(editor, editor.Refresh, release:", view);
    }

    [Fact]
    public void Ring_editor_and_widget_rebuild_only_when_the_ring_changed()
    {
        var editor = Code("Views", "RingEditor.cs");
        Assert.Contains("last.Print != print", editor);
        Assert.Contains("if (_refreshCount == before) Changed();", editor);
        var view = Code("Widgets", "RingWidgetView.cs");
        Assert.Contains("if (changed) _ring.Rebuild();", view);
        // Görünüşü belirleyen ayarlar serileştirilmeden karşılaştırılır.
        var popup = Code("Views", "RingPopup.cs");
        Assert.DoesNotContain("JsonSerializer", popup);
        Assert.Contains("RingFingerprint.Of(", popup);
    }

    // ---------------------------------------------------------------- 6. parmak izi

    [Fact]
    public void Fingerprint_sees_every_item_field_but_not_the_position()
    {
        var config = WidgetSeeds.Create(WidgetSeeds.Ring)!;
        var action = new LauncherAction { Type = RingActionTypes.Url, Url = "https://example.com/" };
        config.Tabs[0].Actions = [action];
        ulong Print() => RingFingerprint.Of(config, false, false);

        var before = Print();
        Assert.Equal(before, Print());
        config.Left = 300;
        config.PixelTop = 40;
        config.Z = 9;
        Assert.Equal(before, Print());

        // LauncherAction'ın her yazılabilir alanı (Extra hariç) parmak izine girer: yeni alan eklenince bu test hatırlatır.
        foreach (var property in typeof(LauncherAction).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanWrite && p.Name != nameof(LauncherAction.Extra)))
        {
            var old = property.GetValue(action);
            object value = property.PropertyType == typeof(string) ? "x" + old
                : property.PropertyType == typeof(bool) ? !(bool)old!
                : property.PropertyType == typeof(int) ? (int)old! + 1
                : property.PropertyType == typeof(List<string>) ? new List<string> { "Ctrl+A" }
                : throw new InvalidOperationException(property.Name);
            var was = Print();
            property.SetValue(action, value);
            Assert.True(was != Print(), property.Name);
        }

        void Changes(Action change)
        {
            var was = Print();
            change();
            Assert.NotEqual(was, Print());
        }
        Changes(() => config.Tabs[0].Items.Add(@"C:\b.lnk"));
        Changes(() => config.Tabs[0].Name = "Başka");
        Changes(() => config.Tabs.Add(new LauncherTab { Name = "Alt" }));
        Changes(() => config.Title = "Ad");
        Changes(() => config.Icon = "sym:Star24");
        Changes(() => config.RingStyle = RingStyles.Glow);
        Changes(() => config.LookTheme = ThemePresets.Okyanus);
        Changes(() => config.LookAccentColor = "#12A594");
        Changes(() => config.HiddenParts.Add("keys"));
        Changes(() => config.Scale = 1.25);
        Changes(() => ItemLooks.SetName(config, @"C:\b.lnk", "Bee", "b"));
        Assert.NotEqual(RingFingerprint.Of(config, false, false), RingFingerprint.Of(config, true, false));
        Assert.NotEqual(RingFingerprint.Of(config, false, false), RingFingerprint.Of(config, false, true));
    }

    // ---------------------------------------------------------------- 5, 7, 9. arayüz tesisatı

    [Fact]
    public void Ring_target_list_is_not_rebound_while_open_or_unchanged()
    {
        var page = Code("Views", "SettingsPage.xaml.cs");
        Assert.Contains("if (!RingTargetBox.IsDropDownOpen)", page);
        Assert.Contains("!listed.SequenceEqual(choices)) RingTargetBox.ItemsSource = choices;", page);
    }

    [Fact]
    public void Ring_item_text_is_cached_not_rebuilt_on_every_render()
    {
        var ring = Code("Widgets", "RingView.cs");
        var start = ring.IndexOf("class RingItemVisual", StringComparison.Ordinal);
        var render = ring.IndexOf("protected override void OnRender", start, StringComparison.Ordinal);
        var end = ring.IndexOf("private (string Text, double Size", render, StringComparison.Ordinal);
        Assert.DoesNotContain("new FormattedText", ring[render..end]);
        Assert.DoesNotContain("BuildGeometry", ring[render..end]);
        Assert.Contains("ReferenceEquals(d.Brush, brush) && d.Dpi == dpi", ring);
        Assert.Contains("ReferenceEquals(l.Brush, brush) && l.Dpi == dpi", ring);
    }

    [Fact]
    public void Galleries_redraw_at_the_screen_scale_and_show_a_keyboard_focus_frame()
    {
        var rings = Code("Views", "RingStyleGallery.cs");
        Assert.Contains("Loaded += (_, _) => Refresh();", rings);
        Assert.Contains("protected override void OnDpiChanged", rings);
        Assert.Contains("button.FocusVisualStyle = GalleryFocus.Style;", rings);
        var looks = Code("Views", "LookGalleryControl.cs");
        Assert.Contains("button.FocusVisualStyle = GalleryFocus.Style;", looks);
        // Odak çerçevesi fareyle belirmez ve oynamaz.
        var focus = looks[looks.IndexOf("class GalleryFocus", StringComparison.Ordinal)..];
        Assert.DoesNotContain("IsMouseOver", focus);
        Assert.DoesNotContain("Animation", focus);
    }

    // ---------------------------------------------------------------- 8. tanınmayan biçim

    [Fact]
    public void Unknown_ring_style_checks_no_card_and_classic_resets_it()
    {
        var c = new WidgetConfig { RingStyle = "gelecek" };
        Assert.Null(RingStyles.SelectedId(c));
        Assert.Equal("gelecek", c.RingStyle); // dokunulmazsa korunur
        Assert.True(RingStyles.Apply(c, RingStyles.ClassicId));
        Assert.Null(c.RingStyle);
        Assert.Equal(RingStyles.ClassicId, RingStyles.SelectedId(c));
        c.RingStyle = RingStyles.Orbit;
        Assert.Equal(RingStyles.Orbit, RingStyles.SelectedId(c));

        Assert.Contains("RingStyles.SelectedId(_config) == id", Code("Widgets", "RingWidgetView.cs"));
        Assert.Contains("() => Core.RingStyles.SelectedId(c)", Code("Views", "EditorContent.cs"));
    }

    // ---------------------------------------------------------------- 10. halka olmayan merkez

    [Fact]
    public void Non_ring_hub_is_kept_intact_and_a_runtime_ring_stands_in()
    {
        var future = new WidgetConfig { Kind = WidgetKind.Launcher, Variant = "gelecek", Id = "merkez00000000000000000000000009", Title = "Benim" };
        var settings = new AppSettings { Hub = future };
        var stand = RingTargets.EnsureHub(settings);
        Assert.Same(future, settings.Hub);
        Assert.Equal("gelecek", settings.Hub!.Variant);
        Assert.True(WidgetVariants.Is(stand, WidgetVariants.Ring));
        Assert.Same(stand, RingTargets.EnsureHub(settings)); // her çağrıda aynısı
        Assert.True(RingTargets.IsHub(settings, stand));
        Assert.Equal(RingEditPlace.Hub, RingTargets.EditPlace(settings, stand).Place);
        // Ayara yazılan merkez değişmez.
        var json = JsonSerializer.Serialize(settings, JsonFile.Options);
        Assert.Contains("\"gelecek\"", json);
        Assert.DoesNotContain(stand.Id, json);
    }

    // ---------------------------------------------------------------- 11. vazgeçince önceki pencere

    [Fact]
    public void Only_the_shortcut_ring_returns_to_the_previous_window()
    {
        Assert.True(RingAvailability.RestoresPrevious(RingOrigin.Hotkey));
        Assert.False(RingAvailability.RestoresPrevious(RingOrigin.Tray));
        Assert.False(RingAvailability.RestoresPrevious(RingOrigin.DoubleClick));
        Assert.False(RingAvailability.RestoresPrevious(RingOrigin.Command));
        var popup = Code("Views", "RingPopup.cs");
        Assert.DoesNotContain("CloseWith(restore: true", popup);
        Assert.Contains("CloseWith(restore: RingAvailability.RestoresPrevious(_origin), after: null)", popup);
    }

    // ---------------------------------------------------------------- yardımcılar

    private static string Code(params string[] parts) =>
        string.Join("\n", File.ReadAllLines(Path.Combine([SourceRoot, .. parts])).Select(line =>
        {
            var index = line.IndexOf("//", StringComparison.Ordinal);
            return index >= 0 && !line[..index].Contains('"') ? line[..index] : line;
        }));

    private static string SourceRoot => Path.Combine(RepoRoot(), "src", "Duzenleme");

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
    }
}
