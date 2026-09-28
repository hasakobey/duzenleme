namespace Duzenleme.Core;

/// <summary>
/// Sahibin (arayüz) iş parçacığında çalışan tek atımlık zamanlayıcı: uygulamada DispatcherTimer, testlerde elle
/// ilerletilen sahte. <see cref="Cancel"/> başka iş parçacığından da çağrılabilir (çökme yolu); uygulama bunu tolere eder.
/// </summary>
public interface IOwnerTimer
{
    /// <summary><paramref name="due"/> sonra bir kez çalışsın; önceki zamanlama iptal olur.</summary>
    void Schedule(TimeSpan due);

    void Cancel();
}

/// <summary>
/// Ayarların kaydedilme zamanlaması. "Durum değişti" bildirimleri (<see cref="MarkDirty"/>) diske hemen yazdırmaz: ilk
/// değişiklikten <see cref="BurstDelay"/> sonra o ana dek birikenler tek seferde yazılır (sonraki değişiklikler süreyi
/// uzatmaz; çökmede en fazla son ~0,5 saniye kaybolur). Anlık görüntü sahibin iş parçacığında alınır (ucuz), yazma
/// <see cref="DurableFile"/> ile arka planda, atomik ve yedekli yapılır.
/// <para>Sık değişen önemsiz durum (widget'lar arası sıra) <see cref="MarkDirtyLazy"/> ile bir sonraki yazmaya bırakılır
/// (en geç <see cref="LazyDelay"/> sonra). Çökmede kaybolmaması gereken bayraklar ve çıkış <see cref="FlushNow"/> kullanır.</para>
/// </summary>
public sealed class SettingsStore : IDisposable
{
    public static readonly TimeSpan BurstDelay = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan LazyDelay = TimeSpan.FromSeconds(60);

    private readonly Func<byte[]> _snapshot;
    private readonly IOwnerTimer _timer;
    private readonly Func<long> _now;
    private volatile bool _dirty;
    private volatile bool _lazyDirty;
    private long _dueAt = long.MaxValue;
    private long _snapshots;

    /// <param name="file">Yazılacak dosya.</param>
    /// <param name="snapshot">Ayarların o anki hâli (sahibin iş parçacığında çağrılır).</param>
    /// <param name="timerFactory">Verilen işi sahibin iş parçacığında çalıştıracak zamanlayıcıyı kurar.</param>
    /// <param name="clock">Milisaniye saati (testler için); varsayılan Environment.TickCount64.</param>
    public SettingsStore(DurableFile file, Func<byte[]> snapshot, Func<Action, IOwnerTimer> timerFactory, Func<long>? clock = null)
    {
        File = file;
        _snapshot = snapshot;
        _now = clock ?? (() => Environment.TickCount64);
        _timer = timerFactory(OnTimer);
    }

    public DurableFile File { get; }

    /// <summary>Diske yazılmamış değişiklik var mı?</summary>
    public bool IsDirty => _dirty || _lazyDirty;

    /// <summary>Kaç kez anlık görüntü alınıp yazmaya verildi (ölçüm ve testler için).</summary>
    public long SnapshotCount => Interlocked.Read(ref _snapshots);

    /// <summary>Durum değişti: en geç <see cref="BurstDelay"/> sonra (bu patlamadaki ilk değişiklikten sayılarak) yazılır.</summary>
    public void MarkDirty()
    {
        _dirty = true;
        Arm(BurstDelay);
    }

    /// <summary>Önemsiz, sık değişen durum: bir sonraki yazmayla, en geç <see cref="LazyDelay"/> sonra yazılır.</summary>
    public void MarkDirtyLazy()
    {
        _lazyDirty = true;
        Arm(LazyDelay);
    }

    private void Arm(TimeSpan delay)
    {
        var due = _now() + (long)delay.TotalMilliseconds;
        // Daha erken bir yazma zaten bekliyorsa süre uzatılmaz (ya da kısaltılmaz).
        if (due >= _dueAt) return;
        _dueAt = due;
        _timer.Schedule(delay);
    }

    private void OnTimer()
    {
        _dueAt = long.MaxValue;
        if (!IsDirty) return;
        if (TakeSnapshot() is { } bytes) File.Enqueue(bytes);
    }

    private byte[]? TakeSnapshot()
    {
        _dirty = false;
        _lazyDirty = false;
        try
        {
            var bytes = _snapshot();
            Interlocked.Increment(ref _snapshots);
            return bytes;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.Text.Json.JsonException)
        {
            // Anlık görüntü alınamadı (ör. çökme yolunda liste o an değişiyordu): değişiklik kaybolmasın, sonra yeniden denensin.
            _dirty = true;
            System.Diagnostics.Debug.WriteLine(ex);
            return null;
        }
    }

    /// <summary>
    /// Bekleyen değişiklikleri hemen, çağıran iş parçacığında diske yazar (çıkış, oturum kapanışı, çökme ve çökmede
    /// kaybolmaması gereken bayraklar). Süre içinde yazıldıysa true; yazılamadıysa içerik arka planda denenmeye devam eder.
    /// </summary>
    /// <param name="changed">
    /// Çağıran durumu az önce değiştirdi (ör. <c>IconsHiddenByApp</c>) ve <see cref="MarkDirty"/> demedi: yine de anlık görüntü
    /// alınıp yazılır. false iken kirli bir şey yoksa hiçbir şey yazılmaz (yalnızca sıradaki yazmalar beklenir).
    /// </param>
    public bool FlushNow(TimeSpan timeout, bool changed = false)
    {
        _timer.Cancel();
        _dueAt = long.MaxValue;
        if (changed) _dirty = true;
        if (!IsDirty) return File.Flush(timeout);
        if (TakeSnapshot() is { } bytes) return File.WriteNow(bytes, timeout);
        // Anlık görüntü alınamadı: en azından daha önce sıraya girenler insin; değişiklik bekliyor sayılır.
        File.Flush(timeout);
        return false;
    }

    public void Dispose()
    {
        _timer.Cancel();
        File.Dispose();
    }
}
