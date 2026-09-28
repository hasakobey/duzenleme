using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>Bölmede yerinde yeniden adlandırma (F2): Gezgin'e benzer ad kuralları.</summary>
public class FileNamesTests
{
    [Theory]
    [InlineData(@"C:\M\Google Chrome.lnk", "Google Chrome", ".lnk", 13)]
    [InlineData(@"C:\M\Site.URL", "Site", ".URL", 4)]
    [InlineData(@"C:\M\setup.exe", "setup", ".exe", 5)]
    [InlineData(@"C:\M\Uygulama.appref-ms", "Uygulama", ".appref-ms", 8)]
    [InlineData(@"C:\M\rapor.final.pdf", "rapor.final.pdf", "", 11)]
    [InlineData(@"C:\M\notlar", "notlar", "", 6)]
    [InlineData(@"C:\M\.gitignore", ".gitignore", "", 10)]
    public void Split_hides_shortcut_extensions_and_selects_the_base_name(string path, string editable, string hidden, int select)
    {
        var split = FileNames.SplitForEditing(path, isDirectory: false);
        Assert.Equal(editable, split.Editable);
        Assert.Equal(hidden, split.HiddenExtension);
        Assert.Equal(select, split.SelectLength);
    }

    [Fact]
    public void Folders_are_edited_whole_even_with_dots()
    {
        var split = FileNames.SplitForEditing(@"C:\M\v1.2\", isDirectory: true);
        Assert.Equal("v1.2", split.Editable);
        Assert.Equal("", split.HiddenExtension);
        Assert.Equal(4, split.SelectLength);
    }

    [Fact]
    public void Compose_keeps_the_hidden_extension_so_shortcuts_do_not_break()
    {
        // Eski hata: kutuda "Google Chrome.lnk" görünüyordu; "Chrome" yazan kullanıcı uzantısız, bozuk bir dosya elde ediyordu.
        var split = FileNames.SplitForEditing(@"C:\M\Google Chrome.lnk", isDirectory: false);
        Assert.Equal("Chrome.lnk", split.Compose("Chrome"));
        Assert.Equal("Chrome.lnk", split.Compose("  Chrome.  "));
    }

    [Theory]
    [InlineData("a. ", "a")]
    [InlineData("  rapor.pdf  ", "rapor.pdf")]
    [InlineData("klasör...", "klasör")]
    [InlineData(". .", "")]
    public void Normalize_drops_what_windows_would_drop_silently(string input, string expected) =>
        Assert.Equal(expected, FileNames.Normalize(input));

    [Theory]
    [InlineData("rapor.pdf")]
    [InlineData("İş planı (2026)")]
    [InlineData("CONSOLE.txt")]
    [InlineData("com10")]
    [InlineData("nul-listesi")]
    public void Valid_names_pass(string name) => Assert.Null(FileNames.Validate(name));

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a:b")]
    [InlineData("soru?")]
    [InlineData("yol/ayrac")]
    [InlineData("tab\tkarakteri")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("con .txt")]
    [InlineData("NUL.tar.gz")]
    [InlineData("COM1")]
    [InlineData("lpt9.doc")]
    [InlineData("COM\u00B9")]
    [InlineData("LPT\u00B3.txt")]
    [InlineData("CONIN$")]
    public void Invalid_and_reserved_names_are_rejected(string name) => Assert.NotNull(FileNames.Validate(name));

    [Fact]
    public void Paths_windows_cannot_open_are_rejected()
    {
        var dir = @"C:\" + new string('d', 200);
        Assert.Null(FileNames.Validate(new string('a', 40), dir));
        Assert.NotNull(FileNames.Validate(new string('a', 60), dir));
        Assert.NotNull(FileNames.Validate(new string('a', 256)));
    }

    [Fact]
    public void Validation_messages_are_localized()
    {
        using var _ = L.Use(Lang.En);
        Assert.Equal("A name can't contain any of these characters: \\ / : * ? \" < > |", FileNames.Validate("a*b"));
        Assert.Contains("reserved", FileNames.Validate("CON.txt"));
    }

    [Theory]
    [InlineData("a.txt", "a.TXT", false, false)]
    [InlineData("a.txt", "b.txt", false, false)]
    [InlineData("a.txt", "a.pdf", false, true)]
    [InlineData("a.txt", "a", false, true)]
    [InlineData("v1.2", "v1.3", true, false)]
    public void Extension_change_is_detected_for_files_only(string oldName, string newName, bool isDirectory, bool expected) =>
        Assert.Equal(expected, FileNames.ExtensionChanged(oldName, newName, isDirectory));

    [Fact]
    public void Unique_name_counts_from_two_like_windows()
    {
        var taken = new HashSet<string>(["Rapor.pdf", "Rapor (2).pdf", "Yeni klasör", "v1.2", ".env"], StringComparer.OrdinalIgnoreCase);
        Assert.Equal("rapor (3).pdf", FileNames.UniqueName("rapor.pdf", false, taken.Contains));
        Assert.Equal("Yeni klasör (2)", FileNames.UniqueName("Yeni klasör", true, taken.Contains));
        // Klasör adındaki nokta uzantı değildir.
        Assert.Equal("v1.2 (2)", FileNames.UniqueName("v1.2", true, taken.Contains));
        Assert.Equal(".env (2)", FileNames.UniqueName(".env", false, taken.Contains));
        Assert.Equal("Boş.txt", FileNames.UniqueName("Boş.txt", false, taken.Contains));
    }

    [Fact]
    public void Case_only_change_is_not_a_collision()
    {
        Assert.True(FileNames.IsCaseOnlyChange("rapor.pdf", "Rapor.pdf"));
        Assert.False(FileNames.IsCaseOnlyChange("rapor.pdf", "rapor.pdf"));
        Assert.False(FileNames.IsCaseOnlyChange("rapor.pdf", "rapor2.pdf"));
    }

    [Fact]
    public void Invalid_characters_are_stripped_from_pasted_text()
    {
        Assert.Equal("ab c", FileNames.StripInvalid("a*b c?"));
        Assert.True(FileNames.IsInvalidChar('|'));
        Assert.True(FileNames.IsInvalidChar('\n'));
        Assert.False(FileNames.IsInvalidChar('ş'));
    }
}
