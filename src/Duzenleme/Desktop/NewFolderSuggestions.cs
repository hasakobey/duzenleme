using System.Collections.Concurrent;
using System.IO;
using Duzenleme.Core;
using Duzenleme.Icons;

namespace Duzenleme.Desktop;

/// <summary>
/// Masaüstünde yeni görülen klasörler için "simge ver" önerisi. Ayrı bir izleyici kurmaz (2.2): masaüstünün anlık
/// görüntüsünün okuma farkını (<see cref="DirectorySnapshot.DirectoriesAppeared"/>) kullanır ve yalnızca öneri açıkken
/// (<see cref="Enabled"/>) dinler. Explorer önce "Yeni klasör" oluşturup sonra yeniden adlandırdığı için ad kesinleşene
/// kadar bekler, sonra simge önerisi için haber verir.
/// </summary>
public sealed class NewFolderSuggestions : IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(4);

    /// <summary>Windows'un farklı dillerdeki varsayılan "Yeni klasör" adları (henüz adı verilmemiş klasör için öneri yapılmaz).</summary>
    private static readonly string[] DefaultNames =
    [
        "yeni klasör", "yeni klasor", "new folder", "neuer ordner", "nouveau dossier", "nueva carpeta", "nuova cartella",
        "nieuwe map", "nowy folder", "nova pasta", "novo pasta", "новая папка", "nová složka", "új mappa", "ny mapp", "ny mappe",
        "uusi kansio", "νέος φάκελος", "yeni_klasör", "新建文件夹", "新しいフォルダー", "새 폴더", "مجلد جديد", "תיקייה חדשה",
    ];

    private readonly DirectorySnapshot _snapshot;
    private readonly ConcurrentDictionary<string, Timer> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string> _onNewFolder;
    private volatile bool _enabled;
    private bool _disposed;

    /// <param name="snapshot">Masaüstünün anlık görüntüsü (uygulama açık kaldıkça izlenir).</param>
    /// <param name="onNewFolder">Adı kesinleşmiş, simgesiz yeni klasör (zamanlayıcı iş parçacığında).</param>
    public NewFolderSuggestions(DirectorySnapshot snapshot, Action<string> onNewFolder)
    {
        _snapshot = snapshot;
        _onNewFolder = onNewFolder;
    }

    /// <summary>
    /// Öneri açık mı (Ayarlar'daki "Yeni klasörlere simge öner")? Kapalıyken anlık görüntü dinlenmez ve bekleyen öneriler
    /// bırakılır. Arayüz iş parçacığından değiştirilir.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value || _disposed) return;
            _enabled = value;
            if (value) _snapshot.DirectoriesAppeared += OnAppeared;
            else
            {
                _snapshot.DirectoriesAppeared -= OnAppeared;
                CancelAll();
            }
        }
    }

    // Okumanın iş parçacığında: diske dokunmaz, yalnızca zamanlayıcı kurar.
    private void OnAppeared(IReadOnlyList<string> folders)
    {
        foreach (var folder in folders)
            if (_enabled) Schedule(folder);
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

    private void CancelAll()
    {
        foreach (var key in _pending.Keys.ToList()) Cancel(key);
    }

    private void Fire(object? state)
    {
        var path = (string)state!;
        Cancel(path);
        if (!_enabled) return;
        try
        {
            // Bu arada yeniden adlandırıldıysa ya da silindiyse eski adı için öneri yapılmaz (yeni adı ayrıca görülür).
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
        Enabled = false;
        _disposed = true;
        CancelAll();
    }
}
