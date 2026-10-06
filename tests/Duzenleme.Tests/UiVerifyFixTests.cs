using System.Text.RegularExpressions;
using Duzenleme.Core;
using Duzenleme.Localization;
using Duzenleme.Views;

namespace Duzenleme.Tests;

/// <summary>
/// 2.2 canlı arayüz denemesinde bulunan hataların gerileme testleri: açılır halkanın belleği (yazılımla çizim) ve ilk
/// açılıştaki ölçeği, halka açılışının başarım sayacında bir kez sayılması, karşılamanın "Şimdilik hayır" önizlemesi, kural
/// kartının ekran okuyucu adı, Düzenle panelinin büyüyen kartın yanına yeniden yerleşmesi, halka pencerelerinin ekranı,
/// Ayarlar'ın başından açılması ve taşınan widget'ın etkinleşmemesi.
/// </summary>
public class UiVerifyFixTests
{
    [Fact]
    public void Ring_popup_renders_in_software_and_reads_its_scale_from_the_window()
    {
        var popup = Code("Views", "RingPopup.cs");
        // Donanım çiziminde her gösterilişte ekran kartı yüzeyi yeniden kuruluyor, bellek geri gelmiyordu (+130 MB / 17 açılış).
        Assert.Contains("RenderMode.SoftwareOnly", popup);
        // Hiç gösterilmemiş pencerede VisualTreeHelper.GetDpi 1 döner: ölçek HwndSource'un dönüşümünden okunur.
        Assert.Contains("_scale = WindowScale;", popup);
        Assert.Matches(@"ScaleIs\(double scale\) => Math\.Abs\(WindowScale - scale\)", popup);
        Assert.Contains("CompositionTarget?.TransformToDevice.M11", popup);
        Assert.Contains("CompositionTarget?.TransformToDevice.M11", Code("Widgets", "RingView.cs"));
    }

    [Fact]
    public void Ring_open_is_counted_once_in_the_perf_log()
    {
        var motion = Code("Views", "Motion.cs");
        // Pencerenin solması (AppearCore) sayar; öğelerin kademesi açılışta ayrıca saymaz, geçişte (RingSwap) ilk öğe sayar.
        Assert.Matches(@"public static void RingOpen\([\s\S]*?RingItems\([^;]*count: false\);", motion);
        Assert.Matches(@"public static void RingSwap\([\s\S]*?RingItems\([^;]*count: true\);", motion);
        Assert.Contains("count: count && i == 0", motion);
    }

    [Fact]
    public void Welcome_preview_does_not_warn_when_moving_is_declined()
    {
        var welcome = Code("Views", "WelcomeWindow.xaml.cs");
        Assert.Matches(@"if \(MoveNo\.IsChecked == true\)\s*ShowPreview\(warning: false", welcome);
        Assert.Matches(@"MoveNo\.Checked \+= \(_, _\) => \{ UpdateNext\(\); UpdatePreview\(\); \};", welcome);
        using var _ = L.Use(Lang.En);
        Assert.Equal("No files will be moved for now; the files on your desktop stay where they are.",
            L.T("Şimdilik hiçbir dosya taşınmaz; masaüstündeki dosyalara dokunulmaz."));
    }

    [Fact]
    public void Rule_cards_have_a_readable_automation_name()
    {
        var rule = new Rule { TargetFolder = "PDF", Extensions = ["pdf", ".XPS", "pdf"] };
        var row = new RuleRow(rule, 0, _ => { });
        Assert.Equal("PDF ← .pdf, .xps", row.ToString());
        rule.Enabled = false;
        Assert.Equal("PDF ← .pdf, .xps (kapalı)", row.ToString());
        using (L.Use(Lang.En)) Assert.Equal("PDF ← .pdf, .xps (off)", row.ToString());
        Assert.DoesNotContain("Duzenleme", new RuleRow(new Rule(), 1, _ => { }).ToString());
    }

    [Fact]
    public void Edit_panel_moves_only_when_the_grown_card_runs_under_it()
    {
        var card = new Box(100, 100, 400, 300);
        Assert.False(WidgetEditorWindow.ShouldReanchor(new Box(412, 100, 862, 700), card));
        Assert.True(WidgetEditorWindow.ShouldReanchor(new Box(380, 100, 830, 700), card));
        Assert.False(WidgetEditorWindow.ShouldReanchor(null, card));
        Assert.False(WidgetEditorWindow.ShouldReanchor(new Box(380, 100, 830, 700), null));
        var editor = Code("Views", "WidgetEditorWindow.cs");
        Assert.Contains("widget.SizeChanged += OnCardSizeChanged;", editor);
        Assert.Contains("widget.SizeChanged -= OnCardSizeChanged;", editor);
        // Pencere sınırları canlandırılmaz: yalnızca bir kez SetWindowPos.
        Assert.DoesNotMatch(@"BeginAnimation|Storyboard", editor);
    }

    [Fact]
    public void Ring_windows_open_on_the_rings_screen()
    {
        var popup = Code("Views", "RingPopup.cs");
        Assert.Contains("RingEditor.Open(config, tab, at)", popup);
        Assert.Contains("RingEditor.Open(empty, near: at)", popup);
        var dialog = Code("Views", "RingActionDialog.cs");
        Assert.Contains("DialogPlacement.CenterOn(Window, _anchor?.CenterPoint ?? _near);", dialog);
        var editor = Code("Views", "RingEditor.cs");
        Assert.Equal(5, Regex.Matches(editor, @"RingActionDialog\.\w+\([^;]*_anchor\(\), _near\(\)\)").Count);
        Assert.Contains("near: () => CenterNow() ?? point", editor);
    }

    [Fact]
    public void Settings_page_starts_at_the_top()
    {
        var settings = Code("Views", "SettingsPage.xaml.cs");
        Assert.Contains("ShowView(_advancedRequested, focus: false, toTop: true);", settings);
        Assert.Contains("else if (changed || toTop) FindScroller(this)?.ScrollToTop();", settings);
    }

    [Fact]
    public void Moving_a_widget_to_another_screen_does_not_activate_it()
    {
        var widget = Code("Widgets", "WidgetWindow.cs");
        // WM_DPICHANGED'i WPF SWP_NOACTIVATE olmadan işler: kendiliğinden yapılan taşımalar QuietShow.Move içinde.
        foreach (var method in new[] { "MoveToFreeSpot", "RestoreSavedPosition", "MoveTo" })
        {
            var body = Regex.Match(widget, @"(?:void|public void) " + method + @"\([^)]*\)\s*\{[\s\S]*?\n    \}").Value;
            Assert.Contains("QuietShow.Move(", body);
        }
        Assert.Contains("public static void Move(IntPtr hwnd, Action move)", Code("Widgets", "QuietShow.cs"));
    }

    // ---------------------------------------------------------------- yardımcılar

    private static string Code(params string[] parts) =>
        string.Join("\n", File.ReadAllLines(Path.Combine([SourceRoot, .. parts])).Select(line =>
        {
            var index = line.IndexOf("//", StringComparison.Ordinal);
            return index >= 0 && !line[..index].Contains('"') ? line[..index] : line;
        }));

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
