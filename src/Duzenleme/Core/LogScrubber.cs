using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Duzenleme.Core;

/// <summary>
/// Temizleyicinin bildiği yerler ve adlar: yollar türüne göre adlandırılır (<c>&lt;path:desktop&gt;</c>), kullanıcı ve
/// bilgisayar adı <c>&lt;user&gt;</c>/<c>&lt;pc&gt;</c> olur. Hiçbiri bilinmese de yollar yine tümüyle atılır.
/// </summary>
public sealed record ScrubContext
{
    public IReadOnlyList<string> Desktop { get; init; } = [];
    public string? PublicDesktop { get; init; }
    public string? Data { get; init; }
    /// <summary>Kutulara taşınan öğelerin klasörü (masaüstünün yanındaki NestDesk klasörü).</summary>
    public string? Box { get; init; }
    public IReadOnlyList<string> UserNames { get; init; } = [];
    public string? MachineName { get; init; }

    /// <summary>
    /// Bu oturumun kullanıcısı: kullanıcı adı, profil klasörünün adı ve bilgisayar adı ortamdan okunur. Etki alanı adı
    /// okunmaz (etki alanına bağlı bilgisayarda ağ sorgusu yapabilir). Hiçbir okuma hata fırlatmaz.
    /// </summary>
    public static ScrubContext ForCurrentUser(IEnumerable<string>? desktop = null, string? data = null, string? box = null)
    {
        static string? Try(Func<string?> read)
        {
            try { return read(); }
            catch { return null; }
        }
        var desktops = desktop?.ToList() ?? [];
        if (desktops.Count == 0 && Try(() => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolderOption.DoNotVerify)) is { Length: > 0 } d)
            desktops.Add(d);
        return new ScrubContext
        {
            Desktop = desktops,
            PublicDesktop = Try(() => Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolderOption.DoNotVerify)),
            Data = data,
            Box = box,
            UserNames = new[]
            {
                Try(() => Environment.UserName),
                Try(() => Path.GetFileName(Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify)))),
            }.OfType<string>().ToList(),
            MachineName = Try(() => Environment.MachineName),
        };
    }
}

/// <summary>
/// Temizlenmiş istisna: tür, HResult, (yalnızca kod tanımlayıcısıysa) parametre adı, temizlenmiş ileti, yığın kareleri ve
/// en çok üç iç istisna. <see cref="Exception.Data"/> hiç okunmaz.
/// </summary>
public sealed record ScrubbedError(string Type, string HResult, string? ParamName, string? Message,
    IReadOnlyList<string> Frames, IReadOnlyList<ScrubbedError> Inner)
{
    /// <summary>Uygulamanın kendi kodundaki ilk kare (imza için); yoksa null.</summary>
    public string? FirstAppFrame => Frames.FirstOrDefault(f => f.StartsWith(LogScrubber.AppNamespace + ".", StringComparison.Ordinal)) is { } frame
        ? LogScrubber.MethodOf(frame)
        : null;

    /// <summary>Çökme raporu için en az bilgi: yalnızca tür, HResult ve yığın kareleri (ileti ve parametre adı yok).</summary>
    public ScrubbedError Minimal() => new(Type, HResult, null, null, Frames, Inner.Select(i => i.Minimal()).ToList());

    public void WriteJson(Utf8JsonWriter json)
    {
        json.WriteStartObject();
        json.WriteString("type", Type);
        json.WriteString("hr", HResult);
        if (ParamName is not null) json.WriteString("param", ParamName);
        if (Message is not null) json.WriteString("msg", Message);
        json.WriteStartArray("frames");
        foreach (var frame in Frames) json.WriteStringValue(frame);
        json.WriteEndArray();
        if (Inner.Count > 0)
        {
            json.WriteStartArray("inner");
            foreach (var inner in Inner) inner.WriteJson(json);
            json.WriteEndArray();
        }
        json.WriteEndObject();
    }

    /// <summary>Diskten okunan kayıt; biçim tanınmıyorsa null. Okunan metin veridir: gösterirken yine kaçışlanır.</summary>
    public static ScrubbedError? FromJson(JsonElement e, int depth = 0)
    {
        if (e.ValueKind != JsonValueKind.Object || depth > 4) return null;
        static string? Str(JsonElement o, string name) =>
            o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var frames = new List<string>();
        if (e.TryGetProperty("frames", out var f) && f.ValueKind == JsonValueKind.Array)
            foreach (var item in f.EnumerateArray().Take(LogScrubber.MaxFrames))
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s) frames.Add(s);
        var inner = new List<ScrubbedError>();
        if (e.TryGetProperty("inner", out var i) && i.ValueKind == JsonValueKind.Array)
            foreach (var item in i.EnumerateArray().Take(LogScrubber.MaxInner))
                if (FromJson(item, depth + 1) is { } x) inner.Add(x);
        return new ScrubbedError(Str(e, "type") ?? "?", Str(e, "hr") ?? "", Str(e, "param"), Str(e, "msg"), frames, inner);
    }
}

/// <summary>
/// Hata günlüğüne giren her metnin temizleyicisi (saf; Core/AppLog ve çökme raporları kullanır). Kişisel veri yazmamak için
/// tutucudur, bilgi kaybını göze alır:
/// <list type="bullet">
/// <item>Mutlak yollar (<c>X:\</c>, <c>X:/</c>, <c>\\sunucu\</c>, <c>\\?\</c>, <c>file:</c>, <c>%DEĞİŞKEN%\</c>) satırın sonuna ya da adlarda
/// yasak bir karaktere kadar <c>&lt;path:desktop|public-desktop|data|box|network|other&gt;</c> olur: ad ve uzantı da atılır.</item>
/// <item>IO, UnauthorizedAccess, Win32, COM/External, Argument, KeyNotFound ve Aggregate istisnalarının iletisi hiç yazılmaz
/// (.NET bu iletilere yolu ya da anahtarı koyar; Aggregate iç iletileri tekrarlar); yalnızca tür, HResult ve ParamName kalır.</item>
/// <item>Tırnak içi metin <c>&lt;text&gt;</c> olur; yalnızca <c>(Parameter '…')</c>, <c>System.|Microsoft.|Duzenleme.|Wpf.</c> ile
/// başlayan noktalı ad ve yalnızca yer tutucudan oluşan içerik kalır. <c>Key:</c>/<c>key=</c> sonrası silinir; API anahtarı
/// (<c>sk-ant-…</c>) ve taşıyıcı belirteç nerede geçerse <c>&lt;secret&gt;</c> olur.</item>
/// <item>Kullanıcı ve bilgisayar adı yalnızca en az 3 harfli tam sözcükse ve yığın karesinin dışındaysa değişir.</item>
/// <item>URL <c>&lt;url&gt;</c> (github.com/anthropic.com için yalnızca sunucu), e-posta <c>&lt;email&gt;</c>, GUID ilk 6 karakter.</item>
/// </list>
/// </summary>
public sealed partial class LogScrubber
{
    public const int MaxMessage = 400;
    public const int MaxFrames = 40;
    public const int MaxInner = 3;

    /// <summary>Uygulamanın kök ad alanı (bilerek eski adlı; kaynak klasörü de bu): kendi kodumuzun kareleri ve adları.</summary>
    internal static readonly string AppNamespace = typeof(LogScrubber).Namespace!.Split('.')[0];

    private readonly (string Root, string Kind)[] _roots;
    private readonly Regex? _names;

    public LogScrubber(ScrubContext context)
    {
        var roots = new List<(string, string)>();
        void AddRoot(string? path, string kind)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var normalized = NormalizeRoot(path);
            if (normalized.Length >= 3) roots.Add((normalized, kind));
        }
        foreach (var d in context.Desktop) AddRoot(d, "desktop");
        AddRoot(context.PublicDesktop, "public-desktop");
        AddRoot(context.Data, "data");
        AddRoot(context.Box, "box");
        // En uzun kök önce: masaüstünün içindeki veri klasörü "data" sayılsın.
        _roots = roots.OrderByDescending(r => r.Item1.Length).ToArray();
        _names = NameRegex(context.UserNames, context.MachineName is null ? [] : [context.MachineName]);
    }

    /// <summary>Bağlamsız temizleyici: yollar "other" olur, ad değiştirilmez (testler ve yedek).</summary>
    public static LogScrubber Plain { get; } = new(new ScrubContext());

    private static string NormalizeRoot(string path)
    {
        var p = path.Trim().Replace('/', '\\');
        if (p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) p = @"\\" + p[8..];
        else if (p.StartsWith(@"\\?\", StringComparison.Ordinal)) p = p[4..];
        return p.TrimEnd('\\');
    }

    /// <summary>Kullanıcı (u) ve bilgisayar (m) adları tek desende: değiştirilen metin yeniden taranmaz.</summary>
    private static Regex? NameRegex(IEnumerable<string> users, IEnumerable<string> machines)
    {
        static List<string> Parts(IEnumerable<string> names) => names.Select(n => n?.Trim() ?? "")
            .Where(n => n.Length >= 3 && n.Any(char.IsLetter))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Length)
            .Select(Regex.Escape)
            .ToList();
        var u = Parts(users);
        var m = Parts(machines);
        if (u.Count == 0 && m.Count == 0) return null;
        var alternatives = new List<string>();
        if (u.Count > 0) alternatives.Add("(?<u>" + string.Join("|", u) + ")");
        if (m.Count > 0) alternatives.Add("(?<m>" + string.Join("|", m) + ")");
        return new Regex(@"(?<![\p{L}\p{N}_])(?:" + string.Join("|", alternatives) + @")(?![\p{L}\p{N}_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    // ------------------------------------------------------------------ istisna

    /// <summary>İstisnayı temizler. Hiçbir zaman hata fırlatmaz.</summary>
    public ScrubbedError Error(Exception ex)
    {
        try
        {
            var budget = MaxInner;
            return Scrub(ex, ref budget, 0);
        }
        catch
        {
            return new ScrubbedError(ex.GetType().FullName ?? "?", "0x" + ex.HResult.ToString("X8", System.Globalization.CultureInfo.InvariantCulture),
                null, null, [], []);
        }
    }

    private ScrubbedError Scrub(Exception ex, ref int innerBudget, int depth)
    {
        var type = ex.GetType();
        var typeName = type.FullName ?? type.Name;
        var hr = "0x" + ex.HResult.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
        string? param = null;
        if (ex is ArgumentException arg && arg.ParamName is { } p)
            param = Identifier().IsMatch(p) ? p : "<text>";
        string? message = null;
        if (!DropsMessage(ex))
        {
            string raw;
            try { raw = ex.Message; }
            catch { raw = ""; }
            message = Text(raw);
        }
        string? stack;
        try { stack = ex.StackTrace; }
        catch { stack = null; }
        var frames = Frames(stack);

        var inner = new List<ScrubbedError>();
        IEnumerable<Exception> children = ex is AggregateException agg
            ? agg.InnerExceptions.Take(MaxInner)
            : ex.InnerException is { } single ? [single] : [];
        foreach (var child in children)
        {
            if (innerBudget <= 0 || depth >= MaxInner) break;
            innerBudget--;
            inner.Add(Scrub(child, ref innerBudget, depth + 1));
        }
        return new ScrubbedError(typeName, hr, param, message, frames, inner);
    }

    /// <summary>
    /// İletisine .NET'in yolu, dosya adını ya da anahtarı koyduğu türler: ileti hiç yazılmaz. AggregateException'ın iletisi iç
    /// istisnaların iletilerini olduğu gibi tekrarlar (ör. gözlenmemiş görev hatası): o da atılır, iç istisnalar kendi türlerine
    /// göre ayrı ayrı temizlenir.
    /// </summary>
    public static bool DropsMessage(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or ExternalException
            or KeyNotFoundException or System.Security.SecurityException or AggregateException;

    // ------------------------------------------------------------------ yığın kareleri

    /// <summary>
    /// <see cref="Exception.StackTrace"/> satırlarını temizler: "at " ve ayraç satırları atılır, en çok <see cref="MaxFrames"/>
    /// kare. Ad değiştirme karelerde yapılmaz (kod adları bozulmasın).
    /// </summary>
    public IReadOnlyList<string> Frames(string? stackTrace)
    {
        var frames = new List<string>();
        if (string.IsNullOrEmpty(stackTrace)) return frames;
        foreach (var raw in stackTrace.Split('\n'))
        {
            if (frames.Count >= MaxFrames) break;
            if (Frame(raw) is { } frame) frames.Add(frame);
        }
        return frames;
    }

    /// <summary>Tek kare: "Ad.Alanı.Tür.Yöntem(Tür ad) in /_/src/Duzenleme/Dosya.cs:line 12"; boş/ayraç satırında null.</summary>
    public string? Frame(string line)
    {
        var s = line.Trim();
        if (s.Length == 0 || s.StartsWith("---", StringComparison.Ordinal)) return null;
        if (s.StartsWith("at ", StringComparison.Ordinal)) s = s[3..].TrimStart();
        string method = s, file = "";
        string? lineNo = null;
        var at = s.LastIndexOf(" in ", StringComparison.Ordinal);
        if (at > 0)
        {
            method = s[..at];
            file = s[(at + 4)..];
            var m = LineSuffix().Match(file);
            if (m.Success)
            {
                lineNo = m.Groups[1].Value;
                file = file[..m.Index];
            }
        }
        method = ScrubPaths(method);
        if (method.Length > 300) method = method[..300] + "…";
        if (at <= 0) return method;
        var source = SourceFile(file);
        return lineNo is null ? $"{method} in {source}" : $"{method} in {source}:line {lineNo}";
    }

    /// <summary>Karedeki yöntem adı (dosya ve satır olmadan).</summary>
    internal static string MethodOf(string frame)
    {
        var at = frame.LastIndexOf(" in ", StringComparison.Ordinal);
        return at > 0 ? frame[..at] : frame;
    }

    /// <summary>
    /// Kaynak dosya: PathMap'li yol (<c>/_/…</c>) olduğu gibi; geliştirici derlemesinde <c>…\src\Duzenleme\…</c> depoya göreli
    /// yazılır; başka bir kaynak yolundan yalnızca dosya adı (ör. "File.cs") kalır, o da değilse <c>&lt;path&gt;</c>.
    /// </summary>
    private static string SourceFile(string file)
    {
        var f = file.Trim().Replace('\\', '/');
        var mapped = f.IndexOf("/_/", StringComparison.Ordinal);
        if (mapped >= 0) return SafeRelative(f[mapped..]);
        var folder = "/src/" + AppNamespace + "/";
        var ours = f.IndexOf(folder, StringComparison.OrdinalIgnoreCase);
        if (ours >= 0) return SafeRelative("/_" + folder + f[(ours + folder.Length)..]);
        var name = f[(f.LastIndexOf('/') + 1)..];
        return SourceName().IsMatch(name) ? name : "<path>";
    }

    /// <summary>Depoya göreli kaynak yolu yalnızca ASCII kod adlarından oluşmalı; değilse yalnızca dosya adı.</summary>
    private static string SafeRelative(string path)
    {
        if (RelativeSource().IsMatch(path)) return path;
        var name = path[(path.LastIndexOf('/') + 1)..];
        return SourceName().IsMatch(name) ? name : "<path>";
    }

    // ------------------------------------------------------------------ metin

    /// <summary>Serbest metni temizler (ileti, ortam bilgisi). Hiçbir zaman hata fırlatmaz.</summary>
    public string Text(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        // Çıktı zaten MaxMessage'a kısalır; çok uzun iletide desenler boşuna çalışmasın.
        if (text.Length > MaxMessage * 20) text = text[..(MaxMessage * 20)];
        try
        {
            // Önce gizli anahtarlar (yapay zekâ anahtarı nerede geçerse geçsin), sonra URL'ler (yolundaki "C:/" yol sanılmasın),
            // sonra yollar (file: adresleri dahil).
            var s = ApiKeyToken().Replace(text, "<secret>");
            s = BearerToken().Replace(s, m => m.Groups["b"].Value + " <secret>");
            s = Url().Replace(s, m => UrlPlaceholder(m.Value));
            s = ScrubPaths(s);
            s = Email().Replace(s, "<email>");
            s = GuidText().Replace(s, m => "<id:" + m.Value[..6].ToLowerInvariant() + ">");
            s = Hex32().Replace(s, m => "<id:" + m.Value[..6].ToLowerInvariant() + ">");
            s = KeySuffix().Replace(s, m => m.Groups["k"].Value + m.Groups["sep"].Value + " <text>");
            s = ScrubQuotes(s);
            s = ReplaceNames(s);
            if (s.Length > MaxMessage) s = s[..MaxMessage] + "…";
            return s;
        }
        catch
        {
            return "<text>";
        }
    }

    private static string UrlPlaceholder(string url)
    {
        var host = UrlHost().Match(url) is { Success: true } h ? h.Groups[1].Value.ToLowerInvariant() : "";
        return host is "github.com" or "www.github.com" or "api.github.com" or "anthropic.com" or "www.anthropic.com" or "api.anthropic.com" or "console.anthropic.com"
            ? $"<url:{host}>"
            : "<url>";
    }

    /// <summary>
    /// Mutlak yolları türlerine göre yer tutucuya çevirir; yol satır sonuna ya da adlarda yasak bir karaktere kadar sürer
    /// (bkz. <see cref="PathEnd"/>).
    /// </summary>
    private string ScrubPaths(string text)
    {
        if (text.Length == 0) return text;
        var sb = new StringBuilder(text.Length);
        var pos = 0;
        foreach (Match m in PathStart().Matches(text))
        {
            if (m.Index < pos) continue;
            var end = PathEnd(text, m.Index, m.Index + m.Length);
            // Sondaki noktalama yolun parçası sayılmasın.
            while (end > m.Index + m.Length && text[end - 1] is '.' or ',' or ';' or ':' or ')' or ']' or ' ') end--;
            sb.Append(text, pos, m.Index - pos);
            sb.Append("<path:").Append(Classify(text[m.Index..end])).Append('>');
            pos = end;
        }
        if (pos == 0) return text;
        sb.Append(text, pos, text.Length - pos);
        return sb.ToString();
    }

    /// <summary>
    /// Yolun bittiği yer: Windows'un dosya adlarında yasakladığı karakter (<c>" &lt; &gt; | * ?</c>, denetim karakterleri) ya da
    /// satır sonu. Kıvrık tırnaklar, « » ve boşluk adlarda geçerlidir: yolu bitirmez (ardındaki metin de atılır; tutucu olmak
    /// bilgi kaybına yeğdir). ASCII <c>'</c> yalnızca yol <c>'</c> ile açıldıysa ve ardından harf ya da rakam gelmiyorsa kapatır
    /// (.NET iletileri yolu '…' içine alır; "Ali's" adın parçasıdır).
    /// </summary>
    private static int PathEnd(string text, int start, int from)
    {
        var quoted = start > 0 && text[start - 1] == '\'';
        var end = from;
        while (end < text.Length)
        {
            var c = text[end];
            if (c is '"' or '<' or '>' or '|' or '*' or '?' || char.IsControl(c)) break;
            if (c == '\'' && quoted && (end + 1 >= text.Length || !char.IsLetterOrDigit(text[end + 1]))) break;
            end++;
        }
        return end;
    }

    private string Classify(string path)
    {
        var p = path.Replace('/', '\\');
        if (p.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            p = p[5..].TrimStart('\\');
            // file:///C:/x → C:\x; file://sunucu/x → \\sunucu\x
            if (!(p.Length >= 2 && p[1] == ':')) p = @"\\" + p;
            p = Uri.UnescapeDataString(p);
        }
        if (p.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return "network";
        if (p.StartsWith(@"\\?\", StringComparison.Ordinal)) p = p[4..];
        foreach (var (root, kind) in _roots)
        {
            if (p.Length >= root.Length && p.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && (p.Length == root.Length || p[root.Length] == '\\'))
                return kind;
        }
        return p.StartsWith(@"\\", StringComparison.Ordinal) ? "network" : "other";
    }

    /// <summary>
    /// Tırnak içini <c>&lt;text&gt;</c> yapar. Kesme işareti (NestDesk'in, can't) tırnak sayılmaz: açan ' öncesinde, kapatan '
    /// sonrasında harf olamaz. Satırda kapanmayan tırnağın ardı satır sonuna dek atılır.
    /// </summary>
    private static string ScrubQuotes(string text)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (!IsOpeningQuote(text, i))
            {
                sb.Append(c);
                i++;
                continue;
            }
            var close = FindClosingQuote(text, i);
            if (close < 0)
            {
                var lineEnd = text.IndexOfAny(['\r', '\n'], i);
                if (lineEnd < 0) lineEnd = text.Length;
                sb.Append(c).Append("<text>");
                i = lineEnd;
                continue;
            }
            var content = text[(i + 1)..close];
            sb.Append(c);
            sb.Append(KeepQuoted(text, i, content) ? content : "<text>");
            sb.Append(text[close]);
            i = close + 1;
        }
        return sb.ToString();
    }

    private static bool IsOpeningQuote(string text, int i) => text[i] switch
    {
        '"' or '“' or '„' or '«' or '‘' => true,
        '\'' => i == 0 || !char.IsLetterOrDigit(text[i - 1]),
        _ => false,
    };

    private static int FindClosingQuote(string text, int open)
    {
        var o = text[open];
        for (var j = open + 1; j < text.Length; j++)
        {
            var c = text[j];
            if (c is '\r' or '\n') return -1;
            var closes = o switch
            {
                '"' => c == '"',
                '“' or '„' => c is '”' or '“' or '"',
                '«' => c == '»',
                '‘' => c is '’' or '\'',
                _ => c is '\'' or '’' && (j + 1 >= text.Length || !char.IsLetterOrDigit(text[j + 1])),
            };
            if (closes) return j;
        }
        return -1;
    }

    private static bool KeepQuoted(string text, int open, string content)
    {
        if (content.Length == 0) return true;
        if (OnlyPlaceholders().IsMatch(content)) return true;
        if (open >= 10 && text.AsSpan(0, open).EndsWith("(Parameter ", StringComparison.Ordinal) && Identifier().IsMatch(content)) return true;
        if (content.Length > 200 || DottedName().Match(content) is not { Success: true } dotted) return false;
        // Yalnızca .NET'in, WPF-UI'nin ve uygulamanın kendi tür/ad alanı adları (kod adı; kullanıcı verisi değil).
        var root = dotted.Groups["root"].Value;
        return root is "System" or "Microsoft" or "Wpf" || root == AppNamespace;
    }

    /// <summary>Kullanıcı ve bilgisayar adlarını değiştirir; yer tutucuların içine dokunmaz.</summary>
    private string ReplaceNames(string text)
    {
        if (_names is null) return text;
        var sb = new StringBuilder(text.Length);
        var pos = 0;
        foreach (Match m in Placeholder().Matches(text))
        {
            sb.Append(ReplaceNamesIn(text[pos..m.Index]));
            sb.Append(m.Value);
            pos = m.Index + m.Length;
        }
        sb.Append(ReplaceNamesIn(text[pos..]));
        return sb.ToString();
    }

    private string ReplaceNamesIn(string segment)
    {
        if (segment.Length == 0 || _names is null) return segment;
        return _names.Replace(segment, m => m.Groups["u"].Success ? "<user>" : "<pc>");
    }

    // ------------------------------------------------------------------ desenler

    /// <summary>Mutlak yolun başlangıcı. Önünde harf/rakam olmamalı (https:// ya da "10:30" yol sayılmasın).</summary>
    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:file:(?:///?|\\\\)|\\\\\?\\|\\\\(?=[^\\\s])|[A-Za-z]:[\\/]|%[A-Za-z_][A-Za-z0-9_]*%[\\/])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PathStart();

    [GeneratedRegex(@"\b(?!file:)[A-Za-z][A-Za-z0-9+.\-]{1,15}://[^\s'""<>“”‘’«»]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Url();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*://(?:[^@/\s]*@)?([^/:?#\s]+)", RegexOptions.CultureInvariant)]
    private static partial Regex UrlHost();

    [GeneratedRegex(@"[\p{L}\p{N}._%+\-]+@[\p{L}\p{N}\-]+(?:\.[\p{L}\p{N}\-]+)*\.\p{L}{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<![0-9A-Fa-f])[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}(?![0-9A-Fa-f])", RegexOptions.CultureInvariant)]
    private static partial Regex GuidText();

    [GeneratedRegex(@"(?<![0-9A-Za-z])[0-9A-Fa-f]{32}(?![0-9A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex Hex32();

    /// <summary>
    /// "Key:", "key =", "ApiKey:", "x-api-key:" ve benzerlerinden sonrası (sözcük sınırı aranmaz: "ApiKey" de yakalanır; "hotkey:"
    /// gibi masum eşleşmeler de silinir, tutucu olmak yeğdir).
    /// </summary>
    [GeneratedRegex(@"(?<k>key)\s*(?<sep>[:=])[^\r\n]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeySuffix();

    /// <summary>Anthropic API anahtarı (sk-ant-…), metnin neresinde geçerse geçsin.</summary>
    [GeneratedRegex(@"sk-ant-[A-Za-z0-9_\-]{8,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyToken();

    /// <summary>HTTP yetki başlığındaki taşıyıcı belirteç ("Bearer abc…").</summary>
    [GeneratedRegex(@"(?<b>\bbearer)\s+[A-Za-z0-9._~+/\-]{8,}=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerToken();

    [GeneratedRegex(@"^(?:\s*<(?:path:[a-z\-]+|path|url(?::[a-z0-9.\-]+)?|email|user|pc|text|secret|id:[0-9a-f]{6})>\s*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex OnlyPlaceholders();

    [GeneratedRegex(@"<(?:path:[a-z\-]+|path|url(?::[a-z0-9.\-]+)?|email|user|pc|text|secret|id:[0-9a-f]{6})>", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex Identifier();

    [GeneratedRegex(@"^(?<root>[A-Za-z_][A-Za-z0-9_]*)(?:\.[A-Za-z0-9_`+\[\],]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex DottedName();

    [GeneratedRegex(@":line (\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex LineSuffix();

    [GeneratedRegex(@"^[A-Za-z0-9_.\-]{1,100}\.(?:cs|xaml|fs|vb)$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceName();

    [GeneratedRegex(@"^/_/(?:[A-Za-z0-9_.\-]+/){0,12}[A-Za-z0-9_.\-]+\.(?:cs|xaml|fs|vb|g\.cs)$", RegexOptions.CultureInvariant)]
    private static partial Regex RelativeSource();
}
