using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Duzenleme.Core;

/// <summary>Hata günlüğü düzeyi (yalnızca çalışma zamanı; diske tek harf yazılır: E, W, I).</summary>
public enum AppLogLevel { Info, Warn, Error }

/// <summary>
/// Hata günlüğünün bir kaydı (temizlenmiş). <see cref="Count"/> aynı imzalı kaç olayı temsil ettiğidir: tekrarlar ayrı satır
/// olmaz, bir sonraki satırda sayılır. <see cref="Signature"/> düzey + alan + ileti + istisna türü + ilk uygulama karesinin
/// özetidir (8 onaltılık karakter; rapor hataları buna göre gruplar).
/// </summary>
public sealed record LogEntry(DateTime Utc, AppLogLevel Level, string Area, string Message, ScrubbedError? Error,
    string Signature, int Count, string Session, string Version, IReadOnlyDictionary<string, string>? Facts = null)
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public char LevelChar => Level switch
    {
        AppLogLevel.Error => 'E',
        AppLogLevel.Warn => 'W',
        _ => 'I',
    };

    /// <summary>JSONL satırı (sonunda "\n").</summary>
    public byte[] ToJsonLine()
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteString("t", Utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
            json.WriteString("l", LevelChar.ToString());
            json.WriteString("sid", Session);
            json.WriteString("v", Version);
            json.WriteString("a", Area);
            json.WriteString("m", Message);
            json.WriteString("s", Signature);
            json.WriteNumber("n", Count);
            if (Error is not null)
            {
                json.WritePropertyName("x");
                Error.WriteJson(json);
            }
            if (Facts is { Count: > 0 })
            {
                json.WriteStartObject("f");
                foreach (var (key, value) in Facts) json.WriteString(key, value);
                json.WriteEndObject();
            }
            json.WriteEndObject();
        }
        buffer.WriteByte((byte)'\n');
        return buffer.ToArray();
    }

    /// <summary>Diskteki bir satırı okur; biçim tanınmıyorsa null. Okunan metin veridir (rapor yine kaçışlar).</summary>
    public static LogEntry? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var e = doc.RootElement;
            if (e.ValueKind != JsonValueKind.Object) return null;
            static string Str(JsonElement o, string name) =>
                o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            if (!DateTime.TryParse(Str(e, "t"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc))
                return null;
            var level = Str(e, "l") switch
            {
                "E" => AppLogLevel.Error,
                "W" => AppLogLevel.Warn,
                _ => AppLogLevel.Info,
            };
            var count = e.TryGetProperty("n", out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var c) && c > 0 ? c : 1;
            var error = e.TryGetProperty("x", out var x) ? ScrubbedError.FromJson(x) : null;
            Dictionary<string, string>? facts = null;
            if (e.TryGetProperty("f", out var f) && f.ValueKind == JsonValueKind.Object)
            {
                facts = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var p in f.EnumerateObject().Take(40))
                    if (p.Value.ValueKind == JsonValueKind.String) facts[p.Name] = p.Value.GetString() ?? "";
            }
            return new LogEntry(DateTime.SpecifyKind(utc, DateTimeKind.Utc), level, Str(e, "a"), Str(e, "m"), error,
                Str(e, "s"), count, Str(e, "sid"), Str(e, "v"), facts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>İmza: düzey + alan + ileti + istisna türü + uygulamanın ilk yığın karesi (SHA-256'nın ilk 8 karakteri).</summary>
    public static string SignatureOf(AppLogLevel level, string area, string message, ScrubbedError? error)
    {
        var key = string.Join("|", level.ToString(), area, message, error?.Type ?? "", error?.FirstAppFrame ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..8].ToLowerInvariant();
    }
}

/// <summary>Günlüğün sınırları (testler küçültür).</summary>
public sealed record AppLogLimits
{
    /// <summary>Dosya başına en çok bayt; aşılınca aynı günün yeni dosyası (nestdesk-YYYYMMDD-1.jsonl).</summary>
    public long FileBytes { get; init; } = 512 * 1024;
    /// <summary>Bütün günlük dosyalarının toplamı; aşılınca en eskiler silinir.</summary>
    public long TotalBytes { get; init; } = 4 * 1024 * 1024;
    /// <summary>Bu kadar günden eski dosyalar silinir.</summary>
    public int RetentionDays { get; init; } = 14;
    /// <summary>Dakikada diske giden en çok satır; fazlası yalnızca sayaca.</summary>
    public int LinesPerMinute { get; init; } = 60;
    /// <summary>Aynı imzalı tekrarların ayrı satır olmadığı süre.</summary>
    public TimeSpan DedupeWindow { get; init; } = TimeSpan.FromMinutes(10);
    /// <summary>Bellekteki son kayıtlar.</summary>
    public int RingSize { get; init; } = 256;
    /// <summary>İlk kayıttan sonra toplu yazmadan önce beklenen süre (aynı anda gelenler tek yazmada).</summary>
    public TimeSpan BatchDelay { get; init; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Hata günlüğü kanalı (DebugLog'dan ayrı; içerik <see cref="LogScrubber"/> ile temizlenir). Kayıtlar çağıran iş
/// parçacığında temizlenir ve bellekteki halka arabelleğe girer; diske arka planda (<see cref="BackgroundIo"/>) toplu yazılır,
/// dosya her toplu yazmada açılıp kapanır ("Günlükleri sil" bekleyen silme bırakmasın). Diske yazma kapalıyken
/// (<see cref="SetPersist"/>) hiçbir dosya oluşmaz; halka arabellek ve sayaçlar sürer. Hiçbir yöntemi hata fırlatmaz.
/// </summary>
public sealed class AppLogChannel
{
    private sealed class Repeat(LogEntry first, DateTime windowStart)
    {
        public LogEntry First { get; } = first;
        public DateTime WindowStart { get; } = windowStart;
        public int Suppressed { get; set; }
        public DateTime LastUtc { get; set; } = windowStart;
    }

    private const int MaxPending = 2048;

    private readonly object _gate = new();
    private readonly object _fileGate = new();
    private readonly AppLogLimits _limits;
    private readonly Func<DateTime> _utcNow;
    private readonly bool _background;
    private readonly Queue<LogEntry> _ring = new();
    private readonly List<LogEntry> _pending = [];
    private readonly Dictionary<string, Repeat> _repeats = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, long> _counters = new(StringComparer.Ordinal);
    private LogScrubber _scrubber;
    private string? _directory;
    private bool _persist;
    private Action<string>? _mirror;
    private DateTime _minuteStart = DateTime.MinValue;
    private int _minuteLines;
    private bool _scheduled;

    // Yazılan dosyanın durumu (_fileGate altında).
    private string? _currentFile;
    private string? _currentDay;
    private int _currentIndex;
    private long _currentSize;
    private string? _prunedDay;

    public AppLogChannel(string? directory, LogScrubber scrubber, bool persist, AppLogLimits? limits = null,
        Func<DateTime>? utcNow = null, bool background = true, Action<string>? mirror = null, string? session = null, string? version = null)
    {
        _directory = directory;
        _scrubber = scrubber;
        _persist = persist;
        _limits = limits ?? new AppLogLimits();
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _background = background;
        _mirror = mirror;
        Session = session ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();
        Version = version ?? AppInfo.Version;
    }

    /// <summary>Bu oturumun kısa kimliği (6 onaltılık karakter; satırlar ve çökme raporu onu taşır).</summary>
    public string Session { get; }
    public string Version { get; }

    public string? Directory
    {
        get { lock (_gate) return _directory; }
    }

    public bool Persist
    {
        get { lock (_gate) return _persist; }
    }

    public LogScrubber Scrubber
    {
        get { lock (_gate) return _scrubber; }
    }

    /// <summary>Günlük klasörünü ve temizleyiciyi bağlar; o ana dek bekleyen kayıtlar yazılır (diske yazma açıksa).</summary>
    public void Attach(string directory, LogScrubber scrubber, bool persist, Action<string>? mirror = null)
    {
        lock (_gate)
        {
            _directory = directory;
            _scrubber = scrubber;
            _persist = persist;
            _mirror = mirror;
            if (!persist) _pending.Clear();
            else if (_pending.Count > 0) ScheduleLocked();
        }
    }

    /// <summary>"Hata günlüğü tut" anahtarı: kapalıyken diske hiçbir şey yazılmaz (bekleyenler atılır).</summary>
    public void SetPersist(bool on)
    {
        lock (_gate)
        {
            _persist = on;
            if (!on) _pending.Clear();
        }
    }

    public void Write(AppLogLevel level, string area, string message, Exception? ex)
    {
        try
        {
            LogScrubber scrubber;
            lock (_gate) scrubber = _scrubber;
            var error = ex is null ? null : scrubber.Error(ex);
            Record(level, area, message, error, null);
        }
        catch
        {
            // Günlük hiçbir zaman hata kaynağı olmamalı.
        }
    }

    /// <summary>
    /// Kayıt ve bir sayı (ör. HTTP durum kodu): istisna (ve onun iletisi) yazılmadan yalnızca sayı <c>f.code</c> olarak girer.
    /// </summary>
    public void Write(AppLogLevel level, string area, string message, int code)
    {
        try
        {
            Record(level, area, message, null,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = code.ToString(CultureInfo.InvariantCulture) });
        }
        catch
        {
        }
    }

    /// <summary>Kayıt (temizlenmiş istisnayla). Alan, ileti ve ek bilgiler de temizleyiciden geçer.</summary>
    internal void Record(AppLogLevel level, string area, string message, ScrubbedError? error, IReadOnlyDictionary<string, string>? facts)
    {
        string? mirrorLine = null;
        Action<string>? mirror;
        lock (_gate)
        {
            var now = _utcNow();
            area = Short(_scrubber.Text(area), 40);
            message = Short(_scrubber.Text(message), 200);
            if (facts is not null)
                facts = facts.Take(40).ToDictionary(p => Short(_scrubber.Text(p.Key), 40), p => Short(_scrubber.Text(p.Value), 200), StringComparer.Ordinal);
            BumpLocked(level switch { AppLogLevel.Error => "log.error", AppLogLevel.Warn => "log.warn", _ => "log.info" }, 1);
            var signature = LogEntry.SignatureOf(level, area, message, error);
            EmitExpiredRepeatsLocked(now);
            if (_repeats.TryGetValue(signature, out var repeat))
            {
                repeat.Suppressed++;
                repeat.LastUtc = now;
                BumpLocked("log.suppressed", 1);
                return;
            }
            var entry = new LogEntry(now, level, area, message, error, signature, 1, Session, Version, facts);
            _repeats[signature] = new Repeat(entry, now);
            _ring.Enqueue(entry);
            while (_ring.Count > _limits.RingSize) _ring.Dequeue();
            EnqueueLocked(entry, rateLimited: true, now);
            mirror = _mirror;
            if (mirror is not null)
                mirrorLine = $"AppLog {entry.LevelChar} {area}: {message}" + (error is null ? "" : $" [{error.Type} {error.HResult}]");
        }
        if (mirrorLine is not null)
        {
            try { mirror!(mirrorLine); }
            catch { }
        }
    }

    private static string Short(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>Bellekteki sayaç (her zaman açık, diske yazılmaz; tanılama sayfası ve paket gösterir).</summary>
    public void Count(string name, long delta = 1)
    {
        lock (_gate) BumpLocked(name, delta);
    }

    private void BumpLocked(string name, long delta) =>
        _counters[name] = _counters.TryGetValue(name, out var value) ? value + delta : delta;

    public IReadOnlyDictionary<string, long> Counters
    {
        get { lock (_gate) return new SortedDictionary<string, long>(_counters, StringComparer.Ordinal); }
    }

    /// <summary>Son kayıtlar (eskiden yeniye, en çok <paramref name="max"/>); temizlenmiştir.</summary>
    public IReadOnlyList<LogEntry> Recent(int max = 256)
    {
        lock (_gate)
        {
            var all = _ring.ToArray();
            return max >= all.Length ? all : all[^max..];
        }
    }

    /// <summary>Penceresi dolan tekrarlar: bastırılan sayı ayrı bir satırla (n = sayı) yazılır.</summary>
    private void EmitExpiredRepeatsLocked(DateTime now)
    {
        List<string>? expired = null;
        foreach (var (signature, repeat) in _repeats)
        {
            if (now - repeat.WindowStart < _limits.DedupeWindow) continue;
            (expired ??= []).Add(signature);
            if (repeat.Suppressed > 0)
                EnqueueLocked(repeat.First with { Utc = repeat.LastUtc, Count = repeat.Suppressed }, rateLimited: false, now);
        }
        if (expired is null) return;
        foreach (var signature in expired) _repeats.Remove(signature);
    }

    private void EmitAllRepeatsLocked(DateTime now)
    {
        foreach (var repeat in _repeats.Values)
        {
            if (repeat.Suppressed == 0) continue;
            EnqueueLocked(repeat.First with { Utc = repeat.LastUtc, Count = repeat.Suppressed }, rateLimited: false, now);
            repeat.Suppressed = 0;
        }
    }

    private void EnqueueLocked(LogEntry entry, bool rateLimited, DateTime now)
    {
        if (!_persist) return;
        if (rateLimited)
        {
            if (now - _minuteStart >= TimeSpan.FromMinutes(1) || now < _minuteStart)
            {
                _minuteStart = now;
                _minuteLines = 0;
            }
            if (_minuteLines >= _limits.LinesPerMinute)
            {
                BumpLocked("log.ratelimited", 1);
                return;
            }
            _minuteLines++;
        }
        var cap = _directory is null ? _limits.RingSize : MaxPending;
        if (_pending.Count >= cap)
        {
            _pending.RemoveAt(0);
            BumpLocked("log.dropped", 1);
        }
        _pending.Add(entry);
        if (_directory is not null) ScheduleLocked();
    }

    private void ScheduleLocked()
    {
        if (!_background || _scheduled) return;
        _scheduled = true;
        BackgroundIo.Run(AppInfo.Name + " log", Drain, _limits.BatchDelay, onError: _ =>
        {
            lock (_gate) _scheduled = false;
        });
    }

    private void Drain()
    {
        while (true)
        {
            List<LogEntry> batch;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    _scheduled = false;
                    return;
                }
                batch = [.. _pending];
                _pending.Clear();
            }
            lock (_fileGate) WriteBatch(batch);
        }
    }

    /// <summary>
    /// Bekleyen kayıtları (ve bastırılan tekrar sayılarını) ÇAĞIRAN iş parçacığında hemen yazar: çıkış, oturum sonu ve çökme
    /// için. Arka plandaki yazma o an sürüyorsa en çok <paramref name="timeout"/> bekler; yazamazsa false.
    /// </summary>
    public bool FlushSync(TimeSpan timeout)
    {
        var fileLocked = false;
        try
        {
            Monitor.TryEnter(_fileGate, timeout, ref fileLocked);
            if (!fileLocked) return false;
            List<LogEntry> batch;
            var gateLocked = false;
            try
            {
                Monitor.TryEnter(_gate, timeout, ref gateLocked);
                if (!gateLocked) return false;
                // Klasör henüz bağlanmadıysa (Start'tan önce) bekleyenler Start'ta yazılsın.
                if (_directory is null) return true;
                EmitAllRepeatsLocked(_utcNow());
                batch = [.. _pending];
                _pending.Clear();
            }
            finally
            {
                if (gateLocked) Monitor.Exit(_gate);
            }
            WriteBatch(batch);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (fileLocked) Monitor.Exit(_fileGate);
        }
    }

    /// <summary>
    /// Saklama süresini ve toplam boyutu hemen uygular (açılıştaki arka plan bakımı için; o gün hiç hata yazılmasa da eski
    /// günlükler silinsin). Çağıran iş parçacığında çalışır.
    /// </summary>
    public void PruneNow()
    {
        lock (_fileGate)
        {
            string? dir;
            lock (_gate) dir = _directory;
            if (dir is null || !System.IO.Directory.Exists(dir)) return;
            try { Prune(dir, _utcNow()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Count("log.prunefailed"); }
        }
    }

    /// <summary>"Günlükleri sil": günlük dosyalarını siler, bellekteki son kayıtları ve tekrar sayaçlarını boşaltır.</summary>
    public void DeleteLogFiles()
    {
        lock (_fileGate)
        {
            string? dir;
            lock (_gate)
            {
                dir = _directory;
                _pending.Clear();
                _ring.Clear();
                _repeats.Clear();
            }
            _currentFile = null;
            _currentDay = null;
            if (dir is null) return;
            foreach (var file in LogFiles.List(dir))
            {
                try { File.Delete(file); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Count("log.deletefailed"); }
            }
        }
    }

    /// <summary>Toplu yazma (_fileGate altında): gün ve boyuta göre dosya seçer, sınırları uygular.</summary>
    private void WriteBatch(List<LogEntry> batch)
    {
        string? dir;
        bool persist;
        lock (_gate)
        {
            dir = _directory;
            persist = _persist;
        }
        if (!persist || dir is null || batch.Count == 0) return;
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            var now = _utcNow();
            var today = LogFiles.Day(now);
            if (_prunedDay != today)
            {
                Prune(dir, now);
                _prunedDay = today;
            }
            if (_currentFile is not null && !File.Exists(_currentFile)) _currentFile = null;

            FileStream? stream = null;
            string? streamPath = null;
            try
            {
                foreach (var entry in batch)
                {
                    var bytes = entry.ToJsonLine();
                    var path = TargetFile(dir, LogFiles.Day(entry.Utc), bytes.Length);
                    if (path != streamPath)
                    {
                        stream?.Dispose();
                        stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                        streamPath = path;
                    }
                    stream!.Write(bytes);
                    _currentSize += bytes.Length;
                }
            }
            finally
            {
                stream?.Dispose();
            }
            EnforceTotal(dir, streamPath);
            Count("log.lines", batch.Count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Disk dolu, Denetimli klasör erişimi: yalnızca sayaç (bildirim döngüsü yapılmaz).
            _currentFile = null;
            _currentDay = null;
            Count("log.writefailed");
        }
    }

    private string TargetFile(string dir, string day, int length)
    {
        if (_currentFile is null || _currentDay != day)
        {
            _currentDay = day;
            (_currentIndex, _currentSize) = LastFileOf(dir, day);
            _currentFile = Path.Combine(dir, LogFiles.NameFor(day, _currentIndex));
        }
        while (_currentSize > 0 && _currentSize + length > _limits.FileBytes)
        {
            _currentIndex++;
            _currentFile = Path.Combine(dir, LogFiles.NameFor(day, _currentIndex));
            _currentSize = File.Exists(_currentFile) ? new FileInfo(_currentFile).Length : 0;
        }
        return _currentFile;
    }

    private static (int Index, long Size) LastFileOf(string dir, string day)
    {
        var last = -1;
        foreach (var file in LogFiles.List(dir))
        {
            if (LogFiles.TryParse(Path.GetFileName(file), out var d, out var index) && d == day && index > last) last = index;
        }
        if (last < 0) return (0, 0);
        var path = Path.Combine(dir, LogFiles.NameFor(day, last));
        return (last, new FileInfo(path).Length);
    }

    /// <summary>Saklama süresini geçen dosyaları siler.</summary>
    private void Prune(string dir, DateTime now)
    {
        var oldest = LogFiles.Day(now.AddDays(-(_limits.RetentionDays - 1)));
        foreach (var file in LogFiles.List(dir))
        {
            if (!LogFiles.TryParse(Path.GetFileName(file), out var day, out _) || string.CompareOrdinal(day, oldest) >= 0) continue;
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        EnforceTotal(dir, _currentFile);
    }

    /// <summary>Toplam boyut sınırı: en eski dosyalardan başlayarak siler (yeni yazılana dokunmaz).</summary>
    private void EnforceTotal(string dir, string? keep)
    {
        var files = LogFiles.List(dir).Select(f => (Path: f, Size: SafeLength(f))).ToList();
        var total = files.Sum(f => f.Size);
        foreach (var (path, size) in files)
        {
            if (total <= _limits.TotalBytes) break;
            if (string.Equals(path, keep, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                File.Delete(path);
                total -= size;
                if (string.Equals(path, _currentFile, StringComparison.OrdinalIgnoreCase)) _currentFile = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }
}

/// <summary>
/// Uygulamanın hata günlüğü (Core; UI'dan bağımsız). Kullanım: <c>AppLog.Error("alan", "sabit-ileti", ex)</c>. Alan ve ileti
/// HER ZAMAN metin sabitidir (test denetler); değişen bilgi yalnızca istisna olarak girer, o da temizlenir. Yol, dosya adı,
/// not, kural, pencere adı, tuş ve pano içeriği asla yazılmaz.
/// </summary>
public static class AppLog
{
    private static AppLogChannel? s_channel;

    /// <summary>Kanal. <see cref="Start"/>'tan önce yalnızca bellekte tutar (bekleyen kayıtlar Start'ta yazılır).</summary>
    public static AppLogChannel Channel
    {
        get
        {
            var channel = Volatile.Read(ref s_channel);
            if (channel is not null) return channel;
            LogScrubber scrubber;
            try { scrubber = new LogScrubber(ScrubContext.ForCurrentUser()); }
            catch { scrubber = LogScrubber.Plain; }
            Interlocked.CompareExchange(ref s_channel, new AppLogChannel(null, scrubber, persist: true), null);
            return s_channel!;
        }
    }

    /// <summary>Günlük klasörünü bağlar (asıl örnek, Initialize'da). <paramref name="mirror"/>: DebugLog açıksa satırların kopyası.</summary>
    public static void Start(string logsDirectory, LogScrubber scrubber, bool persist, Action<string>? mirror = null) =>
        Channel.Attach(logsDirectory, scrubber, persist, mirror);

    public static void Error(string area, string message, Exception? ex = null) => Channel.Write(AppLogLevel.Error, area, message, ex);
    public static void Warn(string area, string message, Exception? ex = null) => Channel.Write(AppLogLevel.Warn, area, message, ex);
    public static void Info(string area, string message, Exception? ex = null) => Channel.Write(AppLogLevel.Info, area, message, ex);

    /// <summary>İstisnasız, yalnızca bir sayıyla (ör. yapay zekâ isteğinin HTTP durum kodu; yanıt gövdesi ya da ileti yazılmaz).</summary>
    public static void Warn(string area, string message, int code) => Channel.Write(AppLogLevel.Warn, area, message, code);
    public static void Info(string area, string message, int code) => Channel.Write(AppLogLevel.Info, area, message, code);

    /// <summary>Oturum başı kaydı: sürüm, kanal, mimari, Windows derlemesi, dil, ekranlar, tema (yalnızca yazılır).</summary>
    public static void Session(DiagnosticEnvironment environment)
    {
        try { Channel.Record(AppLogLevel.Info, "session", "start", null, environment.ToFacts()); }
        catch { }
    }

    public static void Count(string counter, long delta = 1) => Channel.Count(counter, delta);
    public static IReadOnlyDictionary<string, long> Counters => Channel.Counters;
    public static IReadOnlyList<LogEntry> Recent(int max = 256) => Channel.Recent(max);
    public static void SetPersist(bool on) => Channel.SetPersist(on);
    public static bool FlushSync(TimeSpan timeout) => Channel.FlushSync(timeout);
    public static string? Directory => Channel.Directory;
}

/// <summary>Günlük dosyalarının adları: <c>nestdesk-YYYYMMDD.jsonl</c>, aynı gün dolunca <c>nestdesk-YYYYMMDD-1.jsonl</c>… (UTC gün).</summary>
public static class LogFiles
{
    public const string FolderName = "logs";
    public const string Prefix = "nestdesk-";
    public const string Extension = ".jsonl";

    public static string Day(DateTime utc) => utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    public static string NameFor(string day, int index) => index <= 0 ? $"{Prefix}{day}{Extension}" : $"{Prefix}{day}-{index}{Extension}";

    /// <summary>Ad bir günlük dosyasıysa günü ("yyyyMMdd") ve sırası.</summary>
    public static bool TryParse(string fileName, out string day, out int index)
    {
        day = "";
        index = 0;
        if (!fileName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) || !fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            return false;
        var core = fileName[Prefix.Length..^Extension.Length];
        var dash = core.IndexOf('-');
        var d = dash < 0 ? core : core[..dash];
        if (d.Length != 8 || !d.All(char.IsAsciiDigit)
            || !DateTime.TryParseExact(d, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return false;
        if (dash >= 0)
        {
            var rest = core[(dash + 1)..];
            if (rest.Length is 0 or > 4 || !rest.All(char.IsAsciiDigit)) return false;
            index = int.Parse(rest, CultureInfo.InvariantCulture);
        }
        day = d;
        return true;
    }

    /// <summary>Klasördeki günlük dosyaları (yalnızca bu adlar), eskiden yeniye.</summary>
    public static IReadOnlyList<string> List(string directory)
    {
        try
        {
            if (!System.IO.Directory.Exists(directory)) return [];
            return System.IO.Directory.EnumerateFiles(directory, Prefix + "*" + Extension, SearchOption.TopDirectoryOnly)
                .Select(f => (Path: f, Ok: TryParse(Path.GetFileName(f), out var day, out var index), Day: day, Index: index))
                .Where(x => x.Ok)
                .OrderBy(x => x.Day, StringComparer.Ordinal).ThenBy(x => x.Index)
                .Select(x => x.Path)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static long TotalBytes(string directory) =>
        List(directory).Sum(f =>
        {
            try { return new FileInfo(f).Length; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0L; }
        });

    /// <summary>
    /// <paramref name="sinceUtc"/> gününden bu yana yazılan kayıtlar (eskiden yeniye). En çok <paramref name="maxBytes"/> okunur
    /// (en yeni dosyalar önce seçilir); bozuk satırlar atlanır.
    /// </summary>
    public static IReadOnlyList<LogEntry> Read(string directory, DateTime sinceUtc, long maxBytes = 8 * 1024 * 1024)
    {
        var since = Day(sinceUtc);
        var files = List(directory)
            .Where(f => TryParse(Path.GetFileName(f), out var day, out _) && string.CompareOrdinal(day, since) >= 0)
            .ToList();
        var chosen = new List<string>();
        long budget = maxBytes;
        for (var i = files.Count - 1; i >= 0; i--)
        {
            long size;
            try { size = new FileInfo(files[i]).Length; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            if (size > budget) break;
            budget -= size;
            chosen.Insert(0, files[i]);
        }
        var entries = new List<LogEntry>();
        foreach (var file in chosen)
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (reader.ReadLine() is { } line)
                    if (LogEntry.Parse(line) is { } entry && entry.Utc >= sinceUtc) entries.Add(entry);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return entries;
    }
}
