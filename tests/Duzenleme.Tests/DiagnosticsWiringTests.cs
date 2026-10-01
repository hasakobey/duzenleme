using System.Text.RegularExpressions;
using Duzenleme.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Wpf.Ui.Controls;

namespace Duzenleme.Tests;

/// <summary>
/// Yerel tanılamanın uygulamaya bağlanması: arayüz nöbetçisinin ve hata şeridinin kararları, okunamayan dosya bildirimi,
/// "Ayrıntıları kopyala" metni, sorun bağlantısı, yayın ayarları (gömülü PDB, PathMap) ve kaynak taramaları.
/// </summary>
public class DiagnosticsWiringTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-diagwire-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch (IOException) { }
    }

    private static Exception Thrown(Exception ex)
    {
        try { throw ex; }
        catch (Exception caught) { return caught; }
    }

    // ------------------------------------------------------------------ arayüz nöbetçisi

    [Fact]
    public void Watchdog_posts_one_probe_at_a_time_and_counts_late_probes()
    {
        var t = new StallTracker();
        Assert.Equal(StallTick.Post, t.Tick(0, systemActive: true));
        // Önceki yoklama çalışmadan yenisi gönderilmez.
        Assert.Equal(StallTick.None, t.Tick(2000, systemActive: true));
        var probe = t.Ran(2500);
        Assert.Equal(2500, probe.DelayMs);
        Assert.True(probe.Stalled);
        Assert.False(probe.Recovered);
        Assert.Equal(StallTick.Post, t.Tick(4000, systemActive: true));
        Assert.False(t.Ran(4100).Stalled);
    }

    [Fact]
    public void Watchdog_reports_a_hang_once_and_clears_it_when_the_ui_recovers()
    {
        var t = new StallTracker();
        Assert.Equal(StallTick.Post, t.Tick(0, true));
        Assert.Equal(StallTick.None, t.Tick(2000, true));
        Assert.Equal(StallTick.None, t.Tick(4000, true));
        Assert.Equal(StallTick.HangStarted, t.Tick(6000, true));
        Assert.True(t.Hung);
        Assert.Equal(StallTick.None, t.Tick(8000, true));
        var probe = t.Ran(9000);
        Assert.True(probe.Recovered);
        Assert.False(t.Hung);
        Assert.Equal(StallTick.Post, t.Tick(10000, true));
    }

    [Fact]
    public void Watchdog_ignores_sleep_lock_and_process_pauses()
    {
        var t = new StallTracker();
        Assert.Equal(StallTick.Post, t.Tick(0, true));
        // Kilit/uyku: bekleme sayılmaz, yeni yoklama da gönderilmez.
        Assert.Equal(StallTick.None, t.Tick(2000, false));
        Assert.Equal(StallTick.None, t.Tick(60_000, false));
        Assert.Equal(StallTick.None, t.Tick(62_000, true));
        Assert.Equal(StallTick.None, t.Tick(64_000, true));
        Assert.False(t.Hung);

        // Zamanlayıcının kendisi geç kaldı (bütün süreç durmuştu: uyku, hata ayıklayıcı): süre baştan.
        var u = new StallTracker();
        Assert.Equal(StallTick.Post, u.Tick(0, true));
        Assert.Equal(StallTick.None, u.Tick(30_000, true));
        Assert.Equal(StallTick.None, u.Tick(32_000, true));
        Assert.Equal(StallTick.HangStarted, u.Tick(36_000, true));

        // Sistem etkin değilken yoklama hiç başlamaz.
        var w = new StallTracker();
        Assert.Equal(StallTick.None, w.Tick(0, false));
        Assert.False(w.ProbePending);
    }

    // ------------------------------------------------------------------ arayüz hatası şeridi

    [Fact]
    public void Ui_error_notice_once_per_signature_and_at_most_three()
    {
        var limiter = new ErrorNoticeLimiter();
        Assert.True(limiter.Note("a", 0).Notify);
        Assert.False(limiter.Note("a", 10).Notify);
        Assert.True(limiter.Note("b", 20).Notify);
        Assert.True(limiter.Note("c", 30).Notify);
        Assert.False(limiter.Note("d", 40).Notify);
    }

    [Fact]
    public void Exception_storm_is_reported_once()
    {
        var limiter = new ErrorNoticeLimiter();
        var storms = Enumerable.Range(0, 50).Count(i => limiter.Note("x", i * 100).Storm);
        Assert.Equal(1, storms);
        // Yavaş gelen hatalar fırtına değildir.
        var slow = new ErrorNoticeLimiter();
        Assert.Equal(0, Enumerable.Range(0, 50).Count(i => slow.Note("x", i * 10_000L).Storm));
    }

    [Fact]
    public void Copied_details_are_scrubbed()
    {
        var scrubber = new LogScrubber(new ScrubContext { UserNames = ["Zeynep"], Desktop = [@"C:\Users\Zeynep\Desktop"] });
        var error = scrubber.Error(Thrown(new InvalidOperationException(@"Gizli 'Maaş bordrosu.xlsx' C:\Users\Zeynep\Desktop\PDF\fatura.pdf",
            Thrown(new FileNotFoundException("yok", @"C:\Users\Zeynep\Desktop\a.txt")))));
        var text = ErrorDetails.Text(error, "2.2.0", "abc123");
        Assert.StartsWith(AppInfo.Name + " 2.2.0 · abc123", text);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("FileNotFoundException", text);
        Assert.Contains("Thrown", text);
        foreach (var secret in new[] { "Zeynep", "Maaş", "fatura", "a.txt", @"C:\" })
            Assert.DoesNotContain(secret, text);
    }

    [Fact]
    public void Sharing_violations_are_recognised()
    {
        Assert.True(FileLocks.IsSharingViolation(new IOException("x", unchecked((int)0x80070020))));
        Assert.True(FileLocks.IsSharingViolation(new IOException("x", unchecked((int)0x80070021))));
        Assert.False(FileLocks.IsSharingViolation(new IOException("x", unchecked((int)0x80070005))));
        Assert.False(FileLocks.IsSharingViolation(new UnauthorizedAccessException()));
    }

    // ------------------------------------------------------------------ okunamayan dosya

    [Theory]
    [InlineData(@"C:\x\settings.json", JsonFile.KindSettings)]
    [InlineData(@"C:\x\Journal.JSON", JsonFile.KindJournal)]
    [InlineData(@"C:\x\box-moves.json", JsonFile.KindBoxMoves)]
    [InlineData(@"C:\x\other.json", JsonFile.KindOther)]
    public void Corrupt_file_kind(string path, string kind) => Assert.Equal(kind, JsonFile.KindOf(path));

    [Fact]
    public void Corrupted_reports_backup_copy_and_whether_bak_was_loaded()
    {
        var calls = new List<(string Kind, string? Backup, bool UsedBak)>();
        var dir = Path.Combine(_root, "data");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        var previous = JsonFile.Corrupted;
        // Yalnızca bu testin dosyası (testler paralel çalışır).
        JsonFile.Corrupted = (kind, backup, usedBak) =>
        {
            if (backup is not null && backup.StartsWith(path, StringComparison.OrdinalIgnoreCase)) lock (calls) calls.Add((kind, backup, usedBak));
        };
        try
        {
            File.WriteAllText(path, "{ bozuk");
            File.WriteAllText(path + ".bak", """{ "Paused": true }""");
            var loaded = JsonFile.Load(path, () => new AppSettings());
            Assert.True(loaded.Paused);

            File.Delete(path + ".bak");
            File.WriteAllText(path, "{ yine bozuk");
            var fresh = JsonFile.Load(path, () => new AppSettings { Language = "fallback" });
            Assert.Equal("fallback", fresh.Language);
        }
        finally { JsonFile.Corrupted = previous; }

        Assert.Equal(2, calls.Count);
        Assert.Equal((JsonFile.KindSettings, true), (calls[0].Kind, calls[0].UsedBak));
        Assert.Equal((JsonFile.KindSettings, false), (calls[1].Kind, calls[1].UsedBak));
        Assert.All(calls, c => Assert.True(File.Exists(c.Backup)));
        Assert.Contains(".bozuk-", calls[0].Backup);
    }

    // ------------------------------------------------------------------ hata günlüğü: yalnızca sayı

    [Fact]
    public void Code_entries_carry_only_the_number()
    {
        var logs = Path.Combine(_root, "logs");
        var channel = new AppLogChannel(logs, LogScrubber.Plain, persist: true, background: false, session: "abc123", version: "2.2.0");
        channel.Write(AppLogLevel.Warn, "ai", "generate-api-error", 529);
        Assert.True(channel.FlushSync(TimeSpan.FromSeconds(5)));
        var entry = Assert.Single(LogFiles.Read(logs, DateTime.MinValue));
        Assert.Null(entry.Error);
        Assert.Equal("529", entry.Facts!["code"]);
        Assert.Equal("generate-api-error", entry.Message);
    }

    // ------------------------------------------------------------------ sorun bağlantısı

    [Fact]
    public void Report_problem_link_carries_only_template_title_and_version()
    {
        var url = SupportLinks.ReportProblem("2.2.0");
        Assert.StartsWith(AppInfo.IssuesUrl + "/new?", url);
        Assert.DoesNotContain(" ", url);
        var query = new Uri(url).Query.TrimStart('?').Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        Assert.Equal(["template", "title", SupportLinks.ProblemVersionField], query.Keys.Order());
        Assert.Equal(SupportLinks.ProblemTemplate, query["template"]);
        Assert.Equal("2.2.0", query[SupportLinks.ProblemVersionField]);
    }

    [Fact]
    public void Issue_template_matches_the_link_and_warns_that_issues_are_public()
    {
        var yml = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "ISSUE_TEMPLATE", SupportLinks.ProblemTemplate));
        Assert.Matches(new Regex(@"^\s+id:\s*" + SupportLinks.ProblemVersionField + @"\s*$", RegexOptions.Multiline), yml);
        Assert.Contains("herkese açıktır", yml);
        Assert.Contains("Issues are public", yml);
        // Paket kullanıcı tarafından eklenir; şablon hiçbir şeyi zorunlu kılmaz.
        Assert.DoesNotMatch(new Regex(@"id:\s*package[\s\S]*?required:\s*true"), yml);
    }

    // ------------------------------------------------------------------ yayın: satır numaralı yığınlar

    [Fact]
    public void Publish_scripts_embed_the_pdb_and_check_it()
    {
        foreach (var script in new[] { "publish.ps1", "package-msix.ps1" })
        {
            var text = File.ReadAllText(Path.Combine(RepoRoot(), "tools", script));
            Assert.DoesNotContain("DebugType=none", text);
            Assert.Contains("-p:DebugType=embedded", text);
            Assert.Contains("'MPDB'", text);
            // Yayından hemen sonra denetlenir (işlevin tanımı + çağrısı).
            Assert.True(Regex.Matches(text, "Assert-EmbeddedPdb").Count >= 2, script);
        }
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "Duzenleme.csproj"));
        Assert.Matches(new Regex(@"<PathMap Condition=""'\$\(Configuration\)' == 'Release'"">.*=/_/src/Duzenleme/</PathMap>"), csproj);
    }

    [Fact]
    public void Open_package_script_accepts_only_package_names()
    {
        var script = Path.Combine(RepoRoot(), "tools", "diag", "open-package.ps1");
        var bytes = File.ReadAllBytes(script);
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "open-package.ps1 BOM'lu UTF-8 olmalı");
        var text = File.ReadAllText(script);
        var patterns = Regex.Matches(text, @"'(\^[^']+\$)'").Select(m => new Regex(m.Groups[1].Value)).ToList();
        Assert.NotEmpty(patterns);
        bool Allowed(string name) => patterns.Any(p => p.IsMatch(name));
        foreach (var name in new[] { DiagnosticPackage.ReportName, DiagnosticPackage.SummaryName, DiagnosticPackage.ReadmeName,
                     "logs/" + LogFiles.NameFor("20261001", 0), "logs/" + LogFiles.NameFor("20261001", 3), "crashes/crash-20261001-101500-000.json" })
            Assert.True(Allowed(name), name);
        foreach (var name in new[] { "settings.json", "logs/../settings.json", "crashes/pending-20261001-101500-000.json", "journal.json",
                     "yedekler/settings-2026-10-01.json", "a/rapor.html" })
            Assert.False(Allowed(name), name);
        Assert.Contains("50MB", text);
        Assert.DoesNotContain("Invoke-Expression", text);
        // Çıkarırken gerçekten yazılan bayt sayılır (bildirilen boyuta güvenilmez).
        Assert.Contains("$entryWritten -gt $entry.Length", text);
        Assert.Contains("$written -gt $maxBytes", text);
        // Sayfa yalnızca -Open ile ve denetimden geçince açılır; tek Start-Process o dalda.
        Assert.Contains("[switch]$Open", text);
        Assert.DoesNotContain("NoOpen", text);
        Assert.Single(Regex.Matches(text, "Start-Process"));
        Assert.Matches(new Regex(@"elseif \(\$Open\) \{\s*Start-Process"), text);
    }

    /// <summary>open-package.ps1'in rapor.html denetimi (PowerShell -match = .NET düzenli ifadesi, harf duyarsız).</summary>
    private static (List<(string Name, Regex Pattern)> Forbidden, string Csp) ReportCheck()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "diag", "open-package.ps1"));
        var block = Regex.Match(text, @"\$forbidden = \[ordered\]@\{(?<body>[\s\S]*?)\n\s*\}").Groups["body"].Value;
        var forbidden = Regex.Matches(block, @"'(?<name>[^']+)'\s*=\s*'(?<p>(?:[^']|'')*)'")
            .Select(m => (m.Groups["name"].Value, new Regex(m.Groups["p"].Value.Replace("''", "'"), RegexOptions.IgnoreCase)))
            .ToList();
        var csp = Regex.Match(text, @"\$csp = '(?<v>(?:[^']|'')*)'").Groups["v"].Value.Replace("''", "'");
        return (forbidden, csp);
    }

    private static List<string> ReportProblems(string html)
    {
        var (forbidden, csp) = ReportCheck();
        var problems = forbidden.Where(f => f.Pattern.IsMatch(html)).Select(f => f.Name).ToList();
        var head = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        var at = html.IndexOf(csp, StringComparison.Ordinal);
        if (head < 0 || at < head + 6 || Regex.IsMatch(html[(head + 6)..at], "<(?!meta charset)", RegexOptions.IgnoreCase)) problems.Add("csp");
        // Etiket ve öznitelik beyaz listesi ve <style> içeriği (betikteki düzenli ifadelerle).
        var script = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "diag", "open-package.ps1"));
        var styleForbidden = new Regex(Regex.Match(script, @"\$styleForbidden = '([^']*)'").Groups[1].Value, RegexOptions.IgnoreCase);
        foreach (Match style in Regex.Matches(html, "(?is)<style[^>]*>(.*?)</style>"))
            if (styleForbidden.IsMatch(style.Groups[1].Value)) problems.Add("style");
        var tags = new Regex("^(" + Regex.Match(script, @"\$tags = '([^']*)'").Groups[1].Value + ")$", RegexOptions.IgnoreCase);
        var attributes = new Regex("^(" + Regex.Match(script, @"\$attributes = '([^']*)'").Groups[1].Value + ")$", RegexOptions.IgnoreCase);
        if (Regex.Matches(html, "<!").Count != 1 || !html.StartsWith("<!DOCTYPE html>", StringComparison.Ordinal)) problems.Add("doctype");
        foreach (Match tag in Regex.Matches(html, @"<\s*(/?)\s*([A-Za-z][^\s>/]*)([^>]*)>"))
        {
            if (!tags.IsMatch(tag.Groups[2].Value)) { problems.Add("tag " + tag.Groups[2].Value); continue; }
            foreach (Match attribute in Regex.Matches(tag.Groups[3].Value, @"([^\s=/>""']+)\s*(?:=\s*(?:""[^""]*""|'[^']*'|[^\s>]+))?"))
                if (!attributes.IsMatch(attribute.Groups[1].Value)) problems.Add("attribute " + attribute.Groups[1].Value);
        }
        return problems;
    }

    [Fact]
    public void Open_package_report_check_accepts_our_report_and_rejects_crafted_ones()
    {
        var (forbidden, csp) = ReportCheck();
        Assert.Equal(7, forbidden.Count);
        Assert.StartsWith("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'sha256-", csp);

        // Uygulamanın ürettiği sayfa, günlükte düşmanca metin olsa da (hepsi kaçışlanır) geçer.
        const string hostile = "<script>alert(1)</script> <img src=x onerror=alert(2)> javascript:alert(3) data:text/html,x url(x) href=x";
        var error = new ScrubbedError(hostile, "0x1", null, hostile, [hostile], []);
        var html = DiagnosticReport.Render(new ReportInput
        {
            Snapshot = DiagnosticSnapshot.From(new AppSettings()),
            Lines = [new LogEntry(DateTime.UtcNow, AppLogLevel.Error, hostile, hostile, error, "aaaaaaaa", 1, "s", "v")],
            NowUtc = DateTime.UtcNow,
        });
        Assert.Empty(ReportProblems(html));

        // Elle değiştirilmiş paketler reddedilir.
        Assert.Contains("betik", ReportProblems(html.Replace("<body>", "<body><script>alert(1)</script>")));
        Assert.NotEmpty(ReportProblems(html.Replace("<body>", "<body onload=alert(1)>")));
        Assert.NotEmpty(ReportProblems(html.Replace("<header>", "<header><a href=\"https://example.com\">x</a>")));
        Assert.NotEmpty(ReportProblems(html.Replace("<header>", "<header><a href='javascript:alert(1)'>x</a>")));
        Assert.NotEmpty(ReportProblems(html.Replace("<header>", "<header><iframe></iframe>")));
        Assert.NotEmpty(ReportProblems(html.Replace("<header>", "<header><a href=\"#x\" ping=\"https://example.com\">x</a>")));
        Assert.NotEmpty(ReportProblems(html.Replace("<header>", "<header><marquee>x</marquee>")));
        Assert.NotEmpty(ReportProblems(html.Replace("<header>", "<header><div style=\"x\">x</div>")));
        Assert.NotEmpty(ReportProblems(html.Replace("</style>", "@import 'x';</style>")));
        Assert.Contains("csp", ReportProblems(html.Replace("default-src 'none'", "default-src *")));
        Assert.Contains("csp", ReportProblems(html.Replace("<meta charset=\"utf-8\">", "<meta charset=\"utf-8\"><meta name=\"x\" content=\"y\">")));
    }

    // ------------------------------------------------------------------ kaynak taramaları

    [Fact]
    public void Settings_symbols_are_drawable()
    {
        // Ayarlar > Yardım > Sorun bildir ve Gelişmiş > Hata günlüğü simgeleri (U+FFFF üstü "X" çizilir).
        foreach (var symbol in new[] { SymbolRegular.Bug24, SymbolRegular.FolderZip24, SymbolRegular.DocumentSearch24, SymbolRegular.DocumentBulletList24 })
            Assert.True((int)symbol is > 0 and <= 0xFFFF, symbol.ToString());
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "Views", "SettingsPage.xaml"));
        foreach (var name in new[] { "Bug24", "FolderZip24", "DocumentSearch24", "DocumentBulletList24" }) Assert.Contains(name, xaml);
    }

    [Fact]
    public void Ui_errors_no_longer_open_a_message_box()
    {
        var app = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "App.xaml.cs"));
        var start = app.IndexOf("private void OnUnhandledException(", StringComparison.Ordinal);
        Assert.True(start > 0);
        var end = app.IndexOf("\n    }", start, StringComparison.Ordinal);
        var body = app[start..end];
        Assert.DoesNotContain("MessageBox", body);
        Assert.Contains("e.Handled = true", body);
        Assert.Contains("AppLog.Error(", body);
        // Deneme çökmesi yalnızca test masaüstüyle.
        Assert.Matches(new Regex(@"IsTestDesktop\s*\|\|\s*AppEnvironment\.Get\(""TEST_CRASH""\)|!AppHost\.IsTestDesktop\s*\|\|\s*AppEnvironment\.Get\(""TEST_CRASH""\)"), app);
    }

    /// <summary>
    /// Günlüğe giden çağrılarda (DebugLog.Write, PerfLog.Write, AppLog.*, AppLog.Channel.*) basılan tuş, yazılan metin ya da pano:
    /// bağımsız değişkenin herhangi bir yerinde (ara değer, birleştirme, birden çok satır) <c>.Key</c>, <c>.SystemKey</c>,
    /// <c>.Text</c>, <c>.SelectedText</c>, <c>.Password</c> ya da <c>Clipboard</c>. Sorunlu çağrılar (dosya:satır).
    /// </summary>
    internal static List<string> KeyLogCalls(string rel, string code)
    {
        var problems = new List<string>();
        var root = CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)).GetRoot();
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax access) continue;
            var target = access.Expression.ToString().Replace(" ", "");
            var last = target.Split('.').Last();
            var isLog = (last is "DebugLog" or "PerfLog" && access.Name.Identifier.Text == "Write")
                        || last == "AppLog" || target.EndsWith("AppLog.Channel", StringComparison.Ordinal);
            if (!isLog) continue;
            var leaks = call.ArgumentList.DescendantNodes().Any(n => n switch
            {
                MemberAccessExpressionSyntax m => m.Name.Identifier.Text is "Key" or "SystemKey" or "ImeProcessedKey" or "DeadCharProcessedKey"
                    or "Text" or "SelectedText" or "Password" or "PasswordChars",
                IdentifierNameSyntax id => id.Identifier.Text == "Clipboard",
                _ => false,
            });
            if (leaks) problems.Add($"{rel}:{call.GetLocation().GetLineSpan().StartLinePosition.Line + 1}");
        }
        return problems;
    }

    [Fact]
    public void Pressed_keys_typed_text_and_clipboard_are_never_logged()
    {
        // Not yazarken basılan tuşlar, yazılan metin ve pano hiçbir günlüğe (DebugLog ve PerfLog dahil) girmez.
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            if (rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) || rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase)) continue;
            offenders.AddRange(KeyLogCalls(rel, File.ReadAllText(file)));
        }
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    [Fact]
    public void Key_scanner_finds_any_receiver_concatenation_and_multiline_calls()
    {
        const string code = """
            class A
            {
                void M(KeyEventArgs args, TextBox box, string path)
                {
                    DebugLog.Write($"tuş {args.Key}");
                    DebugLog.Write("tuş " +
                        args.SystemKey.ToString());
                    AppLog.Info("a", "b" + box.Text);
                    PerfLog.Write("p " + Clipboard.GetText());
                    AppLog.Channel.Write(AppLogLevel.Warn, "a", box.SelectedText, 1);
                    DebugLog.Write("yol " + path);
                    Other.Write(box.Text);
                }
            }
            """;
        var problems = KeyLogCalls("A.cs", code);
        Assert.Equal(["A.cs:5", "A.cs:6", "A.cs:8", "A.cs:9", "A.cs:10"], problems);
    }

    // ------------------------------------------------------------------ yayın kapısı

    [Fact]
    public void Release_scripts_refuse_to_ship_until_the_error_log_is_disclosed()
    {
        // Hata günlüğü varsayılan açık: PRIVACY.md (TR+EN), kurulumun "verileri sil" sorusu ve Store metinleri anlatmadan yayın yok.
        var publish = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "publish.ps1"));
        Assert.Contains("function Assert-DiagnosticsDisclosed", publish);
        Assert.Contains("Assert-DiagnosticsDisclosed -WarnOnly:$Draft", publish);
        Assert.Contains("logs/nestdesk-", publish);
        Assert.Contains("english\\.DeleteDataPrompt=", publish);
        Assert.Contains("turkish\\.DeleteDataPrompt=", publish);
        // Kapı, dist/ silinmeden ve derleme başlamadan önce.
        Assert.True(publish.IndexOf("Assert-DiagnosticsDisclosed -WarnOnly", StringComparison.Ordinal) < publish.IndexOf("Remove-Item $dist", StringComparison.Ordinal));

        var msix = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "package-msix.ps1"));
        Assert.Contains("function Assert-DiagnosticsDisclosed", msix);
        Assert.Contains("Assert-DiagnosticsDisclosed -WarnOnly:(-not $Strict)", msix);
        Assert.Contains("listing.md", msix);
        Assert.True(msix.IndexOf("Assert-DiagnosticsDisclosed -WarnOnly", StringComparison.Ordinal) < msix.IndexOf("dotnet publish", StringComparison.Ordinal));
        // Gömülü PDB denetimi kendi açıklamasının altında, Invoke-Tool'unki onun üstünde.
        Assert.True(msix.IndexOf("function Assert-EmbeddedPdb", StringComparison.Ordinal) < msix.IndexOf("# Yerel aracı çalıştırır.", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ oturum nöbetçisi: kapanış ve donma

    [Fact]
    public void Hang_during_logoff_keeps_the_ending_flag_and_recovery_never_clears_it()
    {
        var state = new SessionState("s1", "2.2.0", DateTime.UtcNow, 1, Hang: false);
        var ending = state with { Ending = true };
        // Kapanıştan hemen sonra (FlushAll arayüzü tutarken) gelen donma: olağan kapanış sayılmaya devam eder.
        var during = SessionMarker.WithHang(ending, hung: true, sinceEnding: TimeSpan.FromSeconds(6));
        Assert.True(during.Hang);
        Assert.True(during.Ending);
        Assert.Equal(PreviousSession.Clean, SessionMarker.Assess(during, "2.2.0"));
        // Toparlanma "kapanıyor" işaretini silmez.
        var recovered = SessionMarker.WithHang(during, hung: false, sinceEnding: TimeSpan.FromSeconds(7));
        Assert.False(recovered.Hang);
        Assert.True(recovered.Ending);
        Assert.Same(recovered, SessionMarker.WithHang(recovered, hung: false, sinceEnding: null));
        // İptal edilmiş kapanıştan çok sonra gelen gerçek donma yine bildirilir.
        var later = SessionMarker.WithHang(ending, hung: true, sinceEnding: TimeSpan.FromMinutes(5));
        Assert.True(later.Hang);
        Assert.False(later.Ending);
        Assert.Equal(PreviousSession.Hang, SessionMarker.Assess(later, "2.2.0"));
        // Kapanış yokken donma.
        var plain = SessionMarker.WithHang(state, hung: true, sinceEnding: null);
        Assert.True(plain.Hang);
        Assert.False(plain.Ending);
        Assert.Same(plain, SessionMarker.WithHang(plain, hung: true, sinceEnding: null));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
