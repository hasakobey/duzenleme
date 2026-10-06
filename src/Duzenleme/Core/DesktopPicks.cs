using System.IO;

namespace Duzenleme.Core;

/// <summary>Masaüstündeki bir öğenin türü (yalnızca çalışma zamanı; asla yazılmaz).</summary>
public enum DesktopPickKind { Folder, Shortcut, File }

/// <summary>"Widget ekle"nin "Masaüstünden" bölümündeki bir öğe: tam yol, görünen ad (kısayolda uzantısız), türü ve son değişimi.</summary>
public sealed record DesktopPick(string Path, string Name, DesktopPickKind Kind, DateTime ChangedUtc)
{
    public bool IsFolder => Kind == DesktopPickKind.Folder;
}

/// <summary>
/// Seçilenlerin nasıl ekleneceği: her klasör kendi bölmesi (en çok <see cref="DesktopPicks.MaxPanels"/>; kalan sayısı
/// <see cref="SkippedPanels"/>), kısayollar ve dosyalar tek bir kısayol kutusuna.
/// </summary>
public sealed record DesktopAddPlan(IReadOnlyList<DesktopPick> Panels, IReadOnlyList<DesktopPick> BoxItems, int SkippedPanels);

/// <summary>
/// "Masaüstünden ekle"nin saf kararları: masaüstünün anlık görüntüsünden hangi öğeler önerilir (gizli/sistem, desktop.ini, yarım
/// indirme, kutudaki öğeler, zaten bölmesi olan klasörler ve NestDesk'in kutu klasörü hariç), hangi sırayla (kısayollar ve
/// klasörler önce, sonra dosyalar; her grupta en son değişen önce), seçilenler nereye gider (klasör → bölme, öbürleri → kutu)
/// ve "Geri al" kutudan neyi çıkarır. Diske dokunmaz; arayüz anlık görüntüyü verir.
/// </summary>
public static class DesktopPicks
{
    /// <summary>"Widget ekle"nin ilk sayfasındaki satırda gösterilen öğe sayısı (kalanı "Tümü…").</summary>
    public const int RowCount = 6;

    /// <summary>Bir seferde eklenen en çok bölme sayısı (masaüstü bölmelerle dolup taşmasın).</summary>
    public const int MaxPanels = 5;

    /// <summary>Kısayol gibi açılan dosya (adı uzantısız gösterilir): .lnk, .url, .exe, .appref-ms.</summary>
    public static bool IsShortcutName(string name) => DesktopItems.IsShortcut(name);

    /// <summary>Görünen ad: kısayol ve programda uzantısız (Gezgin gibi), öbürlerinde dosya adı.</summary>
    public static string DisplayName(string name, bool isDirectory) =>
        !isDirectory && IsShortcutName(name) ? Path.GetFileNameWithoutExtension(name) : name;

    /// <summary>Tek bir yoldan öğe (bırakılan dosyalar için; tür adına ve klasör olup olmadığına göre).</summary>
    public static DesktopPick Of(string path, bool isDirectory, DateTime changedUtc = default)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (string.IsNullOrEmpty(name)) name = path;
        var kind = isDirectory ? DesktopPickKind.Folder : IsShortcutName(name) ? DesktopPickKind.Shortcut : DesktopPickKind.File;
        return new DesktopPick(path, DisplayName(name, isDirectory), kind, changedUtc);
    }

    /// <summary>
    /// Önerilecek masaüstü öğeleri, sıralı (<see cref="Order"/>). Gizli/sistem öğeler, desktop.ini, yarım indirmeler,
    /// <paramref name="pinned"/> (kutularda duranlar) ve <paramref name="excluded"/> (bölmesi olan klasörler, kutu klasörü)
    /// atlanır. Aynı adlı öğe (kullanıcının ve ortak masaüstünde) bir kez, ilk geldiği yerden.
    /// </summary>
    public static List<DesktopPick> From(IEnumerable<DirEntry> entries, IReadOnlySet<string> pinned, IEnumerable<string> excluded)
    {
        var skip = new HashSet<string>(excluded.Where(p => !string.IsNullOrEmpty(p)).Select(Trim), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var picks = new List<DesktopPick>();
        foreach (var entry in entries)
        {
            if (entry.IsHiddenOrSystem) continue;
            if (entry.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
            if (!entry.IsDirectory && DesktopItems.IsPartialDownload(entry.Name)) continue;
            if (pinned.Contains(entry.Path) || skip.Contains(Trim(entry.Path))) continue;
            if (!seen.Add((entry.IsDirectory ? "d|" : "f|") + entry.Name)) continue;
            picks.Add(Of(entry.Path, entry.IsDirectory, entry.LastWriteUtc));
        }
        return Order(picks);
    }

    /// <summary>Kısayollar ve klasörler önce, sonra dosyalar; her grupta en son değişen önce, eşitse ada göre.</summary>
    public static List<DesktopPick> Order(IEnumerable<DesktopPick> picks) =>
        picks.OrderBy(p => p.Kind == DesktopPickKind.File ? 1 : 0)
            .ThenByDescending(p => p.ChangedUtc)
            .ThenBy(p => p.Name, L.Sorter)
            .ToList();

    /// <summary>Aramaya uyanlar (sırası korunur): her sözcük adın bir yerinde geçmeli (harf, aksan ve Türkçe harf duyarsız).</summary>
    public static IReadOnlyList<DesktopPick> Filter(IReadOnlyList<DesktopPick> picks, string? query)
    {
        var words = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return picks;
        return picks.Where(p => words.All(w => AppList.Matches(p.Name, w))).ToList();
    }

    /// <summary>Seçilenler: klasörler bölme (en çok <see cref="MaxPanels"/>), öbürleri kutu; aynı yol bir kez.</summary>
    public static DesktopAddPlan Plan(IEnumerable<DesktopPick> picks)
    {
        var unique = picks.DistinctBy(p => Trim(p.Path), StringComparer.OrdinalIgnoreCase).ToList();
        var folders = unique.Where(p => p.IsFolder).ToList();
        var items = unique.Where(p => !p.IsFolder).ToList();
        return new DesktopAddPlan(folders.Take(MaxPanels).ToList(), items, Math.Max(0, folders.Count - MaxPanels));
    }

    /// <summary>Zaten bir klasör bölmesinde (ya da portalda) gösterilen klasörlerin tam yolları.</summary>
    public static List<string> ShownFolders(IEnumerable<WidgetConfig> widgets, string desktopDirectory) =>
        widgets.Where(w => w.Kind == WidgetKind.Fence && w.Filter == DesktopFilter.None && !string.IsNullOrWhiteSpace(w.FolderName))
            .Select(w => Path.IsPathFullyQualified(w.FolderName!) ? w.FolderName! : Path.Combine(desktopDirectory, w.FolderName!))
            .ToList();

    /// <summary>Kısayolların ve dosyaların gideceği kutu: düz kısayol kutularından en öndeki (en son kullanılan); yoksa null.</summary>
    public static WidgetConfig? TargetBox(IEnumerable<WidgetConfig> widgets) =>
        widgets.Where(w => w.Kind == WidgetKind.Launcher && WidgetVariants.Of(w) is null).MaxBy(w => w.Z);

    /// <summary>
    /// Yeni kutuda öğelerin gireceği sekme (tohumda "Uygulamalar", "Dosyalar"): hepsi dosyaysa "Dosyalar" (1), değilse
    /// "Uygulamalar" (0). Var olan kutuda kullanıcının açık sekmesi kullanılır.
    /// </summary>
    public static int NewBoxTab(IEnumerable<DesktopPick> items)
    {
        var list = items.ToList();
        return list.Count > 0 && list.All(i => i.Kind == DesktopPickKind.File) ? 1 : 0;
    }

    /// <summary>
    /// "Geri al": kutuya eklenen yolların kutudaki şimdiki yerleri. Öğe o arada kutunun klasörüne taşındıysa
    /// (<see cref="BoxMove"/>, hâlâ orada) kutu yeni yolunu gösterir. Sekme ve yol çiftleri, kutudaki sırayla.
    /// </summary>
    public static List<(LauncherTab Tab, string Path)> TakeBack(WidgetConfig box, IReadOnlyCollection<string> added, IEnumerable<BoxMove> moves)
    {
        var wanted = new HashSet<string>(added.Select(Trim), StringComparer.OrdinalIgnoreCase);
        foreach (var move in moves)
            if (move.Active && wanted.Contains(Trim(move.Original))) wanted.Add(Trim(move.Current));
        var result = new List<(LauncherTab, string)>();
        foreach (var tab in box.Tabs)
            foreach (var path in tab.Items)
                if (path is not null && wanted.Contains(Trim(path))) result.Add((tab, path));
        return result;
    }

    private static string Trim(string path) => Path.TrimEndingDirectorySeparator(path);
}
