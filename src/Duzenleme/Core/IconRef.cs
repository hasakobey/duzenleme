using System.Globalization;
using System.IO;

namespace Duzenleme.Core;

/// <summary>Simge başvurusunun türü (yalnızca bellekte; kalıcı olan metindir, bkz. <see cref="IconRef"/>).</summary>
public enum IconRefKind { Symbol, Resource, Image }

/// <summary>
/// Kullanıcının seçtiği simge (<see cref="WidgetConfig.Icon"/>, <see cref="ItemLook.Icon"/>). Ayarlarda metin olarak durur:
/// <list type="bullet">
/// <item><c>sym:&lt;SymbolRegular adı&gt;</c> — Fluent simgesi (ör. <c>sym:Games24</c>), vurgu renginde çizilir.</item>
/// <item><c>res:&lt;yol&gt;,&lt;sıra&gt;</c> — .ico/.exe/.dll içindeki simge (Windows'un "Simge Değiştir" penceresinden);
/// yol <c>%SystemRoot%</c> gibi değişkenler taşıyabilir, yüklenirken açılır.</item>
/// <item><c>img:&lt;dosya adı&gt;</c> — kullanıcının seçtiği resmin veri klasöründeki kopyası (<see cref="IconFiles"/>).</item>
/// </list>
/// Enum değil metin: eski sürüm bilmediği özelliği yok sayar; tanınmayan değer "varsayılan simge" demektir.
/// </summary>
public readonly record struct IconRef(IconRefKind Kind, string Value, int Index = 0)
{
    public const string SymbolPrefix = "sym:";
    public const string ResourcePrefix = "res:";
    public const string ImagePrefix = "img:";

    /// <summary>Metni çözer; boş, bozuk ya da tanınmayan değerde null (çağıran varsayılan simgeyi kullanır).</summary>
    public static IconRef? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        if (text.StartsWith(SymbolPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = text[SymbolPrefix.Length..].Trim();
            return name.Length > 0 && name.All(char.IsAsciiLetterOrDigit) ? new IconRef(IconRefKind.Symbol, name) : null;
        }
        if (text.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Yol virgül içerebilir: sıra numarası son virgülden sonradır (negatifse kaynak kimliği).
            var body = text[ResourcePrefix.Length..];
            var comma = body.LastIndexOf(',');
            if (comma <= 0) return null;
            var path = body[..comma].Trim();
            return path.Length > 0 && path.IndexOfAny(['\0', '\r', '\n']) < 0 &&
                   int.TryParse(body[(comma + 1)..].Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var index)
                ? new IconRef(IconRefKind.Resource, path, index)
                : null;
        }
        if (text.StartsWith(ImagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var file = text[ImagePrefix.Length..].Trim();
            return IconFiles.IsStoredName(file) ? new IconRef(IconRefKind.Image, file) : null;
        }
        return null;
    }

    public override string ToString() => Kind switch
    {
        IconRefKind.Symbol => SymbolPrefix + Value,
        IconRefKind.Resource => ResourcePrefix + Value + "," + Index.ToString(CultureInfo.InvariantCulture),
        _ => ImagePrefix + Value,
    };

    public static string Symbol(string name) => new IconRef(IconRefKind.Symbol, name).ToString();

    public static string Resource(string path, int index) => new IconRef(IconRefKind.Resource, path, index).ToString();

    public static string Image(string storedName) => new IconRef(IconRefKind.Image, storedName).ToString();

    /// <summary>Kaynak dosyasının yolu, ortam değişkenleri açılmış (yalnızca <see cref="IconRefKind.Resource"/>).</summary>
    public string ExpandedPath => Environment.ExpandEnvironmentVariables(Value);
}

/// <summary>
/// Kullanıcının "Resim dosyasından…" ile seçtiği simgeler: veri klasöründeki <c>icons</c> klasörüne içeriğin özetiyle
/// adlandırılmış bir kopya olarak alınır (asıl dosya silinse ya da taşınsa da simge kalır; aynı resim iki kez kopyalanmaz).
/// Yalnızca bu bilgisayarda saklanır, hiçbir yere gönderilmez.
/// </summary>
public static class IconFiles
{
    /// <summary>Veri klasöründeki alt klasörün adı.</summary>
    public const string FolderName = "icons";

    /// <summary>Kabul edilen en büyük resim (daha büyüğü simge için gereksizdir ve belleği şişirir).</summary>
    public const long MaxBytes = 8L << 20;

    /// <summary>Kabul edilen uzantılar (OpenFileDialog süzgeci de bunlardır).</summary>
    public static readonly IReadOnlyList<string> Extensions = [".png", ".ico", ".jpg", ".jpeg", ".bmp", ".gif"];

    /// <summary>Saklanan ad güvenli mi: yalnızca "&lt;onaltılık özet&gt;.&lt;uzantı&gt;" (klasör ayıracı, "..", sürücü yok).</summary>
    public static bool IsStoredName(string name)
    {
        var ext = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        return stem.Length is >= 16 and <= 64 && stem.All(char.IsAsciiHexDigit) &&
               Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase) && name.Length == stem.Length + ext.Length;
    }

    /// <summary>
    /// Resmi <paramref name="iconsDirectory"/>'ye kopyalar ve saklanan adı döner (aynı içerik zaten varsa yeniden yazılmaz).
    /// Disk işidir: arka planda çağrılır. Uzantısı desteklenmeyen ya da çok büyük dosyada <see cref="NotSupportedException"/>.
    /// </summary>
    public static string Import(string sourceFile, string iconsDirectory)
    {
        var ext = Path.GetExtension(sourceFile).ToLowerInvariant();
        if (!Extensions.Contains(ext)) throw new NotSupportedException(ext);
        var bytes = File.ReadAllBytes(sourceFile);
        if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new NotSupportedException(sourceFile);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..32].ToLowerInvariant();
        var name = hash + ext;
        Directory.CreateDirectory(iconsDirectory);
        var target = Path.Combine(iconsDirectory, name);
        if (!File.Exists(target))
        {
            var temp = target + ".tmp";
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, target, overwrite: true);
        }
        return name;
    }

    /// <summary>Saklanan adın tam yolu; ad güvenli değilse null.</summary>
    public static string? PathOf(string storedName, string iconsDirectory) =>
        IsStoredName(storedName) ? Path.Combine(iconsDirectory, storedName) : null;
}
