using System.Text.Json;
using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>
/// Halka öğelerinin kuralları (Store matrisi 2b, 3a–3h): web adresi asla dosya/program olmaz, tuş ve metin sınırları,
/// içe aktarılan riskli öğeler kapalı gelir, NestDesk eylemleri kendi kimlikleriyle, halka kısayolu önerisi.
/// </summary>
public class RingActionsTests
{
    [Theory]
    [InlineData("https://example.com", UrlCheck.Allowed)]
    [InlineData("  http://a.b/c?d=1#e ", UrlCheck.Allowed)]
    [InlineData("HTTPS://Example.COM/Yol", UrlCheck.Allowed)]
    [InlineData("mailto:ali@example.com", UrlCheck.Allowed)]
    [InlineData("ms-settings:display", UrlCheck.NeedsConfirmation)]
    [InlineData("steam://run/440", UrlCheck.NeedsConfirmation)]
    [InlineData(@"C:\x.exe", UrlCheck.Invalid)]
    [InlineData("C:/Windows/System32/cmd.exe", UrlCheck.Invalid)]
    [InlineData(@"\\server\share", UrlCheck.Invalid)]
    [InlineData(@"\\server\share\run.exe", UrlCheck.Invalid)]
    [InlineData("//server/share", UrlCheck.Invalid)]
    [InlineData("file:///C:/x.exe", UrlCheck.Invalid)]
    [InlineData("FILE://server/share/x.exe", UrlCheck.Invalid)]
    [InlineData("javascript:alert(1)", UrlCheck.Invalid)]
    [InlineData("vbscript:msgbox", UrlCheck.Invalid)]
    [InlineData("search-ms:query=x&crumb=location:\\\\evil", UrlCheck.Invalid)]
    [InlineData("ms-msdt:/id PCWDiagnostic", UrlCheck.Invalid)]
    [InlineData("data:text/html,<b>x</b>", UrlCheck.Invalid)]
    [InlineData("notepad.exe", UrlCheck.Invalid)]
    [InlineData("example.com", UrlCheck.Invalid)]
    [InlineData("/etc/passwd", UrlCheck.Invalid)]
    [InlineData("http://", UrlCheck.Invalid)]
    [InlineData("https://exa\nmple.com", UrlCheck.Invalid)]
    [InlineData("", UrlCheck.Invalid)]
    [InlineData(null, UrlCheck.Invalid)]
    public void Url_must_be_an_absolute_non_file_uri(string? text, UrlCheck expected) =>
        Assert.Equal(expected, RingActions.CheckUrl(text));

    [Fact]
    public void Url_is_opened_escaped()
    {
        Assert.True(RingActions.TryParseUrl("https://example.com/a b", out var uri, out var check));
        Assert.Equal(UrlCheck.Allowed, check);
        Assert.Equal("https://example.com/a%20b", uri.AbsoluteUri);
        Assert.False(RingActions.TryParseUrl(new string('a', RingActions.MaxUrlLength) + ".com", out _, out _));
    }

    [Theory]
    [InlineData("example.com", "https://example.com")]
    [InlineData("www.example.com/yol?q=1", "https://www.example.com/yol?q=1")]
    [InlineData("https://a.b", "https://a.b")]
    [InlineData("ms-settings:display", "ms-settings:display")]
    [InlineData(@"C:\x.exe", null)]
    [InlineData(@"\\server\share", null)]
    [InlineData("notepad", null)]
    [InlineData("iki kelime.com", null)]
    [InlineData(".hidden", null)]
    [InlineData("", null)]
    public void Typed_address_gets_https(string text, string? expected) =>
        Assert.Equal(expected, RingActions.TryNormalizeUrl(text));

    [Fact]
    public void Bare_keys_parse_only_for_sent_chords()
    {
        Assert.False(Hotkey.TryParse("F5", out _));                                   // genel kısayol: değiştirici şart
        Assert.True(Hotkey.TryParse("F5", out var f5, requireModifier: false));
        Assert.Equal(new Hotkey(HotkeyModifiers.None, "F5"), f5);
        Assert.True(Hotkey.TryParse("Ctrl+F5", out _));
        Assert.True(Hotkey.TryParse("Ctrl+F5", out _, requireModifier: true));
        Assert.False(Hotkey.TryParse("Ctrl+Alt", out _, requireModifier: false));      // tuşsuz
        Assert.False(Hotkey.TryParse("A+B", out _, requireModifier: false));           // iki tuş
    }

    [Fact]
    public void Key_items_are_capped_at_eight_chords()
    {
        Assert.True(RingActions.ValidKeys(["F5"]));
        Assert.True(RingActions.ValidKeys(["Ctrl+Shift+T", "Enter"]));
        Assert.True(RingActions.ValidKeys(Enumerable.Repeat("Ctrl+C", RingActions.MaxChords).ToList()));
        Assert.False(RingActions.ValidKeys(Enumerable.Repeat("Ctrl+C", RingActions.MaxChords + 1).ToList()));
        Assert.False(RingActions.ValidKeys([]));
        Assert.False(RingActions.ValidKeys(null));
        Assert.False(RingActions.ValidKeys(["Ctrl+Alt"]));
        Assert.Equal(8, RingActions.MaxChords);
    }

    [Fact]
    public void Text_items_are_capped()
    {
        Assert.Equal(10_000, RingActions.MaxTextLength);
        Assert.True(RingActions.ValidText(new string('x', RingActions.MaxTextLength)));
        Assert.False(RingActions.ValidText(new string('x', RingActions.MaxTextLength + 1)));
        Assert.False(RingActions.ValidText(""));
        Assert.False(RingActions.ValidText(null));
    }

    [Fact]
    public void Only_valid_enabled_known_items_run()
    {
        Assert.True(RingActions.IsRunnable(new LauncherAction { Type = RingActionTypes.Url, Url = "https://example.com" }));
        Assert.True(RingActions.IsRunnable(new LauncherAction { Type = RingActionTypes.Url, Url = "ms-settings:display" }));  // eklenirken onaylandı
        Assert.False(RingActions.IsRunnable(new LauncherAction { Type = RingActionTypes.Url, Url = @"C:\x.exe" }));         // elle düzenlenmiş dosya
        Assert.False(RingActions.IsRunnable(new LauncherAction { Type = RingActionTypes.Url, Url = "https://example.com", Disabled = true }));
        Assert.True(RingActions.IsRunnable(new LauncherAction { Type = RingActionTypes.Command, Command = "notepad.exe" }));
        Assert.False(RingActions.IsRunnable(new LauncherAction { Type = RingActionTypes.Command, Command = " " }));
        Assert.False(RingActions.IsRunnable(new LauncherAction { Type = RingActionTypes.Keys, Keys = Enumerable.Repeat("F5", 9).ToList() }));
        Assert.True(RingActions.IsRunnable(new LauncherAction { Type = RingActionTypes.Text, Text = "Merhaba" }));
        Assert.True(RingActions.IsRunnable(RingActions.ForApp(RingAppIds.NewNote)));
        Assert.False(RingActions.IsRunnable(new LauncherAction { Type = RingActionTypes.App, App = "OrganizeNow" }));
        Assert.False(RingActions.IsRunnable(new LauncherAction { Type = "macro" }));
        Assert.False(RingActions.IsRunnable(new LauncherAction()));
    }

    [Fact]
    public void Imported_commands_keys_and_text_stay_off_until_confirmed()
    {
        LauncherAction Item(string type, string? url = null) =>
            new() { Type = type, Url = url, Command = "cmd", Keys = ["F5"], Text = "x", App = RingAppIds.OpenApp };

        Assert.True(RingActions.MarkImported(Item(RingActionTypes.Command)));
        Assert.True(RingActions.MarkImported(Item(RingActionTypes.Keys)));
        Assert.True(RingActions.MarkImported(Item(RingActionTypes.Text)));
        Assert.True(RingActions.MarkImported(Item(RingActionTypes.Url, "ms-settings:display")));
        Assert.True(RingActions.MarkImported(Item(RingActionTypes.Url, @"C:\x.exe")));
        Assert.False(RingActions.MarkImported(Item(RingActionTypes.Url, "https://example.com")));
        Assert.False(RingActions.MarkImported(Item(RingActionTypes.App)));

        var command = Item(RingActionTypes.Command);
        RingActions.MarkImported(command);
        Assert.True(command.Disabled);
        Assert.False(RingActions.IsRunnable(command));
        Assert.False(RingActions.MarkImported(command));   // zaten kapalı
    }

    [Fact]
    public void App_ids_are_own_strings_mapped_to_hotkey_actions()
    {
        Assert.Equal(new[] { "new-note", "peek-desktop", "quick-add", "open-app" }, RingAppIds.All);
        foreach (var id in RingAppIds.All)
        {
            var action = RingAppIds.ToHotkeyAction(id);
            Assert.NotNull(action);
            Assert.Equal(id, RingAppIds.FromHotkeyAction(action!.Value));
            Assert.False(string.IsNullOrWhiteSpace(RingAppIds.Label(id)));
        }
        Assert.Null(RingAppIds.FromHotkeyAction(HotkeyAction.OrganizeNow));   // Paused'u atlar: halkada yok
        Assert.Null(RingAppIds.FromHotkeyAction(HotkeyAction.Ring));
        Assert.Null(RingAppIds.ToHotkeyAction(nameof(HotkeyAction.NewNote)));  // enum adı kalıcı kimlik değildir
        Assert.Null(RingAppIds.Label("yok"));
        using var _ = L.Use(Lang.En);
        Assert.Equal("Open NestDesk", RingAppIds.Label(RingAppIds.OpenApp));
        Assert.Equal(RingAppIds.All.Count, RingAppIds.All.Select(RingAppIds.Label).Distinct().Count());
    }

    [Fact]
    public void Ring_seed_is_a_titled_launcher_with_app_actions_only()
    {
        var seed = WidgetSeeds.Create(WidgetSeeds.Ring)!;
        Assert.Equal(WidgetKind.Launcher, seed.Kind);
        Assert.Equal(WidgetVariants.Ring, WidgetVariants.Of(seed));
        Assert.Equal("Halka menü", seed.Title);
        var tab = Assert.Single(seed.Tabs);
        Assert.Empty(tab.Items);
        Assert.Equal(RingAppIds.Seed, tab.Actions!.Select(a => a.App));
        Assert.All(tab.Actions!, a =>
        {
            Assert.Equal(RingActionTypes.App, a.Type);
            Assert.True(RingActions.IsRunnable(a));
            Assert.Null(a.Command);
        });
        Assert.Equal(tab.Actions!.Count, tab.Actions.Select(a => a.Id).Distinct().Count());
        Assert.Equal(WidgetSeeds.Ring, WidgetSeeds.Keys[^1]);   // compat-probe tohumları konumla seçer: sona eklenir

        using var _ = L.Use(Lang.En);
        Assert.Equal("Ring menu", WidgetSeeds.Create(WidgetSeeds.Ring)!.Title);
    }

    // ---------------------------------------------------------------- halka kısayolu önerisi

    [Fact]
    public void Suggest_starts_with_ctrl_alt_r()
    {
        Assert.Equal("Ctrl+Alt+R", RingHotkey.Suggest(new HotkeySettings()));
        Assert.All(RingHotkey.Candidates, c => Assert.True(RingHotkey.Suggestable(c), c));
        Assert.False(RingHotkey.Suggestable("Win+Alt+R"));
        Assert.False(RingHotkey.Suggestable("Ctrl+Alt+F12"));
        Assert.False(RingHotkey.Suggestable("Ctrl+Space"));
        Assert.False(RingHotkey.Suggestable("R"));
    }

    [Fact]
    public void Suggest_skips_shortcuts_already_used()
    {
        var settings = new HotkeySettings { OpenApp = "ctrl+alt+r", NewNote = "Alt+Ctrl+K" };
        Assert.Equal("Ctrl+Alt+J", RingHotkey.Suggest(settings));
        settings.QuickAdd = "Ctrl+Alt+J";
        settings.PeekDesktop = "Ctrl+Alt+Y";
        Assert.Null(RingHotkey.Suggest(settings));

        // Daha yeni sürümün kısayolu (Extra) da kullanılıyor sayılır.
        var future = JsonSerializer.Deserialize<HotkeySettings>("""{ "Gelecek": "Ctrl+Alt+R" }""", JsonFile.Options)!;
        Assert.Equal("Ctrl+Alt+K", RingHotkey.Suggest(future));
    }

    [Fact]
    public void Suggest_skips_failed_and_tried_ring_shortcuts_but_ignores_other_failures()
    {
        var settings = new HotkeySettings();
        var otherFailed = new Dictionary<HotkeyAction, string> { [HotkeyAction.OrganizeNow] = "Başka bir uygulama kullanıyor" };
        Assert.Equal("Ctrl+Alt+R", RingHotkey.Suggest(settings, otherFailed));

        settings.Set(HotkeyAction.Ring, "Ctrl+Alt+R");
        var ringFailed = new Dictionary<HotkeyAction, string> { [HotkeyAction.Ring] = "Başka bir uygulama kullanıyor" };
        Assert.Equal("Ctrl+Alt+K", RingHotkey.Suggest(settings, ringFailed));
        Assert.Equal("Ctrl+Alt+R", RingHotkey.Suggest(settings));      // kayıt başarılıysa kendi kısayolu "kullanılmış" sayılmaz
        Assert.Equal("Ctrl+Alt+J", RingHotkey.Suggest(settings, ringFailed, tried: ["Ctrl+Alt+K"]));
        Assert.Null(RingHotkey.Suggest(settings, ringFailed, tried: ["Ctrl+Alt+K", "Ctrl+Alt+J", "Ctrl+Alt+Y"]));
    }
}
