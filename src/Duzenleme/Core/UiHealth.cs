using System.Text;

namespace Duzenleme.Core;

/// <summary>Zamanlayıcı tıkında yapılacak iş (<see cref="StallTracker.Tick"/>).</summary>
public enum StallTick
{
    /// <summary>Bir şey yapılmaz (yoklama bekliyor, sistem uykuda/kilitli ya da donma zaten bildirildi).</summary>
    None,
    /// <summary>Arayüz iş parçacığına yeni bir yoklama gönderilsin.</summary>
    Post,
    /// <summary>Bekleyen yoklama <see cref="StallTracker.HangMs"/>'den uzun süredir çalışmadı: donma başladı (bir kez).</summary>
    HangStarted,
}

/// <summary>Yoklama arayüz iş parçacığında çalıştı: ne kadar geç (ms), takılma mıydı, bildirilmiş bir donma bitti mi.</summary>
public readonly record struct StallProbe(long DelayMs, bool Stalled, bool Recovered);

/// <summary>
/// Arayüz nöbetçisinin kararları (saf; <c>UiWatchdog</c> zamanlayıcı ve iş dağıtıcıyla bağlar). Önceki yoklama çalışmadan
/// yenisi gönderilmez; 1 sn'den geç çalışan yoklama "takılma", 5 sn'den uzun çalışmayan "donma"dır. Sistem uykuda ya da
/// kilitliyken (<c>WidgetTicker.SystemActive</c>) sayılmaz; zamanlayıcının kendisi geç kaldıysa (bütün süreç durmuştu: uyku,
/// hata ayıklayıcı) bekleyen yoklamanın süresi baştan başlar. Donma, arayüz toparlanınca biter. İş parçacığı güvenli değildir.
/// </summary>
public sealed class StallTracker
{
    public const long ProbeIntervalMs = 2000;
    public const long StallMs = 1000;
    public const long HangMs = 5000;
    /// <summary>İki tık arası bundan uzunsa süreç bütünüyle durmuştu (uyku, hata ayıklayıcı): arayüzün suçu değil.</summary>
    public const long ProcessPauseMs = 3 * ProbeIntervalMs;

    private long? _probeSince;
    private long _lastTick = long.MinValue;

    /// <summary>Donma bildirildi ve arayüz henüz toparlanmadı.</summary>
    public bool Hung { get; private set; }

    /// <summary>Bir yoklama gönderildi ve henüz çalışmadı.</summary>
    public bool ProbePending => _probeSince is not null;

    public StallTick Tick(long nowMs, bool systemActive)
    {
        var paused = _lastTick != long.MinValue && nowMs - _lastTick > ProcessPauseMs;
        _lastTick = nowMs;
        if (_probeSince is { } since)
        {
            if (!systemActive || paused)
            {
                // Uyku/kilit ya da süreç durması: bekleme süresi arayüzün yanıtsızlığı sayılmaz.
                _probeSince = nowMs;
                return StallTick.None;
            }
            if (Hung || nowMs - since <= HangMs) return StallTick.None;
            Hung = true;
            return StallTick.HangStarted;
        }
        if (!systemActive) return StallTick.None;
        _probeSince = nowMs;
        return StallTick.Post;
    }

    /// <summary>Yoklama arayüz iş parçacığında çalıştı.</summary>
    public StallProbe Ran(long nowMs)
    {
        var delay = _probeSince is { } since ? Math.Max(0, nowMs - since) : 0;
        _probeSince = null;
        var recovered = Hung;
        Hung = false;
        return new StallProbe(delay, delay > StallMs, recovered);
    }
}

/// <summary>Bir arayüz hatası için ne yapılacağı (<see cref="ErrorNoticeLimiter.Note"/>).</summary>
public readonly record struct ErrorNoticeDecision(bool Notify, bool Storm);

/// <summary>
/// İşlenmeyen arayüz hatalarının bildirimi (saf): aynı imza oturumda bir kez, toplam en çok <see cref="MaxPerSession"/> şerit;
/// <see cref="StormWindowMs"/> içinde <see cref="StormCount"/>'tan çok hata olursa bir kez "hata fırtınası" (günlüğe tek Warn).
/// İş parçacığı güvenli değildir (arayüz iş parçacığında kullanılır).
/// </summary>
public sealed class ErrorNoticeLimiter
{
    public const int MaxPerSession = 3;
    public const int StormCount = 20;
    public const long StormWindowMs = 60_000;

    private readonly HashSet<string> _notified = new(StringComparer.Ordinal);
    private readonly Queue<long> _recent = new();
    private bool _stormReported;

    public ErrorNoticeDecision Note(string signature, long nowMs)
    {
        _recent.Enqueue(nowMs);
        while (_recent.Count > 0 && nowMs - _recent.Peek() > StormWindowMs) _recent.Dequeue();
        var storm = false;
        if (!_stormReported && _recent.Count > StormCount)
        {
            _stormReported = true;
            storm = true;
        }
        var notify = _notified.Count < MaxPerSession && _notified.Add(signature);
        return new ErrorNoticeDecision(notify, storm);
    }
}

/// <summary>"Ayrıntıları kopyala" metni: sürüm, oturum, hata türü, HResult, temizlenmiş ileti ve yığın kareleri (yalnızca temizlenmiş veri).</summary>
public static class ErrorDetails
{
    public static string Text(ScrubbedError error, string version, string session)
    {
        var sb = new StringBuilder();
        sb.Append(AppInfo.Name).Append(' ').Append(version).Append(" · ").Append(session).Append('\n');
        Append(sb, error, 0);
        return sb.ToString().TrimEnd();
    }

    private static void Append(StringBuilder sb, ScrubbedError error, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.Append(indent).Append(error.Type).Append(' ').Append(error.HResult);
        if (error.ParamName is { } param) sb.Append(" (").Append(param).Append(')');
        if (error.Message is { Length: > 0 } message) sb.Append(": ").Append(message);
        sb.Append('\n');
        foreach (var frame in error.Frames.Take(LogScrubber.MaxFrames)) sb.Append(indent).Append("  at ").Append(frame).Append('\n');
        if (depth >= LogScrubber.MaxInner) return;
        foreach (var inner in error.Inner.Take(LogScrubber.MaxInner)) Append(sb, inner, depth + 1);
    }
}
