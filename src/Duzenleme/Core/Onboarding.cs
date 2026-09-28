namespace Duzenleme.Core;

/// <summary>Karşılamadaki seçimler: bellekte tutulur, hepsi "Bitti"de uygulanır (yarıda kalırsa masaüstüne bir şey olmaz).</summary>
public sealed class WelcomeChoices
{
    /// <summary>Başlangıç bölmeleri eklensin mi? null = seçilmedi.</summary>
    public bool? Fences { get; set; }

    /// <summary>Masaüstü simgeleri yalnızca bölmelerde görünsün (FencesReplaceIcons). Yalnızca Fences true iken anlamlı.</summary>
    public bool IconsOnlyInFences { get; set; } = true;

    /// <summary>
    /// "Kutulara eklediklerim masaüstünden kalksın" (BoxItemsLeaveDesktop). Yalnızca Fences true iken ve IconsOnlyInFences
    /// false iken anlamlı; ikisi de false ise "Hepsi masaüstünde görünsün".
    /// </summary>
    public bool BoxItemsLeaveDesktop { get; set; }

    /// <summary>Otomatik taşıma: true açılır, false kapatılır, null dokunulmaz.</summary>
    public bool? AutoMove { get; set; }

    /// <summary>Kullanılacak kural klasörleri (Rule.TargetFolder ya da masaüstündeki adı; FolderName.Equal ile karşılaştırılır).</summary>
    public List<string> MoveFolders { get; set; } = [];

    /// <summary>Eklenecek araçlar: WidgetCatalog anahtarları (Clock, Date, Note, Checklist, Launcher).</summary>
    public HashSet<string> Tools { get; } = [];

    /// <summary>Windows ile başlatma. null = değiştirilmez (durum bilinmiyor ya da ilk okunan durumla aynı).</summary>
    public bool? StartWithWindows { get; set; }
}

/// <summary>
/// Karşılamanın 2. adımındaki klasör seçeneği. Folder: masaüstündeki gerçek ad, yoksa kuraldaki ad. FilesNow: şu an
/// masaüstünde duran ve klasör olsaydı oraya gidecek dosya sayısı. Extensions: "pdf" ya da "jpg, jpeg, png".
/// </summary>
public sealed record FolderChoice(string Folder, bool Exists, int FilesNow, bool DefaultOn, string Extensions);

/// <summary>Karşılamanın (ilk açılış ve yeniden kurulum) saf hesapları: dosya sistemine ve arayüze dokunmaz.</summary>
public static class Onboarding
{
    /// <summary>Önizleme kutusunda en çok kaç "{n} dosya → {klasör}" satırı gösterilir.</summary>
    public const int PreviewLineLimit = 4;

    /// <summary>
    /// Açılışta, karşılama tamamlanmadıysa (yeni kullanıcı): otomatik taşıma kapatılır, karşılamada onay verilene dek hiçbir
    /// dosya taşınmaz; eski addan yeni ada geçiş balonu da gösterilmiş sayılır (yalnızca eski sürümden gelenler içindir).
    /// desktopFolders verilirse ve kurallar hâlâ hazır kurallarsa, arayüz dilindeki hazır kurallar masaüstünde zaten olan
    /// klasörlere uyarlanır (<see cref="Rule.Defaults(Lang, IEnumerable{string})"/>: İngilizce Windows'ta "Resimler"
    /// klasörü olan kullanıcıya "Pictures" açılmasın). Ayar değiştiyse true döner (çağıran kaydeder).
    /// </summary>
    public static bool PrepareNewUser(AppSettings settings, IEnumerable<string>? desktopFolders = null)
    {
        if (settings.FirstRunDone) return false;
        settings.Paused = true;
        settings.RenameNoticeShown = true;
        // İzleyici kuralları arka planda okur: liste yerinde değişmez, yenisi atanır.
        if (desktopFolders is not null && Rule.AreDefaults(settings.Rules))
            settings.Rules = Rule.Defaults(L.Current, desktopFolders);
        return true;
    }

    /// <summary>
    /// Kural klasörü başına bir seçenek, kuralların sırasıyla. Adı boş ya da uzantısız kurallar gösterilmez; aynı klasörü
    /// hedefleyen kurallar (FolderName.Equal) tek seçenekte birleşir. Varsayılan işaret: kural etkin ve (klasör masaüstünde
    /// var ya da şu an o klasöre gidecek en az bir dosya var). Kapalı kuralın klasörüne dosya gitmez (FilesNow 0).
    /// createMissing ("Klasör yoksa oluştur" açık): etkin kural klasörü olmasa da zaten çalışır, bu yüzden işaretli gelir
    /// (karşılamayı geçip gitmek çalışan kuralı kapatmasın).
    /// </summary>
    public static List<FolderChoice> FolderChoices(IReadOnlyList<Rule> rules, IEnumerable<string> existingFolders, IReadOnlyList<DesktopFile> files,
        bool createMissing = false)
    {
        var existing = existingFolders.ToList();
        // Her kural klasörü varmış gibi: şu an hangi dosya nereye giderdi?
        var moves = MovePlan.Build(files, existing, rules, rules.Select(r => r.TargetFolder));

        var groups = new List<List<Rule>>();
        foreach (var rule in rules.Where(IsChoice))
        {
            if (groups.FirstOrDefault(g => FolderName.Equal(g[0].TargetFolder, rule.TargetFolder)) is { } group) group.Add(rule);
            else groups.Add([rule]);
        }

        var choices = new List<FolderChoice>();
        foreach (var group in groups)
        {
            var real = existing.FirstOrDefault(f => FolderName.Equal(f, group[0].TargetFolder));
            var folder = real ?? group[0].TargetFolder.Trim();
            var filesNow = moves.Count(m => FolderName.Equal(m.Folder, folder));
            var enabled = group.Any(r => r.Enabled);
            var extensions = group.SelectMany(r => r.Extensions).Select(Rule.NormalizeExtension)
                .Where(e => e.Length > 0).Distinct().ToList();
            choices.Add(new FolderChoice(folder, real is not null, filesNow, enabled && (real is not null || filesNow > 0 || createMissing),
                string.Join(", ", extensions)));
        }
        return choices;
    }

    /// <summary>
    /// Seçime göre kuralların yeni listesi (kurallar kopyalanır; verilen liste ve kurallar değişmez, çünkü izleyici arka
    /// planda okur): seçili klasörün kuralı açılır; seçili değil ve klasör masaüstünde var → kapanır (o klasöre dosya
    /// gitmesin); seçili değil ve klasör yok → olduğu gibi kalır (klasörü olmayan kural dosyaya dokunmaz). createMissing
    /// ("Klasör yoksa oluştur" açık): klasörü olmayan kural da taşıdığı için seçilmeyen her kural kapanır.
    /// Seçenek olmayan kurallar (adsız/uzantısız) aynen kopyalanır.
    /// </summary>
    public static List<Rule> ApplyFolderSelection(IReadOnlyList<Rule> rules, IEnumerable<string> selected, IEnumerable<string> existingFolders,
        bool createMissing = false)
    {
        var chosen = selected.ToList();
        var existing = existingFolders.ToList();
        return rules.Select(rule =>
        {
            var copy = new Rule { TargetFolder = rule.TargetFolder, Extensions = [.. rule.Extensions], Enabled = rule.Enabled };
            if (!IsChoice(rule)) return copy;
            if (chosen.Any(s => FolderName.Equal(s, rule.TargetFolder))) copy.Enabled = true;
            else if (createMissing || existing.Any(f => FolderName.Equal(f, rule.TargetFolder))) copy.Enabled = false;
            return copy;
        }).ToList();
    }

    /// <summary>
    /// Karşılamanın önizlemesi: "Bitti"de uygulanacak kurallarla (<see cref="ApplyFolderSelection"/>) ve açılacak klasörlerle
    /// (<see cref="FoldersToCreate"/>) şu an masaüstündeki hangi dosya nereye taşınır? Taşıyıcının kararıyla aynıdır
    /// ("Klasör yoksa oluştur" dahil).
    /// </summary>
    public static List<PlannedMove> PreviewMoves(IReadOnlyList<Rule> rules, IEnumerable<string> selected, IEnumerable<string> existingFolders,
        IReadOnlyList<DesktopFile> files, bool createMissing)
    {
        var chosen = selected.ToList();
        var existing = existingFolders.ToList();
        var applied = ApplyFolderSelection(rules, chosen, existing, createMissing);
        return MovePlan.Build(files, existing, applied, FoldersToCreate(applied, chosen, existing, createMissing), createMissing);
    }

    /// <summary>
    /// Seçili olup masaüstünde olmayan kural klasörleri: kuralların sırasıyla, tekrarsız, adları kırpılmış. createMissing
    /// ("Klasör yoksa oluştur" açık): hiçbiri; taşıyıcı klasörü ilk dosya gelince açar (boş klasör kalabalığı olmasın).
    /// </summary>
    public static List<string> FoldersToCreate(IReadOnlyList<Rule> rules, IEnumerable<string> selected, IEnumerable<string> existingFolders,
        bool createMissing = false)
    {
        if (createMissing) return [];
        var chosen = selected.ToList();
        var existing = existingFolders.ToList();
        var create = new List<string>();
        foreach (var rule in rules.Where(IsChoice))
        {
            var name = rule.TargetFolder.Trim();
            if (!chosen.Any(s => FolderName.Equal(s, name))) continue;
            if (existing.Any(f => FolderName.Equal(f, name)) || create.Any(c => FolderName.Equal(c, name))) continue;
            create.Add(name);
        }
        return create;
    }

    /// <summary>
    /// 1. adımdaki "Evet, bölmelere ayır" kartının açıklaması: hangi bölmeler eklenecek?
    /// Ör. "Klasörler, Kısayollar ve Dosyalar için birer bölme eklenir; PDF klasörü için de bir bölme."
    /// </summary>
    public static string FencePlanText(IReadOnlyList<StarterFence> plan)
    {
        var kinds = plan.Where(p => p.Folder is null).Select(p => p.Label).ToList();
        var folders = plan.Where(p => p.Folder is not null).Select(p => p.Label).ToList();
        if (kinds.Count == 0 && folders.Count == 0) return "Bölmelerin zaten hazır; yeni bölme eklenmez.";

        if (kinds.Count == 0)
            return folders.Count == 1
                ? $"{folders[0]} klasörü için bir bölme eklenir."
                : $"{L.Join(folders)} klasörleri için birer bölme eklenir.";

        var head = $"{L.Join(kinds)} için {(kinds.Count == 1 ? "bir" : "birer")} bölme eklenir";
        return folders.Count switch
        {
            0 => head + ".",
            1 => $"{head}; {folders[0]} klasörü için de bir bölme.",
            _ => $"{head}; {L.Join(folders)} klasörleri için de birer bölme.",
        };
    }

    /// <summary>
    /// Önizleme satırları: klasör başına "{n} dosya → {klasör}" (çoktan aza, en çok <see cref="PreviewLineLimit"/> satır);
    /// fazlası tek satırda "+ {m} dosya daha, {k} klasöre".
    /// </summary>
    public static List<string> MovePreviewLines(IReadOnlyList<PlannedMove> moves)
    {
        var groups = MovePlan.ByFolder(moves);
        var lines = groups.Take(PreviewLineLimit).Select(g => $"{g.Count} dosya → {g.Folder}").ToList();
        if (groups.Count > PreviewLineLimit)
        {
            var rest = groups.Skip(PreviewLineLimit).ToList();
            lines.Add($"+ {rest.Sum(g => g.Count)} dosya daha, {rest.Count} klasöre");
        }
        return lines;
    }

    /// <summary>Sıralı birleştirme ("A, B ve C" / "A, B and C"): <see cref="L.Join"/> (eski ad; yeni kodda L.Join kullan).</summary>
    public static string JoinTr(IReadOnlyList<string> items) => L.Join(items);

    /// <summary>Karşılamada seçenek olarak gösterilen kural: klasör adı ve en az bir uzantısı var.</summary>
    private static bool IsChoice(Rule rule) =>
        !string.IsNullOrWhiteSpace(rule.TargetFolder) && rule.Extensions.Any(e => Rule.NormalizeExtension(e).Length > 0);
}
