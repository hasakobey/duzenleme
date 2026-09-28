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

    // Yeni widget'ın yeri (FindSpot, DesiredSpot)

    private const int Step = 16;

    private static Box At((int X, int Y) spot, int w, int h) => new(spot.X, spot.Y, spot.X + w, spot.Y + h);

    [Fact]
    public void FindSpot_returns_the_desired_spot_when_it_is_free()
    {
        var spot = WidgetLayout.FindSpot(Work, 300, 200, 500, 400, [new Box(1200, 100, 1500, 400)], Gap, Step);
        Assert.Equal((500, 400), spot);
    }

    [Fact]
    public void FindSpot_moves_to_the_nearest_free_spot_and_keeps_the_gap()
    {
        var taken = new Box(400, 300, 900, 700);
        var spot = WidgetLayout.FindSpot(Work, 300, 200, 500, 400, [taken], Gap, Step);
        var card = At(spot, 300, 200);

        Assert.False(card.Overlaps(taken.Inflate(Gap)));
        Assert.True(card.Inside(Work));
        // Dört yöndeki en yakın boş yerden (yukarı ya da aşağı 310 px) bir ızgara adımından fazla uzağa gitmez.
        long dx = spot.X - 500, dy = spot.Y - 400;
        Assert.True(dx * dx + dy * dy <= (310L + Step) * (310L + Step), $"{spot} çok uzak");
        Assert.Equal(500, spot.X);
    }

    [Fact]
    public void FindSpot_clamps_into_the_work_area()
    {
        var spot = WidgetLayout.FindSpot(Work, 300, 200, 1900, 1030, [], Gap, Step);
        Assert.Equal((1620, 840), spot);
        Assert.True(At(spot, 300, 200).Inside(Work));

        var negative = WidgetLayout.FindSpot(new Box(-1920, 0, 0, 1040), 300, 200, -5000, -50, [], Gap, Step);
        Assert.Equal((-1920, 0), negative);
    }

    [Fact]
    public void FindSpot_picks_the_least_overlap_when_the_screen_is_full()
    {
        var small = new Box(0, 0, 400, 300);
        // Ekranın tamamı dolu, yalnızca sol üstte küçük bir widget daha "hafif" dolu değil: hepsi çakışır.
        var everything = new Box(0, 0, 1920, 1040);
        var spot = WidgetLayout.FindSpot(small, 300, 200, 50, 50, [everything], Gap, Step);

        Assert.True(At(spot, 300, 200).Inside(small));

        // İki widget arasında daha az çakışan taraf seçilir.
        var area = new Box(0, 0, 1000, 400);
        Box[] taken = [new Box(0, 0, 600, 400), new Box(600, 0, 1000, 150)];
        var best = WidgetLayout.FindSpot(area, 300, 300, 0, 0, taken, Gap, Step);
        var overlap = taken.Sum(t => t.OverlapArea(At(best, 300, 300)));
        Assert.True(overlap <= 300L * 150, $"{best}: {overlap}");
    }

    [Fact]
    public void Corner_weighting_fills_the_column_downwards_first()
    {
        var area = new Box(0, 0, 1920, 1040);
        var first = new Box(1600, 20, 1900, 220); // sağ üstte bir not var
        var (x, y) = WidgetLayout.DesiredSpot(PlaceMode.Corner, area, 300, 200, 0, 0, cornerRight: true, offset: 16, Gap);
        Assert.Equal((1610, 10), (x, y));

        var straight = WidgetLayout.FindSpot(area, 300, 200, x, y, [first], Gap, Step);
        var column = WidgetLayout.FindSpot(area, 300, 200, x, y, [first], Gap, Step, horizontalWeight: 4);

        // Köşe kipinde aynı sütunda (notun altında) kalır.
        Assert.Equal(1610, column.X);
        Assert.True(column.Y >= first.Bottom + Gap);
        Assert.False(At(straight, 300, 200).Overlaps(first.Inflate(Gap)));
    }

    [Fact]
    public void Desired_spot_next_to_the_cursor_flips_at_screen_edges()
    {
        var area = new Box(0, 0, 1920, 1040);
        Assert.Equal((516, 316), WidgetLayout.DesiredSpot(PlaceMode.Cursor, area, 300, 200, 500, 300, true, 16, Gap));
        // Sağ alt köşede: imlecin soluna ve üstüne.
        Assert.Equal((1900 - 16 - 300, 1000 - 16 - 200), WidgetLayout.DesiredSpot(PlaceMode.Cursor, area, 300, 200, 1900, 1000, true, 16, Gap));
        // Çok dar alan: yine alanın içinde.
        var narrow = new Box(0, 0, 250, 150);
        var spot = WidgetLayout.DesiredSpot(PlaceMode.Cursor, narrow, 300, 200, 100, 100, true, 16, Gap);
        Assert.Equal((0, 0), spot);
    }

    [Fact]
    public void Desired_spot_in_the_center_and_in_corners()
    {
        var area = new Box(-1920, 0, 0, 1040);
        Assert.Equal((-1920 + 810, 420), WidgetLayout.DesiredSpot(PlaceMode.Center, area, 300, 200, 0, 0, true, 16, Gap));
        Assert.Equal((-10 - 300, 10), WidgetLayout.DesiredSpot(PlaceMode.Corner, area, 300, 200, 0, 0, cornerRight: true, 16, Gap));
        Assert.Equal((-1920 + 810, 10), WidgetLayout.DesiredSpot(PlaceMode.Corner, area, 300, 200, 0, 0, cornerRight: false, 16, Gap));
    }

    [Theory]
    [InlineData(null, PlaceMode.Cursor)]
    [InlineData("", PlaceMode.Cursor)]
    [InlineData("cursor", PlaceMode.Cursor)]
    [InlineData("Center", PlaceMode.Center)]
    [InlineData(" corner ", PlaceMode.Corner)]
    [InlineData("gelecekte-yeni", PlaceMode.Cursor)]
    public void Placement_setting_parses_with_a_safe_default(string? value, PlaceMode expected)
    {
        Assert.Equal(expected, PlaceModes.Parse(value));
        Assert.Equal(expected, PlaceModes.Parse(PlaceModes.ToSetting(expected)));
    }
}
