using System.Globalization;

namespace Duzenleme.Core;

/// <summary>Başlat menüsündeki bir uygulama: kimliği (AUMID) ve kabuğun gösterdiği adı (yalnızca çalışma zamanı).</summary>
public sealed record StartApp(string Id, string Name)
{
    /// <summary>Kutuda ve halkada saklanacak yol (<see cref="StartApps.PathFor"/>); kimlik geçersizse null.</summary>
    public string? Path => StartApps.PathFor(Id);
}

/// <summary>
/// "Uygulama seç" penceresinin listesi (saf; testlenir): geçersiz kimlikli ve adsız uygulamalar atılır, aynı kimlik bir kez
/// kalır (harf duyarsız), ada göre arayüz dilinde sıralanır (<see cref="L.Sorter"/>); arama arayüz diline göre büyük/küçük harf
/// ve aksan farkı gözetmez (<see cref="L.Culture"/>), Türkçe harfler yazılmadan da bulunur ("islem" → "İşlem").
/// </summary>
public static class AppList
{
    /// <summary>Gösterilecek liste: geçerli, yinelenmeyen, sıralı.</summary>
    public static IReadOnlyList<StartApp> Prepare(IEnumerable<StartApp> apps)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<StartApp>();
        foreach (var app in apps)
        {
            var name = app.Name?.Trim();
            if (string.IsNullOrEmpty(name) || StartApps.PathFor(app.Id) is null) continue;
            var id = app.Id.Trim();
            if (!seen.Add(id)) continue;
            result.Add(new StartApp(id, name));
        }
        var sorter = L.Sorter;
        result.Sort((a, b) =>
        {
            var byName = sorter.Compare(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.Id, b.Id);
        });
        return result;
    }

    /// <summary>Aramaya uyanlar (sırası korunur). Boş arama hepsini verir; her sözcük adın bir yerinde geçmeli.</summary>
    public static IReadOnlyList<StartApp> Filter(IReadOnlyList<StartApp> apps, string? query)
    {
        var words = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return apps;
        return apps.Where(app => words.All(word => Matches(app.Name, word))).ToList();
    }

    /// <summary>
    /// Ad bu sözcüğü içeriyor mu: arayüz dilinin kurallarıyla harf ve aksan farkı gözetmeden (Türkçede "İ"/"i", "I"/"ı"),
    /// ya da Türkçe harfler katlanarak ("ışık" ile "isik" aynı).
    /// </summary>
    public static bool Matches(string name, string word)
    {
        if (word.Length == 0) return true;
        if (L.Culture.CompareInfo.IndexOf(name, word, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0) return true;
        return FolderName.Fold(name).Contains(FolderName.Fold(word), StringComparison.Ordinal);
    }

    /// <summary>Uygulama zaten eklenmiş mi (kutudaki ya da halkadaki yollar arasında; harf duyarsız)?</summary>
    public static bool IsAdded(StartApp app, IEnumerable<string> existing) =>
        app.Path is { } path && existing.Contains(path, StringComparer.OrdinalIgnoreCase);
}
