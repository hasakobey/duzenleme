using System.Collections.Concurrent;
using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Masaüstünü izler; yeni ya da yeniden adlandırılan dosyayı, yazma işlemi bitince düzenleyiciye verir.
/// Masaüstü klasörü geçici olarak erişilemez olursa (OneDrive, ağ) izleyici kendini yeniden kurar.
/// Yalnızca otomatik taşıma açıkken izler (<see cref="Resume"/> / <see cref="Pause"/>): kapalıyken (yeni kullanıcının
/// varsayılanı) indirmelerin her yazma/boyut olayı için boşuna uyanılmaz.
/// </summary>
public sealed class DesktopWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(10);

    private readonly DesktopOrganizer _organizer;
    private readonly Func<bool> _isPaused;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, (Timer Timer, DateTime FirstSeen)> _pending = new(StringComparer.OrdinalIgnoreCase);
    private ResilientWatcher? _watcher;
    private bool _disposed;
    private readonly Func<string, bool>? _directoryExists; // yalnızca testler (bkz. ForTests)

    public DesktopWatcher(DesktopOrganizer organizer, Func<bool> isPaused)
    {
        _organizer = organizer;
        _isPaused = isPaused;
    }

    /// <summary>Testler için: izleyicinin "klasör var mı" sorusu yerine verilen işlev (ör. ağ zaman aşımında bekleyen).</summary>
    internal DesktopWatcher(DesktopOrganizer organizer, Func<bool> isPaused, Func<string, bool> directoryExists)
        : this(organizer, isPaused) => _directoryExists = directoryExists;

    /// <summary>İzleme şu anda çalışıyor mu? (Duraklatılmışken ya da masaüstü klasörü erişilemezken false.)</summary>
    public bool IsRunning => Volatile.Read(ref _watcher)?.IsRunning == true;

    /// <summary>İzleme duraklatılmış mı (hiç başlatılmadı ya da <see cref="Pause"/>)?</summary>
    public bool IsPaused => Volatile.Read(ref _watcher) is null;

    /// <summary>
    /// İzlemeyi başlatır (açılışta ve otomatik taşıma açılınca). Beklemez: izleyici iş parçacığı havuzunda kurulur (ağdaki ya
    /// da OneDrive'daki masaüstünde klasöre bakmak saniyelerce sürebilir). <paramref name="afterStart"/> izleyici kurulduktan
    /// sonra havuzda çağrılır: kapalıyken gelen dosyaları yakalayan tarama izleyiciden sonra yapılsın, arada gelen dosya
    /// kaçmasın. Kurulum sürerken (ağ zaman aşımı) yeniden duraklatıldıysa çağrılmaz: kapalıyken dosya taşınmasın. Zaten
    /// izleniyorsa yalnızca <paramref name="afterStart"/> çalışır (hemen, çağıranın iş parçacığında).
    /// </summary>
    public void Resume(Action? afterStart = null)
    {
        ResilientWatcher? watcher = null;
        lock (_gate)
        {
            if (_disposed) return;
            if (_watcher is null)
            {
                const NotifyFilters filter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
                watcher = _directoryExists is null
                    ? new ResilientWatcher(_organizer.DesktopDirectory, filter, Schedule,
                        onOverflow: OrganizeAllInBackground,   // olay kaçtıysa masaüstünü baştan tara
                        onRecovered: OrganizeAllInBackground)  // izleyici yeniden kurulduysa aradaki dosyaları yakala
                    : ResilientWatcher.ForTests(_organizer.DesktopDirectory, _directoryExists, e => Schedule(e.FullPath), filter);
                Volatile.Write(ref _watcher, watcher);
            }
        }
        if (watcher is null)
        {
            afterStart?.Invoke();
            return;
        }
        watcher.StartInBackground(afterStart is null ? null : () =>
        {
            // Bu izleyici beklerken Pause (ve belki yeni bir Resume) geldiyse eski kurulumun taraması yapılmaz.
            if (Volatile.Read(ref _watcher) == watcher && !_isPaused()) afterStart();
        });
    }

    /// <summary>
    /// İzlemeyi durdurur (otomatik taşıma kapatıldı). Bekleyen dosyalar bırakılır. İzleyici arka planda kapatılır: ulaşılamayan
    /// ağ klasöründe saniyeler sürebilir, arayüz beklemesin. <see cref="Resume"/> ile yeniden başlar.
    /// </summary>
    public void Pause()
    {
        ResilientWatcher? watcher;
        lock (_gate)
        {
            watcher = _watcher;
            Volatile.Write(ref _watcher, null);
        }
        foreach (var key in _pending.Keys.ToList()) Forget(key);
        if (watcher is not null) ThreadPool.UnsafeQueueUserWorkItem(static w => w.Dispose(), watcher, preferLocal: false);
    }

    private void OrganizeAllInBackground() =>
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { if (!_isPaused()) _organizer.OrganizeAll(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        });

    private void Schedule(string path)
    {
        // Duraklatılırken kapatılmakta olan izleyicinin geç gelen olayı yok sayılır.
        if (_isPaused() || Volatile.Read(ref _watcher) is null) return;
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
        ResilientWatcher? watcher;
        lock (_gate)
        {
            _disposed = true;
            watcher = _watcher;
            Volatile.Write(ref _watcher, null);
        }
        watcher?.Dispose();
        foreach (var key in _pending.Keys.ToList()) Forget(key);
    }
}
