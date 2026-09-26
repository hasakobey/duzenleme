namespace Duzenleme.Core;

/// <summary>Klasör simgesinin ön yüzündeki sembol (Segoe Fluent Icons karakteri).</summary>
public sealed record IconGlyph(string Key, string Label, char Glyph, string[] Keywords);

/// <summary>Klasör rengi (ön yüz için açık, arka sekme için koyu ton).</summary>
public sealed record IconColor(string Key, string Label, uint Front, uint Back);

/// <summary>Hazır klasör simgesi kütüphanesi; API anahtarı olmadan da çalışır.</summary>
public static class FolderIconCatalog
{
    public static readonly IconGlyph[] Glyphs =
    [
        new("folder", "Klasör", '', []),
        new("pdf", "PDF", '', ["pdf"]),
        new("document", "Belgeler", '', ["belge", "dokuman", "doküman", "document", "evrak", "docs", "yazi", "yazı", "word", "excel"]),
        new("image", "Resimler", '', ["resim", "foto", "fotoğraf", "fotograf", "gorsel", "görsel", "image", "photo", "picture", "ekran"]),
        new("music", "Müzik", '', ["muzik", "müzik", "music", "sarki", "şarkı", "ses", "audio", "podcast"]),
        new("video", "Videolar", '', ["video", "film", "dizi", "movie", "kayit", "kayıt"]),
        new("archive", "Arşivler", '', ["arsiv", "arşiv", "archive", "zip", "rar", "yedek", "backup"]),
        new("download", "İndirilenler", '', ["indir", "download"]),
        new("game", "Oyunlar", '', ["oyun", "game", "steam", "epic"]),
        new("code", "Kod", '', ["kod", "code", "proje", "project", "yazilim", "yazılım", "dev", "github", "repo"]),
        new("work", "İş", '', ["is", "iş", "work", "ofis", "office", "sirket", "şirket", "musteri", "müşteri"]),
        new("school", "Okul", '', ["okul", "ders", "odev", "ödev", "school", "universite", "üniversite", "sinav", "sınav", "kurs"]),
        new("money", "Finans", '', ["fatura", "para", "finans", "banka", "maas", "maaş", "vergi", "invoice", "odeme", "ödeme"]),
        new("design", "Tasarım", '', ["tasarim", "tasarım", "design", "figma", "logo", "cizim", "çizim", "sanat", "art"]),
        new("travel", "Seyahat", '', ["seyahat", "tatil", "travel", "ucak", "uçak", "bilet", "gezi"]),
        new("heart", "Kişisel", '', ["aile", "family", "kisisel", "kişisel", "ozel", "özel", "sevgili", "anı", "ani"]),
        new("home", "Ev", '', ["ev", "home", "kira"]),
        new("star", "Önemli", '', ["onemli", "önemli", "favori", "favorite", "yildiz", "yıldız"]),
        new("shopping", "Alışveriş", '', ["alisveris", "alışveriş", "shopping", "siparis", "sipariş"]),
        new("health", "Sağlık", '', ["saglik", "sağlık", "health", "doktor", "hastane", "spor"]),
        new("cloud", "Bulut", '', ["bulut", "cloud", "drive", "onedrive"]),
        new("tools", "Araçlar", '', ["arac", "araç", "tool", "program", "kurulum", "setup", "yazilimlar"]),
        new("camera", "Kamera", '', ["kamera", "camera", "cekim", "çekim"]),
        new("mail", "Posta", '', ["posta", "mail", "eposta", "e-posta"]),
        new("people", "Kişiler", '', ["kisiler", "kişiler", "ekip", "team", "people"]),
        new("lock", "Gizli", '', ["gizli", "sifre", "şifre", "private", "secret", "kilit"]),
        new("book", "Kitaplar", '', ["kitap", "book", "okuma", "ebook", "e-kitap"]),
        new("calendar", "Planlar", '', ["plan", "takvim", "calendar", "etkinlik"]),
        new("car", "Araç", '', ["araba", "car", "otomotiv", "filo"]),
        new("globe", "Web", '', ["web", "site", "internet", "globe"]),
    ];

    public static readonly IconColor[] Colors =
    [
        new("amber", "Kehribar", 0xFFFBBF24, 0xFFD97706),
        new("red", "Kırmızı", 0xFFF87171, 0xFFDC2626),
        new("orange", "Turuncu", 0xFFFB923C, 0xFFEA580C),
        new("pink", "Pembe", 0xFFF472B6, 0xFFDB2777),
        new("violet", "Mor", 0xFFA78BFA, 0xFF7C3AED),
        new("indigo", "Çivit", 0xFF818CF8, 0xFF4F46E5),
        new("blue", "Mavi", 0xFF60A5FA, 0xFF2563EB),
        new("cyan", "Turkuaz", 0xFF22D3EE, 0xFF0891B2),
        new("green", "Yeşil", 0xFF4ADE80, 0xFF16A34A),
        new("slate", "Gri", 0xFF94A3B8, 0xFF475569),
    ];

    /// <summary>Sembole yakışan varsayılan renk.</summary>
    private static readonly Dictionary<string, string> DefaultColor = new()
    {
        ["pdf"] = "red", ["document"] = "blue", ["image"] = "pink", ["music"] = "violet", ["video"] = "indigo",
        ["archive"] = "slate", ["download"] = "cyan", ["game"] = "green", ["code"] = "indigo", ["work"] = "blue",
        ["school"] = "orange", ["money"] = "green", ["design"] = "pink", ["travel"] = "cyan", ["heart"] = "red",
        ["home"] = "orange", ["star"] = "amber", ["shopping"] = "orange", ["health"] = "red", ["cloud"] = "blue",
        ["tools"] = "slate", ["camera"] = "violet", ["mail"] = "blue", ["people"] = "cyan", ["lock"] = "slate",
        ["book"] = "amber", ["calendar"] = "red", ["car"] = "blue", ["globe"] = "cyan", ["folder"] = "amber",
    };

    public static IconGlyph Glyph(string key) => Glyphs.FirstOrDefault(g => g.Key == key) ?? Glyphs[0];

    public static IconColor Color(string key) => Colors.FirstOrDefault(c => c.Key == key) ?? Colors[0];

    public static IconColor ColorFor(IconGlyph glyph) => Color(DefaultColor.GetValueOrDefault(glyph.Key, "amber"));

    /// <summary>Klasör adına göre sembol ve renk önerir (ör. "Oyunlarım" → oyun kolu, yeşil).</summary>
    public static (IconGlyph Glyph, IconColor Color) Suggest(string folderName)
    {
        var folded = FolderName.Fold(folderName);
        var words = folded.Split([' ', '-', '_', '.', ','], StringSplitOptions.RemoveEmptyEntries);
        foreach (var glyph in Glyphs)
        {
            foreach (var keyword in glyph.Keywords.Select(FolderName.Fold))
            {
                // Kısa anahtar kelimeler ("is", "ev") yalnızca tam kelime olarak eşleşsin; uzunlar kelime başında.
                var hit = keyword.Length <= 3
                    ? words.Contains(keyword)
                    : words.Any(w => w.StartsWith(keyword, StringComparison.Ordinal));
                if (hit) return (glyph, ColorFor(glyph));
            }
        }

        // Eşleşme yoksa klasör sembolü; renk addan türetilir ki her klasör farklı görünsün.
        var hash = folded.Aggregate(17, (h, c) => unchecked(h * 31 + c));
        return (Glyphs[0], Colors[Math.Abs(hash % Colors.Length)]);
    }
}
