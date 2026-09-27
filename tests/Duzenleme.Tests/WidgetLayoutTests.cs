using Duzenleme.Core;

namespace Duzenleme.Tests;

public class WidgetLayoutTests
{
    private static readonly Box Work = new(0, 0, 1920, 1040);
    private const int Gap = 10, Threshold = 16;

    [Fact]
    public void Snaps_below_a_neighbour_and_aligns_left_edges()
    {
        var neighbour = new Box(100, 100, 500, 400);
        // Komşunun biraz altında ve 7 piksel sağında bırakılıyor.
        var card = new Box(107, 415, 407, 615);

        var (dx, dy) = WidgetLayout.Snap(card, [neighbour], Work, Threshold, Gap);

        Assert.Equal(410, card.Top + dy); // komşunun altı + aralık
        Assert.Equal(100, card.Left + dx); // sol kenarlar hizalı
    }

    [Fact]
    public void Snaps_beside_a_neighbour()
    {
        var neighbour = new Box(100, 100, 500, 400);
        var card = new Box(505, 104, 805, 304);

        var (dx, dy) = WidgetLayout.Snap(card, [neighbour], Work, Threshold, Gap);

        Assert.Equal(510, card.Left + dx);
        Assert.Equal(100, card.Top + dy); // üst kenarlar hizalı
    }

    [Fact]
    public void Does_not_snap_when_far_away()
    {
        var (dx, dy) = WidgetLayout.Snap(new Box(700, 600, 900, 800), [new Box(100, 100, 500, 400)], Work, Threshold, Gap);
        Assert.Equal((0, 0), (dx, dy));
    }

    [Fact]
    public void Separate_moves_a_dropped_card_off_its_neighbour_with_the_smallest_move()
    {
        var neighbour = new Box(100, 100, 500, 400);
        var dropped = new Box(450, 150, 750, 350); // sağ tarafı komşuya 50 px giriyor

        var moved = WidgetLayout.Separate(dropped, [neighbour], Work, Gap);

        Assert.NotNull(moved);
        Assert.Equal(new Box(510, 150, 810, 350), moved);
        Assert.False(moved.Value.Overlaps(neighbour));
    }

    [Fact]
    public void Separate_skips_places_taken_by_other_cards()
    {
        var a = new Box(100, 100, 500, 400);
        var right = new Box(510, 100, 900, 400); // a'nın sağı dolu
        var dropped = new Box(380, 120, 680, 300);

        var moved = WidgetLayout.Separate(dropped, [a, right], Work, Gap);

        Assert.NotNull(moved);
        Assert.False(moved.Value.Overlaps(a));
        Assert.False(moved.Value.Overlaps(right));
        Assert.True(moved.Value.Inside(Work));
    }

    [Fact]
    public void Separate_pulls_a_card_that_sticks_out_of_the_screen_back_in()
    {
        var neighbour = new Box(0, 0, 400, 300);
        var dropped = new Box(-60, 120, 240, 270); // sol kenardan taşıyor ve komşuya biniyor

        var moved = WidgetLayout.Separate(dropped, [neighbour], Work, Gap);

        Assert.NotNull(moved);
        Assert.True(moved.Value.Inside(Work));
        Assert.False(moved.Value.Overlaps(neighbour));
    }

    [Fact]
    public void Separate_returns_null_when_nothing_overlaps()
    {
        Assert.Null(WidgetLayout.Separate(new Box(0, 0, 100, 100), [new Box(200, 200, 300, 300)], Work, Gap));
    }

    [Fact]
    public void Resizing_stops_at_the_neighbour()
    {
        var start = new Box(100, 100, 400, 300);
        var neighbour = new Box(600, 80, 900, 320);
        var proposed = new Box(100, 100, 750, 300); // sağ kenar komşunun içine çekildi

        var result = WidgetLayout.ClampResize(start, proposed, left: false, top: false, right: true, bottom: false, [neighbour], Gap);

        Assert.Equal(590, result.Right);
        Assert.False(result.Overlaps(neighbour));
    }
}
