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
    /// <summary>Ayarlardaki bütün widget yapılandırmaları (launcher olmayanlar da): yolları güncelleyen kod (yeniden adlandırma) için.</summary>
    public static IEnumerable<WidgetConfig> Configs(AppSettings settings) => settings.Widgets.Where(w => w is not null);

    /// <summary>Bütün kutular ve halkalar, ayardaki sırayla.</summary>
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
}
