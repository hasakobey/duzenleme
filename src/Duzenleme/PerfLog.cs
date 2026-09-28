using System.Collections.Concurrent;
using System.IO;

namespace Duzenleme;

/// <summary>
/// Başarım günlüğü (tools/perf betiği için): yalnızca DUZENLEME_PERF_LOG ortam değişkeni bir dosya yolu gösteriyorsa
/// yazar. Ayar kayıtları, bölme güncellemeleri ve açılış adımları sayılır/süreleri yazılır. Kapalıyken maliyeti yoktur.
/// </summary>
public static class PerfLog
{
    private static readonly string? Path = Environment.GetEnvironmentVariable("DUZENLEME_PERF_LOG");
    private static readonly object Lock = new();
    private static readonly ConcurrentDictionary<string, long> Counters = new();

    public static bool Enabled => Path is not null;

    public static void Write(string message)
    {
        if (Path is null) return;
        lock (Lock)
        {
            try { File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}"); }
            catch (IOException) { }
        }
    }

    /// <summary>Sayacı artırır (yalnızca açıkken); <see cref="Summary"/> ile yazılır.</summary>
    public static void Count(string name, long by = 1)
    {
        if (Path is null) return;
        Counters.AddOrUpdate(name, by, (_, v) => v + by);
    }

    /// <summary>Sayaçların o anki değerleri ("ad=değer ..."), kapalıyken boş.</summary>
    public static string Summary() =>
        Path is null ? "" : string.Join(" ", Counters.OrderBy(c => c.Key).Select(c => $"{c.Key}={c.Value}"));
}
