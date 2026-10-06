using System.IO;

namespace Duzenleme.Core;

/// <summary>Masaüstündeki bir .lnk kısayolunun kabuktan okunan bilgileri (<c>Desktop/ShortcutFacts</c>, STA).</summary>
/// <param name="Target">Hedef dosyanın yolu (yoksa null: ör. Uygulamalar klasörünü gösteren kısayol).</param>
/// <param name="Arguments">Bağımsız değişkenler (yoksa null ya da boş).</param>
/// <param name="AppUserModelId">Kısayola yazılmış uygulama kimliği (PKEY_AppUserModel_ID; yoksa null).</param>
/// <param name="IdListParsingName">Kısayolun gösterdiği kabuk öğesinin ayrıştırma adı (ör. "::{4234D49B…}\Kimlik").</param>
public sealed record ShortcutInfo(string? Target, string? Arguments, string? AppUserModelId, string? IdListParsingName);

/// <summary>Başlat menüsü uygulamasının (Uygulamalar klasörü öğesi) kabuktan okunan bilgileri.</summary>
/// <param name="Aumid">Uygulama kimliği (kutudaki yolun sonu: <see cref="StartApps.Id"/>).</param>
/// <param name="TargetParsingPath">Başlat menüsü kısayoluyla gelen uygulamanın hedefi (System.Link.TargetParsingPath; yoksa null).</param>
/// <param name="Arguments">Onun bağımsız değişkenleri (System.Link.Arguments; yoksa null).</param>
/// <param name="KnownFolderPath">Kimlik "{bilinen klasör}\göreli\uygulama.exe" biçimindeyse açılmış yolu (yoksa null).</param>
public sealed record AppInfoFacts(string Aumid, string? TargetParsingPath, string? Arguments, string? KnownFolderPath);

/// <summary>
/// "Uygulama eklenince masaüstündeki kısayolu da kalksın": Başlat menüsünden kutuya ya da halkaya eklenen uygulamanın
/// masaüstündeki kısayolu hangisi? Yalnızca kesin eşleşme kabul edilir (adla asla): (a) kısayol Uygulamalar klasöründeki o
/// öğeyi gösteriyor, (b) kısayolun uygulama kimliği aynı, (c) hedef ve bağımsız değişkenler birebir aynı (bilinen klasörle
/// yazılmış kimlik açılarak). Bulamamak sorun değildir; yanlış eşleşme olmaz. Saf; testlenir.
/// </summary>
public static class AppShortcutMatch
{
    /// <summary>Kısayol bu uygulamanın mı?</summary>
    public static bool Matches(AppInfoFacts app, ShortcutInfo link)
    {
        if (string.IsNullOrWhiteSpace(app.Aumid)) return false;
        // (a) Uygulamalar klasöründeki öğeyi gösteren kısayol (Başlat menüsünden masaüstüne sürüklenmiş Store uygulaması).
        if (link.IdListParsingName is { Length: > 0 } parsing &&
            string.Equals(parsing.Trim(), StartApps.Prefix + app.Aumid, StringComparison.OrdinalIgnoreCase))
            return true;
        // (b) Aynı uygulama kimliği (Chrome, Edge… kısayollarına kendi kimliklerini yazar).
        if (link.AppUserModelId is { Length: > 0 } aumid && string.Equals(aumid.Trim(), app.Aumid.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        // (c) Aynı hedef ve aynı bağımsız değişkenler (ör. "--profile-directory" farklıysa başka bir kısayoldur).
        var appTarget = Normalize(app.TargetParsingPath) ?? Normalize(app.KnownFolderPath);
        var linkTarget = Normalize(link.Target);
        if (appTarget is null || linkTarget is null || !string.Equals(appTarget, linkTarget, StringComparison.OrdinalIgnoreCase)) return false;
        return string.Equals(Args(app.Arguments), Args(link.Arguments), StringComparison.Ordinal);
    }

    /// <summary>
    /// "{GUID}\göreli\yol" biçimindeki kimliği (Uygulamalar klasörünün masaüstü uygulamaları için yazdığı) bilinen klasörün
    /// yoluyla açar; biçim uymuyorsa ya da klasör bilinmiyorsa null.
    /// </summary>
    public static string? ExpandKnownFolder(string aumid, Func<Guid, string?> knownFolder)
    {
        if (aumid.Length < 40 || aumid[0] != '{' || aumid[37] != '}' || aumid[38] != '\\') return null;
        if (!Guid.TryParse(aumid[..38], out var id) || knownFolder(id) is not { Length: > 0 } root) return null;
        var rest = aumid[39..];
        if (rest.Length == 0 || rest.Contains("..", StringComparison.Ordinal)) return null;
        return Path.Combine(root, rest);
    }

    /// <summary>
    /// Adaylar: masaüstü klasörlerinin doğrudan içindeki .lnk dosyaları (gizli/sistem değil), henüz bir kutuda olmayan ya da
    /// taşınmamış (<paramref name="excluded"/>). Klasörlerin içine bakılmaz.
    /// </summary>
    public static List<string> Candidates(IEnumerable<DirEntry> entries, IReadOnlyList<string> desktopDirs, IReadOnlySet<string> excluded) =>
        entries.Where(e => !e.IsDirectory && !e.IsHiddenOrSystem &&
                           e.Name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                           BoxPlan.IsDirectlyIn(e.Path, desktopDirs) && !excluded.Contains(e.Path))
            .Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var trimmed = path.Trim().Trim('"');
        if (!Path.IsPathFullyQualified(trimmed)) return null;
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    private static string Args(string? arguments) => (arguments ?? "").Trim();
}
