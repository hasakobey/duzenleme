using Duzenleme.Core;

namespace Duzenleme.Tests;

public class TimerLogicTests
{
    private static readonly DateTime T0 = new(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);

    private static TimerState Countdown(int minutes = 10) => new() { Mode = TimerModes.Countdown, Minutes = minutes };

    [Fact]
    public void Countdown_start_pause_resume_keeps_remaining_time()
    {
        var s = Countdown(10);
        Assert.Equal(TimeSpan.FromMinutes(10), TimerLogic.Remaining(s, T0));
        Assert.False(TimerLogic.IsRunning(s));

        TimerLogic.Start(s, T0);
        Assert.True(TimerLogic.IsRunning(s));
        Assert.Equal(T0.AddMinutes(10), s.EndsUtc);
        Assert.Equal(TimeSpan.FromMinutes(7), TimerLogic.Remaining(s, T0.AddMinutes(3)));

        TimerLogic.Pause(s, T0.AddMinutes(3));
        Assert.False(TimerLogic.IsRunning(s));
        Assert.True(TimerLogic.IsPaused(s));
        // Duraklatılmışken zaman geçse de kalan süre değişmez.
        Assert.Equal(TimeSpan.FromMinutes(7), TimerLogic.Remaining(s, T0.AddHours(5)));

        TimerLogic.Start(s, T0.AddHours(5));
        Assert.Equal(T0.AddHours(5).AddMinutes(7), s.EndsUtc);
        Assert.Null(s.RemainingTicks);
    }

    [Fact]
    public void Countdown_completes_once_and_restarts_full_length()
    {
        var s = Countdown(5);
        TimerLogic.Start(s, T0);
        Assert.Null(TimerLogic.Complete(s, T0.AddMinutes(4)));

        var end = TimerLogic.Complete(s, T0.AddMinutes(5).AddSeconds(1));
        Assert.NotNull(end);
        Assert.Equal(TimerModes.Countdown, end!.Mode);
        Assert.Equal(T0.AddMinutes(5), end.EndedUtc);
        Assert.False(TimerLogic.IsRunning(s));
        Assert.Equal(T0.AddMinutes(5), s.FinishedUtc);
        // İkinci kez bitmez; yeniden başlatınca tam süre.
        Assert.Null(TimerLogic.Complete(s, T0.AddMinutes(6)));
        TimerLogic.Start(s, T0.AddMinutes(6));
        Assert.Equal(T0.AddMinutes(11), s.EndsUtc);
        Assert.Null(s.FinishedUtc);
    }

    [Fact]
    public void Expired_while_app_was_closed_is_finished_without_auto_start()
    {
        var s = new TimerState { Mode = TimerModes.Pomodoro, AutoStartNext = true };
        TimerLogic.Start(s, T0);
        // Uygulama kapalıyken 25 dakikalık odak doldu; açılışta bakılır.
        var end = TimerLogic.Complete(s, T0.AddHours(3), whileClosed: true);
        Assert.NotNull(end);
        Assert.False(end!.AutoStarted);
        Assert.False(TimerLogic.IsRunning(s));
        Assert.Equal(TimerModes.Break, s.Phase);
        Assert.Equal(T0.AddMinutes(25), s.FinishedUtc);
    }

    [Fact]
    public void Pomodoro_phase_sequence_has_long_break_after_four_rounds()
    {
        var s = new TimerState { Mode = TimerModes.Pomodoro, FocusMinutes = 25, BreakMinutes = 5, LongBreakMinutes = 15, RoundsBeforeLong = 4 };
        var phases = new List<string> { s.Phase };
        var now = T0;
        for (var i = 0; i < 8; i++)
        {
            TimerLogic.Start(s, now);
            now = s.EndsUtc!.Value;
            var end = TimerLogic.Complete(s, now);
            Assert.NotNull(end);
            phases.Add(s.Phase);
        }
        Assert.Equal(new[]
        {
            TimerModes.Focus, TimerModes.Break, TimerModes.Focus, TimerModes.Break, TimerModes.Focus, TimerModes.Break,
            TimerModes.Focus, TimerModes.LongBreak, TimerModes.Focus,
        }, phases);
        Assert.Equal(1, s.Round);
    }

    [Fact]
    public void Pomodoro_phase_lengths_follow_settings()
    {
        var s = new TimerState { Mode = TimerModes.Pomodoro, FocusMinutes = 50, BreakMinutes = 10, LongBreakMinutes = 30, RoundsBeforeLong = 2 };
        Assert.Equal(TimeSpan.FromMinutes(50), TimerLogic.PhaseLength(s));
        s.Phase = TimerModes.Break;
        Assert.Equal(TimeSpan.FromMinutes(10), TimerLogic.PhaseLength(s));
        s.Phase = TimerModes.LongBreak;
        Assert.Equal(TimeSpan.FromMinutes(30), TimerLogic.PhaseLength(s));
    }

    [Fact]
    public void Auto_start_continues_from_the_end_moment()
    {
        var s = new TimerState { Mode = TimerModes.Pomodoro, AutoStartNext = true };
        TimerLogic.Start(s, T0);
        var end = TimerLogic.Complete(s, T0.AddMinutes(25).AddSeconds(2));
        Assert.True(end!.AutoStarted);
        Assert.True(TimerLogic.IsRunning(s));
        // Mola bitişten itibaren sayar (tik gecikmesi süreyi uzatmaz).
        Assert.Equal(T0.AddMinutes(30), s.EndsUtc);
        Assert.Null(s.FinishedUtc);
    }

    [Fact]
    public void Add_minute_while_running_paused_and_after_finish()
    {
        var s = Countdown(10);
        TimerLogic.Start(s, T0);
        TimerLogic.AddMinute(s, T0.AddMinutes(2));
        Assert.Equal(T0.AddMinutes(11), s.EndsUtc);

        TimerLogic.Pause(s, T0.AddMinutes(3));
        TimerLogic.AddMinute(s, T0.AddMinutes(3));
        Assert.Equal(TimeSpan.FromMinutes(9), TimerLogic.Remaining(s, T0.AddMinutes(30)));

        // Süre dolduktan sonra "+1 dk": bir dakikalık yeni süre başlar.
        var fresh = Countdown(1);
        TimerLogic.Start(fresh, T0);
        TimerLogic.Complete(fresh, T0.AddMinutes(2));
        TimerLogic.AddMinute(fresh, T0.AddMinutes(3));
        Assert.True(TimerLogic.IsRunning(fresh));
        Assert.Equal(T0.AddMinutes(4), fresh.EndsUtc);
    }

    [Fact]
    public void Stopwatch_elapsed_survives_pause()
    {
        var s = new TimerState { Mode = TimerModes.Stopwatch };
        TimerLogic.Start(s, T0);
        Assert.Equal(TimeSpan.FromSeconds(90), TimerLogic.Elapsed(s, T0.AddSeconds(90)));
        TimerLogic.Pause(s, T0.AddSeconds(90));
        Assert.Equal(TimeSpan.FromSeconds(90), TimerLogic.Elapsed(s, T0.AddHours(1)));
        TimerLogic.Start(s, T0.AddHours(1));
        Assert.Equal(TimeSpan.FromSeconds(100), TimerLogic.Elapsed(s, T0.AddHours(1).AddSeconds(10)));
        // Kronometre "bitmez" ve dakika eklenmez.
        Assert.Null(TimerLogic.Complete(s, T0.AddDays(1)));
        TimerLogic.AddMinute(s, T0);
        Assert.Equal(0, TimerLogic.Progress(s, T0));
        TimerLogic.Reset(s);
        Assert.Equal(TimeSpan.Zero, TimerLogic.Elapsed(s, T0.AddDays(2)));
    }

    [Fact]
    public void Progress_and_format()
    {
        var s = Countdown(10);
        TimerLogic.Start(s, T0);
        Assert.Equal(0.25, TimerLogic.Progress(s, T0.AddMinutes(2.5)), 3);
        Assert.Equal("10:00", TimerLogic.Format(TimeSpan.FromMinutes(10), roundUp: true));
        Assert.Equal("09:59", TimerLogic.Format(TimeSpan.FromSeconds(598.2), roundUp: true));
        Assert.Equal("00:01", TimerLogic.Format(TimeSpan.FromMilliseconds(200), roundUp: true));
        Assert.Equal("00:00", TimerLogic.Format(TimeSpan.FromMilliseconds(900), roundUp: false));
        Assert.Equal("1:05:09", TimerLogic.Format(new TimeSpan(1, 5, 9), roundUp: false));
    }

    [Fact]
    public void Unknown_mode_and_minutes_out_of_range_are_safe()
    {
        var s = new TimerState { Mode = "gelecekteki-kip", Minutes = 0 };
        Assert.Equal(TimerModes.Countdown, TimerLogic.Mode(s));
        Assert.Equal(TimeSpan.FromMinutes(TimerLogic.MinMinutes), TimerLogic.PhaseLength(s));
        TimerLogic.SetMinutes(s, 100000);
        Assert.Equal(TimerLogic.MaxMinutes, s.Minutes);
    }

    [Fact]
    public void Duplicate_starts_fresh_but_keeps_settings()
    {
        var s = new TimerState { Mode = TimerModes.Pomodoro, FocusMinutes = 45, Notify = false };
        TimerLogic.Start(s, T0);
        s.Round = 3;
        var copy = TimerLogic.Fresh(s);
        Assert.False(TimerLogic.IsRunning(copy));
        Assert.Equal(1, copy.Round);
        Assert.Equal(45, copy.FocusMinutes);
        Assert.False(copy.Notify);
        Assert.True(TimerLogic.IsRunning(s));
    }
}
