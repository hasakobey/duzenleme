using Duzenleme.Core;

namespace Duzenleme.Tests;

public class MovePlanTests
{
    private static DesktopFile F(string name, FileAttributes attributes = FileAttributes.Normal, bool undone = false) =>
        new(name, attributes, undone);

    [Fact]
    public void Pdf_goes_to_existing_pdf_folder()
    {
        var moves = MovePlan.Build([F("a.pdf")], ["PDF"], Rule.Defaults());

        var move = Assert.Single(moves);
        Assert.Equal("a.pdf", move.FileName);
        Assert.Equal("PDF", move.Folder);
        Assert.True(move.FolderExists);
    }

    [Fact]
    public void Missing_folder_is_planned_only_when_assumed()
    {
        Assert.Empty(MovePlan.Build([F("a.pdf")], [], Rule.Defaults()));

        var move = Assert.Single(MovePlan.Build([F("a.pdf")], [], Rule.Defaults(), assumeFolders: ["PDF"]));
        Assert.Equal("PDF", move.Folder);
        Assert.False(move.FolderExists);
    }

    [Fact]
    public void Folder_name_on_desktop_wins_over_rule_spelling()
    {
        var move = Assert.Single(MovePlan.Build([F("yedek.zip")], ["arsivler"], Rule.Defaults()));

        Assert.Equal("arsivler", move.Folder);
        Assert.True(move.FolderExists);
    }

    [Fact]
    public void Assumed_folder_uses_rule_name()
    {
        var move = Assert.Single(MovePlan.Build([F("yedek.zip")], [], Rule.Defaults(), assumeFolders: ["ARSIVLER"]));

        Assert.Equal("Arşivler", move.Folder);
        Assert.False(move.FolderExists);
    }

    [Fact]
    public void Ignored_disabled_and_undone_files_are_not_planned()
    {
        var rules = Rule.Defaults();
        rules.First(r => r.TargetFolder == "Resimler").Enabled = false;
        string[] folders = ["PDF", "Resimler"];
        DesktopFile[] files =
        [
            F("foto.jpg"),                                   // kural kapalı
            F("Chrome.lnk"), F("site.url"), F("desktop.ini"),
            F("gizli.pdf", FileAttributes.Hidden),
            F("sistem.pdf", FileAttributes.System),
            F("indiriliyor.pdf.crdownload"),
            F("geri.pdf", undone: true),
            F("rapor.pdf"),
        ];

        var move = Assert.Single(MovePlan.Build(files, folders, rules, assumeFolders: folders));
        Assert.Equal("rapor.pdf", move.FileName);
    }

    [Fact]
    public void By_folder_orders_by_count_then_turkish_name()
    {
        PlannedMove[] moves =
        [
            new("a.docx", "Belgeler", true),
            new("b.zip", "Arşivler", true),
            new("c.pdf", "PDF", true), new("d.pdf", "PDF", true), new("e.pdf", "pdf", false),
        ];

        var groups = MovePlan.ByFolder(moves);

        Assert.Equal([("PDF", 3), ("Arşivler", 1), ("Belgeler", 1)], groups);
    }
}
