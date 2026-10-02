using System.Windows.Input;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>Halkanın ölçüleri, fare isabeti, bırakma sırası, taşıma ve klavye eşlemesi (RingView'un saf bölümleri).</summary>
public class RingInputTests
{
    [Fact]
    public void Metrics_keep_the_items_between_the_center_and_the_rim()
    {
        foreach (var icon in new[] { 24.0, 36, 48, 64 })
            for (var places = 0; places <= 24; places++)
            {
                var m = RingMetrics.For(places, icon);
                Assert.Equal(icon + 18, m.ItemDip);
                Assert.True(m.CenterRadius >= RingMetrics.MinCenterRadius);
                // Öğe dairesi ortadaki daireye ve dış kenara değmez.
                Assert.True(m.Radius - m.ItemDip / 2 - m.CenterRadius >= RingMetrics.Gap - 1e-9, $"{icon}/{places}");
                Assert.Equal(m.Radius + m.ItemDip / 2 + RingMetrics.Gap, m.OuterRadius, 6);
                Assert.Equal(m.OuterRadius * 2, m.Size);
                // Öğeler birbirine binmez: komşu merkezler arası en az bir öğe çapı.
                if (places > 1) Assert.True(2 * Math.PI * m.Radius / places >= m.ItemDip - 1e-9, $"{icon}/{places}");
            }
        // Çok öğeli halka büyür, az öğeli halka en küçük boyutunda kalır.
        Assert.Equal(RingMetrics.For(1, 36).Size, RingMetrics.For(6, 36).Size);
        Assert.True(RingMetrics.For(16, 36).Size > RingMetrics.For(6, 36).Size);
    }

    [Fact]
    public void Hit_test_by_angle_with_a_dead_center()
    {
        var m = RingMetrics.For(4, 36);
        var wedges = RingGeometry.Wedges(RingGeometry.Angles(4));   // 0° üst, 90° sağ, 180° alt, 270° sol
        Assert.Equal(RingGeometry.HitKind.Center, RingInput.HitAt(m, wedges, 0, 0, limitToDisc: true).Kind);
        Assert.Equal(RingGeometry.HitKind.Center, RingInput.HitAt(m, wedges, m.CenterRadius - 1, 0, limitToDisc: true).Kind);
        Assert.Equal(new RingGeometry.Hit(RingGeometry.HitKind.Item, 0), RingInput.HitAt(m, wedges, 0, -m.Radius, limitToDisc: true));
        Assert.Equal(new RingGeometry.Hit(RingGeometry.HitKind.Item, 1), RingInput.HitAt(m, wedges, m.Radius, 5, limitToDisc: true));
        Assert.Equal(new RingGeometry.Hit(RingGeometry.HitKind.Item, 2), RingInput.HitAt(m, wedges, -3, m.Radius, limitToDisc: true));
        Assert.Equal(new RingGeometry.Hit(RingGeometry.HitKind.Item, 3), RingInput.HitAt(m, wedges, -m.Radius, 0, limitToDisc: true));
        // Masaüstündeki widget'ta diskin dışı halkanın değil (widget'ı taşımaya kalır); açılır halkada açı yeter.
        Assert.Equal(RingGeometry.HitKind.None, RingInput.HitAt(m, wedges, m.OuterRadius + 1, 0, limitToDisc: true).Kind);
        Assert.Equal(new RingGeometry.Hit(RingGeometry.HitKind.Item, 1), RingInput.HitAt(m, wedges, m.OuterRadius * 3, 0, limitToDisc: false));
    }

    [Fact]
    public void Sub_ring_has_a_back_wedge()
    {
        var angles = RingGeometry.Angles(3, parentAngle: 180);
        var wedges = RingGeometry.Wedges(angles, 180);
        var m = RingMetrics.For(4, 36);
        Assert.Equal(RingGeometry.HitKind.Parent, RingInput.HitAt(m, wedges, 0, m.Radius, limitToDisc: true).Kind);
        Assert.Equal(RingGeometry.HitKind.Item, RingInput.HitAt(m, wedges, 0, -m.Radius, limitToDisc: true).Kind);
    }

    [Fact]
    public void Drop_index_counts_entries_before_the_pointer_and_never_lands_among_sub_rings()
    {
        // 6 yer: 4 öğe (0°, 60°, 120°, 180°) ve 2 alt halka (240°, 300°).
        var angles = RingGeometry.Angles(6);
        static (double, double) Toward(double angle) => RingGeometry.Direction(angle, 100);
        var (x, y) = Toward(30);
        Assert.Equal(1, RingInput.DropIndex(angles, 4, x, y));     // 1. ile 2. öğenin arası
        (x, y) = Toward(150);
        Assert.Equal(3, RingInput.DropIndex(angles, 4, x, y));
        (x, y) = Toward(210);
        Assert.Equal(4, RingInput.DropIndex(angles, 4, x, y));     // son öğeden sonra
        (x, y) = Toward(270);
        Assert.Equal(4, RingInput.DropIndex(angles, 4, x, y));     // alt halkaların arası: öğelerin sonuna
        (x, y) = Toward(355);
        Assert.Equal(4, RingInput.DropIndex(angles, 4, x, y));
        Assert.Equal(0, RingInput.DropIndex([], 0, 10, 10));
    }

    [Theory]
    [InlineData(0, 4, true, 1)]
    [InlineData(3, 4, true, 0)]      // son öğe saat yönünde başa döner
    [InlineData(2, 4, false, 1)]
    [InlineData(0, 4, false, 3)]     // ilk öğe ters yönde sona
    public void Step_moves_around_the_ring(int index, int count, bool clockwise, int expected)
    {
        Assert.Equal(expected, RingInput.StepTarget(index, count, clockwise));
        // LauncherEntries.Move ile uygulanınca öğe gerçekten komşusunun yerine geçer.
        var tab = new LauncherTab { Items = Enumerable.Range(0, count).Select(i => "p" + i).ToList() };
        LauncherEntries.Move(tab, index, expected);
        Assert.Equal("p" + index, tab.Items[expected]);
    }

    [Fact]
    public void Step_needs_two_items()
    {
        Assert.Null(RingInput.StepTarget(0, 1, true));
        Assert.Null(RingInput.StepTarget(5, 3, true));
        Assert.Null(RingInput.StepTarget(-1, 3, false));
    }

    [Theory]
    [InlineData(Key.D1, ModifierKeys.None, "Number", 1)]
    [InlineData(Key.D9, ModifierKeys.None, "Number", 9)]
    [InlineData(Key.NumPad4, ModifierKeys.None, "Number", 4)]
    [InlineData(Key.D0, ModifierKeys.None, "None", 0)]
    [InlineData(Key.NumPad0, ModifierKeys.None, "None", 0)]
    [InlineData(Key.D3, ModifierKeys.Control, "None", 0)]     // Ctrl+3 halkanın değil
    [InlineData(Key.D3, ModifierKeys.Shift, "None", 0)]
    [InlineData(Key.Enter, ModifierKeys.None, "Enter", 0)]
    [InlineData(Key.Space, ModifierKeys.None, "Enter", 0)]
    [InlineData(Key.Back, ModifierKeys.None, "Back", 0)]
    [InlineData(Key.Escape, ModifierKeys.None, "Escape", 0)]
    [InlineData(Key.A, ModifierKeys.None, "None", 0)]
    public void Keys_map_to_ring_commands(Key key, ModifierKeys modifiers, string kind, int number)
    {
        var mapped = RingInput.Map(key, modifiers);
        Assert.Equal(kind, mapped.Kind.ToString());
        Assert.Equal(number, mapped.Number);
    }

    [Theory]
    [InlineData(Key.Left, RingGeometry.Arrow.Left)]
    [InlineData(Key.Right, RingGeometry.Arrow.Right)]
    [InlineData(Key.Up, RingGeometry.Arrow.Up)]
    [InlineData(Key.Down, RingGeometry.Arrow.Down)]
    public void Arrows_map_to_navigation(Key key, RingGeometry.Arrow arrow)
    {
        var mapped = RingInput.Map(key, ModifierKeys.None);
        Assert.Equal(RingKey.Arrow, mapped.Kind);
        Assert.Equal(arrow, mapped.Arrow);
    }

    [Fact]
    public void Number_keys_pick_existing_items_only()
    {
        Assert.Equal(0, RingInput.NumberIndex(1, 3));
        Assert.Equal(2, RingInput.NumberIndex(3, 3));
        Assert.Null(RingInput.NumberIndex(4, 3));
        Assert.Equal(8, RingInput.NumberIndex(9, 12));    // onuncu ve sonrası numarasız
        Assert.Null(RingInput.NumberIndex(0, 12));
        Assert.Null(RingInput.NumberIndex(10, 12));
    }

    [Theory]
    [InlineData(false, true, 4, 0, true)]     // widget, yol öğesi, eşiği geçti: dışarı sürüklenir
    [InlineData(false, true, 1, 1, false)]    // eşiğin altında: tıklama
    [InlineData(false, false, 4, 0, false)]   // eylem / alt halka / soluk yol: biraz kayan tıklama seçmeye devam eder
    [InlineData(false, false, 40, 30, false)]
    [InlineData(true, true, 40, 0, false)]    // açılır halkadan dışarı sürüklenmez
    public void Only_draggable_items_turn_a_moving_press_into_a_drag(bool popup, bool canDrag, double dx, double dy, bool expected) =>
        Assert.Equal(expected, RingInput.StartsDragOut(popup, canDrag, dx, dy, 4, 4));

    [Fact]
    public void A_press_that_slides_a_little_on_an_action_or_sub_ring_still_selects_it()
    {
        var item = new RingGeometry.Hit(RingGeometry.HitKind.Item, 2);
        var other = new RingGeometry.Hit(RingGeometry.HitKind.Item, 3);
        var none = RingGeometry.Hit.None;
        // Basış sürüklemeye dönmedi (sürüklenemeyen öğe): aynı öğede bırakınca o seçilir.
        Assert.False(RingInput.StartsDragOut(false, canDragOut: false, 4, 3, 4, 4));
        Assert.Equal(item, RingInput.ReleaseTarget(item, item, isPopup: false));
        // Widget'ta başka öğede bırakmak bir şey seçmez; açılır halkada o öğeyi seçer (pasta menüsü).
        Assert.Null(RingInput.ReleaseTarget(item, other, isPopup: false));
        Assert.Equal(other, RingInput.ReleaseTarget(item, other, isPopup: true));
        // Ortaya basıp bırakmak (alt halkada geri) yerinde kalır; basılmamışsa hiçbir şey.
        var center = new RingGeometry.Hit(RingGeometry.HitKind.Center, 0);
        Assert.Equal(center, RingInput.ReleaseTarget(center, center, isPopup: false));
        Assert.Null(RingInput.ReleaseTarget(center, item, isPopup: false));
        Assert.Null(RingInput.ReleaseTarget(none, item, isPopup: true));
    }
}
