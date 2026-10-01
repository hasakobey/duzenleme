using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>Çökme raporları: seçime uyar (Sor / Hep kaydet / Kaydetme), en az bilgi, sınırlar, süre, "soruldu" işareti, oturum nöbetçisi.</summary>
public class CrashReportsTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-crash-" + Guid.NewGuid().ToString("N"))).FullName;
    private string Dir => Path.Combine(_root, "logs", CrashReports.FolderName);
    private readonly DateTime _now = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

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

    private static readonly LogScrubber Scrubber = new(new ScrubContext { UserNames = ["Zeynep"], Desktop = [@"C:\Users\Zeynep\Desktop"] });

    private static ScrubbedError Error(string message = "Gizli not metni") =>
        Scrubber.Error(Thrown(new InvalidOperationException(message, Thrown(new ArgumentException("Gizli argüman", "path")))));

    private static IReadOnlyList<LogEntry> Recent(int count) =>
        Enumerable.Range(0, count).Select(i => new LogEntry(new DateTime(2026, 10, 1, 9, 0, i % 60, DateTimeKind.Utc), AppLogLevel.Error,
            "area", "message-" + i, null, "12345678", 1, "abc123", "2.2.0")).ToList();

    private string? Write(string? mode, bool logOff = false, DateTime? at = null, string kind = CrashReports.KindCrash) =>
        CrashReports.Write(Dir, mode, logOff, kind, Error(), Recent(5), "abc123", "2.2.0", at ?? _now);

    private string[] Names() => Directory.Exists(Dir) ? Directory.GetFiles(Dir).Select(f => Path.GetFileName(f)!).Order().ToArray() : [];

    [Fact]
    public void Never_writes_nothing()
    {
        Assert.Null(Write(CrashReportModes.Never));
        Assert.Null(Write(" NEVER "));
        Assert.Empty(Names());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sometimes")]
    public void Ask_or_unknown_writes_pending(string? mode)
    {
        var path = Write(mode)!;
        Assert.StartsWith(CrashReports.PendingPrefix, Path.GetFileName(path));
        var report = CrashReports.Read(path)!;
        Assert.True(report.IsPending);
        Assert.False(report.Asked);
        Assert.Equal(CrashReports.KindCrash, report.Kind);
    }

    [Fact]
    public void Always_writes_saved_report()
    {
        var path = Write(CrashReportModes.Always)!;
        Assert.StartsWith(CrashReports.SavedPrefix, Path.GetFileName(path));
        Assert.False(CrashReports.Read(path)!.IsPending);
    }

    [Fact]
    public void Report_holds_only_version_type_hresult_and_frames()
    {
        var text = File.ReadAllText(Write(null)!);
        Assert.DoesNotContain("Gizli", text);
        Assert.DoesNotContain("Zeynep", text);
        Assert.DoesNotContain("\"msg\"", text);
        Assert.DoesNotContain("\"param\"", text);
        var report = CrashReports.Read(Write(null)!)!;
        Assert.Equal("2.2.0", report.Version);
        Assert.Equal("abc123", report.Session);
        Assert.Equal("System.InvalidOperationException", report.Error!.Type);
        Assert.StartsWith("0x", report.Error.HResult);
        Assert.NotEmpty(report.Error.Frames);
        Assert.Equal("System.ArgumentException", Assert.Single(report.Error.Inner).Type);
        Assert.Null(report.Error.Message);
    }

    [Fact]
    public void Log_tail_only_when_log_is_on()
    {
        Assert.Equal(5, CrashReports.Read(Write(null)!)!.LogTail.Count);
        Assert.Empty(CrashReports.Read(Write(null, logOff: true)!)!.LogTail);
        Assert.DoesNotContain("\"log\"", File.ReadAllText(Write(CrashReportModes.Always, logOff: true)!));
    }

    [Fact]
    public void Report_is_capped_in_size()
    {
        var huge = Enumerable.Range(0, 30).Select(i => new LogEntry(_now, AppLogLevel.Error, "a", new string('x', 190), Error(),
            "12345678", 1, "abc123", "2.2.0")).ToList();
        var path = CrashReports.Write(Dir, null, false, CrashReports.KindCrash, Error(), huge, "abc123", "2.2.0", _now)!;
        Assert.True(new FileInfo(path).Length <= CrashReports.MaxBytes, new FileInfo(path).Length.ToString());
        Assert.NotNull(CrashReports.Read(path)!.Error);
    }

    [Fact]
    public void At_most_three_pending_and_ten_saved()
    {
        for (var i = 0; i < 5; i++) Write(null, at: _now.AddMinutes(i));
        for (var i = 0; i < 12; i++) Write(CrashReportModes.Always, at: _now.AddMinutes(i));
        var pending = CrashReports.List(Dir, pending: true);
        var saved = CrashReports.List(Dir, pending: false);
        Assert.Equal(CrashReports.MaxPending, pending.Count);
        Assert.Equal(CrashReports.MaxSaved, saved.Count);
        // En yeniler kalır.
        Assert.Equal(_now.AddMinutes(4), pending[^1].Utc);
        Assert.Equal(_now.AddMinutes(2), pending[0].Utc);
        Assert.Equal(_now.AddMinutes(2), saved[0].Utc);
    }

    [Fact]
    public void Reports_older_than_14_days_are_deleted()
    {
        Write(null, at: _now.AddDays(-20));
        Write(CrashReportModes.Always, at: _now.AddDays(-15));
        Write(CrashReportModes.Always, at: _now.AddDays(-3));
        CrashReports.Prune(Dir, _now);
        Assert.Empty(CrashReports.List(Dir, pending: true));
        Assert.Equal(_now.AddDays(-3), Assert.Single(CrashReports.List(Dir, pending: false)).Utc);
    }

    [Fact]
    public void Asked_flag_hides_report_from_next_prompt()
    {
        var path = Write(null)!;
        Assert.Single(CrashReports.ToAsk(Dir));
        CrashReports.MarkAsked(path);
        Assert.Empty(CrashReports.ToAsk(Dir));
        Assert.True(CrashReports.Read(path)!.Asked);
        Assert.Single(CrashReports.List(Dir, pending: true));
    }

    /// <summary>
    /// Açılıştan hemen sonra çöken oturum, süreç kapanana dek açılış denetimini çalıştırabilir: kendi raporunu sormamalı
    /// ("soruldu" işaretlerse sonraki açılışta şerit çıkmaz). Önceki oturumların raporları yine sorulur.
    /// </summary>
    [Fact]
    public void Own_session_report_is_left_for_the_next_start()
    {
        var own = Write(null)!; // oturum "abc123"
        var previous = CrashReports.Write(Dir, null, false, CrashReports.KindCrash, Error(), Recent(1), "old999", "2.2.0", _now.AddMinutes(-5))!;
        Assert.Equal([previous], CrashReports.ToAsk(Dir, currentSession: "abc123").Select(r => r.Path));
        Assert.Equal(2, CrashReports.ToAsk(Dir, currentSession: "other1").Count);
        Assert.Equal(2, CrashReports.ToAsk(Dir).Count);
        Assert.False(CrashReports.Read(own)!.Asked);
    }

    /// <summary>"Günlükleri sil" yarıda kalmış yazmaların .tmp kopyalarını da siler (raporun bir kopyasını taşıyabilirler).</summary>
    [Fact]
    public void Delete_all_removes_interrupted_temp_copies()
    {
        var pending = Write(null)!;
        var saved = Write(CrashReportModes.Always, at: _now.AddSeconds(1))!;
        File.WriteAllText(pending + ".tmp", "");
        File.WriteAllText(saved + ".tmp", "{}");
        File.WriteAllText(Path.Combine(Dir, "notes.json.tmp"), "başka bir dosya");
        CrashReports.DeleteAll(Dir);
        Assert.Equal(["notes.json.tmp"], Names());
    }

    [Fact]
    public void Save_and_discard()
    {
        var first = Write(null, at: _now)!;
        var second = Write(null, at: _now.AddSeconds(1))!;
        CrashReports.MarkAsked(first);
        var saved = CrashReports.Save(first, _now)!;
        Assert.StartsWith(CrashReports.SavedPrefix, Path.GetFileName(saved));
        Assert.False(File.Exists(first));
        Assert.False(CrashReports.Read(saved)!.Asked);
        CrashReports.Discard(second);
        Assert.Empty(CrashReports.List(Dir, pending: true));
        Assert.Single(CrashReports.List(Dir, pending: false));

        // Kaydedilmiş rapor "Kaydetme" ile silinmez.
        CrashReports.Discard(saved);
        Assert.True(File.Exists(saved));
        Assert.Null(CrashReports.Save(saved, _now));
    }

    [Fact]
    public void Save_all_and_delete_all()
    {
        Write(null, at: _now);
        Write(null, at: _now.AddSeconds(1));
        File.WriteAllText(Path.Combine(Dir, "notlar.json"), "{}");
        CrashReports.SaveAll(Dir, _now);
        Assert.Equal(2, CrashReports.List(Dir, pending: false).Count);
        CrashReports.DeleteAll(Dir);
        Assert.Equal(["notlar.json"], Names());
    }

    [Fact]
    public void Write_once_writes_only_the_first_crash()
    {
        CrashReports.ResetOnce();
        try
        {
            var first = CrashReports.WriteOnce(Dir, null, false, CrashReports.KindCrash, Thrown(new InvalidOperationException("Gizli")),
                Recent(2), Scrubber, "abc123", _now);
            var second = CrashReports.WriteOnce(Dir, null, false, CrashReports.KindCrash, Thrown(new InvalidOperationException()),
                Recent(2), Scrubber, "abc123", _now.AddSeconds(1));
            Assert.NotNull(first);
            Assert.Null(second);
            Assert.Single(Names());
            Assert.DoesNotContain("Gizli", File.ReadAllText(first));
        }
        finally
        {
            CrashReports.ResetOnce();
        }
    }

    [Fact]
    public void Hang_report_has_no_exception()
    {
        var path = CrashReports.Write(Dir, null, false, CrashReports.KindHang, null, [], "abc123", "2.2.0", _now)!;
        var report = CrashReports.Read(path)!;
        Assert.Equal(CrashReports.KindHang, report.Kind);
        Assert.Null(report.Error);
    }

    [Fact]
    public void Unreadable_report_is_ignored()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "pending-20261001-100000-000.json"), "bozuk");
        Assert.Empty(CrashReports.List(Dir, pending: true));
    }

    // ------------------------------------------------------------------ oturum nöbetçisi

    [Fact]
    public void Session_marker_round_trips_and_is_deleted()
    {
        var logs = Path.Combine(_root, "logs");
        Directory.CreateDirectory(logs);
        var state = new SessionState("abc123", "2.2.0", _now, 42, Hang: false);
        SessionMarker.Write(logs, state);
        Assert.Equal(state, SessionMarker.Read(logs)! with { Extra = null });
        SessionMarker.Write(logs, state with { Hang = true });
        Assert.True(SessionMarker.Read(logs)!.Hang);
        SessionMarker.Delete(logs);
        Assert.Null(SessionMarker.Read(logs));
        SessionMarker.Delete(logs);
    }

    [Fact]
    public void Previous_session_assessment()
    {
        var state = new SessionState("abc123", "2.2.0", _now, 42, Hang: false);
        Assert.Equal(PreviousSession.Clean, SessionMarker.Assess(null, "2.2.0"));
        Assert.Equal(PreviousSession.Unclean, SessionMarker.Assess(state, "2.2.0"));
        Assert.Equal(PreviousSession.Hang, SessionMarker.Assess(state with { Hang = true }, "2.2.0"));
        // Güncelleme süreci kapatır: sürüm değiştiyse donma bile sorulmaz.
        Assert.Equal(PreviousSession.Unclean, SessionMarker.Assess(state with { Hang = true }, "2.2.1"));
    }

    [Fact]
    public void Session_ending_counts_as_clean_and_is_written_only_when_set()
    {
        var logs = Path.Combine(_root, "logs");
        Directory.CreateDirectory(logs);
        var state = new SessionState("abc123", "2.2.0", _now, 42, Hang: true);
        SessionMarker.Write(logs, state);
        Assert.DoesNotContain("Ending", File.ReadAllText(Path.Combine(logs, SessionMarker.FileName)));
        SessionMarker.Write(logs, state with { Ending = true });
        var read = SessionMarker.Read(logs)!;
        Assert.True(read.Ending);
        // Windows oturumu kapanırken sonlandırılan süreç: donma kaydı olsa da soru yok.
        Assert.Equal(PreviousSession.Clean, SessionMarker.Assess(read, "2.2.0"));
    }

    [Fact]
    public void Hang_report_takes_only_the_previous_sessions_log_lines()
    {
        var logs = Path.Combine(_root, "logs");
        var channel = new AppLogChannel(logs, LogScrubber.Plain, persist: true, utcNow: () => _now.AddMinutes(-30), background: false,
            session: "old111", version: "2.2.0");
        channel.Write(AppLogLevel.Warn, "ui", "ui-unresponsive", null);
        Assert.True(channel.FlushSync(TimeSpan.FromSeconds(5)));
        var other = new AppLogChannel(logs, LogScrubber.Plain, persist: true, utcNow: () => _now.AddMinutes(-1), background: false,
            session: "new222", version: "2.2.0");
        other.Write(AppLogLevel.Info, "session", "start", null);
        Assert.True(other.FlushSync(TimeSpan.FromSeconds(5)));
        var previous = new SessionState("old111", "2.2.0", _now.AddHours(-1), 7, Hang: true);

        var path = CrashReports.WriteHang(logs, null, logOff: false, previous, _now);
        var report = CrashReports.Read(path!)!;
        Assert.True(report.IsPending);
        Assert.Equal(CrashReports.KindHang, report.Kind);
        Assert.Equal("old111", report.Session);
        Assert.Null(report.Error);
        Assert.Equal(["ui-unresponsive"], report.LogTail.Select(e => e.Message));

        // Günlük kapalıysa günlük satırı eklenmez; "Kaydetme" hiçbir şey yazmaz.
        CrashReports.DeleteAll(Dir);
        Assert.Empty(CrashReports.Read(CrashReports.WriteHang(logs, CrashReportModes.Always, logOff: true, previous, _now.AddSeconds(1))!)!.LogTail);
        CrashReports.DeleteAll(Dir);
        Assert.Null(CrashReports.WriteHang(logs, CrashReportModes.Never, logOff: false, previous, _now.AddSeconds(2)));
    }

    [Fact]
    public void Hang_then_crash_in_the_same_session_gives_a_single_report()
    {
        var logs = Path.Combine(_root, "logs");
        var previous = new SessionState("abc123", "2.2.0", _now.AddHours(-1), 7, Hang: true);
        // Oturum donmuş, sonra çökmüş: çökme raporu (bekleyen ya da kaydedilmiş) zaten var, donma raporu yazılmaz.
        Assert.NotNull(Write(null));
        Assert.True(CrashReports.HasReportFor(Dir, "abc123"));
        Assert.Null(CrashReports.WriteHang(logs, null, logOff: false, previous, _now.AddMinutes(1)));
        Assert.Single(Names());

        CrashReports.Save(Directory.GetFiles(Dir).Single(), _now);
        Assert.Null(CrashReports.WriteHang(logs, CrashReportModes.Always, logOff: false, previous, _now.AddMinutes(2)));
        Assert.Single(Names());

        // Başka oturumun donması ayrı rapor olur.
        Assert.False(CrashReports.HasReportFor(Dir, "other1"));
        Assert.NotNull(CrashReports.WriteHang(logs, null, logOff: false, previous with { Session = "other1" }, _now.AddMinutes(3)));
        Assert.Equal(2, Names().Length);
        Assert.False(CrashReports.HasReportFor(Dir, ""));
    }

    [Fact]
    public void Unreadable_session_marker_is_null()
    {
        var logs = Path.Combine(_root, "logs");
        Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(logs, SessionMarker.FileName), "{bozuk");
        Assert.Null(SessionMarker.Read(logs));
    }
}
