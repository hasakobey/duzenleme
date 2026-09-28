namespace Duzenleme.Core;

/// <summary>
/// Simge verilen klasörlerin listesi (<see cref="AppSettings.IconFolders"/>). Saf liste işlemleri; her değişiklik yeni bir
/// liste döner (ayarların anlık görüntüsü başka iş parçacığında alınırken yerinde değiştirilmesin). Boşsa null.
/// </summary>
public static class FolderIconLog
{
    /// <summary>En çok bu kadar klasör tutulur; aşılırsa en eskisi düşer.</summary>
    public const int Max = 500;

    private static string Key(string folder) => System.IO.Path.TrimEndingDirectorySeparator(folder);

    private static bool Same(string a, string b) => string.Equals(Key(a), Key(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>Simge verildi (true) ya da kaldırıldı (false). Liste değişmediyse aynı nesne döner.</summary>
    public static List<string>? Note(List<string>? list, string folder, bool given)
    {
        var current = list ?? [];
        var has = current.Any(f => Same(f, folder));
        if (given)
        {
            // Yeniden verilen simge sona geçer (en yeni); sınır aşılırsa en eskisi düşer.
            var next = current.Where(f => !Same(f, folder)).Append(Key(folder)).ToList();
            return next.Count > Max ? next.Skip(next.Count - Max).ToList() : next;
        }
        if (!has) return list;
        var rest = current.Where(f => !Same(f, folder)).ToList();
        return rest.Count == 0 ? null : rest;
    }

    /// <summary>Klasör (ya da üst klasörü) yeniden adlandırıldı: kayıt yeni yolu izler. Değişmediyse aynı nesne döner.</summary>
    public static List<string>? Rename(List<string>? list, string oldPath, string newPath, bool isDirectory)
    {
        if (list is null || !isDirectory) return list;
        var copy = list.ToList();
        return PathRenames.Rewrite(copy, oldPath, newPath, isDirectory) ? copy : list;
    }

    /// <summary>
    /// Toplu kaldırmadan sonra: denenen kayıtlardan yalnızca erişilemeyenler kalır; bu arada eklenenler (denenmemiş) korunur.
    /// </summary>
    public static List<string>? AfterRemoveAll(List<string>? list, IEnumerable<string> tried, IEnumerable<string> failed)
    {
        if (list is null) return null;
        var triedSet = new HashSet<string>(tried.Select(Key), StringComparer.OrdinalIgnoreCase);
        var failedSet = new HashSet<string>(failed.Select(Key), StringComparer.OrdinalIgnoreCase);
        var rest = list.Where(f => !triedSet.Contains(Key(f)) || failedSet.Contains(Key(f))).ToList();
        return rest.Count == 0 ? null : rest;
    }
}
