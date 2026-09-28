namespace Duzenleme.Core;

/// <summary>
/// Klasör simgesinin ön yüzündeki sembol (Segoe Fluent Icons karakteri). LabelKey Türkçe anahtardır (L.N ile işaretli,
/// LabelContext çeviri bağlamı); gösterilen ad <see cref="Label"/>, arayüz dilinde. Keywords klasör adıyla eşleşen veridir.
/// </summary>
public sealed record IconGlyph(string Key, string LabelKey, char Glyph, string[] Keywords, string? LabelContext = null)
{
    /// <summary>Arayüz dilindeki ad (ipucu, seçici, tepsi önerisi).</summary>
    public string Label => L.Dyn(LabelKey, LabelContext);
}

/// <summary>Klasör rengi (ön yüz için açık, arka sekme için koyu ton). LabelKey Türkçe anahtar; gösterilen ad <see cref="Label"/>.</summary>
public sealed record IconColor(string Key, string LabelKey, uint Front, uint Back)
{
    /// <summary>Arayüz dilindeki ad.</summary>
    public string Label => L.Dyn(LabelKey);
}

/// <summary>Hazır klasör simgesi kütüphanesi; API anahtarı olmadan da çalışır.</summary>
public static class FolderIconCatalog
{
    // Anahtar kelimeler (Keywords) klasör adlarıyla eşleşen veridir, çevrilmez: Türkçe ve İngilizce adların ikisi de tanınır.
    public static readonly IconGlyph[] Glyphs =
    [
        new("folder", L.N("Klasör"), '', []),
        new("pdf", L.N("PDF"), '', ["pdf"]),
        new("document", L.N("Belgeler"), '', ["belge", "dokuman", "doküman", "document", "evrak", "docs", "yazi", "yazı", "word", "excel"]), // l10n: çevrilmez (anahtar kelimeler)
        new("image", L.N("Resimler"), '', ["resim", "foto", "fotoğraf", "fotograf", "gorsel", "görsel", "image", "photo", "picture", "ekran"]), // l10n: çevrilmez (anahtar kelimeler)
        new("music", L.N("Müzik"), '', ["muzik", "müzik", "music", "sarki", "şarkı", "ses", "audio", "podcast"]), // l10n: çevrilmez (anahtar kelimeler)
        new("video", L.N("Videolar"), '', ["video", "film", "dizi", "movie", "kayit", "kayıt"]), // l10n: çevrilmez (anahtar kelimeler)
        new("archive", L.N("Arşivler"), '', ["arsiv", "arşiv", "archive", "zip", "rar", "yedek", "backup"]), // l10n: çevrilmez (anahtar kelimeler)
        new("download", L.N("İndirilenler"), '', ["indir", "download"]),
        new("game", L.N("Oyunlar"), '', ["oyun", "game", "steam", "epic"]),
        new("code", L.N("Kod"), '', ["kod", "code", "proje", "project", "yazilim", "yazılım", "dev", "github", "repo"]), // l10n: çevrilmez (anahtar kelimeler)
        new("work", L.N("İş"), '', ["is", "iş", "work", "ofis", "office", "sirket", "şirket", "musteri", "müşteri"]), // l10n: çevrilmez (anahtar kelimeler)
        new("school", L.N("Okul"), '', ["okul", "ders", "odev", "ödev", "school", "universite", "üniversite", "sinav", "sınav", "kurs"]), // l10n: çevrilmez (anahtar kelimeler)
        new("money", L.N("Finans"), '', ["fatura", "para", "finans", "banka", "maas", "maaş", "vergi", "invoice", "odeme", "ödeme"]), // l10n: çevrilmez (anahtar kelimeler)
        new("design", L.N("Tasarım"), '', ["tasarim", "tasarım", "design", "figma", "logo", "cizim", "çizim", "sanat", "art"]), // l10n: çevrilmez (anahtar kelimeler)
        new("travel", L.N("Seyahat"), '', ["seyahat", "tatil", "travel", "ucak", "uçak", "bilet", "gezi"]), // l10n: çevrilmez (anahtar kelimeler)
        new("heart", L.N("Kişisel"), '', ["aile", "family", "kisisel", "kişisel", "ozel", "özel", "sevgili", "anı", "ani"]), // l10n: çevrilmez (anahtar kelimeler)
        new("home", L.N("Ev"), '', ["ev", "home", "kira"]),
        new("star", L.N("Önemli"), '', ["onemli", "önemli", "favori", "favorite", "yildiz", "yıldız"]), // l10n: çevrilmez (anahtar kelimeler)
        new("shopping", L.N("Alışveriş"), '', ["alisveris", "alışveriş", "shopping", "siparis", "sipariş"]), // l10n: çevrilmez (anahtar kelimeler)
        new("health", L.N("Sağlık"), '', ["saglik", "sağlık", "health", "doktor", "hastane", "spor"]), // l10n: çevrilmez (anahtar kelimeler)
        new("cloud", L.N("Bulut"), '', ["bulut", "cloud", "drive", "onedrive"]),
        new("tools", L.N("Araçlar"), '', ["arac", "araç", "tool", "program", "kurulum", "setup", "yazilimlar"]), // l10n: çevrilmez (anahtar kelimeler)
        new("camera", L.N("Kamera"), '', ["kamera", "camera", "cekim", "çekim"]), // l10n: çevrilmez (anahtar kelimeler)
        new("mail", L.N("Posta"), '', ["posta", "mail", "eposta", "e-posta"]),
        new("people", L.N("Kişiler"), '', ["kisiler", "kişiler", "ekip", "team", "people"]), // l10n: çevrilmez (anahtar kelimeler)
        new("lock", L.N("Gizli", "simge"), '', ["gizli", "sifre", "şifre", "private", "secret", "kilit"], LabelContext: "simge"), // l10n: çevrilmez (anahtar kelimeler)
        new("book", L.N("Kitaplar"), '', ["kitap", "book", "okuma", "ebook", "e-kitap"]),
        new("calendar", L.N("Planlar"), '', ["plan", "takvim", "calendar", "etkinlik"]),
        new("car", L.N("Araç", "taşıt"), '', ["araba", "car", "otomotiv", "filo"], LabelContext: "taşıt"), // l10n: çevrilmez (bağlam)
        new("globe", L.N("Web"), '', ["web", "site", "internet", "globe"]),
    ];

    public static readonly IconColor[] Colors =
    [
        new("amber", L.N("Kehribar"), 0xFFFBBF24, 0xFFD97706),
        new("red", L.N("Kırmızı"), 0xFFF87171, 0xFFDC2626),
        new("orange", L.N("Turuncu"), 0xFFFB923C, 0xFFEA580C),
        new("pink", L.N("Pembe"), 0xFFF472B6, 0xFFDB2777),
        new("violet", L.N("Mor"), 0xFFA78BFA, 0xFF7C3AED),
        new("indigo", L.N("Çivit"), 0xFF818CF8, 0xFF4F46E5),
        new("blue", L.N("Mavi"), 0xFF60A5FA, 0xFF2563EB),
        new("cyan", L.N("Turkuaz"), 0xFF22D3EE, 0xFF0891B2),
        new("green", L.N("Yeşil"), 0xFF4ADE80, 0xFF16A34A),
        new("slate", L.N("Gri"), 0xFF94A3B8, 0xFF475569),
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
