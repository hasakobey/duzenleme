using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Kolay NestDesk: soru yerine bildirimdeki "Geri al" ile geri alınan işlerin saf bölümleri ("Widget'ları hizala",
/// "Notu temizle"). Geçmiş (JournalTests) ve kutu sekmesi (LauncherEditsTests) kendi dosyalarında.
/// </summary>
public class KolayUndoTests
{
    [Fact]
    public void Undoing_arrange_moves_back_only_widgets_still_where_arrange_put_them()
    {
        var moves = new[]
        {
            new ArrangeMove("a", 10, 10, 500, 0),    // hâlâ hizalandığı yerde: geri döner
            new ArrangeMove("b", 20, 20, 500, 300),  // sonradan kullanıcı taşıdı: yerinde kalır
            new ArrangeMove("c", 30, 30, 500, 600),  // bu arada kaldırıldı: dokunulmaz
            new ArrangeMove("d", 40, 40, 40, 40),    // hizalama yerinden oynatmadı
            new ArrangeMove("e", 50, 0, 50, 0, Clipped: true),  // yerinde kaldı ama boyu kısıldı: boyu geri gelir
            new ArrangeMove("f", 60, 0, 60, 0, Clipped: true),  // kısıldı, sonra kullanıcı taşıdı: dokunulmaz
        };
        var now = new Dictionary<string, (int X, int Y)>
        {
            ["a"] = (500, 0), ["b"] = (700, 300), ["d"] = (40, 40), ["e"] = (50, 0), ["f"] = (900, 0),
        };
        var back = ArrangeUndo.Pending(moves, id => now.TryGetValue(id, out var p) ? p : null);
        Assert.Equal(["a", "e"], back.Select(m => m.Id));
        Assert.Equal((10, 10), (back[0].FromX, back[0].FromY));
        Assert.True(back[0].Moved);
        Assert.False(back[1].Moved);
        Assert.Empty(ArrangeUndo.Pending([], _ => (0, 0)));
    }

    [Theory]
    [InlineData("alışveriş\r\nsüt", "", "alışveriş\r\nsüt")]
    [InlineData("alışveriş\r\nsüt", null, "alışveriş\r\nsüt")]
    [InlineData("alışveriş\r\nsüt\r\n", "yumurta", "alışveriş\r\nsüt\r\nyumurta")]
    [InlineData("eski", "  ", "eski")]
    public void Undoing_clear_note_brings_the_text_back_without_losing_new_typing(string cleared, string? current, string expected)
    {
        Assert.Equal(expected, NoteClear.Restore(cleared, current));
    }
}
