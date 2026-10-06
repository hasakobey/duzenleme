using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// Kutuların ve halkaların tek listesi (<see cref="Launchers"/>) ve yollarının değişmesi: kutu kimlikle bulunur (geri getirilen
/// kopya da), yol değişince halkanın yol olmayan öğeleri yerinde kalır, eklemeyi geri almak yalnızca eklenen sekmeye dokunur
/// (B14), bağlantı ve yeniden adlandırma da sırayı bozmaz.
/// </summary>
public class LaunchersTests
{
    private static LauncherAction Act(string id, int before) =>
        new() { Id = id, Type = RingActionTypes.Url, PathsBefore = before, Url = "https://example.com/" + id };

    private static string Describe(LauncherTab tab) =>
        string.Join(" ", LauncherEntries.Ordered(tab).Select(e => e.Path ?? e.Action!.Id.ToUpperInvariant()));

    private static WidgetConfig Ring(params LauncherTab[] tabs) =>
        new() { Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring, Tabs = [.. tabs] };

    [Fact]
    public void Launchers_are_boxes_and_rings_found_by_id()
    {
        var box = new WidgetConfig { Kind = WidgetKind.Launcher, Id = "kutu" };
        var ring = Ring(new LauncherTab());
        ring.Id = "halka";
        var settings = new AppSettings { Widgets = [new WidgetConfig { Kind = WidgetKind.Clock, Id = "saat" }, box, ring] };

        Assert.Equal([box, ring], Launchers.All(settings));
        Assert.Equal([ring], Launchers.Rings(settings));
        Assert.Null(Launchers.Find(settings, "saat"));
        Assert.Null(Launchers.Find(settings, null));

        // Widget kaldırılıp geri getirilince ayarın kopyası gelir: eski nesneye değil kimliğe bakılır.
        var copy = box.Clone();
        settings.Widgets[1] = copy;
        Assert.Same(copy, Launchers.Find(settings, "kutu"));
    }

    [Fact]
    public void Replace_paths_keeps_ring_actions_in_place_and_drops_duplicates()
    {
        // [X, P1, A, P2, X'] : X kutuya taşındı (X → X'); X' zaten sonda vardı. İlk görünen kalır, A yerinden oynamaz.
        var tab = new LauncherTab { Items = ["X", "P1", "P2", "X2"], Actions = [Act("a", 2)] };
        var ring = Ring(tab);
        ItemLooks.SetName(ring, "X", "Benim X", "X");
        var settings = new AppSettings { Widgets = [ring] };

        Assert.True(Launchers.ReplacePaths(settings, [("X", "X2", false)]));

        Assert.Equal("X2 P1 A P2", Describe(tab));
        Assert.Equal("Benim X", ItemLooks.Get(ring, "X2")!.Name);
        Assert.False(Launchers.ReplacePaths(settings, [("yok", "hiç", false)]));
    }

    [Fact]
    public void Replace_paths_follows_a_folder_when_asked()
    {
        var box = new WidgetConfig { Kind = WidgetKind.Launcher, Tabs = [new LauncherTab { Items = [@"C:\N\Kutu\Proje", @"C:\N\Kutu\Proje\a.txt"] }] };
        var settings = new AppSettings { Widgets = [box] };

        Launchers.ReplacePaths(settings, [(@"C:\N\Kutu\Proje", @"C:\D\Proje", true)]);

        Assert.Equal([@"C:\D\Proje", @"C:\D\Proje\a.txt"], box.Tabs[0].Items);
    }

    /// <summary>B14: halkada başa bırakılan, kutuya taşınan, sonra "Geri al"ınan öğe çıkınca eylemler eski yerinde kalır (1–9 değişmez).</summary>
    [Fact]
    public void Undo_claim_in_ring_keeps_actions_in_place()
    {
        var tab = new LauncherTab { Items = ["P1", "P2"], Actions = [Act("a", 1)] };
        var ring = Ring(tab);
        var settings = new AppSettings { Widgets = [ring] };
        Assert.Equal("P1 A P2", Describe(tab));

        LauncherEdits.Insert(ring, tab, [@"C:\Desktop\X.lnk"], look: null, at: 0);
        Launchers.ReplacePaths(settings, [(@"C:\Desktop\X.lnk", @"C:\NestDesk\Halka\X.lnk", false)]);
        Assert.Equal(@"C:\NestDesk\Halka\X.lnk P1 A P2", Describe(tab));

        Assert.NotNull(LauncherEdits.Take(ring, tab, LauncherEntries.IndexOfPath(tab, @"C:\NestDesk\Halka\X.lnk")));

        Assert.Equal("P1 A P2", Describe(tab));
    }

    /// <summary>B14: "Geri al" öğeyi yalnızca eklendiği sekmeden çıkarır; geri konunca öteki sekme masaüstündeki yolu gösterir.</summary>
    [Fact]
    public void Undo_claim_keeps_item_in_other_tab()
    {
        var work = new LauncherTab { Name = "İş", Items = [@"C:\Desktop\Steam.lnk"] };
        var games = new LauncherTab { Name = "Oyun" };
        var box = new WidgetConfig { Kind = WidgetKind.Launcher, Tabs = [work, games] };
        var settings = new AppSettings { Widgets = [box] };
        ItemLooks.SetName(box, @"C:\Desktop\Steam.lnk", "Oyunlar", "Steam");

        LauncherEdits.Insert(box, games, [@"C:\Desktop\Steam.lnk"], look: null, at: null);
        Launchers.ReplacePaths(settings, [(@"C:\Desktop\Steam.lnk", @"C:\NestDesk\Kutu\Steam.lnk", false)]);
        LauncherEdits.Take(box, games, LauncherEntries.IndexOfPath(games, @"C:\NestDesk\Kutu\Steam.lnk"));
        // Dosya masaüstüne dönünce (BoxMover.Return) kutular yeni yolu gösterir.
        Launchers.ReplacePaths(settings, [(@"C:\NestDesk\Kutu\Steam.lnk", @"C:\Desktop\Steam.lnk", false)]);

        Assert.Equal([@"C:\Desktop\Steam.lnk"], work.Items);
        Assert.Empty(games.Items);
        Assert.Equal("Oyunlar", ItemLooks.Get(box, @"C:\Desktop\Steam.lnk")!.Name); // öteki sekmede durduğu için adı kalır
    }

    [Fact]
    public void Link_rebase_keeps_ring_order()
    {
        var links = Path.Combine(Path.GetTempPath(), "nd-links-" + Guid.NewGuid().ToString("N"), LinkFiles.FolderName);
        var old = @"E:\NestDesk\data\links\example.com-0011aabb.url";
        var tab = new LauncherTab { Items = [old, "P1", Path.Combine(links, "example.com-0011aabb.url")], Actions = [Act("a", 1)] };
        var ring = Ring(tab);

        Assert.True(LinkFiles.RebaseAll([ring], links));

        Assert.Equal($"{Path.Combine(links, "example.com-0011aabb.url")} A P1", Describe(tab));
    }

    [Fact]
    public void Rename_keeps_ring_order()
    {
        var tab = new LauncherTab { Items = [@"C:\D\a.txt", @"C:\D\b.txt", @"C:\D\B2.txt"], Actions = [Act("a", 1), Act("b", 3)] };
        var ring = Ring(tab);

        Assert.True(PathRenames.Apply([ring], @"C:\D\b.txt", @"C:\D\b2.txt", isDirectory: false, @"C:\D"));

        // "b2" iki kez kaldı: ilki kalır, eylemler yerinde.
        Assert.Equal(@"C:\D\a.txt A C:\D\b2.txt B", Describe(tab));
    }
}
