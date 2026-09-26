using System.Collections.Concurrent;
using System.IO;
using Duzenleme.Core;
using Duzenleme.Icons;

namespace Duzenleme.Desktop;

/// <summary>
/// Masaüstünde yeni oluşturulan klasörleri fark eder. Explorer önce "Yeni klasör" oluşturup sonra yeniden adlandırdığı için
/// ad kesinleşene kadar bekler, sonra simge önerisi için haber verir.
/// </summary>
public sealed class NewFolderWatcher : IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(4);

    /// <summary>Windows'un farklı dillerdeki varsayılan "Yeni klasör" adları (henüz adı verilmemiş klasör için öneri yapılmaz).</summary>
    private static readonly string[] DefaultNames =
    [
        "yeni klasör", "yeni klasor", "new folder", "neuer ordner", "nouveau dossier", "nueva carpeta", "nuova cartella",
        "nieuwe map", "nowy folder", "nova pasta", "novo pasta", "новая папка", "nová složka", "új mappa", "ny mapp", "ny mappe",
        "uusi kansio", "νέος φάκελος", "yeni_klasör", "新建文件夹", "新しいフォルダー", "새 폴더", "مجلد جديد", "תיקייה חדשה",
    ];

    private readonly ResilientWatcher _watcher;
    private readonly ConcurrentDictionary<string, Timer> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string> _onNewFolder;

    public NewFolderWatcher(string desktop, Action<string> onNewFolder)
    {
        _onNewFolder = onNewFolder;
        _watcher = new ResilientWatcher(desktop, NotifyFilters.DirectoryName, path =>
        {
            // Yeniden adlandırmada eski ad artık yok; zamanlayıcısı kendiliğinden boşa düşer.
            if (Directory.Exists(path)) Schedule(path);
        });
        _watcher.Start();
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
        try
        {
            if (!Directory.Exists(path)) return;
            var name = Path.GetFileName(path).ToLowerInvariant();
            // Henüz adı verilmemiş "Yeni klasör (2)" gibi klasörler için öneri yapma.
            if (DefaultNames.Any(d => name.StartsWith(d, StringComparison.Ordinal))) return;
            if (FolderIconService.HasCustomIcon(path)) return;
            _onNewFolder(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        _watcher.Dispose();
        foreach (var key in _pending.Keys.ToList()) Cancel(key);
    }
}
