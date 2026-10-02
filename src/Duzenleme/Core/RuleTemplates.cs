namespace Duzenleme.Core;

/// <summary>
/// "Kural ekle"nin hazır kalıbı: klasör adı (Türkçe anahtar, <see cref="L.N"/> ile işaretli; İngilizcesi çeviri
/// tablosunda) ve uzantılar. Klasör adı kural oluşturulurken bir kez arayüz dilinde yazılır (<see cref="NameIn"/>), sonra
/// kullanıcı verisidir; var olan kural ve masaüstü klasörü iki dildeki adla da tanınır (<see cref="Names"/> =
/// <see cref="L.DynVariants"/>).
/// </summary>
public sealed record RuleTemplate(string Key, string NameKey, IReadOnlyList<string> Extensions)
{
    /// <summary>Arayüz dilindeki ad (kutucuk).</summary>
    public string Name => L.Dyn(NameKey);

    /// <summary>İki dildeki adlar [tr, en] (aynıysa tek): bu adla bir kural ya da klasör varsa kalıp onu tanır.</summary>
    public IReadOnlyList<string> Names => L.DynVariants(NameKey);

    /// <summary>Verilen dildeki ad (yeni klasörün adı).</summary>
    public string NameIn(Lang lang) => lang == Lang.Tr ? NameKey : Names[^1];

    /// <summary>Bu ad kalıbın adlarından biri mi (büyük/küçük harf ve Türkçe karakter fark etmez)?</summary>
    public bool IsNamed(string folder) => Names.Any(n => FolderName.Equal(folder, n));
}

/// <summary>Kalıbın şimdi eklenebilecek hâli: uzantılarından hiçbir kuralda olmayanlar.</summary>
public sealed record RuleOffer(RuleTemplate Template, IReadOnlyList<string> Extensions);

/// <summary>Yazılan uzantının durumu (<see cref="RuleTemplates.Check"/>).</summary>
public enum ExtensionProblem { None, Invalid, Ignored, Taken }

/// <summary>Uzantı denetimi: düzgün biçimi (".PDF" → "pdf"), sorunu ve (alınmışsa) onu kullanan kuralın klasörü.</summary>
public readonly record struct ExtensionCheck(string Extension, ExtensionProblem Problem, string? Owner = null)
{
    public bool Ok => Problem == ExtensionProblem.None;
}

/// <summary>
/// Kural kalıpları ve uzantı kuralları ("Kural ekle" penceresi; saf, testlenir). Bir uzantı en çok bir kuralda olur:
/// kalıplar başka kuralda olan uzantıyı önermez, yazılan uzantı başka kuraldaysa eklenmez (taşıyıcı ilk eşleşen kuralı
/// kullanır; ikinci kural sessizce hiç çalışmazdı). Kural listesi hiç yerinde değiştirilmez: her iş yeni liste döner.
/// </summary>
public static class RuleTemplates
{
    /// <summary>
    /// Kalıplar, kutucuk sırasıyla: hazır altı kural (uzantıları <see cref="Rule.Categories"/>'den) ve iki ek kalıp.
    /// Anahtar UI Automation kimliğinde kullanılır (<c>Rule.Template.&lt;anahtar&gt;</c>).
    /// </summary>
    public static IReadOnlyList<RuleTemplate> All { get; } =
    [
        Default("pdf", L.N("PDF")),
        Default("pictures", L.N("Resimler")),
        Default("documents", L.N("Belgeler")),
        Default("videos", L.N("Videolar")),
        Default("music", L.N("Müzik")),
        Default("archives", L.N("Arşivler")),
        new("design", L.N("Tasarım"), ["psd", "ai", "fig", "xd"]),
        new("ebooks", L.N("E-kitaplar"), ["epub", "mobi"]),
    ];

    private static RuleTemplate Default(string key, string nameKey) =>
        new(key, nameKey, Rule.Categories.First(c => c.Tr == nameKey).Extensions);

    /// <summary>Uzantının en uzun hâli (daha uzunu dosya adı uzantısı olmaz; yazım hatasıdır).</summary>
    public const int MaxExtensionLength = 24;

    /// <summary>
    /// Şimdi önerilecek kalıplar: adıyla (iki dilden biriyle) bir kural zaten olan kalıp önerilmez (kapalı kural da sayılır:
    /// açmak yeter); kalan uzantılarından hiçbir kuralda olmayanlar önerilir, hiçbiri kalmadıysa kalıp önerilmez.
    /// </summary>
    public static List<RuleOffer> Offers(IReadOnlyList<Rule> rules)
    {
        var used = UsedExtensions(rules);
        var offers = new List<RuleOffer>();
        foreach (var template in All)
        {
            if (rules.Any(r => template.IsNamed(r.TargetFolder))) continue;
            var free = template.Extensions.Where(e => !used.Contains(e)).ToList();
            if (free.Count > 0) offers.Add(new RuleOffer(template, free));
        }
        return offers;
    }

    /// <summary>Kuralların kullandığı uzantılar (düzgün biçimde); except verilirse o kural sayılmaz (düzenlenen kural).</summary>
    public static HashSet<string> UsedExtensions(IEnumerable<Rule> rules, Rule? except = null) =>
        rules.Where(r => !ReferenceEquals(r, except))
            .SelectMany(r => r.Extensions).Select(Normalize).Where(e => e.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Kalıbın klasörü: masaüstünde o dildeki adla klasör varsa o (gerçek yazılışıyla), yoksa öteki dildeki adla olan
    /// (İngilizce Windows'ta "Resimler" gibi), hiçbiri yoksa o dildeki ad (yeni klasör). Bkz. <see cref="Rule.Defaults(Lang, IEnumerable{string})"/>.
    /// </summary>
    public static string FolderFor(RuleTemplate template, Lang lang, IEnumerable<string> desktopFolders)
    {
        var folders = desktopFolders.ToList();
        var own = template.NameIn(lang);
        return folders.FirstOrDefault(f => FolderName.Equal(f, own))
               ?? folders.FirstOrDefault(template.IsNamed)
               ?? own;
    }

    /// <summary>
    /// Yazılan uzantılar için önerilen klasör: ilk uzantı bir kalıbınsa onun klasörü ("psd" → "Tasarım", <see cref="FolderFor"/>),
    /// değilse uzantının büyük harfli hâli ("dwg" → "DWG"; masaüstünde bu adda klasör varsa onun yazılışı). Liste boşsa boş metin.
    /// </summary>
    public static string SuggestFolder(IReadOnlyList<string> extensions, Lang lang, IEnumerable<string> desktopFolders)
    {
        if (extensions.Count == 0) return "";
        var first = Normalize(extensions[0]);
        if (All.FirstOrDefault(t => t.Extensions.Contains(first)) is { } template) return FolderFor(template, lang, desktopFolders);
        var name = first.ToUpperInvariant();
        return desktopFolders.FirstOrDefault(f => FolderName.Equal(f, name)) ?? name;
    }

    /// <summary>
    /// Uzantının düzgün biçimi: baştaki "*" ve ".", büyük harf ve boşluk atılır; noktalıysa yalnızca sonu kalır ("tar.gz" →
    /// "gz": Windows'un uzantısı son noktadan sonrasıdır, "tar.gz" kuralı hiçbir dosyaya uymazdı).
    /// </summary>
    public static string Normalize(string raw)
    {
        var ext = Rule.NormalizeExtension(raw);
        var dot = ext.LastIndexOf('.');
        return dot >= 0 ? ext[(dot + 1)..] : ext;
    }

    /// <summary>Yazılan metindeki uzantılar (virgül, noktalı virgül ya da boşlukla ayrılmış), düzgün biçimde ve tekil.</summary>
    public static List<string> Split(string text) =>
        text.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize).Where(e => e.Length > 0).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>
    /// Uzantı eklenebilir mi: dosya adında olamayan karakter ya da çok uzun → Invalid; hiç taşınmayan tür (kısayol, yarım
    /// indirme…) → Ignored; başka bir kuralda (editing dışında) → Taken (Owner: o kuralın klasörü).
    /// </summary>
    public static ExtensionCheck Check(string raw, IReadOnlyList<Rule> rules, Rule? editing = null)
    {
        var ext = Normalize(raw);
        if (ext.Length is 0 or > MaxExtensionLength || ext.Any(c => FileNames.IsInvalidChar(c) || char.IsWhiteSpace(c)))
            return new(ext, ExtensionProblem.Invalid);
        if (RuleEngine.IgnoredExtensions.Contains(ext)) return new(ext, ExtensionProblem.Ignored);
        var owner = rules.FirstOrDefault(r => !ReferenceEquals(r, editing) && r.Extensions.Any(e => Normalize(e) == ext));
        return owner is null ? new(ext, ExtensionProblem.None) : new(ext, ExtensionProblem.Taken, owner.TargetFolder);
    }

    /// <summary>Uzantıların gösterimi: ".psd .ai .fig".</summary>
    public static string Dotted(IEnumerable<string> extensions) => string.Join(" ", extensions.Select(e => "." + e));

    /// <summary>Yeni kural (açık).</summary>
    public static Rule Create(IEnumerable<string> extensions, string folder) =>
        new() { TargetFolder = folder, Extensions = extensions.Select(Normalize).Where(e => e.Length > 0).Distinct().ToList() };

    /// <summary>
    /// Düzenlenen kuralın yeni hâli: yeni nesne (izleyici eskisini okurken değişmesin); açık/kapalı ve daha yeni sürümün
    /// alanları (<see cref="Rule.Extra"/>) korunur.
    /// </summary>
    public static Rule Edited(Rule rule, IEnumerable<string> extensions, string folder)
    {
        var copy = rule.Clone();
        copy.TargetFolder = folder;
        copy.Extensions = extensions.Select(Normalize).Where(e => e.Length > 0).Distinct().ToList();
        return copy;
    }

    /// <summary>Kural sona eklenmiş yeni liste.</summary>
    public static List<Rule> Added(IReadOnlyList<Rule> rules, Rule rule) => [.. rules, rule];

    /// <summary>old'un yerine updated konmuş yeni liste (old listede yoksa sona eklenir).</summary>
    public static List<Rule> Replaced(IReadOnlyList<Rule> rules, Rule old, Rule updated)
    {
        var list = new List<Rule>(rules);
        var index = list.FindIndex(r => ReferenceEquals(r, old));
        if (index >= 0) list[index] = updated;
        else list.Add(updated);
        return list;
    }
}
