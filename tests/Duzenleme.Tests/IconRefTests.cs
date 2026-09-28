using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>Kullanıcının seçtiği simgenin metin biçimi (WidgetConfig.Icon, ItemLook.Icon) ve resim kopyaları.</summary>
public sealed class IconRefTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nestdesk-icons-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Symbol_round_trips()
    {
        var parsed = IconRef.Parse("sym:Games24");
        Assert.Equal(new IconRef(IconRefKind.Symbol, "Games24"), parsed);
        Assert.Equal("sym:Games24", parsed!.Value.ToString());
        Assert.Equal("sym:Rocket24", IconRef.Symbol("Rocket24"));
    }

    [Fact]
    public void Resource_path_may_contain_commas_and_variables()
    {
        var parsed = IconRef.Parse(@"res:%SystemRoot%\System32\a,b.dll,-12");
        Assert.Equal(IconRefKind.Resource, parsed!.Value.Kind);
        Assert.Equal(@"%SystemRoot%\System32\a,b.dll", parsed.Value.Value);
        Assert.Equal(-12, parsed.Value.Index);
        Assert.Equal(@"res:%SystemRoot%\System32\a,b.dll,-12", parsed.Value.ToString());
        Assert.DoesNotContain("%", parsed.Value.ExpandedPath);
        Assert.Equal(@"res:C:\x.ico,3", IconRef.Resource(@"C:\x.ico", 3));
    }

    [Fact]
    public void Image_accepts_only_a_stored_file_name()
    {
        var name = new string('a', 32) + ".png";
        Assert.Equal(new IconRef(IconRefKind.Image, name), IconRef.Parse("img:" + name));
        Assert.Null(IconRef.Parse(@"img:..\..\settings.json"));
        Assert.Null(IconRef.Parse(@"img:C:\Windows\x.png"));
        Assert.Null(IconRef.Parse("img:" + new string('a', 32) + ".exe"));
        Assert.Null(IconFiles.PathOf("../x.png", _dir));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Games24")]
    [InlineData("sym:")]
    [InlineData("sym:Games 24")]
    [InlineData("sym:<script>")]
    [InlineData("res:C:\\a.dll")]
    [InlineData("res:,3")]
    [InlineData("res:C:\\a.dll,x")]
    [InlineData("emoji:🙂")]
    public void Bad_values_mean_default_icon(string? text) => Assert.Null(IconRef.Parse(text));

    [Fact]
    public void Import_copies_once_by_content()
    {
        var source = Path.Combine(_dir, "Logo.PNG");
        File.WriteAllBytes(source, [1, 2, 3, 4]);
        var icons = Path.Combine(_dir, "icons");

        var first = IconFiles.Import(source, icons);
        var copy = Path.Combine(_dir, "kopya.png");
        File.Copy(source, copy);
        var second = IconFiles.Import(copy, icons);

        Assert.Equal(first, second);
        Assert.True(IconFiles.IsStoredName(first));
        Assert.EndsWith(".png", first);
        Assert.Single(Directory.GetFiles(icons));
        // Asıl dosya silinse de simge kalır.
        File.Delete(source);
        Assert.True(File.Exists(IconFiles.PathOf(first, icons)));
        Assert.Equal(IconRefKind.Image, IconRef.Parse(IconRef.Image(first))!.Value.Kind);
    }

    [Fact]
    public void Import_rejects_other_files()
    {
        var text = Path.Combine(_dir, "a.txt");
        File.WriteAllText(text, "x");
        Assert.Throws<NotSupportedException>(() => IconFiles.Import(text, _dir));
        var empty = Path.Combine(_dir, "b.png");
        File.WriteAllBytes(empty, []);
        Assert.Throws<NotSupportedException>(() => IconFiles.Import(empty, _dir));
    }
}
