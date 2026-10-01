using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Kısayol kutusuna bırakılan bağlantılar (2.2): yalnızca http/https/mailto, ASCII ve sınırlı .url dosyası, güvenli dosya adı,
/// aynı adres aynı dosya; açarken dosyadaki adres yeniden denetlenir (elle değiştirilmiş dosya file:/javascript: açtıramaz).
/// </summary>
public sealed class LinkFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nd-links-" + Guid.NewGuid().ToString("N"), LinkFiles.FolderName);

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_dir)!, recursive: true); }
        catch (IOException) { } // klasör hiç açılmadıysa da (DirectoryNotFoundException)
    }

    [Theory]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("  HTTP://Example.COM/Yol?q=1#x  ", "http://example.com/Yol?q=1#x")]
    [InlineData("https://example.com/a b", "https://example.com/a%20b")]
    [InlineData("mailto:ali@example.com", "mailto:ali@example.com")]
    [InlineData("mailto:ali@example.com?subject=Merhaba", "mailto:ali@example.com?subject=Merhaba")]
    public void Allowed_links_are_normalized(string input, string expected) => Assert.Equal(expected, LinkFiles.Normalize(input));

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    [InlineData(@"\\sunucu\paylaşım\x.txt")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("shell:AppsFolder")]
    [InlineData("ms-settings:privacy")]
    [InlineData("ftp://example.com/x")]
    [InlineData("example.com")]
    [InlineData("/yol/göreli")]
    [InlineData("http://")]
    [InlineData("mailto:")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Other_schemes_and_non_links_are_rejected(string? input) => Assert.Null(LinkFiles.Normalize(input));

    [Theory]
    [InlineData("https://example.com/\r\nURL=file:///C:/x")]
    [InlineData("https://example.com/\nx")]
    [InlineData("https://example.com/\tx")]
    [InlineData("https://example.com/\0x")]
    public void Line_breaks_and_control_characters_are_rejected(string input) => Assert.Null(LinkFiles.Normalize(input));

    [Fact]
    public void Length_is_limited()
    {
        var at = "https://example.com/" + new string('a', LinkFiles.MaxLength - "https://example.com/".Length);
        Assert.Equal(LinkFiles.MaxLength, at.Length);
        Assert.Equal(at, LinkFiles.Normalize(at));
        Assert.Null(LinkFiles.Normalize(at + "a"));
        // Kaçışla uzayan adres de sınırı aşamaz.
        Assert.Null(LinkFiles.Normalize("https://example.com/" + new string('ş', 1000)));
    }

    [Fact]
    public void Unicode_hosts_and_paths_become_ascii()
    {
        var url = LinkFiles.Normalize("https://müller.de/çay?ş=ğ");
        Assert.NotNull(url);
        Assert.StartsWith("https://xn--mller-kva.de/", url);
        Assert.All(url!, c => Assert.InRange(c, '!', '~'));
        var mail = LinkFiles.Normalize("mailto:ayşe@örnek.com");
        Assert.NotNull(mail);
        Assert.StartsWith("mailto:", mail);
        Assert.All(mail!, c => Assert.InRange(c, '!', '~'));
    }

    [Theory]
    [InlineData("https://example.com", LinkKind.Allowed)]
    [InlineData("  https://example.com/x  ", LinkKind.Allowed)]
    [InlineData("mailto:ali@example.com", LinkKind.Allowed)]
    [InlineData("javascript:alert(1)", LinkKind.Blocked)]
    [InlineData("file:///C:/x.txt", LinkKind.Blocked)]
    [InlineData("shell:startup", LinkKind.Blocked)]
    [InlineData("ms-settings:display", LinkKind.Blocked)]
    [InlineData("ftp://example.com", LinkKind.Blocked)]
    [InlineData("merhaba dünya", LinkKind.None)]
    [InlineData(@"C:\Users\ali\x.txt", LinkKind.None)]
    [InlineData("Saat:12", LinkKind.None)]
    [InlineData("https://example.com\nhttps://example.org", LinkKind.None)]
    [InlineData("", LinkKind.None)]
    public void Dropped_text_is_classified(string text, LinkKind expected) => Assert.Equal(expected, LinkFiles.ClassifyText(text, out _));

    [Fact]
    public void Url_formats_count_as_links_even_when_blocked()
    {
        Assert.Equal(LinkKind.Allowed, LinkFiles.ClassifyUrl("https://example.com", out var url));
        Assert.Equal("https://example.com/", url);
        Assert.Equal(LinkKind.Blocked, LinkFiles.ClassifyUrl("file:///C:/x", out url));
        Assert.Null(url);
        Assert.Equal(LinkKind.Blocked, LinkFiles.ClassifyUrl("Saat:12", out _)); // kaynak bağlantı dedi
        Assert.Equal(LinkKind.None, LinkFiles.ClassifyUrl("  ", out _));
    }

    [Fact]
    public void Create_writes_a_small_ascii_url_file_with_a_safe_name()
    {
        var link = LinkFiles.Create("https://www.Example.com/sayfa?a=1", "  Örnek\tsayfa\r\n ", _dir);
        Assert.Equal(_dir, Path.GetDirectoryName(link.Path));
        Assert.Matches(new Regex("^www\\.example\\.com-[0-9a-f]{8}\\.url$"), Path.GetFileName(link.Path));
        Assert.Equal("Örnek sayfa", link.Name);
        var bytes = File.ReadAllBytes(link.Path);
        Assert.All(bytes, b => Assert.True(b < 0x80));
        Assert.Equal("[InternetShortcut]\r\nURL=https://www.example.com/sayfa?a=1\r\n", Encoding.ASCII.GetString(bytes));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
        Assert.Equal("https://www.example.com/sayfa?a=1", LinkFiles.ReadUrl(link.Path));
    }

    [Fact]
    public void Same_address_reuses_its_file_and_others_get_their_own()
    {
        var a = LinkFiles.Create("https://example.com/a", null, _dir);
        var written = File.GetLastWriteTimeUtc(a.Path);
        var again = LinkFiles.Create(" https://EXAMPLE.com/a ", "Başlık", _dir);
        Assert.Equal(a.Path, again.Path);
        Assert.Equal(written, File.GetLastWriteTimeUtc(again.Path));
        Assert.Equal("Başlık", again.Name);
        var b = LinkFiles.Create("https://example.com/b", null, _dir);
        Assert.NotEqual(a.Path, b.Path);
        Assert.Equal(2, Directory.GetFiles(_dir).Length);
    }

    [Fact]
    public void A_name_taken_by_another_address_moves_to_the_next_name()
    {
        var url = LinkFiles.Normalize("https://example.com/x")!;
        Directory.CreateDirectory(_dir);
        var taken = Path.Combine(_dir, LinkFiles.FileStem(url) + ".url");
        File.WriteAllText(taken, LinkFiles.Content("https://example.com/baska"));
        var link = LinkFiles.Create(url, null, _dir);
        Assert.Equal(Path.Combine(_dir, LinkFiles.FileStem(url) + "-2.url"), link.Path);
        Assert.Equal("https://example.com/baska", LinkFiles.ReadUrl(taken)); // var olan dosyaya dokunulmaz
    }

    [Fact]
    public void Create_refuses_links_that_are_not_allowed()
    {
        Assert.Throws<ArgumentException>(() => LinkFiles.Create("file:///C:/x", null, _dir));
        Assert.Throws<ArgumentException>(() => LinkFiles.Create("javascript:alert(1)", null, _dir));
        Assert.False(Directory.Exists(_dir));
    }

    [Theory]
    [InlineData("https://www.example.com/x", "example.com")]
    [InlineData("https://www/x", "www")]
    [InlineData("https://xn--mller-kva.de/", "müller.de")]
    [InlineData("mailto:ali@example.com?subject=x", "ali@example.com")]
    public void Without_a_title_the_host_or_address_is_shown(string url, string expected) =>
        Assert.Equal(expected, LinkFiles.DefaultName(LinkFiles.Normalize(url)!));

    [Fact]
    public void File_names_use_only_safe_characters()
    {
        foreach (var url in new[] { "https://con/", "https://NUL/", "https://-a.b-/", "http://[::1]:8080/", "mailto:x@y.z",
                     "https://" + new string('a', 63) + "." + new string('b', 63) + ".com/" })
        {
            var stem = LinkFiles.FileStem(LinkFiles.Normalize(url)!);
            Assert.Matches(new Regex("^[a-z0-9.-]{1,60}-[0-9a-f]{8}$"), stem);
            Assert.False(stem.StartsWith('.') || stem.StartsWith('-'), stem);
        }
        Assert.StartsWith("con-", LinkFiles.FileStem("https://con/")); // ayrılmış ad değil: özet hep eklenir
        Assert.StartsWith("mailto-", LinkFiles.FileStem("mailto:x@y.z"));
    }

    [Theory]
    [InlineData("[InternetShortcut]\r\nURL=file:///C:/Windows/System32/calc.exe\r\n")]
    [InlineData("[InternetShortcut]\r\nURL=javascript:alert(1)\r\n")]
    [InlineData("[InternetShortcut]\r\nURL=shell:AppsFolder\\x\r\n")]
    [InlineData("[Başka]\r\nURL=https://example.com/\r\n")]
    [InlineData("URL=https://example.com/\r\n")]
    [InlineData("")]
    public void Changed_link_files_do_not_open_other_schemes(string content) => Assert.Null(LinkFiles.ParseUrlFile(content));

    [Fact]
    public void Link_files_written_by_windows_are_read()
    {
        Assert.Equal("https://example.com/", LinkFiles.ParseUrlFile("[{000214A0-0000-0000-C000-000000000046}]\nProp3=19,11\n[InternetShortcut]\nIDList=\nURL=https://example.com/\nIconIndex=0\n"));
        Directory.CreateDirectory(_dir);
        var big = Path.Combine(_dir, "buyuk.url");
        File.WriteAllText(big, LinkFiles.Content("https://example.com/") + new string(';', 20_000));
        Assert.Null(LinkFiles.ReadUrl(big)); // aşırı büyük dosya okunmaz
        Assert.Null(LinkFiles.ReadUrl(Path.Combine(_dir, "yok.url")));
    }

    [Fact]
    public void Only_url_files_in_the_links_folder_are_ours()
    {
        Assert.True(LinkFiles.IsLinkFile(Path.Combine(_dir, "example.com-0011aabb.url"), _dir));
        Assert.True(LinkFiles.IsLinkFile(Path.Combine(_dir, "X.URL"), _dir + Path.DirectorySeparatorChar));
        Assert.False(LinkFiles.IsLinkFile(Path.Combine(_dir, "x.lnk"), _dir));
        Assert.False(LinkFiles.IsLinkFile(Path.Combine(_dir, "alt", "x.url"), _dir));
        Assert.False(LinkFiles.IsLinkFile(Path.Combine(_dir, "..", "x.url"), _dir));
        Assert.False(LinkFiles.IsLinkFile(@"C:\Users\ali\Desktop\x.url", _dir));
        Assert.False(LinkFiles.IsLinkFile(Path.Combine(_dir, "x.url"), ""));
    }

    [Fact]
    public void Link_files_written_under_another_data_folder_are_recognized()
    {
        // Taşınabilir sürümün sürücü harfi değişti ya da eski veri klasörü taşındı: ad ve "links" klasörü bizim biçimimizde.
        Assert.True(LinkFiles.LooksOwn(@"E:\NestDesk\data\links\example.com-0011aabb.url"));
        Assert.True(LinkFiles.LooksOwn(@"C:\Users\ali\AppData\Roaming\Duzenleme\links\mailto-0011aabb-2.url"));
        Assert.True(LinkFiles.LooksOwn(@"D:\x\LINKS\Example.COM-0011AABB.URL"));
        Assert.False(LinkFiles.LooksOwn(@"D:\x\linkler\example.com-0011aabb.url"));     // başka klasör
        Assert.False(LinkFiles.LooksOwn(@"D:\x\links\Örnek sayfa.url"));                // kullanıcının kendi .url'si
        Assert.False(LinkFiles.LooksOwn(@"D:\x\links\example.com-0011aab.url"));        // özet 8 hane değil
        Assert.False(LinkFiles.LooksOwn(@"D:\x\links\example.com-0011aabb.lnk"));
        Assert.False(LinkFiles.LooksOwn(@"::{4234D49B-0245-4DF3-B780-3893943456E1}\App"));
        Assert.False(LinkFiles.LooksOwn(""));

        var stale = @"E:\NestDesk\data\links\example.com-0011aabb.url";
        Assert.Equal(Path.Combine(_dir, "example.com-0011aabb.url"), LinkFiles.Rebase(stale, _dir));
        Assert.Null(LinkFiles.Rebase(Path.Combine(_dir, "example.com-0011aabb.url"), _dir)); // zaten burada
        Assert.Null(LinkFiles.Rebase(@"C:\Users\ali\Desktop\x.url", _dir));
        Assert.Null(LinkFiles.Rebase(stale, ""));
    }

    [Fact]
    public void Box_links_from_another_data_folder_are_moved_to_this_one_with_their_names()
    {
        var old = @"E:\NestDesk\data\links\";
        var box = new WidgetConfig
        {
            Kind = WidgetKind.Launcher,
            Tabs =
            [
                new LauncherTab { Name = "A", Items = [old + "example.com-0011aabb.url", @"C:\Windows\notepad.exe", old + "mailto-22334455.url"] },
                // Aynı bağlantı hem eski hem yeni yoluyla: eski çıkar, yineleme olmaz.
                new LauncherTab { Name = "B", Items = [old + "example.com-0011aabb.url", Path.Combine(_dir, "example.com-0011aabb.url")] },
            ],
        };
        ItemLooks.SetName(box, old + "example.com-0011aabb.url", "Örnek sayfa", "example.com-0011aabb");
        var plain = new WidgetConfig { Kind = WidgetKind.Clock };

        Assert.True(LinkFiles.RebaseAll([box, plain], _dir));

        Assert.Equal([Path.Combine(_dir, "example.com-0011aabb.url"), @"C:\Windows\notepad.exe", Path.Combine(_dir, "mailto-22334455.url")],
            box.Tabs[0].Items);
        Assert.Equal([Path.Combine(_dir, "example.com-0011aabb.url")], box.Tabs[1].Items);
        Assert.Equal("Örnek sayfa", ItemLooks.Get(box, Path.Combine(_dir, "example.com-0011aabb.url"))?.Name);
        Assert.Null(ItemLooks.Get(box, old + "example.com-0011aabb.url"));
        Assert.False(LinkFiles.RebaseAll([box, plain], _dir)); // ikinci kez: değişiklik yok
    }

    [Fact]
    public void Firefox_link_data_gives_address_and_title()
    {
        var (url, title) = LinkFiles.ParseMozUrl("https://example.com/\nÖrnek\0");
        Assert.Equal("https://example.com/", url);
        Assert.Equal("Örnek", title);
        (url, title) = LinkFiles.ParseMozUrl("https://example.com/\r\n");
        Assert.Equal("https://example.com/", url);
        Assert.Null(title);
        (url, title) = LinkFiles.ParseMozUrl("");
        Assert.Null(url);
        Assert.Null(title);
    }

    [Fact]
    public void Browser_file_descriptor_gives_the_page_title()
    {
        Assert.Equal("Örnek sayfa", LinkFiles.TitleFromFileGroupDescriptor(Descriptor("Örnek sayfa.url")));
        Assert.Null(LinkFiles.TitleFromFileGroupDescriptor(Descriptor("resim.png")));      // bağlantı değil
        Assert.Null(LinkFiles.TitleFromFileGroupDescriptor(Descriptor("x.url", count: 0)));
        Assert.Null(LinkFiles.TitleFromFileGroupDescriptor(Descriptor("x.url").AsSpan(0, 300)));
        Assert.Equal(ItemLooks.MaxName, LinkFiles.TitleFromFileGroupDescriptor(Descriptor(new string('a', 150) + ".url"))!.Length);
    }

    [Fact]
    public void Clean_names_drop_control_characters_and_are_cut()
    {
        Assert.Equal("a b c", ItemLooks.CleanName(" a\r\n b\t\tc \0"));
        Assert.Null(ItemLooks.CleanName("\r\n\t "));
        Assert.Null(ItemLooks.CleanName(null));
        Assert.Equal(ItemLooks.MaxName, ItemLooks.CleanName(new string('x', 500))!.Length);
        // Vekil çift ortadan bölünmez.
        var emoji = new string('x', ItemLooks.MaxName - 1) + "😀";
        Assert.Equal(new string('x', ItemLooks.MaxName - 1), ItemLooks.CleanName(emoji));
    }

    /// <summary>FILEGROUPDESCRIPTORW: sayı + 592 baytlık FILEDESCRIPTORW (ad 72. bayttan, 260 UTF-16 karakter).</summary>
    private static byte[] Descriptor(string name, uint count = 1)
    {
        var data = new byte[4 + 592];
        BinaryPrimitives.WriteUInt32LittleEndian(data, count);
        var text = Encoding.Unicode.GetBytes(name.Length > 259 ? name[..259] : name);
        text.CopyTo(data, 4 + 72);
        return data;
    }
}
