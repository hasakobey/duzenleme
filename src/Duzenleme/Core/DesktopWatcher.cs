using System.Collections.Concurrent;
using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Masaüstünü izler; yeni ya da yeniden adlandırılan dosyayı, yazma işlemi bitince düzenleyiciye verir.
/// Masaüstü klasörü geçici olarak erişilemez olursa (OneDrive, ağ) izleyici kendini yeniden kurar.
/// </summary>
public sealed class DesktopWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(10);

    private readonly DesktopOrganizer _organizer;
    private readonly Func<bool> _isPaused;
    private readonly ResilientWatcher _watcher;
    private readonly ConcurrentDictionary<string, (Timer Timer, DateTime FirstSeen)> _pending = new(StringComparer.OrdinalIgnoreCase);

    public DesktopWatcher(DesktopOrganizer organizer, Func<bool> isPaused)
    {
        _organizer = organizer;
        _isPaused = isPaused;
        _watcher = new ResilientWatcher(organizer.DesktopDirectory,
            NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            Schedule,
            onOverflow: OrganizeAllInBackground,   // olay kaçtıysa masaüstünü baştan tara
            onRecovered: OrganizeAllInBackground); // izleyici yeniden kurulduysa aradaki dosyaları yakala
    }

    /// <summary>İzleme şu anda çalışıyor mu? (Masaüstü klasörü erişilemezken false.)</summary>
    public bool IsRunning => _watcher.IsRunning;

    public void Start() => _watcher.Start();

    private void OrganizeAllInBackground() =>
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { if (!_isPaused()) _organizer.OrganizeAll(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        });

    private void Schedule(string path)
    {
        if (_isPaused()) return;
        // Adından asla taşınmayacağı belli olanlar (yarım indirmeler, kısayollar, desktop.ini, "~$" geçici dosyaları)
        // için zamanlayıcı bile kurulmaz: indirme sürerken her yazma olayı ucuz kalır.
        if (RuleEngine.IsIgnored(Path.GetFileName(path), FileAttributes.Normal)) return;
        var entry = _pending.GetOrAdd(path, p => (new Timer(Fire, p, Timeout.Infinite, Timeout.Infinite), DateTime.UtcNow));
        entry.Timer.Change(Debounce, Timeout.InfiniteTimeSpan);
    }

    private void Fire(object? state)
    {
        var path = (string)state!;
        // Zamanlayıcı iş parçacığındaki bir hata tüm uygulamayı kapatmasın.
        try
        {
            if (!_pending.TryGetValue(path, out var entry)) return;

            // Önce kural: kurala uymayan (ya da geri alınmış) dosya, yazılıyor mu diye özel kilitle hiç açılmaz.
            if (_isPaused() || !_organizer.WouldMove(path))
            {
                Forget(path);
                return;
            }

            if (!FileMover.IsReady(path))
            {
                // Hâlâ yazılıyor: biraz sonra tekrar dene, çok uzarsa vazgeç.
                if (DateTime.UtcNow - entry.FirstSeen > GiveUpAfter) Forget(path);
                else entry.Timer.Change(Debounce, Timeout.InfiniteTimeSpan);
                return;
            }

            Forget(path);
            _organizer.Organize(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Forget(path);
        }
    }

    private void Forget(string path)
    {
        if (_pending.TryRemove(path, out var entry)) entry.Timer.Dispose();
    }

    public void Dispose()
    {
        _watcher.Dispose();
        foreach (var key in _pending.Keys.ToList()) Forget(key);
    }
}
