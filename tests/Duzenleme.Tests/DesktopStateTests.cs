using System.Text.Json;
using Duzenleme.Core;

namespace Duzenleme.Tests;

public class DesktopStateTests
{
    // DesktopState.Compute: masaüstünün görünürlüğü tek yerde.

    [Theory]
    // fences, hidden, peeking, peekHides, hideWithIcons → simgeler gizli, widget'lar gizli
    [InlineData(false, false, false, true, true, false, false)]   // olağan: her şey görünür
    [InlineData(true, false, false, true, true, true, false)]     // bölmeler yönetiyor: yalnızca Windows simgeleri gizli
    [InlineData(false, true, false, true, true, true, true)]      // Ctrl+Alt+H: simgeler ve widget'lar
    [InlineData(false, true, false, true, false, true, false)]    // "Gizlerken widget'ları da gizle" kapalı: yalnızca simgeler
    [InlineData(true, true, false, true, false, true, true)]      // bölmeler yönetirken "gizle" bölmeleri de gizler
    [InlineData(true, false, true, true, true, false, true)]      // göz at: simgeler görünür, widget'lar çekilir
    [InlineData(true, false, true, false, true, false, false)]    // göz at, widget'lar kalsın
    [InlineData(false, false, true, true, false, false, true)]    // bölmesiz göz at: widget'lar çekilir
    [InlineData(true, true, true, true, true, false, true)]       // göz atma gizli masaüstünü de açar
    public void Compute_matches_the_table(bool fences, bool hidden, bool peeking, bool peekHides, bool hideWithIcons,
        bool iconsHidden, bool widgetsHidden)
    {
        var view = DesktopState.Compute(fences, hidden, peeking, peekHides, hideWithIcons);
        Assert.Equal(new DesktopView(iconsHidden, widgetsHidden), view);
    }

    public static IEnumerable<object[]> AllStates() =>
        Enumerable.Range(0, 32).Select(i => new object[] { (i & 1) != 0, (i & 2) != 0, (i & 4) != 0, (i & 8) != 0, (i & 16) != 0 });

    /// <summary>32 durumun hepsi: kurallar tablodan bağımsız olarak da doğrulanır.</summary>
    [Theory]
    [MemberData(nameof(AllStates))]
    public void Every_state_follows_the_rules(bool fences, bool hidden, bool peeking, bool peekHides, bool hideWithIcons)
    {
        var view = DesktopState.Compute(fences, hidden, peeking, peekHides, hideWithIcons);

        if (peeking)
        {
            // Göz atarken Windows simgeleri her zaman görünür (çökmede de görünür kalsınlar).
            Assert.False(view.IconsHidden);
            Assert.Equal(peekHides, view.WidgetsHidden);
            return;
        }
        // Gizli değilse widget'lar asla gizlenmez; bölmeler yönetiyorsa ya da gizlendiyse simgeler gizlidir.
        if (!hidden) Assert.False(view.WidgetsHidden);
        Assert.Equal(hidden || fences, view.IconsHidden);
        // Widget'lar gizliyse simgeler de gizlidir (boş masaüstü; widget'lar tek başına kaybolmaz).
        if (view.WidgetsHidden) Assert.True(view.IconsHidden);
        // Bölmeler yönetirken "gizle" her şeyi gizler.
        if (fences && hidden) Assert.True(view.WidgetsHidden);
    }

    // Çift tıklama

    [Theory]
    [InlineData(null, true, false, DoubleClickEffect.ToggleDesktop)]    // 2.0'dan gelen, açık: bugünkü davranış
    [InlineData(null, true, true, DoubleClickEffect.Peek)]              // otomatik + bölmeler yönetiyor: göz at
    [InlineData(null, false, false, DoubleClickEffect.None)]            // 2.0'da kapatılmış: hiçbir şey (taşınır)
    [InlineData(null, false, true, DoubleClickEffect.None)]
    [InlineData("auto", false, false, DoubleClickEffect.ToggleDesktop)] // yeni alan eski anahtara baskın
    [InlineData("auto", true, true, DoubleClickEffect.Peek)]
    [InlineData("toggle", true, true, DoubleClickEffect.ToggleDesktop)]
    [InlineData("peek", true, false, DoubleClickEffect.Peek)]
    [InlineData("none", true, true, DoubleClickEffect.None)]
    [InlineData(" PEEK ", true, false, DoubleClickEffect.Peek)]         // büyük/küçük harf, boşluk
    [InlineData("gelecek-surum", true, false, DoubleClickEffect.ToggleDesktop)] // bilinmeyen → otomatik
    [InlineData("", false, true, DoubleClickEffect.None)]               // boş = eski anahtar
    public void Double_click_resolves(string? action, bool legacy, bool fences, DoubleClickEffect expected) =>
        Assert.Equal(expected, DesktopState.ResolveDoubleClick(action, legacy, fences));

    [Fact]
    public void Double_click_choice_is_one_of_the_list()
    {
        foreach (var action in new[] { null, "", "auto", "toggle", "peek", "none", "x", "TOGGLE" })
            foreach (var legacy in new[] { true, false })
                Assert.Contains(DesktopState.DoubleClickChoice(action, legacy), DesktopState.DoubleClickChoices);
        Assert.Equal("none", DesktopState.DoubleClickChoice(null, legacyHidesDesktop: false));
        Assert.Equal("auto", DesktopState.DoubleClickChoice(null, legacyHidesDesktop: true));
        Assert.Equal("toggle", DesktopState.DoubleClickChoice("Toggle", legacyHidesDesktop: false));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(0, 0)]
    [InlineData(3, 2)]
    [InlineData(-1, 2)]
    [InlineData(999, 2)]
    public void Peek_minutes_are_normalized(int stored, int expected) =>
        Assert.Equal(expected, DesktopState.NormalizePeekMinutes(stored));

    // Ayarlar: yeni alanlar eski dosyaya varsayılanla gelir, kalıcı enum eklenmez.

    [Fact]
    public void Old_settings_get_the_new_desktop_defaults()
    {
        var s = JsonSerializer.Deserialize<AppSettings>("""{ "DoubleClickHidesDesktop": false, "FencesReplaceIcons": true }""", JsonFile.Options)!;

        Assert.Null(s.DoubleClickAction);
        Assert.Equal(DoubleClickEffect.None, DesktopState.ResolveDoubleClick(s.DoubleClickAction, s.DoubleClickHidesDesktop, s.FencesReplaceIcons));
        Assert.True(s.PeekHidesWidgets);
        Assert.False(s.PeekShowsDesktop);
        Assert.Equal(2, s.PeekMinutes);
        Assert.Equal(PlaceMode.Cursor, PlaceModes.Parse(s.NewWidgetPlacement));
        Assert.False(s.BoxItemsLeaveDesktop);
        Assert.False(s.BoxIncludesPublicDesktop);
        Assert.Equal("Ctrl+Alt+G", s.Hotkeys.PeekDesktop);
    }

    [Fact]
    public void New_settings_are_plain_values_that_an_old_version_can_skip()
    {
        var s = new AppSettings
        {
            DoubleClickAction = "peek", PeekMinutes = 5, NewWidgetPlacement = "center", BoxItemsLeaveDesktop = true,
            Widgets = [new WidgetConfig { Kind = WidgetKind.Launcher, BoxFolder = "Oyunlar" }],
        };
        var json = JsonSerializer.Serialize(s, JsonFile.Options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.String, root.GetProperty("DoubleClickAction").ValueKind);
        Assert.Equal(JsonValueKind.Number, root.GetProperty("PeekMinutes").ValueKind);
        Assert.Equal(JsonValueKind.String, root.GetProperty("NewWidgetPlacement").ValueKind);
        Assert.Equal(JsonValueKind.True, root.GetProperty("BoxItemsLeaveDesktop").ValueKind);
        Assert.Equal("Oyunlar", root.GetProperty("Widgets")[0].GetProperty("BoxFolder").GetString());

        var back = JsonSerializer.Deserialize<AppSettings>(json, JsonFile.Options)!;
        Assert.Equal("peek", back.DoubleClickAction);
        Assert.Equal(PlaceMode.Center, PlaceModes.Parse(back.NewWidgetPlacement));
    }

    // Kısayollar (F3: bilinmeyen eylem NewNote'un kısayolunu paylaşmasın)

    [Fact]
    public void Every_hotkey_action_round_trips_on_its_own()
    {
        var defaults = new HotkeySettings();
        var actions = Enum.GetValues<HotkeyAction>();
        foreach (var action in actions)
        {
            var hotkeys = new HotkeySettings();
            var value = $"Ctrl+Shift+F{(int)action + 1}";
            hotkeys.Set(action, value);

            Assert.Equal(value, hotkeys.Get(action));
            foreach (var other in actions.Where(a => a != action))
                Assert.Equal(defaults.Get(other), hotkeys.Get(other));
        }
    }

    [Fact]
    public void Default_hotkeys_are_all_set_and_distinct()
    {
        var hotkeys = new HotkeySettings();
        var values = Enum.GetValues<HotkeyAction>().Select(hotkeys.Get).ToList();

        Assert.All(values, v => Assert.True(Hotkey.TryParse(v, out _), v));
        Assert.Equal(values.Count, values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("Ctrl+Alt+G", hotkeys.Get(HotkeyAction.PeekDesktop));
    }

    [Fact]
    public void Unknown_hotkey_action_touches_nothing()
    {
        var hotkeys = new HotkeySettings();
        var unknown = (HotkeyAction)99;

        Assert.Equal("", hotkeys.Get(unknown));
        hotkeys.Set(unknown, "Ctrl+Alt+Z");
        Assert.All(Enum.GetValues<HotkeyAction>(), a => Assert.Equal(new HotkeySettings().Get(a), hotkeys.Get(a)));
    }

    [Fact]
    public void Old_hotkey_file_gets_the_peek_default_and_keeps_the_rest()
    {
        var hotkeys = JsonSerializer.Deserialize<HotkeySettings>("""{ "NewNote": "Ctrl+Shift+N" }""", JsonFile.Options)!;

        Assert.Equal("Ctrl+Alt+G", hotkeys.PeekDesktop);
        Assert.Equal("Ctrl+Shift+N", hotkeys.NewNote);
        hotkeys.Set(HotkeyAction.PeekDesktop, "");
        Assert.Equal("Ctrl+Shift+N", hotkeys.NewNote);
        Assert.Equal("", hotkeys.Get(HotkeyAction.PeekDesktop));
    }
}

public class PeekClockTests
{
    private static readonly DateTime T0 = new(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Until_i_return_never_expires()
    {
        var clock = new PeekClock(T0, 0);

        Assert.False(clock.AutoReturn);
        Assert.Null(clock.Remaining(T0));
        Assert.False(clock.Expired(T0.AddHours(5), userBusy: false));
        clock.Extend(T0, PeekClock.Extension);
        Assert.Null(clock.Remaining(T0));
    }

    [Fact]
    public void Expires_after_the_chosen_minutes()
    {
        var clock = new PeekClock(T0, 2);

        Assert.Equal(TimeSpan.FromMinutes(2), clock.Remaining(T0));
        Assert.False(clock.Expired(T0.AddSeconds(119), userBusy: false));
        Assert.True(clock.Expired(T0.AddMinutes(2), userBusy: false));
        Assert.Equal(TimeSpan.Zero, clock.Remaining(T0.AddMinutes(3)));
    }

    [Fact]
    public void Busy_user_postpones_the_return()
    {
        var clock = new PeekClock(T0, 1);
        var due = T0.AddMinutes(1);

        Assert.False(clock.Expired(due, userBusy: true));
        Assert.Equal(PeekClock.Postponement, clock.Remaining(due));
        Assert.False(clock.Expired(due.AddSeconds(10), userBusy: false));
        Assert.True(clock.Expired(due + PeekClock.Postponement, userBusy: false));
    }

    [Fact]
    public void Extend_adds_time_but_not_forever()
    {
        var clock = new PeekClock(T0, 2);
        clock.Extend(T0, PeekClock.Extension);
        Assert.Equal(TimeSpan.FromMinutes(7), clock.Remaining(T0));

        for (var i = 0; i < 30; i++) clock.Extend(T0, PeekClock.Extension);
        Assert.Equal(PeekClock.MaxRemaining, clock.Remaining(T0));

        // Süre dolduktan sonra uzatmak "şimdi"den sayılır.
        var late = new PeekClock(T0, 1);
        late.Extend(T0.AddMinutes(3), PeekClock.Extension);
        Assert.Equal(PeekClock.Extension, late.Remaining(T0.AddMinutes(3)));
    }

    [Theory]
    [InlineData(102, "1:42")]
    [InlineData(120, "2:00")]
    [InlineData(0.2, "0:01")]
    [InlineData(0, "0:00")]
    [InlineData(-5, "0:00")]
    [InlineData(600, "10:00")]
    public void Formats_minutes_and_seconds(double seconds, string expected) =>
        Assert.Equal(expected, PeekClock.Format(TimeSpan.FromSeconds(seconds)));
}

public class PrimaryMouseButtonTests
{
    [Fact]
    public void Swapped_buttons_make_the_physical_right_button_primary()
    {
        // Solak ayarı: ham giriş ve GetAsyncKeyState fiziksel düğmeyi bildirir, birincil düğme sağdadır.
        Assert.Equal(0x0001, PrimaryMouseButton.RawInputDownFlag(swapped: false));
        Assert.Equal(0x0004, PrimaryMouseButton.RawInputDownFlag(swapped: true));
        Assert.Equal(0x01, PrimaryMouseButton.VirtualKey(swapped: false));
        Assert.Equal(0x02, PrimaryMouseButton.VirtualKey(swapped: true));
    }
}
