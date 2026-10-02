using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>
/// "Kutuya koyduğum simgeler masaüstünden kalksın"ın saf hesapları (<see cref="BoxPlan"/>): seçenek açılırken kaç simge kalkar
/// (aynı öğe bir kez, klasör ve herkesin masaüstü ayrı), bölme kipinde masaüstü bölmelerinin göstermedikleri (B5), kaldırınca
/// kaç simge döner (B13), dışarı sürüklenen öğe ne oldu (B9), kaldırırken neler geri konur ve "Bu klasör nedir.txt".
/// </summary>
public sealed class BoxUxPlanTests : IDisposable
{
    private const string Desktop = @"C:\Users\ali\Desktop";
    private const string Public = @"C:\Users\Public\Desktop";
    private const string Root = @"C:\Users\ali\NestDesk";

    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-box-ux-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    private static WidgetConfig Box(string id, params string[][] tabs) =>
        new() { Id = id, Kind = WidgetKind.Launcher, Tabs = [.. tabs.Select(items => new LauncherTab { Items = [.. items] })] };

    private static WidgetConfig Ring(string id, params string[] items) =>
        new() { Id = id, Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring, Tabs = [new LauncherTab { Items = [.. items] }] };

    // ---------------------------------------------------------------- seçenek açılırken

    [Fact]
    public void Counts_are_distinct_and_leave_out_public_folders_and_protected_items()
    {
        var attrs = new Dictionary<string, FileAttributes>(StringComparer.OrdinalIgnoreCase)
        {
            [$@"{Desktop}\a.pdf"] = FileAttributes.Normal,
            [$@"{Desktop}\b.lnk"] = FileAttributes.Normal,
            [$@"{Desktop}\Proje"] = FileAttributes.Directory,
            [$@"{Desktop}\gizli.txt"] = FileAttributes.Hidden,
            [$@"{Public}\Edge.lnk"] = FileAttributes.Normal,
        };
        // a.pdf iki kutuda (bir kez sayılır); masaüstünde olmayan ve bulunamayan sayılmaz.
        string[] paths = [$@"{Desktop}\a.pdf", $@"{Desktop}\A.PDF", $@"{Desktop}\b.lnk", $@"{Desktop}\Proje", $@"{Desktop}\gizli.txt",
            $@"{Public}\Edge.lnk", @"D:\Belgeler\c.pdf", $@"{Desktop}\yok.pdf"];
        FileAttributes? Attr(string p) => attrs.TryGetValue(p, out var a) ? a : null;

        var counts = BoxPlan.ClaimCandidates(paths, Attr, new BoxContext(Desktop, [Public], IncludePublic: false));
        Assert.Equal([$@"{Desktop}\a.pdf", $@"{Desktop}\b.lnk"], counts.Movable);
        Assert.Equal([$@"{Public}\Edge.lnk"], counts.PublicOnly);

        var withPublic = BoxPlan.ClaimCandidates(paths, Attr, new BoxContext(Desktop, [Public], IncludePublic: true));
        Assert.Equal(3, withPublic.Movable.Count);
        Assert.Empty(withPublic.PublicOnly);
    }

    [Fact]
    public void Counting_reads_attributes_only_for_desktop_paths()
    {
        var asked = new List<string>();
        BoxPlan.ClaimCandidates([@"\\nas\paylaşım\a.pdf", StartApps.Prefix + "App", $@"{Desktop}\b.pdf"],
            p => { asked.Add(p); return FileAttributes.Normal; }, new BoxContext(Desktop, [Public], false));
        Assert.Equal([$@"{Desktop}\b.pdf"], asked);
    }

    // ---------------------------------------------------------------- bölme kipi (B5)

    [Fact]
    public void Fences_mode_excludes_boxed_desktop_and_public_paths()
    {
        var box = Box("kutu", [$@"{Desktop}\a.pdf", $@"{Desktop}\Proje", @"D:\Belgeler\c.pdf"]);
        var ring = Ring("halka", $@"{Public}\Edge.lnk", StartApps.Prefix + "Chrome");
        var set = BoxPlan.FenceExclusions([box, ring], [Desktop, Public]);
        Assert.Equal(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { $@"{Desktop}\a.pdf", $@"{Desktop}\Proje", $@"{Public}\Edge.lnk" }, set);
    }

    [Fact]
    public void App_shortcuts_are_hidden_only_while_their_app_is_in_a_box()
    {
        var app = StartApps.Prefix + "Chrome";
        var box = Box("kutu", [app]);
        var shortcuts = new Dictionary<string, string> { [$@"{Public}\Google Chrome.lnk"] = app, [@"D:\x.lnk"] = app };
        Assert.Equal([$@"{Public}\Google Chrome.lnk"], BoxPlan.FenceExclusions([box], [Desktop, Public], shortcuts));

        // Uygulama kutudan çıkınca kısayolu bölmede yeniden görünür.
        box.Tabs[0].Items.Clear();
        Assert.Empty(BoxPlan.FenceExclusions([box], [Desktop, Public], shortcuts));
    }

    [Fact]
    public void Removing_from_box_reincludes_in_fences()
    {
        var box = Box("kutu", [$@"{Desktop}\a.pdf"]);
        Assert.Contains($@"{Desktop}\a.pdf", BoxPlan.FenceExclusions([box], [Desktop]));
        box.Tabs[0].Items.Clear();
        Assert.DoesNotContain($@"{Desktop}\a.pdf", BoxPlan.FenceExclusions([box], [Desktop]));
    }

    // ---------------------------------------------------------------- kaldırınca dönecekler (B13)

    [Fact]
    public void ReturningIf_counts_only_items_held_nowhere_else()
    {
        var moved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { $@"{Root}\Kutu\a.pdf", $@"{Root}\Kutu\b.pdf", $@"{Root}\Kutu\c.pdf" };
        var tab1 = new LauncherTab { Items = [$@"{Root}\Kutu\a.pdf", $@"{Root}\Kutu\b.pdf", $@"{Desktop}\d.pdf"] };
        var tab2 = new LauncherTab { Items = [$@"{Root}\Kutu\b.pdf", $@"{Root}\Kutu\c.pdf"] };
        var box = new WidgetConfig { Id = "kutu", Kind = WidgetKind.Launcher, Tabs = [tab1, tab2] };
        var ring = Ring("halka", $@"{Root}\Kutu\c.pdf");
        WidgetConfig[] all = [box, ring];

        // Bütün kutu: a ve b döner (c halkada da duruyor; d taşınmamış).
        Assert.Equal([$@"{Root}\Kutu\a.pdf", $@"{Root}\Kutu\b.pdf"], BoxPlan.ReturningIf(all, "kutu", null, null, moved.Contains).Order());
        // Yalnızca ilk sekme: b öbür sekmede de var, yalnızca a döner.
        Assert.Equal([$@"{Root}\Kutu\a.pdf"], BoxPlan.ReturningIf(all, "kutu", tab1, null, moved.Contains));
        // Tek öğe: b başka sekmede, c halkada da duruyor.
        Assert.Empty(BoxPlan.ReturningIf(all, "kutu", tab1, $@"{Root}\Kutu\b.pdf", moved.Contains));
        Assert.Empty(BoxPlan.ReturningIf(all, "kutu", tab2, $@"{Root}\Kutu\c.pdf", moved.Contains));
        Assert.Equal([$@"{Root}\Kutu\a.pdf"], BoxPlan.ReturningIf(all, "kutu", tab1, $@"{Root}\Kutu\A.PDF", moved.Contains));
    }

    [Fact]
    public void Held_elsewhere_sees_other_tabs_boxes_and_rings()
    {
        var box = Box("kutu", [$@"{Desktop}\a.pdf"], [$@"{Desktop}\b.pdf"]);
        var ring = Ring("halka", $@"{Desktop}\b.pdf");
        Assert.False(BoxPlan.HeldElsewhere([box, ring], $@"{Desktop}\a.pdf"));
        Assert.True(BoxPlan.HeldElsewhere([box, ring], $@"{Desktop}\B.pdf"));
        Assert.True(BoxPlan.HeldElsewhere([Box("k", [$@"{Desktop}\a.pdf"], [$@"{Desktop}\a.pdf"])], $@"{Desktop}\a.pdf"));
    }

    // ---------------------------------------------------------------- dışarı sürükleme (B9)

    [Fact]
    public void Dropped_on_desktop_returns_and_takes_out()
    {
        var record = new BoxMove { Original = $@"{Desktop}\Not.lnk", Current = $@"{Root}\Kutu\Not.lnk" };
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { $@"{Desktop}\Not.lnk" };
        Assert.Equal((DragOutResult.Returned, $@"{Desktop}\Not.lnk"), BoxPlan.AfterDragOut(record, files.Contains, [Desktop, Public]));

        // Herkesin masaüstünden gelen öğe kullanıcının masaüstüne bırakıldıysa orada bulunur.
        var shared = new BoxMove { Original = $@"{Public}\Edge.lnk", Current = $@"{Root}\Kutu\Edge.lnk" };
        files.Add($@"{Desktop}\Edge.lnk");
        Assert.Equal((DragOutResult.Returned, $@"{Desktop}\Edge.lnk"), BoxPlan.AfterDragOut(shared, files.Contains, [Desktop, Public]));
    }

    [Fact]
    public void Moved_elsewhere_leaves_box()
    {
        var record = new BoxMove { Original = $@"{Desktop}\a.pdf", Current = $@"{Root}\Kutu\a.pdf" };
        Assert.Equal((DragOutResult.LeftBox, (string?)null), BoxPlan.AfterDragOut(record, _ => false, [Desktop]));
    }

    [Fact]
    public void Copied_does_nothing()
    {
        var record = new BoxMove { Original = $@"{Desktop}\a.pdf", Current = $@"{Root}\Kutu\a.pdf" };
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { $@"{Root}\Kutu\a.pdf", $@"{Desktop}\a.pdf" };
        Assert.Equal((DragOutResult.Copied, (string?)null), BoxPlan.AfterDragOut(record, files.Contains, [Desktop]));
    }

    // ---------------------------------------------------------------- kaldırırken geri koyma

    [Fact]
    public void Every_active_record_returns_on_uninstall()
    {
        BoxMove[] records =
        [
            new() { Original = $@"{Desktop}\a.pdf", Current = $@"{Root}\Kutu\a.pdf" },
            new() { Original = $@"{Public}\Edge.lnk", Current = $@"{Root}\Kutu\Edge.lnk" },
            new() { Original = $@"{Desktop}\b.pdf", Current = $@"{Root}\Kutu\b.pdf", ReturnedTo = $@"{Desktop}\b.pdf" },
        ];
        var plan = BoxPlan.PlanUninstallReturns(records, [Desktop, Public], Desktop, quiet: false);
        Assert.Equal([records[0], records[1]], plan.InPlace);
        Assert.Empty(plan.ToUserDesktop);
    }

    [Fact]
    public void Quiet_puts_public_on_user_desktop()
    {
        BoxMove[] records =
        [
            new() { Original = $@"{Desktop}\a.pdf", Current = $@"{Root}\Kutu\a.pdf" },
            new() { Original = $@"{Public}\Edge.lnk", Current = $@"{Root}\Kutu\Edge.lnk" },
        ];
        var plan = BoxPlan.PlanUninstallReturns(records, [Desktop, Public], Desktop, quiet: true);
        Assert.Equal([records[0]], plan.InPlace);
        Assert.Equal([records[1]], plan.ToUserDesktop);
    }

    [Fact]
    public void Test_mode_touches_only_test_paths()
    {
        const string test = @"C:\Temp\nd\Desktop";
        const string testRoot = @"C:\Temp\nd\NestDesk";
        BoxMove[] records =
        [
            new() { Original = $@"{test}\a.pdf", Current = $@"{testRoot}\Kutu\a.pdf" },
            new() { Original = $@"{Desktop}\b.pdf", Current = $@"{Root}\Kutu\b.pdf" },     // gerçek masaüstü: asla
            new() { Original = $@"{Desktop}\c.pdf", Current = $@"{testRoot}\Kutu\c.pdf" }, // eski yeri test masaüstü değil
        ];
        var plan = BoxPlan.PlanUninstallReturns(records, [test], test, quiet: false, BoxPlan.UninstallScope(testRoot, [test]));
        Assert.Equal([records[0]], plan.InPlace);
    }

    [Fact]
    public void Uninstall_returns_files_and_removes_the_root_with_its_breadcrumb()
    {
        var desktop = Directory.CreateDirectory(Path.Combine(_dir, "Desktop")).FullName;
        var root = Path.Combine(_dir, "NestDesk");
        var folder = Directory.CreateDirectory(Path.Combine(root, "Kutu")).FullName;
        File.WriteAllText(Path.Combine(folder, "a.pdf"), "a");
        File.WriteAllText(Path.Combine(root, "Bu klasör nedir.txt"), "açıklama");
        var log = new BoxMoveLog(Path.Combine(_dir, "box-moves.json"));
        log.Add(new BoxMove { Original = Path.Combine(desktop, "a.pdf"), Current = Path.Combine(folder, "a.pdf") });

        var plan = BoxPlan.PlanUninstallReturns(log.Snapshot(), [desktop], desktop, quiet: true, BoxPlan.UninstallScope(root, [desktop]));
        var outcomes = new BoxEngine(new RealDisk(), log).Return(plan.InPlace, new ReturnRequest([desktop], desktop, [], Interactive: false, Reclaim: false));

        Assert.Equal(ReturnState.Restored, Assert.Single(outcomes).State);
        Assert.True(File.Exists(Path.Combine(desktop, "a.pdf")));
        Assert.False(Directory.Exists(root));   // açıklama dosyası kökün silinmesini engellemez
        Assert.Equal(0, log.ActiveCount);
    }

    // ---------------------------------------------------------------- açıklama dosyası

    [Fact]
    public void Breadcrumb_lists_this_roots_items_and_their_old_folders()
    {
        BoxMove[] records =
        [
            new() { Original = $@"{Desktop}\a.pdf", Current = $@"{Root}\Oyunlar\a.pdf" },
            new() { Original = $@"{Public}\Edge.lnk", Current = $@"{Root}\Halka menü\Edge.lnk" },
            new() { Original = $@"{Desktop}\b.pdf", Current = $@"{Root}\Oyunlar\b.pdf", ReturnedTo = $@"{Desktop}\b.pdf" },
            new() { Original = @"D:\Eski\Desktop\c.pdf", Current = @"D:\Eski\NestDesk\Kutu\c.pdf" },
        ];
        var text = BoxPlan.BreadcrumbText(records, Root)!;
        Assert.Contains(@"Oyunlar\a.pdf  ←  " + Desktop, text);
        Assert.Contains(@"Halka menü\Edge.lnk  ←  " + Public, text);
        Assert.DoesNotContain("b.pdf", text);   // geri konmuş
        Assert.DoesNotContain("c.pdf", text);   // başka kök
        Assert.Contains("NestDesk", text);
        Assert.Null(BoxPlan.BreadcrumbText([records[2]], Root));
    }

    [Fact]
    public void Breadcrumb_is_recognized_in_both_languages()
    {
        Assert.Equal("Bu klasör nedir.txt", BoxPlan.BreadcrumbName);
        using (L.Use(Lang.En)) Assert.Equal("What is this folder.txt", BoxPlan.BreadcrumbName);
        Assert.True(BoxPlan.IsBreadcrumb("bu klasör nedir.txt"));
        Assert.True(BoxPlan.IsBreadcrumb("What is this folder.txt"));
        Assert.False(BoxPlan.IsBreadcrumb("rapor.txt"));
    }

    [Fact]
    public void Empty_folder_cleanup_ignores_only_the_breadcrumb()
    {
        var root = Path.Combine(_dir, "NestDesk");
        var folder = Directory.CreateDirectory(Path.Combine(root, "Kutu")).FullName;
        var note = Path.Combine(root, "What is this folder.txt");
        File.WriteAllText(note, "x");
        var mine = Path.Combine(root, "benim.txt");
        File.WriteAllText(mine, "x");

        // Kullanıcının koyduğu dosya varken kök kalır.
        BoxFiles.RemoveIfEmpty(folder, root);
        Assert.False(Directory.Exists(folder));
        Assert.True(File.Exists(note));
        Assert.True(Directory.Exists(root));

        File.Delete(mine);
        BoxFiles.RemoveIfEmpty(root, root);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void OneDrive_desktop_is_recognized_by_its_path()
    {
        Assert.True(BoxPlan.IsOneDrivePath(@"C:\Users\ali\OneDrive\Desktop"));
        Assert.True(BoxPlan.IsOneDrivePath(@"C:\Users\ali\OneDrive - Contoso\Masaüstü\"));
        Assert.False(BoxPlan.IsOneDrivePath(@"C:\Users\ali\Desktop"));
        Assert.False(BoxPlan.IsOneDrivePath(@"C:\Users\OneDriveFan\Desktop"));
    }

    // ---------------------------------------------------------------- uygulamanın kısayolu kaydı

    [Fact]
    public void App_shortcut_claims_carry_their_app()
    {
        var disk = new FakeBoxDisk().File($@"{Desktop}\Chrome.lnk");
        var log = new BoxMoveLog(Path.Combine(_dir, "box-moves.json"));
        var app = StartApps.Prefix + "Chrome";
        var steps = BoxPlan.PlanClaims([new ClaimInput("kutu", $@"{Root}\Kutu", [$@"{Desktop}\Chrome.lnk"], app)], disk.Attributes,
            new BoxContext(Desktop, [], false), disk.Exists);
        Assert.Equal(app, Assert.Single(steps).ForItem);

        var outcome = Assert.Single(new BoxEngine(disk, log).Claim(steps, allowElevation: false));
        Assert.True(outcome.Moved);
        Assert.Equal(app, Assert.Single(log.Snapshot()).ForItem);

        // Uygulama bir kutuda durdukça kısayol geri dönmez; çıkınca döner.
        Assert.Empty(BoxPlan.Reconcile(log.Snapshot(), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { app }, disk.Exists).Return);
        Assert.Single(BoxPlan.Reconcile(log.Snapshot(), new HashSet<string>(), disk.Exists).Return);
    }

    /// <summary>Gerçek dosya sistemi (geçici klasörde; ortak masaüstü ve yönetici onayı yok).</summary>
    private sealed class RealDisk : IBoxDisk
    {
        public FileAttributes? Attributes(string path) => BoxFiles.Attributes(path);
        public bool Exists(string path) => BoxFiles.Exists(path);
        public void Move(string from, string to) => BoxFiles.Move(from, to);
        public void MoveElevated(IReadOnlyList<(string From, string To)> moves) => throw new OperationCanceledException();
        public void RemoveIfEmpty(string folder, string root) => BoxFiles.RemoveIfEmpty(folder, root);
        public bool SameContent(string a, string b) => BoxFiles.SameContent(a, b);
        public void Recycle(string path) => File.Delete(path);
    }
}
