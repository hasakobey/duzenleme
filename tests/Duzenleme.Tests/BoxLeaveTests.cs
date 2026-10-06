using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>
/// "Widget'lara koyduğum öğeler masaüstünden kalksın" (2.2, tam hâli): widget'ta duran masaüstü öğesi masaüstünden kalkar,
/// widget'ta değilse masaüstünde durur. Kutu ve halka öğelerinin yanında klasör bölmesinin gösterdiği masaüstü klasörü de
/// sayılır (bölme yeni yolu portal olarak gösterir); masaüstü bölmeleri (Klasörler, Kısayollar, Dosyalar, Tümü) sayılmaz.
/// Otomatik taşıma kuralının klasörü ve halka komutunun yolu masaüstünde kalır. Elle "Masaüstünden kaldır" / "Masaüstüne geri
/// koy" anahtar kapalıyken de çalışır; anahtarı kapatmak hepsini geri koyar. Sahte diskle (<see cref="FakeBoxDisk"/>).
/// </summary>
public sealed class BoxLeaveTests : IDisposable
{
    private const string Desktop = @"C:\Users\ali\Desktop";
    private const string Public = @"C:\Users\Public\Desktop";
    private const string Root = @"C:\Users\ali\NestDesk";

    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-box-leave-" + Guid.NewGuid().ToString("N"))).FullName;
    private readonly FakeBoxDisk _disk = new();
    private readonly BoxMoveLog _log;
    private readonly BoxEngine _engine;

    public BoxLeaveTests()
    {
        _disk.AdminOnly.Add(Public);
        _disk.Folders.Add(@"C:\Users\ali");
        _disk.Folders.Add(Desktop);
        _log = new BoxMoveLog(Path.Combine(_dir, "box-moves.json"));
        _engine = new BoxEngine(_disk, _log);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    private static WidgetConfig Panel(string folder, string? id = null) =>
        new() { Id = id ?? Guid.NewGuid().ToString("N"), Kind = WidgetKind.Fence, Filter = DesktopFilter.None, FolderName = folder };

    private static WidgetConfig Box(params string[] items)
    {
        var box = new WidgetConfig { Id = Guid.NewGuid().ToString("N"), Kind = WidgetKind.Launcher };
        box.Tabs.Add(new LauncherTab { Name = "Kutu", Items = [.. items] });
        return box;
    }

    private static ReturnRequest Request(bool interactive = true, bool reclaim = true) =>
        new([Desktop, Public], Desktop, [Public], interactive, reclaim);

    private void Folder(string path, params string[] files)
    {
        _disk.Folders.Add(path);
        foreach (var file in files) _disk.File(Path.Combine(path, file));
    }

    // ---------------------------------------------------------------- hangi widget neyi tutar

    [Fact]
    public void Folder_panels_hold_their_folder_and_desktop_panels_hold_nothing()
    {
        var settings = new AppSettings();
        var panel = Panel("Projeler");
        var portal = Panel(@"D:\Arşiv");
        var all = new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.All };
        var folders = new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.Folders, FolderName = "eski" };
        var box = Box($@"{Desktop}\rapor.pdf");
        settings.Widgets.AddRange([panel, portal, all, folders, box]);
        settings.Hub = Box($@"{Desktop}\hub.lnk");
        settings.Hub.Variant = WidgetVariants.Ring;

        Assert.True(Launchers.IsFolderPanel(panel));
        Assert.True(Launchers.IsFolderPanel(portal));
        Assert.False(Launchers.IsFolderPanel(all));
        Assert.False(Launchers.IsFolderPanel(folders));
        Assert.Equal($@"{Desktop}\Projeler", Launchers.PanelPath(panel, Desktop));
        Assert.Equal(@"D:\Arşiv", Launchers.PanelPath(portal, Desktop));
        Assert.Null(Launchers.PanelPath(box, Desktop));

        // Kutular, NestDesk halkası (merkez halka da) ve klasör bölmeleri.
        Assert.Equal([box.Id, settings.Hub.Id, panel.Id, portal.Id], Launchers.Holders(settings).Select(w => w.Id));
        Assert.Same(panel, Launchers.FindHolder(settings, panel.Id));
        Assert.Null(Launchers.FindHolder(settings, all.Id));
        Assert.Equal(new HashSet<string>([$@"{Desktop}\rapor.pdf", $@"{Desktop}\hub.lnk", $@"{Desktop}\Projeler", @"D:\Arşiv"]),
            Launchers.Held(settings, Desktop));
        // Kurallardan korunan ve bölme kipinde masaüstü bölmelerinin göstermedikleri: yalnızca masaüstündekiler.
        Assert.Equal(new HashSet<string>([$@"{Desktop}\rapor.pdf", $@"{Desktop}\hub.lnk", $@"{Desktop}\Projeler"], StringComparer.OrdinalIgnoreCase),
            Launchers.HeldDesktopPaths(settings, [Desktop, Public]));
    }

    [Fact]
    public void Panel_box_folder_is_named_after_its_folder()
    {
        Assert.Equal("Projeler", BoxPlan.FolderTitleOf(Panel("Projeler")));
        Assert.Equal("Arşiv", BoxPlan.FolderTitleOf(Panel(@"D:\Arşiv")));
        Assert.Equal("İşler", BoxPlan.FolderTitleOf(new WidgetConfig { Kind = WidgetKind.Fence, FolderName = "Projeler", Title = "İşler" }));
    }

    // ---------------------------------------------------------------- yolların izlenmesi

    [Fact]
    public void Panel_follows_its_folder_off_the_desktop_and_back()
    {
        var settings = new AppSettings();
        var panel = Panel("projeler"); // ad Türkçe harflerden ve büyük/küçük harften bağımsız eşleşir
        panel.HiddenItems.Add($@"{Desktop}\Projeler\gizli.txt");
        panel.ItemOrder = [$@"{Desktop}\Projeler\b.txt", $@"{Desktop}\Projeler\a.txt"];
        var box = Box($@"{Desktop}\Projeler\plan.docx", $@"{Desktop}\rapor.pdf");
        var other = Panel("Başka");
        settings.Widgets.AddRange([panel, box, other]);
        var moved = $@"{Root}\Projeler\Projeler";

        Assert.True(Launchers.ReplaceHeld(settings, [($@"{Desktop}\Projeler", moved, true)], Desktop));

        // Bölme yeni yolu portal olarak gösterir; içindeki yolları gösterenler de izler; başka bölme ve dosya değişmez.
        Assert.Equal(moved, panel.FolderName);
        Assert.True(WidgetVariants.IsPortal(panel));
        Assert.Equal([$@"{moved}\gizli.txt"], panel.HiddenItems);
        Assert.Equal([$@"{moved}\b.txt", $@"{moved}\a.txt"], panel.ItemOrder);
        Assert.Equal([$@"{moved}\plan.docx", $@"{Desktop}\rapor.pdf"], box.Tabs[0].Items);
        Assert.Equal("Başka", other.FolderName);

        // Masaüstüne dönünce (adı alınmışsa "(1)") bölme yine yalnızca adını tutar.
        Assert.True(Launchers.ReplaceHeld(settings, [(moved, $@"{Desktop}\Projeler (1)", true)], Desktop));
        Assert.Equal("Projeler (1)", panel.FolderName);
        Assert.False(WidgetVariants.IsPortal(panel));
        Assert.Equal([$@"{Desktop}\Projeler (1)\plan.docx", $@"{Desktop}\rapor.pdf"], box.Tabs[0].Items);
        Assert.False(Launchers.ReplaceHeld(settings, [(moved, $@"{Desktop}\Projeler (1)", true)], Desktop));
    }

    // ---------------------------------------------------------------- taşıma, geri koyma (sahte disk)

    [Fact]
    public void Panel_folder_leaves_with_its_contents_and_returns_when_the_panel_is_removed()
    {
        Folder($@"{Desktop}\Projeler", "plan.docx", "notlar.txt");
        var settings = new AppSettings();
        var panel = Panel("Projeler");
        settings.Widgets.Add(panel);
        var folder = $@"{Root}\Projeler";

        var steps = BoxPlan.PlanClaims([new ClaimInput(panel.Id, folder, [.. Launchers.HeldBy(panel, Desktop)])], _disk.Attributes,
            new BoxContext(Desktop, [Public], IncludePublic: false), _disk.Exists);
        var outcome = Assert.Single(_engine.Claim(steps, allowElevation: true));
        Assert.True(outcome.Moved);
        Assert.Equal($@"{folder}\Projeler", outcome.Current);
        Assert.False(_disk.Exists($@"{Desktop}\Projeler"));
        Assert.True(_disk.Exists($@"{folder}\Projeler\plan.docx"));
        Launchers.ReplaceHeld(settings, [(outcome.Step.Original, outcome.Current!, true)], Desktop);
        Assert.Equal($@"{folder}\Projeler", panel.FolderName);

        // Bölme durdukça kayıt açık kalır (uzlaştırma geri koymaz).
        var kept = BoxPlan.Reconcile(_log.Snapshot(), Launchers.Held(settings, Desktop), _disk.Exists);
        Assert.Empty(kept.Return);
        Assert.Empty(kept.Close);

        // Bölme kaldırılınca klasör içindekilerle masaüstüne döner.
        settings.Widgets.Remove(panel);
        var plan = BoxPlan.Reconcile(_log.Snapshot(), Launchers.Held(settings, Desktop), _disk.Exists);
        var back = Assert.Single(_engine.Return([Assert.Single(plan.Return)], Request()));
        Assert.Equal(ReturnState.Restored, back.State);
        Assert.Equal($@"{Desktop}\Projeler", back.Path);
        Assert.True(_disk.Exists($@"{Desktop}\Projeler\notlar.txt"));
        Assert.Equal(0, _log.ActiveCount);
    }

    [Fact]
    public void Removed_panel_coming_back_takes_its_folder_off_the_desktop_again()
    {
        Folder($@"{Desktop}\Projeler", "plan.docx");
        var settings = new AppSettings();
        var panel = Panel("Projeler");
        settings.Widgets.Add(panel);
        var steps = BoxPlan.PlanClaims([new ClaimInput(panel.Id, $@"{Root}\Projeler", [$@"{Desktop}\Projeler"])], _disk.Attributes,
            new BoxContext(Desktop, [], false), _disk.Exists);
        var current = _engine.Claim(steps, allowElevation: false)[0].Current!;
        Launchers.ReplaceHeld(settings, [($@"{Desktop}\Projeler", current, true)], Desktop);
        var copy = panel.Clone();

        // Kaldırıldı: döner (kutu gibi "yeniden taşınsın" işaretiyle).
        settings.Widgets.Clear();
        _engine.Return(BoxPlan.Reconcile(_log.Snapshot(), Launchers.Held(settings, Desktop), _disk.Exists).Return, Request(reclaim: true));
        Assert.True(_disk.Exists($@"{Desktop}\Projeler"));

        // "Geri al": bölme NestDesk yolunu gösterir; uzlaştırma onu masaüstündeki klasöre bağlar ve yeniden taşınmasını ister.
        settings.Widgets.Add(copy);
        var plan = BoxPlan.Reconcile(_log.Snapshot(), Launchers.Held(settings, Desktop), _disk.Exists);
        var remap = Assert.Single(plan.Remap);
        Assert.Equal((current, $@"{Desktop}\Projeler", true), remap);
        Launchers.ReplaceHeld(settings, [(remap.From, remap.To, true)], Desktop);
        Assert.Equal("Projeler", copy.FolderName);
        var batches = BoxPlan.ReclaimBatches([remap.To], Launchers.Holders(settings).ToList(), _log.Snapshot(), w => Launchers.HeldBy(w, Desktop));
        Assert.Equal(copy.Id, Assert.Single(batches).BoxId);
    }

    [Fact]
    public void Rule_and_command_folders_stay_on_the_desktop()
    {
        Folder($@"{Desktop}\PDF", "a.pdf");
        Folder($@"{Desktop}\Araçlar", "derle.cmd");
        Folder($@"{Desktop}\Projeler");
        var rules = new[] { new Rule { TargetFolder = "PDF" } };
        var ring = Box();
        ring.Variant = WidgetVariants.Ring;
        ring.Tabs[0].Actions = [new LauncherAction { Type = "command", Command = $@"{Desktop}\Araçlar\derle.cmd" }];
        var settings = new AppSettings { Rules = [.. rules] };
        settings.Widgets.Add(ring);
        var context = new BoxContext(Desktop, [], false, BoxPlan.KeptBy(settings.Rules.Select(r => r.TargetFolder), Launchers.CommandTexts(settings), Desktop));

        var steps = BoxPlan.PlanClaims([new ClaimInput("p", $@"{Root}\X", [$@"{Desktop}\PDF", $@"{Desktop}\Araçlar", $@"{Desktop}\Projeler"])],
            _disk.Attributes, context, _disk.Exists);

        Assert.Equal([BoxLinkReason.UsedFolder, BoxLinkReason.UsedFolder, BoxLinkReason.None], steps.Select(s => s.Reason));
        var outcomes = _engine.Claim(steps, allowElevation: false);
        Assert.Equal([false, false, true], outcomes.Select(o => o.Moved));
        Assert.True(_disk.Exists($@"{Desktop}\PDF\a.pdf"));
        // Klasörü kalan öğe yeniden denenmez (kural kaldırılınca yeniden eklemekle ya da "Masaüstünden kaldır"la kalkar).
        Assert.Null(BoxPlan.UpdateRetry(null, outcomes));
    }

    [Fact]
    public void A_folder_is_never_moved_into_itself()
    {
        // Masaüstü bir sürücünün köküyse NestDesk klasörü masaüstündeki bir klasörün içinde olabilir.
        _disk.Folders.Add(@"D:\");
        Folder(@"D:\Users");
        var steps = BoxPlan.PlanClaims([new ClaimInput("k", @"D:\Users\ali\NestDesk\Kutu", [@"D:\Users"])], _disk.Attributes,
            new BoxContext(@"D:\", [], false), _disk.Exists);
        Assert.Equal(BoxLinkReason.Protected, Assert.Single(steps).Reason);
    }

    [Fact]
    public void Open_file_inside_the_folder_is_retried_later()
    {
        Folder($@"{Desktop}\Projeler", "acik.docx");
        _disk.Locked.Add($@"{Desktop}\Projeler\acik.docx");
        var steps = BoxPlan.PlanClaims([new ClaimInput("p", $@"{Root}\Projeler", [$@"{Desktop}\Projeler"])], _disk.Attributes,
            new BoxContext(Desktop, [], false), _disk.Exists);

        var outcome = Assert.Single(_engine.Claim(steps, allowElevation: false));

        Assert.Equal(BoxFailure.Locked, outcome.Failure);
        Assert.True(_disk.Exists($@"{Desktop}\Projeler\acik.docx"));
        Assert.Equal(0, _log.ActiveCount); // kayıt silindi: hiçbir şey taşınmadı
        Assert.Equal([$@"{Desktop}\Projeler"], BoxPlan.UpdateRetry(null, [outcome]));
    }

    [Fact]
    public void Uninstall_returns_moved_folders_too()
    {
        Folder($@"{Desktop}\Projeler", "plan.docx");
        _disk.File($@"{Desktop}\rapor.pdf");
        var steps = BoxPlan.PlanClaims([new ClaimInput("p", $@"{Root}\Projeler", [$@"{Desktop}\Projeler", $@"{Desktop}\rapor.pdf"])],
            _disk.Attributes, new BoxContext(Desktop, [], false), _disk.Exists);
        Assert.All(_engine.Claim(steps, allowElevation: false), o => Assert.True(o.Moved));

        var plan = BoxPlan.PlanUninstallReturns(_log.Snapshot(), [Desktop], Desktop, quiet: true);
        Assert.Equal(2, plan.InPlace.Count);
        var outcomes = _engine.Return(plan.InPlace, new ReturnRequest([Desktop], Desktop, [], Interactive: false, Reclaim: false));

        Assert.All(outcomes, o => Assert.Equal(ReturnState.Restored, o.State));
        Assert.True(_disk.Exists($@"{Desktop}\Projeler\plan.docx"));
        Assert.True(_disk.Exists($@"{Desktop}\rapor.pdf"));
    }

    // ---------------------------------------------------------------- elle ve anahtar (kaynak denetimi)

    /// <summary>
    /// Kullanıcı modeli: anahtar açıkken widget'a konan masaüstü öğesi kalkar; kapalıyken öğe tek tek kaldırılabilir ve geri
    /// konabilir (kayıt aynı); anahtarı kapatmak hepsini (elle kaldırılanları da) geri koyar. Bu kararlar BoxMover'da (arayüz)
    /// olduğu için kaynaktan denetlenir.
    /// </summary>
    [Fact]
    public void Manual_leave_and_put_back_work_with_the_switch_off_and_turning_off_returns_everything()
    {
        var mover = Source("Widgets", "BoxMover.cs");
        // Elle kaldırma anahtara bakmaz (yalnızca bölme kipi + anahtar açıkken öğe zaten gizli).
        Assert.Matches(@"public static void LeaveDesktop\([^)]*\)\s*\{\s*if \(HidesFromFences\) return;", mover);
        Assert.Contains("includePublic: asAdmin ? true : null, manual: true)", mover);
        Assert.Contains("if (!MovesFiles && !manual)", mover);
        // "Masaüstüne geri koy"un "Geri al"ı yine elle kaldırır.
        Assert.Matches(@"public static void PutBack\(string path\)[\s\S]*?undo: back => ReclaimPaths\(back, manual: true\)", mover);
        // Uzlaştırma ve taşıma klasör bölmelerini de görür.
        Assert.Contains("var referenced = Launchers.Held(settings, Desktop);", mover);
        Assert.Contains("Launchers.FindHolder(settings, batch.BoxId)", mover);
        Assert.Contains("Launchers.ReplaceHeld(", mover);

        // Anahtarı kapatmak hepsini geri koyar ("Geri al" anahtarı yeniden açar ve yeniden taşır).
        var modes = Source("Views", "DesktopModes.cs");
        Assert.Matches(@"if \(BoxMover\.MovedCount > 0\)\s*BoxMover\.ReturnAll\(", modes);

        // Yeni klasör bölmesi ve kaynağı değişen bölme tek kapıdan.
        Assert.Contains("BoxMover.PanelShows(config, opened.CenterPoint)", Source("Widgets", "WidgetManager.cs"));
        var fence = Source("Widgets", "FenceView.xaml.cs");
        Assert.Contains("BoxMover.PanelShows(_config, DialogPoint)", fence);
        Assert.Contains("\"Editor.Fence.LeaveDesktop\"", fence);
        Assert.Contains("\"Editor.Fence.PutBack\"", fence);
        Assert.Contains("BoxMover.Changed += OnBoxChanged;", fence);
        Assert.Contains("BoxMover.Changed -= OnBoxChanged;", fence);
    }

    [Fact]
    public void Switch_is_on_the_first_settings_view_with_the_new_name()
    {
        var xaml = Source("Views", "SettingsPage.xaml");
        var main = xaml[xaml.IndexOf("x:Name=\"MainView\"", StringComparison.Ordinal)..xaml.IndexOf("x:Name=\"AdvancedView\"", StringComparison.Ordinal)];
        Assert.Contains("AutomationProperties.AutomationId=\"Settings.BoxLeaves\"", main);
        Assert.Contains("{l:T 'Widget\\'lara koyduğum öğeler masaüstünden kalksın'}", main);
        Assert.DoesNotContain("Kutuya koyduğum simgeler", xaml);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(xaml, "Settings.BoxLeaves\""));

        using var _ = L.Use(Lang.En);
        Assert.Equal("Items I put in widgets leave the desktop", L.T("Widget'lara koyduğum öğeler masaüstünden kalksın"));
    }

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine([RepoRoot(), "src", "Duzenleme", .. parts]));

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
