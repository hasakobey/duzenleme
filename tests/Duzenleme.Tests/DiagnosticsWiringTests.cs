using System.Text.RegularExpressions;
using Duzenleme.Core;
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

    [Fact]
    public void Pressed_keys_are_never_logged()
    {
        // Not yazarken basılan tuşlar hiçbir günlüğe (DebugLog dahil) girmez.
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            if (rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) || rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var (line, number) in File.ReadLines(file).Select((l, i) => (l, i + 1)))
                if (Regex.IsMatch(line, @"(DebugLog\.Write|AppLog\.\w+)\(.*\{e\.(Key|SystemKey|Text)\b")) offenders.Add($"{rel}:{number}");
        }
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
