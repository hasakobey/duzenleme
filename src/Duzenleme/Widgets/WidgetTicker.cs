using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Duzenleme.Widgets;

/// <summary>
/// Saat benzeri widget'ların ortak zamanlayıcısı: tek bir DispatcherTimer, yalnızca gereken ilk sınıra (saniye, dakika ya da
/// gece yarısı) kurulur ve abonesi yokken hiç uyanmaz. N saat/tarih/takvim widget'ı dakikada bir kez, aynı karede güncellenir
/// (katmanlı pencerede her çizim işlemci ister). Saat ayarı, saat dilimi değişimi, uykudan uyanma ve oturum kilidi de burada,
/// bir kez dinlenir. Görünür olmayan widget abone olmaz (bkz. <see cref="IWidgetView.SetLive"/>). Yalnızca arayüz iş parçacığından.
/// </summary>
public static class WidgetTicker
{
    private static readonly TimeSpan Slack = TimeSpan.FromMilliseconds(15);

    private static DispatcherTimer? _timer;
    private static Action? _second, _minute, _day;
    private static long _lastMinute = DateTime.Now.Ticks / TimeSpan.TicksPerMinute;
    private static DateTime _lastDate = DateTime.Today;
    private static bool _systemHooked, _locked, _suspended;

    /// <summary>Her saniye başında (saniyeli saat, çalışan zamanlayıcı).</summary>
    public static event Action? SecondTick
    {
        add { _second += value; Reschedule(); }
        remove { _second -= value; Reschedule(); }
    }

    /// <summary>Her dakika başında; saat ayarı değişince ya da uykudan uyanınca da hemen.</summary>
    public static event Action? MinuteTick
    {
        add { _minute += value; Reschedule(); }
        remove { _minute -= value; Reschedule(); }
    }

    /// <summary>Gün değişince (gece yarısı, saat ayarı, uykudan uyanma).</summary>
    public static event Action? DayChanged
    {
        add { _day += value; Reschedule(); }
        remove { _day -= value; Reschedule(); }
    }

    /// <summary>Kullanıcı ekranı görebilir mi: oturum kilitli değil ve bilgisayar uykuda değil.</summary>
    public static bool SystemActive => !_locked && !_suspended;

    /// <summary><see cref="SystemActive"/> değişti (arayüz iş parçacığında).</summary>
    public static event Action? SystemActiveChanged
    {
        add { EnsureSystemEvents(); _activeChanged += value; }
        remove { _activeChanged -= value; }
    }

    private static Action? _activeChanged;

    private static void Reschedule()
    {
        EnsureSystemEvents();
        var now = DateTime.Now;
        TimeSpan? due = _second is not null ? UntilNext(now, TimeSpan.TicksPerSecond)
            : _minute is not null ? UntilNext(now, TimeSpan.TicksPerMinute)
            : _day is not null ? now.Date.AddDays(1) - now + Slack
            : null;
        if (due is not { } wait)
        {
            _timer?.Stop();
            return;
        }
        if (_timer is null)
        {
            // Render önceliği: yoğun anda da saat dakikanın başında güncellenir.
            _timer = new DispatcherTimer(DispatcherPriority.Render, Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
            _timer.Tick += (_, _) => Tick();
        }
        _timer.Stop();
        _timer.Interval = wait;
        _timer.Start();
    }

    private static TimeSpan UntilNext(DateTime now, long unit) => TimeSpan.FromTicks(unit - now.Ticks % unit) + Slack;

    private static void Tick()
    {
        var now = DateTime.Now;
        _second?.Invoke();
        var minute = now.Ticks / TimeSpan.TicksPerMinute;
        if (minute != _lastMinute)
        {
            _lastMinute = minute;
            _minute?.Invoke();
        }
        if (now.Date != _lastDate)
        {
            _lastDate = now.Date;
            _day?.Invoke();
        }
        Reschedule();
    }

    /// <summary>Saat değişti ya da uykudan uyanıldı: beklemeden herkes güncellensin, zamanlayıcı yeni saate kurulsun.</summary>
    private static void Resync()
    {
        TimeZoneInfo.ClearCachedData();
        _lastMinute = -1;
        _lastDate = DateTime.MinValue;
        Tick();
    }

    private static void EnsureSystemEvents()
    {
        if (_systemHooked) return;
        _systemHooked = true;
        SystemEvents.TimeChanged += (_, _) => OnUi(Resync);
        SystemEvents.PowerModeChanged += (_, e) => OnUi(() =>
        {
            if (e.Mode == PowerModes.Suspend) SetState(locked: _locked, suspended: true);
            else if (e.Mode == PowerModes.Resume)
            {
                SetState(locked: _locked, suspended: false);
                Resync();
            }
        });
        SystemEvents.SessionSwitch += (_, e) => OnUi(() =>
        {
            if (e.Reason == SessionSwitchReason.SessionLock) SetState(locked: true, suspended: _suspended);
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                SetState(locked: false, suspended: _suspended);
                Resync();
            }
        });
    }

    private static void SetState(bool locked, bool suspended)
    {
        var before = SystemActive;
        _locked = locked;
        _suspended = suspended;
        if (before != SystemActive) _activeChanged?.Invoke();
    }

    /// <summary>SystemEvents kendi iş parçacığında da tetikleyebilir: iş arayüz iş parçacığına aktarılır.</summary>
    private static void OnUi(Action action) => Application.Current?.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
}
