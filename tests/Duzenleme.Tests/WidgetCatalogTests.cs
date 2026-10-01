using Duzenleme.Core;
using Duzenleme.Views;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>Ekleme kataloğu: her araç kutucuğunun tohumu, tanıması ve simgesi tutarlı; karşılama yalnızca ilk beşi gösterir.</summary>
public class WidgetCatalogTests
{
    [Fact]
    public void Welcome_shows_only_the_original_five_tools()
    {
        Assert.Equal(new[] { "Clock", "Date", "Note", "Checklist", "Launcher" }, WidgetCatalog.WelcomeTools.Select(c => c.Key));
    }

    [Fact]
    public void Every_tool_has_a_seed_that_only_it_recognises_with_the_same_icon()
    {
        var tools = WidgetCatalog.Tools;
        Assert.Equal(tools.Count, tools.Select(t => t.Key).Distinct().Count());
        foreach (var choice in tools)
        {
            var seed = WidgetSeeds.Create(choice.Key);
            Assert.NotNull(seed);
            Assert.Single(tools, c => c.Matches!(seed!));
            Assert.True(choice.Matches!(seed!), choice.Key);
            Assert.Equal(choice.Icon, WidgetIcons.DefaultFor(seed!));
            Assert.Equal(choice.Icon, WidgetIcons.For(seed!));
            Assert.NotEqual(WidgetGroup.Fence, choice.Group);
        }
    }

    [Fact]
    public void Ring_menu_is_a_tool_of_its_own()
    {
        var ring = Assert.Single(WidgetCatalog.Tools, c => c.Key == WidgetSeeds.Ring);
        Assert.Equal("Halka menü", ring.Label);
        Assert.Equal(Wpf.Ui.Controls.SymbolRegular.DataPie24, ring.Icon);
        Assert.True(WidgetIcons.IsDrawable(ring.Icon));
        Assert.False(ring.ShowInWelcome);
        Assert.False(ring.AsksName);
        Assert.Equal(WidgetGroup.Tool, ring.Group);
        // Kısayol kutusu kutucuğu halkayı (aynı temel tür) kendisi saymaz; halka da kutuyu.
        var box = WidgetSeeds.Create(WidgetSeeds.Launcher)!;
        Assert.False(ring.Matches!(box));
        Assert.False(WidgetCatalog.Tools.Single(c => c.Key == WidgetSeeds.Launcher).Matches!(WidgetSeeds.Create(WidgetSeeds.Ring)!));
    }
}
