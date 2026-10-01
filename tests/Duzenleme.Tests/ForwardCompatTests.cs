using System.Text.Json;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// 2.1'den sonraki sürümlerin yazdığı ayarlar 2.1'de silinmesin: tanınmayan enum değeri bütün dosyayı bozuk saydırmaz
/// (yedek değer), bilinmeyen alanlar okunup aynen geri yazılır.
/// </summary>
public class ForwardCompatTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-compat-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    private AppSettings Load(string json)
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, json);
        return JsonFile.Load(path, () => new AppSettings());
    }

    private int BrokenBackups => Directory.GetFiles(_dir, "settings.json.bozuk-*").Length;

    [Fact]
    public void Unknown_widget_kind_does_not_wipe_settings()
    {
        var logged = new List<string>();
        JsonFile.Log = logged.Add;
        try
        {
            var s = Load("""{ "Paused": true, "Widgets": [ { "Kind": "Timer", "Id": "a" }, { "Kind": "Note", "Id": "b", "NoteText": "x" } ] }""");
            Assert.True(s.Paused);
            Assert.Equal(2, s.Widgets.Count);
            Assert.Equal(WidgetKind.Clock, s.Widgets[0].Kind);    // yedek: enum'un ilk değeri
            Assert.Equal("x", s.Widgets[1].NoteText);
            Assert.Equal(0, BrokenBackups);
            Assert.Contains(logged, l => l.Contains("WidgetKind") && l.Contains("Timer"));
        }
        finally { JsonFile.Log = null; }
    }

    [Theory]
    [InlineData("""{ "Kind": "Fence", "Sort": "Size" }""", nameof(WidgetConfig.Sort), "Newest")]
    [InlineData("""{ "Kind": "Fence", "Filter": "Images" }""", nameof(WidgetConfig.Filter), "All")]       // EnumFallback: hiçbir öğe görünmez kalmasın
    [InlineData("""{ "Kind": "Note", "NoteColor": "Orange" }""", nameof(WidgetConfig.NoteColor), "Yellow")]
    [InlineData("""{ "Kind": "Fence", "IconSize": 9 }""", nameof(WidgetConfig.IconSize), "Small")]          // tanımsız sayı
    [InlineData("""{ "Kind": "Fence", "View": "Icons, List" }""", nameof(WidgetConfig.View), "Icons")]      // bayrak olmayan enum'da liste
    public void Unknown_enum_value_falls_back(string widget, string property, string expected)
    {
        var s = Load($$"""{ "Paused": true, "Widgets": [ {{widget}} ] }""");
        Assert.True(s.Paused);
        var value = typeof(WidgetConfig).GetProperty(property)!.GetValue(Assert.Single(s.Widgets));
        Assert.Equal(expected, value!.ToString());
        Assert.Equal(0, BrokenBackups);
    }

    [Fact]
    public void Known_enum_values_read_as_before()
    {
        var s = Load("""{ "Theme": "dark", "Widgets": [ { "Kind": "launcher", "Style": 2, "Sort": "Type", "Corners": " Soft " } ] }""");
        var w = Assert.Single(s.Widgets);
        Assert.Equal(AppTheme.Dark, s.Theme);
        Assert.Equal(WidgetKind.Launcher, w.Kind);
        Assert.Equal(WidgetStyle.Glass, w.Style);
        Assert.Equal(FenceSort.Type, w.Sort);
        Assert.Equal(CornerStyle.Soft, w.Corners);
    }

    [Fact]
    public void Enums_are_written_by_name()
    {
        var json = JsonSerializer.Serialize(new WidgetConfig { Kind = WidgetKind.Launcher, Style = WidgetStyle.Light, Filter = DesktopFilter.Shortcuts }, JsonFile.Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Launcher", doc.RootElement.GetProperty("Kind").GetString());
        Assert.Equal("Light", doc.RootElement.GetProperty("Style").GetString());
        Assert.Equal("Shortcuts", doc.RootElement.GetProperty("Filter").GetString());
    }

    [Fact]
    public void Flags_enum_round_trips()
    {
        var mods = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt;
        var json = JsonSerializer.Serialize(mods, JsonFile.Options);
        Assert.Equal(mods, JsonSerializer.Deserialize<HotkeyModifiers>(json, JsonFile.Options));
        Assert.Equal(HotkeyModifiers.None, JsonSerializer.Deserialize<HotkeyModifiers>("\"Ctrl, Hyper\"", JsonFile.Options));
    }

    [Fact]
    public void Nullable_enum_reads_null_and_unknown()
    {
        Assert.Null(JsonSerializer.Deserialize<WidgetKind?>("null", JsonFile.Options));
        Assert.Equal(WidgetKind.Clock, JsonSerializer.Deserialize<WidgetKind?>("\"Gelecek\"", JsonFile.Options));
    }

    [Fact]
    public void Unknown_properties_survive_load_save_and_clone()
    {
        var s = Load("""
            {
              "Paused": true, "GelecekAyar": { "a": 1 },
              "Hotkeys": { "ToggleDesktop": "Ctrl+Alt+H", "GelecekKisayol": "Ctrl+Alt+J" },
              "Rules": [ { "TargetFolder": "PDF", "Extensions": ["pdf"], "Oncelik": 3 } ],
              "Layouts": [ { "Name": "İş", "Renk": "mavi", "Widgets": [] } ],
              "Widgets": [ { "Kind": "Launcher", "Hava": "güneşli",
                             "Tabs": [ { "Name": "Web", "Items": ["C:\\a.exe"], "Labels": { "C:\\a.exe": "Benim" } } ],
                             "Timer": null } ]
            }
            """);
        var saved = Path.Combine(_dir, "resaved.json");
        JsonFile.Save(saved, s);
        using var doc = JsonDocument.Parse(File.ReadAllText(saved));
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("GelecekAyar").GetProperty("a").GetInt32());
        Assert.Equal("Ctrl+Alt+J", root.GetProperty("Hotkeys").GetProperty("GelecekKisayol").GetString());
        Assert.Equal(3, root.GetProperty("Rules")[0].GetProperty("Oncelik").GetInt32());
        Assert.Equal("mavi", root.GetProperty("Layouts")[0].GetProperty("Renk").GetString());
        var widget = root.GetProperty("Widgets")[0];
        Assert.Equal("güneşli", widget.GetProperty("Hava").GetString());
        Assert.Equal("Benim", widget.GetProperty("Tabs")[0].GetProperty("Labels").GetProperty(@"C:\a.exe").GetString());

        // Kopya (Çoğalt, geri getir, düzen) da korur.
        var clone = s.Widgets[0].Clone();
        Assert.Equal("güneşli", clone.Extra!["Hava"].GetString());
        Assert.True(clone.Tabs[0].Extra!.ContainsKey("Labels"));
    }

    [Fact]
    public void Timer_state_and_zones_round_trip()
    {
        var config = new WidgetConfig
        {
            Kind = WidgetKind.Clock, Variant = WidgetVariants.Timer,
            Timer = new TimerState { Mode = TimerModes.Pomodoro, Round = 3, Phase = TimerModes.Break, EndsUtc = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc) },
            Zones = [new WorldZone { Id = "Tokyo Standard Time", Label = "Ofis" }],
        };
        var copy = config.Clone();
        Assert.Equal(TimerModes.Pomodoro, copy.Timer!.Mode);
        Assert.Equal(3, copy.Timer.Round);
        Assert.Equal(TimerModes.Break, copy.Timer.Phase);
        Assert.Equal(config.Timer.EndsUtc, copy.Timer.EndsUtc);
        Assert.Equal(DateTimeKind.Utc, copy.Timer.EndsUtc!.Value.Kind);
        Assert.Equal("Ofis", Assert.Single(copy.Zones!).Label);
    }

    [Fact]
    public void Empty_new_fields_are_not_written()
    {
        var json = JsonSerializer.Serialize(new WidgetConfig { Kind = WidgetKind.Note }, JsonFile.Options);
        foreach (var name in new[] { "Variant", "SortBy", "ItemOrder", "ChecklistDoneLast", "Clock12Hour", "FolderKnownId", "FirstDayOfWeek",
                     "TargetDate", "CountdownYearly", "Timer", "Zones", "StatusIntervalSeconds", "StatusDrive", "Extra" })
            Assert.DoesNotContain($"\"{name}\"", json);
        var calendar = JsonSerializer.Serialize(new WidgetConfig { Kind = WidgetKind.Date, Variant = WidgetVariants.Month, CountdownYearly = true }, JsonFile.Options);
        Assert.Contains("\"Variant\": \"month\"", calendar);
        Assert.Contains("\"CountdownYearly\": true", calendar);
    }

    [Fact]
    public void Settings_from_20_load_with_new_defaults()
    {
        var s = Load("""{ "Widgets": [ { "Kind": "Note", "NoteText": "x" }, { "Kind": "Fence", "FolderName": "PDF" } ] }""");
        Assert.All(s.Widgets, w =>
        {
            Assert.Null(w.Variant);
            Assert.Null(w.SortBy);
            Assert.Null(w.Timer);
            Assert.False(w.ChecklistDoneLast);
            Assert.Null(w.Clock12Hour);
        });
        Assert.Null(s.Extra);
    }

    [Fact]
    public void Copied_rules_keep_fields_written_by_a_newer_version()
    {
        // Daha yeni sürümün kurala eklediği bir alan: F2 ile klasör yeniden adlandırılıp "Kuralları güncelle" denince ya da
        // karşılama yeniden bitirilince kaybolmamalı.
        var rules = Load("""{ "Rules": [ { "TargetFolder": "PDF", "Extensions": ["pdf"], "Enabled": true, "Schedule": "daily" } ] }""").Rules;
        Assert.True(rules[0].Extra?.ContainsKey("Schedule"));

        var renamed = PathRenames.RetargetRules(rules, "PDF", "Belgelerim")!;
        Assert.Equal("Belgelerim", renamed[0].TargetFolder);
        Assert.Equal("daily", renamed[0].Extra!["Schedule"].GetString());
        Assert.NotSame(rules[0], renamed[0]);
        Assert.Equal("PDF", rules[0].TargetFolder); // eski liste yerinde değişmez (izleyici okuyor olabilir)

        var chosen = Onboarding.ApplyFolderSelection(rules, ["PDF"], ["PDF"]);
        Assert.Equal("daily", chosen[0].Extra!["Schedule"].GetString());
        Assert.Contains("\"Schedule\": \"daily\"", System.Text.Encoding.UTF8.GetString(JsonFile.Serialize(new AppSettings { Rules = chosen })));
    }

    // ---------------------------------------------------------------- 2.2 Halka

    /// <summary>2.1'in ayar biçimi (Halka alanlarını tanımaz; bilinmeyenleri Extra'da aynen geri yazar).</summary>
    private sealed class Settings21
    {
        public bool Paused { get; set; }
        public List<Widget21> Widgets { get; set; } = [];
        public Hotkeys21 Hotkeys { get; set; } = new();

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    private sealed class Widget21
    {
        public string Id { get; set; } = "";
        public WidgetKind Kind { get; set; }
        public string? Variant { get; set; }
        public string? Title { get; set; }
        public List<Tab21> Tabs { get; set; } = [];

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    private sealed class Tab21
    {
        public string Name { get; set; } = "";
        public List<string> Items { get; set; } = [];

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    private sealed class Hotkeys21
    {
        public string ToggleDesktop { get; set; } = "Ctrl+Alt+H";
        public string PeekDesktop { get; set; } = "Ctrl+Alt+G";

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    private static AppSettings RingSettings()
    {
        var ring = WidgetSeeds.Create(WidgetSeeds.Ring)!;
        var root = ring.Tabs[0];
        LauncherEntries.Insert(root, 1, LauncherEntry.Of(@"C:\Users\x\Desktop\a.lnk"));
        LauncherEntries.Insert(root, 99, LauncherEntry.Of(@"C:\Users\x\Desktop\b.lnk"));
        LauncherEntries.Insert(root, 2, LauncherEntry.Of(new LauncherAction
        {
            Type = RingActionTypes.Url, Url = "https://example.com", Name = "Örnek", Icon = "sym:Globe24",
        }));
        LauncherEntries.Insert(root, 99, LauncherEntry.Of(new LauncherAction
        {
            Type = RingActionTypes.Command, Command = "notepad.exe", Args = "x.txt", Dir = @"C:\Users\x", HideWindow = true, Admin = true,
        }));
        var sub = new LauncherTab { Name = "Alt" };
        LauncherEntries.Insert(sub, 0, LauncherEntry.Of(new LauncherAction { Type = RingActionTypes.Keys, Keys = ["Ctrl+Shift+T", "F5"] }));
        LauncherEntries.Insert(sub, 1, LauncherEntry.Of(new LauncherAction { Type = RingActionTypes.Text, Text = "Merhaba\ndünya", Disabled = true }));
        LauncherEntries.Insert(sub, 2, LauncherEntry.Of(JsonSerializer.Deserialize<LauncherAction>(
            """{ "Id": "gelecek1", "Type": "gesture", "Path": "LRL", "PathsBefore": 0 }""", JsonFile.Options)!));
        ring.Tabs.Add(sub);
        var settings = new AppSettings { Paused = true, Widgets = [ring], RingWidgetId = ring.Id, RingHoldSelectOff = true };
        settings.Hotkeys.Set(HotkeyAction.Ring, "Ctrl+Alt+R");
        return settings;
    }

    [Fact]
    public void Ring_actions_and_settings_survive_a_round_trip_through_21()
    {
        var original = RingSettings();
        var json22 = JsonSerializer.Serialize(original, JsonFile.Options);

        // 2.1 açar: sekme düzeyindeki Actions ve yeni ayarlar Extra'da durur.
        var old = JsonSerializer.Deserialize<Settings21>(json22, JsonFile.Options)!;
        var widget = Assert.Single(old.Widgets);
        Assert.Equal(WidgetKind.Launcher, widget.Kind);
        Assert.Equal("Halka menü", widget.Title);
        Assert.True(widget.Tabs[0].Extra!.ContainsKey("Actions"));
        Assert.True(widget.Tabs[1].Extra!.ContainsKey("Actions"));
        Assert.True(old.Extra!.ContainsKey("RingWidgetId"));
        Assert.True(old.Extra.ContainsKey("RingHoldSelectOff"));
        Assert.Equal("Ctrl+Alt+R", old.Hotkeys.Extra!["Ring"].GetString());

        // 2.1 Actions'ı bilmeden kutuyu düzenler: ilk yolu siler, sona yol ekler; sonra kaydeder.
        widget.Tabs[0].Items = [@"C:\Users\x\Desktop\b.lnk", @"C:\Users\x\Desktop\c.lnk"];
        var json21 = JsonSerializer.Serialize(old, JsonFile.Options);

        // 2.2 yeniden açar: bozuk yok, hiçbir eylem kaybolmaz, alanları aynı.
        var back = Load(json21);
        Assert.Equal(0, BrokenBackups);
        var ring = Assert.Single(back.Widgets);
        Assert.Equal(WidgetVariants.Ring, WidgetVariants.Of(ring));
        Assert.Equal(original.RingWidgetId, back.RingWidgetId);
        Assert.True(back.RingHoldSelectOff);
        Assert.Equal("Ctrl+Alt+R", back.Hotkeys.Get(HotkeyAction.Ring));
        for (var t = 0; t < 2; t++)
        {
            var before = original.Widgets[0].Tabs[t].Actions!;
            var after = ring.Tabs[t].Actions!;
            Assert.Equal(before.Count, after.Count);
            for (var i = 0; i < before.Count; i++)
            {
                before[i].PathsBefore = after[i].PathsBefore = 0;   // sıra yolların değişmesiyle kayabilir; içerik aynı kalmalı
                Assert.Equal(JsonSerializer.Serialize(before[i], JsonFile.Options), JsonSerializer.Serialize(after[i], JsonFile.Options));
            }
        }
        Assert.Equal(6 + 2, LauncherEntries.Count(ring.Tabs[0]));      // 4 uygulama + url + komut; b ve c
        Assert.Equal("LRL", ring.Tabs[1].Actions![2].Extra!["Path"].GetString());   // tanınmayan tür alanlarıyla korunur
        Assert.False(RingActions.IsRunnable(ring.Tabs[1].Actions![1]));           // onaylanmamış metin kapalı kalır
    }

    [Fact]
    public void Ring_survives_clone_and_layout_snapshot()
    {
        var ring = RingSettings().Widgets[0];
        var copy = LayoutSnapshot.Capture("Düzen", [ring]).Restore()[0];
        Assert.Equal(JsonSerializer.Serialize(ring, JsonFile.Options), JsonSerializer.Serialize(copy, JsonFile.Options));
        Assert.NotSame(ring.Tabs[0].Actions![0], copy.Tabs[0].Actions![0]);
    }

    [Fact]
    public void Empty_ring_fields_are_not_written()
    {
        var action = JsonSerializer.Serialize(RingActions.ForApp(RingAppIds.NewNote), JsonFile.Options);
        using (var doc = JsonDocument.Parse(action))
            Assert.Equal(new[] { "App", "Id", "Type" }, doc.RootElement.EnumerateObject().Select(p => p.Name).Order());

        var plain = JsonSerializer.Serialize(new AppSettings(), JsonFile.Options);
        foreach (var name in new[] { "RingWidgetId", "RingHoldSelectOff", "\"Ring\"" })
            Assert.DoesNotContain(name, plain);
        var tab = JsonSerializer.Serialize(new LauncherTab { Name = "Uygulamalar", Items = ["a"] }, JsonFile.Options);
        Assert.DoesNotContain("Actions", tab);

        var hotkeys = new HotkeySettings();
        hotkeys.Set(HotkeyAction.Ring, "Ctrl+Alt+R");
        using (var written = JsonDocument.Parse(JsonSerializer.Serialize(hotkeys, JsonFile.Options)))
            Assert.Equal("Ctrl+Alt+R", written.RootElement.GetProperty("Ring").GetString());
        hotkeys.Set(HotkeyAction.Ring, "");
        Assert.DoesNotContain("Ring", JsonSerializer.Serialize(hotkeys, JsonFile.Options));
    }

    [Fact]
    public void Old_hotkey_file_leaves_the_ring_off()
    {
        var s = Load("""{ "Hotkeys": { "ToggleDesktop": "Ctrl+Alt+H", "NewNote": "Ctrl+Shift+N" } }""");
        Assert.Null(s.Hotkeys.Ring);
        Assert.Equal("", s.Hotkeys.Get(HotkeyAction.Ring));
        Assert.False(s.Hotkeys.Accepts(HotkeyAction.Ring));
        Assert.Equal("Ctrl+Shift+N", s.Hotkeys.NewNote);
        Assert.Null(s.RingWidgetId);
        Assert.False(s.RingHoldSelectOff);
    }
}
