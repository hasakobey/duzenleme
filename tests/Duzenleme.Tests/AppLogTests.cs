using Duzenleme.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Duzenleme.Tests;

/// <summary>Hata günlüğü kanalı: dosya adları, döndürme, sınırlar, tekrar bastırma, kapalıyken yazmama, halka arabellek.</summary>
public class AppLogTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-applog-" + Guid.NewGuid().ToString("N"))).FullName;
    private string Dir => Path.Combine(_root, "logs");
    private DateTime _now = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch (IOException) { }
    }

    private AppLogChannel Channel(AppLogLimits? limits = null, bool persist = true, LogScrubber? scrubber = null, string? dir = "",
        Action<string>? mirror = null) =>
        new(dir == "" ? Dir : dir, scrubber ?? LogScrubber.Plain, persist, limits ?? new AppLogLimits(), () => _now,
            background: false, mirror: mirror, session: "abc123", version: "2.2.0");

    private static Exception Thrown(Exception ex)
    {
        try { throw ex; }
        catch (Exception caught) { return caught; }
    }

    private List<string> Files() => LogFiles.List(Dir).Select(Path.GetFileName).ToList()!;

    private List<LogEntry> ReadAll() => LogFiles.Read(Dir, DateTime.MinValue).ToList();

    [Fact]
    public void Writes_one_scrubbed_json_line_per_entry()
    {
        var log = Channel(scrubber: new LogScrubber(new ScrubContext { Desktop = [@"C:\Users\Zeynep\Desktop"], UserNames = ["Zeynep"] }));
        log.Write(AppLogLevel.Error, "widget", "restore-failed", Thrown(new InvalidOperationException(@"open 'Gizli' C:\Users\Zeynep\Desktop\Gizli.pdf")));
        Assert.True(log.FlushSync(TimeSpan.FromSeconds(5)));

        Assert.Equal(["nestdesk-20261001.jsonl"], Files());
        var text = File.ReadAllText(Path.Combine(Dir, "nestdesk-20261001.jsonl"));
        Assert.DoesNotContain("Gizli", text);
        Assert.DoesNotContain("Zeynep", text);
        Assert.EndsWith("\n", text);
        var entry = Assert.Single(ReadAll());
        Assert.Equal(AppLogLevel.Error, entry.Level);
        Assert.Equal("widget", entry.Area);
        Assert.Equal("restore-failed", entry.Message);
        Assert.Equal("abc123", entry.Session);
        Assert.Equal("2.2.0", entry.Version);
        Assert.Equal(1, entry.Count);
        Assert.Equal(_now, entry.Utc);
        Assert.Equal("System.InvalidOperationException", entry.Error!.Type);
        Assert.Equal("open '<text>' <path:desktop>", entry.Error.Message);
        Assert.NotEmpty(entry.Error.Frames);
        Assert.Equal(8, entry.Signature.Length);
    }

    [Fact]
    public void New_file_each_day()
    {
        var log = Channel();
        log.Write(AppLogLevel.Warn, "a", "one", null);
        log.FlushSync(TimeSpan.FromSeconds(5));
        _now = _now.AddDays(1);
        log.Write(AppLogLevel.Warn, "a", "two", null);
        log.FlushSync(TimeSpan.FromSeconds(5));
        Assert.Equal(["nestdesk-20261001.jsonl", "nestdesk-20261002.jsonl"], Files());
    }

    [Fact]
    public void New_file_when_size_limit_is_reached()
    {
        var log = Channel(new AppLogLimits { FileBytes = 600 });
        for (var i = 0; i < 12; i++)
        {
            log.Write(AppLogLevel.Info, "area", "message-" + i, null);
            log.FlushSync(TimeSpan.FromSeconds(5));
        }
        var files = Files();
        Assert.True(files.Count >= 3, string.Join(", ", files));
        Assert.Equal("nestdesk-20261001.jsonl", files[0]);
        Assert.Equal("nestdesk-20261001-1.jsonl", files[1]);
        Assert.All(LogFiles.List(Dir), f => Assert.True(new FileInfo(f).Length <= 600));
        Assert.Equal(12, ReadAll().Count);
    }

    [Fact]
    public void Total_size_is_capped_by_deleting_oldest()
    {
        var log = Channel(new AppLogLimits { FileBytes = 400, TotalBytes = 1500 });
        for (var i = 0; i < 40; i++)
        {
            log.Write(AppLogLevel.Info, "area", "message-" + i, null);
            log.FlushSync(TimeSpan.FromSeconds(5));
            _now = _now.AddMinutes(30);
        }
        Assert.True(LogFiles.TotalBytes(Dir) <= 1500, LogFiles.TotalBytes(Dir).ToString());
        // En yenisi kalır.
        Assert.Contains(ReadAll(), e => e.Message == "message-39");
        Assert.DoesNotContain(ReadAll(), e => e.Message == "message-0");
    }

    [Fact]
    public void Files_older_than_retention_are_deleted_and_foreign_files_kept()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "nestdesk-20260901.jsonl"), "{}\n");    // 30 gün önce
        File.WriteAllText(Path.Combine(Dir, "nestdesk-20260917-2.jsonl"), "{}\n");  // 14 gün önce: sınırın dışında
        File.WriteAllText(Path.Combine(Dir, "nestdesk-20260918.jsonl"), "{}\n");    // 13 gün önce: kalır
        File.WriteAllText(Path.Combine(Dir, "notlar.txt"), "x");
        File.WriteAllText(Path.Combine(Dir, "nestdesk-bozuk.jsonl"), "x");
        var log = Channel();
        log.Write(AppLogLevel.Error, "a", "b", null);
        log.FlushSync(TimeSpan.FromSeconds(5));
        Assert.Equal(["nestdesk-20260918.jsonl", "nestdesk-20261001.jsonl"], Files());
        Assert.True(File.Exists(Path.Combine(Dir, "notlar.txt")));
        Assert.True(File.Exists(Path.Combine(Dir, "nestdesk-bozuk.jsonl")));
    }

    [Fact]
    public void Prune_now_applies_retention_without_new_entries()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "nestdesk-20260901.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(Dir, "nestdesk-20260930.jsonl"), "{}\n");
        Channel().PruneNow();
        Assert.Equal(["nestdesk-20260930.jsonl"], Files());
        Channel(dir: null).PruneNow();
    }

    [Fact]
    public void Static_facade_never_throws_and_keeps_entries_in_memory_before_start()
    {
        // Testte Start çağrılmaz: kanal klasörsüzdür, diske (gerçek veri klasörüne) hiçbir şey yazılmaz.
        Assert.Null(AppLog.Directory);
        AppLog.Error("test", "static-facade", Thrown(new InvalidOperationException()));
        AppLog.Warn("test", "static-facade-warn");
        AppLog.Info("test", "static-facade-info");
        AppLog.Count("test.counter");
        Assert.Contains(AppLog.Recent(), e => e.Message == "static-facade");
        Assert.True(AppLog.Counters["test.counter"] >= 1);
        Assert.True(AppLog.FlushSync(TimeSpan.FromSeconds(1)));
        Assert.Null(AppLog.Directory);
    }

    [Fact]
    public void Repeats_within_window_are_counted_not_written()
    {
        var log = Channel();
        var ex = Thrown(new InvalidOperationException("x"));
        for (var i = 0; i < 5; i++)
        {
            log.Write(AppLogLevel.Error, "move", "failed", ex);
            _now = _now.AddSeconds(30);
        }
        Assert.Single(log.Recent());
        Assert.Equal(4, log.Counters["log.suppressed"]);
        log.FlushSync(TimeSpan.FromSeconds(5));
        var lines = ReadAll();
        Assert.Equal(2, lines.Count);
        Assert.Equal([1, 4], lines.Select(l => l.Count));
        Assert.Single(lines.Select(l => l.Signature).Distinct());

        // Pencere bitince yeni satır.
        _now = _now.AddMinutes(11);
        log.Write(AppLogLevel.Error, "move", "failed", ex);
        log.FlushSync(TimeSpan.FromSeconds(5));
        Assert.Equal(6, ReadAll().Sum(l => l.Count));
        Assert.Equal(3, ReadAll().Count);

        var group = Assert.Single(DiagnosticReport.Group(ReadAll()));
        Assert.Equal(6, group.Count);
    }

    [Fact]
    public void Repeats_are_flushed_when_window_expires_on_next_entry()
    {
        var log = Channel();
        for (var i = 0; i < 3; i++) log.Write(AppLogLevel.Warn, "a", "same", null);
        _now = _now.AddMinutes(10);
        log.Write(AppLogLevel.Warn, "b", "other", null);
        log.FlushSync(TimeSpan.FromSeconds(5));
        var lines = ReadAll();
        Assert.Equal(3, lines.Count);
        Assert.Equal(3, lines.Where(l => l.Message == "same").Sum(l => l.Count));
    }

    [Fact]
    public void Different_signatures_are_not_merged()
    {
        var log = Channel();
        log.Write(AppLogLevel.Error, "a", "m", Thrown(new InvalidOperationException()));
        log.Write(AppLogLevel.Error, "a", "m", Thrown(new FormatException()));
        log.Write(AppLogLevel.Warn, "a", "m", null);
        log.Write(AppLogLevel.Error, "b", "m", null);
        Assert.Equal(4, log.Recent().Select(e => e.Signature).Distinct().Count());
    }

    [Fact]
    public void At_most_n_lines_per_minute_reach_disk()
    {
        var log = Channel(new AppLogLimits { LinesPerMinute = 5 });
        for (var i = 0; i < 10; i++) log.Write(AppLogLevel.Info, "a", "m" + i, null);
        log.FlushSync(TimeSpan.FromSeconds(5));
        Assert.Equal(5, ReadAll().Count);
        Assert.Equal(5, log.Counters["log.ratelimited"]);
        Assert.Equal(10, log.Recent().Count);  // bellekte hepsi

        _now = _now.AddMinutes(1);
        log.Write(AppLogLevel.Info, "a", "later", null);
        log.FlushSync(TimeSpan.FromSeconds(5));
        Assert.Equal(6, ReadAll().Count);
    }

    [Fact]
    public void Nothing_is_written_when_off()
    {
        var log = Channel(persist: false);
        log.Write(AppLogLevel.Error, "a", "b", Thrown(new InvalidOperationException()));
        Assert.True(log.FlushSync(TimeSpan.FromSeconds(5)));
        Assert.False(Directory.Exists(Dir));
        Assert.Single(log.Recent());
        Assert.Equal(1, log.Counters["log.error"]);

        var on = Channel();
        on.Write(AppLogLevel.Error, "a", "pending", null);
        on.SetPersist(false);
        on.FlushSync(TimeSpan.FromSeconds(5));
        on.Write(AppLogLevel.Error, "a", "after", null);
        on.FlushSync(TimeSpan.FromSeconds(5));
        Assert.False(Directory.Exists(Dir));
    }

    [Fact]
    public void Ring_buffer_is_bounded_and_scrubbed()
    {
        var log = Channel(new AppLogLimits { RingSize = 4 }, persist: false,
            scrubber: new LogScrubber(new ScrubContext { UserNames = ["Zeynep"] }));
        for (var i = 0; i < 10; i++)
            log.Write(AppLogLevel.Error, "a", "m" + i, Thrown(new InvalidOperationException($"zeynep D:\\Gizli\\{i}.pdf")));
        var recent = log.Recent();
        Assert.Equal(4, recent.Count);
        Assert.Equal("m9", recent[^1].Message);
        Assert.All(recent, e =>
        {
            Assert.DoesNotContain("Gizli", e.Error!.Message);
            Assert.Equal("<user> <path:other>", e.Error.Message);
        });
        Assert.Equal(2, log.Recent(2).Count);
    }

    [Fact]
    public void Entries_before_attach_are_written_after_attach()
    {
        var log = Channel(dir: null);
        log.Write(AppLogLevel.Error, "startup", "early", null);
        log.FlushSync(TimeSpan.FromSeconds(5));
        Assert.False(Directory.Exists(Dir));
        log.Attach(Dir, LogScrubber.Plain, persist: true);
        log.FlushSync(TimeSpan.FromSeconds(5));
        Assert.Equal("early", Assert.Single(ReadAll()).Message);
    }

    [Fact]
    public void Mirror_receives_scrubbed_line()
    {
        var mirrored = new List<string>();
        var log = Channel(persist: false, mirror: mirrored.Add);
        log.Write(AppLogLevel.Warn, "box", "move-failed", Thrown(new IOException(@"C:\Gizli\x")));
        var line = Assert.Single(mirrored);
        Assert.StartsWith("AppLog W box: move-failed [System.IO.IOException 0x", line);
        Assert.DoesNotContain("Gizli", line);
    }

    [Fact]
    public void Delete_removes_log_files_and_recent_entries()
    {
        var log = Channel();
        log.Write(AppLogLevel.Error, "a", "b", null);
        log.FlushSync(TimeSpan.FromSeconds(5));
        Directory.CreateDirectory(Path.Combine(Dir, "crashes"));
        File.WriteAllText(Path.Combine(Dir, "notlar.txt"), "x");
        log.DeleteLogFiles();
        Assert.Empty(Files());
        Assert.Empty(log.Recent());
        Assert.True(File.Exists(Path.Combine(Dir, "notlar.txt")));
        // Silindikten sonra yine yazılabilir.
        log.Write(AppLogLevel.Error, "a", "c", null);
        log.FlushSync(TimeSpan.FromSeconds(5));
        Assert.Equal("c", Assert.Single(ReadAll()).Message);
    }

    [Fact]
    public void Background_writer_writes_batches()
    {
        var log = new AppLogChannel(Dir, LogScrubber.Plain, persist: true, new AppLogLimits { BatchDelay = TimeSpan.Zero }, () => _now, background: true);
        log.Write(AppLogLevel.Error, "a", "b", null);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (ReadAll().Count == 0 && DateTime.UtcNow < deadline) Thread.Sleep(20);
        Assert.Equal("b", Assert.Single(ReadAll()).Message);
    }

    [Fact]
    public void Session_facts_are_written()
    {
        var log = Channel();
        log.Record(AppLogLevel.Info, "session", "start", null, new DiagnosticEnvironment { Channel = "test", Monitors = 2, Scales = "100,125" }.ToFacts());
        log.FlushSync(TimeSpan.FromSeconds(5));
        var entry = Assert.Single(ReadAll());
        Assert.Equal("test", entry.Facts!["channel"]);
        Assert.Equal("2", entry.Facts["monitors"]);
    }

    [Fact]
    public void Unreadable_lines_are_skipped()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "nestdesk-20261001.jsonl"), "not json\n{\"t\":\"x\"}\n");
        var log = Channel();
        log.Write(AppLogLevel.Error, "a", "b", null);
        log.FlushSync(TimeSpan.FromSeconds(5));
        Assert.Equal("b", Assert.Single(ReadAll()).Message);
    }

    [Theory]
    [InlineData("nestdesk-20261001.jsonl", true, "20261001", 0)]
    [InlineData("nestdesk-20261001-12.jsonl", true, "20261001", 12)]
    [InlineData("NESTDESK-20261001.JSONL", true, "20261001", 0)]
    [InlineData("nestdesk-2026100.jsonl", false, "", 0)]
    [InlineData("nestdesk-20261341.jsonl", false, "", 0)]
    [InlineData("nestdesk-20261001-x.jsonl", false, "", 0)]
    [InlineData("settings.json", false, "", 0)]
    public void Log_file_names(string name, bool ok, string day, int index)
    {
        Assert.Equal(ok, LogFiles.TryParse(name, out var d, out var i));
        Assert.Equal(day, d);
        Assert.Equal(index, i);
    }

    // ------------------------------------------------------------------ kaynak taraması

    private static readonly HashSet<string> AppLogNames = ["AppLog", "Core.AppLog", "Duzenleme.Core.AppLog", "global::Duzenleme.Core.AppLog"];

    /// <summary>
    /// Hata günlüğüne giden çağrılarda ad metinleri sabit mi? AppLog.Error/Warn/Info (alan ve ileti), AppLog.Count (sayaç adı: o da
    /// tanılama sayfasına ve ozet.json'a girer) ve kanala doğrudan yazan AppLog.Channel.Write (alan ve ileti) / .Count (ad).
    /// Sorunlu çağrılar (dosya:satır).
    /// </summary>
    internal static List<string> NonLiteralLogCalls(string rel, string code)
    {
        var problems = new List<string>();
        var root = CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)).GetRoot();
        static bool Literal(ArgumentSyntax a) => a.Expression is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression);
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax access) continue;
            var method = access.Name.Identifier.Text;
            var target = access.Expression.ToString().Replace(" ", "");
            var onAppLog = AppLogNames.Contains(target);
            var onChannel = AppLogNames.Any(n => target == n + ".Channel");
            // Kaç sabit bağımsız değişken gerekir, hangisinden başlayarak (Channel.Write'ın ilki düzeydir).
            (int Skip, int Take)? need = (method, onAppLog, onChannel) switch
            {
                ("Error" or "Warn" or "Info", true, _) => (0, 2),
                ("Count", true, _) => (0, 1),
                ("Write", _, true) => (1, 2),
                ("Count", _, true) => (0, 1),
                _ => null,
            };
            if (need is not { } n) continue;
            var args = call.ArgumentList.Arguments;
            var line = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            if (args.Count < n.Skip + n.Take
                || args.Skip(n.Skip).Take(n.Take).Any(a => a.NameColon is not null && a.NameColon.Name.Identifier.Text is not ("area" or "message" or "counter" or "name")
                                                          || !Literal(a)))
                problems.Add($"{rel}:{line}: {call}");
        }
        return problems;
    }

    [Fact]
    public void AppLog_messages_are_literals_everywhere()
    {
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        var problems = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            if (rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) || rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase)) continue;
            problems.AddRange(NonLiteralLogCalls(rel, File.ReadAllText(file)));
        }
        Assert.True(problems.Count == 0,
            "AppLog alanı ve iletisi metin sabiti olmalı (değişen bilgi yalnızca istisnayla girer):\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Literal_scanner_flags_dynamic_messages()
    {
        const string code = """
            class A
            {
                void M(string path, Exception ex)
                {
                    AppLog.Error("box", "move-failed", ex);
                    AppLog.Warn("box", $"move {path}");
                    AppLog.Info("box", "a" + path);
                    Core.AppLog.Error(path, "x");
                    AppLog.Error("box", path, ex);
                    Other.Error("box", path);
                    AppLog.Count(path);
                    AppLog.Count("box.locked");
                    AppLog.Channel.Write(AppLogLevel.Warn, "box", path, ex);
                    AppLog.Channel.Write(AppLogLevel.Warn, "box", "ok", 5);
                    AppLog.Channel.Count("x." + path);
                    Other.Channel.Write(AppLogLevel.Warn, "box", path, ex);
                }
            }
            """;
        var problems = NonLiteralLogCalls("A.cs", code);
        Assert.Equal(7, problems.Count);
        // Sabitli çağrılar ve başka sınıflar sayılmaz.
        foreach (var ok in new[] { 5, 10, 12, 14, 16 }) Assert.DoesNotContain(problems, p => p.StartsWith($"A.cs:{ok}:"));
        foreach (var bad in new[] { 6, 7, 8, 9, 11, 13, 15 }) Assert.Contains(problems, p => p.StartsWith($"A.cs:{bad}:"));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
