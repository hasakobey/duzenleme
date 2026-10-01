using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>Hata günlüğü temizleyicisi: yol, dosya/klasör adı, anahtar, kullanıcı/bilgisayar adı, URL, e-posta hiçbir biçimde sızmaz.</summary>
public class LogScrubberTests
{
    private const string Profile = @"C:\Users\Zeynep";

    private static LogScrubber Scrubber(string user = "Zeynep", string machine = "MASA-PC") => new(new ScrubContext
    {
        Desktop = [Profile + @"\Desktop", Profile + @"\OneDrive\Masaüstü"],
        PublicDesktop = @"C:\Users\Public\Desktop",
        Data = Profile + @"\AppData\Roaming\NestDesk",
        Box = Profile + @"\NestDesk",
        UserNames = [user],
        MachineName = machine,
    });

    private static Exception Thrown(Exception ex)
    {
        try { throw ex; }
        catch (Exception caught) { return caught; }
    }

    private static string Json(ScrubbedError error)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer)) error.WriteJson(json);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    // ------------------------------------------------------------------ yollar

    [Theory]
    [InlineData(Profile + @"\Desktop\Faturalar\ocak.pdf", "desktop")]
    [InlineData("C:/Users/Zeynep/Desktop/Faturalar/ocak.pdf", "desktop")]
    [InlineData(@"c:\users\zeynep\desktop\x.txt", "desktop")]
    [InlineData(Profile + @"\OneDrive\Masaüstü\Rapor (2).docx", "desktop")]
    [InlineData(@"C:\Users\Public\Desktop\Oyun.lnk", "public-desktop")]
    [InlineData(Profile + @"\AppData\Roaming\NestDesk\settings.json", "data")]
    [InlineData(Profile + @"\NestDesk\Kutu\a.txt", "box")]
    [InlineData(@"\\sunucu\paylaşım\gizli.xlsx", "network")]
    [InlineData(@"\\?\UNC\sunucu\paylaşım\x.txt", "network")]
    [InlineData(@"\\?\C:\Users\Zeynep\Desktop\x.txt", "desktop")]
    [InlineData("file:///C:/Users/Zeynep/Desktop/x%20y.txt", "desktop")]
    [InlineData("file://sunucu/pay/x.txt", "network")]
    [InlineData(@"D:\Arşiv\özel.zip", "other")]
    [InlineData(@"%USERPROFILE%\Desktop\x.txt", "other")]
    public void Paths_become_typed_placeholders(string path, string kind)
    {
        Assert.Equal($"Could not process <path:{kind}>", Scrubber().Text("Could not process " + path));
        Assert.Equal($"Dosya '<path:{kind}>' açılamadı", Scrubber().Text($"Dosya '{path}' açılamadı"));
    }

    [Fact]
    public void Path_ends_at_quote_or_line_end_and_keeps_trailing_punctuation_only()
    {
        var s = Scrubber();
        Assert.Equal("a \"<path:desktop>\" b", s.Text($"a \"{Profile}\\Desktop\\Gizli Dosya.txt\" b"));
        Assert.Equal("<path:other>.\nnext", s.Text(@"E:\Klasör\x.pdf." + "\nnext"));
    }

    [Fact]
    public void Times_and_urls_are_not_paths()
    {
        var s = Scrubber();
        Assert.Equal("at 10:30 ok", s.Text("at 10:30 ok"));
        Assert.Equal("see <url>", s.Text("see https://example.com/C:/x"));
    }

    private static readonly string[] Roots =
    [
        Profile + @"\Desktop", Profile + @"\OneDrive\Masaüstü", @"C:\Users\Public\Desktop", Profile + @"\AppData\Roaming\NestDesk",
        Profile + @"\NestDesk", @"\\sunucu\paylaşım", @"D:\Yedek", @"E:", @"\\?\C:\Uzun",
    ];

    private static readonly string[] Templates =
    [
        "Could not open {0}", "Dosya '{0}' bulunamadı", "\"{0}\" kilitli", "{0}: locked by another process", "Kaynak: {0}\nikinci satır",
        "“{0}” taşınamadı", "path={0}", "[{0}]", "({0})",
    ];

    private const string Letters = "abcçdefgğhıijklmnoöprsştuüvyzABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ0123456789";

    private static string RandomName(Random rng)
    {
        var sb = new StringBuilder("Qx");
        var length = rng.Next(4, 12);
        for (var i = 0; i < length; i++)
            sb.Append(rng.Next(8) == 0 && i > 0 && i < length - 1 ? ' ' : Letters[rng.Next(Letters.Length)]);
        return sb.ToString();
    }

    [Fact]
    public void Random_paths_never_leak_any_segment()
    {
        var rng = new Random(20261001);
        var scrubber = Scrubber();
        string[] extensions = ["pdf", "docx", "lnk", "url", "txt", "jpeg", "tar.gz", ""];
        for (var i = 0; i < 2000; i++)
        {
            var segments = Enumerable.Range(0, rng.Next(0, 5)).Select(_ => RandomName(rng)).ToList();
            var ext = extensions[rng.Next(extensions.Length)];
            var stem = RandomName(rng);
            var file = ext.Length == 0 ? stem : stem + "." + ext;
            var sep = rng.Next(3) == 0 ? "/" : @"\";
            var root = Roots[rng.Next(Roots.Length)];
            if (sep == "/" && !root.StartsWith(@"\\", StringComparison.Ordinal)) root = root.Replace('\\', '/');
            var path = root + sep + string.Concat(segments.Select(s => s + sep)) + file;
            var input = string.Format(Templates[rng.Next(Templates.Length)], path);
            var output = scrubber.Text(input);
            foreach (var secret in segments.Append(stem).Append(file))
                Assert.True(output.IndexOf(secret, StringComparison.OrdinalIgnoreCase) < 0, $"\"{secret}\" sızdı: {input} → {output}");
            Assert.DoesNotContain("Zeynep", output, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Random_paths_in_exceptions_never_leak()
    {
        var rng = new Random(7);
        var scrubber = Scrubber();
        for (var i = 0; i < 200; i++)
        {
            var name = RandomName(rng);
            var path = Roots[rng.Next(Roots.Length)] + @"\" + name + ".pdf";
            var ex = Thrown(new InvalidOperationException($"Cannot open {path} now", Thrown(new IOException($"'{path}' is locked"))));
            var json = Json(scrubber.Error(ex));
            Assert.DoesNotContain(name, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------ tırnaklar ve anahtarlar

    [Theory]
    [InlineData("Klasör 'Faturalar' bulunamadı", "Klasör '<text>' bulunamadı")]
    [InlineData("Folder \"PDF\" is missing", "Folder \"<text>\" is missing")]
    [InlineData("Item “todo” failed", "Item “<text>” failed")]
    [InlineData("Tab ‘Oyunlar’ x", "Tab ‘<text>’ x")]
    [InlineData("«Notlar» yok", "«<text>» yok")]
    [InlineData("NestDesk'in 'Gizli' klasörü", "NestDesk'in '<text>' klasörü")]
    [InlineData("can't open 'abc'", "can't open '<text>'")]
    [InlineData("Bad 'Gizli value\nnext line", "Bad '<text>\nnext line")]
    [InlineData("Value cannot be null. (Parameter 'path')", "Value cannot be null. (Parameter 'path')")]
    [InlineData("Unable to cast 'System.String' to 'Duzenleme.Core.Rule'", "Unable to cast 'System.String' to 'Duzenleme.Core.Rule'")]
    [InlineData("Type 'Faturalar.Core.Rule' x", "Type '<text>' x")]
    [InlineData("(Parameter 'Gizli Ad')", "(Parameter '<text>')")]
    public void Quoted_text_is_removed_except_code_names(string input, string expected) =>
        Assert.Equal(expected, Scrubber().Text(input));

    [Theory]
    [InlineData("An item with the same key has already been added. Key: Faturalar", "An item with the same key has already been added. Key: <text>")]
    [InlineData("Key: C:\\Users\\Zeynep\\Desktop\\x.lnk", "Key: <text>")]
    [InlineData("duplicate key: abc def\nline 2", "duplicate Key: <text>\nline 2")]
    public void Everything_after_key_is_removed(string input, string expected) =>
        Assert.Equal(expected, Scrubber().Text(input));

    // ------------------------------------------------------------------ istisnalar

    public static TheoryData<Exception> MessageDroppingExceptions => new()
    {
        new IOException(@"The process cannot access the file 'C:\Users\Zeynep\Desktop\Gizli.pdf'"),
        new FileNotFoundException("Could not find file 'Gizli.pdf'", "Gizli.pdf"),
        new DirectoryNotFoundException(@"Could not find a part of the path 'C:\Gizli'"),
        new PathTooLongException("Gizli uzun"),
        new UnauthorizedAccessException("Access to the path 'Gizli' is denied."),
        new Win32Exception(5, "Gizli erişim"),
        new COMException("Gizli COM", unchecked((int)0x80004005)),
        new ExternalException("Gizli dış"),
        new ArgumentException("An item with the same key has already been added. Key: Gizli"),
        new ArgumentNullException("path", "Gizli boş"),
        new ArgumentOutOfRangeException("index", "Gizli değer", "Gizli aralık"),
        new KeyNotFoundException("The given key 'Gizli' was not present in the dictionary."),
    };

    [Theory]
    [MemberData(nameof(MessageDroppingExceptions))]
    public void Io_win32_com_argument_exceptions_keep_only_type_hresult_and_param(Exception ex)
    {
        var error = Scrubber().Error(Thrown(ex));
        Assert.Null(error.Message);
        Assert.Equal(ex.GetType().FullName, error.Type);
        Assert.Equal("0x" + ex.HResult.ToString("X8"), error.HResult);
        Assert.Equal((ex as ArgumentException)?.ParamName, error.ParamName);
        var json = Json(error);
        Assert.DoesNotContain("Gizli", json);
        Assert.DoesNotContain("Zeynep", json);
    }

    [Fact]
    public void Other_exceptions_keep_a_scrubbed_message()
    {
        var error = Scrubber().Error(Thrown(new InvalidOperationException($"Cannot open 'Faturalar' at {Profile}\\Desktop\\x.pdf for zeynep")));
        Assert.Equal("Cannot open '<text>' at <path:desktop>", error.Message);
        Assert.Equal("System.InvalidOperationException", error.Type);
    }

    [Fact]
    public void Inner_exceptions_are_limited_and_scrubbed()
    {
        var aggregate = new AggregateException(Enumerable.Range(0, 5).Select(i => Thrown(new IOException("Gizli " + i))));
        var error = Scrubber().Error(Thrown(aggregate));
        Assert.Equal(LogScrubber.MaxInner, error.Inner.Count);
        Assert.All(error.Inner, i => Assert.Null(i.Message));

        Exception deep = new InvalidOperationException("a");
        for (var i = 0; i < 6; i++) deep = new InvalidOperationException("b", deep);
        var chain = Scrubber().Error(deep);
        var depth = 0;
        for (var e = chain; e.Inner.Count > 0; e = e.Inner[0]) depth++;
        Assert.Equal(LogScrubber.MaxInner, depth);
    }

    private sealed class DataTrap(string message) : Exception(message)
    {
        public bool Touched { get; private set; }
        public override IDictionary Data
        {
            get
            {
                Touched = true;
                return base.Data;
            }
        }
    }

    [Fact]
    public void Exception_data_is_never_read()
    {
        var trap = new DataTrap("x");
        Scrubber().Error(Thrown(trap));
        Assert.False(trap.Touched);
    }

    [Fact]
    public void Message_length_is_capped()
    {
        var text = Scrubber().Text(new string('a', 1000));
        Assert.True(text.Length <= LogScrubber.MaxMessage + 1);
    }

    // ------------------------------------------------------------------ yığın kareleri

    [Fact]
    public void Frames_keep_code_names_and_repo_relative_sources()
    {
        var s = Scrubber(user: "Ali");
        var frames = s.Frames(string.Join("\n",
            "   at Duzenleme.Views.Ali.Open(String path) in /_/src/Duzenleme/Views/Ali.cs:line 12",
            "   at Duzenleme.Core.Alignment.Fit() in C:\\Users\\Ali\\Desktop\\duzenleme\\src\\Duzenleme\\Core\\Alignment.cs:line 7",
            "--- End of stack trace from previous location ---",
            "   at System.IO.File.Move(String a, String b) in D:\\a\\_work\\1\\s\\src\\libraries\\System.Private.CoreLib\\src\\System\\IO\\File.cs:line 99",
            "   at Lib.X() in C:\\Users\\Ali\\Belgeler\\Gizli Klasör\\x y.cs:line 3",
            "   at Duzenleme.App.Run()"));
        Assert.Equal(
        [
            "Duzenleme.Views.Ali.Open(String path) in /_/src/Duzenleme/Views/Ali.cs:line 12",
            "Duzenleme.Core.Alignment.Fit() in /_/src/Duzenleme/Core/Alignment.cs:line 7",
            "System.IO.File.Move(String a, String b) in File.cs:line 99",
            "Lib.X() in <path>:line 3",
            "Duzenleme.App.Run()",
        ], frames);
    }

    [Fact]
    public void Frames_are_capped()
    {
        var trace = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"   at Duzenleme.X.M{i}()"));
        Assert.Equal(LogScrubber.MaxFrames, Scrubber().Frames(trace).Count);
    }

    [Fact]
    public void Real_exception_has_first_app_frame()
    {
        var error = Scrubber().Error(Thrown(new InvalidOperationException("x")));
        Assert.NotEmpty(error.Frames);
        Assert.StartsWith("Duzenleme.Tests.LogScrubberTests.Thrown", error.FirstAppFrame);
        Assert.DoesNotContain(" in ", error.FirstAppFrame);
    }

    // ------------------------------------------------------------------ adlar

    [Fact]
    public void User_name_only_as_whole_word_of_three_letters_and_never_in_frames()
    {
        var s = Scrubber(user: "Ali");
        Assert.Equal("<user> tarafından açıldı", s.Text("Ali tarafından açıldı"));
        Assert.Equal("<user>'nin dosyası", s.Text("ali'nin dosyası"));
        Assert.Equal("Alignment failed for Alicia", s.Text("Alignment failed for Alicia"));
        Assert.Equal("mutex Duzenleme.<user>.test", s.Text("mutex Duzenleme.ali.test"));
        Assert.Equal("Duzenleme.Views.Ali.Open()", s.Frame("   at Duzenleme.Views.Ali.Open()"));

        var shortName = Scrubber(user: "Al");
        Assert.Equal("Al geldi", shortName.Text("Al geldi"));
        Assert.Equal("Computer <pc> is offline", s.Text("Computer masa-pc is offline"));
    }

    [Fact]
    public void Placeholders_are_never_replaced_by_names()
    {
        var s = Scrubber(user: "text", machine: "user");
        Assert.Equal("<user> <pc> at <path:desktop>", s.Text($"text user at {Profile}\\Desktop\\x"));
    }

    // ------------------------------------------------------------------ URL, e-posta, GUID

    [Theory]
    [InlineData("see https://example.com/a?b=c now", "see <url> now")]
    [InlineData("GET https://api.github.com/repos/x/y failed", "GET <url:api.github.com> failed")]
    [InlineData("POST https://api.anthropic.com/v1/messages", "POST <url:api.anthropic.com>")]
    [InlineData("ftp://kullanici:sifre@host/x", "<url>")]
    [InlineData("mail zeynep.k@example.com.tr bitti", "mail <email> bitti")]
    [InlineData("id 3f2504e0-4f89-11d3-9a0c-0305e82c3301 x", "id <id:3f2504> x")]
    [InlineData("widget {3F2504E0-4F89-11D3-9A0C-0305E82C3301}", "widget {<id:3f2504>}")]
    [InlineData("widget 0123456789abcdef0123456789ABCDEF yok", "widget <id:012345> yok")]
    [InlineData("hr 0x80070005", "hr 0x80070005")]
    public void Urls_emails_and_ids_are_shortened(string input, string expected) =>
        Assert.Equal(expected, Scrubber().Text(input));

    [Fact]
    public void Scrubbing_is_idempotent()
    {
        var s = Scrubber();
        var once = s.Text($"Cannot open '{Profile}\\Desktop\\x' (Parameter 'path') https://x.org Key: abc");
        Assert.Equal(once, s.Text(once));
        Assert.Equal(once, LogScrubber.Plain.Text(once));
    }
}
