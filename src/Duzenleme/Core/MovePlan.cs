using System.Globalization;
using System.IO;

namespace Duzenleme.Core;

/// <summary>Masaüstündeki bir dosyanın önizleme için anlık görüntüsü (dosyanın kendisine dokunulmaz).</summary>
public sealed record DesktopFile(string Name, FileAttributes Attributes, bool WasUndone);

/// <summary>Şimdi taşınsaydı dosyanın gideceği klasör. FolderExists false ise klasör masaüstünde henüz yok.</summary>
public sealed record PlannedMove(string FileName, string Folder, bool FolderExists);

/// <summary>Taşıma önizlemesi ve bekleyen dosya sayısı (karşılama ve Otomatik taşıma sayfası). Saf hesap: dosya sistemine dokunmaz.</summary>
public static class MovePlan
{
    private static readonly StringComparer TrOrder = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);

    /// <summary>Bu dosyalar şimdi taşınsaydı nereye giderdi? Dosya sistemine dokunmaz. RuleEngine.Decide kullanılır
    /// (.lnk/.url/desktop.ini/gizli/sistem/yarım indirme hariç); WasUndone olanlar hariç. assumeFolders: masaüstünde yok ama
    /// varmış sayılacak klasörler (karşılamada oluşturulacaklar; "bekleyen" sayımında tüm kural klasörleri).
    /// Folder: masaüstündeki gerçek ad (ör. "arsivler"), yoksa kuraldaki ad. CreateMissingFolders her zaman false sayılır.</summary>
    public static List<PlannedMove> Build(IReadOnlyList<DesktopFile> files, IEnumerable<string> existingFolders,
        IReadOnlyList<Rule> rules, IEnumerable<string>? assumeFolders = null)
    {
        var existing = existingFolders.ToList();
        var known = new List<string>(existing);
        foreach (var name in assumeFolders ?? [])
            if (!string.IsNullOrWhiteSpace(name) && !known.Any(k => FolderName.Equal(k, name))) known.Add(name.Trim());

        // Kurallar kopyalanmaz; motor yalnızca okur. "Yoksa oluştur" önizlemede hesaba katılmaz: varsayılan kapalıdır ve
        // açıkken her kural klasörü zaten assumeFolders ile verilir.
        var settings = new AppSettings { Rules = rules as List<Rule> ?? rules.ToList(), CreateMissingFolders = false };
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

    /// <summary>Klasör başına sayı; çoktan aza, eşitse tr-TR sırasına göre.</summary>
    public static List<(string Folder, int Count)> ByFolder(IEnumerable<PlannedMove> moves) =>
        moves.GroupBy(m => FolderName.Fold(m.Folder))
            .Select(g => (Folder: g.First().Folder, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Folder, TrOrder)
            .ToList();
}
