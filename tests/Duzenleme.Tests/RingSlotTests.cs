using Duzenleme.Core;
using Duzenleme.Localization;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;

namespace Duzenleme.Tests;

/// <summary>
/// Halkanın yerleri: sekmenin öğeleri görünen sırasıyla, kök halkada arkasından alt halkalar (<c>Tabs[1..]</c>); adlar,
/// varsayılan simgeler ve çalıştırılamayan öğelerin soluk çizilmesi.
/// </summary>
public class RingSlotTests
{
    private static WidgetConfig Ring() => WidgetSeeds.Create(WidgetSeeds.Ring)!;

    [Fact]
    public void Seed_ring_has_the_four_nestdesk_actions_and_no_sub_rings()
    {
        var slots = RingSlot.Build(Ring(), 0);
        Assert.Equal(RingAppIds.Seed.ToArray(), slots.Select(s => s.Action!.App!).ToArray());
        Assert.All(slots, s => Assert.Equal(RingSlotKind.Action, s.Kind));
        Assert.Equal(new[] { 0, 1, 2, 3 }, slots.Select(s => s.EntryIndex));
        Assert.Equal(RingAppIds.Seed.Select(id => RingAppIds.Label(id)!).ToArray(), slots.Select(s => s.Name).ToArray());
        Assert.All(slots, s => Assert.True(s.Runnable));
        Assert.All(slots, s => Assert.False(s.Dimmed));
    }

    [Fact]
    public void Sub_rings_are_appended_to_the_root_with_their_item_count()
    {
        var ring = Ring();
        ring.Tabs.Add(new LauncherTab { Name = "Oyunlar", Items = [@"C:\Oyunlar\a.exe", @"C:\Oyunlar\b.exe"], Actions = [new() { Type = RingActionTypes.Url, Url = "https://example.com" }] });
        ring.Tabs.Add(new LauncherTab { Name = "  " });

        var root = RingSlot.Build(ring, 0);
        Assert.Equal(6, root.Count);
        var games = root[4];
        Assert.Equal(RingSlotKind.SubRing, games.Kind);
        Assert.Equal(1, games.TabIndex);
        Assert.Equal(-1, games.EntryIndex);
        Assert.Equal("Oyunlar", games.Name);
        Assert.Equal(3, games.ChildCount);
        Assert.Equal(RingSlot.SubRingGlyph, games.Glyph);
        Assert.Equal("Alt halka", root[5].Name);   // adsız sekme
        Assert.Equal(0, root[5].ChildCount);

        // Alt halkanın kendisi: yalnızca öğeleri (alt halka tek düzey); eylem PathsBefore = 0 ile en başta.
        var sub = RingSlot.Build(ring, 1);
        Assert.Equal(new[] { RingSlotKind.Action, RingSlotKind.Path, RingSlotKind.Path }, sub.Select(s => s.Kind));
        Assert.Equal(new[] { "example.com", "a", "b" }, sub.Select(s => s.Name));
        Assert.Empty(RingSlot.Build(ring, 7));
    }

    [Fact]
    public void Order_follows_launcher_entries()
    {
        var ring = Ring();
        var tab = ring.Tabs[0];
        LauncherEntries.Insert(tab, 1, LauncherEntry.Of(@"C:\x\Rapor.pdf"));
        var slots = RingSlot.Build(ring, 0);
        Assert.Equal(RingSlotKind.Path, slots[1].Kind);
        Assert.Equal("Rapor.pdf", slots[1].Name);
        Assert.Equal(Enumerable.Range(0, slots.Count), slots.Select(s => s.EntryIndex));
        Assert.Equal(LauncherEntries.Ordered(tab).Select(e => e.Path ?? e.Action!.Id), slots.Select(s => s.Path ?? s.Action!.Id));
    }

    [Fact]
    public void Given_names_and_icons_win()
    {
        var ring = Ring();
        LauncherEntries.Insert(ring.Tabs[0], 0, LauncherEntry.Of(@"C:\x\Uygulama.lnk"));
        ItemLooks.SetName(ring, @"C:\X\uygulama.lnk", "Benim", "Uygulama");
        ItemLooks.SetIcon(ring, @"C:\x\Uygulama.lnk", "sym:Rocket24");
        ring.Tabs[0].Actions![0].Name = "  Notum  ";
        ring.Tabs[0].Actions![0].Icon = "sym:Games24";

        var slots = RingSlot.Build(ring, 0);
        Assert.Equal("Benim", slots[0].Name);
        Assert.Equal("sym:Rocket24", slots[0].IconRef);
        Assert.Equal("Notum", slots[1].Name);
        Assert.Equal("sym:Games24", slots[1].IconRef);
        Assert.Equal("Uygulama", RingSlot.Build(new WidgetConfig { Tabs = [new LauncherTab { Items = [@"C:\x\Uygulama.lnk"] }] }, 0)[0].Name);
    }

    [Fact]
    public void Unknown_disabled_and_invalid_items_are_dimmed()
    {
        var ring = new WidgetConfig
        {
            Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring,
            Tabs =
            [
                new LauncherTab
                {
                    Items = [],
                    Actions =
                    [
                        new() { Type = "macro" },                                                     // daha yeni sürümün türü
                        new() { Type = RingActionTypes.Text, Text = "x", Disabled = true },           // onaylanmamış içe aktarma
                        new() { Type = RingActionTypes.Url, Url = @"C:\Windows\notepad.exe" },        // adres değil
                        new() { Type = RingActionTypes.App, App = "organize-now" },                   // tanınmayan eylem
                        new() { Type = RingActionTypes.Url, Url = "https://www.example.com/a" },
                    ],
                },
            ],
        };
        var slots = RingSlot.Build(ring, 0);
        Assert.Equal(new[] { true, true, true, true, false }, slots.Select(s => s.Dimmed));
        Assert.Equal(SymbolRegular.QuestionCircle24, slots[0].Glyph);
        Assert.Equal("Bilinmeyen öğe", slots[0].Name);
        Assert.Equal("example.com", slots[4].Name);
    }

    [Theory]
    [InlineData(RingActionTypes.Url, "https://www.github.com/kando", "github.com")]
    [InlineData(RingActionTypes.Url, "mailto:ali@example.com", "ali@example.com")]
    [InlineData(RingActionTypes.Url, "javascript:alert(1)", "Web adresi")]
    [InlineData(RingActionTypes.Command, "\"C:\\Program Files\\App\\app.exe\"", "app")]
    [InlineData(RingActionTypes.Command, "notepad.exe", "notepad")]
    [InlineData(RingActionTypes.Command, "  ", "Komut")]
    public void Default_names_per_type(string type, string value, string expected)
    {
        var action = new LauncherAction { Type = type };
        if (type == RingActionTypes.Url) action.Url = value;
        else action.Command = value;
        Assert.Equal(expected, RingSlot.DefaultName(action));
    }

    [Fact]
    public void Default_names_for_keys_text_and_apps()
    {
        Assert.Equal("Ctrl+Shift+T, F5", RingSlot.DefaultName(new LauncherAction { Type = RingActionTypes.Keys, Keys = ["Ctrl+Shift+T", "F5"] }));
        Assert.Equal("Tuş kısayolu", RingSlot.DefaultName(new LauncherAction { Type = RingActionTypes.Keys }));
        Assert.Equal("Merhaba", RingSlot.DefaultName(new LauncherAction { Type = RingActionTypes.Text, Text = "\n  Merhaba \r\ndünya" }));
        Assert.Equal(new string('a', 31) + "…", RingSlot.DefaultName(new LauncherAction { Type = RingActionTypes.Text, Text = new string('a', 50) }));
        Assert.Equal("Metin", RingSlot.DefaultName(new LauncherAction { Type = RingActionTypes.Text }));
        Assert.Equal("NestDesk'i aç", RingSlot.DefaultName(RingActions.ForApp(RingAppIds.OpenApp)));
        Assert.Equal("NestDesk eylemi", RingSlot.DefaultName(new LauncherAction { Type = RingActionTypes.App, App = "x" }));

        using var _ = L.Use(Lang.En);
        Assert.Equal("Text", RingSlot.DefaultName(new LauncherAction { Type = RingActionTypes.Text }));
        Assert.Equal("Unknown item", RingSlot.DefaultName(new LauncherAction { Type = "macro" }));
        Assert.Equal("Sub-ring", RingSlot.SubRingName(new LauncherTab { Name = "" }));
    }

    [Fact]
    public void Every_default_glyph_is_drawable()
    {
        foreach (var glyph in RingSlot.AllGlyphs)
        {
            Assert.True(Enum.IsDefined(glyph), glyph.ToString());
            Assert.True(WidgetIcons.IsDrawable(glyph), $"{glyph} çizilemiyor");
        }
        // Her türün ve her NestDesk eyleminin kendi simgesi var (tanınmayan soru işareti).
        Assert.Equal(RingActionTypes.All.Count, RingActionTypes.All.Select(t => RingSlot.DefaultGlyph(new LauncherAction { Type = t })).Distinct().Count());
        Assert.Equal(RingAppIds.All.Count, RingAppIds.All.Select(RingSlot.AppGlyph).Distinct().Count());
    }

    [Theory]
    [InlineData(@"C:\Program Files\App\app.exe", @"C:\Program Files\App\app.exe")]
    [InlineData("\"C:\\Tools\\run.cmd\"", @"C:\Tools\run.cmd")]
    [InlineData("notepad.exe", null)]                  // tam yol değil: türün simgesi
    [InlineData(@"\\sunucu\paylaşım\a.exe", null)]     // ağ yolu: simge için bile sorulmaz
    [InlineData(@"C:\Belgeler\rapor.pdf", null)]
    public void Program_icon_only_for_a_full_program_path(string command, string? expected) =>
        Assert.Equal(expected, RingSlot.ProgramPath(command));
}
