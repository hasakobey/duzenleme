using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Duzenleme.Core;

/// <summary>
/// Kaydedilmiş ya da kaydedilmeyi bekleyen çökme raporu. Yalnızca sürüm, oturum, tür (crash/hang), istisnanın türü, HResult
/// ve temizlenmiş yığın kareleri; günlük açıksa günlüğün son satırları. İleti, yol ve ayar yoktur.
/// </summary>
public sealed record CrashReport(string Path, DateTime Utc, string Version, string Session, string Kind,
    ScrubbedError? Error, IReadOnlyList<LogEntry> LogTail, bool Asked)
{
    public bool IsPending => System.IO.Path.GetFileName(Path).StartsWith(CrashReports.PendingPrefix, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Çökme raporları (<c>&lt;veri&gt;\logs\crashes</c>): "Sor" seçiliyse <c>pending-*.json</c> (sonraki açılışta şeritle sorulur),
/// "Sormadan kaydet" ise <c>crash-*.json</c>, "Kaydetme" ise hiçbir şey. En çok 3 bekleyen, 10 kaydedilmiş rapor; 14 günü
/// geçenler silinir. Çökme anında çağıran iş parçacığında, eşzamanlı ve süreçte bir kez yazılır (<see cref="WriteOnce"/>).
/// </summary>
public static class CrashReports
{
    public const string FolderName = "crashes";
    public const string PendingPrefix = "pending-";
    public const string SavedPrefix = "crash-";
    public const string KindCrash = "crash";
    public const string KindHang = "hang";
    public const int MaxPending = 3;
    public const int MaxSaved = 10;
    public const int RetentionDays = 14;
    public const int MaxBytes = 32 * 1024;
    public const int MaxLogTail = 30;

    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = true };
    private static int s_once;

    /// <summary>
    /// Çökme anında: süreçte yalnızca ilk çağrı yazar (iç içe/çoklu iş parçacığından gelen çökmeler tek rapor). Hata fırlatmaz;
    /// yazılan dosyanın yolu ya da null.
    /// </summary>
    public static string? WriteOnce(string crashesDirectory, string? mode, bool logOff, string kind, Exception? ex,
        IReadOnlyList<LogEntry> recent, LogScrubber scrubber, string session, DateTime utcNow)
    {
        if (Interlocked.Exchange(ref s_once, 1) != 0) return null;
        try
        {
            var error = ex is null ? null : scrubber.Error(ex);
            return Write(crashesDirectory, mode, logOff, kind, error, recent, session, AppInfo.Version, utcNow);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Testler için: <see cref="WriteOnce"/>'ı yeniden kurar.</summary>
    internal static void ResetOnce() => Interlocked.Exchange(ref s_once, 0);

    /// <summary>
    /// Raporu yazar (çağıran iş parçacığında). <paramref name="mode"/>: <see cref="AppSettings.CrashReportMode"/> (null/tanınmayan
    /// = sor). Günlük kapalıysa (<paramref name="logOff"/>) son satırlar eklenmez. Yazılan dosya ya da ("Kaydetme") null.
    /// Disk hatasını fırlatır (<see cref="WriteOnce"/> yutar; donma raporunu arka planda yazan da yakalamalı).
    /// </summary>
    public static string? Write(string crashesDirectory, string? mode, bool logOff, string kind, ScrubbedError? error,
        IReadOnlyList<LogEntry> recent, string session, string version, DateTime utcNow)
    {
        var normalized = CrashReportModes.Normalize(mode);
        if (normalized == CrashReportModes.Never) return null;
        var pending = normalized != CrashReportModes.Always;
        var tail = logOff ? [] : recent.Skip(Math.Max(0, recent.Count - MaxLogTail)).ToList();
        var report = new CrashReport("", utcNow, version, session, kind == KindHang ? KindHang : KindCrash,
            error?.Minimal(), tail, Asked: false);
        var bytes = Serialize(report);

        Directory.CreateDirectory(crashesDirectory);
        var path = UniquePath(crashesDirectory, (pending ? PendingPrefix : SavedPrefix) + Stamp(utcNow));
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        try { Prune(crashesDirectory, utcNow, keep: path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return path;
    }

    /// <summary>
    /// Önceki oturum donmuş ve düzgün kapanmamıştı (sonraki açılışta, arka planda): "hang" raporu. Günlük açıksa o oturumun
    /// son satırları diskteki günlükten alınır (bu oturumun belleğindekiler değil). Disk hatasını fırlatır.
    /// </summary>
    public static string? WriteHang(string logsDirectory, string? mode, bool logOff, SessionState previous, DateTime utcNow)
    {
        // Donup sonra çöken oturumun çökme raporu zaten var (bekleyen ya da kaydedilmiş): aynı oturum için ikinci rapor ve
        // ikinci soru olmasın.
        if (HasReportFor(Path.Combine(logsDirectory, FolderName), previous.Session)) return null;
        IReadOnlyList<LogEntry> tail = logOff || CrashReportModes.Normalize(mode) == CrashReportModes.Never
            ? []
            : LogFiles.Read(logsDirectory, previous.StartUtc).Where(e => e.Session == previous.Session).ToList();
        return Write(Path.Combine(logsDirectory, FolderName), mode, logOff, KindHang, null, tail, previous.Session,
            previous.Version, utcNow);
    }

    private static string Stamp(DateTime utc) => utc.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);

    private static string UniquePath(string directory, string stem)
    {
        var path = Path.Combine(directory, stem + ".json");
        for (var i = 1; File.Exists(path) && i < 100; i++) path = Path.Combine(directory, $"{stem}-{i}.json");
        return path;
    }

    /// <summary>JSON (en çok <see cref="MaxBytes"/>: sığmazsa önce günlük satırları, sonra kareler kısaltılır).</summary>
    internal static byte[] Serialize(CrashReport report)
    {
        var bytes = SerializeOnce(report);
        var tail = report.LogTail.ToList();
        while (bytes.Length > MaxBytes && tail.Count > 0)
        {
            tail.RemoveAt(0);
            bytes = SerializeOnce(report with { LogTail = tail });
        }
        if (bytes.Length > MaxBytes && report.Error is { } error)
        {
            var shorter = error with { Frames = error.Frames.Take(15).ToList(), Inner = [] };
            bytes = SerializeOnce(report with { LogTail = [], Error = shorter });
        }
        return bytes;
    }

    private static byte[] SerializeOnce(CrashReport report)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteNumber("format", 1);
            json.WriteString("t", report.Utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
            json.WriteString("v", report.Version);
            json.WriteString("sid", report.Session);
            json.WriteString("kind", report.Kind);
            if (report.Asked) json.WriteBoolean("asked", true);
            if (report.Error is not null)
            {
                json.WritePropertyName("x");
                report.Error.WriteJson(json);
            }
            if (report.LogTail.Count > 0)
            {
                json.WriteStartArray("log");
                foreach (var entry in report.LogTail)
                {
                    var line = entry.ToJsonLine();
                    json.WriteRawValue(line.AsSpan(0, line.Length - 1), skipInputValidation: false);
                }
                json.WriteEndArray();
            }
            json.WriteEndObject();
        }
        return buffer.ToArray();
    }

    /// <summary>Rapor dosyasını okur; okunamıyorsa ya da biçimi tanınmıyorsa null.</summary>
    public static CrashReport? Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 4 * MaxBytes) return null;
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            var e = doc.RootElement;
            if (e.ValueKind != JsonValueKind.Object) return null;
            static string Str(JsonElement o, string name) =>
                o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var utc = DateTime.TryParse(Str(e, "t"), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
                ? DateTime.SpecifyKind(t, DateTimeKind.Utc)
                : info.LastWriteTimeUtc;
            var tail = new List<LogEntry>();
            if (e.TryGetProperty("log", out var log) && log.ValueKind == JsonValueKind.Array)
                foreach (var item in log.EnumerateArray().Take(MaxLogTail))
                    if (LogEntry.Parse(item.GetRawText()) is { } entry) tail.Add(entry);
            var kind = Str(e, "kind") == KindHang ? KindHang : KindCrash;
            var asked = e.TryGetProperty("asked", out var a) && a.ValueKind == JsonValueKind.True;
            var error = e.TryGetProperty("x", out var x) ? ScrubbedError.FromJson(x) : null;
            return new CrashReport(path, utc, Str(e, "v"), Str(e, "sid"), kind, error, tail, asked);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Bekleyen (<paramref name="pending"/>) ya da kaydedilmiş raporlar, eskiden yeniye.</summary>
    public static IReadOnlyList<CrashReport> List(string crashesDirectory, bool pending) =>
        Files(crashesDirectory, pending ? PendingPrefix : SavedPrefix).Select(Read).OfType<CrashReport>().ToList();

    /// <summary>Bu oturum için bekleyen ya da kaydedilmiş bir rapor var mı?</summary>
    public static bool HasReportFor(string crashesDirectory, string session) =>
        session.Length > 0
        && List(crashesDirectory, pending: true).Concat(List(crashesDirectory, pending: false))
            .Any(r => string.Equals(r.Session, session, StringComparison.Ordinal));

    /// <summary>
    /// Henüz sorulmamış bekleyen raporlar (açılıştaki şerit yalnızca bunları sorar; tepsi balonu her açılışta çıkmasın).
    /// <paramref name="currentSession"/>: bu oturumun kendi raporu sorulmaz. Açılıştan hemen sonra çöken oturum, süreç
    /// kapanana dek açılış denetimini çalıştırıp az önce yazdığı raporu "soruldu" işaretleyebilirdi: sonraki açılışta şerit
    /// çıkmazdı (ve yarıda kalan işaretleme bir .tmp bırakırdı).
    /// </summary>
    public static IReadOnlyList<CrashReport> ToAsk(string crashesDirectory, string? currentSession = null) =>
        List(crashesDirectory, pending: true)
            .Where(r => !r.Asked && !(currentSession is { Length: > 0 } && string.Equals(r.Session, currentSession, StringComparison.Ordinal)))
            .ToList();

    /// <summary>Raporun bir kez sorulduğunu işaretler.</summary>
    public static void MarkAsked(string path)
    {
        if (Read(path) is not { IsPending: true } report || report.Asked) return;
        try { DurableFile.WriteAtomic(path, Serialize(report with { Asked = true }), keepBackup: false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>"Kaydet": bekleyen raporu kaydedilmişlere taşır. Yeni yol ya da (taşınamadıysa) null.</summary>
    public static string? Save(string pendingPath, DateTime utcNow)
    {
        var name = Path.GetFileName(pendingPath);
        if (!name.StartsWith(PendingPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(pendingPath)) return null;
        var directory = Path.GetDirectoryName(pendingPath)!;
        var target = UniquePath(directory, SavedPrefix + Path.GetFileNameWithoutExtension(name)[PendingPrefix.Length..]);
        try
        {
            if (Read(pendingPath) is { } report && report.Asked)
                DurableFile.WriteAtomic(pendingPath, Serialize(report with { Asked = false }), keepBackup: false);
            File.Move(pendingPath, target);
            Prune(directory, utcNow, keep: target);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>"Kaydetme": bekleyen raporu siler.</summary>
    public static void Discard(string pendingPath)
    {
        if (!Path.GetFileName(pendingPath).StartsWith(PendingPrefix, StringComparison.OrdinalIgnoreCase)) return;
        try { File.Delete(pendingPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Bütün bekleyenleri kaydeder ("Hep kaydet" seçildiğinde).</summary>
    public static void SaveAll(string crashesDirectory, DateTime utcNow)
    {
        foreach (var file in Files(crashesDirectory, PendingPrefix)) Save(file, utcNow);
    }

    /// <summary>"Günlükleri sil": bütün çökme raporlarını (yarıda kalmış yazmaların .tmp kopyaları dahil) siler.</summary>
    public static void DeleteAll(string crashesDirectory)
    {
        foreach (var file in Files(crashesDirectory, PendingPrefix).Concat(Files(crashesDirectory, SavedPrefix)).Concat(Leftovers(crashesDirectory)))
        {
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Yarıda kalmış atomik yazmaların kopyaları (pending-*.json.tmp, crash-*.json.tmp): raporun kendisi gibi silinir.</summary>
    private static IEnumerable<string> Leftovers(string crashesDirectory)
    {
        try
        {
            if (!Directory.Exists(crashesDirectory)) return [];
            return Directory.EnumerateFiles(crashesDirectory, "*.json.tmp", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f) is var name
                            && (name.StartsWith(PendingPrefix, StringComparison.OrdinalIgnoreCase) || name.StartsWith(SavedPrefix, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>14 günü geçenleri siler; en çok 3 bekleyen ve 10 kaydedilmiş kalır (en eskiler gider, <paramref name="keep"/> kalır).</summary>
    public static void Prune(string crashesDirectory, DateTime utcNow, string? keep = null)
    {
        foreach (var (prefix, max) in new[] { (PendingPrefix, MaxPending), (SavedPrefix, MaxSaved) })
        {
            var files = Files(crashesDirectory, prefix).ToList();
            foreach (var file in files.ToList())
            {
                if (string.Equals(file, keep, StringComparison.OrdinalIgnoreCase)) continue;
                if (utcNow - TimeOf(file) > TimeSpan.FromDays(RetentionDays) && TryDelete(file)) files.Remove(file);
            }
            foreach (var file in files.ToList())
            {
                if (files.Count <= max) break;
                if (string.Equals(file, keep, StringComparison.OrdinalIgnoreCase)) continue;
                if (TryDelete(file)) files.Remove(file);
            }
        }
    }

    private static bool TryDelete(string file)
    {
        try
        {
            File.Delete(file);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Dosya adındaki an (pending-yyyyMMdd-HHmmss-fff); okunamazsa dosyanın yazılma zamanı.</summary>
    internal static DateTime TimeOf(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var dash = name.IndexOf('-');
        var stamp = dash < 0 ? "" : name[(dash + 1)..];
        if (stamp.Length >= 19
            && DateTime.TryParseExact(stamp[..19], "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc))
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        try { return File.GetLastWriteTimeUtc(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return DateTime.MinValue; }
    }

    /// <summary>Önekle başlayan rapor dosyaları, eskiden yeniye (adı bu biçimde olmayanlara dokunulmaz).</summary>
    private static IEnumerable<string> Files(string crashesDirectory, string prefix)
    {
        try
        {
            if (!Directory.Exists(crashesDirectory)) return [];
            return Directory.EnumerateFiles(crashesDirectory, prefix + "*.json", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(TimeOf).ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

/// <summary>Önceki oturumun nasıl bittiği (yalnızca çalışma zamanı).</summary>
public enum PreviousSession
{
    /// <summary>Nöbetçi dosya yok: temiz çıkış ya da ilk açılış.</summary>
    Clean,
    /// <summary>Düzgün kapanmadı ama donma kaydı yok ya da sürüm değişti (güncelleme, elektrik kesintisi): yalnızca Info, soru yok.</summary>
    Unclean,
    /// <summary>Arayüz uzun süre yanıt vermedi ve süreç düzgün kapanmadı: "hang" raporu sorulur.</summary>
    Hang,
}

/// <summary>Oturum nöbetçisi (<c>logs\session.json</c>): yalnızca asıl örnek yazar ve siler.</summary>
public sealed record SessionState(string Session, string Version, DateTime StartUtc, int ProcessId, bool Hang)
{
    /// <summary>
    /// Windows oturumu kapanıyor (SessionEnding): süreç Exit'e varmadan sonlandırılabilir; nöbetçi silinmez (kapanış iptal
    /// edilebilir) ama sonraki açılış bunu olağan kapanış sayar.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Ending { get; init; }

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public static class SessionMarker
{
    public const string FileName = "session.json";

    /// <summary>Oturum kapanışı işaretlendikten sonra bu süre içinde başlayan yanıtsızlık kapanışın kendisidir (bkz. <see cref="WithHang"/>).</summary>
    public static readonly TimeSpan EndingGrace = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Arayüz nöbetçisinin donma (true) ya da toparlanma (false) bildirimini nöbetçiye işler. Toparlanma "kapanıyor" işaretine
    /// dokunmaz. Donma, oturum kapanışı işaretlendikten (<paramref name="sinceEnding"/>) sonraki <see cref="EndingGrace"/> içinde
    /// geldiyse kapanışın kendisidir (SessionEnding'deki eşzamanlı yazmalar arayüzü birkaç saniye tutabilir): "kapanıyor" kalır ve
    /// sonraki açılış bunu olağan kapanış sayar. Daha sonra gelirse kapanış iptal edilmiş sayılır, işaret kalkar ve gerçek donma
    /// yine bildirilir.
    /// </summary>
    public static SessionState WithHang(SessionState state, bool hung, TimeSpan? sinceEnding)
    {
        if (!hung) return state.Hang ? state with { Hang = false } : state;
        var keepEnding = state.Ending && sinceEnding is { } since && since < EndingGrace;
        return state.Hang && state.Ending == keepEnding ? state : state with { Hang = true, Ending = keepEnding };
    }

    public static void Write(string logsDirectory, SessionState state) =>
        DurableFile.WriteAtomic(Path.Combine(logsDirectory, FileName), JsonSerializer.SerializeToUtf8Bytes(state, JsonFile.Options), keepBackup: false);

    public static SessionState? Read(string logsDirectory)
    {
        var path = Path.Combine(logsDirectory, FileName);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<SessionState>(File.ReadAllBytes(path), JsonFile.Options) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void Delete(string logsDirectory)
    {
        try { File.Delete(Path.Combine(logsDirectory, FileName)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Açılışta bulunan nöbetçiye göre önceki oturum. Sürüm farklıysa (Store güncellemesi süreci kapatır) ya da donma kaydı
    /// yoksa (elektrik kesintisi, Görev Yöneticisi) soru sorulmaz; yalnızca donmuş ve aynı sürümdeki oturum "Hang" olur.
    /// </summary>
    public static PreviousSession Assess(SessionState? previous, string currentVersion)
    {
        // Oturum kapanırken (SessionEnding) Windows süreci bekletmeden sonlandırabilir: olağan kapanış.
        if (previous is null || previous.Ending) return PreviousSession.Clean;
        if (!string.Equals(previous.Version, currentVersion, StringComparison.Ordinal)) return PreviousSession.Unclean;
        return previous.Hang ? PreviousSession.Hang : PreviousSession.Unclean;
    }
}
