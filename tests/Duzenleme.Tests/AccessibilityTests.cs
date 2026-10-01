using System.Xml.Linq;

namespace Duzenleme.Tests;

/// <summary>Ekran okuyucu (UI Automation) adları: seçim düğmeleri kendi adlarıyla okunur.</summary>
public class AccessibilityTests
{
    /// <summary>
    /// İçeriği olan seçim düğmesine (RadioButton, CheckBox, ToggleButton, ToggleSwitch) LabeledBy verilmez: WPF'te UIA adı
    /// etiketten gelir ve içeriğin adını ezer. Ayarlar > Animasyonlar'da Canlı / Sakin / Kapalı'nın üçü de "Animasyonlar"
    /// okunuyordu; hangisinin seçili olduğu duyulmuyordu. Grubun başlığı HelpText ile verilir.
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
            var doc = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var element in doc.Descendants())
            {
                if (element.Name.LocalName is not ("RadioButton" or "CheckBox" or "ToggleButton" or "ToggleSwitch")) continue;
                var hasContent = element.Attribute("Content") is not null
                                 || element.Nodes().Any(n => n is XText { Value.Length: > 0 } || n is XElement child && !child.Name.LocalName.Contains('.'));
                if (hasContent && element.Attribute("AutomationProperties.LabeledBy") is not null)
                    problems.Add($"{rel}:{((System.Xml.IXmlLineInfo)element).LineNumber}");
            }
        }
        Assert.Empty(problems);
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

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
