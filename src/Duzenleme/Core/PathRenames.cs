using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Uygulamanın kendi yaptığı yeniden adlandırmadan (bölmede F2) sonra ayarlardaki yolların güncellenmesi: bölmelerin
/// gizlenen öğeleri, kısayol kutularının öğeleri ve öğe adları/simgeleri, klasörü yeniden adlandırılan klasör bölmesi.
/// Taşıma geçmişi (<see cref="MoveJournal.NoteRename"/>) ve kutu kayıtları (<see cref="BoxMoveLog.NoteRename"/>) ayrıca
/// güncellenir. Saf; testlenir. Gezgin'de yapılan yeniden adlandırmalar izlenmez (bilinen sınır).
/// </summary>
public static class PathRenames
{
    /// <summary>
    /// Yol yeniden adlandırmadan etkileniyorsa yeni hâli, değilse null. Klasörde altındaki yollar da taşınır
    /// ("C:\M\A\x.pdf", A → B: "C:\M\B\x.pdf"). Büyük/küçük harf duyarsız karşılaştırılır; yeni adın harfleri korunur.
    /// </summary>
    public static string? Map(string path, string from, string to, bool isDirectory)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var source = Path.TrimEndingDirectorySeparator(from);
        var target = Path.TrimEndingDirectorySeparator(to);
        var trimmed = Path.TrimEndingDirectorySeparator(path);
        if (string.Equals(trimmed, source, StringComparison.OrdinalIgnoreCase)) return target;
        if (!isDirectory) return null;
        var prefix = source + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? target + Path.DirectorySeparatorChar + path[prefix.Length..] : null;
    }

    /// <summary>Listede etkilenen yolları yerinde değiştirir (aynı yol iki kez kalmaz). Değiştiyse true.</summary>
    public static bool Rewrite(List<string> paths, string from, string to, bool isDirectory)
    {
        var changed = false;
        for (var i = 0; i < paths.Count; i++)
        {
            if (Map(paths[i], from, to, isDirectory) is not { } moved) continue;
            paths[i] = moved;
            changed = true;
        }
        if (changed)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            paths.RemoveAll(p => !seen.Add(p));
        }
        return changed;
    }

    /// <summary>
    /// Masaüstündeki bir klasör yeniden adlandırıldı: onu hedefleyen kurallar yeni ada geçirilmiş yeni bir liste (kurallar
    /// kopyalanır; izleyici eski listeyi okurken yerinde değiştirilmez). Hedefleyen kural yoksa null. Kendiliğinden
    /// uygulanmaz: kullanıcıya sorulur (yeni adlı klasöre dosya taşınmaya başlaması onun kararıdır).
    /// </summary>
    public static List<Rule>? RetargetRules(IReadOnlyList<Rule> rules, string oldName, string newName)
    {
        if (!rules.Any(r => FolderName.Equal(r.TargetFolder, oldName))) return null;
        return rules.Select(r => new Rule
        {
            TargetFolder = FolderName.Equal(r.TargetFolder, oldName) ? newName : r.TargetFolder,
            Extensions = [.. r.Extensions],
            Enabled = r.Enabled,
        }).ToList();
    }

    /// <summary>
    /// Widget ayarlarındaki yolları günceller. <paramref name="desktopDirectory"/>'deki bir klasör yeniden adlandırıldıysa onu
    /// gösteren klasör bölmeleri de yeni adı izler (yoksa "klasör yok" derlerdi). Kural hedefleri değişmez: kural klasör adına
    /// bağlıdır, yeni adlı klasöre kendiliğinden dosya taşınmaya başlamasın. Değiştiyse true.
    /// </summary>
    public static bool Apply(IEnumerable<WidgetConfig> widgets, string from, string to, bool isDirectory, string desktopDirectory)
    {
        var changed = false;
        var fromName = Path.GetFileName(Path.TrimEndingDirectorySeparator(from));
        var toName = Path.GetFileName(Path.TrimEndingDirectorySeparator(to));
        var onDesktop = isDirectory && string.Equals(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(from)),
            Path.TrimEndingDirectorySeparator(desktopDirectory), StringComparison.OrdinalIgnoreCase);
        foreach (var widget in widgets)
        {
            changed |= Rewrite(widget.HiddenItems, from, to, isDirectory);
            foreach (var tab in widget.Tabs) changed |= Rewrite(tab.Items, from, to, isDirectory);
            changed |= ItemLooks.Move(widget, from, to, isDirectory);
            // Elle sıralı bölmede öğe yerini korur (listede olmayan yol sona düşerdi).
            if (widget.ItemOrder is { } order) changed |= Rewrite(order, from, to, isDirectory);
            // Klasör portalı tam yolu tutar: klasörü ya da üstlerinden biri yeniden adlandırılınca yeni yolu izler.
            if (WidgetVariants.IsPortal(widget) && Map(widget.FolderName!, from, to, isDirectory) is { } portal &&
                !string.Equals(portal, widget.FolderName, StringComparison.Ordinal))
            {
                widget.FolderName = portal;
                changed = true;
            }
            if (onDesktop && widget.Kind == WidgetKind.Fence && widget.Filter == DesktopFilter.None &&
                widget.FolderName is { } folder && FolderName.Equal(folder, fromName) && !string.Equals(folder, toName, StringComparison.Ordinal))
            {
                widget.FolderName = toName;
                changed = true;
            }
        }
        return changed;
    }
}
