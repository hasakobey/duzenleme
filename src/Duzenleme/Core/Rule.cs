namespace Duzenleme.Core;

/// <summary>Belirli uzantılardaki dosyaların taşınacağı masaüstü klasörünü tanımlar.</summary>
public sealed class Rule
{
    public string TargetFolder { get; set; } = "";
    public List<string> Extensions { get; set; } = [];
    public bool Enabled { get; set; } = true;

    public bool Matches(string extension)
    {
        var ext = NormalizeExtension(extension);
        return Extensions.Any(e => NormalizeExtension(e) == ext);
    }

    public static string NormalizeExtension(string extension) =>
        extension.Trim().TrimStart('*').TrimStart('.').ToLowerInvariant();

    /// <summary>
    /// Hazır kuralların klasörleri, iki dilde (Windows kitaplıklarının o dildeki adları gibi). Veri: çeviri tablosundan
    /// geçmez; kural bir kez oluşturulunca kullanıcı verisidir, dil değişince yeniden adlandırılmaz.
    /// </summary>
    private static readonly (string Tr, string En, string[] Extensions)[] Categories =
    [
        ("PDF", "PDF", ["pdf"]),
        ("Resimler", "Pictures", ["jpg", "jpeg", "png", "gif", "webp", "bmp", "heic", "svg"]),
        ("Belgeler", "Documents", ["doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "rtf", "odt", "csv"]),
        ("Arşivler", "Archives", ["zip", "rar", "7z", "tar", "gz"]),   // l10n: çevrilmez
        ("Videolar", "Videos", ["mp4", "mov", "avi", "mkv", "webm"]),
        ("Müzik", "Music", ["mp3", "wav", "flac", "m4a", "ogg"]),      // l10n: çevrilmez
    ];

    /// <summary>Arayüz dilindeki hazır kurallar (yeni ayarlar bununla oluşur).</summary>
    public static List<Rule> Defaults() => Defaults(L.Current);

    /// <summary>
    /// Verilen dildeki hazır kurallar. desktopFolders verilirse masaüstünde zaten olan klasöre uyulur: o dildeki ad yoksa
    /// ama öteki dildeki karşılığı varsa (ör. İngilizce Windows'ta "Resimler") kural o klasörü hedefler; hiçbiri yoksa o
    /// dildeki ad. Mevcut kurallar ve klasörler hiç yeniden adlandırılmaz; bu yalnızca kural oluşturulurken kullanılır.
    /// </summary>
    public static List<Rule> Defaults(Lang lang, IEnumerable<string>? desktopFolders = null)
    {
        var folders = desktopFolders?.ToList() ?? [];
        return Categories.Select(c =>
        {
            var (own, other) = lang == Lang.Tr ? (c.Tr, c.En) : (c.En, c.Tr);
            var target = folders.Any(f => FolderName.Equal(f, own))
                ? own
                : folders.FirstOrDefault(f => FolderName.Equal(f, other)) ?? own;
            return new Rule { TargetFolder = target, Extensions = [.. c.Extensions] };
        }).ToList();
    }

    /// <summary>
    /// Kurallar hâlâ (herhangi bir dildeki) hazır kurallar mı: aynı sırada, açık, aynı uzantılarla ve klasör adı o
    /// kategorinin iki dildeki adından biri. Kullanıcının değiştirdiği kurallara dokunmamak için.
    /// </summary>
    public static bool AreDefaults(IReadOnlyList<Rule> rules) =>
        rules.Count == Categories.Length
        && rules.Zip(Categories).All(p =>
            p.First.Enabled
            && (FolderName.Equal(p.First.TargetFolder, p.Second.Tr) || FolderName.Equal(p.First.TargetFolder, p.Second.En))
            && p.First.Extensions.SequenceEqual(p.Second.Extensions));

    // 2.1 P6
    /// <summary>Daha yeni sürümün yazdığı bilinmeyen alanlar (aynen geri yazılır).</summary>
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }
}
