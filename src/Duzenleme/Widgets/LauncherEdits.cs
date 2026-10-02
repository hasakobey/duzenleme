using System.IO;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Kısayol kutusunun (<see cref="LauncherView"/>) ve halka menünün (<see cref="RingWidgetView"/>) ortak öğe düzenlemeleri:
/// ekleme (varlık denetimi arka planda; "Kutulara eklediklerim masaüstünden kalksın" açıksa masaüstündekiler kutunun
/// klasörüne taşınır), çıkarma ("Geri al" için son çıkarılan tutulur; masaüstünden taşınmış öğe başka kutuda yoksa
/// masaüstüne döner) ve başka bir widget'a sürüklenince çıkarma. Her değişiklik kaydedilir, kutudaki masaüstü dosyaları
/// kurallardan korunur (<see cref="AppHost.RefreshPinnedPaths"/>) ve görünüm yeniden çizilir. Sıra yalnızca
/// <see cref="LauncherEntries"/> ile yazılır: halkanın yol olmayan öğeleri (Actions) yollarla birlikte yerinde kalır.
/// Ayarı değiştiren saf bölümler (<see cref="Take"/>, <see cref="Put"/>, <see cref="Insert"/>) testlenir.
/// </summary>
internal sealed class LauncherEdits(WidgetConfig config, Action render)
{
    /// <summary>Çıkarılan öğe: sekmesi, görünen sırası, kendisi ve (başka sekmede yoksa) kutuda verilmiş adı/simgesi.</summary>
    public sealed record Removed(LauncherTab Tab, int Index, LauncherEntry Entry, ItemLook? Look);

    /// <summary>Son çıkarılan öğe (menüdeki ve bildirimdeki "Geri al" için); yoksa null.</summary>
    public Removed? LastRemoved { get; private set; }

    /// <summary>Değiştirir, kaydettirir, kutudaki masaüstü dosyalarını kurallardan korur ve yeniden çizer.</summary>
    public void Change(Action change)
    {
        change();
        AppHost.SaveSettings();
        // Kutudaki masaüstü dosyaları kurallarla taşınmasın (bkz. DesktopOrganizer.Pinned).
        AppHost.RefreshPinnedPaths();
        render();
    }

    /// <summary>
    /// Sekmenin <paramref name="index"/>. öğesini çıkarır ("Geri al" için tutulur). Bulunamazsa null; yoksa öğe masaüstünden
    /// kutuya taşınmışsa ve artık hiçbir kutuda değilse (masaüstüne geri konuyor) true, değilse false.
    /// </summary>
    public bool? Remove(LauncherTab tab, int index)
    {
        if (index < 0 || index >= LauncherEntries.Count(tab)) return null;
        var path = LauncherEntries.Ordered(tab)[index].Path;
        var moved = path is not null && BoxMover.IsMoved(path);
        Removed? removed = null;
        Change(() => removed = Take(config, tab, index));
        if (removed is null) return null;
        LastRemoved = removed;
        if (!moved) return false;
        BoxMover.Reconcile();
        return !BoxPlan.Referenced(AppHost.Settings.Widgets).Contains(path!);
    }

    /// <summary>
    /// Son çıkarılanı yerine koyar (sekmesi hâlâ varsa). Öğe bu arada masaüstüne geri konduysa kutu onu yeniden bulur (kip
    /// açıksa yeniden taşınır). Koyduysa true.
    /// </summary>
    public bool UndoRemove()
    {
        if (LastRemoved is not { } last || !config.Tabs.Contains(last.Tab)) return false;
        LastRemoved = null;
        Change(() => Put(config, last));
        if (last.Entry.IsPath) BoxMover.Reconcile();
        return true;
    }

    /// <summary>"Geri al" artık sunulmaz (ör. öğenin sekmesi silindi).</summary>
    public void ForgetRemoved() => LastRemoved = null;

    /// <summary>
    /// Yolları sekmeye ekler (varlık denetimi arka planda; sekmede zaten olan eklenmez). <paramref name="at"/> verilirse o
    /// sıraya (halkada bırakılan yer), yoksa sona. "Kutulara eklediklerim masaüstünden kalksın" açıksa masaüstündekiler
    /// kutunun klasörüne taşınır.
    /// </summary>
    /// <param name="target">Eklenecek sekme; denetim bittiğinde bu sekme silinmişse yeniden sorulur.</param>
    /// <param name="look">Başka kutudan taşınan öğenin orada verilmiş adı ve simgesi.</param>
    /// <param name="then">Ekleme bittikten sonra (ör. kaynak kutudan çıkarmak): öğe önce burada olmalı, yoksa kutunun
    /// klasörüne taşınmış öğe arada masaüstüne geri konur.</param>
    /// <param name="duplicate">Hiçbiri eklenemedi çünkü hepsi zaten sekmedeydi (halka "zaten var" der).</param>
    /// <param name="names">Bırakılan bağlantının sayfa başlığı, uygulamanın adı (yola göre; öğenin adı yoksa yazılır).</param>
    public async void AddItems(Func<LauncherTab> target, IReadOnlyList<string> paths, ItemLook? look = null, Action? then = null,
        int? at = null, Action? duplicate = null, IReadOnlyDictionary<string, string>? names = null)
    {
        var tab = target();
        // Başlat menüsü uygulaması diskte değildir (kabuk nesnesi): denetlenmez.
        var existing = await Task.Run(() => paths.Where(p => StartApps.IsApp(p) || File.Exists(p) || Directory.Exists(p)).ToList());
        if (!config.Tabs.Contains(tab)) tab = target();
        var added = new List<string>();
        Change(() => added = Insert(config, tab, existing, look, at, names));
        if (existing.Count > 0) then?.Invoke();
        if (existing.Count > 0 && added.Count == 0) duplicate?.Invoke();
        if (added.Count > 0 && BoxMover.Active) BoxMover.Claim(config, added, justAdded: true);
    }

    /// <summary>
    /// Öğe sürüklenerek başka bir widget'a taşındı: bu sekmeden çıkar (başka sekmede de yoksa adı ve simgesi de). Kutunun
    /// klasörüne taşınmış öğe başka kutuda yoksa masaüstüne döner (BoxMover.Reconcile).
    /// </summary>
    public void TakeOut(LauncherTab tab, string path)
    {
        if (!config.Tabs.Contains(tab)) return;
        var index = LauncherEntries.IndexOfPath(tab, path);
        if (index < 0) return;
        Change(() => Take(config, tab, index));
        BoxMover.Reconcile();
    }

    // ---------------------------------------------------------------- saf bölümler (testlenir)

    /// <summary>Yol başka bir sekmede de duruyor mu (adı ve simgesi orada kalır)?</summary>
    public static bool Elsewhere(WidgetConfig config, LauncherTab tab, string path) =>
        config.Tabs.Where(t => t != tab).Any(t => t.Items.Contains(path, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Sekmenin <paramref name="index"/>. öğesini çıkarır. Yolsa ve başka sekmede yoksa kutudaki adı ve simgesi de silinir
    /// (çıkarılan kayıtta tutulur). Sıra yoksa null.
    /// </summary>
    public static Removed? Take(WidgetConfig config, LauncherTab tab, int index)
    {
        if (index < 0 || index >= LauncherEntries.Count(tab)) return null;
        var entry = LauncherEntries.Ordered(tab)[index];
        var elsewhere = entry.Path is { } p && Elsewhere(config, tab, p);
        var look = entry.Path is { } path && !elsewhere ? ItemLooks.Get(config, path) : null;
        LauncherEntries.RemoveAt(tab, index);
        if (entry.Path is { } removed && !elsewhere) ItemLooks.Reset(config, removed);
        return new Removed(tab, index, entry, look);
    }

    /// <summary>
    /// Çıkarılanı aynı sıraya (kısılır) geri koyar, adını ve simgesini geri verir. Zaten sekmedeyse eklenmez ve o arada verilmiş
    /// ad/simge ezilmez.
    /// </summary>
    public static bool Put(WidgetConfig config, Removed removed)
    {
        var at = LauncherEntries.Insert(removed.Tab, Math.Min(removed.Index, LauncherEntries.Count(removed.Tab)), removed.Entry);
        if (at < 0) return false;
        if (removed.Entry.Path is { } path) ItemLooks.Restore(config, path, removed.Look);
        return true;
    }

    /// <summary>Silinen sekme: kendisi, yeri, silinmeden önceki etkin sekme ve onunla silinen ad/simge kayıtları ("Geri al" için).</summary>
    public sealed record RemovedTab(LauncherTab Tab, int Index, int ActiveTab, IReadOnlyList<KeyValuePair<string, ItemLook>> Looks);

    /// <summary>
    /// <paramref name="index"/>. sekmeyi siler (son sekme silinmez). Başka sekmede kalmayan öğelerin adları ve simgeleri de
    /// gider (<paramref name="keepLooks"/> hariç: son çıkarılan öğe "Geri al" için tutulur). Silinemezse null.
    /// </summary>
    public static RemovedTab? TakeTab(WidgetConfig config, int index, IEnumerable<string>? keepLooks = null)
    {
        if (config.Tabs.Count <= 1 || index < 0 || index >= config.Tabs.Count) return null;
        var tab = config.Tabs[index];
        var active = config.ActiveTab;
        var before = config.ItemLooks?.ToList() ?? [];
        config.Tabs.RemoveAt(index);
        config.ActiveTab = Math.Clamp(config.ActiveTab, 0, config.Tabs.Count - 1);
        ItemLooks.Prune(config, keepLooks);
        var lost = before.Where(p => config.ItemLooks is not { } map || !map.ContainsKey(p.Key)).ToList();
        return new RemovedTab(tab, index, active, lost);
    }

    /// <summary>
    /// Silinen sekmeyi yerine (kısılır) koyar, etkin sekmeyi ve silinen adları/simgeleri geri verir; o arada verilmiş ad/simge
    /// ezilmez. Sekme zaten duruyorsa false.
    /// </summary>
    public static bool PutTab(WidgetConfig config, RemovedTab removed)
    {
        if (config.Tabs.Contains(removed.Tab)) return false;
        config.Tabs.Insert(Math.Min(removed.Index, config.Tabs.Count), removed.Tab);
        config.ActiveTab = Math.Clamp(removed.ActiveTab, 0, config.Tabs.Count - 1);
        foreach (var (path, look) in removed.Looks)
            if (ItemLooks.Get(config, path) is null) ItemLooks.Restore(config, path, look);
        return true;
    }

    /// <summary>
    /// Yolları sekmeye ekler: <paramref name="at"/> sırasından başlayarak (yoksa sona), sekmede olmayanları (harf duyarsız).
    /// <paramref name="look"/> verilirse kendi adı/simgesi olmayan öğeye verilir. Eklenenleri döner.
    /// </summary>
    public static List<string> Insert(WidgetConfig config, LauncherTab tab, IEnumerable<string> paths, ItemLook? look, int? at,
        IReadOnlyDictionary<string, string>? names = null)
    {
        var added = new List<string>();
        var next = at ?? int.MaxValue;
        foreach (var path in paths)
        {
            var index = LauncherEntries.Insert(tab, next, LauncherEntry.Of(path));
            if (index < 0) continue;
            if (at is not null) next = index + 1;
            added.Add(path);
            if (look is not null && ItemLooks.Get(config, path) is null) ItemLooks.Restore(config, path, look);
            else if (names is not null && names.TryGetValue(path, out var name) && ItemLooks.Get(config, path) is null)
                ItemLooks.Restore(config, path, new ItemLook { Name = name });
        }
        return added;
    }
}
