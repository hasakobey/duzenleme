using System.Collections.Concurrent;
using System.IO;
using Duzenleme.Icons;

namespace Duzenleme.Desktop;

/// <summary>
/// Masaüstünde yeni oluşturulan klasörleri fark eder. Explorer önce "Yeni klasör" oluşturup sonra yeniden adlandırdığı için
/// ad kesinleşene kadar bekler, sonra simge önerisi için haber verir.
/// </summary>
public sealed class NewFolderWatcher : IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(4);
    private static readonly string[] DefaultNames = ["yeni klasör", "new folder", "yeni klasor"];

    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, Timer> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string> _onNewFolder;

    public NewFolderWatcher(string desktop, Action<string> onNewFolder)
    {
        _onNewFolder = onNewFolder;
        _watcher = new FileSystemWatcher(desktop) { NotifyFilter = NotifyFilters.DirectoryName, IncludeSubdirectories = false };
        _watcher.Created += (_, e) => Schedule(e.FullPath);
        _watcher.Renamed += (_, e) =>
        {
            Cancel(e.OldFullPath);
            Schedule(e.FullPath);
        };
        _watcher.EnableRaisingEvents = true;
    }

    private void Schedule(string path)
    {
        var timer = _pending.GetOrAdd(path, p => new Timer(Fire, p, Timeout.Infinite, Timeout.Infinite));
        timer.Change(Settle, Timeout.InfiniteTimeSpan);
    }

    private void Cancel(string path)
    {
        if (_pending.TryRemove(path, out var timer)) timer.Dispose();
    }

    private void Fire(object? state)
    {
        var path = (string)state!;
        Cancel(path);
        if (!Directory.Exists(path)) return;
        var name = Path.GetFileName(path).ToLowerInvariant();
        // Henüz adı verilmemiş "Yeni klasör (2)" gibi klasörler için öneri yapma.
        if (DefaultNames.Any(d => name.StartsWith(d, StringComparison.Ordinal))) return;
        if (FolderIconService.HasCustomIcon(path)) return;
        _onNewFolder(path);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        foreach (var key in _pending.Keys.ToList()) Cancel(key);
    }
}
