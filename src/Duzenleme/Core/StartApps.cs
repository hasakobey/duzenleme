namespace Duzenleme.Core;

/// <summary>
/// Başlat menüsünden kısayol kutusuna bırakılan uygulamalar. Öğe yolu kabuk nesnesidir:
/// <c>::{4234D49B-0245-4DF3-B780-3893943456E1}\&lt;AUMID&gt;</c> (Uygulamalar klasörü + uygulama kimliği). "::" ile başladığı
/// için 2.1 de onu kabuk kutucuğu sayar ("bulunamadı" değil); adı <see cref="ItemLooks"/>'ta durur. Kabuk bu biçimi doğrudan
/// ayrıştıramaz (E_INVALIDARG): simge ve açma <see cref="ShellPath"/> ("shell:AppsFolder\…") ile yapılır. Saf; testlenir.
/// </summary>
public static class StartApps
{
    /// <summary>Uygulamalar klasörünün (shell:AppsFolder) sınıf kimliği.</summary>
    public static readonly Guid FolderId = new("4234D49B-0245-4DF3-B780-3893943456E1");

    /// <summary>Klasörün kendi ayrıştırma adı (üst öğesi bu olan öğe bir uygulamadır).</summary>
    public const string FolderParsingName = "::{4234D49B-0245-4DF3-B780-3893943456E1}";

    /// <summary>Kutuda saklanan uygulama yolunun öneki.</summary>
    public const string Prefix = FolderParsingName + "\\";

    /// <summary>Uygulama kimliği sınırı (paket kimlikleri ve masaüstü uygulamalarının yol biçimli kimlikleri bunun çok altında).</summary>
    public const int MaxId = 1024;

    /// <summary>Başlat menüsü uygulaması yolu mu?</summary>
    public static bool IsApp(string? path) =>
        path is not null && path.Length > Prefix.Length && path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Yoldaki uygulama kimliği (AUMID); uygulama yolu değilse null.</summary>
    public static string? Id(string path) => IsApp(path) ? path[Prefix.Length..] : null;

    /// <summary>Uygulama kimliğinden kutu yolu; kimlik boş, çok uzun ya da denetim karakterli ise null.</summary>
    public static string? PathFor(string? id)
    {
        id = id?.Trim();
        if (string.IsNullOrEmpty(id) || id.Length > MaxId || id.Any(char.IsControl)) return null;
        return Prefix + id;
    }

    /// <summary>Kabuğun ayrıştırabildiği ad (simge ve açma): "shell:AppsFolder\&lt;AUMID&gt;"; uygulama değilse yolun kendisi.</summary>
    public static string ShellPath(string path) => Id(path) is { } id ? "shell:AppsFolder\\" + id : path;
}
