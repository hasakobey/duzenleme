namespace Duzenleme.Core;

/// <summary>Eklenecek başlangıç bölmesi: Filter None değilse masaüstü türü, değilse Folder klasörünün içi.</summary>
public sealed record StarterFence(DesktopFilter Filter, string? Folder, string Label);

/// <summary>"Masaüstümü bölmelere ayır"ın saf mantığı: hangi bölmeler eklenecek?</summary>
public static class StarterFences
{
    private static readonly DesktopFilter[] Kinds = [DesktopFilter.Folders, DesktopFilter.Shortcuts, DesktopFilter.Files];

    /// <summary>Eklenecek bölmeler, sırasıyla:
    /// 1. Eksik Klasörler/Kısayollar/Dosyalar bölmeleri (o Filter'da bölme yoksa).
    /// 2. Masaüstünde var olan, etkin kuralların klasörleri. Bölmesi olanlar (Filter None, FolderName.Equal) atlanır;
    ///    aynı klasöre iki kural varsa tek bölme açılır.
    /// Label: DesktopItems.Label(filter) ya da klasörün masaüstündeki adı.</summary>
    public static List<StarterFence> Plan(IEnumerable<WidgetConfig> widgets, IEnumerable<Rule> rules, IEnumerable<string> desktopFolders)
    {
        var fences = widgets.Where(w => w.Kind == WidgetKind.Fence).ToList();
        var folders = desktopFolders.ToList();
        var plan = new List<StarterFence>();

        foreach (var filter in Kinds)
            if (!fences.Any(w => w.Filter == filter))
                plan.Add(new StarterFence(filter, null, DesktopItems.Label(filter)));

        foreach (var rule in rules.Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.TargetFolder)))
        {
            if (folders.FirstOrDefault(f => FolderName.Equal(f, rule.TargetFolder)) is not { } folder) continue;
            if (fences.Any(w => w.Filter == DesktopFilter.None && FolderName.Equal(w.FolderName ?? "", folder))) continue;
            if (plan.Any(p => p.Folder is { } planned && FolderName.Equal(planned, folder))) continue;
            plan.Add(new StarterFence(DesktopFilter.None, folder, folder));
        }
        return plan;
    }
}
