using Duzenleme.Core;

namespace Duzenleme.Tests;

public class StarterFencesTests
{
    private static WidgetConfig Fence(DesktopFilter filter, string? folder = null) =>
        new() { Kind = WidgetKind.Fence, Filter = filter, FolderName = folder };

    [Fact]
    public void Empty_desktop_gets_kind_fences_then_existing_rule_folders_in_rule_order()
    {
        var plan = StarterFences.Plan([], Rule.Defaults(), ["Resimler", "Oyunlar", "PDF"]);

        Assert.Equal(
        [
            new StarterFence(DesktopFilter.Folders, null, "Klasörler"),
            new StarterFence(DesktopFilter.Shortcuts, null, "Kısayollar"),
            new StarterFence(DesktopFilter.Files, null, "Dosyalar"),
            new StarterFence(DesktopFilter.None, "PDF", "PDF"),
            new StarterFence(DesktopFilter.None, "Resimler", "Resimler"),
        ], plan);
    }

    [Fact]
    public void Existing_kind_fence_is_not_added_again()
    {
        var plan = StarterFences.Plan([Fence(DesktopFilter.Folders), new WidgetConfig { Kind = WidgetKind.Clock }], Rule.Defaults(), []);

        Assert.Equal([DesktopFilter.Shortcuts, DesktopFilter.Files], plan.Select(p => p.Filter));
    }

    [Fact]
    public void Folder_fence_matches_regardless_of_case_and_turkish_letters()
    {
        var plan = StarterFences.Plan([Fence(DesktopFilter.None, "pdf"), Fence(DesktopFilter.None, "arsivler")], Rule.Defaults(),
            ["PDF", "Arşivler"]);

        Assert.DoesNotContain(plan, p => p.Folder is not null);
    }

    [Fact]
    public void Folder_uses_desktop_spelling()
    {
        var plan = StarterFences.Plan([], Rule.Defaults(), ["arsivler"]);

        Assert.Contains(new StarterFence(DesktopFilter.None, "arsivler", "arsivler"), plan);
    }

    [Fact]
    public void Disabled_rule_folder_is_not_added()
    {
        var rules = Rule.Defaults();
        rules.First(r => r.TargetFolder == "PDF").Enabled = false;

        var plan = StarterFences.Plan([], rules, ["PDF"]);

        Assert.DoesNotContain(plan, p => p.Folder is not null);
    }

    [Fact]
    public void Two_rules_for_same_folder_give_one_fence()
    {
        List<Rule> rules =
        [
            new() { TargetFolder = "Belgeler", Extensions = ["docx"] },
            new() { TargetFolder = "belgeler", Extensions = ["txt"] },
        ];

        var plan = StarterFences.Plan([], rules, ["Belgeler"]);

        Assert.Single(plan, p => p.Folder is not null);
    }
}
