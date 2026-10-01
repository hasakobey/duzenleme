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
/// PathsBefore değerlerini yeniden yazar; eylem nesneleri (ve Extra'ları) korunur.
/// </summary>
public static class LauncherEntries
{
    /// <summary>Sekmedeki öğe sayısı (yollar + eylemler).</summary>
    public static int Count(LauncherTab tab) => tab.Items.Count + (tab.Actions?.Count ?? 0);

    /// <summary>Görünen sıra.</summary>
    public static List<LauncherEntry> Ordered(LauncherTab tab)
    {
        var result = new List<LauncherEntry>(Count(tab));
        var actions = tab.Actions is { Count: > 0 } list
            ? list.Select((a, i) => (Action: a, At: Math.Clamp(a.PathsBefore, 0, tab.Items.Count), Order: i))
                .OrderBy(x => x.At).ThenBy(x => x.Order).ToList()
            : [];
        var next = 0;
        for (var paths = 0; paths <= tab.Items.Count; paths++)
        {
            while (next < actions.Count && actions[next].At == paths) result.Add(LauncherEntry.Of(actions[next++].Action));
            if (paths < tab.Items.Count) result.Add(LauncherEntry.Of(tab.Items[paths]));
        }
        return result;
    }

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

    /// <summary>Öğeyi <paramref name="index"/> sırasına ekler (kısılır). Eklendiği sırayı döner.</summary>
    public static int Insert(LauncherTab tab, int index, LauncherEntry entry)
    {
        if (entry.Path is null && entry.Action is null) throw new ArgumentException("empty entry", nameof(entry));
        var list = Ordered(tab);
        index = Math.Clamp(index, 0, list.Count);
        list.Insert(index, entry);
        Write(tab, list);
        return index;
    }

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
    /// <paramref name="toIndex"/> sırasına girer (kısılır). Aynı sekmede <see cref="Move"/>. Taşıdıysa true.
    /// </summary>
    public static bool MoveTo(LauncherTab from, int fromIndex, LauncherTab to, int toIndex)
    {
        if (ReferenceEquals(from, to)) return Move(from, fromIndex, toIndex);
        if (RemoveAt(from, fromIndex) is not { } entry) return false;
        Insert(to, toIndex, entry);
        return true;
    }

    /// <summary>Yolun görünen sırası (harf duyarsız); yoksa -1.</summary>
    public static int IndexOfPath(LauncherTab tab, string path) =>
        Ordered(tab).FindIndex(e => e.Path is { } p && string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>Eylemin görünen sırası (kimliğiyle); yoksa -1.</summary>
    public static int IndexOfAction(LauncherTab tab, string id) =>
        Ordered(tab).FindIndex(e => e.Action is { } a && string.Equals(a.Id, id, StringComparison.Ordinal));
}
