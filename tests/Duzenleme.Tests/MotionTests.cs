using System.Text.RegularExpressions;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Hareket kuralları (2.1 P7): yalnızca kısa saydamlık geçişleri, yalnızca Windows'un ve uygulamanın ayarı izin verince ve
/// yalnızca Views/Motion.cs üzerinden. Kaynak taraması başka yerde animasyon (BeginAnimation, Storyboard, kayan açılır
/// pencere) açılmasını yakalar.
/// </summary>
public class MotionTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]    // uygulamada kapatıldı
    [InlineData(false, false, false)]  // Windows'ta "Animasyon efektleri" kapalı: ayar açık da olsa oynamaz
    [InlineData(false, true, false)]
    public void Enabled_needs_windows_and_app(bool windows, bool appOff, bool expected) =>
        Assert.Equal(expected, MotionPolicy.Enabled(windows, appOff));

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Menus_fade_only_when_windows_menu_animation_is_on(bool enabled, bool menuAnimation, bool expected) =>
        Assert.Equal(expected, MotionPolicy.MenuFade(enabled, menuAnimation));

    [Fact]
    public void Durations_are_short()
    {
        Assert.InRange(MotionPolicy.FadeMs, 100, MotionPolicy.MaxMs);
        Assert.InRange(MotionPolicy.GlowFadeMs, 100, MotionPolicy.MaxMs);
        Assert.Equal(200, MotionPolicy.MaxMs);
        Assert.Equal(MotionPolicy.MaxMs, MotionPolicy.Duration(1000));
        Assert.Equal(0, MotionPolicy.Duration(-5));
        Assert.Equal(120, MotionPolicy.Duration(120));
        Assert.InRange(MotionPolicy.WidgetFrameRate, 15, 30);
    }

    [Fact]
    public void Only_a_few_widget_windows_fade_at_once()
    {
        Assert.False(MotionPolicy.FadeWindows(true, 0));
        Assert.True(MotionPolicy.FadeWindows(true, 1));
        Assert.True(MotionPolicy.FadeWindows(true, MotionPolicy.MaxFadedWindows));
        Assert.False(MotionPolicy.FadeWindows(true, MotionPolicy.MaxFadedWindows + 1));
        Assert.False(MotionPolicy.FadeWindows(false, 1));
    }

    [Fact]
    public void Animation_setting_is_off_by_default_and_not_written_when_default()
    {
        var settings = new AppSettings();
        Assert.False(settings.AnimationsOff);
        Assert.DoesNotContain("AnimationsOff", System.Text.Encoding.UTF8.GetString(JsonFile.Serialize(settings)));
        settings.AnimationsOff = true;
        Assert.Contains("\"AnimationsOff\": true", System.Text.Encoding.UTF8.GetString(JsonFile.Serialize(settings)));
    }

    // ---------------------------------------------------------------- kaynak taraması

    private static readonly Regex CodeAnimation =
        new(@"\bBeginAnimation\b|\bBeginStoryboard\b|\bStoryboard\b|\b\w+Animation(UsingKeyFrames|UsingPath)?\s*[({]|new\s+\w+Animation\b");

    [Fact]
    public void Animations_only_through_Motion()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.cs"))
        {
            var relative = Path.GetRelativePath(SourceRoot, file);
            if (relative == Path.Combine("Views", "Motion.cs")) continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var code = StripComment(lines[i]);
                if (CodeAnimation.IsMatch(code)) offenders.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
            }
        }
        Assert.True(offenders.Count == 0, "Animasyon yalnızca Views/Motion.cs'ten açılır:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Xaml_has_no_storyboards_or_sliding_popups()
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.xaml"))
        {
            var relative = Path.GetRelativePath(SourceRoot, file);
            var text = Regex.Replace(File.ReadAllText(file), "<!--.*?-->", "", RegexOptions.Singleline);
            if (Regex.IsMatch(text, @"<(\w+:)?(Storyboard|BeginStoryboard|(?!Popup)\w+Animation(UsingKeyFrames)?)\b"))
                offenders.Add($"{relative}: Storyboard/Animation");
            // Açılır pencerelerde kayma (Slide/Scroll) yok; solmayı yalnızca Motion kodla açar.
            foreach (Match m in Regex.Matches(text, @"PopupAnimation(=""|"">|\"" Value=\"")(?<v>\w+)"))
                if (m.Groups["v"].Value != "None") offenders.Add($"{relative}: PopupAnimation {m.Groups["v"].Value}");
            foreach (Match m in Regex.Matches(text, @">(?<v>\w+)</PopupAnimation>"))
                if (m.Groups["v"].Value != "None") offenders.Add($"{relative}: PopupAnimation {m.Groups["v"].Value}");
            // Sayfa geçişi Motion.ApplyTo ile koddan: XAML'de yalnızca "None" başlangıç değeri.
            foreach (Match m in Regex.Matches(text, @"\bTransition=""(?<v>\w+)"""))
                if (m.Groups["v"].Value != "None") offenders.Add($"{relative}: Transition {m.Groups["v"].Value}");
        }
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    [Fact]
    public void Size_to_content_dialogs_fit_their_chrome()
    {
        // WPF-UI FluentWindow çerçevesini ilk ölçümden sonra kaldırır: içeriğe göre boyutlanan her pencere yeniden ölçülmeli
        // (yoksa altında ~70 piksel boşluk kalır; bkz. WindowFit.FitChromeToContent).
        var offenders = new List<string>();
        foreach (var file in SourceFiles("*.cs"))
        {
            var text = File.ReadAllText(file);
            var fluent = text.Contains("new FluentWindow", StringComparison.Ordinal) || text.Contains(": FluentWindow", StringComparison.Ordinal);
            if (fluent && text.Contains("SizeToContent.WidthAndHeight", StringComparison.Ordinal) &&
                !text.Contains("FitChromeToContent(", StringComparison.Ordinal))
                offenders.Add(Path.GetRelativePath(SourceRoot, file));
        }
        Assert.True(offenders.Count == 0, "FitChromeToContent çağrılmıyor:\n" + string.Join("\n", offenders));
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 && !line[..index].Contains('"') ? line[..index] : line;
    }

    private static string SourceRoot => Path.Combine(RepoRoot(), "src", "Duzenleme");

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(SourceRoot, pattern, SearchOption.AllDirectories)
            .Where(f =>
            {
                var rel = Path.GetRelativePath(SourceRoot, f);
                return !rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) && !rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase);
            });

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
    }
}
