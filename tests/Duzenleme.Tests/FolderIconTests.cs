using Duzenleme.Core;

namespace Duzenleme.Tests;

public class FolderIconTests
{
    [Theory]
    [InlineData("PDF", "pdf")]
    [InlineData("Oyunlarım", "game")]
    [InlineData("OKUL ÖDEVLERİ", "school")]
    [InlineData("Faturalar 2026", "money")]
    [InlineData("Resimler", "image")]
    [InlineData("Müzik", "music")]
    [InlineData("github projeleri", "code")]
    [InlineData("İş", "work")]
    public void Suggests_glyph_from_folder_name(string name, string expected) =>
        Assert.Equal(expected, FolderIconCatalog.Suggest(name).Glyph.Key);

    [Fact]
    public void Short_keywords_need_whole_word()
    {
        // "ev" "Everest" içinde geçse de ev simgesi seçilmemeli.
        Assert.NotEqual("home", FolderIconCatalog.Suggest("Everest").Glyph.Key);
        Assert.Equal("home", FolderIconCatalog.Suggest("Ev").Glyph.Key);
    }

    [Fact]
    public void Unknown_name_gets_stable_folder_icon()
    {
        var a = FolderIconCatalog.Suggest("Xyzzy");
        Assert.Equal("folder", a.Glyph.Key);
        Assert.Equal(a.Color, FolderIconCatalog.Suggest("Xyzzy").Color);
    }

    [Fact]
    public void Catalog_keys_are_unique()
    {
        Assert.Equal(FolderIconCatalog.Glyphs.Length, FolderIconCatalog.Glyphs.Select(g => g.Key).Distinct().Count());
        Assert.Equal(FolderIconCatalog.Colors.Length, FolderIconCatalog.Colors.Select(c => c.Key).Distinct().Count());
    }
}
