using System.Collections.Concurrent;
using System.Globalization;
using System.IO;

namespace Duzenleme;

/// <summary>
/// Başarım günlüğü (tools/perf betiği için, bkz. docs/perf.md): yalnızca NESTDESK_PERF_LOG (ya da eski DUZENLEME_PERF_LOG)
/// ortam değişkeni bir dosya yolu gösteriyorsa yazar. Ayar kayıtları, bölme güncellemeleri ve açılış adımları
/// sayılır/süreleri yazılır. Kapalıyken maliyeti yoktur (her yöntem ilk satırda döner).
/// </summary>
public static class PerfLog
{
    private static readonly string? Path = Core.AppEnvironment.Get("PERF_LOG");
    private static readonly object Lock = new();
    private static readonly ConcurrentDictionary<string, long> Counters = new();
    private static DateTime? _processStart;

    public static bool Enabled => Path is not null;

    public static void Write(string message)
    {
        if (Path is null) return;
        lock (Lock)
        {
            // Yazılamayan yol (izin, geçersiz ad) uygulamayı düşürmesin: bu günlük yalnızca ölçüm içindir.
            try { File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                           or System.Security.SecurityException or ArgumentException) { }
        }
    }

    /// <summary>
    /// Açılış işareti: "mark &lt;ad&gt; &lt;süreç başından ms&gt; ms [ayrıntı]". Ad ASCII'dir (perf-run.ps1 okur):
    /// App.resources, Settings.loaded, AppHost.initialized, Tray.created, Widget.firstRendered, RestoreAll.end, StartServices.end.
    /// Yalnızca asıl örnek yazar (yardımcı süreçler --exit, --peek, ikinci açılış tek örnek denetiminden önce işaret koymaz):
    /// zamanı daha önce alınan işaret <paramref name="atMs"/> ile (<see cref="Now"/>) sonradan yazılır.
    /// </summary>
    public static void Mark(string name, string? detail = null, double? atMs = null)
    {
        if (Path is null) return;
        var since = atMs ?? SinceProcessStart();
        Write(string.Create(CultureInfo.InvariantCulture, $"mark {name} {since:0} ms") + (detail is null ? "" : " " + detail));
    }

    /// <summary>Süreç başından bu yana geçen ms (<see cref="Mark"/> için); kapalıyken -1 ve maliyetsiz.</summary>
    public static double Now() => Path is null ? -1 : SinceProcessStart();

    /// <summary>Sayacı artırır (yalnızca açıkken); <see cref="Summary"/> ile yazılır.</summary>
    public static void Count(string name, long by = 1)
    {
        if (Path is null) return;
        Counters.AddOrUpdate(name, by, (_, v) => v + by);
    }

    /// <summary>Sayaçların o anki değerleri ve yönetilen yığın ("ad=değer ..."), kapalıyken boş.</summary>
    public static string Summary()
    {
        if (Path is null) return "";
        var counters = string.Join(" ", Counters.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => $"{c.Key}={c.Value}"));
        return counters.Length == 0 ? Gc() : counters + " " + Gc();
    }

    /// <summary>
    /// Yönetilen bellek (GC.GetGCMemoryInfo: son çöp toplamadaki yığın, ayrılmış bellek, parçalanma; toplama sayıları ve
    /// toplam duraklama), kapalıyken boş.
    /// </summary>
    public static string Gc()
    {
        if (Path is null) return "";
        var info = GC.GetGCMemoryInfo();
        return string.Create(CultureInfo.InvariantCulture,
            $"gcHeapKB={info.HeapSizeBytes / 1024} gcCommittedKB={info.TotalCommittedBytes / 1024} gcFragmentedKB={info.FragmentedBytes / 1024} managedKB={GC.GetTotalMemory(false) / 1024} gc0={GC.CollectionCount(0)} gc1={GC.CollectionCount(1)} gc2={GC.CollectionCount(2)} gcPauseMs={GC.GetTotalPauseDuration().TotalMilliseconds:0}");
    }

    private static double SinceProcessStart()
    {
        try
        {
            _processStart ??= System.Diagnostics.Process.GetCurrentProcess().StartTime;
            return (DateTime.Now - _processStart.Value).TotalMilliseconds;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            return -1;
        }
    }
}
