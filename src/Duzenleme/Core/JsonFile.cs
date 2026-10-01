using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duzenleme.Core;

/// <summary>JSON dosyalarını atomik olarak (önce geçici dosyaya) yazar; bozuk dosyada yedeğe, o da yoksa varsayılana döner.</summary>
public static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Enum'lar adla yazılır; tanınmayan ad (daha yeni sürümden) bütün ayarları bozuk saydırmaz, yedek değere döner.
        Converters = { new TolerantEnumConverterFactory() },
        // Elle düzenlenmiş dosyalarda "12" gibi metin sayılar da kabul edilsin.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals | JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>Okuma sırasındaki uyarılar (ör. tanınmayan enum değeri); uygulama günlüğe bağlar. Herhangi bir iş parçacığından çağrılır.</summary>
    public static Action<string>? Log { get; set; }

    /// <summary>
    /// Dosyayı okur. Yoksa varsayılanı döner; içerik bozuk ya da boşsa yedekleyip (<c>.bozuk-*</c>) bir önceki sürüme
    /// (<c>.bak</c>, bkz. <see cref="DurableFile"/>) döner, o da okunamazsa varsayılana.
    /// Dosya geçici olarak okunamıyorsa (başka süreç kilitlemiş, ağ gecikmesi) birkaç kez dener ve sonunda hata fırlatır:
    /// varsayılana dönüp sonra kaydetmek kullanıcının gerçek ayarlarını ezerdi.
    /// </summary>
    public static T Load<T>(string path, Func<T> fallback) where T : class
    {
        if (!File.Exists(path))
        {
            // Yazma tam yer değiştirirken kesildiyse (hedef yedeğe alınmış, yenisi henüz yerine konmamış) yeni içerik
            // .tmp'de bütün durur. Yalnızca .bak varsa kullanıcı ayarları bilerek silmiş olabilir: sıfırdan başlanır.
            var tmp = path + ".tmp";
            if (File.Exists(tmp) && File.Exists(path + ".bak"))
                return TryRead<T>(tmp) ?? TryRead<T>(path + ".bak") ?? fallback();
            return fallback();
        }

        string text;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                text = File.ReadAllText(path);
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 6)
            {
                Thread.Sleep(150 * attempt);
            }
        }

        if (TryParse<T>(text, out var value)) return value;

        // Okunamayan dosya varsayılanla ezilmeden önce yedeklensin; kullanıcının ayarları kaybolmasın.
        string? backup = $"{path}.bozuk-{DateTime.Now:yyyyMMdd-HHmmss}";
        try { File.Copy(path, backup, overwrite: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { backup = null; }
        var previous = TryRead<T>(path + ".bak");
        // Kopya alınamasa da bildirilir: kullanıcı ayarlarının neden değiştiğini bilsin (metin .bak'ın yüklenip yüklenmediğine göre).
        try { Corrupted?.Invoke(KindOf(path), backup, previous is not null); }
        catch (Exception ex) { Log?.Invoke("bozuk dosya bildirimi: " + ex.GetType().Name); } // l10n: çevrilmez (günlük)
        return previous ?? fallback();
    }

    /// <summary>
    /// Bir dosya okunamadı ve yerine yedeği ya da varsayılan yüklendi (herhangi bir iş parçacığından, Load içinde):
    /// tür (<see cref="KindOf"/>), <c>.bozuk-*</c> kopyasının yolu (kopya alınamadıysa null) ve <c>.bak</c> yüklendi mi.
    /// </summary>
    public static Action<string, string?, bool>? Corrupted { get; set; }

    public const string KindSettings = "settings";
    public const string KindJournal = "journal";
    public const string KindBoxMoves = "box-moves";
    public const string KindOther = "other";

    /// <summary>Okunamayan dosyanın türü (bildirimin metni buna göre seçilir): ayarlar, taşıma geçmişi, kutu kayıtları ya da başka.</summary>
    public static string KindOf(string path) => Path.GetFileName(path).ToLowerInvariant() switch
    {
        "settings.json" => KindSettings,
        "journal.json" => KindJournal,
        "box-moves.json" => KindBoxMoves,
        _ => KindOther,
    };

    private static bool TryParse<T>(string text, out T value) where T : class
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(text, Options)!;
            return value is not null;
        }
        catch (JsonException)
        {
            value = null!;
            return false;
        }
    }

    private static T? TryRead<T>(string path) where T : class
    {
        try { return File.Exists(path) && TryParse<T>(File.ReadAllText(path), out var value) ? value : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    /// <summary>Eşzamanlı, atomik ve yedekli yazar (bkz. <see cref="DurableFile.WriteAtomic"/>).</summary>
    public static void Save<T>(string path, T value) => DurableFile.WriteAtomic(path, Serialize(value));
}
