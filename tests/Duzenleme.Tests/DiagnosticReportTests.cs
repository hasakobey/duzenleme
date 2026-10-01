using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>Yerel tanılama sayfası: betik yok, dış kaynak yok, her metin kaçışlanır, CSP özeti CSS'le eşleşir.</summary>
public class DiagnosticReportTests
{
    private const string Evil = "<script>alert(1)</script>\"><img onerror=alert(2)>&Tom's";

    private static readonly DateTime Now = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private static readonly HashSet<string> AllowedTags =
    [
        "html", "head", "meta", "title", "style", "body", "header", "nav", "main", "section", "h1", "h2", "h3", "p", "a",
        "table", "thead", "tbody", "tr", "th", "td", "details", "summary", "pre", "div",
    ];

    private static ScrubbedError EvilError() =>
        new(Evil, "0x80004005", Evil, Evil, [Evil + " in " + Evil, "Duzenleme.X.Y() in /_/src/Duzenleme/X.cs:line 3"],
            [new ScrubbedError(Evil, "0x1", null, Evil, [Evil], [])]);

    private static ReportInput EvilInput()
    {
        var lines = new List<LogEntry>
        {
            new(Now.AddHours(-1), AppLogLevel.Error, Evil, Evil, EvilError(), "aaaaaaaa", 3, Evil, Evil),
            new(Now.AddHours(-2), AppLogLevel.Error, Evil, Evil, EvilError(), "aaaaaaaa", 2, Evil, Evil),
            new(Now.AddHours(-3), AppLogLevel.Warn, "box", "move-failed see https://example.com/x src=x", null, "bbbbbbbb", 1, "s", "v"),
            new(Now.AddDays(-9), AppLogLevel.Error, "old", "too-old", null, "cccccccc", 1, "s", "v"),
            new(Now.AddHours(-1), AppLogLevel.Info, "session", "start", null, "dddddddd", 1, "s", "v"),
        };
        var crash = new CrashReport(@"C:\x\crash-1.json", Now.AddHours(-5), Evil, Evil, Evil, EvilError(), lines.Take(2).ToList(), false);
        var snapshot = new DiagnosticSnapshot
        {
            Flags = new Dictionary<string, bool> { [Evil] = true, ["Paused"] = false },
            Choices = new Dictionary<string, string> { [Evil] = Evil },
            Counts = new Dictionary<string, long> { [Evil] = 3 },
            Widgets = [new WidgetRow(Evil, Evil, 2, 1, Evil, 1, 1.5, 0, 0)],
        };
        return new ReportInput
        {
            Snapshot = snapshot,
            Environment = new DiagnosticEnvironment { Channel = Evil, Scales = Evil, DataFolderSource = Evil },
            Perf = new PerfFacts { Counters = new Dictionary<string, long> { [Evil] = 1 } },
            Lines = lines,
            Crashes = [crash],
            PendingCrashes = 1,
            LogFileCount = 2,
            LogBytes = 12345,
            CorruptFiles = 1,
            NowUtc = Now,
        };
    }

    [Fact]
    public void Escape_covers_html_and_attribute_characters()
    {
        Assert.Equal("&lt;script&gt;&amp;&quot;&#39;&#61;&#96;", DiagnosticReport.Escape("<script>&\"'=`"));
        Assert.Equal("", DiagnosticReport.Escape(null));
        Assert.Equal("a b", DiagnosticReport.Escape("a\u0001b"));
    }

    [Fact]
    public void Hostile_text_is_escaped_everywhere()
    {
        var html = DiagnosticReport.Render(EvilInput());
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Tom's", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("&quot;&gt;&lt;img onerror&#61;alert(2)&gt;&amp;Tom&#39;s", html);
    }

    [Fact]
    public void Only_whitelisted_tags_and_no_external_resources()
    {
        var html = DiagnosticReport.Render(EvilInput());
        var tags = Regex.Matches(html, @"<\s*/?\s*([A-Za-z][A-Za-z0-9-]*)").Select(m => m.Groups[1].Value.ToLowerInvariant()).ToHashSet();
        Assert.Empty(tags.Except(AllowedTags));
        Assert.DoesNotContain("http", html.Replace("http-equiv", ""), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("url(", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", html, StringComparison.OrdinalIgnoreCase);
        foreach (Match href in Regex.Matches(html, @"href\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase))
            Assert.StartsWith("#", href.Groups[1].Value.Trim('"', '\''));
        Assert.Equal(Regex.Matches(html, "href", RegexOptions.IgnoreCase).Count, Regex.Matches(html, @"href=""#[a-z]+""").Count);
        Assert.Empty(Regex.Matches(html, @"\s(on[a-z]+)\s*=", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Csp_hash_matches_the_only_style_element()
    {
        var html = DiagnosticReport.Render(EvilInput());
        var styles = Regex.Matches(html, "<style>(.*?)</style>", RegexOptions.Singleline);
        var css = Assert.Single(styles).Groups[1].Value;
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(css)));
        var csp = Regex.Match(html, @"<meta http-equiv=""Content-Security-Policy"" content=""([^""]*)"">").Groups[1].Value;
        Assert.Contains("default-src 'none'", csp);
        Assert.Contains($"style-src 'sha256-{hash}'", csp);
        Assert.Contains("base-uri 'none'", csp);
        Assert.Contains("form-action 'none'", csp);
        Assert.DoesNotContain("script-src", csp);
        Assert.DoesNotContain("unsafe", csp);
        Assert.True(html.IndexOf("Content-Security-Policy", StringComparison.Ordinal) < html.IndexOf("<style>", StringComparison.Ordinal));
    }

    [Fact]
    public void Css_follows_system_theme_and_never_reveals_on_hover()
    {
        Assert.Contains("prefers-color-scheme:dark", DiagnosticReport.Css);
        Assert.Contains("forced-colors:active", DiagnosticReport.Css);
        Assert.DoesNotContain(":hover", DiagnosticReport.Css);
        Assert.DoesNotContain("url(", DiagnosticReport.Css);
        Assert.DoesNotContain("@import", DiagnosticReport.Css);
        Assert.DoesNotContain("<", DiagnosticReport.Css);
        // Görünürlük değiştiren hiçbir fare/odak kuralı yok.
        Assert.DoesNotMatch(@":(hover|focus)[^{]*\{[^}]*(display|visibility|opacity)", DiagnosticReport.Css);
    }

    [Fact]
    public void Urls_in_log_text_are_rescrubbed()
    {
        var html = DiagnosticReport.Render(EvilInput());
        Assert.Contains("move-failed see &lt;url&gt; src&#61;x", html);
        Assert.DoesNotContain("example.com", html);
    }

    [Fact]
    public void Summary_counts_last_seven_days_and_groups_by_signature()
    {
        var input = EvilInput();
        var groups = DiagnosticReport.Group(input.Lines.Where(l => l.Utc >= Now.AddDays(-7)));
        Assert.Equal(2, groups.Count);
        var evil = groups.Single(g => g.Signature == "aaaaaaaa");
        Assert.Equal(5, evil.Count);
        Assert.Equal(Now.AddHours(-2), evil.First);
        Assert.Equal(Now.AddHours(-1), evil.Last);
        Assert.DoesNotContain(groups, g => g.Level == AppLogLevel.Info);

        var html = DiagnosticReport.Render(input);
        Assert.DoesNotContain("too-old", html);
        Assert.Matches(@"<th>Son 7 günde hata</th><td>5</td>", html);
        Assert.Matches(@"<th>Son 7 günde uyarı</th><td>1</td>", html);
        Assert.Matches(@"<th>Son 7 günde çökme</th><td>1</td>", html);
    }

    [Fact]
    public void Headings_follow_ui_language_and_sections_have_anchors()
    {
        var tr = DiagnosticReport.Render(EvilInput());
        Assert.Contains("<html lang=\"tr\">", tr);
        Assert.Contains(">Son hatalar<", tr);
        foreach (var id in new[] { "ozet", "hatalar", "cokmeler", "ortam", "widgetlar", "basarim", "dosyalar", "ayarlar" })
        {
            Assert.Contains($"<section id=\"{id}\">", tr);
            Assert.Contains($"<a href=\"#{id}\">", tr);
        }
        using var _ = L.Use(Lang.En);
        var en = DiagnosticReport.Render(EvilInput());
        Assert.Contains("<html lang=\"en\">", en);
        Assert.Contains(">Recent errors<", en);
        Assert.Contains(">Performance<", en);
    }

    [Fact]
    public void Empty_input_renders()
    {
        var html = DiagnosticReport.Render(new ReportInput
        {
            Snapshot = DiagnosticSnapshot.From(new AppSettings()),
            NowUtc = Now,
            LogOff = true,
        });
        Assert.Contains(L.T("Son 7 günde hata ya da uyarı yok."), html);
        Assert.Contains(L.T("Kaydedilmiş çökme raporu yok."), html);
        Assert.Contains(L.T("Hata günlüğü kapalı: yeni kayıt yazılmıyor."), html);
        Assert.EndsWith("</html>\n", html);
    }
}
