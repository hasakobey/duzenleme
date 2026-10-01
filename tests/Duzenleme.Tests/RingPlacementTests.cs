using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>Kısayolla açılan halkanın yeri: çember çalışma alanında, pencere monitörde; eksi x'teki sol monitör dahil.</summary>
public class RingPlacementTests
{
    private const int R = 150;   // çember yarıçapı
    private const int W = 400;   // pencere (çember + nişan alanı)
    private static readonly int Reach = R + RingPlacement.MarginPx;

    private static readonly Box Main = new(0, 0, 1920, 1080);
    private static readonly Box MainWork = new(0, 0, 1920, 1040);   // altta görev çubuğu

    private static RingPlacement.Result Place(Box monitor, Box work, int x, int y) =>
        RingPlacement.Place(monitor, work, x, y, R, W, W);

    [Fact]
    public void Centred_on_the_cursor_when_there_is_room()
    {
        var p = Place(Main, MainWork, 960, 500);
        Assert.Equal((960, 500), (p.CenterX, p.CenterY));
        Assert.Equal((760, 300), (p.Left, p.Top));
        Assert.Equal((200, 200), (p.OffsetX, p.OffsetY));
    }

    [Theory]
    [InlineData(5, 500, 158, 500)]          // sol kenar
    [InlineData(1915, 500, 1762, 500)]      // sağ kenar
    [InlineData(960, 3, 960, 158)]          // üst kenar
    [InlineData(960, 1075, 960, 882)]       // alt: görev çubuğunun üstünde kalır
    [InlineData(0, 0, 158, 158)]            // köşe
    [InlineData(1919, 1079, 1762, 882)]
    public void Circle_stays_inside_the_work_area(int x, int y, int cx, int cy)
    {
        var p = Place(Main, MainWork, x, y);
        Assert.Equal((cx, cy), (p.CenterX, p.CenterY));
        AssertInside(p, Main, MainWork);
    }

    [Fact]
    public void Window_is_clamped_to_the_monitor_and_the_offset_follows()
    {
        var p = Place(Main, MainWork, 5, 500);
        Assert.Equal(0, p.Left);               // 158 - 200 = -42 → monitörün içine
        Assert.Equal(158, p.OffsetX);           // çizim halkayı pencerenin ortasında değil burada çizer
        var right = Place(Main, MainWork, 1915, 500);
        Assert.Equal(1920 - W, right.Left);
        Assert.Equal(1762 - (1920 - W), right.OffsetX);
    }

    [Fact]
    public void Left_test_monitor_with_negative_coordinates()
    {
        var monitor = new Box(-1920, 0, 0, 1080);
        var work = new Box(-1920, 0, 0, 1040);
        var p = Place(monitor, work, -1915, 5);
        Assert.Equal((-1920 + Reach, Reach), (p.CenterX, p.CenterY));
        Assert.Equal(-1920, p.Left);
        AssertInside(p, monitor, work);

        var right = Place(monitor, work, -2, 1000);
        Assert.Equal((-Reach, 1040 - Reach), (right.CenterX, right.CenterY));
        Assert.Equal(-W, right.Left);
        AssertInside(right, monitor, work);
    }

    [Fact]
    public void Work_area_smaller_than_the_ring_centres_it()
    {
        var tiny = new Box(-301, 0, 0, 200);
        var p = Place(tiny, tiny, -290, 190);
        Assert.Equal(-151, p.CenterX);          // (-301 + 0) / 2 aşağı yuvarlanır
        Assert.Equal(100, p.CenterY);
        Assert.Equal(-301, p.Left);             // pencere sığmıyor: sol/üst kenara
        Assert.Equal(0, p.Top);
    }

    [Fact]
    public void Taskbar_on_the_left()
    {
        var work = new Box(60, 0, 1920, 1080);
        var p = Place(Main, work, 5, 500);
        Assert.Equal(60 + Reach, p.CenterX);
        Assert.Equal(18, p.Left);               // pencerenin saydam nişan alanı görev çubuğuna taşabilir, çember taşmaz
        AssertInside(p, Main, work);
    }

    [Fact]
    public void Any_cursor_position_keeps_everything_inside()
    {
        var monitor = new Box(-1920, -200, 0, 880);
        var work = new Box(-1880, -200, 0, 840);
        var random = new Random(8);
        for (var i = 0; i < 500; i++)
        {
            var p = Place(monitor, work, random.Next(-1920, 0), random.Next(-200, 880));
            AssertInside(p, monitor, work);
        }
    }

    private static void AssertInside(RingPlacement.Result p, Box monitor, Box work)
    {
        Assert.True(p.CenterX - Reach >= work.Left && p.CenterX + Reach <= work.Right, $"x {p.CenterX}");
        Assert.True(p.CenterY - Reach >= work.Top && p.CenterY + Reach <= work.Bottom, $"y {p.CenterY}");
        Assert.True(new Box(p.Left, p.Top, p.Left + W, p.Top + W).Inside(monitor), $"window {p.Left},{p.Top}");
        // Çember pencerenin içinde.
        Assert.InRange(p.OffsetX, R, W - R);
        Assert.InRange(p.OffsetY, R, W - R);
    }
}
