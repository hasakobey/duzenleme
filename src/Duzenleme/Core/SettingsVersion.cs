using System.Globalization;
using System.Text.RegularExpressions;

namespace Duzenleme.Core;

/// <summary>Ayar dosyasını yazan sürümün, açan sürüme göre durumu (yalnızca çalışma zamanı; kalıcı değil).</summary>
public enum SettingsVersionKind { Same, Upgrade, Downgrade }

/// <summary>Sürüm karşılaştırmasının sonucu.</summary>
/// <param name="Kind">Aynı sürüm, yükseltme ya da daha eski bir sürüme dönüş.</param>
/// <param name="From">Dosyadaki sürüm (<see cref="AppSettings.SavedWithAppVersion"/>); null = damgasız (2.2'den önceki sürüm).</param>
public readonly record struct SettingsVersionChange(SettingsVersionKind Kind, string? From);

/// <summary>
/// Sürüm değişikliği yedekleri (2.2, Kando'nun sürüm yedeği fikri). Ayar dosyası açılışta bir kez, açan sürümle damgalanır.
/// Damga açan sürümden farklıysa (yükseltme ya da daha eski sürüme dönüş) <c>settings.json</c>, uygulama ona dokunmadan
/// önce "yedekler" klasörüne <see cref="BackupName"/> adıyla kopyalanır; en yeni <see cref="VersionBackupsKept"/> tanesi
/// kalır. Ad "settings-" ile başlamaz: günlük yedeklerin döndürmesi (<see cref="DailyBackupPattern"/>) onları silmesin.
/// Saf kurallar; dosya işleri AppHost'ta.
/// </summary>
public static class SettingsVersion
{
    /// <summary>Ayar yedeklerinin klasörü (veri klasörünün altında). DEĞİŞMEZ: kullanıcıların mevcut yedekleri burada.</summary>
    public const string BackupFolder = "yedekler";

    /// <summary>Günlük yedekler ("settings-2026-10-01.json"); son yedisi tutulur.</summary>
    public const string DailyBackupPattern = "settings-*.json";

    /// <summary>Sürüm değişikliği yedekleri (<see cref="BackupName"/>).</summary>
    public const string VersionBackupPattern = "before-*.json";

    /// <summary>Tutulan sürüm yedeği sayısı.</summary>
    public const int VersionBackupsKept = 3;

    /// <summary>Damgasız (2.2'den önceki) dosyanın yedek adındaki "from" parçası.</summary>
    public const string UnknownFrom = "eski";

    private const string TimeFormat = "yyyyMMdd-HHmmss";

    private static readonly Regex VersionPrefix = new(@"^[vV]?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?", RegexOptions.CultureInvariant);

    private static readonly Regex BackupNamePattern = new(@"^before-[0-9A-Za-z._]+-from-[0-9A-Za-z._]+-(\d{8}-\d{6})\.json$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Dosyadaki sürümü (<paramref name="saved"/>) çalışan sürümle (<paramref name="current"/>) karşılaştırır. Damgasız dosya
    /// yükseltmedir. Okunamayan damga da yükseltme sayılır: yedek alınır ama "daha yeni sürüm" denmez (emin olunamaz).
    /// Çalışan sürüm okunamıyorsa hiçbir şey yapılmaz (Same).
    /// </summary>
    public static SettingsVersionChange Check(string? saved, string? current)
    {
        if (Parse(current) is not { } now) return new(SettingsVersionKind.Same, Clean(saved));
        if (Clean(saved) is not { } from) return new(SettingsVersionKind.Upgrade, null);
        if (Parse(from) is not { } then) return new(SettingsVersionKind.Upgrade, from);
        var order = then.CompareTo(now);
        return new(order == 0 ? SettingsVersionKind.Same : order < 0 ? SettingsVersionKind.Upgrade : SettingsVersionKind.Downgrade, from);
    }

    /// <summary>
    /// Hoşgörülü sürüm okuma: baştaki "v" ve sayı olmayan son ek ("-beta", "+abc") yok sayılır, eksik parçalar 0'dır
    /// ("2.2" = "2.2.0.0"). Sayıyla başlamayan ya da taşan metinde null.
    /// </summary>
    public static Version? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = VersionPrefix.Match(text.Trim());
        if (!match.Success) return null;
        var parts = new int[4];
        for (var i = 0; i < 4; i++)
        {
            var group = match.Groups[i + 1];
            if (!group.Success) continue;
            if (!int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out parts[i])) return null;
        }
        return new Version(parts[0], parts[1], parts[2], parts[3]);
    }

    /// <summary>
    /// Yedeğin dosya adı: "before-2.2.0-from-2.1.1-20261001-143000.json" (damgasız dosyada "from-eski"). Sürüm metinleri
    /// dosya adına uygun hâle getirilir (harf, rakam ve nokta kalır).
    /// </summary>
    public static string BackupName(string? from, string to, DateTime now) =>
        $"before-{Safe(to)}-from-{(Clean(from) is { } f ? Safe(f) : UnknownFrom)}-{now.ToString(TimeFormat, CultureInfo.InvariantCulture)}.json";

    /// <summary>Ad bir sürüm yedeğinin adı mı (<see cref="BackupName"/> biçimi)? Klasördeki başka dosyalara dokunulmaz.</summary>
    public static bool IsVersionBackup(string? name) => name is not null && BackupNamePattern.IsMatch(name);

    /// <summary>
    /// Silinecek sürüm yedekleri: adındaki zamana göre en yeni <paramref name="keep"/> tanesi kalır. Sürüm yedeği olmayan
    /// adlar (günlük yedekler, kullanıcının dosyaları) hiç dönmez.
    /// </summary>
    public static IReadOnlyList<string> Prune(IEnumerable<string> names, int keep = VersionBackupsKept) =>
        names.Select(n => (Name: n, Match: BackupNamePattern.Match(n)))
            .Where(x => x.Match.Success)
            .Select(x => (x.Name, Time: DateTime.TryParseExact(x.Match.Groups[1].Value, TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time) ? time : DateTime.MinValue))
            .OrderByDescending(x => x.Time)
            .ThenByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(0, keep))
            .Select(x => x.Name)
            .ToList();

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Safe(string text)
    {
        var chars = text.Trim().Select(c => char.IsAsciiLetterOrDigit(c) || c == '.' ? c : '_').Take(24).ToArray();
        return chars.Length == 0 ? "_" : new string(chars);
    }
}
