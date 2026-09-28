using Duzenleme.Core;

namespace Duzenleme.Tests;

public class JournalBatchingTests
{
    [Fact]
    public void Many_moves_are_written_in_one_batch()
    {
        using var t = new TempDesktop();
        for (var i = 0; i < 200; i++)
            t.Journal.Add(new MoveEntry { Source = $@"C:\d\{i}.pdf", Destination = $@"C:\d\PDF\{i}.pdf" });

        Assert.True(t.Journal.Flush());
        // Eskiden her taşıma bütün geçmişi yeniden yazıyordu (200 yazma).
        Assert.InRange(t.Journal.WriteCount, 1, 3);
        Assert.Equal(200, new MoveJournal(Path.Combine(t.Root, "journal.json")).Snapshot().Count);
    }

    [Fact]
    public void Batched_moves_reach_the_disk_shortly_without_an_explicit_flush()
    {
        using var t = new TempDesktop();
        t.Journal.Add(new MoveEntry { Source = @"C:\d\a.pdf", Destination = @"C:\d\PDF\a.pdf" });

        var path = Path.Combine(t.Root, "journal.json");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!File.Exists(path) && DateTime.UtcNow < deadline) Thread.Sleep(50);
        Assert.Single(new MoveJournal(path).Snapshot());
    }

    [Fact]
    public void Undo_decision_is_written_immediately()
    {
        using var t = new TempDesktop();
        var entry = new MoveEntry { Source = @"C:\d\kalsin.pdf", Destination = @"C:\d\PDF\kalsin.pdf" };
        t.Journal.Add(entry);
        t.Journal.MarkUndone(entry);

        // Flush yok: çökme anında da "bir daha taşıma" kararı diskte olmalı.
        Assert.True(new MoveJournal(Path.Combine(t.Root, "journal.json")).WasUndone(entry.Source));
    }

    [Fact]
    public void Manually_returned_file_is_written_immediately()
    {
        using var t = new TempDesktop();
        t.Journal.Add(new MoveEntry { Source = @"C:\d\geri.pdf", Destination = @"C:\d\PDF\geri.pdf", Undone = true });

        Assert.True(new MoveJournal(Path.Combine(t.Root, "journal.json")).WasUndone(@"C:\d\geri.pdf"));
    }

    [Fact]
    public void Undone_sources_are_looked_up_case_insensitively()
    {
        using var t = new TempDesktop();
        var entry = new MoveEntry { Source = @"C:\d\Rapor.PDF", Destination = @"C:\d\PDF\Rapor.PDF" };
        t.Journal.Add(entry);
        t.Journal.MarkUndone(entry);

        Assert.Contains(@"c:\D\rapor.pdf", t.Journal.UndoneSources());
    }
}
