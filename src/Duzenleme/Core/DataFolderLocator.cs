using System.IO;

namespace Duzenleme.Core;

/// <summary>Veri klasörünün nereden geldiği.</summary>
public enum DataFolderSource
{
    /// <summary>--data ile verilen klasör.</summary>
    Override,
    /// <summary>Taşınabilir kullanım: exe'nin yanındaki "data".</summary>
    Portable,
    /// <summary>%AppData%\NestDesk (yeni kullanıcı ya da zaten taşınmış).</summary>
    Current,
    /// <summary>%AppData%\Duzenleme bu açılışta %AppData%\NestDesk olarak yeniden adlandırıldı.</summary>
    Moved,
    /// <summary>%AppData%\Duzenleme yerinde kullanılıyor (Store sürümü, bağlantı klasörü ya da iki klasör birden dolu).</summary>
    Legacy,
    /// <summary>Taşıma denendi ama olmadı (klasör açık/kilitli); bu oturum eski klasörle çalışır, sonraki açılış yeniden dener.</summary>
    MoveFailed,
}

/// <summary>Hangi klasörün kullanılacağı; <see cref="MoveFrom"/> doluysa önce o klasör <see cref="Directory"/>'ye taşınır.</summary>
public sealed record DataFolderPlan(string Directory, DataFolderSource Source, string? MoveFrom = null);

/// <summary>
/// Veri klasörü (ayarlar, günlük, yapay zekâ simgeleri). 2.1'de %AppData%\Duzenleme → %AppData%\NestDesk:
/// paketsiz sürüm eski klasörü açılışta, tek örnek kilidini tutarken yeniden adlandırır (aynı birimde tek adımlık işlem;
/// kopyalayıp silmez). Olmazsa o oturum eski klasörle çalışır. Store (MSIX) sürümü paket dışındaki klasörü taşımaz:
/// kurulum sürümünden kalan ayarları yerinde okur, yoksa yeni klasörü kullanır.
/// </summary>
public static class DataFolderLocator
{
    public const string SettingsFile = "settings.json";

    /// <summary>Yeni ve eski klasörün %AppData% (Roaming) altındaki yolları.</summary>
    public static (string Current, string Legacy) Paths(string roamingAppData) =>
        (Path.Combine(roamingAppData, AppInfo.DataFolderName), Path.Combine(roamingAppData, AppInfo.LegacyDataFolderName));

    /// <summary>Planı gerçek dosya sistemine bakarak çıkarır (diske yazmaz).</summary>
    public static DataFolderPlan Plan(string? dataOverride, string? portableDirectory, bool isPackaged, string roamingAppData) =>
        Plan(dataOverride, portableDirectory, isPackaged, roamingAppData, File.Exists, Directory.Exists, IsReparsePoint, IsEmptyDirectory);

    /// <summary>
    /// Kural sırası: --data, taşınabilir klasör, ayarları olan yeni klasör, eski klasör yoksa yeni klasör. Eski klasör varsa:
    /// Store sürümünde (ayarları varsa) yerinde; bağlantı (junction/symlink) ise yerinde; yeni klasör boş değilse iki klasör
    /// birleştirilmez (eski ayarlar varsa eski yerinde, yoksa yeni); aksi hâlde taşınır.
    /// </summary>
    public static DataFolderPlan Plan(string? dataOverride, string? portableDirectory, bool isPackaged, string roamingAppData,
        Func<string, bool> fileExists, Func<string, bool> directoryExists, Func<string, bool> isReparsePoint, Func<string, bool> isEmptyDirectory)
    {
        if (dataOverride is not null) return new(dataOverride, DataFolderSource.Override);
        if (portableDirectory is not null) return new(portableDirectory, DataFolderSource.Portable);

        var (current, legacy) = Paths(roamingAppData);
        if (fileExists(Path.Combine(current, SettingsFile)) || !directoryExists(legacy)) return new(current, DataFolderSource.Current);
        var legacyHasSettings = fileExists(Path.Combine(legacy, SettingsFile));
        if (isPackaged) return legacyHasSettings ? new(legacy, DataFolderSource.Legacy) : new(current, DataFolderSource.Current);
        if (isReparsePoint(legacy)) return new(legacy, DataFolderSource.Legacy);
        if (directoryExists(current) && !isEmptyDirectory(current))
            return legacyHasSettings ? new(legacy, DataFolderSource.Legacy) : new(current, DataFolderSource.Current);
        return new(current, DataFolderSource.Moved, legacy);
    }

    /// <summary>
    /// Planı uygular: gerekiyorsa boş yeni klasörü silip eski klasörü yeni adına taşır. Taşıma olmazsa (Gezgin penceresi
    /// içeride açık, virüs tarayıcısı dosyayı tutuyor…) eski klasörle devam edilir; hata fırlatmaz.
    /// </summary>
    public static DataFolderPlan Apply(DataFolderPlan plan, Action<string, string>? move = null, Action<string>? log = null)
    {
        if (plan.MoveFrom is not { } from) return plan;
        try
        {
            if (Directory.Exists(plan.Directory)) Directory.Delete(plan.Directory);   // yalnızca boşsa (Plan denetledi)
            (move ?? Directory.Move)(from, plan.Directory);
            log?.Invoke($"veri klasörü taşındı: {from} → {plan.Directory}"); // l10n: çevrilmez (günlük)
            return plan with { MoveFrom = null };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"veri klasörü taşınamadı, bu oturum eski klasörü kullanıyor: {ex.GetType().Name} {ex.Message}"); // l10n: çevrilmez (günlük)
            return new(from, DataFolderSource.MoveFailed);
        }
    }

    /// <summary>
    /// Masaüstünü geri açma kipinde (kaldırma, --restore-desktop) okunacak ayar dosyaları, öncelik sırasıyla. Hiçbir şey
    /// taşınmaz ve oluşturulmaz.
    /// </summary>
    public static IReadOnlyList<string> SettingsCandidates(string? dataOverride, string? portableDirectory, string roamingAppData)
    {
        if (dataOverride is not null) return [Path.Combine(dataOverride, SettingsFile)];
        var (current, legacy) = Paths(roamingAppData);
        List<string> files = [];
        if (portableDirectory is not null) files.Add(Path.Combine(portableDirectory, SettingsFile));
        files.Add(Path.Combine(current, SettingsFile));
        files.Add(Path.Combine(legacy, SettingsFile));
        return files;
    }

    private static bool IsReparsePoint(string path)
    {
        // Olmayan yolda Attributes -1 (bütün bayraklar) döner.
        try { return new DirectoryInfo(path) is { Exists: true } info && info.Attributes.HasFlag(FileAttributes.ReparsePoint); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool IsEmptyDirectory(string path)
    {
        try { return !Directory.EnumerateFileSystemEntries(path).Any(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
