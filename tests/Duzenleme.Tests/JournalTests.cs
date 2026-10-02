using Duzenleme.Core;

namespace Duzenleme.Tests;

public class JournalTests
{
    [Fact]
    public void Undone_moves_survive_when_history_is_trimmed()
    {
        using var t = new TempDesktop();
        var undone = new MoveEntry { Source = Path.Combine(t.Desktop, "kalsin.pdf"), Destination = Path.Combine(t.Desktop, "PDF", "kalsin.pdf") };
        t.Journal.Add(undone);
        t.Journal.MarkUndone(undone);

        // Sınırın çok üstüne çıkacak kadar yeni taşıma.
        for (var i = 0; i < MoveJournal.MaxEntries + 50; i++)
            t.Journal.Add(new MoveEntry { Source = $@"C:\d\{i}.pdf", Destination = $@"C:\d\PDF\{i}.pdf" });

        Assert.True(t.Journal.WasUndone(undone.Source));
        Assert.Equal(MoveJournal.MaxEntries, t.Journal.Snapshot().Count);
        var reloaded = new MoveJournal(Path.Combine(t.Root, "journal.json"));
        Assert.True(reloaded.WasUndone(undone.Source));
    }

    // Kolay NestDesk: "Geçmişi temizle" soru sormaz; bildirimdeki "Geri al" silinen kayıtları geri koyar.

    [Fact]
    public void Clearing_history_keeps_undone_moves_and_returns_the_cleared_ones_for_undo()
    {
        using var t = new TempDesktop();
        var start = new DateTime(2026, 10, 1, 9, 0, 0);
        var a = new MoveEntry { Source = @"C:\d\a.pdf", Destination = @"C:\d\PDF\a.pdf", Time = start };
        var undone = new MoveEntry { Source = @"C:\d\b.pdf", Destination = @"C:\d\PDF\b.pdf", Time = start.AddMinutes(1) };
        var c = new MoveEntry { Source = @"C:\d\c.pdf", Destination = @"C:\d\PDF\c.pdf", Time = start.AddMinutes(2) };
        t.Journal.Add(a);
        t.Journal.Add(undone);
        t.Journal.Add(c);
        t.Journal.MarkUndone(undone);

        var cleared = t.Journal.Clear();
        Assert.Equal([a.Id, c.Id], cleared.Select(e => e.Id));
        Assert.Equal([undone.Id], t.Journal.Snapshot().Select(e => e.Id));
        Assert.True(t.Journal.WasUndone(undone.Source)); // "bir daha taşıma" kararı kalır
        Assert.Null(t.Journal.LastActive());

        // Bu arada yeni bir taşıma oldu; geri alma onu silmez, kayıtlar zamana göre dizilir.
        var later = new MoveEntry { Source = @"C:\d\d.pdf", Destination = @"C:\d\PDF\d.pdf", Time = start.AddMinutes(5) };
        t.Journal.Add(later);
        Assert.True(t.Journal.Restore(cleared));
        Assert.Equal([later.Id, c.Id, undone.Id, a.Id], t.Journal.Snapshot().Select(e => e.Id));
        Assert.Equal(later.Id, t.Journal.LastActive()?.Id);
        Assert.False(t.Journal.Restore(cleared)); // ikinci kez eklenmez

        Assert.True(t.Journal.Flush());
        var reloaded = new MoveJournal(Path.Combine(t.Root, "journal.json"));
        Assert.Equal(4, reloaded.Snapshot().Count);
    }
}
