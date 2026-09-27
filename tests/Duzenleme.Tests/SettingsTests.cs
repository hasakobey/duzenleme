using System.Text.Json;
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
    public void Locked_settings_file_throws_instead_of_returning_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{ "Paused": true }""");
        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.ThrowsAny<IOException>(() => JsonFile.Load(path, () => new AppSettings()));
            Assert.True(JsonFile.Load(path, () => new AppSettings()).Paused);
        }
        finally { File.Delete(path); }
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
        File.WriteAllText(path, """{ "Paused": true, "FirstRunDone": true, "Widgets": [ { "Kind": "Clock", "Left": "NaN" } ] }""");
        try
        {
            var s = JsonFile.Load(path, () => new AppSettings());
            Assert.True(s.Paused);
            Assert.Equal("Ctrl+Alt+H", s.Hotkeys.ToggleDesktop);
            Assert.True(s.DoubleClickHidesDesktop);
            Assert.Equal(1.0, s.Widgets[0].Scale);
            Assert.NotEmpty(s.Rules);
            // 1.x kullanıcısı: karşılamayı görmez, "Düzenleme artık NestDesk" balonunu bir kez görür.
            Assert.True(s.FirstRunDone);
            Assert.False(s.RenameNoticeShown);
            Assert.False(s.CloseToTrayHintShown);
            Assert.False(s.Widgets[0].NoteChecklist);
        }
        finally { File.Delete(path); }
    }

    /// <summary>Eski sürüm yeni alanları yok sayar (System.Text.Json varsayılanı); ayarları bozuk sayıp silmez.</summary>
    [Fact]
    public void Unknown_properties_are_ignored()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, """{ "Widgets": [ { "Kind": "Note", "GelecekOzellik": true, "NoteText": "x" } ], "GelecekAyar": 1 }""");
        try
        {
            var s = JsonFile.Load(path, () => new AppSettings());
            Assert.Equal("x", Assert.Single(s.Widgets).NoteText);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".bozuk-*"));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Kalıcı enum'a yeni üye eklenirse eski sürüm dosyayı okuyamaz ve bütün ayarları ".bozuk-*" yapar. Yeni özellikler
    /// (ör. Yapılacaklar) yalnızca bilinen enum adlarıyla yazılmalı.
    /// </summary>
    [Fact]
    public void New_features_use_only_known_enum_names()
    {
        var json = JsonSerializer.Serialize(new WidgetConfig { Kind = WidgetKind.Note, NoteChecklist = true }, JsonFile.Options);
        using var doc = JsonDocument.Parse(json);

        Assert.Contains(doc.RootElement.GetProperty("Kind").GetString(), new[] { "Clock", "Date", "Fence", "Note", "Launcher" });
        Assert.True(doc.RootElement.GetProperty("NoteChecklist").GetBoolean());
    }

    [Fact]
    public void Layout_snapshot_keeps_checklist_flag()
    {
        var snap = LayoutSnapshot.Capture("Liste", [new WidgetConfig { Kind = WidgetKind.Note, NoteChecklist = true, NoteText = "☐ süt" }]);

        var restored = Assert.Single(snap.Restore());
        Assert.True(restored.NoteChecklist);
        Assert.Equal("☐ süt", restored.NoteText);
    }
}
