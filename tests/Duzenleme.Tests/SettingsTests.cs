using Duzenleme.Core;

namespace Duzenleme.Tests;

public class SettingsTests
{
    [Theory]
    [InlineData("Ctrl+Alt+H", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "H")]
    [InlineData("alt + shift + f5", HotkeyModifiers.Alt | HotkeyModifiers.Shift, "f5")]
    [InlineData("Win+Ctrl+d", HotkeyModifiers.Win | HotkeyModifiers.Ctrl, "D")]
    public void Hotkey_parses(string text, HotkeyModifiers mods, string key)
    {
        Assert.True(Hotkey.TryParse(text, out var hk));
        Assert.Equal(mods, hk.Modifiers);
        Assert.Equal(key, hk.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("H")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+A+B")]
    public void Hotkey_rejects_invalid(string text) => Assert.False(Hotkey.TryParse(text, out _));

    [Fact]
    public void Hotkey_formats_in_canonical_order()
    {
        Assert.True(Hotkey.TryParse("shift+win+alt+ctrl+k", out var hk));
        Assert.Equal("Ctrl+Alt+Shift+Win+K", hk.ToString());
    }

    [Fact]
    public void Layout_snapshot_is_independent_copy()
    {
        var live = new List<WidgetConfig>
        {
            new() { Kind = WidgetKind.Launcher, Left = 10, Tabs = [new LauncherTab { Name = "Oyun", Items = [@"C:\a.exe"] }] },
        };
        var snap = LayoutSnapshot.Capture("İş", live);

        live[0].Left = 999;
        live[0].Tabs[0].Items.Add(@"C:\b.exe");
        var restored = snap.Restore();
        restored[0].Tabs[0].Name = "değişti";

        Assert.Equal(10, snap.Widgets[0].Left);
        Assert.Single(snap.Widgets[0].Tabs[0].Items);
        Assert.Equal("Oyun", snap.Restore()[0].Tabs[0].Name);
    }

    [Fact]
    public void Corrupt_settings_are_backed_up_before_falling_back()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
        var path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, "{ bozuk");
        try
        {
            var s = JsonFile.Load(path, () => new AppSettings());
            Assert.NotNull(s);
            Assert.Single(Directory.GetFiles(dir, "settings.json.bozuk-*"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Numbers_written_as_strings_are_accepted()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{ "Widgets": [ { "Kind": "Note", "Left": "-1525", "Top": 8 } ] }""");
        try { Assert.Equal(-1525, JsonFile.Load(path, () => new AppSettings()).Widgets[0].Left); }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Old_settings_file_loads_with_new_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{ "Paused": true, "Widgets": [ { "Kind": "Clock", "Left": "NaN" } ] }""");
        try
        {
            var s = JsonFile.Load(path, () => new AppSettings());
            Assert.True(s.Paused);
            Assert.Equal("Ctrl+Alt+H", s.Hotkeys.ToggleDesktop);
            Assert.True(s.DoubleClickHidesDesktop);
            Assert.Equal(1.0, s.Widgets[0].Scale);
            Assert.NotEmpty(s.Rules);
        }
        finally { File.Delete(path); }
    }
}
