using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;

namespace Duzenleme.Core;

/// <summary>Bir yolun diskteki durumu.</summary>
public readonly record struct PathState(bool Exists, bool IsDirectory, FileAttributes Attributes)
{
    public static readonly PathState Absent = new(false, false, 0);
}

/// <summary>
/// Kısayol kutusundaki gibi rastgele yolların var olup olmadığını arayüzü bekletmeden öğrenir. Her bakış arka plandadır;
/// ağ yollarında (\\sunucu\paylaşım, eşlenmiş sürücü) önce sunucuya ulaşılıyor mu diye bir kez bakılır ve bir süre
/// sınırı uygulanır: kapalı bir NAS için Windows'un bir dosyayı sorması 40 saniyeyi bulabilir; o sürede ne widget'lar
/// ne de başka öğelerin denetimi bekler. Sonuçlar bir süre önbellekte tutulur (sekme değiştirince yeniden bakılmaz).
/// </summary>
public sealed class PathProbe
{
    public static PathProbe Shared { get; } = new();

    private readonly Func<string, PathState> _stat;
    private readonly Func<string, bool> _rootExists;
    private readonly Func<string, bool> _isNetworkDrive;
    private readonly ConcurrentDictionary<string, (PathState State, long At)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, (Task<bool> Reachable, long At)> _roots = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ağ yolu için en fazla bu kadar beklenir; aşılırsa sunucu bir süre "ulaşılamıyor" sayılır.</summary>
    public TimeSpan NetworkTimeout { get; init; } = TimeSpan.FromSeconds(2.5);

    /// <summary>Sonuç (ve sunucunun ulaşılabilirliği) bu süre boyunca yeniden sorulmaz.</summary>
    public TimeSpan CacheFor { get; init; } = TimeSpan.FromSeconds(60);

    public PathProbe() : this(StatDisk, RootExists, IsNetworkDriveLetter) { }

    /// <param name="stat">Yolun durumu (arka planda çağrılır, uzun sürebilir).</param>
    /// <param name="rootExists">Ağ kökü (\\sunucu\paylaşım ya da "Z:\") ulaşılabilir mi?</param>
    /// <param name="isNetworkDrive">"Z:\" gibi bir sürücü kökü ağ sürücüsü mü?</param>
    public PathProbe(Func<string, PathState> stat, Func<string, bool> rootExists, Func<string, bool> isNetworkDrive)
    {
        _stat = stat;
        _rootExists = rootExists;
        _isNetworkDrive = isNetworkDrive;
    }

    /// <summary>Önbellekte taze bir sonuç varsa (diske dokunmadan) döner.</summary>
    public bool TryGetCached(string path, out PathState state)
    {
        if (_cache.TryGetValue(path, out var hit) && Environment.TickCount64 - hit.At < (long)CacheFor.TotalMilliseconds)
        {
            state = hit.State;
            return true;
        }
        state = default;
        return false;
    }

    /// <summary>Önbellekteki sonucu unutur (ör. öğe az önce eklendi ya da silindi).</summary>
    public void Forget(string path) => _cache.TryRemove(path, out _);

    /// <summary>
    /// Yolun durumunu arka planda öğrenir. null: bilinemedi (ağ sunucusu yanıt vermiyor ya da süre doldu); çağıran bunu
    /// "şu an ulaşılamıyor" olarak gösterebilir.
    /// </summary>
    public Task<PathState?> CheckAsync(string path)
    {
        if (TryGetCached(path, out var cached)) return Task.FromResult<PathState?>(cached);
        return Task.Run(() => CheckCoreAsync(path));
    }

    private async Task<PathState?> CheckCoreAsync(string path)
    {
        var root = NetworkRoot(path);
        if (root is null) return Remember(path, _stat(path));

        if (!await Reachable(root).ConfigureAwait(false)) return null;
        var stat = Blocking(() => _stat(path));
        if (await Task.WhenAny(stat, Task.Delay(NetworkTimeout)).ConfigureAwait(false) != stat)
        {
            // Sunucu yanıt veriyordu ama bu yol takıldı: bir süre bu kökteki öğeler yeniden sorulmasın.
            _roots[root] = (Task.FromResult(false), Environment.TickCount64);
            return null;
        }
        return Remember(path, stat.Result);
    }

    private PathState Remember(string path, PathState state)
    {
        _cache[path] = (state, Environment.TickCount64);
        return state;
    }

    /// <summary>Kök (sunucu paylaşımı ya da ağ sürücüsü) ulaşılabilir mi? Kök başına tek deneme, sonucu paylaşılır.</summary>
    private Task<bool> Reachable(string root)
    {
        var now = Environment.TickCount64;
        if (_roots.TryGetValue(root, out var known) && now - known.At < (long)CacheFor.TotalMilliseconds) return known.Reachable;
        var probe = ProbeRoot(root);
        _roots[root] = (probe, now);
        return probe;
    }

    private async Task<bool> ProbeRoot(string root)
    {
        var check = Blocking(() =>
        {
            try { return _rootExists(root); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
        });
        return await Task.WhenAny(check, Task.Delay(NetworkTimeout)).ConfigureAwait(false) == check && check.Result;
    }

    /// <summary>
    /// Ağda saniyelerce takılabilen çağrı kendi iş parçacığında çalışır: iş parçacığı havuzunu tıkamaz, böylece süre
    /// dolduğunu bildiren devam (Task.Delay) havuz meşgulken de zamanında çalışır.
    /// </summary>
    private static Task<T> Blocking<T>(Func<T> work) =>
        Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>
    /// Yol ağdaysa kökü (\\sunucu\paylaşım ya da "Z:\"), değilse null. UNC yolları yalnızca metinden anlaşılır; sürücü harfi
    /// için sürücü türü sorulur (bu yüzden yalnızca arka planda çağrılır).
    /// </summary>
    internal string? NetworkRoot(string path)
    {
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal)) return null;
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            var parts = path[2..].Split('\\', 3, StringSplitOptions.None);
            return parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0 ? $@"\\{parts[0]}\{parts[1]}" : null;
        }
        if (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/')
        {
            var root = path[..2] + "\\";
            return _isNetworkDrive(root) ? root : null;
        }
        return null;
    }

    private static PathState StatDisk(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return new PathState(true, (attributes & FileAttributes.Directory) != 0, attributes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return PathState.Absent;
        }
    }

    private static bool RootExists(string root) => Directory.Exists(root);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetDriveType(string root);

    private static bool IsNetworkDriveLetter(string root)
    {
        const uint DRIVE_REMOTE = 4;
        return GetDriveType(root) == DRIVE_REMOTE;
    }
}
