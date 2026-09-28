namespace Duzenleme.Core;

/// <summary>Biten bir aşama: ne bitti, sırada ne var, sonraki kendiliğinden başladı mı.</summary>
public sealed record TimerEnd(string Mode, string FinishedPhase, string? NextPhase, DateTime EndedUtc, bool AutoStarted);

/// <summary>
/// Zamanlayıcı, Pomodoro ve kronometrenin hesabı (saf; arayüzden bağımsız, testlenir). Durum <see cref="TimerState"/>'tedir
/// ve yalnızca başlat, duraklat, sıfırla ve aşama sonunda değişir: saniyede bir kayıt yapılmaz, kalan süre her an
/// <see cref="Remaining"/> ile bitiş anından hesaplanır. Zamanlar UTC'dir; saat ya da saat dilimi değişse de süre doğru kalır.
/// </summary>
public static class TimerLogic
{
    public const int MinMinutes = 1, MaxMinutes = 24 * 60;

    private static int Clamp(int minutes) => Math.Clamp(minutes, MinMinutes, MaxMinutes);

    public static string Mode(TimerState s) => TimerModes.Normalize(s.Mode);

    public static bool IsRunning(TimerState s) => Mode(s) == TimerModes.Stopwatch ? s.StartedUtc is not null : s.EndsUtc is not null;

    /// <summary>Çalışmıyor ama başlangıçtaki hâlinde de değil (duraklatılmış).</summary>
    public static bool IsPaused(TimerState s) => !IsRunning(s) &&
        (Mode(s) == TimerModes.Stopwatch ? s.ElapsedTicks > 0 : s.RemainingTicks is not null);

    /// <summary>Geçerli aşamanın tam süresi (kronometrede sıfır).</summary>
    public static TimeSpan PhaseLength(TimerState s) => Mode(s) switch
    {
        TimerModes.Pomodoro => TimeSpan.FromMinutes(Clamp(TimerModes.NormalizePhase(s.Phase) switch
        {
            TimerModes.Break => s.BreakMinutes,
            TimerModes.LongBreak => s.LongBreakMinutes,
            _ => s.FocusMinutes,
        })),
        TimerModes.Countdown => TimeSpan.FromMinutes(Clamp(s.Minutes)),
        _ => TimeSpan.Zero,
    };

    /// <summary>Kalan süre (zamanlayıcı, Pomodoro): çalışırken bitiş anından, duraklatılmışken saklanandan.</summary>
    public static TimeSpan Remaining(TimerState s, DateTime nowUtc)
    {
        if (s.EndsUtc is { } ends)
        {
            var left = ends - nowUtc;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
        return s.RemainingTicks is { } ticks ? TimeSpan.FromTicks(Math.Max(0, ticks)) : PhaseLength(s);
    }

    /// <summary>Kronometrenin geçen süresi (duraklatmalar düşülmüş).</summary>
    public static TimeSpan Elapsed(TimerState s, DateTime nowUtc)
    {
        var running = s.StartedUtc is { } started && nowUtc > started ? nowUtc - started : TimeSpan.Zero;
        return TimeSpan.FromTicks(Math.Max(0, s.ElapsedTicks)) + running;
    }

    /// <summary>İlerleme 0–1 (zamanlayıcı, Pomodoro: geçen / tam süre). Kronometrede 0.</summary>
    public static double Progress(TimerState s, DateTime nowUtc)
    {
        var length = PhaseLength(s);
        if (length <= TimeSpan.Zero) return 0;
        return Math.Clamp(1 - Remaining(s, nowUtc).TotalSeconds / length.TotalSeconds, 0, 1);
    }

    public static void Start(TimerState s, DateTime nowUtc)
    {
        if (IsRunning(s)) return;
        s.FinishedUtc = null;
        if (Mode(s) == TimerModes.Stopwatch)
        {
            s.StartedUtc = nowUtc;
            return;
        }
        var remaining = s.RemainingTicks is { } ticks && ticks > 0 ? TimeSpan.FromTicks(ticks) : PhaseLength(s);
        s.EndsUtc = nowUtc + remaining;
        s.RemainingTicks = null;
    }

    public static void Pause(TimerState s, DateTime nowUtc)
    {
        if (!IsRunning(s)) return;
        if (Mode(s) == TimerModes.Stopwatch)
        {
            s.ElapsedTicks = Elapsed(s, nowUtc).Ticks;
            s.StartedUtc = null;
            return;
        }
        s.RemainingTicks = Remaining(s, nowUtc).Ticks;
        s.EndsUtc = null;
    }

    /// <summary>Başa döner: zamanlayıcı tam süresine, Pomodoro ilk odak turuna, kronometre sıfıra (durur).</summary>
    public static void Reset(TimerState s)
    {
        s.EndsUtc = null;
        s.RemainingTicks = null;
        s.StartedUtc = null;
        s.ElapsedTicks = 0;
        s.FinishedUtc = null;
        s.Round = 1;
        s.Phase = TimerModes.Focus;
    }

    /// <summary>
    /// Zamanlayıcıya bir dakika ekler (çalışırken de, duraklatılmışken de). Süre az önce dolduysa bir dakikalık yeni
    /// süre başlar ("biraz daha").
    /// </summary>
    public static void AddMinute(TimerState s, DateTime nowUtc)
    {
        if (Mode(s) == TimerModes.Stopwatch) return;
        var minute = TimeSpan.FromMinutes(1);
        if (s.EndsUtc is { } ends)
        {
            s.EndsUtc = (ends > nowUtc ? ends : nowUtc) + minute;
            return;
        }
        if (s.FinishedUtc is not null && s.RemainingTicks is null)
        {
            s.RemainingTicks = minute.Ticks;
            Start(s, nowUtc);
            return;
        }
        s.RemainingTicks = (Remaining(s, nowUtc) + minute).Ticks;
    }

    /// <summary>
    /// Çalışan aşama bittiyse durumu ilerletir ve bitişi döner; bitmediyse null. Pomodoro sırası: odak, mola, odak, mola…
    /// <see cref="TimerState.RoundsBeforeLong"/>. odaktan sonra uzun mola, sonra yeniden ilk tur.
    /// <paramref name="whileClosed"/>: süre uygulama kapalıyken doldu (açılışta bakılıyor): sonraki aşama kendiliğinden
    /// başlamaz; kullanıcı "Süre doldu" görür.
    /// </summary>
    public static TimerEnd? Complete(TimerState s, DateTime nowUtc, bool whileClosed = false)
    {
        var mode = Mode(s);
        if (mode == TimerModes.Stopwatch || s.EndsUtc is not { } ends || ends > nowUtc) return null;
        s.EndsUtc = null;
        s.RemainingTicks = null;
        s.FinishedUtc = ends;
        if (mode == TimerModes.Countdown) return new TimerEnd(mode, TimerModes.Countdown, null, ends, AutoStarted: false);

        var finished = TimerModes.NormalizePhase(s.Phase);
        var rounds = Math.Max(1, s.RoundsBeforeLong);
        switch (finished)
        {
            case TimerModes.Focus:
                s.Phase = s.Round >= rounds ? TimerModes.LongBreak : TimerModes.Break;
                break;
            case TimerModes.LongBreak:
                s.Phase = TimerModes.Focus;
                s.Round = 1;
                break;
            default:
                s.Phase = TimerModes.Focus;
                s.Round = Math.Min(s.Round + 1, rounds);
                break;
        }
        // Sonraki aşama bitişten itibaren sayar (kesintisiz); o da çoktan geçtiyse (bilgisayar uykudaydı) başlamaz.
        var autoStarted = false;
        if (s.AutoStartNext && !whileClosed && ends + PhaseLength(s) > nowUtc)
        {
            s.EndsUtc = ends + PhaseLength(s);
            s.FinishedUtc = null;
            autoStarted = true;
        }
        return new TimerEnd(mode, finished, s.Phase, ends, autoStarted);
    }

    /// <summary>Kipi değiştirir; durum baştan başlar (süre ayarları korunur).</summary>
    public static void SetMode(TimerState s, string mode)
    {
        s.Mode = TimerModes.Normalize(mode);
        Reset(s);
    }

    /// <summary>Süreyi (dakika) değiştirir; çalışmıyorsa yeni süre hemen görünür, çalışıyorsa bir sonraki başlatmada.</summary>
    public static void SetMinutes(TimerState s, int minutes)
    {
        s.Minutes = Clamp(minutes);
        if (!IsRunning(s)) s.RemainingTicks = null;
    }

    /// <summary>Çoğaltılan zamanlayıcı baştan başlar (kopyası da aynı anda çalmasın).</summary>
    public static TimerState Fresh(TimerState s)
    {
        var copy = new TimerState
        {
            Mode = s.Mode, Minutes = s.Minutes, FocusMinutes = s.FocusMinutes, BreakMinutes = s.BreakMinutes,
            LongBreakMinutes = s.LongBreakMinutes, RoundsBeforeLong = s.RoundsBeforeLong, AutoStartNext = s.AutoStartNext,
            Notify = s.Notify, Extra = s.Extra,
        };
        Reset(copy);
        return copy;
    }

    /// <summary>"09:58", bir saati geçince "1:05:09". Kalan süre yukarı yuvarlanır (son saniyede "00:01" görünür).</summary>
    public static string Format(TimeSpan span, bool roundUp)
    {
        var seconds = roundUp ? (long)Math.Ceiling(span.TotalSeconds - 1e-7) : (long)Math.Floor(span.TotalSeconds);
        seconds = Math.Max(0, seconds);
        var hours = seconds / 3600;
        var minutes = seconds / 60 % 60;
        var secs = seconds % 60;
        return hours > 0
            ? $"{hours}:{minutes:00}:{secs:00}"
            : $"{minutes:00}:{secs:00}";
    }
}
