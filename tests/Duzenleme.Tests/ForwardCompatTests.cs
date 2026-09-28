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
}
