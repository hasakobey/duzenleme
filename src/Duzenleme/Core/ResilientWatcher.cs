using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Kendini toparlayan FileSystemWatcher. Ağ/OneDrive klasörü geçici olarak kaybolursa ya da izleyici hata verip
/// durursa artan aralıklarla yeniden kurulur. Olay tamponu taşarsa <c>onOverflow</c> çağrılır (tam tarama için).
/// Hata olayları iş parçacığı havuzunda gelir; oradaki istisnalar süreci çökertmesin diye her adım korunur.
/// </summary>
public sealed class ResilientWatcher : IDisposable
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

    private readonly string _path;
    private readonly NotifyFilters _filter;
    private readonly Action<FileSystemEventArgs> _onChange;
    private readonly Action? _onOverflow;
    private readonly Action? _onRecovered;
    private readonly object _lock = new();
    private readonly Func<string, bool> _directoryExists;
    private FileSystemWatcher? _watcher;
    private Timer? _retry;
    private Timer? _stable;
    private int _failures;
    private bool _disposed;

    public string Path => _path;

    /// <summary>İzleyici şu anda çalışıyor mu?</summary>
    public bool IsRunning
    {
        get { lock (_lock) return _watcher?.EnableRaisingEvents == true; }
    }

    public ResilientWatcher(string path, NotifyFilters filter, Action<string> onChange, Action? onOverflow = null, Action? onRecovered = null)
        : this(path, filter, (FileSystemEventArgs e) => onChange(e.FullPath), onOverflow, onRecovered, rich: true)
    {
    }

    /// <summary>Olayın türünü (oluşturma, silme, yeniden adlandırmada eski ad) de isteyenler için.</summary>
    public static ResilientWatcher ForEvents(string path, NotifyFilters filter, Action<FileSystemEventArgs> onChange,
        Action? onOverflow = null, Action? onRecovered = null) =>
        new(path, filter, onChange, onOverflow, onRecovered, rich: true);

    // "rich" yalnızca iki kurucuyu ayırır: ortak kurucu Action<string> alan genel kurucuyla karışmasın (lambda belirsizliği).
    private ResilientWatcher(string path, NotifyFilters filter, Action<FileSystemEventArgs> onChange, Action? onOverflow, Action? onRecovered, bool rich,
        Func<string, bool>? directoryExists = null)
    {
        _ = rich;
        _path = path;
        _filter = filter;
        _onChange = onChange;
        _onOverflow = onOverflow;
        _onRecovered = onRecovered;
        _directoryExists = directoryExists ?? Directory.Exists;
    }

    /// <summary>Testler için: klasör var mı sorusu yerine verilen işlev (ör. ulaşılamayan ağ yolunda saniyelerce bekleyen).</summary>
    internal static ResilientWatcher ForTests(string path, Func<string, bool> directoryExists, Action<FileSystemEventArgs>? onChange = null) =>
        new(path, NotifyFilters.FileName, onChange ?? (_ => { }), null, null, rich: true, directoryExists);

    public void Start() => TryStart();

    /// <summary>
    /// İzleyiciyi (yeniden) kurar. Klasöre bakmak ve izleyiciyi açmak kilidin DIŞINDA yapılır: ulaşılamayan bir ağ klasöründe
    /// (\\sunucu\paylaşım, VPN kapalı) bunlar SMB zaman aşımına dek (~40 sn) bekler; kilit tutulsaydı arayüzden gelen
    /// <see cref="Dispose"/> (portal kaldırıldı, klasör değişti, çıkış) de o kadar beklerdi. Kilit yalnızca durumu okumak ve
    /// kurulan izleyiciyi yerine koymak için alınır; bu arada kapatıldıysa yeni izleyici atılır.
    /// </summary>
    private void TryStart()
    {
        FileSystemWatcher? old;
        lock (_lock)
        {
            if (_disposed) return;
            old = _watcher;
            _watcher = null;
        }
        DisposeQuietly(old);

        FileSystemWatcher? w = null;
        try
        {
            if (!_directoryExists(_path)) throw new DirectoryNotFoundException(_path);
            w = new FileSystemWatcher(_path) { IncludeSubdirectories = false, NotifyFilter = _filter, InternalBufferSize = 64 * 1024 };
            var created = w;
            w.Created += (_, e) => Safe(() => _onChange(e));
            w.Changed += (_, e) => Safe(() => _onChange(e));
            w.Deleted += (_, e) => Safe(() => _onChange(e));
            w.Renamed += (_, e) => Safe(() => _onChange(e));
            w.Error += (_, e) => OnError(created, e.GetException());
            w.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or DirectoryNotFoundException)
        {
            DisposeQuietly(w);
            lock (_lock) ScheduleRetry();
            return;
        }

        FileSystemWatcher? discard = null;
        lock (_lock)
        {
            // Bu arada kapatıldı ya da (üst üste gelen bir yeniden deneme) başka bir izleyici kuruldu: bu fazladır.
            if (_disposed || _watcher is not null) discard = w;
            else
            {
                _watcher = w;
                if (_failures > 0)
                {
                    if (_onRecovered is not null) Safe(_onRecovered);
                    // Sayaç hemen sıfırlanmaz: izleyici hemen yine düşerse bekleme süresi büyümeye devam etsin
                    // (aksi halde her 2 saniyede bir yeniden kurulup tam tarama tetiklenir).
                    _stable?.Dispose();
                    _stable = new Timer(_ => { lock (_lock) if (_watcher == w) _failures = 0; }, null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
                }
            }
        }
        DisposeQuietly(discard);
    }

    private void OnError(FileSystemWatcher source, Exception? error)
    {
        if (error is InternalBufferOverflowException)
        {
            // İzleyici hâlâ çalışıyor; yalnızca bazı olaylar kaçtı.
            if (_onOverflow is not null) Safe(_onOverflow);
            return;
        }
        lock (_lock)
        {
            if (_watcher != source) return; // yerine yenisi kurulmuş eski izleyicinin geç gelen hatası
            ScheduleRetry();
        }
    }

    private void ScheduleRetry()
    {
        if (_disposed) return;
        var delay = Backoff[Math.Min(_failures, Backoff.Length - 1)];
        _failures++;
        _retry?.Dispose();
        _retry = new Timer(_ => TryStart(), null, delay, Timeout.InfiniteTimeSpan);
    }

    private static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    /// <summary>
    /// İzlemeyi bırakır. Kurulmakta olan bir izleyiciyi beklemez (o, kurulunca kendini atar); izleyicinin kapatılması kilit
    /// dışında yapılır. Arayüz iş parçacığından güvenle çağrılabilir.
    /// </summary>
    public void Dispose()
    {
        FileSystemWatcher? watcher;
        Timer? retry, stable;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            (watcher, retry, stable) = (_watcher, _retry, _stable);
            (_watcher, _retry, _stable) = (null, null, null);
        }
        retry?.Dispose();
        stable?.Dispose();
        DisposeQuietly(watcher);
    }

    private static void DisposeQuietly(FileSystemWatcher? watcher)
    {
        if (watcher is null) return;
        try { watcher.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }
}
