using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Duzenleme.Core;

/// <summary>Bırakılan metnin bağlantı olarak durumu.</summary>
public enum LinkKind
{
    /// <summary>Bağlantı değil (sıradan metin).</summary>
    None,
    /// <summary>Eklenebilir bağlantı (http, https, mailto).</summary>
    Allowed,
    /// <summary>Bağlantı ama türü eklenemez (file:, javascript:, shell:, ms-*…).</summary>
    Blocked,
}

/// <summary>Yazılan (ya da zaten var olan) bağlantı dosyası ve kutuda görünecek adı.</summary>
public sealed record LinkFile(string Path, string Name);

/// <summary>
/// Kısayol kutusuna bırakılan web ve e-posta bağlantıları: veri klasöründe <c>links\&lt;host&gt;-&lt;özet8&gt;.url</c> dosyası
/// (2.1 de gösterir ve açar). Yalnızca http, https ve mailto kabul edilir (file:, shell:, ms-*, javascript: asla); adres en çok
/// 2048 karakter, satır sonu ve denetim karakteri yok; dosyaya ASCII yazılır. Aynı adres aynı dosyayı kullanır. Açarken dosyadaki
/// adres yeniden denetlenir ve tarayıcıyla açılır (<c>TileItem.Launch</c> → <c>Browser.Open</c>): paketli sürümün dosya
/// yönlendirmesi önemsizleşir. Kendiliğinden silinmez (yedekler ve öteki kutular onu gösterebilir). Saf; testlenir.
/// </summary>
public static class LinkFiles
{
    /// <summary>Veri klasörünün altındaki klasör adı.</summary>
    public const string FolderName = "links";

    /// <summary>Adres sınırı (karakter).</summary>
    public const int MaxLength = 2048;

    /// <summary>Okunan .url dosyasının üst sınırı (bayt).</summary>
    private const int MaxFileBytes = 16 * 1024;

    private const string Section = "[InternetShortcut]";

    /// <summary>
    /// Eklenebilir bağlantının dosyaya yazılacak biçimi (ASCII, IDN alan adı punycode, ASCII dışı karakterler %XX); http,
    /// https ya da mailto değilse, çok uzunsa, satır sonu/denetim karakteri taşıyorsa ya da alan adı yoksa null.
    /// </summary>
    public static string? Normalize(string? text)
    {
        if (text is null) return null;
        var s = text.Trim();
        if (s.Length is 0 or > MaxLength || s.Any(char.IsControl)) return null;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) || !IsAllowedScheme(uri.Scheme)) return null;
        if (uri.Scheme == Uri.UriSchemeMailto ? s.Length <= "mailto:".Length : string.IsNullOrEmpty(uri.Host)) return null;
        string ascii;
        try { ascii = Ascii(uri); }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or InvalidOperationException) { return null; }
        if (ascii.Length > MaxLength || ascii.Any(c => c is < '!' or > '~')) return null;
        // Son biçim de aynı denetimden geçmeli (kaçışlar türü değiştiremez).
        return Uri.TryCreate(ascii, UriKind.Absolute, out var again) && IsAllowedScheme(again.Scheme) ? ascii : null;
    }

    private static bool IsAllowedScheme(string scheme) =>
        scheme == Uri.UriSchemeHttp || scheme == Uri.UriSchemeHttps || scheme == Uri.UriSchemeMailto;

    /// <summary>
    /// Bağlantı biçimindeki veri (UniformResourceLocatorW, text/x-moz-url): eklenebilir değilse <see cref="LinkKind.Blocked"/>
    /// (kaynak onu bağlantı diye verdi); boşsa <see cref="LinkKind.None"/>.
    /// </summary>
    public static LinkKind ClassifyUrl(string? text, out string? url)
    {
        url = Normalize(text);
        return url is not null ? LinkKind.Allowed : string.IsNullOrWhiteSpace(text) ? LinkKind.None : LinkKind.Blocked;
    }

    /// <summary>
    /// Sıradan metin (UnicodeText): tek parça ve eklenebilir bir adresse <see cref="LinkKind.Allowed"/>; "://" taşıyan ya da
    /// bilinen tehlikeli türdeki (file:, javascript:, vbscript:, data:, shell:, ms-*) tek parça adres <see cref="LinkKind.Blocked"/>;
    /// gerisi (cümle, dosya yolu "C:\…", "Saat:12") <see cref="LinkKind.None"/>.
    /// </summary>
    public static LinkKind ClassifyText(string? text, out string? url)
    {
        url = null;
        var s = text?.Trim();
        if (string.IsNullOrEmpty(s) || s.Length > MaxLength || s.Any(char.IsWhiteSpace)) return LinkKind.None;
        if ((url = Normalize(s)) is not null) return LinkKind.Allowed;
        var colon = s.IndexOf(':');
        if (colon < 2) return LinkKind.None; // "C:\…" ya da şema yok
        var scheme = s[..colon].ToLowerInvariant();
        if (!(char.IsAsciiLetter(scheme[0]) && scheme.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.'))) return LinkKind.None;
        var dangerous = scheme is "file" or "javascript" or "vbscript" or "data" or "shell" || scheme.StartsWith("ms-", StringComparison.Ordinal);
        return dangerous || s.AsSpan(colon).StartsWith("://") ? LinkKind.Blocked : LinkKind.None;
    }

    /// <summary>
    /// Bağlantıyı <paramref name="directory"/> içine yazar ya da aynı adresin dosyasını kullanır; dosya yolunu ve görünen adı
    /// döner (<paramref name="title"/> temizlenir; yoksa alan adı ya da e-posta adresi). Arka planda çağrılır. Adres eklenebilir
    /// değilse <see cref="ArgumentException"/>; yazılamazsa <see cref="IOException"/> / <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    public static LinkFile Create(string url, string? title, string directory)
    {
        var normalized = Normalize(url) ?? throw new ArgumentException(null, nameof(url));
        var name = ItemLooks.CleanName(title) ?? DefaultName(normalized);
        Directory.CreateDirectory(directory);
        var stem = FileStem(normalized);
        for (var i = 1; i <= 9; i++)
        {
            var path = System.IO.Path.Combine(directory, (i == 1 ? stem : $"{stem}-{i}") + ".url");
            if (File.Exists(path))
            {
                if (ReadUrl(path) == normalized) return new LinkFile(path, name);
                continue; // aynı adlı başka adres (özet çakışması): sonraki ad
            }
            var temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, Content(normalized), Encoding.ASCII);
                File.Move(temp, path, overwrite: true);
            }
            finally
            {
                try { File.Delete(temp); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            return new LinkFile(path, name);
        }
        throw new IOException();
    }

    /// <summary>Dosya içeriği (Windows'un .url biçimi).</summary>
    public static string Content(string normalizedUrl) => $"{Section}\r\nURL={normalizedUrl}\r\n";

    /// <summary>
    /// Dosya adı gövdesi: "&lt;alan adı&gt;-&lt;adresin SHA-256 özetinin ilk 8 onaltılığı&gt;" (mailto için "mailto-…"). Yalnızca
    /// [a-z0-9.-]; Windows'un ayrılmış adlarına (CON, NUL…) denk gelmez, çünkü özet hep eklenir.
    /// </summary>
    public static string FileStem(string normalizedUrl)
    {
        var host = "mailto";
        if (Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) && uri.Scheme != Uri.UriSchemeMailto)
            host = new string(uri.IdnHost.ToLowerInvariant().Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-').ToArray());
        host = host.Trim('.', '-');
        if (host.Length > 60) host = host[..60].TrimEnd('.', '-');
        if (host.Length == 0) host = "link";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUrl));
        return $"{host}-{Convert.ToHexStringLower(hash.AsSpan(0, 4))}";
    }

    /// <summary>Başlık yoksa kutuda görünen ad: alan adı ("www." olmadan, okunur yazımıyla) ya da e-posta adresi.</summary>
    public static string DefaultName(string normalizedUrl)
    {
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri)) return normalizedUrl;
        if (uri.Scheme == Uri.UriSchemeMailto)
        {
            var address = normalizedUrl["mailto:".Length..];
            var query = address.IndexOf('?');
            if (query >= 0) address = address[..query];
            return ItemLooks.CleanName(Uri.UnescapeDataString(address)) ?? normalizedUrl;
        }
        var host = uri.IdnHost;
        try { host = new IdnMapping().GetUnicode(host); }
        catch (ArgumentException) { }
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && host.Length > 4) host = host[4..];
        return ItemLooks.CleanName(host) ?? normalizedUrl;
    }

    /// <summary>Dosyadaki adres, denetlenmiş biçimiyle; okunamıyorsa, çok büyükse ya da adres eklenebilir türde değilse null.</summary>
    public static string? ReadUrl(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes) return null;
            return ParseUrlFile(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>.url içeriğinden [InternetShortcut] bölümündeki URL=, <see cref="Normalize"/>'dan geçmişse.</summary>
    public static string? ParseUrlFile(string content)
    {
        var inSection = false;
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inSection = string.Equals(line, Section, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (inSection && line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) return Normalize(line[4..]);
        }
        return null;
    }

    /// <summary>Bu uygulamanın yazdığı bağlantı dosyası mı (bağlantılar klasörünün içinde, .url)?</summary>
    public static bool IsLinkFile(string path, string linksDirectory)
    {
        if (string.IsNullOrEmpty(linksDirectory) || !path.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var parent = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
            return parent is not null && string.Equals(System.IO.Path.TrimEndingDirectorySeparator(parent),
                System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(linksDirectory)), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>Firefox'un "text/x-moz-url" verisi: ilk satır adres, ikinci satır sayfa başlığı.</summary>
    public static (string? Url, string? Title) ParseMozUrl(string? text)
    {
        if (string.IsNullOrEmpty(text)) return (null, null);
        var lines = text.Split('\n');
        var url = lines[0].Trim().TrimEnd('\0');
        var title = lines.Length > 1 ? lines[1].Trim().TrimEnd('\0') : null;
        return (url.Length == 0 ? null : url, string.IsNullOrEmpty(title) ? null : title);
    }

    /// <summary>
    /// Tarayıcının bağlantıyla verdiği "FileGroupDescriptorW" (ilk dosya "&lt;Sayfa başlığı&gt;.url"): sayfa başlığı. İlk dosya
    /// .url değilse (resim gibi) ya da veri bozuksa null. FILEDESCRIPTORW 592 bayt, adı 72. bayttan başlar (260 UTF-16 karakter).
    /// </summary>
    public static string? TitleFromFileGroupDescriptor(ReadOnlySpan<byte> data)
    {
        const int Count = 4, NameOffset = 72, NameChars = 260, Descriptor = 592;
        if (data.Length < Count + Descriptor || BinaryPrimitives.ReadUInt32LittleEndian(data) == 0) return null;
        var name = Encoding.Unicode.GetString(data.Slice(Count + NameOffset, NameChars * 2));
        var end = name.IndexOf('\0');
        if (end >= 0) name = name[..end];
        return name.EndsWith(".url", StringComparison.OrdinalIgnoreCase) ? ItemLooks.CleanName(name[..^4]) : null;
    }

    /// <summary>
    /// ASCII biçim: Uri ASCII dışı yolu ve sorguyu zaten kaçışlar; Unicode alan adı punycode'a çevrilir, kalan ASCII dışı
    /// karakterler (mailto) UTF-8 %XX olur.
    /// </summary>
    private static string Ascii(Uri uri)
    {
        var text = uri.AbsoluteUri;
        if (IsAscii(text)) return text;
        if (!string.IsNullOrEmpty(uri.Host) && !string.Equals(uri.Host, uri.IdnHost, StringComparison.Ordinal))
        {
            var at = text.IndexOf(uri.Host, StringComparison.Ordinal);
            if (at >= 0) text = string.Concat(text.AsSpan(0, at), uri.IdnHost, text.AsSpan(at + uri.Host.Length));
        }
        if (IsAscii(text)) return text;
        var sb = new StringBuilder(text.Length * 2);
        Span<byte> utf8 = stackalloc byte[4];
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.IsAscii) sb.Append((char)rune.Value);
            else
            {
                var n = rune.EncodeToUtf8(utf8);
                for (var i = 0; i < n; i++) sb.Append('%').Append(utf8[i].ToString("X2", CultureInfo.InvariantCulture));
            }
        }
        return sb.ToString();
    }

    private static bool IsAscii(string text) => text.All(char.IsAscii);
}
