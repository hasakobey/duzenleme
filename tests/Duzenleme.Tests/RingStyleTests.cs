using System.Windows;
using Duzenleme.Core;
using Duzenleme.Localization;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// Halka biçimleri tablosu (<see cref="RingStyles"/>) ve biçimlerin ölçüleri: kimlikler ayarda kalıcı (ASCII, tekil, Klasik
/// yazılmaz), tanınmayan ad Klasik çizilir ve korunur, yüksek karşıtlıkta her biçim Klasik; Etiketli'de etiketler ne birbirine
/// ne öğe dairelerine ne orta daireye biner ve dış disk hepsini kapsar; Daire'nin yörüngesi simgelerin altından geçmez.
/// </summary>
public class RingStyleTests
{
    [Fact]
    public void Style_table_is_stable()
    {
        Assert.Equal(4, RingStyles.All.Count);
        Assert.Same(RingStyles.Classic, RingStyles.All[0]);
        Assert.Null(RingStyles.Classic.Id);
        var ids = RingStyles.All.Skip(1).Select(s => s.Id!).ToList();
        Assert.Equal([RingStyles.Orbit, RingStyles.Labeled, RingStyles.Glow], ids);
        Assert.Equal(["orbit", "labels", "glow"], ids); // ayarda yazılı: değişmez
        Assert.All(ids, id => Assert.Matches("^[a-z]+$", id));
        Assert.DoesNotContain(RingStyles.ClassicId, ids);
        Assert.Equal(RingStyles.All.Count, RingStyles.All.Select(s => s.Name).Distinct().Count());
        // Klasik 2.2'nin ilk çizimi: dilim, ayırıcı ve yay; süs yok.
        var c = RingStyles.Classic;
        Assert.True(c.Wedges && c.Separators && c.Rims);
        Assert.False(c.Track || c.Labels || c.Glow || c.Pointer);
        // Her biçimin bir ayırt edici yanı var (iki biçim aynı çizilmez).
        Assert.Equal(RingStyles.All.Count, RingStyles.All.Select(s => (s.Wedges, s.Separators, s.Rims, s.Track, s.Labels, s.Glow, s.Pointer)).Distinct().Count());
    }

    [Fact]
    public void Unknown_and_missing_ids_draw_classic_and_high_contrast_is_plain()
    {
        Assert.Same(RingStyles.Classic, RingStyles.For(null));
        Assert.Same(RingStyles.Classic, RingStyles.For(""));
        Assert.Same(RingStyles.Classic, RingStyles.For("hologram"));
        Assert.Same(RingStyles.Classic, RingStyles.For("ORBIT"));
        foreach (var style in RingStyles.All)
        {
            Assert.Same(style, RingStyles.For(style.Id));
            Assert.Same(RingStyles.Classic, RingStyles.Effective(style.Id, highContrast: true));
            Assert.Same(style, RingStyles.Effective(style.Id, highContrast: false));
        }
    }

    [Fact]
    public void Choosing_a_style_writes_only_its_id()
    {
        var c = new WidgetConfig { Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring };
        Assert.Equal(RingStyles.ClassicId, RingStyles.CurrentId(c));
        Assert.True(RingStyles.Apply(c, RingStyles.Glow));
        Assert.Equal("glow", c.RingStyle);
        Assert.Equal(RingStyles.Glow, RingStyles.CurrentId(c));
        Assert.True(RingStyles.Apply(c, RingStyles.ClassicId));
        Assert.Null(c.RingStyle);
        // Tanınmayan kimlik bir şeyi değiştirmez; ayardaki tanınmayan ad korunur, kart Klasik görünür.
        c.RingStyle = "hologram";
        Assert.False(RingStyles.Apply(c, "nope"));
        Assert.Equal("hologram", c.RingStyle);
        Assert.Equal(RingStyles.ClassicId, RingStyles.CurrentId(c));
    }

    [Fact]
    public void Style_names_are_translated()
    {
        var turkish = RingStyles.All.Select(s => s.Label).ToList();
        Assert.Equal(["Klasik", "Daire", "Etiketli", "Işıltı"], turkish);
        using var _ = L.Use(Lang.En);
        Assert.Equal(["Classic", "Orbit", "Labeled", "Glow"], RingStyles.All.Select(s => s.Label));
    }

    /// <summary>
    /// Etiketli: her öğe sayısında (alt halkanın geri yeriyle de) etiketler birbirine, başka öğenin dairesine ve orta daireye
    /// binmez; hepsi dış diskin içinde. Etiket dışa bakar (üst yarıda üstte).
    /// </summary>
    [Fact]
    public void Labels_never_overlap_and_stay_on_the_disc()
    {
        var style = RingStyles.For(RingStyles.Labeled);
        foreach (var icon in new[] { 24.0, 32, 48 })
            for (var places = 1; places <= 12; places++)
                foreach (var parent in new double?[] { null, 0, 135, 250 })
                {
                    var count = parent is null ? places : Math.Max(1, places - 1);
                    var m = RingMetrics.For(places, icon, style);
                    var angles = RingGeometry.Angles(count, parent);
                    var r = m.ItemDip / 2;
                    var o = new Point(0, 0);
                    var labels = angles.Select(a => RingItemVisual.LabelRect(RingView.At(o, m.Radius, a), m, a)).ToList();
                    var chips = angles.Select(a => RingView.At(o, m.Radius, a)).ToList();
                    if (parent is { } p) chips.Add(RingView.At(o, m.Radius, p));
                    var what = $"{icon}/{places}/{parent}";
                    for (var i = 0; i < labels.Count; i++)
                    {
                        var label = labels[i];
                        Assert.True(label.Top < chips[i].Y ? label.Bottom <= chips[i].Y - r : label.Top >= chips[i].Y + r, $"{what}: etiket kendi dairesinde");
                        Assert.Equal(RingStyles.LabelAbove(angles[i]), label.Bottom <= chips[i].Y);
                        for (var j = 0; j < labels.Count; j++)
                            if (j != i) Assert.False(label.IntersectsWith(Inflate(labels[j], 1)), $"{what}: {i} ile {j} etiketi");
                        for (var j = 0; j < chips.Count; j++)
                            if (j != i) Assert.True(Distance(label, chips[j]) >= r + 1, $"{what}: {i} etiketi {j} dairesinde");
                        Assert.True(Distance(label, o) >= m.CenterRadius + 4, $"{what}: {i} etiketi orta dairede");
                        foreach (var corner in new[] { label.TopLeft, label.TopRight, label.BottomLeft, label.BottomRight })
                            Assert.True((corner - o).Length <= m.OuterRadius - 2, $"{what}: {i} etiketi diskin dışında");
                    }
                }
    }

    /// <summary>Göstergeli biçimlerde orta daire ile öğeler arasında gösterge çizgisine yer kalır; her biçimde odak halkası yerinde.</summary>
    [Fact]
    public void Every_style_leaves_room_between_center_and_items()
    {
        foreach (var style in RingStyles.All)
            foreach (var icon in new[] { 24.0, 36, 48, 64 })
                foreach (var places in new[] { 1, 4, 8, 12, 16 })
                {
                    var m = RingMetrics.For(places, icon, style);
                    var gap = m.Radius - m.ItemDip / 2 - m.CenterRadius;
                    Assert.True(gap >= RingMetrics.Gap + (style.Pointer ? RingMetrics.PointerRoom : 0) - 0.01, $"{style.Id}/{icon}/{places}: {gap:0.0}");
                    Assert.True(m.CenterRadius >= RingMetrics.MinCenterRadius);
                    Assert.True(m.OuterRadius >= m.Radius + m.ItemDip / 2 + RingMetrics.Gap - 0.01);
                }
        // Klasik'in ölçüleri değişmedi.
        Assert.Equal(RingMetrics.For(8, 36), RingMetrics.For(8, 36, RingStyles.Classic));
    }

    /// <summary>Daire'nin yörüngesi öğe dairelerinin (ve küçük geri dairesinin) içinden geçmez: simgenin altına çizgi düşmez.</summary>
    [Fact]
    public void Orbit_track_stays_between_the_circles() => OnSta(() =>
    {
        foreach (var places in new[] { 0, 1, 2, 5, 8, 12 })
            foreach (var parent in new double?[] { null, 90, 300 })
            {
                var m = RingMetrics.For(places + (parent is null ? 0 : 1), 32, RingStyles.For(RingStyles.Orbit));
                var c = new Point(m.OuterRadius, m.OuterRadius);
                var angles = RingGeometry.Angles(places, parent);
                var track = RingView.Track(c, m, angles, parent);
                Assert.True(track.IsFrozen);
                // Her yay: başından sonuna saat yönünde 1 derecelik adımlarla noktalar (yaylar yörünge çemberindedir).
                var path = System.Windows.Media.PathGeometry.CreateFromGeometry(track);
                var points = new List<Point>();
                foreach (var figure in path.Figures)
                {
                    var from = RingGeometry.AngleOf(figure.StartPoint.X - c.X, figure.StartPoint.Y - c.Y);
                    foreach (var arc in figure.Segments.OfType<System.Windows.Media.ArcSegment>())
                    {
                        var to = RingGeometry.AngleOf(arc.Point.X - c.X, arc.Point.Y - c.Y);
                        var span = ((to - from) % 360 + 360) % 360;
                        if (span < 0.01) span = 360;
                        for (var t = 0.0; t <= span; t += 1) points.Add(RingView.At(c, m.Radius, from + t));
                        points.Add(arc.Point);
                        from = to;
                    }
                }
                if (places <= 5) Assert.NotEmpty(points);
                foreach (var point in points)
                {
                    Assert.InRange((point - c).Length, m.Radius - 0.5, m.Radius + 0.5);
                    foreach (var a in angles)
                        Assert.True((point - RingView.At(c, m.Radius, a)).Length >= m.ItemDip / 2 + 2, $"{places}/{parent}: yörünge dairede");
                    if (parent is { } p)
                        Assert.True((point - RingView.At(c, m.Radius, p)).Length >= m.ItemDip / 2 * RingView.BackScale + 2, $"{places}/{parent}: geri dairesinde");
                }
            }
    });

    private static void OnSta(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA testi zaman aşımına uğradı");
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static Rect Inflate(Rect rect, double by)
    {
        rect.Inflate(by, by);
        return rect;
    }

    /// <summary>Noktanın dikdörtgene uzaklığı (içindeyse 0).</summary>
    private static double Distance(Rect rect, Point point)
    {
        var dx = Math.Max(Math.Max(rect.Left - point.X, 0), point.X - rect.Right);
        var dy = Math.Max(Math.Max(rect.Top - point.Y, 0), point.Y - rect.Bottom);
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
