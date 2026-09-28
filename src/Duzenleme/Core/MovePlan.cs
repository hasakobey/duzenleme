using System.IO;

namespace Duzenleme.Core;

/// <summary>Masaüstündeki bir dosyanın önizleme için anlık görüntüsü (dosyanın kendisine dokunulmaz).</summary>
public sealed record DesktopFile(string Name, FileAttributes Attributes, bool WasUndone);

/// <summary>Şimdi taşınsaydı dosyanın gideceği klasör. FolderExists false ise klasör masaüstünde henüz yok.</summary>
public sealed record PlannedMove(string FileName, string Folder, bool FolderExists);

/// <summary>Taşıma önizlemesi ve bekleyen dosya sayısı (karşılama ve Otomatik taşıma sayfası). Saf hesap: dosya sistemine dokunmaz.</summary>
public static class MovePlan
{
    /// <summary>Bu dosyalar şimdi taşınsaydı nereye giderdi? Dosya sistemine dokunmaz. RuleEngine.Decide kullanılır
    /// (.lnk/.url/desktop.ini/gizli/sistem/yarım indirme hariç); WasUndone olanlar hariç. assumeFolders: masaüstünde yok ama
    /// varmış sayılacak klasörler (karşılamada oluşturulacaklar; "bekleyen" sayımında tüm kural klasörleri).
    /// createMissing: "Klasör yoksa oluştur" ayarı (AppSettings.CreateMissingFolders); açıkken etkin her kural, klasörü
    /// olmasa da taşır (taşıyıcı klasörü açar). Folder: masaüstündeki gerçek ad (ör. "arsivler"), yoksa kuraldaki ad.</summary>
    public static List<PlannedMove> Build(IReadOnlyList<DesktopFile> files, IEnumerable<string> existingFolders,
        IReadOnlyList<Rule> rules, IEnumerable<string>? assumeFolders = null, bool createMissing = false)
    {
        var existing = existingFolders.ToList();
        var known = new List<string>(existing);
        foreach (var name in assumeFolders ?? [])
            if (!string.IsNullOrWhiteSpace(name) && !known.Any(k => FolderName.Equal(k, name))) known.Add(name.Trim());

        // Kurallar kopyalanmaz; motor yalnızca okur. Karar taşıyıcınınkiyle aynı olsun diye "yoksa oluştur" da verilir.
        var settings = new AppSettings { Rules = rules as List<Rule> ?? rules.ToList(), CreateMissingFolders = createMissing };
        var engine = new RuleEngine(() => settings);

        var moves = new List<PlannedMove>();
        foreach (var file in files)
        {
            if (file.WasUndone) continue;
            var decision = engine.Decide("", file.Name, file.Attributes, known);
            if (!decision.ShouldMove || decision.Rule is not { } rule) continue;
            var real = existing.FirstOrDefault(f => FolderName.Equal(f, rule.TargetFolder));
            moves.Add(new PlannedMove(file.Name, real ?? rule.TargetFolder.Trim(), real is not null));
        }
        return moves;
    }

    /// <summary>Klasör başına sayı; çoktan aza, eşitse arayüz dilinin sırasına göre (Türkçede tr-TR).</summary>
    public static List<(string Folder, int Count)> ByFolder(IEnumerable<PlannedMove> moves) =>
        moves.GroupBy(m => FolderName.Fold(m.Folder))
            .Select(g => (Folder: g.First().Folder, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Folder, L.Sorter)
            .ToList();
}
