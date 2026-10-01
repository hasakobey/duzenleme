using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme;

/// <summary>
/// Arayüz nöbetçisi: iş parçacığı havuzundaki zamanlayıcı 2 sn'de bir, önceki yoklama çalıştıysa, arayüz iş parçacığına Input
/// önceliğinde boş bir iş gönderir. 1 sn'den geç çalışan yoklama yalnızca sayaçtır (<c>ui.stall</c>); 5 sn'den uzun
/// çalışmazsa günlüğe <c>ui-unresponsive</c> yazılır ve oturum nöbetçisine "donma" işlenir (süreç o hâlde kapatılırsa
/// sonraki açılış "hang" raporunu sorar). Arayüz toparlanınca işaret kalkar. Uyku ve kilit sırasında sayılmaz
/// (<see cref="WidgetTicker.SystemActive"/>). Kararlar <see cref="StallTracker"/>'da.
/// </summary>
internal sealed class UiWatchdog : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<bool> _hangChanged;
    private readonly StallTracker _tracker = new();
    private readonly object _gate = new();
    private readonly Timer _timer;
    // SystemActive arayüz iş parçacığında değişir; zamanlayıcı buradan okur.
    private volatile bool _systemActive = true;
    private bool _disposed;

    /// <param name="hangChanged">Donma başladı (true) ya da bitti (false); arka plan iş parçacığından çağrılır.</param>
    public UiWatchdog(Dispatcher dispatcher, Action<bool> hangChanged)
    {
        _dispatcher = dispatcher;
        _hangChanged = hangChanged;
        _systemActive = WidgetTicker.SystemActive;
        WidgetTicker.SystemActiveChanged += OnSystemActiveChanged;
        _timer = new Timer(_ => Tick(), null, StallTracker.ProbeIntervalMs, StallTracker.ProbeIntervalMs);
    }

    private void OnSystemActiveChanged() => _systemActive = WidgetTicker.SystemActive;

    private void Tick()
    {
        StallTick action;
        lock (_gate)
        {
            if (_disposed) return;
            action = _tracker.Tick(Environment.TickCount64, _systemActive);
        }
        switch (action)
        {
            case StallTick.Post:
                try { _dispatcher.BeginInvoke(Probe, DispatcherPriority.Input); }
                catch (InvalidOperationException) { }   // iş dağıtıcı kapanıyor
                break;
            case StallTick.HangStarted:
                AppLog.Warn("ui", "ui-unresponsive");
                AppLog.Count("ui.hang");
                Notify(true);
                break;
        }
    }

    private void Probe()
    {
        StallProbe result;
        lock (_gate)
        {
            if (_disposed) return;
            result = _tracker.Ran(Environment.TickCount64);
        }
        if (result.Stalled) AppLog.Count("ui.stall");
        if (!result.Recovered) return;
        AppLog.Info("ui", "ui-recovered");
        // Nöbetçi dosyası arayüz iş parçacığında yazılmaz.
        ThreadPool.QueueUserWorkItem(_ => Notify(false));
    }

    private void Notify(bool hung)
    {
        try { _hangChanged(hung); }
        catch (Exception ex) { AppLog.Warn("ui", "hang-marker-failed", ex); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        WidgetTicker.SystemActiveChanged -= OnSystemActiveChanged;
        _timer.Dispose();
    }
}
