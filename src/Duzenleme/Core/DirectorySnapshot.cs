using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Klasördeki bir öğenin anlık kaydı. Klasör okunurken bir kez alınır (numaralandırma verisinden; ek disk erişimi yok);
/// bölmeler simge ve sıralamayı bununla yapar, diske yeniden bakmaz.
/// </summary>
public sealed record DirEntry(string Path, string Name, bool IsDirectory, FileAttributes Attributes, DateTime LastWriteUtc, long Length)
{
    /// <summary>
    /// Kayıtta tutulan öznitelikler: gösterimi etkileyenler (gizli, sistem, klasör, bağlantı noktası) ve önizleme kararındaki
    /// bulut yer tutucu bitleri. Arşiv ve OneDrive'ın "sabitlendi" bitleri gibi sık değişenler tutulmaz (boşuna yenileme olmasın).
    /// </summary>
    public const FileAttributes KeptAttributes = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory |
        FileAttributes.ReparsePoint | FileAttributes.Offline | (FileAttributes)0x40000 | (FileAttributes)0x400000;

    public bool IsHiddenOrSystem => (Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;

    public static DirEntry From(FileSystemInfo info) =>
        new(info.FullName, info.Name, info is DirectoryInfo, info.Attributes & KeptAttributes, info.LastWriteTimeUtc,
            info is FileInfo file ? file.Length : 0);
}

/// <summary>Anlık görüntünün durumu.</summary>
public enum SnapshotState
{
    /// <summary>İlk okuma henüz bitmedi.</summary>
    Pending,

    /// <summary>Okundu; <see cref="DirectorySnapshot.Entries"/> güncel.</summary>
    Ready,

    /// <summary>Klasör yok (silinmiş, taşınmış, sürücü takılı değil).</summary>
    Missing,

    /// <summary>Klasör var ama okunamıyor (erişim yok, ağ kesik).</summary>
    Unreadable,
}

/// <summary>
/// Bir klasörün (masaüstü, Genel Masaüstü ya da bir klasör bölmesinin klasörü) paylaşılan, sürekli güncel içerik listesi.
/// Tek izleyiciyle izlenir (yalnızca ad, klasör adı ve öznitelik değişiklikleri; yazma/boyut olayları değil). Olaylar
/// izleyicinin iş parçacığında birleştirilir (<see cref="IdleDelay"/> sessizlik, en fazla <see cref="MaxDelay"/> bekleme),
/// klasör arka planda bir kez yeniden okunur ve yalnızca bir şey değiştiyse <see cref="Changed"/> sahibin iş parçacığında
/// bir kez tetiklenir. Yarım indirmelerin (".crdownload" vb.) oluşma ve ad değişiklikleri yeniden okuma tetiklemez.
/// </summary>
public sealed class DirectorySnapshot : IDisposable
{
    public static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(1500);

    private readonly object _lock = new();
    private readonly Action<Action> _post;
    private readonly Timer _timer;
    private readonly ResilientWatcher _watcher;
    private IReadOnlyList<DirEntry> _entries = [];
    private SnapshotState _state = SnapshotState.Pending;
    private long _firstRequest = -1;
    private bool _requested;
    private bool _scanning;
    private bool _disposed;
    private long _version;
    private long _scans;

    /// <param name="directory">İzlenecek klasör.</param>
    /// <param name="post"><see cref="Changed"/>'i sahibin (arayüz) iş parçacığında çalıştırır.</param>
    public DirectorySnapshot(string directory, Action<Action> post)
    {
        Directory = Path.TrimEndingDirectorySeparator(directory);
        _post = post;
        _timer = new Timer(_ => Scan(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = ResilientWatcher.ForEvents(Directory, NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes,
            OnEvent, onOverflow: Request, onRecovered: Request);
    }

    public string Directory { get; }

    /// <summary>Son okumadaki öğeler (değişmez liste; her değişiklikte yenisi atanır). Herhangi bir iş parçacığından okunabilir.</summary>
    public IReadOnlyList<DirEntry> Entries => Volatile.Read(ref _entries);

    public SnapshotState State
    {
        get { lock (_lock) return _state; }
    }

    /// <summary>Her değişiklikte artar (tüketiciler eski sonuçları ayırt edebilsin).</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>Klasör kaç kez okundu (ölçüm ve testler için).</summary>
    public long ScanCount => Interlocked.Read(ref _scans);

    /// <summary>İçerik ya da durum değişti (sahibin iş parçacığında, birleştirilmiş olarak).</summary>
    public event Action? Changed;

    /// <summary>
    /// İzlemeyi başlatır ve ilk okumayı yapar; ikisi de arka planda (2.1 P6: klasör portalı ağdaki ya da takılı olmayan bir
    /// sürücüdeki klasörü gösterebilir: izleyiciyi kurmak — klasör var mı bakmak — orada saniyelerce sürebilir). İlk okuma
    /// izleyici kurulduktan sonra yapılır: "hazır" görünen klasördeki sonraki değişiklik kaçmaz.
    /// </summary>
    public void Start()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            _watcher.Start();
            lock (_lock)
            {
                if (_disposed) return;
                _requested = true;
                _timer.Change(0, Timeout.Infinite);
            }
        });
    }

    /// <summary>Klasörün yeniden okunmasını ister (birleştirilir; ör. "Yenile" ya da uygulamanın kendi yaptığı değişiklik).</summary>
    public void Request()
    {
        lock (_lock)
        {
            if (_disposed) return;
            var now = Environment.TickCount64;
            if (_firstRequest < 0) _firstRequest = now;
            _requested = true;
            if (_scanning) return; // okuma bitince yeniden okunur
            ArmLocked(now);
        }
    }

    private void ArmLocked(long now)
    {
        var maxWait = _firstRequest + (long)MaxDelay.TotalMilliseconds - now;
        var due = Math.Max(0, Math.Min((long)IdleDelay.TotalMilliseconds, maxWait));
        _timer.Change(due, Timeout.Infinite);
    }

    /// <summary>
    /// Uygulamanın kendisinin az önce oluşturduğu öğeyi okumayı beklemeden listeye ekler (ör. "Klasörü oluştur" sonrası
    /// bölme bir an "klasör yok" demesin); ardından klasör yeniden okunup gerçek kayıtla eşitlenir.
    /// </summary>
    public void NoteCreated(string path, bool isDirectory)
    {
        var added = false;
        lock (_lock)
        {
            if (_disposed || _state != SnapshotState.Ready) return;
            var current = Volatile.Read(ref _entries);
            if (!current.Any(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                var entry = new DirEntry(path, Path.GetFileName(path), isDirectory,
                    isDirectory ? FileAttributes.Directory : FileAttributes.Normal, DateTime.UtcNow, 0);
                Volatile.Write(ref _entries, current.Append(entry).ToList());
                Interlocked.Increment(ref _version);
                added = true;
            }
        }
        if (added) RaiseChanged();
        Request();
    }

    /// <summary>Yarım indirmenin oluşması, öznitelik değişmesi ya da başka bir yarım ada dönmesi görünümü değiştirmez.</summary>
    internal static bool IsNoise(FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Deleted) return false; // silinen yarım dosya listeden de kalkmalı
        if (!DesktopItems.IsPartialDownload(e.Name)) return false;
        return e is not RenamedEventArgs renamed || DesktopItems.IsPartialDownload(renamed.OldName);
    }

    private void OnEvent(FileSystemEventArgs e)
    {
        if (IsNoise(e)) return;
        Request();
    }

    private void Scan()
    {
        lock (_lock)
        {
            if (_scanning || _disposed || !_requested) return;
            _scanning = true;
            _requested = false;
            _firstRequest = -1;
        }
        var retryLater = false;
        try
        {
            var (entries, state) = Read(Directory);
            Interlocked.Increment(ref _scans);
            Publish(entries, state);
            // Okunamayan klasör (ağ kesintisi) kendiliğinden olay göndermeyebilir: bir süre sonra yeniden bakılır.
            retryLater = state == SnapshotState.Unreadable;
        }
        catch (Exception ex)
        {
            // Zamanlayıcı iş parçacığındaki beklenmedik hata süreci düşürmesin.
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            lock (_lock)
            {
                _scanning = false;
                if (!_disposed)
                {
                    if (_requested) ArmLocked(Environment.TickCount64);
                    else if (retryLater)
                    {
                        _requested = true;
                        _timer.Change(UnreadableRetry, Timeout.InfiniteTimeSpan);
                    }
                }
            }
        }
    }

    private static readonly TimeSpan UnreadableRetry = TimeSpan.FromSeconds(10);

    /// <summary>Klasörü bir kez okur. Diske yalnızca burada, arka planda dokunulur.</summary>
    internal static (List<DirEntry> Entries, SnapshotState State) Read(string directory)
    {
        try
        {
            if (!System.IO.Directory.Exists(directory)) return ([], SnapshotState.Missing);
            var list = new List<DirEntry>();
            foreach (var info in new DirectoryInfo(directory).EnumerateFileSystemInfos()) list.Add(DirEntry.From(info));
            return (list, SnapshotState.Ready);
        }
        catch (DirectoryNotFoundException) { return ([], SnapshotState.Missing); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return ([], SnapshotState.Unreadable);
        }
    }

    private void Publish(List<DirEntry> entries, SnapshotState state)
    {
        lock (_lock)
        {
            if (_disposed) return;
            // Okunamayan (ağ kesik) klasörde son bilinen liste korunmaz: bölme "okunamıyor" der, eski öğeleri açtırmaz.
            if (state == _state && SameEntries(Volatile.Read(ref _entries), entries)) return;
            _state = state;
            Volatile.Write(ref _entries, entries);
            Interlocked.Increment(ref _version);
        }
        RaiseChanged();
    }

    private void RaiseChanged() => _post(() =>
    {
        if (!_disposed) Changed?.Invoke();
    });

    /// <summary>İki okuma aynı mı? Sıra önemsenmez (numaralandırma sırası garanti değil).</summary>
    internal static bool SameEntries(IReadOnlyList<DirEntry> a, IReadOnlyList<DirEntry> b)
    {
        if (a.Count != b.Count) return false;
        var map = new Dictionary<string, DirEntry>(a.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in a) map[entry.Path] = entry;
        foreach (var entry in b)
            if (!map.TryGetValue(entry.Path, out var old) || old != entry) return false;
        return true;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _timer.Dispose();
        }
        _watcher.Dispose();
    }
}

/// <summary>
/// Paylaşılan anlık görüntüler: aynı klasörü gösteren bütün bölmeler tek izleyici ve tek okuma kullanır. Referans
/// sayılır; son kullanan bırakınca izleme durur.
/// </summary>
public sealed class DirectorySnapshots : IDisposable
{
    private readonly Action<Action> _post;
    private readonly Dictionary<string, (DirectorySnapshot Snapshot, int Refs)> _open = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public DirectorySnapshots(Action<Action> post) => _post = post;

    private static string KeyOf(string directory)
    {
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Path.TrimEndingDirectorySeparator(directory);
        }
    }

    /// <summary>Klasörün anlık görüntüsünü verir (yoksa kurup izlemeye başlar). Her çağrı bir <see cref="Release"/> ister.</summary>
    public DirectorySnapshot Acquire(string directory)
    {
        var key = KeyOf(directory);
        lock (_lock)
        {
            if (_open.TryGetValue(key, out var existing))
            {
                _open[key] = (existing.Snapshot, existing.Refs + 1);
                return existing.Snapshot;
            }
            var snapshot = new DirectorySnapshot(key, _post);
            _open[key] = (snapshot, 1);
            snapshot.Start();
            return snapshot;
        }
    }

    public void Release(DirectorySnapshot snapshot)
    {
        var key = KeyOf(snapshot.Directory);
        lock (_lock)
        {
            if (!_open.TryGetValue(key, out var entry) || !ReferenceEquals(entry.Snapshot, snapshot)) return;
            if (entry.Refs > 1)
            {
                _open[key] = (snapshot, entry.Refs - 1);
                return;
            }
            _open.Remove(key);
        }
        snapshot.Dispose();
    }

    /// <summary>İzlenen klasörse anlık görüntüsü (sayaç artmaz); değilse null.</summary>
    public DirectorySnapshot? Find(string directory)
    {
        lock (_lock) return _open.TryGetValue(KeyOf(directory), out var entry) ? entry.Snapshot : null;
    }

    public void Dispose()
    {
        List<DirectorySnapshot> all;
        lock (_lock)
        {
            all = _open.Values.Select(v => v.Snapshot).ToList();
            _open.Clear();
        }
        foreach (var snapshot in all) snapshot.Dispose();
    }
}
