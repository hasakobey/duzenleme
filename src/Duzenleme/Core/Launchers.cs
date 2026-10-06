using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Kısayol kutuları ve halkalar ("launcher"): kutu kayıtlarının (<see cref="BoxPlan"/>), taşıyıcının korunan yollarının,
/// yeniden adlandırmanın ve halka kısayolunun baktığı tek liste. Bugün masaüstü widget'larındaki Kind=Launcher
/// yapılandırmaları; ayarlarda widget dışında duran bir launcher (2.2 merkez halka) yalnızca burada eklenir. Kutuları sayan
/// her kod bunu kullanır, <c>Settings.Widgets</c>'ı kendisi süzmez. Kutular hep kimlikle bulunur (<see cref="Find"/>): widget
/// kaldırılıp geri getirilince ya da düzen uygulanınca ayarın yeni bir kopyası gelir, eski nesne artık ayarda değildir.
/// Saf; testlenir.
/// </summary>
public static class Launchers
{
    /// <summary>
    /// Ayarlardaki bütün widget yapılandırmaları (launcher olmayanlar da) ve varsa NestDesk halkası (<see cref="AppSettings.Hub"/>,
    /// sonda): yolları güncelleyen kod (yeniden adlandırma) için.
    /// </summary>
    public static IEnumerable<WidgetConfig> Configs(AppSettings settings)
    {
        foreach (var widget in settings.Widgets)
            if (widget is not null) yield return widget;
        if (settings.Hub is { } hub) yield return hub;
    }

    /// <summary>Bütün kutular ve halkalar, ayardaki sırayla; NestDesk halkası (varsa) en sonda.</summary>
    public static IEnumerable<WidgetConfig> All(AppSettings settings) => Configs(settings).Where(w => w.Kind == WidgetKind.Launcher);

    /// <summary>Kimliği <paramref name="id"/> olan kutu ya da halka; yoksa (kaldırıldı) null.</summary>
    public static WidgetConfig? Find(AppSettings settings, string? id) =>
        string.IsNullOrEmpty(id) ? null : All(settings).FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.Ordinal));

    /// <summary>Kutulardaki ve halkalardaki bütün yollar (harf duyarsız).</summary>
    public static HashSet<string> Referenced(AppSettings settings) => BoxPlan.Referenced(All(settings));

    /// <summary>Kutularda duran masaüstü öğeleri (kurallar bunları taşımaz).</summary>
    public static HashSet<string> PinnedDesktopPaths(AppSettings settings, IReadOnlyList<string> desktopDirs) =>
        BoxPlan.PinnedDesktopPaths(All(settings), desktopDirs);

    /// <summary>Halka menüler (kısayolla açılan halkanın adayları), ayardaki sırayla.</summary>
    public static List<WidgetConfig> Rings(AppSettings settings) => RingTargets.All(All(settings));

    /// <summary>
    /// Yolları bütün kutularda ve halkalarda değiştirir (kutuya taşındı, masaüstüne döndü): sıra <see cref="LauncherEntries"/>
    /// ile yazılır (halka eylemleri yerinde kalır, aynı yol iki kez kalmaz), öğenin kutudaki adı ve simgesi yeni yola gider.
    /// <c>IsDirectory</c>: klasörse altındaki yollar da. Değiştiyse true.
    /// </summary>
    public static bool ReplacePaths(AppSettings settings, IReadOnlyCollection<(string From, string To, bool IsDirectory)> changes)
    {
        if (changes.Count == 0) return false;
        var changed = false;
        foreach (var box in All(settings))
        {
            foreach (var tab in box.Tabs)
                if (tab is not null)
                    changed |= LauncherEntries.Rewrite(tab, path => Map(path, changes));
            foreach (var (from, to, isDirectory) in changes) changed |= ItemLooks.Move(box, from, to, isDirectory);
        }
        return changed;
    }

    private static string? Map(string path, IEnumerable<(string From, string To, bool IsDirectory)> changes)
    {
        foreach (var (from, to, isDirectory) in changes)
            if (PathRenames.Map(path, from, to, isDirectory) is { } moved) return moved;
        return null;
    }

    // ------------------------------------------------------------------------------------------------ 2.2 widget'a konanlar
    // "Widget'lara koyduğum öğeler masaüstünden kalksın": kutu ve halka öğelerinin yanında klasör bölmesinin gösterdiği klasör
    // de "widget'ta" sayılır (masaüstünden kalkar, bölme yeni yolu portal olarak gösterir). Masaüstü bölmeleri (Klasörler,
    // Kısayollar, Dosyalar, Tümü) masaüstünün kendisidir: sayılmaz.

    /// <summary>Bir klasörü gösteren bölme mi (masaüstündeki klasörün adı ya da portalın tam yolu)?</summary>
    public static bool IsFolderPanel(WidgetConfig widget) =>
        widget.Kind == WidgetKind.Fence && widget.Filter == DesktopFilter.None && !string.IsNullOrWhiteSpace(widget.FolderName);

    /// <summary>Masaüstündeki klasör bölmeleri ve portallar, ayardaki sırayla.</summary>
    public static IEnumerable<WidgetConfig> FolderPanels(AppSettings settings) =>
        settings.Widgets.Where(w => w is not null && IsFolderPanel(w));

    /// <summary>Öğe tutan bütün widget'lar: kutular ve halkalar (NestDesk halkası dahil), sonra klasör bölmeleri.</summary>
    public static IEnumerable<WidgetConfig> Holders(AppSettings settings) => All(settings).Concat(FolderPanels(settings));

    /// <summary>Kimliği <paramref name="id"/> olan kutu, halka ya da klasör bölmesi; yoksa null.</summary>
    public static WidgetConfig? FindHolder(AppSettings settings, string? id) =>
        string.IsNullOrEmpty(id) ? null : Holders(settings).FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Klasör bölmesinin gösterdiği klasörün yolu: portalda tam yol, masaüstündeki klasörde masaüstü + adı (diske bakmaz).
    /// Klasör bölmesi değilse null.
    /// </summary>
    public static string? PanelPath(WidgetConfig widget, string userDesktop)
    {
        if (!IsFolderPanel(widget)) return null;
        var folder = widget.FolderName!.Trim();
        return Path.IsPathFullyQualified(folder)
            ? Path.TrimEndingDirectorySeparator(folder)
            : Path.Combine(Path.TrimEndingDirectorySeparator(userDesktop), folder);
    }

    /// <summary>Widget'ın tuttuğu yollar: kutu ve halkada öğeleri, klasör bölmesinde klasörü (harf duyarsız).</summary>
    public static HashSet<string> HeldBy(WidgetConfig widget, string userDesktop)
    {
        if (widget.Kind == WidgetKind.Launcher) return BoxPlan.Referenced([widget]);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (PanelPath(widget, userDesktop) is { } path) set.Add(path);
        return set;
    }

    /// <summary>Widget'larda duran bütün yollar: kutu ve halka öğeleri ve klasör bölmelerinin klasörleri.</summary>
    public static HashSet<string> Held(AppSettings settings, string userDesktop)
    {
        var set = Referenced(settings);
        foreach (var panel in FolderPanels(settings))
            if (PanelPath(panel, userDesktop) is { } path) set.Add(path);
        return set;
    }

    /// <summary>
    /// Widget'larda duran masaüstü öğeleri (kutu ve halka öğeleri ve klasör bölmelerinin masaüstündeki klasörleri): kurallar
    /// bunları taşımaz; bölmeler masaüstünü yönetirken masaüstü bölmeleri bunları göstermez.
    /// </summary>
    public static HashSet<string> HeldDesktopPaths(AppSettings settings, IReadOnlyList<string> desktopDirs)
    {
        var set = PinnedDesktopPaths(settings, desktopDirs);
        if (desktopDirs.Count == 0) return set;
        foreach (var panel in FolderPanels(settings))
            if (PanelPath(panel, desktopDirs[0]) is { } path && BoxPlan.IsDirectlyIn(path, desktopDirs)) set.Add(path);
        return set;
    }

    /// <summary>
    /// Masaüstünden kalkan ya da masaüstüne dönen öğelerin yollarını widget'larda değiştirir (bkz. <see cref="ReplacePaths"/>):
    /// kutu ve halka öğeleri (klasörse içindeki yollar da), bölmelerin gizlenen öğeleri ve elle sırası, öğe adları/simgeleri,
    /// portallar ve klasör bölmeleri. Masaüstündeki klasörü gösteren bölme, klasör masaüstünden kalkınca yeni yolu portal
    /// olarak gösterir; portalın klasörü masaüstüne dönünce yine yalnızca adını tutar (<see cref="FolderPortal.Normalize"/>).
    /// Değiştiyse true. Saf.
    /// </summary>
    public static bool ReplaceHeld(AppSettings settings, IReadOnlyCollection<(string From, string To, bool IsDirectory)> changes,
        string userDesktop)
    {
        if (changes.Count == 0) return false;
        var changed = ReplacePaths(settings, changes);
        foreach (var widget in Configs(settings))
        {
            if (widget.Kind == WidgetKind.Launcher) continue;
            foreach (var (from, to, isDirectory) in changes)
            {
                changed |= PathRenames.Rewrite(widget.HiddenItems, from, to, isDirectory);
                if (widget.ItemOrder is { } order) changed |= PathRenames.Rewrite(order, from, to, isDirectory);
                changed |= ItemLooks.Move(widget, from, to, isDirectory);
                if (!IsFolderPanel(widget)) continue;
                var shown = PanelPath(widget, userDesktop)!;
                // Masaüstündeki klasörün adı Türkçe harflerden bağımsız eşleşir (bölmenin kendisi de öyle bulur).
                if (!Path.IsPathFullyQualified(widget.FolderName!) && BoxPlan.IsDirectlyIn(from, [userDesktop]) &&
                    FolderName.Equal(BoxPlan.NameOf(from), widget.FolderName!))
                    shown = Path.TrimEndingDirectorySeparator(from);
                if (PathRenames.Map(shown, from, to, isDirectory) is not { } moved) continue;
                var source = FolderPortal.Normalize(moved, userDesktop);
                if (source.Filter != DesktopFilter.None || source.FolderName is not { } next ||
                    string.Equals(next, widget.FolderName, StringComparison.Ordinal)) continue;
                widget.FolderName = next;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// Halka komutlarının (program, bağımsız değişkenler, çalışma klasörü) metinleri: geçtikleri yollar masaüstünden kalkmaz
    /// (<see cref="BoxPlan.KeptBy"/>).
    /// </summary>
    public static IEnumerable<string?> CommandTexts(AppSettings settings) =>
        All(settings).SelectMany(w => w.Tabs).SelectMany(t => t?.Actions ?? [])
            .Where(a => a is not null).SelectMany(a => new[] { a.Command, a.Args, a.Dir });
}
