using Duzenleme.Core;
using static Duzenleme.Core.RingGeometry;

namespace Duzenleme.Tests;

/// <summary>Halka geometrisi (Kando'dan taşınan açılar, dilimler, isabet testi, ok tuşlarıyla gezinme, bırakma sırası).</summary>
public class RingGeometryTests
{
    private const double Eps = 1e-9;

    [Fact]
    public void Angle_zero_is_up_and_grows_clockwise()
    {
        Assert.Equal(0, AngleOf(0, -1), 9);
        Assert.Equal(90, AngleOf(1, 0), 9);
        Assert.Equal(180, AngleOf(0, 1), 9);
        Assert.Equal(270, AngleOf(-1, 0), 9);
        var (x, y) = Direction(90, 10);
        Assert.Equal(10, x, 9);
        Assert.Equal(0, y, 9);
        Assert.Equal(10, AngularDifference(355, 5), 9);
        Assert.Equal(180, AngularDifference(0, 180), 9);
    }

    [Fact]
    public void Items_are_evenly_spread_starting_at_the_top()
    {
        Assert.Empty(Angles(0));
        Assert.Equal(new[] { 0.0 }, Angles(1));
        Assert.Equal(new[] { 0.0, 90, 180, 270 }, Angles(4));
        Assert.Equal(new[] { 0.0, 120, 240 }, Angles(3).Select(a => Math.Round(a, 9)));
    }

    [Fact]
    public void Parent_direction_gets_a_free_slot()
    {
        // Üst öğe altta (180°): üç öğe üst, sağ ve solda; alttaki boşluk geri dilimidir.
        Assert.Equal(new[] { 0.0, 90, 270 }, Angles(3, parentAngle: 180).Select(a => Math.Round(a, 9)));
        var layout = Wedges(Angles(3, 180), 180);
        Assert.NotNull(layout.Parent);
        Assert.True(layout.Parent!.Value.Contains(180));
        Assert.All(layout.Items, w => Assert.False(w.Contains(180)));
    }

    [Fact]
    public void Fixed_angles_are_kept_and_decreasing_ones_ignored()
    {
        var angles = Angles(4, null, [null, 100, null, 50]);
        Assert.Equal(100, angles[1], 9);
        Assert.NotEqual(50, angles[3], 9); // 100'den sonra gelen küçük açı yok sayılır
    }

    public static TheoryData<int, double?> Layouts()
    {
        var data = new TheoryData<int, double?>();
        foreach (var n in new[] { 1, 2, 3, 4, 5, 7, 8, 12 })
        {
            data.Add(n, null);
            data.Add(n, 180);
            data.Add(n, 37.5);
        }
        data.Add(0, 90);
        return data;
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Wedges_cover_the_circle_without_overlap(int count, double? parent)
    {
        var layout = Wedges(Angles(count, parent), parent);
        var all = layout.Items.ToList();
        if (layout.Parent is { } p) all.Add(p);

        Assert.All(all, w => Assert.True(w.End > w.Start));
        Assert.Equal(360, all.Sum(w => w.Sweep), 6);
        for (var a = 0.05; a < 360; a += 0.5)
            Assert.Equal(1, all.Count(w => w.Contains(a)));
        // Her öğe kendi diliminin içinde.
        var angles = Angles(count, parent);
        for (var i = 0; i < count; i++) Assert.True(layout.Items[i].Contains(angles[i]) || count == 1, $"{count}/{i}");
    }

    [Fact]
    public void Wrap_around_the_top_belongs_to_the_first_item()
    {
        var layout = Wedges(Angles(4));
        Assert.Equal(-45, layout.Items[0].Start, 9);
        Assert.Equal(45, layout.Items[0].End, 9);
        foreach (var angle in new[] { 359.9, 0.1, 0.0 })
        {
            var (x, y) = Direction(angle, 100);
            var hit = HitTest(x, y, layout, deadZone: 20);
            Assert.Equal(HitKind.Item, hit.Kind);
            Assert.Equal(0, hit.Index);
        }
        var (rx, ry) = Direction(45.1, 100);
        Assert.Equal(1, HitTest(rx, ry, layout, 20).Index);
    }

    [Fact]
    public void Dead_zone_and_max_radius()
    {
        var layout = Wedges(Angles(4));
        Assert.Equal(HitKind.Center, HitTest(3, -5, layout, deadZone: 10).Kind);
        Assert.Equal(HitKind.Item, HitTest(0, -11, layout, deadZone: 10).Kind);
        Assert.Equal(HitKind.None, HitTest(0, -200, layout, deadZone: 10, maxRadius: 150).Kind);
        Assert.Equal(HitKind.Item, HitTest(0, -200, layout, deadZone: 10).Kind); // sınırsız: uzak imleç de seçer
        Assert.Equal(HitKind.None, HitTest(0, -50, Wedges([]), deadZone: 10).Kind);

        var withParent = Wedges(Angles(3, 180), 180);
        var down = HitTest(0, 80, withParent, 10);
        Assert.Equal(HitKind.Parent, down.Kind);
        Assert.Equal(ParentIndex, down.Index);
    }

    // Dört öğe: 0 üst, 1 sağ, 2 alt, 3 sol.
    private static readonly double[] Four = [0, 90, 180, 270];

    [Fact]
    public void Navigate_picks_the_extreme_item_first()
    {
        Assert.Equal(1, Navigate(Four, null, Arrow.Right));
        Assert.Equal(3, Navigate(Four, null, Arrow.Left));
        Assert.Equal(0, Navigate(Four, null, Arrow.Up));
        Assert.Equal(2, Navigate(Four, null, Arrow.Down));
        Assert.Equal(1, Navigate(Four, 1, Arrow.Right));      // zaten en uçta: değişmez
        Assert.Null(Navigate([], null, Arrow.Right));
        Assert.Equal(1, Navigate(Four, 9, Arrow.Right));      // artık olmayan öğe: hiçbiri sayılır
    }

    [Fact]
    public void Navigate_jumps_across_when_the_hovered_item_is_opposite()
    {
        Assert.Equal(1, Navigate(Four, 3, Arrow.Right));      // soldayken sağ ok → en sağ
        Assert.Equal(0, Navigate(Four, 2, Arrow.Up));
    }

    [Fact]
    public void Navigate_steps_to_the_neighbour_towards_the_direction()
    {
        Assert.Equal(1, Navigate(Four, 0, Arrow.Right));      // üstten sağa: saat yönündeki komşu
        Assert.Equal(3, Navigate(Four, 0, Arrow.Left));       // üstten sola: ters yöndeki komşu
        var eight = Angles(8);
        Assert.Equal(1, Navigate(eight, 0, Arrow.Right));     // 45°'lik adım
        Assert.Equal(7, Navigate(eight, 0, Arrow.Left));
        Assert.Equal(3, Navigate(eight, 2, Arrow.Down));
    }

    [Fact]
    public void Navigate_reaches_the_parent_slot()
    {
        var angles = Angles(3, 180);
        Assert.Equal(ParentIndex, Navigate(angles, null, Arrow.Down, parentAngle: 180));
        Assert.Equal(ParentIndex, Navigate(angles, 0, Arrow.Down, parentAngle: 180));
        Assert.Equal(1, Navigate(angles, ParentIndex, Arrow.Right, parentAngle: 180));
    }

    [Fact]
    public void Drop_index_follows_the_pointer_clockwise_from_the_first_item()
    {
        Assert.Equal(0, DropIndex([], 123));
        Assert.Equal(0, DropIndex(Four, 0));
        Assert.Equal(1, DropIndex(Four, 45));
        Assert.Equal(2, DropIndex(Four, 100));
        Assert.Equal(3, DropIndex(Four, 260));
        Assert.Equal(4, DropIndex(Four, 300));
        Assert.Equal(4, DropIndex(Four, 359.9));   // ilk öğenin hemen solu: sona (çizimde yine solunda)
        Assert.Equal(1, DropIndex(Four, 0.1));
        Assert.Equal(2, DropIndex(Angles(3, 180), 200));
    }

    [Fact]
    public void Radius_grows_with_items_and_has_a_minimum()
    {
        Assert.Equal(60, Radius(0, 48, 60));
        Assert.Equal(60, Radius(4, 48, 60));
        Assert.Equal(12 * (48 + ItemGapDip) / (2 * Math.PI), Radius(12, 48, 60), 9);
        Assert.True(Radius(13, 48, 60) > Radius(12, 48, 60));
        // Komşu öğe merkezleri arasındaki yay en az öğe çapı + aralık.
        var r = Radius(16, 48, 0);
        Assert.True(2 * Math.PI * r / 16 >= 48 + ItemGapDip - Eps);
    }
}
