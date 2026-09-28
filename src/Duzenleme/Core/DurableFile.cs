using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Bir dosyanın tek yazarı: içerik önce geçici dosyaya yazılıp diske indirilir (FlushFileBuffers), sonra eskisinin yerine
/// atomik olarak geçer; önceki sürüm <c>.bak</c> olarak kalır. Güç kesilse ya da süreç öldürülse de dosya ya eski ya yeni
/// hâliyle bütün kalır.
/// <para><see cref="Enqueue"/> beklemez: yazma arka planda yapılır, arada gelen yeni içerik eskisinin yerini alır (son
/// yazılan kazanır). Virüs tarayıcısı/yedekleme dosyayı o an tutuyorsa birkaç kez yeniden denenir; hiçbir hata çağırana
/// (arayüze) fırlatılmaz, <see cref="Failed"/> ile bildirilir ve içerik bir süre sonra yeniden denenmek üzere tutulur.</para>
/// </summary>
public sealed class DurableFile : IDisposable
{
    /// <summary>Arka plandaki art arda denemelerin arası (paylaşım ihlali genelde birkaç yüz ms sürer).</summary>
    private static readonly TimeSpan[] RetryDelays =
        [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(800)];

    /// <summary>Bütün denemeler tükenince içerik atılmaz; bu aralarla yeniden denenir.</summary>
    private static readonly TimeSpan[] LaterDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(120)];

    private readonly object _gate = new();   // _pending, _version, _written, _worker
    private readonly object _io = new();     // aynı anda tek yazma
    private readonly bool _keepBackup;
    private byte[]? _pending;
    private long _pendingVersion;
    private long _version;
    private long _written;
    private bool _working;
    private int _laterIndex;
    private Timer? _later;
    private bool _failing;
    private bool _disposed;
    private long _writeCount;

    public DurableFile(string path, bool keepBackup = true)
    {
        Path = path;
        _keepBackup = keepBackup;
    }

    public string Path { get; }

    /// <summary>Önceki sürümün tutulduğu dosya.</summary>
    public string BackupPath => Path + ".bak";

    /// <summary>Diske yazılmış içerik sayısı (ölçüm ve testler için).</summary>
    public long WriteCount => Interlocked.Read(ref _writeCount);

    /// <summary>Henüz diske inmemiş içerik var mı?</summary>
    public bool HasPending
    {
        get { lock (_gate) return _pending is not null || _working; }
    }

    /// <summary>Yazma bütün denemelere rağmen başarısız oldu (arka plan iş parçacığında, bir hata dizisinde bir kez).</summary>
    public event Action<Exception>? Failed;

    /// <summary>Başarısızlıktan sonra yazma yeniden başardı (arka plan iş parçacığında).</summary>
    public event Action? Recovered;

    /// <summary>İçeriği arka planda yazdırır; hemen döner. Arada gelen daha yeni içerik bunun yerini alır.</summary>
    public void Enqueue(byte[] content)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending = content;
            _pendingVersion = ++_version;
            StartWorkerLocked();
        }
    }

    /// <summary>
    /// İçeriği çağıran iş parçacığında hemen yazar (çıkış, oturum kapanışı, çökme; ya da çökmede kaybolmaması gereken
    /// bir bayrak). Süre içinde yazılamazsa false döner ve içerik arka planda yazılmak üzere sıraya girer.
    /// </summary>
    public bool WriteNow(byte[] content, TimeSpan timeout)
    {
        long version;
        lock (_gate)
        {
            version = ++_version;
            // Sıradaki daha eski içerik artık gereksiz.
            _pending = null;
        }
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        for (var attempt = 0; ; attempt++)
        {
            var remaining = deadline - Environment.TickCount64;
            if (Monitor.TryEnter(_io, (int)Math.Max(0, remaining)))
            {
                try
                {
                    if (Volatile.Read(ref _written) >= version) return true;
                    WriteAtomic(Path, content, _keepBackup);
                    Written(version);
                    return true;
                }
                catch (Exception ex) when (IsTransient(ex))
                {
                    if (Environment.TickCount64 >= deadline) break;
                }
                finally { Monitor.Exit(_io); }
                Thread.Sleep(RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)]);
                if (Environment.TickCount64 >= deadline) break;
            }
            else break;
        }
        // Yazılamadı: kaybolmasın, arka planda denensin (daha yeni bir içerik geldiyse o yazılır).
        lock (_gate)
        {
            if (_pending is null && version > _written)
            {
                _pending = content;
                _pendingVersion = version;
            }
            StartWorkerLocked();
        }
        return false;
    }

    /// <summary>Şu ana dek sıraya giren her şey diske inene (ya da süre dolana) dek bekler. Yazıldıysa true.</summary>
    public bool Flush(TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        byte[] retry;
        lock (_gate)
        {
            var target = _version;
            while (true)
            {
                if (_written >= target) return true;
                if (!_working && _pending is not null)
                {
                    // Yazıcı "sonra yeniden dene" beklemesinde: beklemeden şimdi dene.
                    retry = _pending;
                    _pending = null;
                    break;
                }
                var remaining = deadline - Environment.TickCount64;
                if (remaining <= 0) return false;
                // Başka bir iş parçacığının WriteNow'u sürüyor olabilir: kısa aralıklarla yeniden bakılır.
                Monitor.Wait(_gate, (int)Math.Min(remaining, 50));
            }
        }
        return WriteNow(retry, TimeSpan.FromMilliseconds(Math.Max(0, deadline - Environment.TickCount64)));
    }

    private void StartWorkerLocked()
    {
        if (_working || _pending is null || _disposed) return;
        _working = true;
        _later?.Dispose();
        _later = null;
        ThreadPool.UnsafeQueueUserWorkItem(_ => WorkLoop(), null);
    }

    private void WorkLoop()
    {
        while (true)
        {
            byte[] content;
            long version;
            lock (_gate)
            {
                if (_pending is null || _disposed)
                {
                    _working = false;
                    Monitor.PulseAll(_gate);
                    return;
                }
                content = _pending;
                version = _pendingVersion;
                _pending = null;
            }

            Exception? error = null;
            for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
            {
                if (attempt > 0)
                {
                    Thread.Sleep(RetryDelays[attempt - 1]);
                    // Beklerken daha yeni içerik geldiyse onu yaz.
                    lock (_gate)
                    {
                        if (_pending is not null)
                        {
                            content = _pending;
                            version = _pendingVersion;
                            _pending = null;
                        }
                    }
                }
                lock (_io)
                {
                    try
                    {
                        if (Volatile.Read(ref _written) < version)
                        {
                            WriteAtomic(Path, content, _keepBackup);
                            Written(version);
                        }
                        error = null;
                        break;
                    }
                    catch (Exception ex) when (IsTransient(ex))
                    {
                        error = ex;
                    }
                }
            }

            if (error is null)
            {
                bool recovered;
                lock (_gate)
                {
                    recovered = _failing;
                    _failing = false;
                    _laterIndex = 0;
                }
                if (recovered) Raise(() => Recovered?.Invoke());
                continue;
            }

            // Denemeler tükendi: içerik tutulur (daha yenisi yoksa) ve bir süre sonra yeniden denenir.
            bool first;
            lock (_gate)
            {
                if (_pending is null && version > _written)
                {
                    _pending = content;
                    _pendingVersion = version;
                }
                first = !_failing;
                _failing = true;
                var delay = LaterDelays[Math.Min(_laterIndex++, LaterDelays.Length - 1)];
                _working = false;
                Monitor.PulseAll(_gate);
                if (!_disposed)
                {
                    _later?.Dispose();
                    _later = new Timer(_ => { lock (_gate) StartWorkerLocked(); }, null, delay, Timeout.InfiniteTimeSpan);
                }
            }
            if (first) Raise(() => Failed?.Invoke(error));
            return;
        }
    }

    private void Written(long version)
    {
        Interlocked.Increment(ref _writeCount);
        lock (_gate)
        {
            if (version > _written) _written = version;
            Monitor.PulseAll(_gate);
        }
    }

    private static void Raise(Action action)
    {
        try { action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    /// <summary>Kilit/paylaşım ihlali, erişim reddi (virüs tarayıcısı, yedekleme, eşitleme) gibi geçici olabilecek hatalar.</summary>
    private static bool IsTransient(Exception ex) => ex is IOException or UnauthorizedAccessException;

    /// <summary>
    /// Tek deneme: <c>.tmp</c>'ye yazar, diske indirir, hedefin yerine koyar (varsa eskisini <c>.bak</c> yapar). Hata fırlatır.
    /// </summary>
    public static void WriteAtomic(string path, byte[] content, bool keepBackup = true)
    {
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var tmp = path + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }

        if (!File.Exists(path))
        {
            File.Move(tmp, path);
            return;
        }
        if (!keepBackup)
        {
            File.Move(tmp, path, overwrite: true);
            return;
        }
        try
        {
            // Hedef ve yedek tek işlemde yer değiştirir (NTFS'te atomik).
            File.Replace(tmp, path, path + ".bak", ignoreMetadataErrors: true);
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException && !IsLocked(ex))
        {
            // Replace hedefi yedeğe taşıyıp yenisini koyamadıysa (ERROR_UNABLE_TO_MOVE_REPLACEMENT_2): yenisi yerine konur.
            if (!File.Exists(path))
            {
                File.Move(tmp, path);
                return;
            }
            // Dosya sistemi Replace'i desteklemiyor (bazı ağ paylaşımları, eski bellekler): kopyayla yedekle, üstüne yaz.
            File.Copy(path, path + ".bak", overwrite: true);
            File.Move(tmp, path, overwrite: true);
        }
    }

    /// <summary>
    /// Başka bir süreç dosyayı tutuyor: paylaşım/kilit ihlali ya da ReplaceFile'ın "eski dosya kaldırılamadı / yenisi yerine
    /// konamadı, iki dosya da adını korudu" hataları (1175, 1176). Bekleyip yeniden denemek gerekir.
    /// </summary>
    private static bool IsLocked(Exception ex) => (ex.HResult & 0xFFFF) is 32 or 33 or 1175 or 1176;

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _later?.Dispose();
            _later = null;
            Monitor.PulseAll(_gate);
        }
    }
}
