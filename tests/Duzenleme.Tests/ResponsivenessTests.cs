using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>Taşıyıcının dosyalara gereksiz dokunmaması ve yardımcı parçalar (2.1 "Akıcılık").</summary>
public class ResponsivenessTests
{
    [Fact]
    public void WouldMove_checks_rules_without_opening_the_file()
    {
        using var t = new TempDesktop();
        t.Folder("PDF");
        var pdf = t.File("rapor.pdf");
        var link = t.File("oyun.lnk");
        var noFolder = t.File("resim.png");

        // Dosya başka süreçte özel kilitliyken de karar verilebilir (açılmadan).
        using (new FileStream(pdf, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(t.Organizer.WouldMove(pdf));
            Assert.False(t.Organizer.WouldMove(link));
            Assert.False(t.Organizer.WouldMove(noFolder));
            Assert.False(t.Organizer.WouldMove(Path.Combine(t.Desktop, "yok.pdf")));
        }
    }

    [Fact]
    public void WouldMove_respects_undo()
    {
        using var t = new TempDesktop();
        t.Folder("PDF");
        var entry = t.Organizer.Organize(t.File("rapor.pdf"))!;
        t.Organizer.Undo(entry);

        Assert.False(t.Organizer.WouldMove(entry.Source));
    }

    [Fact]
    public void OrganizeAll_skips_locked_matching_files_and_ignores_unmatched_ones()
    {
        using var t = new TempDesktop();
        t.Folder("PDF");
        var busy = t.File("indiriliyor.pdf");
        var ready = t.File("hazir.pdf");
        t.File("not.txt");

        using (new FileStream(busy, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var moved = t.Organizer.OrganizeAll();
            Assert.Equal(Path.Combine(t.Desktop, "PDF", "hazir.pdf"), Assert.Single(moved).Destination);
        }
        Assert.True(File.Exists(busy));
        Assert.False(File.Exists(ready));
        Assert.True(File.Exists(Path.Combine(t.Desktop, "not.txt")));
    }

    [Fact]
    public void OrganizeAll_creates_a_missing_folder_once_for_many_files()
    {
        using var t = new TempDesktop();
        t.Settings.CreateMissingFolders = true;
        for (var i = 0; i < 5; i++) t.File($"belge {i}.pdf");

        var moved = t.Organizer.OrganizeAll();

        Assert.Equal(5, moved.Count);
        Assert.Single(Directory.GetDirectories(t.Desktop));
    }

    [Fact]
    public void MoveManually_uses_the_given_mover()
    {
        using var t = new TempDesktop();
        var target = t.Folder("PDF");
        var file = t.File("a.pdf");
        string? from = null, to = null;

        var entry = t.Organizer.MoveManually(file, target, (s, d) => { from = s; to = d; File.Move(s, d); });

        Assert.Equal(file, from);
        Assert.Equal(Path.Combine(target, "a.pdf"), to);
        Assert.Equal(to, entry.Destination);
    }

    [Theory]
    [InlineData(@"C:\a\b.txt", @"C:\c", true)]
    [InlineData(@"C:\a\b.txt", @"D:\c", false)]
    [InlineData(@"\\nas\p\a.txt", @"C:\c", false)]
    [InlineData(@"\\nas\p\a.txt", @"\\NAS\P\b", true)]
    public void Same_volume_is_decided_from_text(string a, string b, bool same) =>
        Assert.Equal(same, FileMover.SameVolume(a, b));

    [Theory]
    [InlineData("indirme.crdownload", true)]
    [InlineData("film.mkv.part", true)]
    [InlineData("kurulum.exe.download", true)]
    [InlineData("rapor.pdf", false)]
    [InlineData("partial.txt", false)]
    public void Partial_downloads_are_recognised(string name, bool partial) =>
        Assert.Equal(partial, DesktopItems.IsPartialDownload(name));

    [Fact]
    public void Snapshot_entries_are_classified_like_files_on_disk()
    {
        var folder = new DirEntry(@"C:\d\PDF", "PDF", true, FileAttributes.Directory, DateTime.UnixEpoch, 0);
        var link = new DirEntry(@"C:\d\Oyun.lnk", "Oyun.lnk", false, 0, DateTime.UnixEpoch, 1);
        var file = new DirEntry(@"C:\d\a.pdf", "a.pdf", false, 0, DateTime.UnixEpoch, 1);
        var ini = new DirEntry(@"C:\d\desktop.ini", "desktop.ini", false, 0, DateTime.UnixEpoch, 1);
        var hidden = new DirEntry(@"C:\d\x.txt", "x.txt", false, FileAttributes.Hidden, DateTime.UnixEpoch, 1);

        Assert.True(DesktopItems.Matches(DesktopFilter.Folders, folder));
        Assert.True(DesktopItems.Matches(DesktopFilter.Shortcuts, link));
        Assert.True(DesktopItems.Matches(DesktopFilter.Files, file));
        Assert.False(DesktopItems.Matches(DesktopFilter.Files, link));
        Assert.False(DesktopItems.Matches(DesktopFilter.All, ini));
        Assert.False(DesktopItems.Matches(DesktopFilter.All, hidden));
    }

    [Fact]
    public async Task Background_io_runs_after_the_delay_and_reports_errors()
    {
        var ran = false;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await BackgroundIo.Run("test", () => ran = true, TimeSpan.FromMilliseconds(100));
        Assert.True(ran);
        Assert.True(clock.ElapsedMilliseconds >= 90);

        Exception? error = null;
        await BackgroundIo.Run("test", () => throw new IOException("disk"), onError: ex => error = ex);
        Assert.IsType<IOException>(error);
    }
}
