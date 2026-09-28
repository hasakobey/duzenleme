using System.IO;

namespace Duzenleme.Core;

/// <summary>Bir bölmenin neyi göstereceği: masaüstü süzgeci ya da klasör (masaüstündeki klasörün adı ya da tam yol).</summary>
public readonly record struct FenceSource(DesktopFilter Filter, string? FolderName);

/// <summary>
/// Klasör portalları (masaüstü dışındaki klasörü gösteren bölmeler) için saf yardımcılar. Portalda
/// <see cref="WidgetConfig.FolderName"/> tam yoldur; masaüstündeki klasör yine yalnızca adıyla tutulur (2.0 da onu tanır,
/// "Klasörü oluştur" ve başlangıç bölmeleri adla eşleşir).
/// </summary>
public static class FolderPortal
{
    /// <summary>Windows'un bilinen klasörleri (portal kutucukları ve "Ne gösterilsin?" menüsü); kimlik FolderKnownId'de saklanır.</summary>
    public const string Downloads = "Downloads", Documents = "Documents", Pictures = "Pictures", Music = "Music",
        Videos = "Videos", Screenshots = "Screenshots";

    public static IReadOnlyList<string> KnownIds => [Downloads, Documents, Pictures, Music, Videos, Screenshots];

    /// <summary>Bilinen klasörün arayüzdeki adı (portal eklenirken başlık olarak bir kez yazılır).</summary>
    public static string KnownName(string id) => id switch
    {
        Downloads => L.T("İndirilenler"),
        Documents => L.T("Belgeler"),
        Pictures => L.T("Resimler"),
        Music => L.T("Müzik"),
        Videos => L.T("Videolar"),
        Screenshots => L.T("Ekran görüntüleri"),
        _ => id,
    };

    /// <summary>
    /// Seçilen klasörü bölmenin kaynağına çevirir: masaüstünün kendisi "Tümü", masaüstündeki bir klasör yalnızca adı
    /// (klasik klasör bölmesi), başka her yer tam yol (portal). Yollar diske bakılmadan karşılaştırılır.
    /// </summary>
    public static FenceSource Normalize(string picked, string desktopDirectory)
    {
        var path = Trim(picked);
        var desktop = Trim(desktopDirectory);
        if (string.Equals(path, desktop, StringComparison.OrdinalIgnoreCase)) return new(DesktopFilter.All, null);
        if (string.Equals(Path.GetDirectoryName(path), desktop, StringComparison.OrdinalIgnoreCase))
            return new(DesktopFilter.None, Path.GetFileName(path));
        return new(DesktopFilter.None, path);
    }

    /// <summary>Klasörün görünen adı: son parçası; sürücü kökünde kökün kendisi ("D:\").</summary>
    public static string DisplayName(string path)
    {
        var trimmed = Trim(path);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? path : name;
    }

    /// <summary>
    /// Uzun yolun kısa gösterimi (dar bölmede okunsun): kök, "…" ve son iki parça ("C:\…\Proje\Çizimler"). Kısaysa olduğu gibi.
    /// </summary>
    public static string ShortPath(string path, int maxLength = 48)
    {
        var trimmed = Trim(path);
        if (trimmed.Length <= maxLength) return trimmed;
        var root = Path.GetPathRoot(trimmed) ?? "";
        var parts = trimmed[root.Length..].Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 2 ? trimmed : Path.Combine(root, "…", parts[^2], parts[^1]);
    }

    /// <summary>Aynı klasör mü (büyük/küçük harf ve sondaki ayraç önemsiz)?</summary>
    public static bool SamePath(string a, string b) => string.Equals(Trim(a), Trim(b), StringComparison.OrdinalIgnoreCase);

    private static string Trim(string path)
    {
        var trimmed = path.Trim();
        // "C:\" kökü ayraçsız "C:" olursa göreli yol anlamına gelir: kökte ayraç kalır.
        return trimmed.Length > 3 ? Path.TrimEndingDirectorySeparator(trimmed) : trimmed;
    }
}

/// <summary>Bölme sıralaması (saf; testlenir). <see cref="WidgetConfig.SortBy"/> 2.0'ın <see cref="FenceSort"/>'unu ezer.</summary>
public static class FenceOrder
{
    public const string Size = "size", Oldest = "oldest", Manual = "manual";

    /// <summary>Tanınan SortBy; bilinmeyen (daha yeni sürümün) değer yok sayılır, Sort kullanılır.</summary>
    public static string? Normalize(string? sortBy) => sortBy is Size or Oldest or Manual ? sortBy : null;

    /// <summary>SortBy seçilince 2.0 için Sort'a yazılan en yakın değer (2.0 SortBy'ı tanımaz, bununla sıralar).</summary>
    public static FenceSort Legacy(string sortBy) => sortBy switch
    {
        Size => FenceSort.Type,
        Manual => FenceSort.Name,
        _ => FenceSort.Newest,
    };

    /// <summary>
    /// Sıralar. Ada ve türe göre sıralamada klasörler önce gelir. Elle sırada listede olanlar kayıtlı sırasıyla, yeni
    /// öğeler (listede yok) eskiden yeniye sona eklenir. Boyuta göre: klasörler önce (ada göre), sonra dosyalar büyükten küçüğe.
    /// </summary>
    public static IEnumerable<DirEntry> Sort(IEnumerable<DirEntry> entries, FenceSort sort, string? sortBy,
        IReadOnlyList<string>? order, Func<DirEntry, string> displayName, StringComparer byName)
    {
        switch (Normalize(sortBy))
        {
            case Size:
                return entries.OrderBy(e => e.IsDirectory ? 0 : 1).ThenByDescending(e => e.IsDirectory ? 0 : e.Length)
                    .ThenBy(displayName, byName);
            case Oldest:
                return entries.OrderBy(e => e.LastWriteUtc);
            case Manual:
                var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (order is not null)
                    for (var i = 0; i < order.Count; i++) index.TryAdd(order[i], i);
                return entries.OrderBy(e => index.TryGetValue(e.Path, out var i) ? i : int.MaxValue)
                    .ThenBy(e => e.LastWriteUtc);
        }
        return sort switch
        {
            FenceSort.Name => entries.OrderBy(e => e.IsDirectory ? 0 : 1).ThenBy(displayName, byName),
            FenceSort.Type => entries.OrderBy(e => e.IsDirectory ? "" : Path.GetExtension(e.Name).ToLowerInvariant()).ThenBy(e => e.Name, byName),
            _ => entries.OrderByDescending(e => e.LastWriteUtc),
        };
    }

    /// <summary>
    /// Elle sıralamada öğeyi taşır: <paramref name="shown"/> şu an görünen sıra, öğe <paramref name="before"/>'un önüne
    /// (null: sona) gider. Yeni sıra döner (yalnızca yollar; olmayanlar sonra budanır).
    /// </summary>
    public static List<string> Move(IReadOnlyList<string> shown, IReadOnlyCollection<string> moving, string? before)
    {
        var set = new HashSet<string>(moving, StringComparer.OrdinalIgnoreCase);
        var rest = shown.Where(p => !set.Contains(p)).ToList();
        var ordered = shown.Where(set.Contains).ToList();
        foreach (var path in moving) if (!ordered.Contains(path, StringComparer.OrdinalIgnoreCase)) ordered.Add(path);
        var at = before is null ? rest.Count : rest.FindIndex(p => string.Equals(p, before, StringComparison.OrdinalIgnoreCase));
        if (at < 0) at = rest.Count;
        rest.InsertRange(at, ordered);
        return rest;
    }
}
