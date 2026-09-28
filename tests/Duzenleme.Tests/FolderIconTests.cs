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

public class FolderIconLogTests
{
    [Fact]
    public void Given_icons_are_remembered_once_and_forgotten_when_removed()
    {
        var list = FolderIconLog.Note(null, @"C:\M\İş\Logolar\", given: true);
        list = FolderIconLog.Note(list, @"D:\Proje", given: true);
        // Yeniden verildi: tek kayıt, sona geçer. (NTFS "i"yi "I" yapar, "İ" değil: İ ve i farklı harftir.)
        list = FolderIconLog.Note(list, @"c:\m\İŞ\LOGOLAR", given: true);
        Assert.Equal([@"D:\Proje", @"c:\m\İŞ\LOGOLAR"], list);

        var same = FolderIconLog.Note(list, @"E:\yok", given: false);
        Assert.Same(list, same);                                              // değişiklik yok: aynı liste
        list = FolderIconLog.Note(list, @"D:\Proje\", given: false);
        Assert.Equal([@"c:\m\İŞ\LOGOLAR"], list);
        Assert.Null(FolderIconLog.Note(list, @"C:\M\İş\Logolar", given: false));
    }

    [Fact]
    public void Log_keeps_the_newest_folders()
    {
        List<string>? list = null;
        for (var i = 0; i < FolderIconLog.Max + 5; i++) list = FolderIconLog.Note(list, $@"C:\k{i}", given: true);
        Assert.Equal(FolderIconLog.Max, list!.Count);
        Assert.Equal(@"C:\k5", list[0]);
        Assert.Equal($@"C:\k{FolderIconLog.Max + 4}", list[^1]);
    }

    [Fact]
    public void Renamed_folder_is_followed()
    {
        List<string> list = [@"C:\M\Eski\Alt", @"C:\M\Başka"];
        var renamed = FolderIconLog.Rename(list, @"C:\M\Eski", @"C:\M\Yeni", isDirectory: true);
        Assert.Equal([@"C:\M\Yeni\Alt", @"C:\M\Başka"], renamed);
        Assert.Equal([@"C:\M\Eski\Alt", @"C:\M\Başka"], list);                // eski liste yerinde değişmez
        Assert.Same(list, FolderIconLog.Rename(list, @"C:\M\x.txt", @"C:\M\y.txt", isDirectory: false));
    }

    [Fact]
    public void Remove_all_keeps_only_unreachable_and_newly_added_folders()
    {
        List<string> list = [@"C:\a", @"C:\b", @"C:\c", @"C:\yeni"];
        var after = FolderIconLog.AfterRemoveAll(list, tried: [@"C:\a", @"C:\b", @"C:\c"], failed: [@"C:\b"]);
        Assert.Equal([@"C:\b", @"C:\yeni"], after);
        Assert.Null(FolderIconLog.AfterRemoveAll([@"C:\a"], [@"C:\a"], []));
    }
}
