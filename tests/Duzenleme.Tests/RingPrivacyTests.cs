using System.Text;
using System.Text.RegularExpressions;

namespace Duzenleme.Tests;

/// <summary>
/// Halka gizliliği (PRIVACY §4, Store 10.5): yapıştırılan metin, web adresi, komut, gönderilen tuşlar, ön plandaki pencerenin
/// başlığı ve sürecinin adı hiçbir günlüğe (DebugLog, AppLog, PerfLog, JsonFile.Log, FolderIconLog, CrashReports,
/// Debug/Trace/Console/EventLog) girmez; ön plan bilgisi ve pano içeriği diske yazılmaz; adresler günlüğe yazmayan
/// <c>Browser.Open(…, log: false)</c> ile açılır. Kaynak taraması: Ring*, Snippet*, KeySender ve ForegroundInfo dosyaları
/// (arayüz bölümü eklenince RingRunner, RingPopup… kendiliğinden kapsanır).
/// <para>
/// Günlük çağrısının argümanlarında (dize sabitlerinin metni dışında; enterpolasyon delikleri dahil) şu sözcükleri içeren
/// hiçbir ad bulunamaz (deve hörgücü adların içinde de: <c>snippetText</c>, <c>targetUrl</c>, <c>prevTitle</c>): bkz.
/// <see cref="SensitiveWords"/>. Hata nesnesi ancak türüyle ya da HResult'uyla yazılır (<c>ex.GetType().Name</c>):
/// <c>ex</c>, <c>ex.Message</c>, <c>ex.ToString()</c> yasak (Process.Start'ın iletisi dosya adını içerir). Güvenli günlük
/// argümanları: sabit metin, sonuç, sayı, süre, <c>nameof(…)</c>.
/// </para>
/// </summary>
public class RingPrivacyTests
{
    /// <summary>Günlük çağrıları: argümanları taranır.</summary>
    private static readonly Regex LogCall = new(
        @"\b(DebugLog\.Write|AppLog\.\w+|PerfLog\.\w+|JsonFile\.Log|FolderIconLog\.\w+|CrashReports\.\w+|EventLog\.\w+|" +
        @"Debug\.\w+|Trace\.\w+|Console\.(Error\.|Out\.)?Write(Line)?)\s*\(");

    /// <summary>Günlüğe girmemesi gereken veri: bu sözcükleri (harf duyarsız) içeren her ad.</summary>
    private static readonly string[] SensitiveWords =
    [
        "text", "url", "uri", "command", "cmd", "args", "argument", "key", "chord", "snippet", "process", "title",
        "clipboard", "saved", "previous", "value", "data", "path", "message", "content", "payload", "stack", "name",
    ];

    /// <summary>Hata nesnesi: türü ya da HResult'u dışında yazılamaz (ileti ve ToString kullanıcı verisi taşır).</summary>
    private static readonly Regex RawException = new(
        @"\b(ex|e|exc|exception|error|err|failure|inner|\w*Exception)\b(?!\s*(\(\s*\))?\s*\??\.\s*(GetType\s*\(|HResult\b|InnerException\b|GetBaseException\s*\())");

    // Güvenli kalıplar: önce atılır.
    private static readonly Regex SafeTypeName = new(@"\.\s*GetType\s*\(\s*\)\s*\.\s*(Full)?Name\b");
    private static readonly Regex NameOf = new(@"\bnameof\s*\([^()]*\)");
    private static readonly Regex Identifier = new(@"[\p{L}_][\p{L}\p{Nd}_]*");

    /// <summary>Adres açma: halka dosyalarında yalnızca günlüğe yazmayan biçim.</summary>
    private static readonly Regex BrowserOpen = new(@"\bBrowser\.Open\s*\(");
    private static readonly Regex NoLogArgument = new(@"\blog\s*:\s*false\b");

    /// <summary>Ön plan ve pano bilgisini taşıyan dosyalar diske/kayıt defterine yazmaz.</summary>
    private static readonly Regex DiskWrite = new(
        @"\b(File\.(Write|Append|Create|Copy|Move|Replace|Open)\w*|FileInfo|\w+\.(OpenWrite|AppendText|CreateText)\b|" +
        @"StreamWriter|FileStream|Directory\.Create\w*|IsolatedStorage\w*|Registry\w*|JsonFile\.Save|DurableFile)\b");

    [Fact]
    public void Ring_sources_never_log_user_content()
    {
        var files = RingSources().ToList();
        Assert.Contains(files, f => f.EndsWith("KeySender.cs", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith("SnippetPaste.cs", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith("ForegroundInfo.cs", StringComparison.Ordinal));
        Assert.Contains(files, f => f.EndsWith("RingActions.cs", StringComparison.Ordinal));

        var offenders = files.SelectMany(f => Offenders(Path.GetFileName(f), File.ReadAllText(f))).ToList();
        Assert.True(offenders.Count == 0, "Halka içeriği günlüğe giriyor:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Foreground_and_clipboard_code_writes_nothing_to_disk()
    {
        var offenders = new List<string>();
        foreach (var file in RingSources().Where(f => Path.GetFileName(f) is "ForegroundInfo.cs" or "KeySender.cs" or "SnippetPaste.cs"))
        {
            var code = StripComments(File.ReadAllText(file));
            foreach (Match m in DiskWrite.Matches(code)) offenders.Add($"{Path.GetFileName(file)}: {m.Value}");
        }
        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    [Fact]
    public void Scanner_flags_bad_calls_and_allows_constant_messages()
    {
        string[] bad =
        [
            """DebugLog.Write($"yapıştırıldı: {text}");""",
            """AppLog.Warn("RingRunner", action.Url);""",
            """PerfLog.Write("ring " + ForegroundInfo.ProcessName(hwnd));""",
            """Debug.WriteLine(string.Join(",", chords));""",
            """AppLog.Info("Ring", $"sonuç {outcome} {snippet.Length}");""",
            // Deve hörgücü adlar
            """DebugLog.Write($"{snippetText}");""",
            """DebugLog.Write($"açılıyor {targetUrl}");""",
            """DebugLog.Write($"komut {cmdLine}");""",
            """DebugLog.Write($"ön plan {fgProcessName}");""",
            """DebugLog.Write($"pencere {prevTitle}");""",
            // Hata iletisi ve nesnesi
            """DebugLog.Write($"başlatılamadı {ex.Message}");""",
            """DebugLog.Write($"başlatılamadı {ex}");""",
            """AppLog.Error("Ring", ex);""",
            """DebugLog.Write("x" + ex.ToString());""",
            """DebugLog.Write($"{failure.InnerException}");""",
            // Öteki dize biçimleri ve günlükler
            """DebugLog.Write($@"yol {itemPath}");""",
            """DebugLog.Write(@$"yol {itemPath}");""",
            "DebugLog.Write($\"\"\"raw {pastedText}\"\"\");",
            """JsonFile.Log($"bozuk {value}");""",
            """FolderIconLog.Write($"{folder.FullName}");""",
            """CrashReports.Note($"{text}");""",
            """Console.Error.WriteLine(text);""",
            // Günlüğe yazan adres açma
            """Browser.Open(url);""",
            """Browser.Open(action.Url!, log: true);""",
        ];
        foreach (var line in bad) Assert.True(Offenders("x.cs", line).Count == 1, "yakalanmadı: " + line);

        const string good = """
            DebugLog.Write("halka açıldı");
            PerfLog.Write($"Ring.Open {elapsed.TotalMilliseconds:0} ms");
            AppLog.Info("Ring", "Text ve Url yazılmaz");   // sabit metindeki sözcükler sayılmaz
            // DebugLog.Write($"{text}") yorum satırı
            /* DebugLog.Write($"{text}") */
            PerfLog.Count("Ring.Paste");
            DebugLog.Write("bkz. https://example.com"); var keys = 1;   // dizedeki "//" yorum değildir
            DebugLog.Write($"SnippetPaste: {ex.GetType().Name}");
            DebugLog.Write($"hata {ex.HResult:X8} {failure.GetBaseException().GetType().Name}");
            DebugLog.Write($"{nameof(KeySender)}: {outcome} {count}");
            DebugLog.Write($@"C:\ yolu {{değil}} {count}");
            DebugLog.Write($"süre {(ok ? "tamam" : "yok")}");
            DebugLog.Write(')' + "(");
            Browser.Open(action.Url!, log: false);
            var text = "x"; var url = text;
            """;
        Assert.Empty(Offenders("x.cs", good));
    }

    private static List<string> Offenders(string name, string source)
    {
        var result = new List<string>();
        var code = StripComments(source);
        foreach (Match m in LogCall.Matches(code))
        {
            var visible = Visible(Arguments(code, m.Index + m.Length));
            if (Problem(visible) is { } problem) result.Add($"{name}:{LineOf(code, m.Index)}: {m.Value}… ({problem})");
        }
        foreach (Match m in BrowserOpen.Matches(code))
            if (!NoLogArgument.IsMatch(Visible(Arguments(code, m.Index + m.Length))))
                result.Add($"{name}:{LineOf(code, m.Index)}: {m.Value}… (log: false yok)");
        return result;
    }

    /// <summary>Argüman kodunda yasak ad ya da ham hata nesnesi; yoksa null.</summary>
    private static string? Problem(string visible)
    {
        var code = NameOf.Replace(SafeTypeName.Replace(visible, ".GetType()"), " ");
        if (RawException.Match(code) is { Success: true } raw) return raw.Value;
        foreach (Match id in Identifier.Matches(code))
        {
            var lower = id.Value.ToLowerInvariant();
            if (SensitiveWords.FirstOrDefault(lower.Contains) is { } word) return $"{id.Value}: {word}";
        }
        return null;
    }

    /// <summary>Açılan parantezden kapanana dek (dize ve karakter sabitlerindeki parantezler sayılmaz).</summary>
    private static string Arguments(string code, int start)
    {
        var depth = 1;
        for (var i = start; i < code.Length;)
        {
            if (LiteralEnd(code, i) is > 0 and var end) { i = end; continue; }
            var c = code[i];
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return code[start..i];
            i++;
        }
        return code[start..];
    }

    /// <summary>Görünen kod: dize/karakter sabitlerinin metni atılır, enterpolasyon deliklerinin kodu kalır.</summary>
    private static string Visible(string code)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < code.Length;)
        {
            var holes = new List<(int Start, int Length)>();
            if (LiteralEnd(code, i, holes) is > 0 and var end)
            {
                sb.Append(' ');
                foreach (var (s, l) in holes) sb.Append(Visible(code.Substring(s, l))).Append(' ');
                i = end;
                continue;
            }
            sb.Append(code[i++]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// <paramref name="i"/>'de bir dize ya da karakter sabiti başlıyorsa bittiği yerden sonraki sıra; değilse -1. Dize
    /// biçimleri: "…", @"…", $"…", $@"…", @$"…", ham """…""" ($'lı); enterpolasyon delikleri <paramref name="holes"/>'a yazılır
    /// (ham enterpolasyonlu dizenin içeriği bütünüyle delik sayılır: temkinli).
    /// </summary>
    private static int LiteralEnd(string code, int i, List<(int Start, int Length)>? holes = null)
    {
        if (code[i] == '\'')
        {
            var close = code.IndexOf('\'', i + (i + 1 < code.Length && code[i + 1] == '\\' ? 3 : 2));
            return close < 0 ? code.Length : close + 1;
        }

        var j = i;
        var dollars = 0;
        var verbatim = false;
        while (j < code.Length && j - i < 4 && code[j] is '$' or '@')
        {
            if (code[j] == '$') dollars++;
            else verbatim = true;
            j++;
        }
        if (j >= code.Length || code[j] != '"') return -1;

        var quotes = 0;
        while (j + quotes < code.Length && code[j + quotes] == '"') quotes++;
        if (quotes >= 3)
        {
            var start = j + quotes;
            var close = code.IndexOf(new string('"', quotes), start, StringComparison.Ordinal);
            if (close < 0) close = code.Length;
            if (dollars > 0) holes?.Add((start, close - start));
            return Math.Min(code.Length, close + quotes);
        }
        if (quotes == 2) return j + 2;   // ""

        var p = j + 1;
        while (p < code.Length)
        {
            var c = code[p];
            if (!verbatim && c == '\\') { p += 2; continue; }
            if (c == '"')
            {
                if (verbatim && p + 1 < code.Length && code[p + 1] == '"') { p += 2; continue; }
                return p + 1;
            }
            if (dollars > 0 && c is '{' or '}' && p + 1 < code.Length && code[p + 1] == c) { p += 2; continue; }
            if (dollars > 0 && c == '{')
            {
                var start = p + 1;
                var depth = 1;
                for (p = start; p < code.Length && depth > 0;)
                {
                    if (LiteralEnd(code, p) is > 0 and var end) { p = end; continue; }
                    if (code[p] == '{') depth++;
                    else if (code[p] == '}') depth--;
                    p++;
                }
                holes?.Add((start, Math.Max(0, p - 1 - start)));
                continue;
            }
            if (!verbatim && c == '\n') return p;   // kapanmamış sabit: satır sonunda bırak
            p++;
        }
        return code.Length;
    }

    /// <summary>Yorumları atar (satırlar korunur); dize ve karakter sabitlerinin içindeki "//" yorum sayılmaz.</summary>
    private static string StripComments(string source)
    {
        var sb = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length;)
        {
            if (LiteralEnd(source, i) is > 0 and var end)
            {
                sb.Append(source, i, end - i);
                i = end;
                continue;
            }
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }
            if (c == '/' && next == '*')
            {
                var close = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                close = close < 0 ? source.Length : close + 2;
                sb.Append('\n', source.AsSpan(i, close - i).Count('\n'));
                i = close;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static int LineOf(string code, int index) => code.AsSpan(0, index).Count('\n') + 1;

    private static IEnumerable<string> RingSources() =>
        Directory.EnumerateFiles(SourceRoot, "*.cs", SearchOption.AllDirectories).Where(f =>
        {
            var rel = Path.GetRelativePath(SourceRoot, f);
            if (rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) || rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase)) return false;
            var name = Path.GetFileName(f);
            return name.StartsWith("Ring", StringComparison.Ordinal) || name.StartsWith("Snippet", StringComparison.Ordinal) ||
                   name is "KeySender.cs" or "ForegroundInfo.cs";
        });

    private static string SourceRoot => Path.Combine(RepoRoot(), "src", "Duzenleme");

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
    }
}
