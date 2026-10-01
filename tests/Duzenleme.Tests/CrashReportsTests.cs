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

    private string[] Names() => Directory.Exists(Dir) ? Directory.GetFiles(Dir).Select(Path.GetFileName).Order().ToArray()! : [];

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
    public void Unreadable_session_marker_is_null()
    {
        var logs = Path.Combine(_root, "logs");
        Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(logs, SessionMarker.FileName), "{bozuk");
        Assert.Null(SessionMarker.Read(logs));
    }
}
