using System.Text;
using System.Text.Json;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Altın ayar dosyaları (Golden/): 2.0.0'ın ve 2.1.1'in yazdığı biçimde gerçek kullanıcı dosyaları ve daha yeni bir sürümün
/// yazdığı ("future") bir dosya. Her biri <c>.bozuk-*</c> olmadan yüklenmeli, iki kayıt turunda aynı kalmalı ve bilinmeyen
/// veriyi korumalı. 2.1.1 dosyası bugünkü kodla kaydedilince birebir aynı çıkmalı: yeni özellikler boşken yazılmaz.
/// Dosyalar yalnızca geçici klasöre kopyalanıp orada okunur.
/// </summary>
[Collection(JsonFileLogCollection.Name)]
public class GoldenSettingsTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-golden-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Golden", name);

    /// <summary>Altın dosyayı geçici klasöre settings.json olarak kopyalar ve uygulamanın yoluyla yükler.</summary>
    private AppSettings Load(string name, string file = "settings.json")
    {
        var path = Path.Combine(_dir, file);
        File.Copy(Fixture(name), path, overwrite: true);
        return JsonFile.Load(path, () => new AppSettings());
    }

    private int BrokenBackups => Directory.GetFiles(_dir, "*.bozuk-*").Length;

    private static string Text(byte[] json) => Encoding.UTF8.GetString(json);

    private static string Lines(string text) => text.Replace("\r\n", "\n").TrimEnd();

    public static TheoryData<string> Fixtures => new() { "settings-2.0.0.json", "settings-2.1.1.json", "settings-future.json" };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Loads_without_a_corrupt_backup(string name)
    {
        Assert.True(File.Exists(Fixture(name)), Fixture(name));
        var s = Load(name);
        Assert.Equal(0, BrokenBackups);
        Assert.True(s.FirstRunDone);    // varsayılana düşmedi
        Assert.NotEmpty(s.Widgets);
        Assert.All(s.Widgets, w => Assert.Equal(32, w.Id.Length));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Saving_twice_gives_the_same_file(string name)
    {
        var first = Text(JsonFile.Serialize(Load(name)));
        var again = Path.Combine(_dir, "again.json");
        File.WriteAllText(again, first);
        var second = Text(JsonFile.Serialize(JsonFile.Load(again, () => new AppSettings())));
        Assert.Equal(first, second);

        var third = Path.Combine(_dir, "third.json");
        File.WriteAllText(third, second);
        Assert.Equal(second, Text(JsonFile.Serialize(JsonFile.Load(third, () => new AppSettings()))));
        Assert.Equal(0, BrokenBackups);
    }

    [Fact]
    public void Settings_from_200_keep_their_widgets_and_get_new_defaults()
    {
        var s = Load("settings-2.0.0.json");
        Assert.Equal(AppTheme.Dark, s.Theme);
        Assert.True(s.RenameNoticeShown);
        Assert.Equal(7, s.Rules.Count);
        Assert.Equal("Arşivler", s.Rules[3].TargetFolder);
        Assert.False(s.Rules[6].Enabled);
        Assert.Equal(["cs", "py"], s.Rules[6].Extensions);

        Assert.Equal([WidgetKind.Clock, WidgetKind.Date, WidgetKind.Fence, WidgetKind.Fence, WidgetKind.Fence, WidgetKind.Note, WidgetKind.Note, WidgetKind.Launcher],
            s.Widgets.Select(w => w.Kind));
        Assert.True(s.Widgets[0].ShowSeconds);
        Assert.Equal(WidgetAccent.Green, s.Widgets[0].Accent);
        Assert.True(double.IsNaN(s.Widgets[0].Width));
        Assert.Equal(["week", "greeting"], s.Widgets[1].HiddenParts);
        Assert.Equal("PDF", s.Widgets[2].FolderName);
        Assert.Equal(ItemView.List, s.Widgets[2].View);
        Assert.Equal(DesktopFilter.All, s.Widgets[3].Filter);
        Assert.Equal("Masaüstü", s.Widgets[3].Title);
        Assert.Contains("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", s.Widgets[3].HiddenItems);
        Assert.Equal(DesktopFilter.Shortcuts, s.Widgets[4].Filter);
        Assert.Equal(0.8, s.Widgets[4].Opacity);
        Assert.Equal(CornerStyle.Square, s.Widgets[4].Corners);
        Assert.Equal("Alışveriş\r\n- süt\r\n- ekmek", s.Widgets[5].NoteText);
        Assert.True(s.Widgets[6].NoteChecklist);
        Assert.True(s.Widgets[6].Locked);
        Assert.StartsWith("☐ fatura", s.Widgets[6].NoteText);
        var box = s.Widgets[7];
        Assert.Equal(1, box.ActiveTab);
        Assert.Equal(["Uygulamalar", "Dosyalar"], box.Tabs.Select(t => t.Name));
        Assert.Equal(@"D:\Projeler", box.Tabs[1].Items[1]);

        Assert.Equal("", s.Hotkeys.OrganizeNow);
        Assert.Equal("Ctrl+Alt+B", s.Hotkeys.QuickAdd);
        Assert.Equal("Ctrl+Alt+G", s.Hotkeys.PeekDesktop);       // 2.0'da yoktu: varsayılan
        var layout = Assert.Single(s.Layouts);
        Assert.Equal("İş düzeni", layout.Name);
        Assert.Equal(WidgetKind.Clock, Assert.Single(layout.Widgets).Kind);

        // 2.1 ve 2.2 alanları varsayılanında; bilinmeyen veri yok.
        Assert.All(s.Widgets, w =>
        {
            Assert.Null(w.Variant);
            Assert.Null(w.Icon);
            Assert.Null(w.ItemLooks);
            Assert.Null(w.SortBy);
            Assert.Null(w.Timer);
            Assert.Null(w.BoxFolder);
            Assert.Null(w.Extra);
            Assert.All(w.Tabs, t => Assert.Null(t.Extra));
        });
        Assert.Null(s.Language);
        Assert.True(s.PeekHidesWidgets);
        Assert.Equal(PlaceModes.Cursor, s.NewWidgetPlacement);
        Assert.False(s.AnimationsOff);
        Assert.Null(s.SavedWithAppVersion);
        Assert.Null(s.Extra);
        Assert.Null(s.Hotkeys.Extra);
        Assert.Equal(new SettingsVersionChange(SettingsVersionKind.Upgrade, null), SettingsVersion.Check(s.SavedWithAppVersion, "2.2.0"));
    }

    [Fact]
    public void Settings_from_211_keep_every_211_feature()
    {
        var s = Load("settings-2.1.1.json");
        Assert.Equal("tr", s.Language);
        Assert.Equal(DesktopState.DoubleClickPeek, s.DoubleClickAction);
        Assert.Equal(5, s.PeekMinutes);
        Assert.Equal(PlaceModes.Corner, s.NewWidgetPlacement);
        Assert.True(s.BoxItemsLeaveDesktop);
        Assert.True(s.AnimationsOff);
        Assert.True(s.FencesReplaceIcons);
        Assert.Equal([@"C:\Users\ali\Desktop\PDF", @"D:\Projeler"], s.IconFolders!);
        Assert.Equal("Win+Shift+N", s.Hotkeys.NewNote);
        Assert.False(string.IsNullOrEmpty(s.AiKeyProtected));
        Assert.False(s.Rules[5].Enabled);

        var byId = s.Widgets.ToDictionary(w => w.Id[^2..]);
        Assert.Equal(15, byId.Count);
        Assert.True(byId["01"].Clock12Hour);
        Assert.Equal("sym:Clock24", byId["01"].Icon);
        var timer = byId["02"];
        Assert.Equal(WidgetVariants.Timer, WidgetVariants.Of(timer));
        Assert.Equal(TimerModes.Pomodoro, timer.Timer!.Mode);
        Assert.Equal(DateTimeKind.Utc, timer.Timer.EndsUtc!.Value.Kind);
        Assert.Equal(new DateTime(2026, 10, 1, 12, 30, 0, DateTimeKind.Utc), timer.Timer.EndsUtc);
        Assert.Equal(["Tokyo Standard Time", "GMT Standard Time"], byId["03"].Zones!.Select(z => z.Id));
        Assert.Equal(WidgetVariants.System, WidgetVariants.Of(byId["04"]));
        Assert.Equal(5, byId["04"].StatusIntervalSeconds);
        Assert.Equal(1, byId["05"].FirstDayOfWeek);
        var countdown = byId["06"];
        Assert.Equal(WidgetVariants.Countdown, WidgetVariants.Of(countdown));
        Assert.Equal(new DateTime(2026, 12, 31), countdown.TargetDate);
        Assert.Equal(DateTimeKind.Unspecified, countdown.TargetDate!.Value.Kind);
        Assert.True(countdown.CountdownYearly);
        Assert.Equal("manual", byId["08"].SortBy);
        Assert.Equal(2, byId["08"].ItemOrder!.Count);
        Assert.Equal("size", byId["09"].SortBy);
        var portal = byId["10"];
        Assert.True(WidgetVariants.IsPortal(portal));
        Assert.Equal("Downloads", portal.FolderKnownId);
        Assert.True(byId["12"].NoteChecklist);
        Assert.True(byId["12"].ChecklistDoneLast);
        Assert.Equal(NoteColor.Graphite, byId["13"].NoteColor);
        var box = byId["14"];
        Assert.Equal("Uygulamalar", box.BoxFolder);
        Assert.Equal("Web", ItemLooks.Get(box, @"c:\users\ali\desktop\Tarayıcı.LNK")?.Name);
        Assert.Equal(@"res:C:\Windows\System32\shell32.dll,12", ItemLooks.Get(box, @"C:\Program Files\Not\not.exe")?.Icon);
        Assert.Equal(WidgetVariants.Recycle, WidgetVariants.Of(byId["15"]));

        Assert.Equal(2, Assert.Single(s.Layouts).Widgets.Count);
        Assert.Null(s.Extra);
        Assert.All(s.Widgets, w => Assert.Null(w.Extra));
        Assert.Null(s.SavedWithAppVersion);
        Assert.Equal(SettingsVersionKind.Upgrade, SettingsVersion.Check(s.SavedWithAppVersion, "2.2.0").Kind);
    }

    [Fact]
    public void Settings_from_211_are_written_back_byte_for_byte()
    {
        // 2.2'de eklenen ya da eklenecek her alan boşken yazılmamalı (CLAUDE.md: [JsonIgnore(WhenWritingNull/Default)]);
        // yoksa her 2.1 kullanıcısının dosyası ilk kayıtta büyür ve 2.1'e dönünce Extra'ya düşer.
        var original = File.ReadAllText(Fixture("settings-2.1.1.json"));
        var saved = Text(JsonFile.Serialize(Load("settings-2.1.1.json")));
        Assert.Equal(Lines(original), Lines(saved));
    }

    [Fact]
    public void Settings_from_a_newer_version_keep_what_this_version_does_not_know()
    {
        var s = Load("settings-future.json");
        Assert.Equal(0, BrokenBackups);
        Assert.True(s.Paused);
        Assert.Equal("9.1.0", s.SavedWithAppVersion);
        Assert.Equal(SettingsVersionKind.Downgrade, SettingsVersion.Check(s.SavedWithAppVersion, AppInfo.Version).Kind);
        Assert.Equal(new SettingsVersionChange(SettingsVersionKind.Downgrade, "9.1.0"), SettingsVersion.Check(s.SavedWithAppVersion, "2.2.0"));

        // Tanınmayan enum değerleri yedek değerine döner (bütün dosya bozuk sayılmaz).
        Assert.Equal(AppTheme.System, s.Theme);
        Assert.Equal(WidgetStyle.Dark, s.Widgets[0].Style);
        Assert.Equal(DesktopFilter.All, s.Widgets[1].Filter);       // [EnumFallback(All)]
        Assert.Equal(IconSize.Small, s.Widgets[1].IconSize);
        Assert.Equal(NoteColor.Yellow, s.Widgets[2].NoteColor);
        Assert.Equal(WidgetKind.Clock, s.Widgets[3].Kind);

        // Bilinmeyen alanlar her düzeyde Extra'da. Burada yalnızca hiçbir sürümün gerçek alanı olmayacak adlar ("Zz_")
        // denetlenir: 2.2'nin planlanan alanları (CalmMotion, CrashReportMode, Hotkeys.Ring, sekme Actions, LookTheme…)
        // eklendikçe Extra'dan çıkar; onlar aşağıda yalnızca kaydedilen çıktıdan denetlenir (alan varken de yokken de doğru).
        Assert.True(s.Extra!.ContainsKey("Zz_GelecekNesne"));
        Assert.Equal("Ctrl+Alt+Q", s.Hotkeys.Extra!["Zz_Hologram"].GetString());
        Assert.Equal("daily", s.Rules[0].Extra!["Zz_Schedule"].GetString());
        var ring = s.Widgets[0];
        Assert.Equal("ring", ring.Variant);
        Assert.True(ring.Extra!["Zz_Gelecek"].GetProperty("Halka").GetBoolean());
        Assert.Equal("#336699", ring.Tabs[0].Extra!["Zz_Color"].GetString());
        Assert.Equal("#FF0000", ring.ItemLooks!.Values.Single().Extra!["Zz_Color"].GetString());
        Assert.True(s.Widgets[1].Extra!.ContainsKey("Zz_Virtualize"));
        Assert.Equal("hologram", s.Widgets[3].Variant);
        Assert.Equal(3, s.Widgets[3].Extra!["Zz_Projection"].GetArrayLength());
        Assert.Equal("bell", s.Widgets[4].Timer!.Extra!["Zz_Sound"].GetString());
        Assert.Equal(2, s.Widgets[4].Zones![0].Extra!["Zz_Order"].GetInt32());
        Assert.Equal("mavi", s.Layouts[0].Extra!["Zz_Renk"].GetString());
        Assert.Equal(1, s.Layouts[0].Widgets[0].Extra!["Zz_Gelecek"].GetInt32());

        // Kaydedince de korunur (2.1'e dönüp yeniden 2.2'ye geçen kullanıcı kaybetmez): bilinmeyenler ve 2.2'nin alanları.
        using var doc = JsonDocument.Parse(JsonFile.Serialize(s));
        var root = doc.RootElement;
        Assert.Equal("9.1.0", root.GetProperty("SavedWithAppVersion").GetString());
        Assert.Equal("ç", root.GetProperty("Zz_GelecekNesne").GetProperty("c").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("Zz_GelecekNesne").GetProperty("a")[2].GetProperty("b").ValueKind);
        Assert.True(root.GetProperty("CalmMotion").GetBoolean());
        Assert.Equal("always", root.GetProperty("CrashReportMode").GetString());
        Assert.True(root.GetProperty("ErrorLogOff").GetBoolean());
        Assert.Equal("90000000000000000000000000000001", root.GetProperty("RingWidgetId").GetString());
        var hotkeys = root.GetProperty("Hotkeys");
        Assert.Equal("Ctrl+Alt+R", hotkeys.GetProperty("Ring").GetString());
        Assert.Equal("Ctrl+Alt+Q", hotkeys.GetProperty("Zz_Hologram").GetString());
        Assert.Equal("daily", root.GetProperty("Rules")[0].GetProperty("Zz_Schedule").GetString());

        var saved = root.GetProperty("Widgets")[0];
        Assert.Equal("ring", saved.GetProperty("Variant").GetString());
        Assert.Equal("neon", saved.GetProperty("LookTheme").GetString());
        Assert.Equal("#12AB34", saved.GetProperty("LookAccentColor").GetString());
        Assert.True(saved.GetProperty("QuickKeys").GetBoolean());
        Assert.Equal("glow", saved.GetProperty("RingStyle").GetString());
        Assert.True(saved.GetProperty("Zz_Gelecek").GetProperty("Halka").GetBoolean());
        var tabs = saved.GetProperty("Tabs");
        Assert.Equal(2, tabs[0].GetProperty("Actions").GetArrayLength());
        var action = tabs[0].GetProperty("Actions")[0];
        Assert.Equal("url", action.GetProperty("Kind").GetString());
        Assert.Equal("https://example.com/", action.GetProperty("Url").GetString());
        Assert.Equal(7, action.GetProperty("Extra1").GetInt32());
        var unknownAction = tabs[0].GetProperty("Actions")[1];
        Assert.Equal("teleport", unknownAction.GetProperty("Kind").GetString());     // tanınmayan eylem türü de korunur
        Assert.Equal("mars", unknownAction.GetProperty("Target").GetString());
        Assert.True(unknownAction.GetProperty("Disabled").GetBoolean());
        Assert.Equal("#336699", tabs[0].GetProperty("Zz_Color").GetString());
        Assert.Equal("new-note", tabs[1].GetProperty("Actions")[0].GetProperty("App").GetString());
        var look = saved.GetProperty("ItemLooks").EnumerateObject().Single().Value;
        Assert.Equal("Web", look.GetProperty("Name").GetString());
        Assert.Equal("#FF0000", look.GetProperty("Zz_Color").GetString());

        var widgets = root.GetProperty("Widgets");
        Assert.Equal(150, widgets[1].GetProperty("Zz_Virtualize").GetProperty("From").GetInt32());
        Assert.Equal("kagit", widgets[2].GetProperty("LookTheme").GetString());
        Assert.Equal(3, widgets[3].GetProperty("Zz_Projection").GetArrayLength());
        Assert.Equal("bell", widgets[4].GetProperty("Timer").GetProperty("Zz_Sound").GetString());
        Assert.Equal(2, widgets[4].GetProperty("Zones")[0].GetProperty("Zz_Order").GetInt32());
        var layout = root.GetProperty("Layouts")[0];
        Assert.Equal("mavi", layout.GetProperty("Zz_Renk").GetString());
        Assert.Equal("gece", layout.GetProperty("Widgets")[0].GetProperty("LookTheme").GetString());
        Assert.Equal(1, layout.GetProperty("Widgets")[0].GetProperty("Zz_Gelecek").GetInt32());
    }
}

/// <summary>
/// <see cref="JsonFile.Log"/>'a dokunan ya da onu tetikleyen (tanınmayan enum değeri okuyan) testler aynı anda çalışmasın:
/// <c>ForwardCompatTests</c> günlüğü bir listeye bağlar.
/// </summary>
[CollectionDefinition(Name)]
public sealed class JsonFileLogCollection
{
    public const string Name = "JsonFile.Log";
}
