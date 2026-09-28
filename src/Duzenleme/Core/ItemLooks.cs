namespace Duzenleme.Core;

/// <summary>
/// Kısayol kutusu öğelerinin kullanıcı adları ve simgeleri (<see cref="WidgetConfig.ItemLooks"/>). Anahtar öğenin yoludur;
/// JSON sözlüğü büyük/küçük harfe duyarlı okunduğu için bütün aramalar buradan, harf duyarsız yapılır. Boşalan kayıt silinir,
/// sözlük boşalınca null olur (ayar dosyası kalabalıklaşmasın). Saf; testlenir.
/// </summary>
public static class ItemLooks
{
    /// <summary>Kutu adı sınırı (başlıkla aynı ölçüde kısa kalsın).</summary>
    public const int MaxName = 80;

    /// <summary>Öğenin kaydı; yoksa null.</summary>
    public static ItemLook? Get(WidgetConfig config, string path) =>
        config.ItemLooks is { } map && FindKey(map, path) is { } key ? map[key] : null;

    /// <summary>
    /// Görünen adı yazar. Boş ya da dosyanın kendi adıyla aynıysa ad kaldırılır (varsayılana döner). Değiştiyse true.
    /// </summary>
    public static bool SetName(WidgetConfig config, string path, string? name, string defaultName)
    {
        name = name?.Trim();
        if (name is { Length: > MaxName }) name = name[..MaxName].TrimEnd();
        if (string.IsNullOrEmpty(name) || string.Equals(name, defaultName, StringComparison.Ordinal)) name = null;
        return Update(config, path, look => look.Name = name, look => look.Name);
    }

    /// <summary>Simgeyi yazar (<see cref="IconRef"/> metni; null = dosyanın kendi simgesi). Değiştiyse true.</summary>
    public static bool SetIcon(WidgetConfig config, string path, string? icon)
    {
        icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        return Update(config, path, look => look.Icon = icon, look => look.Icon);
    }

    /// <summary>Öğenin adını ve simgesini varsayılana döndürür. Kaydı varsa true.</summary>
    public static bool Reset(WidgetConfig config, string path)
    {
        if (config.ItemLooks is not { } map || FindKey(map, path) is not { } key) return false;
        map.Remove(key);
        if (map.Count == 0) config.ItemLooks = null;
        return true;
    }

    /// <summary>Kaydı çıkarır ve döner (ör. öğe kutudan çıkarılınca; "Geri al" <see cref="Restore"/> ile geri koyar).</summary>
    public static ItemLook? Take(WidgetConfig config, string path)
    {
        var look = Get(config, path);
        if (look is not null) Reset(config, path);
        return look;
    }

    /// <summary>Alınan kaydı yeniden yazar.</summary>
    public static void Restore(WidgetConfig config, string path, ItemLook? look)
    {
        if (look is null || look.IsEmpty) return;
        Reset(config, path);
        (config.ItemLooks ??= [])[path] = new ItemLook { Name = look.Name, Icon = look.Icon };
    }

    /// <summary>
    /// Yol değişti (kutuya taşındı, masaüstüne döndü, yeniden adlandırıldı): kayıt yeni yola taşınır. Klasör yeniden
    /// adlandırıldıysa (<paramref name="isDirectory"/>) altındaki yollar da taşınır. Değiştiyse true.
    /// </summary>
    public static bool Move(WidgetConfig config, string from, string to, bool isDirectory = false)
    {
        if (config.ItemLooks is not { } map || map.Count == 0) return false;
        var changed = false;
        foreach (var key in map.Keys.ToList())
        {
            if (PathRenames.Map(key, from, to, isDirectory) is not { } moved) continue;
            var look = map[key];
            map.Remove(key);
            // Hedefte zaten kayıt varsa (aynı öğe iki yoldan gelmiş) yeni yolunki korunur.
            if (FindKey(map, moved) is null) map[moved] = look;
            changed = true;
        }
        return changed;
    }

    /// <summary>Hiçbir sekmede olmayan öğelerin kayıtlarını siler (ör. sekme silinince). Sildiyse true.</summary>
    public static bool Prune(WidgetConfig config, IEnumerable<string>? keep = null)
    {
        if (config.ItemLooks is not { } map) return false;
        var used = new HashSet<string>(config.Tabs.SelectMany(t => t.Items), StringComparer.OrdinalIgnoreCase);
        if (keep is not null) used.UnionWith(keep);
        var removed = 0;
        foreach (var key in map.Keys.ToList())
            if (!used.Contains(key) || map[key].IsEmpty)
            {
                map.Remove(key);
                removed++;
            }
        if (map.Count == 0) config.ItemLooks = null;
        return removed > 0;
    }

    /// <summary>Kutudaki bütün kayıtlar, harf duyarsız (çizim sırasında tek arama tablosu).</summary>
    public static IReadOnlyDictionary<string, ItemLook> Lookup(WidgetConfig config) =>
        config.ItemLooks is { Count: > 0 } map
            ? new Dictionary<string, ItemLook>(map.Where(p => !p.Value.IsEmpty)
                .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()), StringComparer.OrdinalIgnoreCase)
            : Empty;

    private static readonly IReadOnlyDictionary<string, ItemLook> Empty = new Dictionary<string, ItemLook>();

    private static bool Update(WidgetConfig config, string path, Action<ItemLook> set, Func<ItemLook, string?> read)
    {
        var existing = Get(config, path);
        var before = existing is null ? null : read(existing);
        var look = existing ?? new ItemLook();
        set(look);
        if (string.Equals(before, read(look), StringComparison.Ordinal) && existing is not null) return false;
        if (look.IsEmpty)
        {
            var had = existing is not null;
            Reset(config, path);
            return had;
        }
        if (existing is null) (config.ItemLooks ??= [])[path] = look;
        return true;
    }

    private static string? FindKey(Dictionary<string, ItemLook> map, string path)
    {
        if (map.ContainsKey(path)) return path;
        return map.Keys.FirstOrDefault(k => string.Equals(k, path, StringComparison.OrdinalIgnoreCase));
    }
}
