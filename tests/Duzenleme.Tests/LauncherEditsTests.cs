using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// Kısayol kutusu ile halka menünün ortak düzenlemeleri (LauncherEdits'in saf bölümleri): kutudan çıkarıp geri alma eski
/// davranışla aynı (aynı sıraya döner, kutudaki adı ve simgesi geri gelir, başka sekmede de duran öğenin adı silinmez);
/// ekleme aynı yolu iki kez eklemez; halkanın yol olmayan öğeleri yollarla birlikte yerinde kalır.
/// </summary>
public class LauncherEditsTests
{
    private static WidgetConfig Box(params string[] paths) =>
        new() { Kind = WidgetKind.Launcher, Tabs = [new LauncherTab { Name = "Uygulamalar", Items = [.. paths] }, new LauncherTab { Name = "Dosyalar" }] };

    [Fact]
    public void Take_and_put_return_the_item_to_its_place_with_its_look()
    {
        var box = Box(@"C:\a.exe", @"C:\b.exe", @"C:\c.exe");
        var tab = box.Tabs[0];
        ItemLooks.SetName(box, @"C:\b.exe", "Benim B", "b");
        ItemLooks.SetIcon(box, @"C:\b.exe", "sym:Rocket24");

        var removed = LauncherEdits.Take(box, tab, LauncherEntries.IndexOfPath(tab, @"C:\B.EXE"))!;
        Assert.Equal(new[] { @"C:\a.exe", @"C:\c.exe" }, tab.Items);
        Assert.Null(ItemLooks.Get(box, @"C:\b.exe"));                     // adı ve simgesi de gitti…
        Assert.Equal("Benim B", removed.Look!.Name);                       // …ama "Geri al" için tutuldu
        Assert.Equal(1, removed.Index);

        Assert.True(LauncherEdits.Put(box, removed));
        Assert.Equal(new[] { @"C:\a.exe", @"C:\b.exe", @"C:\c.exe" }, tab.Items);
        Assert.Equal("Benim B", ItemLooks.Get(box, @"C:\b.exe")!.Name);
        Assert.Equal("sym:Rocket24", ItemLooks.Get(box, @"C:\b.exe")!.Icon);
    }

    [Fact]
    public void Look_stays_when_the_item_is_in_another_tab_too()
    {
        var box = Box(@"C:\a.exe");
        box.Tabs[1].Items.Add(@"C:\A.exe");
        ItemLooks.SetName(box, @"C:\a.exe", "Ortak", "a");
        var removed = LauncherEdits.Take(box, box.Tabs[0], 0)!;
        Assert.Null(removed.Look);
        Assert.Equal("Ortak", ItemLooks.Get(box, @"C:\a.exe")!.Name);
        Assert.True(LauncherEdits.Elsewhere(box, box.Tabs[0], @"c:\A.EXE"));
        Assert.False(LauncherEdits.Elsewhere(box, box.Tabs[1], @"C:\a.exe"));
    }

    [Fact]
    public void Put_does_not_duplicate_and_clamps_the_position()
    {
        var box = Box(@"C:\a.exe", @"C:\b.exe");
        var tab = box.Tabs[0];
        var removed = LauncherEdits.Take(box, tab, 1)!;
        // Arada aynı öğe yeniden eklendi: "Geri al" ikinci bir kopya eklemez.
        tab.Items.Add(@"C:\B.exe");
        Assert.False(LauncherEdits.Put(box, removed));
        Assert.Equal(2, tab.Items.Count);

        // Arada öğeler azaldıysa sona döner.
        var other = Box(@"C:\a.exe", @"C:\b.exe", @"C:\c.exe");
        var last = LauncherEdits.Take(other, other.Tabs[0], 2)!;
        other.Tabs[0].Items.Clear();
        Assert.True(LauncherEdits.Put(other, last));
        Assert.Equal(new[] { @"C:\c.exe" }, other.Tabs[0].Items);
        Assert.Null(LauncherEdits.Take(other, other.Tabs[0], 5));
    }

    [Fact]
    public void Insert_appends_new_paths_only_and_gives_a_moved_look()
    {
        var box = Box(@"C:\a.exe");
        var look = new ItemLook { Name = "Taşınan", Icon = "sym:Games24" };
        var added = LauncherEdits.Insert(box, box.Tabs[0], [@"C:\A.EXE", @"C:\b.exe", @"C:\c.exe"], look, at: null);
        Assert.Equal(new[] { @"C:\b.exe", @"C:\c.exe" }, added);
        Assert.Equal(new[] { @"C:\a.exe", @"C:\b.exe", @"C:\c.exe" }, box.Tabs[0].Items);
        Assert.Equal("Taşınan", ItemLooks.Get(box, @"C:\b.exe")!.Name);
        // Öğenin kendi adı varsa taşınan ad onu ezmez.
        ItemLooks.SetName(box, @"C:\d.exe", "Kendi", "d");
        LauncherEdits.Insert(box, box.Tabs[0], [@"C:\d.exe"], look, at: null);
        Assert.Equal("Kendi", ItemLooks.Get(box, @"C:\d.exe")!.Name);
    }

    [Fact]
    public void Ring_insert_at_the_drop_position_keeps_actions_in_place()
    {
        var ring = WidgetSeeds.Create(WidgetSeeds.Ring)!;              // 4 NestDesk eylemi
        var tab = ring.Tabs[0];
        var ids = tab.Actions!.Select(a => a.Id).ToList();
        var added = LauncherEdits.Insert(ring, tab, [@"C:\x.exe", @"C:\y.exe"], null, at: 2);
        Assert.Equal(2, added.Count);
        var order = LauncherEntries.Ordered(tab).Select(e => e.Path ?? e.Action!.Id).ToList();
        Assert.Equal(new[] { ids[0], ids[1], @"C:\x.exe", @"C:\y.exe", ids[2], ids[3] }, order);

        // Eylem çıkarılıp geri alınınca aynı nesne aynı yere döner (yollar yerinde kalır).
        var removed = LauncherEdits.Take(ring, tab, 4)!;
        Assert.True(removed.Entry.IsAction);
        Assert.Null(removed.Look);
        Assert.True(LauncherEdits.Put(ring, removed));
        Assert.Equal(order, LauncherEntries.Ordered(tab).Select(e => e.Path ?? e.Action!.Id));
        Assert.Same(removed.Entry.Action, tab.Actions![2]);
    }
}
