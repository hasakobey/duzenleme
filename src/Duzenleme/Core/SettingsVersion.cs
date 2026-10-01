using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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
/// Kurallar saftır; dosya işleri (<see cref="BackupBeforeChange"/>, <see cref="PruneBackups"/>,
/// <see cref="ClearPropertyInBackups"/>) verilen klasörde çalışır ve testlenir; AppHost yalnızca yolları ve zamanlamayı verir.
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
    /// adlar (günlük yedekler, kullanıcının dosyaları) hiç dönmez. <paramref name="justTaken"/> (bu açılışta alınan kopya)
    /// adındaki zamandan bağımsız olarak kalır ve kalanlardan biri sayılır: saat geri alındıysa ya da daha eski bir yedeğin
    /// adında gelecekten bir zaman varsa da az önce alınan kopya silinmez.
    /// </summary>
    public static IReadOnlyList<string> Prune(IEnumerable<string> names, int keep = VersionBackupsKept, string? justTaken = null)
    {
        var list = names.ToList();
        var held = justTaken is not null && list.Contains(justTaken, StringComparer.OrdinalIgnoreCase) ? 1 : 0;
        return list
            .Where(n => held == 0 || !string.Equals(n, justTaken, StringComparison.OrdinalIgnoreCase))
            .Select(n => (Name: n, Match: BackupNamePattern.Match(n)))
            .Where(x => x.Match.Success)
            .Select(x => (x.Name, Time: DateTime.TryParseExact(x.Match.Groups[1].Value, TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time) ? time : DateTime.MinValue))
            .OrderByDescending(x => x.Time)
            .ThenByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(0, keep - held))
            .Select(x => x.Name)
            .ToList();
    }

    /// <summary>
    /// Kullanıcıya gösterilecek sürüm ("2.3.0"): damganın yalnızca okunan sayıları (elle düzenlenmiş dosyadaki son ek
    /// gösterilmez). Okunamazsa "?".
    /// </summary>
    public static string Display(string? version) => Parse(version) is { } v ? v.ToString(3) : "?";

    // --- Dosya işleri (veri klasörünün "yedekler" alt klasöründe; AppHost yolları ve zamanlamayı verir) ---

    private static readonly JsonSerializerOptions BackupWriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Sürüm değiştiyse ayar dosyasını <paramref name="backupDir"/>'e <see cref="BackupName"/> adıyla birebir kopyalar.
    /// Açılışta, dosya yüklendikten hemen sonra ve ilk kayıttan önce, eşzamanlı çağrılır (sürüm başına bir kez): daha eski
    /// sürüm tanımadığı enum değerlerini ilk kayıtta yedek değerle ezer. Kopyanın adı; sürüm aynıysa, ayar dosyası yoksa
    /// (yeni kullanıcı) ya da kopyalanamadıysa null (açılış sürer, neden <paramref name="log"/>'a).
    /// </summary>
    public static string? BackupBeforeChange(string settingsPath, string backupDir, SettingsVersionChange change, string current,
        DateTime now, Action<string>? log = null)
    {
        if (change.Kind == SettingsVersionKind.Same) return null;
        try
        {
            if (!File.Exists(settingsPath)) return null;
            Directory.CreateDirectory(backupDir);
            var name = BackupName(change.From, current, now);
            File.Copy(settingsPath, Path.Combine(backupDir, name), overwrite: true);
            log?.Invoke($"sürüm değişti ({change.From ?? UnknownFrom} → {current}): ayarların kopyası {name}"); // l10n: çevrilmez (günlük)
            return name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"sürüm yedeği alınamadı: {ex.GetType().Name} {ex.Message}"); // l10n: çevrilmez (günlük)
            return null;
        }
    }

    /// <summary>
    /// <paramref name="backupDir"/>'deki sürüm yedeklerinden en yeni <paramref name="keep"/> tanesi dışındakileri siler
    /// (<see cref="Prune"/>; <paramref name="justTaken"/> her zaman kalır). Başka dosyalara dokunulmaz. Silinen dosya sayısı.
    /// </summary>
    public static int PruneBackups(string backupDir, string? justTaken, int keep = VersionBackupsKept, Action<string>? log = null)
    {
        var deleted = 0;
        try
        {
            if (!Directory.Exists(backupDir)) return 0;
            var names = Directory.EnumerateFiles(backupDir, VersionBackupPattern).Select(f => Path.GetFileName(f)).ToList();
            foreach (var old in Prune(names, keep, justTaken))
            {
                try
                {
                    File.Delete(Path.Combine(backupDir, old));
                    deleted++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log?.Invoke($"eski sürüm yedeği silinemedi: {ex.GetType().Name} {ex.Message}"); // l10n: çevrilmez (günlük)
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"sürüm yedekleri listelenemedi: {ex.GetType().Name} {ex.Message}"); // l10n: çevrilmez (günlük)
        }
        return deleted;
    }

    /// <summary>
    /// Sürüm yedeklerinde en üst düzeydeki bir metin ayarını (yapay zekâ anahtarının şifreli kopyası, <c>AiKeyProtected</c>)
    /// boşaltır: yedek o alan null olarak yeniden yazılır (uygulamanın anahtarsız yazdığı biçim), geri kalanı (widget'lar,
    /// notlar, kurallar) kalır. Alan zaten boşsa dosyaya dokunulmaz. JSON olarak okunamayan bir yedekte alan dolu görünüyorsa
    /// yedek silinir (temizlenemez). Başka dosyalara (günlük yedekler, kullanıcının dosyaları) dokunulmaz. Değişen ya da
    /// silinen yedek sayısı.
    /// </summary>
    public static int ClearPropertyInBackups(string backupDir, string property, Action<string>? log = null)
    {
        var changed = 0;
        // Hızlı ön eleme: alan bir metin değeri taşıyor mu ("Ad": "…"); "Ad": null olan yedek ayrıştırılmaz.
        var filled = new Regex("\"" + Regex.Escape(property) + "\"\\s*:\\s*\"", RegexOptions.CultureInvariant);
        try
        {
            if (!Directory.Exists(backupDir)) return 0;
            foreach (var file in Directory.EnumerateFiles(backupDir, VersionBackupPattern).ToList())
            {
                if (!IsVersionBackup(Path.GetFileName(file))) continue;
                string? tmp = null;
                try
                {
                    var text = File.ReadAllText(file);
                    if (!filled.IsMatch(text)) continue;
                    switch (Cleared(text, property, out var rewritten))
                    {
                        case Clearing.AlreadyEmpty:
                            continue;
                        case Clearing.Rewritten:
                            tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                            File.WriteAllText(tmp, rewritten);
                            File.Move(tmp, file, overwrite: true);
                            tmp = null;
                            break;
                        default:
                            File.Delete(file);
                            break;
                    }
                    changed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    log?.Invoke($"sürüm yedeği temizlenemedi: {ex.GetType().Name} {ex.Message}"); // l10n: çevrilmez (günlük)
                    if (tmp is not null)
                    {
                        try { File.Delete(tmp); }
                        catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"sürüm yedekleri listelenemedi: {ex.GetType().Name} {ex.Message}"); // l10n: çevrilmez (günlük)
        }
        return changed;
    }

    private enum Clearing { AlreadyEmpty, Rewritten, Unreadable }

    private static Clearing Cleared(string json, string property, out string rewritten)
    {
        rewritten = "";
        try
        {
            if (JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                }) is not JsonObject root)
                return Clearing.Unreadable;
            if (!root.TryGetPropertyValue(property, out var value) || value is null) return Clearing.AlreadyEmpty;
            root[property] = null;
            rewritten = root.ToJsonString(BackupWriteOptions);
            return Clearing.Rewritten;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            return Clearing.Unreadable;
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Safe(string text)
    {
        var chars = text.Trim().Select(c => char.IsAsciiLetterOrDigit(c) || c == '.' ? c : '_').Take(24).ToArray();
        return chars.Length == 0 ? "_" : new string(chars);
    }
}
