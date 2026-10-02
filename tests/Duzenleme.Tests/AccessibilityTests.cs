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

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
