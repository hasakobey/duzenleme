using System.Xml.Linq;

namespace Duzenleme.Tests;

/// <summary>
/// Kolay NestDesk 3b: Widget'lar sayfası widget'ların yönetim yeridir (görünüm şeridi, liste + gömülü Düzenle paneli, katlı
/// "Daha fazla"); Ayarlar yedi satır ve aynı sayfada "Gelişmiş ayarlar" görünümüdür. Hiçbir ayar düşmez, yalnızca yer değiştirir.
/// </summary>
public class ManagePagesTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>3b öncesi Ayarlar'daki kimlikler: "Windows masaüstü simgeleri" kutusu anahtara döndü, gerisi yerinde durur.</summary>
    private static readonly string[] SettingsIdsBefore =
    [
        "Ai", "AiPrivacy", "AnimationsNote", "BeautifyAll", "BoxAppShortcuts", "BoxCount", "BoxLeaves", "BoxLeavesText", "Channel",
        "CrashMode", "DeleteLogs", "DiagnosticPackage", "DiagnosticPage", "DiscardPendingCrash", "DoubleClick", "ErrorLog", "ErrorLogOn",
        "Exit", "FolderIcons", "HideNow", "HideWidgets", "Hotkeys", "IconModeText", "Language", "Locations", "LogStatus",
        "Motion.Calm", "Motion.Lively", "Motion.Off", "Motion.OpenWindows", "NewWidgetPlacement", "Notifications", "OpenBoxFolder",
        "OpenData", "OpenDesktop", "OpenLogs", "OpenThemes", "Peek", "PeekHidesWidgets", "PeekMinutes", "PeekShowsDesktop",
        "PendingCrash", "Privacy", "PublicBox", "Releases", "RemoveAllFolderIcons", "ReportProblem", "ResetHotkeys", "Restart",
        "ReturnAll", "Ring", "RingAdd", "RingHoldSelect", "RingShortcut", "RingTarget", "SavePendingCrash", "Startup", "StartupState",
        "SuggestIcons", "SystemIcons", "Theme", "Version", "Welcome",
    ];

    private static XElement View(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static HashSet<string> Ids(XElement root) =>
        root.Descendants().Select(e => (string?)e.Attribute("AutomationProperties.AutomationId")).OfType<string>().ToHashSet();

    [Fact]
    public void Settings_keep_every_setting_and_split_into_two_views()
    {
        var doc = XDocument.Load(Path.Combine(RepoRoot(), "src", "Duzenleme", "Views", "SettingsPage.xaml"));
        var main = View(doc, "MainView");
        var advanced = View(doc, "AdvancedView");
        var all = Ids(main).Concat(Ids(advanced)).Select(id => id["Settings.".Length..]).ToHashSet();
        foreach (var id in SettingsIdsBefore) Assert.Contains(id, all);
        // Simge kipi artık anahtar; Snap ve PreventOverlap Widget'lar sayfasından geldi.
        Assert.DoesNotContain("IconMode", all);
        foreach (var id in new[] { "IconsInFences", "BoxChange", "EditRing", "HowTo", "Advanced", "Back", "Snap", "PreventOverlap" })
            Assert.Contains(id, all);

        // İlk görünüm: dil, başlangıç, simgeler, halka, yardım, hakkında ve "Gelişmiş ayarlar ›" (başlık ve bölüm adı dışında).
        var rows = main.Elements().Where(e => e.Name.LocalName is "Border" or "Button").ToList();
        Assert.Equal(7, rows.Count);
        Assert.Equal("Settings.Advanced", (string?)rows[^1].Attribute("AutomationProperties.AutomationId"));
        Assert.Single(main.Elements(), e => (string?)e.Attribute("Text") == "{l:T 'Hakkında'}");
        var mainIds = Ids(main);
        foreach (var id in new[] { "Settings.Language", "Settings.Startup", "Settings.IconsInFences", "Settings.Ring", "Settings.RingShortcut",
                     "Settings.HowTo", "Settings.Welcome", "Settings.ReportProblem", "Settings.Privacy", "Settings.Releases", "Settings.Exit" })
            Assert.Contains(id, mainIds);
        // Gelişmiş görünüm geri düğmesiyle başlar ve "Çoğu zaman varsayılanlar yeterli." der.
        Assert.Equal("Settings.Back", (string?)advanced.Elements().First().Attribute("AutomationProperties.AutomationId"));
        Assert.Contains(advanced.Elements(), e => (string?)e.Attribute("Text") == "{l:T 'Çoğu zaman varsayılanlar yeterli.'}");
    }

    [Fact]
    public void Settings_switch_listens_to_checked_and_goes_through_desktop_modes()
    {
        var code = Read("Views", "SettingsPage.xaml.cs");
        var xaml = Read("Views", "SettingsPage.xaml");
        Assert.Contains("Checked=\"IconsInFences_Changed\" Unchecked=\"IconsInFences_Changed\"", xaml);
        Assert.Contains("Checked=\"SnapToggle_Changed\" Unchecked=\"SnapToggle_Changed\"", xaml);
        Assert.Contains("Checked=\"OverlapToggle_Changed\" Unchecked=\"OverlapToggle_Changed\"", xaml);
        Assert.Matches(@"IconsInFences_Changed[\s\S]*?if \(_loadingDesktop\) return;[\s\S]*?DesktopModes\.Set\(", code);
        // "Halkayı düzenle" Ana sayfadakinin aynısı; "Nasıl kullanılır" kartları yeniden gösterir.
        Assert.Contains("HomeRing.Edit(", code);
        Assert.Contains("HowToCards.ShowAgain()", code);
        // Kısayol kutusuna odaklanmak (halka kısayolu önerilemedi) önce Gelişmiş görünümü açar.
        Assert.Matches(@"private void FocusHotkeyBox\(HotkeyAction action\)\s*\{\s*ShowView\(advanced: true", code);
        Assert.Contains("Motion.Enter(target, MotionKind.Page)", code);
    }

    [Fact]
    public void Widgets_page_is_the_management_center()
    {
        var xaml = Read("Views", "WidgetsPage.xaml");
        var code = Read("Views", "WidgetsPage.xaml.cs");
        // Tek ekleme yolu, galeri ve renkler bütün widget'lara (geri alınır), liste ve gömülü panel.
        Assert.Contains("ShowQuickAdd()", code);
        Assert.Contains("new LookGalleryControl(\"Widgets.Look.\"", code);
        Assert.Contains("AppHost.Widgets.ChangeLookForAll(change)", code);
        Assert.Contains("AppHost.Widgets.UndoLookCopy(before)", code);
        Assert.Contains("new() { ShowDone = false }", code);
        Assert.Contains("WidgetEditorWindow.ShowFor(window)", code);
        Assert.Contains("PageLife.WhileShown(", code);
        Assert.Matches(@"detach: \(\) =>[\s\S]*?_editor\.Release\(\);", code);
        Assert.Contains("AppHost.Widgets.Reveal(row.Id)", code);
        // Düzenle panelinin ya da Ayarlar'ın verdiği denetimler burada yinelenmez.
        foreach (var gone in new[] { "ComboBox", "ToggleSwitch", "ThemeBox", "StyleBox", "AccentBox", "SnapToggle", "OverlapToggle", "Remove_Click" })
            Assert.DoesNotContain(gone, xaml + code);
        foreach (var id in new[] { "Widgets.AddWidget", "Widgets.List", "Widgets.EmptyAdd", "Widgets.More", "Widgets.Arrange",
                     "Widgets.RevealAll", "Widgets.SaveLayout", "Widgets.Backups" })
            Assert.Contains($"AutomationProperties.AutomationId=\"{id}\"", xaml);
        // Liste satırının şablonunda hareket yok (fareyle yalnızca zemin, hemen).
        Assert.DoesNotContain("Storyboard", xaml);
    }

    /// <summary>
    /// İki sayfanın simgeleri gerçekten var (yoksa sayfa açılırken XamlParseException) ve WPF-UI'nin çizebildiği aralıkta
    /// (U+FFFF üstü "X" çizilir).
    /// </summary>
    [Fact]
    public void Page_symbols_exist_and_are_drawable()
    {
        foreach (var page in new[] { "SettingsPage.xaml", "WidgetsPage.xaml" })
        {
            var xaml = Read("Views", page);
            var names = System.Text.RegularExpressions.Regex.Matches(xaml, @"Symbol=""(\w+)""|SymbolIcon (\w+)\}")
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Distinct().ToList();
            Assert.NotEmpty(names);
            foreach (var name in names)
            {
                Assert.True(Enum.TryParse<Wpf.Ui.Controls.SymbolRegular>(name, out var symbol), $"{page}: {name}");
                Assert.True((int)symbol is > 0 and <= 0xFFFF, $"{page}: {name}");
            }
        }
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([RepoRoot(), "src", "Duzenleme", .. parts]));

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
