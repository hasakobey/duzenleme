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
}
