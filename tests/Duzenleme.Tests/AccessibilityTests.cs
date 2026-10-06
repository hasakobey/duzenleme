using System.Xml.Linq;

namespace Duzenleme.Tests;

/// <summary>Ekran okuyucu (UI Automation) adları: seçim düğmeleri kendi adlarıyla okunur.</summary>
public class AccessibilityTests
{
    /// <summary>
    /// İçeriği olan seçim düğmesine (RadioButton, CheckBox, ToggleButton, ToggleSwitch) LabeledBy verilmez: WPF'te UIA adı
    /// etiketten gelir ve içeriğin adını ezer. Ayarlar > Animasyonlar'da Canlı / Sakin / Kapalı'nın üçü de "Animasyonlar"
    /// okunuyordu; hangisinin seçili olduğu duyulmuyordu. Grubun başlığı HelpText ile verilir. İçerik öznitelikle, metinle,
    /// alt öğeyle ya da <c>&lt;RadioButton.Content&gt;</c> özellik öğesiyle (ToggleSwitch'te OnContent/OffContent da) verilebilir;
    /// LabeledBy öznitelikle, özellik öğesiyle ya da bir seçim düğmesi stilinin Setter'ıyla gelebilir.
    /// </summary>
    [Fact]
    public void Choice_buttons_with_content_keep_their_own_name()
    {
        var problems = new List<string>();
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        foreach (var file in Directory.EnumerateFiles(src, "*.xaml", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            if (rel.StartsWith("obj", StringComparison.OrdinalIgnoreCase) || rel.StartsWith("bin", StringComparison.OrdinalIgnoreCase)) continue;
            problems.AddRange(LabeledChoiceButtons(XDocument.Load(file, LoadOptions.SetLineInfo)).Select(line => $"{rel}:{line}"));
        }
        Assert.Empty(problems);
    }

    /// <summary>Taramanın kendisi: içerik ve LabeledBy'nin her yazılışı yakalanır, içeriksiz düğmenin etiketi serbesttir.</summary>
    [Theory]
    [InlineData("""<RadioButton Content="Canlı" AutomationProperties.LabeledBy="{Binding ElementName=T}" />""", true)]
    [InlineData("""<CheckBox AutomationProperties.LabeledBy="{Binding ElementName=T}">Göster</CheckBox>""", true)]
    [InlineData("""<RadioButton AutomationProperties.LabeledBy="{Binding ElementName=T}"><RadioButton.Content>Canlı</RadioButton.Content></RadioButton>""", true)]
    [InlineData("""<RadioButton AutomationProperties.LabeledBy="{Binding ElementName=T}"><TextBlock Text="Canlı" /></RadioButton>""", true)]
    [InlineData("""<ui:ToggleSwitch OnContent="Açık" OffContent="Kapalı" AutomationProperties.LabeledBy="{Binding ElementName=T}" />""", true)]
    [InlineData("""<ui:ToggleSwitch AutomationProperties.LabeledBy="{Binding ElementName=T}"><ui:ToggleSwitch.OnContent>Açık</ui:ToggleSwitch.OnContent></ui:ToggleSwitch>""", true)]
    [InlineData("""<RadioButton Content="Canlı"><AutomationProperties.LabeledBy><Binding ElementName="T" /></AutomationProperties.LabeledBy></RadioButton>""", true)]
    [InlineData("""<Style x:Key="Segment" TargetType="RadioButton"><Setter Property="AutomationProperties.LabeledBy" Value="{Binding ElementName=T}" /></Style>""", true)]
    [InlineData("""<Style TargetType="{x:Type ui:ToggleSwitch}"><Setter Property="AutomationProperties.LabeledBy" Value="{Binding ElementName=T}" /></Style>""", true)]
    [InlineData("""<ui:ToggleSwitch AutomationProperties.LabeledBy="{Binding ElementName=T}" />""", false)]
    [InlineData("""<RadioButton Content="Canlı" AutomationProperties.HelpText="Animasyonlar" />""", false)]
    [InlineData("""<Style TargetType="TextBox"><Setter Property="AutomationProperties.LabeledBy" Value="{Binding ElementName=T}" /></Style>""", false)]
    public void Labeled_choice_button_scan_catches_every_spelling(string xaml, bool problem)
    {
        var doc = XDocument.Parse($"""<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">{xaml}</Grid>""",
            LoadOptions.SetLineInfo);
        Assert.Equal(problem, LabeledChoiceButtons(doc).Any());
    }

    private static readonly string[] ChoiceButtons = ["RadioButton", "CheckBox", "ToggleButton", "ToggleSwitch"];
    private const string LabeledBy = "AutomationProperties.LabeledBy";

    /// <summary>LabeledBy ile adı ezilen içerikli seçim düğmelerinin (ve LabeledBy veren seçim düğmesi stillerinin) satırları.</summary>
    private static IEnumerable<int> LabeledChoiceButtons(XDocument doc)
    {
        foreach (var element in doc.Descendants())
        {
            var name = element.Name.LocalName;
            if (ChoiceButtons.Contains(name))
            {
                var hasContent = element.Attribute("Content") is not null || element.Attribute("OnContent") is not null
                                 || element.Attribute("OffContent") is not null
                                 || element.Nodes().Any(n => n is XText { Value: var text } && text.Trim().Length > 0
                                                             || n is XElement child && IsContentElement(name, child.Name.LocalName));
                var labeled = element.Attribute(LabeledBy) is not null || element.Elements().Any(c => c.Name.LocalName == LabeledBy);
                if (hasContent && labeled) yield return ((System.Xml.IXmlLineInfo)element).LineNumber;
            }
            else if (name == "Setter" && (string?)element.Attribute("Property") == LabeledBy
                     && element.Parent is { Name.LocalName: "Style" } style && TargetsChoiceButton((string?)style.Attribute("TargetType")))
                yield return ((System.Xml.IXmlLineInfo)element).LineNumber;
        }
    }

    /// <summary>Alt öğe içerik mi: düz alt öğe ya da Content/OnContent/OffContent özellik öğesi (öbür özellik öğeleri değil).</summary>
    private static bool IsContentElement(string owner, string child) =>
        !child.Contains('.') || child == owner + ".Content" || child == owner + ".OnContent" || child == owner + ".OffContent";

    private static bool TargetsChoiceButton(string? targetType)
    {
        if (targetType is null) return false;
        var type = targetType.Replace("{x:Type", "").Trim(' ', '{', '}');
        type = type[(type.LastIndexOf(':') + 1)..];
        return ChoiceButtons.Contains(type);
    }

    /// <summary>Animasyonlar seçimi: her bölüm kendi adıyla, satırın başlığı yardım metni olarak.</summary>
    [Fact]
    public void Motion_segments_are_named_by_their_choice()
    {
        var doc = XDocument.Load(Path.Combine(RepoRoot(), "src", "Duzenleme", "Views", "SettingsPage.xaml"));
        var segments = doc.Descendants().Where(e => e.Name.LocalName == "RadioButton"
            && ((string?)e.Attribute("AutomationProperties.AutomationId"))?.StartsWith("Settings.Motion.", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(3, segments.Count);
        Assert.All(segments, s =>
        {
            Assert.NotNull(s.Attribute("Content"));
            Assert.Null(s.Attribute("AutomationProperties.Name"));
            Assert.Equal("{l:T 'Animasyonlar'}", (string?)s.Attribute("AutomationProperties.HelpText"));
        });
    }

    /// <summary>
    /// Ana sayfadaki her düğme ve anahtar ekran okuyucuda adıyla okunur: içeriği metin olan düğme metniyle, içeriği simge ve iki
    /// satırdan oluşan büyük düğme AutomationProperties.Name ile (ikinci büyük düğmenin adı halka olup olmamasına göre koddan
    /// verilir), anahtar Name ile. "Anladım" (HowToCards) metniyle ve kimliğiyle bulunur.
    /// </summary>
    [Fact]
    public void Home_page_controls_are_named()
    {
        var views = Path.Combine(RepoRoot(), "src", "Duzenleme", "Views");
        var doc = XDocument.Load(Path.Combine(views, "HomePage.xaml"));
        var code = File.ReadAllText(Path.Combine(views, "HomePage.xaml.cs"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var controls = doc.Descendants().Where(e => e.Name.LocalName is "Button" or "ToggleSwitch").ToList();
        Assert.True(controls.Count >= 7);
        Assert.All(controls, c =>
        {
            var name = (string?)c.Attribute(x + "Name");
            var named = c.Attribute("AutomationProperties.Name") is not null
                        || c.Name.LocalName == "Button" && c.Attribute("Content") is not null
                        || name is not null && code.Contains($"AutomationProperties.SetName({name},", StringComparison.Ordinal);
            Assert.True(named, $"{c.Name.LocalName} {name ?? (string?)c.Attribute("AutomationProperties.AutomationId")} adsız");
            Assert.NotNull(c.Attribute("AutomationProperties.AutomationId"));
        });

        var howTo = File.ReadAllText(Path.Combine(views, "HowToCards.cs"));
        Assert.Contains("Content = L.T(\"Anladım\")", howTo);
        Assert.Contains("\"Home.HowTo.Dismiss\"", howTo);
    }

    /// <summary>
    /// Düzenle paneli (kodla kurulur): her etkileşimli denetimin ekran okuyucu adı ve UI Automation kimliği vardır, kimlikler
    /// tektir ve plandaki kimliklerin hepsi bulunur (UIA ile deneme bunlarla yapılır). Seçim düğmelerinin adı kendi seçeneğidir.
    /// </summary>
    [Fact]
    public void Editor_controls_are_named()
    {
        EditorFakes.OnSta(() =>
        {
            var config = new Duzenleme.Core.WidgetConfig { Kind = Duzenleme.Core.WidgetKind.Fence, Filter = Duzenleme.Core.DesktopFilter.Files };
            var (editor, _, _) = EditorFakes.Open(config);
            var controls = EditorFakes.All(editor).Where(d => d is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.Primitives.TextBoxBase
                or System.Windows.Controls.ComboBox).ToList();
            Assert.NotEmpty(controls);
            foreach (var control in controls)
            {
                var id = System.Windows.Automation.AutomationProperties.GetAutomationId(control);
                Assert.False(string.IsNullOrEmpty(id), $"kimliksiz denetim: {control.GetType().Name} {EditorFakes.Text(control)}");
                var name = System.Windows.Automation.AutomationProperties.GetName(control);
                Assert.False(string.IsNullOrWhiteSpace(name) && EditorFakes.Text(control).Trim().Length == 0, "adsız denetim: " + id);
            }
            var ids = EditorFakes.Ids(editor);
            Assert.Equal(ids.Count, ids.Distinct().Count());
            string[] required =
            [
                "Editor.Icon", "Editor.Name", "Editor.Accent.1", "Editor.Accent.2", "Editor.Accent.3", "Editor.Accent.4", "Editor.Accent.5",
                "Editor.Accent.Custom", "Editor.ApplyAll", "Editor.Size.Small", "Editor.Size.Medium", "Editor.Size.Large", "Editor.Part.greeting",
                "Editor.Part.close", "Editor.More", "Editor.Undo", "Editor.Remove", "Editor.Done",
                .. Duzenleme.Core.LookGallery.All.Select(e => "Editor.Look." + e.Id),
            ];
            Assert.All(required, id => Assert.Contains(id, ids));

            // Görünümün kendi içeriği (yongalar, öğe listesi ve seçili satırın düğmeleri) de adlı ve kimlikli.
            var (native, _) = EditorFakes.Open(new FakeNativeView(config));
            EditorFakes.Click(EditorFakes.Find(native, "Editor.Item.1"));
            var nativeControls = EditorFakes.All(native).Where(d => d is System.Windows.Controls.Primitives.ButtonBase).ToList();
            foreach (var control in nativeControls)
            {
                var id = System.Windows.Automation.AutomationProperties.GetAutomationId(control);
                Assert.False(string.IsNullOrEmpty(id), $"kimliksiz denetim: {control.GetType().Name} {EditorFakes.Text(control)}");
                var name = System.Windows.Automation.AutomationProperties.GetName(control);
                Assert.False(string.IsNullOrWhiteSpace(name) && EditorFakes.Text(control).Trim().Length == 0, "adsız denetim: " + id);
            }
            var nativeIds = EditorFakes.Ids(native);
            Assert.Equal(nativeIds.Count, nativeIds.Distinct().Count());
            Assert.All(["Editor.Items", "Editor.Item.1", "Editor.Item.2", "Editor.Item.Remove", "Editor.Item.Up", "Editor.Item.Down", "Editor.Part.edit"],
                id => Assert.Contains(id, nativeIds));
        });

        // Pencerenin kendisi "Editor" (UIA'da FromHandle ile bulunur).
        var window = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "Views", "WidgetEditorWindow.cs"));
        Assert.Contains("AutomationProperties.SetAutomationId(this, \"Editor\")", window);
    }

    /// <summary>
    /// Halka düzenleyicisi klavyeyle de kullanılır: seçili öğe Alt+← / Alt+→ ile taşınır (fareyle "◀" / "▶"), Delete kaldırır;
    /// önizlemenin yerleri, düğmeler ve ekleme çubuğu adlı ve kimliklidir; açılır halkanın "Halkayı düzenle" düğmesi de.
    /// </summary>
    [Fact]
    public void Ring_editor_reorders_with_alt_arrows_and_names_its_controls()
    {
        var editor = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "Views", "RingEditor.cs"));
        Assert.Matches(@"Keyboard\.Modifiers == ModifierKeys\.Alt && key is Key\.Left or Key\.Right", editor);
        Assert.Contains("Step(clockwise: key == Key.Right)", editor);
        Assert.Contains("e.Key == Key.System ? e.SystemKey : e.Key", editor); // Alt basılıyken ok tuşu Key.System gelir
        Assert.Contains("key == Key.Delete", editor);
        foreach (var id in new[]
                 {
                     "\"RingEditor\"", "\"RingEditor.Slot.\"", "\"RingEditor.Left\"", "\"RingEditor.Right\"", "\"RingEditor.Remove\"",
                     "\"RingEditor.Change\"", "\"RingEditor.Add.App\"", "\"RingEditor.Add.Folder\"", "\"RingEditor.Add.Web\"",
                     "\"RingEditor.Add.Action\"", "\"RingEditor.Add.More\"",
                 })
            Assert.Contains(id, editor);
        // "◀" ve "▶" yalnızca simge: ekran okuyucu ne yaptıklarını duyar.
        Assert.Contains("Small(\"◀\", \"RingEditor.Left\", L.T(\"Ters yönde taşı\")", editor);
        Assert.Contains("Small(\"▶\", \"RingEditor.Right\", L.T(\"Saat yönünde taşı\")", editor);
        Assert.Contains("AutomationProperties.SetName(button, name)", editor);
        var popup = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "Views", "RingPopup.cs"));
        Assert.Contains("SetAutomationId(button, \"Ring.Edit\")", popup);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
