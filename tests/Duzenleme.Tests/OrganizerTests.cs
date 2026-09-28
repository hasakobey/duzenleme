using Duzenleme.Core;

namespace Duzenleme.Tests;

public class OrganizerTests
{
    [Fact]
    public void Moves_pdf_into_user_created_folder()
    {
        using var t = new TempDesktop();
        var pdfDir = t.Folder("PDF");
        var file = t.File("satis.pdf");

        var entry = t.Organizer.Organize(file);

        Assert.NotNull(entry);
        Assert.False(File.Exists(file));
        Assert.True(File.Exists(Path.Combine(pdfDir, "satis.pdf")));
    }

    [Fact]
    public void Leaves_file_when_folder_does_not_exist()
    {
        using var t = new TempDesktop();
        var file = t.File("satis.pdf");

        Assert.Null(t.Organizer.Organize(file));
        Assert.True(File.Exists(file));
        Assert.False(Directory.Exists(Path.Combine(t.Desktop, "PDF")));
    }

    [Fact]
    public void Name_collision_gets_numbered_suffix()
    {
        using var t = new TempDesktop();
        var pdfDir = t.Folder("PDF");
        File.WriteAllText(Path.Combine(pdfDir, "a.pdf"), "old");
        File.WriteAllText(Path.Combine(pdfDir, "a (1).pdf"), "old");

        var entry = t.Organizer.Organize(t.File("a.pdf", "new"))!;

        Assert.Equal(Path.Combine(pdfDir, "a (2).pdf"), entry.Destination);
        Assert.Equal("old", File.ReadAllText(Path.Combine(pdfDir, "a.pdf")));
    }

    [Fact]
    public void Undo_restores_file_and_it_is_not_moved_again()
    {
        using var t = new TempDesktop();
        t.Folder("PDF");
        var file = t.File("rapor.pdf");
        var entry = t.Organizer.Organize(file)!;

        t.Organizer.Undo(entry);

        Assert.True(File.Exists(file));
        Assert.True(entry.Undone);
        Assert.Null(t.Organizer.Organize(file));
        Assert.Empty(t.Organizer.OrganizeAll());
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Undo_does_not_overwrite_new_file_with_same_name()
    {
        using var t = new TempDesktop();
        t.Folder("PDF");
        var entry = t.Organizer.Organize(t.File("rapor.pdf", "first"))!;
        t.File("rapor.pdf", "second");

        t.Organizer.Undo(entry);

        Assert.Equal("second", File.ReadAllText(Path.Combine(t.Desktop, "rapor.pdf")));
        Assert.Equal("first", File.ReadAllText(Path.Combine(t.Desktop, "rapor (1).pdf")));
    }

    [Fact]
    public void OrganizeAll_moves_only_matching_files_and_keeps_shortcuts()
    {
        using var t = new TempDesktop();
        t.Folder("pdf");
        t.Folder("Arşivler");
        t.File("a.pdf");
        t.File("b.zip");
        t.File("Discord.lnk");
        t.File("not.txt");

        var moved = t.Organizer.OrganizeAll();

        Assert.Equal(2, moved.Count);
        Assert.True(File.Exists(Path.Combine(t.Desktop, "Discord.lnk")));
        Assert.True(File.Exists(Path.Combine(t.Desktop, "not.txt")));
        Assert.True(File.Exists(Path.Combine(t.Desktop, "Arşivler", "b.zip")));
    }

    [Fact]
    public void Manual_move_is_journaled_and_undoable()
    {
        using var t = new TempDesktop();
        var pdfDir = t.Folder("PDF");
        var file = t.File("elle.txt");

        var entry = t.Organizer.MoveManually(file, pdfDir);
        Assert.True(File.Exists(Path.Combine(pdfDir, "elle.txt")));

        t.Organizer.Undo(entry);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void File_taken_back_out_of_folder_is_not_moved_again()
    {
        using var t = new TempDesktop();
        var pdfDir = t.Folder("PDF");
        File.WriteAllText(Path.Combine(pdfDir, "geri.pdf"), "x");
        var onDesktop = Path.Combine(t.Desktop, "geri.pdf");
        File.Move(Path.Combine(pdfDir, "geri.pdf"), onDesktop);
        t.Journal.Add(new MoveEntry { Source = onDesktop, Destination = Path.Combine(pdfDir, "geri.pdf"), Undone = true });

        Assert.Null(t.Organizer.Organize(onDesktop));
        Assert.True(File.Exists(onDesktop));
    }

    [Fact]
    public void Journal_persists_across_instances()
    {
        using var t = new TempDesktop();
        t.Folder("PDF");
        t.Organizer.Organize(t.File("a.pdf"));
        // Yeni kayıtlar toplanıp kısa süre sonra yazılır; çıkışta olduğu gibi hemen diske indirilir.
        Assert.True(t.Journal.Flush());

        var reloaded = new MoveJournal(Path.Combine(t.Root, "journal.json"));

        Assert.Single(reloaded.Snapshot());
        Assert.Equal("a.pdf", reloaded.Snapshot()[0].FileName);
    }

    [Fact]
    public void Locked_file_is_not_ready()
    {
        using var t = new TempDesktop();
        var file = t.File("indiriliyor.pdf");
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.False(FileMover.IsReady(file));
        Assert.True(FileMover.IsReady(file));
    }

    [Fact]
    public async Task Watcher_moves_new_file_after_it_settles()
    {
        using var t = new TempDesktop();
        var pdfDir = t.Folder("PDF");
        using var watcher = new DesktopWatcher(t.Organizer, () => false);
        watcher.Start();

        t.File("yeni.pdf");

        var target = Path.Combine(pdfDir, "yeni.pdf");
        for (var i = 0; i < 100 && !File.Exists(target); i++) await Task.Delay(100);
        Assert.True(File.Exists(target));
    }

    [Fact]
    public async Task Watcher_picks_up_download_renamed_to_pdf()
    {
        using var t = new TempDesktop();
        var pdfDir = t.Folder("PDF");
        using var watcher = new DesktopWatcher(t.Organizer, () => false);
        watcher.Start();

        var partial = t.File("fatura.pdf.crdownload");
        await Task.Delay(300);
        File.Move(partial, Path.Combine(t.Desktop, "fatura.pdf"));

        var target = Path.Combine(pdfDir, "fatura.pdf");
        for (var i = 0; i < 100 && !File.Exists(target); i++) await Task.Delay(100);
        Assert.True(File.Exists(target));
    }
}
