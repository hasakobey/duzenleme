using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>
/// "Kutuya koyduğum simgeler masaüstünden kalksın": ilk soru ve herkesin masaüstü sorusu ne zaman sorulur, öğe menüsünde hangi
/// satırlar var (kutu ve halka aynı, tek kaldırma fiili, istisna durumunda "Masaüstünden kaldır", bulunamayan öğede geri koyma
/// yok), bırakma kaplaması ne der. Saf (<see cref="BoxChoices"/>); diske dokunmaz.
/// </summary>
public class BoxChoicesTests
{
    private const string Desktop = @"C:\Users\ali\Desktop";
    private const string Public = @"C:\Users\Public\Desktop";

    private static readonly BoxContext Move = new(Desktop, [Public], IncludePublic: false);

    private static readonly Dictionary<string, FileAttributes> Disk = new(StringComparer.OrdinalIgnoreCase)
    {
        [$@"{Desktop}\rapor.pdf"] = FileAttributes.Normal,
        [$@"{Desktop}\Proje"] = FileAttributes.Directory,
        [$@"{Desktop}\gizli.txt"] = FileAttributes.Hidden,
        [$@"{Desktop}\desktop.ini"] = FileAttributes.Hidden | FileAttributes.System,
        [$@"{Public}\Edge.lnk"] = FileAttributes.Normal,
    };

    private static FileAttributes? Attributes(string path) => Disk.TryGetValue(path, out var a) ? a : null;

    // ---------------------------------------------------------------- ilk soru

    [Fact]
    public void Switch_question_is_asked_once_for_a_movable_desktop_icon()
    {
        Assert.True(BoxChoices.AskSwitch(asked: false, on: false, [$@"{Desktop}\rapor.pdf"], Attributes, Move));
        // Sorulduysa (iki yanıt da yazar) ya da seçenek zaten açıksa bir daha sorulmaz.
        Assert.False(BoxChoices.AskSwitch(asked: true, on: false, [$@"{Desktop}\rapor.pdf"], Attributes, Move));
        Assert.False(BoxChoices.AskSwitch(asked: false, on: true, [$@"{Desktop}\rapor.pdf"], Attributes, Move));
    }

    [Fact]
    public void Switch_question_ignores_folders_protected_items_and_paths_off_the_desktop()
    {
        Assert.False(BoxChoices.AskSwitch(false, false, [$@"{Desktop}\Proje"], Attributes, Move));
        Assert.False(BoxChoices.AskSwitch(false, false, [$@"{Desktop}\gizli.txt", $@"{Desktop}\desktop.ini"], Attributes, Move));
        Assert.False(BoxChoices.AskSwitch(false, false, [@"C:\Program Files\App\app.exe", StartApps.Prefix + "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"],
            Attributes, Move));
        // Herkesin masaüstündeki öğe de sorulur (ortak masaüstü izni bu soruda sayılmaz; o ayrıca sorulur).
        Assert.True(BoxChoices.AskSwitch(false, false, [$@"{Desktop}\Proje", $@"{Public}\Edge.lnk"], Attributes, Move));
    }

    [Fact]
    public void Asked_counts_the_switch_itself()
    {
        Assert.False(BoxChoices.Asked(new AppSettings()));
        Assert.True(BoxChoices.Asked(new AppSettings { BoxLeaveAsked = true }));
        // 2.1'den açık gelen ya da Ayarlar'dan açılan seçenek sorulmuş sayılır.
        Assert.True(BoxChoices.Asked(new AppSettings { BoxItemsLeaveDesktop = true }));
    }

    [Fact]
    public void Public_question_is_only_for_move_mode_outside_the_store_and_once()
    {
        var publicOnly = BoxChoices.PublicOnly([$@"{Public}\Edge.lnk", $@"{Desktop}\rapor.pdf"], Attributes, Move);
        Assert.Equal([$@"{Public}\Edge.lnk"], publicOnly);

        Assert.True(BoxChoices.AskPublic(movesFiles: true, publicAsked: false, includePublic: false, packaged: false, publicOnly));
        Assert.False(BoxChoices.AskPublic(movesFiles: false, false, false, false, publicOnly));   // bölme kipi: dosyaya dokunulmaz
        Assert.False(BoxChoices.AskPublic(true, publicAsked: true, false, false, publicOnly));
        Assert.False(BoxChoices.AskPublic(true, false, includePublic: true, false, publicOnly));
        Assert.False(BoxChoices.AskPublic(true, false, false, packaged: true, publicOnly));       // Store sürümü hiç taşımaz
        Assert.False(BoxChoices.AskPublic(true, false, false, false, []));
    }

    // ---------------------------------------------------------------- öğe menüsü

    [Fact]
    public void Item_state_follows_the_switch_and_the_desktop()
    {
        Assert.Equal(BoxItemState.Moved, BoxChoices.StateOf(moved: true, missing: false, movesFiles: true, BoxLinkReason.NotOnDesktop, false));
        // Taşınmış ama bulunamayan öğe geri konamaz: geri koyma satırı yok.
        Assert.Equal(BoxItemState.Link, BoxChoices.StateOf(moved: true, missing: true, movesFiles: true, BoxLinkReason.None, false));
        Assert.Equal(BoxItemState.OnDesktop, BoxChoices.StateOf(false, false, movesFiles: true, BoxLinkReason.None, false));
        Assert.Equal(BoxItemState.Link, BoxChoices.StateOf(false, false, movesFiles: false, BoxLinkReason.None, false));
        Assert.Equal(BoxItemState.Link, BoxChoices.StateOf(false, false, true, BoxLinkReason.Folder, false));
        Assert.Equal(BoxItemState.Link, BoxChoices.StateOf(false, false, true, BoxLinkReason.Protected, false));
        Assert.Equal(BoxItemState.PublicNeedsAdmin, BoxChoices.StateOf(false, false, true, BoxLinkReason.PublicDesktop, packaged: false));
        Assert.Equal(BoxItemState.Link, BoxChoices.StateOf(false, false, true, BoxLinkReason.PublicDesktop, packaged: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_state_has_one_removal_verb_and_no_put_back_row(bool ring)
    {
        foreach (var state in Enum.GetValues<BoxItemState>())
        {
            var rows = BoxChoices.MenuRows(state, ring, heldElsewhere: false);
            Assert.Single(rows, r => r.Action == BoxMenuAction.Remove);
            Assert.Equal(BoxMenuAction.Remove, rows[0].Action);
            Assert.Equal(ring ? "Halkadan kaldır" : "Kutudan kaldır", rows[0].Label);
            Assert.DoesNotContain(rows, r => r.Label.Contains("geri koy", StringComparison.OrdinalIgnoreCase));
            Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.ToolTip)));
        }
    }

    [Fact]
    public void Exception_state_offers_leave_desktop_in_box_and_ring()
    {
        var box = BoxChoices.MenuRows(BoxItemState.OnDesktop, ring: false, heldElsewhere: false);
        var ring = BoxChoices.MenuRows(BoxItemState.OnDesktop, ring: true, heldElsewhere: false);
        Assert.Equal([BoxMenuAction.Remove, BoxMenuAction.LeaveDesktop], box.Select(r => r.Action));
        Assert.Equal([BoxMenuAction.Remove, BoxMenuAction.LeaveDesktop], ring.Select(r => r.Action));
        Assert.Equal("Masaüstünden kaldır", ring[1].Label);
        Assert.Contains("kutuda durur", box[1].ToolTip);
        Assert.Contains("halkada durur", ring[1].ToolTip);

        var admin = BoxChoices.MenuRows(BoxItemState.PublicNeedsAdmin, ring: false, heldElsewhere: false);
        Assert.Equal([BoxMenuAction.Remove, BoxMenuAction.LeaveDesktopAsAdmin], admin.Select(r => r.Action));

        Assert.Single(BoxChoices.MenuRows(BoxItemState.Moved, ring: true, heldElsewhere: false));
        Assert.Single(BoxChoices.MenuRows(BoxItemState.Link, ring: true, heldElsewhere: false));
    }

    [Fact]
    public void Removal_tooltip_says_whether_the_icon_goes_back()
    {
        Assert.Equal("Simge masaüstüne geri döner.", BoxChoices.MenuRows(BoxItemState.Moved, false, heldElsewhere: false)[0].ToolTip);
        Assert.Contains("yalnızca buradan kalkar", BoxChoices.MenuRows(BoxItemState.Moved, true, heldElsewhere: true)[0].ToolTip);
        Assert.Contains("dosyaya dokunulmaz", BoxChoices.MenuRows(BoxItemState.Link, false, false)[0].ToolTip);
        Assert.Contains("dosyaya dokunulmaz", BoxChoices.MenuRows(BoxItemState.Link, true, false)[0].ToolTip);
    }

    [Fact]
    public void Menu_rows_are_translated()
    {
        using var _ = L.Use(Lang.En);
        var rows = BoxChoices.MenuRows(BoxItemState.OnDesktop, ring: true, heldElsewhere: false);
        Assert.Equal("Remove from ring", rows[0].Label);
        Assert.Equal("Remove from desktop", rows[1].Label);
        Assert.Equal("Remove from box", BoxChoices.MenuRows(BoxItemState.Moved, ring: false, heldElsewhere: false)[0].Label);
    }

    // ---------------------------------------------------------------- bırakma kaplaması

    [Fact]
    public void Drop_text_depends_on_the_switch_and_the_mode()
    {
        string[] file = [$@"{Desktop}\rapor.pdf"];
        Assert.Equal(BoxDropEffect.None, BoxChoices.DropEffect(file, on: false, fencesManage: false, Move));
        Assert.Equal(BoxDropEffect.Moves, BoxChoices.DropEffect(file, on: true, fencesManage: false, Move));
        Assert.Equal(BoxDropEffect.Hides, BoxChoices.DropEffect(file, on: true, fencesManage: true, Move));
        // Masaüstünde olmayan, klasör ve izin verilmemiş herkesin masaüstü öğesi taşınmaz.
        Assert.Equal(BoxDropEffect.None, BoxChoices.DropEffect([@"D:\Belgeler\a.pdf"], true, false, Move));
        Assert.Equal(BoxDropEffect.None, BoxChoices.DropEffect([$@"{Desktop}\Proje"], true, false, Move,
            p => Attributes(p) is { } a && (a & FileAttributes.Directory) != 0));
        Assert.Equal(BoxDropEffect.None, BoxChoices.DropEffect([$@"{Public}\Edge.lnk"], true, false, Move));
        Assert.Equal(BoxDropEffect.Moves, BoxChoices.DropEffect([$@"{Public}\Edge.lnk"], true, false, Move with { IncludePublic = true }));
        // Bölme kipinde herkesin masaüstündekiler de bölmeden kalkar (dosyaya dokunulmaz).
        Assert.Equal(BoxDropEffect.Hides, BoxChoices.DropEffect([$@"{Public}\Edge.lnk"], true, true, Move));
    }
}
