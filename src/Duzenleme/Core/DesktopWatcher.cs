using System.Collections.Concurrent;
using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Masaüstünü izler; yeni ya da yeniden adlandırılan dosyayı, yazma işlemi bitince düzenleyiciye verir.
/// </summary>
public sealed class DesktopWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(10);

    private readonly DesktopOrganizer _organizer;
    private readonly Func<bool> _isPaused;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, (Timer Timer, DateTime FirstSeen)> _pending = new(StringComparer.OrdinalIgnoreCase);

    public DesktopWatcher(DesktopOrganizer organizer, Func<bool> isPaused)
    {
        _organizer = organizer;
        _isPaused = isPaused;
        Directory.CreateDirectory(organizer.DesktopDirectory);
        _watcher = new FileSystemWatcher(organizer.DesktopDirectory)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 64 * 1024,
        };
        _watcher.Created += (_, e) => Schedule(e.FullPath);
        _watcher.Changed += (_, e) => Schedule(e.FullPath);
        _watcher.Renamed += (_, e) => Schedule(e.FullPath);
        // Olay kaçarsa (tampon taşması) masaüstünü baştan tara.
        _watcher.Error += (_, _) => ThreadPool.QueueUserWorkItem(_ => { if (!_isPaused()) _organizer.OrganizeAll(); });
    }

    public void Start() => _watcher.EnableRaisingEvents = true;

    public void Stop() => _watcher.EnableRaisingEvents = false;

    private void Schedule(string path)
    {
        if (_isPaused()) return;
        var entry = _pending.GetOrAdd(path, p => (new Timer(Fire, p, Timeout.Infinite, Timeout.Infinite), DateTime.UtcNow));
        entry.Timer.Change(Debounce, Timeout.InfiniteTimeSpan);
    }

    private void Fire(object? state)
    {
        var path = (string)state!;
        if (!_pending.TryGetValue(path, out var entry)) return;

        if (!File.Exists(path) || _isPaused())
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
