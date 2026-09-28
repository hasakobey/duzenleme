using Duzenleme.Core;
using Duzenleme.Views;

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
            Assert.Equal(choice.Icon, WidgetCatalog.IconFor(seed!));
            Assert.NotEqual(WidgetGroup.Fence, choice.Group);
        }
    }
}
