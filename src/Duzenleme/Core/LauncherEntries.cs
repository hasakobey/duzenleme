namespace Duzenleme.Core;

/// <summary>Sekmedeki bir öğe: ya yol (<see cref="LauncherTab.Items"/>) ya da yol olmayan öğe (<see cref="LauncherTab.Actions"/>).</summary>
public readonly record struct LauncherEntry
{
    public string? Path { get; }
    public LauncherAction? Action { get; }

    public LauncherEntry(string path) => Path = path;
    public LauncherEntry(LauncherAction action) => Action = action;

    public bool IsPath => Path is not null;
    public bool IsAction => Action is not null;

    public static LauncherEntry Of(string path) => new(path);
    public static LauncherEntry Of(LauncherAction action) => new(action);
}

/// <summary>
/// Sekmenin yollarıyla yol olmayan öğelerinin tek sıralı listesi (saf; testlenir). Items yalnızca yol kalır (2.0/2.1,
/// BoxMover, yeniden adlandırma ve ItemLooks olduğu gibi çalışır); her eylem kendinden önceki yol sayısını
/// (<see cref="LauncherAction.PathsBefore"/>) taşır. Birleştirme kararlıdır: aynı PathsBefore'daki eylemler liste
/// sırasıyla, yolların arkasına girer. 2.1 kutuya yol ekleyip sildiyse (Actions'ı bilmeden) fazla PathsBefore sona kısılır;
/// eylemler en çok silinen yol sayısı kadar kayar, hiçbiri kaybolmaz. Taşı/ekle/sil Items'ın sırasını ve bütün
/// PathsBefore değerlerini yeniden yazar; eylem nesneleri (ve Extra'ları) korunur. Elle düzenlenmiş dosyadaki boş (null)
/// yol ve eylemler atlanır (bir sonraki yazmada düşer). Bir sekmede aynı yol (harf duyarsız) ve aynı eylem kimliği bir kez
/// bulunur: <see cref="Insert"/> ve <see cref="MoveTo"/> bunu korur.
/// </summary>
public static class LauncherEntries
{
    /// <summary>Sekmedeki öğe sayısı (yollar + eylemler).</summary>
    public static int Count(LauncherTab tab) => Paths(tab).Count + (tab.Actions?.Count(a => a is not null) ?? 0);

    /// <summary>Görünen sıra.</summary>
    public static List<LauncherEntry> Ordered(LauncherTab tab)
    {
        var paths = Paths(tab);
        var result = new List<LauncherEntry>(paths.Count + (tab.Actions?.Count ?? 0));
        var actions = tab.Actions is { Count: > 0 } list
            ? list.Where(a => a is not null)
                .Select((a, i) => (Action: a, At: Math.Clamp(a.PathsBefore, 0, paths.Count), Order: i))
                .OrderBy(x => x.At).ThenBy(x => x.Order).ToList()
            : [];
        var next = 0;
        for (var at = 0; at <= paths.Count; at++)
        {
            while (next < actions.Count && actions[next].At == at) result.Add(LauncherEntry.Of(actions[next++].Action));
            if (at < paths.Count) result.Add(LauncherEntry.Of(paths[at]));
        }
        return result;
    }

    // Boş yollar sayılmaz (PathsBefore da onlarsız sayılır).
    private static List<string> Paths(LauncherTab tab) =>
        tab.Items is { } items ? items.Where(p => p is not null).ToList() : [];

    /// <summary>
    /// Sırayı sekmeye yazar: Items yolların sırası, Actions eylemlerin sırası ve PathsBefore değerleri. Eylem kalmadıysa
    /// Actions null olur (yazılmaz). Listeler yeni atanır: yerinde değiştirilmez.
    /// </summary>
    public static void Write(LauncherTab tab, IReadOnlyList<LauncherEntry> entries)
    {
        var items = new List<string>();
        var actions = new List<LauncherAction>();
        foreach (var entry in entries)
        {
            if (entry.Path is { } path) items.Add(path);
            else if (entry.Action is { } action)
            {
                action.PathsBefore = items.Count;
                actions.Add(action);
            }
        }
        tab.Items = items;
        tab.Actions = actions.Count > 0 ? actions : null;
    }

    /// <summary>Öğeyi <paramref name="from"/> sırasından <paramref name="to"/> sırasına taşır (to, öğe çıkarıldıktan sonraki
    /// listede; kısılır). Değiştiyse true.</summary>
    public static bool Move(LauncherTab tab, int from, int to)
    {
        var list = Ordered(tab);
        if (from < 0 || from >= list.Count) return false;
        var entry = list[from];
        list.RemoveAt(from);
        to = Math.Clamp(to, 0, list.Count);
        if (to == from) return false;
        list.Insert(to, entry);
        Write(tab, list);
        return true;
    }

    /// <summary>
    /// Öğeyi <paramref name="index"/> sırasına ekler (kısılır) ve eklendiği sırayı döner. Sekmede zaten bulunan yol (harf
    /// duyarsız) ya da aynı eylem nesnesi eklenmez: -1. Kimliği sekmedeki bir eylemle çakışan (ya da boş) eylem yeni kimlik
    /// alır (ör. çoğaltılmış halkadan taşınan öğe): <see cref="IndexOfAction"/> tek sonuç versin.
    /// </summary>
    public static int Insert(LauncherTab tab, int index, LauncherEntry entry)
    {
        if (entry.Path is null && entry.Action is null) throw new ArgumentException("empty entry", nameof(entry));
        var list = Ordered(tab);
        if (Contains(list, entry)) return -1;
        if (entry.Action is { } action)
        {
            var ids = new HashSet<string>(list.Where(e => e.Action is not null).Select(e => e.Action!.Id), StringComparer.Ordinal);
            while (string.IsNullOrEmpty(action.Id) || ids.Contains(action.Id)) action.Id = LauncherAction.NewId();
        }
        index = Math.Clamp(index, 0, list.Count);
        list.Insert(index, entry);
        Write(tab, list);
        return index;
    }

    /// <summary>Sekmede aynı yol (harf duyarsız) ya da aynı eylem nesnesi var mı?</summary>
    public static bool Contains(LauncherTab tab, LauncherEntry entry) => Contains(Ordered(tab), entry);

    private static bool Contains(List<LauncherEntry> list, LauncherEntry entry) => entry.Path is { } path
        ? list.Exists(e => e.Path is { } p && string.Equals(p, path, StringComparison.OrdinalIgnoreCase))
        : list.Exists(e => ReferenceEquals(e.Action, entry.Action));

    /// <summary>Sıradaki öğeyi çıkarır ve döner; sıra yoksa null.</summary>
    public static LauncherEntry? RemoveAt(LauncherTab tab, int index)
    {
        var list = Ordered(tab);
        if (index < 0 || index >= list.Count) return null;
        var entry = list[index];
        list.RemoveAt(index);
        Write(tab, list);
        return entry;
    }

    /// <summary>
    /// Öğeyi başka bir sekmeye (alt halkaya) taşır: <paramref name="fromIndex"/> sırasından çıkar, hedefte
    /// <paramref name="toIndex"/> sırasına girer (kısılır). Aynı sekmede <see cref="Move"/>. Hedefte aynı yol zaten varsa
    /// hiçbir şey değişmez (öğe kaybolmasın): false. Taşıdıysa true.
    /// </summary>
    public static bool MoveTo(LauncherTab from, int fromIndex, LauncherTab to, int toIndex)
    {
        if (ReferenceEquals(from, to)) return Move(from, fromIndex, toIndex);
        var list = Ordered(from);
        if (fromIndex < 0 || fromIndex >= list.Count || Contains(to, list[fromIndex])) return false;
        if (RemoveAt(from, fromIndex) is not { } entry) return false;
        Insert(to, toIndex, entry);
        return true;
    }

    /// <summary>
    /// Yolları <paramref name="map"/> ile değiştirir (null: değişmez): kutuya taşındı, masaüstüne döndü, yeniden adlandırıldı,
    /// bağlantı yeni veri klasörüne çevrildi. Aynı yol iki kez kalırsa ilk görüneni kalır. Yol olmayan öğeler (halka eylemleri)
    /// yollarla birlikte yerinde kalır (PathsBefore yeniden yazılır). Değiştiyse true.
    /// </summary>
    public static bool Rewrite(LauncherTab tab, Func<string, string?> map)
    {
        var list = Ordered(tab);
        var changed = false;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Path is not { } path || map(path) is not { } moved || string.Equals(moved, path, StringComparison.Ordinal)) continue;
            list[i] = LauncherEntry.Of(moved);
            changed = true;
        }
        if (!changed) return false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        list.RemoveAll(e => e.Path is { } p && !seen.Add(p));
        Write(tab, list);
        return true;
    }

    /// <summary>Yolun görünen sırası (harf duyarsız); yoksa -1.</summary>
    public static int IndexOfPath(LauncherTab tab, string path) =>
        Ordered(tab).FindIndex(e => e.Path is { } p && string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>Eylemin görünen sırası (kimliğiyle); yoksa -1.</summary>
    public static int IndexOfAction(LauncherTab tab, string id) =>
        Ordered(tab).FindIndex(e => e.Action is { } a && string.Equals(a.Id, id, StringComparison.Ordinal));
}
