using System.Text;
using System.Text.RegularExpressions;

namespace Duzenleme.Tests;

/// <summary>
/// Halka gizliliği (PRIVACY §4, Store 10.5): yapıştırılan metin, web adresi, komut, gönderilen tuşlar ve ön plandaki sürecin
/// adı hiçbir günlüğe (DebugLog, AppLog, PerfLog, Debug/Trace/Console) girmez; ön plan bilgisi ve pano içeriği diske yazılmaz.
/// Kaynak taraması: Ring*, Snippet*, KeySender ve ForegroundInfo dosyaları (arayüz bölümü eklenince RingRunner, RingPopup…
/// kendiliğinden kapsanır).
/// </summary>
public class RingPrivacyTests
{
    /// <summary>Günlük çağrıları: argümanları taranır.</summary>
    private static readonly Regex LogCall =
        new(@"\b(DebugLog\.Write|AppLog\.\w+|PerfLog\.\w+|Debug\.Write(Line)?|Trace\.\w+|Console\.Write(Line)?)\s*\(");

    /// <summary>Günlüğe girmemesi gereken veri (argümanın kodunda, dize sabitlerinin dışında).</summary>
    private static readonly Regex Sensitive = new(
        @"\b(Text|Url|Uri|Command|Args|Arguments|Keys|Chords?|Snippet|ProcessName|Process|Title|Clipboard|Saved|Value|Data)\b",
        RegexOptions.IgnoreCase);

    /// <summary>Ön plan ve pano bilgisini taşıyan dosyalar diske/kayıt defterine yazmaz.</summary>
    private static readonly Regex DiskWrite =
        new(@"\b(File\.(Write|Append|Create|Copy|Move|Replace)\w*|StreamWriter|FileStream|Registry\w*|JsonFile\.Save|DurableFile)\b");

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
        const string bad = """
            DebugLog.Write($"yapıştırıldı: {text}");
            AppLog.Warn("RingRunner", action.Url);
            PerfLog.Write("ring " + ForegroundInfo.ProcessName(hwnd));
            Debug.WriteLine(string.Join(",", chords));
            AppLog.Info("Ring", $"sonuç {outcome} {snippet.Length}");
            """;
        Assert.Equal(5, Offenders("x.cs", bad).Count);

        const string good = """
            DebugLog.Write("halka açıldı");
            PerfLog.Write($"Ring.Open {elapsed.TotalMilliseconds:0} ms");
            AppLog.Info("Ring", "Text ve Url yazılmaz");   // sabit metindeki sözcükler sayılmaz
            // DebugLog.Write($"{text}") yorum satırı
            PerfLog.Count("Ring.Paste");
            DebugLog.Write("bkz. https://example.com"); var keys = 1;   // dizedeki "//" yorum değildir
            """;
        Assert.Empty(Offenders("x.cs", good));
    }

    private static List<string> Offenders(string name, string source)
    {
        var result = new List<string>();
        var code = StripComments(source);
        foreach (Match m in LogCall.Matches(code))
        {
            var args = Arguments(code, m.Index + m.Length);
            var visible = CodeOnly(args);
            if (Sensitive.Match(visible) is { Success: true } hit)
                result.Add($"{name}:{LineOf(code, m.Index)}: {m.Value}… ({hit.Value})");
        }
        return result;
    }

    /// <summary>Açılan parantezden kapanana dek (dize sabitlerindeki parantezler sayılmaz).</summary>
    private static string Arguments(string code, int start)
    {
        var depth = 1;
        var inString = false;
        for (var i = start; i < code.Length; i++)
        {
            var c = code[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') inString = true;
            else if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return code[start..i];
        }
        return code[start..];
    }

    /// <summary>Kod kısmı: düz dize sabitleri atılır, enterpolasyonlu dizelerden yalnızca {…} delikleri kalır.</summary>
    private static string CodeOnly(string args)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < args.Length; i++)
        {
            var interpolated = args[i] == '$' && i + 1 < args.Length && args[i + 1] == '"';
            if (interpolated) i++;
            if (args[i] != '"')
            {
                sb.Append(args[i]);
                continue;
            }
            var hole = 0;
            for (i++; i < args.Length; i++)
            {
                var c = args[i];
                if (hole == 0 && c == '\\') { i++; continue; }
                if (interpolated && c == '{' && i + 1 < args.Length && args[i + 1] == '{' && hole == 0) { i++; continue; }
                if (interpolated && c == '{') { hole++; sb.Append(' '); continue; }
                if (interpolated && c == '}' && hole > 0) { hole--; sb.Append(' '); continue; }
                if (hole > 0) { sb.Append(c); continue; }
                if (c == '"') break;
            }
            sb.Append(' ');
        }
        return sb.ToString();
    }

    /// <summary>Yorumları atar (satırlar korunur); dize ve karakter sabitlerinin içindeki "//" yorum sayılmaz.</summary>
    private static string StripComments(string source)
    {
        var sb = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                if (i < source.Length) sb.Append('\n');
                continue;
            }
            if (c == '/' && next == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                sb.Append('\n', source.AsSpan(i, end - i).Count('\n'));
                i = end - 1;
                continue;
            }
            if (c is '"' or '\'')
            {
                var verbatim = c == '"' && i > 0 && source[i - 1] == '@';
                sb.Append(c);
                for (i++; i < source.Length; i++)
                {
                    sb.Append(source[i]);
                    if (!verbatim && source[i] == '\\' && i + 1 < source.Length) { sb.Append(source[++i]); continue; }
                    if (source[i] == c)
                    {
                        if (verbatim && i + 1 < source.Length && source[i + 1] == '"') { sb.Append(source[++i]); continue; }
                        break;
                    }
                    if (source[i] == '\n' && !verbatim) break;   // kapanmamış sabit: satır sonunda bırak
                }
                continue;
            }
            sb.Append(c);
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
