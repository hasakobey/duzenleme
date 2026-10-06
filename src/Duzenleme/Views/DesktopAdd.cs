using System.IO;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>
/// "Masaüstünden ekle": masaüstündeki bir klasör, kısayol ya da dosya tek tıkla widget olur. Klasör kendi bölmesini alır
/// (<see cref="WidgetCatalog.AddFolder"/>: masaüstündeki klasör adıyla, ortak masaüstündeki portal), kısayollar ve dosyalar
/// en öndeki kısayol kutusuna eklenir (yoksa yeni kutu açılır). Kutuya ekleme tek kapıdan geçer (<see cref="LauncherView.AddFromDesktop"/>
/// → LauncherEdits.AddItems → BoxMover.Added: "masaüstünden kalksın mı?" sorusu yalnızca orada). Öneriler masaüstlerinin
/// anlık görüntüsünden gelir (arayüzde diske dokunulmaz; kararlar <see cref="DesktopPicks"/>'te). Kullanan yerler: "Widget
/// ekle" penceresinin "Masaüstünden" satırı, "Tümü…" sayfası ve pencereye/Widget'lar sayfasına bırakılan dosyalar.
/// </summary>
internal static class DesktopAdd
{
    /// <summary>Önerilecek masaüstü öğeleri, sıralı. Anlık görüntü henüz hazır değilse o masaüstü atlanır.</summary>
    public static List<DesktopPick> Current()
    {
        var entries = new List<DirEntry>();
        foreach (var dir in AppHost.DesktopDirectories)
            if (AppHost.Snapshots?.Find(dir) is { State: SnapshotState.Ready } snapshot) entries.AddRange(snapshot.Entries);
        var excluded = DesktopPicks.ShownFolders(AppHost.Settings.Widgets, AppHost.DesktopDirectory);
        excluded.Add(BoxMover.Root);
        return DesktopPicks.From(entries, AppHost.Organizer?.Pinned ?? new HashSet<string>(), excluded);
    }

    /// <summary>
    /// Bırakılan yollar (FileDrop): klasör mü, hâlâ var mı arka planda bakılır; sonra <see cref="Add"/> ile aynı yol. Widget'tan
    /// gelen sürükleme (WidgetDrag) burada işlenmez.
    /// </summary>
    public static async void AddDropped(IReadOnlyList<string> paths, NativeMethods.POINT? near)
    {
        var picks = await Task.Run(() => paths.Select(path =>
        {
            try
            {
                if (Directory.Exists(path)) return DesktopPicks.Of(path, isDirectory: true);
                return File.Exists(path) ? DesktopPicks.Of(path, isDirectory: false) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
        }).OfType<DesktopPick>().ToList());
        if (picks.Count > 0) Add(picks, near);
    }

    /// <summary>
    /// Seçilenleri ekler: her klasör bir bölme (en çok <see cref="DesktopPicks.MaxPanels"/>), öbürleri tek kutuya. Widget'lar
    /// ayardaki yere (<paramref name="near"/>'ın ekranında) gelir; var olan kutu öne gelip bir kez nabız atar. Ana pencere açıksa
    /// şeritte "eklendi" + "Geri al"; değilse ilk eklemelerde tepside nereye geldiği.
    /// </summary>
    public static void Add(IReadOnlyList<DesktopPick> picks, NativeMethods.POINT? near)
    {
        var plan = DesktopPicks.Plan(picks);
        var created = new List<string>();
        foreach (var folder in plan.Panels)
        {
            if (near is { } point) AppHost.Widgets.PlacementHint = WidgetPlacement.FromSettingsAt(point);
            if (WidgetCatalog.AddFolder(folder.Path, null) is { } config && AppHost.Settings.Widgets.Any(w => w.Id == config.Id))
                created.Add(config.Id);
            else AppHost.Widgets.PlacementHint = null;
        }
        if (plan.BoxItems.Count == 0)
        {
            Announce(created, null, [], plan.SkippedPanels);
            return;
        }

        var paths = plan.BoxItems.Select(p => p.Path).ToList();
        if (DesktopPicks.TargetBox(AppHost.Settings.Widgets) is { } box && AppHost.Widgets.ViewOf(box.Id) is LauncherView view)
        {
            var boxId = box.Id;
            AppHost.Widgets.Reveal(boxId);
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in plan.BoxItems) names.TryAdd(item.Path, item.Name);
            view.AddFromDesktop(paths, added => Announce(created, boxId, added, plan.SkippedPanels,
                added.Count == 1 && names.TryGetValue(added[0], out var name) ? name : null));
            return;
        }

        // Kutu yok: öğelerle yeni kutu (tohumun sekmeleri: hepsi dosyaysa "Dosyalar", değilse "Uygulamalar" açık gelir).
        var seed = WidgetSeeds.Create(WidgetSeeds.Launcher)!;
        seed.ActiveTab = Math.Clamp(DesktopPicks.NewBoxTab(plan.BoxItems), 0, seed.Tabs.Count - 1);
        if (near is { } at) AppHost.Widgets.PlacementHint = WidgetPlacement.FromSettingsAt(at);
        var added = AppHost.Widgets.AddSeed(seed);
        if (!AppHost.Settings.Widgets.Any(w => w.Id == added.Id) || AppHost.Widgets.ViewOf(added.Id) is not LauncherView fresh)
        {
            Announce(created, null, [], plan.SkippedPanels);
            return;
        }
        created.Add(added.Id);
        fresh.AddFromDesktop(paths, _ => Announce(created, null, [], plan.SkippedPanels));
    }

    /// <summary>
    /// Ne eklendiğini söyler. Ana pencere gizliyse yalnızca yeni widget'ın yeri (ilk üç eklemede tepside); açıksa şeritte
    /// tek cümle (birkaç iş birden olduysa cümleler art arda) ve hepsini birden geri alan "Geri al".
    /// </summary>
    /// <param name="single">Kutuya tek öğe eklendiyse adı ("Paint" kısayol kutusuna eklendi.).</param>
    private static void Announce(IReadOnlyList<string> created, string? boxId, IReadOnlyList<string> boxAdded, int skipped,
        string? single = null)
    {
        if (!Notice.MainWindowShown)
        {
            if (created.Count > 0 && AppHost.Settings.Widgets.FirstOrDefault(w => w.Id == created[0]) is { } first)
                AppHost.ShowNewWidgetHint(first);
            return;
        }
        var sentences = new List<string>();
        var live = created.Select(id => AppHost.Settings.Widgets.FirstOrDefault(w => w.Id == id)).OfType<WidgetConfig>().ToList();
        if (live.Count == 1)
            sentences.Add(L.F("{0} eklendi.", WidgetText.DisplayName(live[0])));
        else if (live.Count > 1)
        {
            // Birkaç bölme ve (kutu yoksa) yeni kutu: türüyle söylenir.
            var panels = live.Count(w => w.Kind == WidgetKind.Fence);
            if (panels > 0) sentences.Add(L.P(panels, "{0} bölme eklendi."));
            if (live.Count > panels) sentences.Add(L.T("Yeni kısayol kutusu eklendi."));
        }
        if (boxId is not null)
            sentences.Add(single is not null ? L.F("\"{0}\" kısayol kutusuna eklendi.", single)
                : boxAdded.Count > 0 ? L.P(boxAdded.Count, "{0} öğe kısayol kutusuna eklendi.")
                : L.T("Seçtiklerin zaten kısayol kutusunda."));
        if (skipped > 0)
            sentences.Add(L.P(skipped, "{0} klasör eklenmedi: bir seferde en çok {1} bölme eklenir.", DesktopPicks.MaxPanels));
        if (sentences.Count == 0) return;
        var text = string.Join(" ", sentences);
        if (created.Count == 0 && boxAdded.Count == 0)
        {
            Notice.Show(text, NoticeKind.Info);
            return;
        }
        Notice.Show(text, NoticeKind.Success, L.T("Geri al"), () => Undo(created, boxId, boxAdded));
    }

    /// <summary>
    /// "Geri al": kutuya eklenenler çıkar (taşınmışsa masaüstüne döner), eklenen widget'lar geri getirilebilir biçimde kalkar
    /// (şerit uzun kalabilir; o arada doldurulan kutu kaybolmasın). Tek widget her zamanki "{0} kaldırıldı." + "Geri al" ile.
    /// </summary>
    private static void Undo(IReadOnlyList<string> created, string? boxId, IReadOnlyList<string> boxAdded)
    {
        var taken = boxId is not null && boxAdded.Count > 0 && AppHost.Widgets.ViewOf(boxId) is LauncherView view
            ? view.TakeBackFromDesktop(boxAdded) : 0;
        var live = created.Where(id => AppHost.Settings.Widgets.Any(w => w.Id == id)).ToList();
        if (live.Count == 1)
        {
            WidgetCatalog.UndoAdd(live[0]);
            return;
        }
        if (live.Count > 1)
        {
            foreach (var id in live) AppHost.Widgets.RemoveWithUndo(id, notify: false);
            Notice.Show(L.P(live.Count, "{0} widget kaldırıldı."), NoticeKind.Info, L.T("Geri al"), () =>
            {
                foreach (var id in live) AppHost.Widgets.UndoRemove(id);
            });
            return;
        }
        if (taken > 0) Notice.Show(L.P(taken, "{0} öğe kısayol kutusundan çıkarıldı."), NoticeKind.Info);
    }
}

/// <summary>
/// Dışarıdan (masaüstü, Gezgin) bırakılan dosyaları "Masaüstünden ekle"ye çeviren ortak sürükle-bırak kararı ("Widget ekle"
/// penceresi ve Widget'lar sayfası). Yalnızca FileDrop; widget'tan gelen sürükleme (<see cref="WidgetDrag"/>) alınmaz. Etki
/// Link (izin yoksa Copy), asla Move: dosya yerinden oynamaz.
/// </summary>
internal static class DesktopDrop
{
    /// <summary>Bırakılırsa eklenecek yollar; uygun değilse null.</summary>
    public static string[]? PathsOf(System.Windows.DragEventArgs e) =>
        WidgetDrag.For(e.Data) is null && e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] { Length: > 0 } paths ? paths : null;

    /// <summary>İmlecin gösterdiği etki: uygunsa Link (ya da Copy), değilse None.</summary>
    public static System.Windows.DragDropEffects EffectFor(System.Windows.DragEventArgs e)
    {
        if (WidgetDrag.For(e.Data) is not null || !e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            return System.Windows.DragDropEffects.None;
        if (e.AllowedEffects.HasFlag(System.Windows.DragDropEffects.Link)) return System.Windows.DragDropEffects.Link;
        return e.AllowedEffects.HasFlag(System.Windows.DragDropEffects.Copy) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
    }
}
