using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Toplu taşıma planı (<see cref="BoxPlan.PlanClaims"/>, saf) ve gerçek dosyalarla bir iş: aynı öğe bir kez, hedef adlar iş
/// boyunca ayrılır, klasörler bağlantı kalır, masaüstünde olmayan yola bakılmaz; geçici klasörde hiçbir dosyanın üzerine
/// yazılmaz.
/// </summary>
public class BoxBatchTests
{
    private const string Desktop = @"C:\Users\ali\Desktop";
    private const string Public = @"C:\Users\Public\Desktop";
    private const string Box = @"C:\Users\ali\NestDesk\Kutu";
    private const string Ring = @"C:\Users\ali\NestDesk\Halka";

    private static readonly BoxContext Context = new(Desktop, [Public], IncludePublic: true);

    private static List<ClaimStep> Plan(FakeBoxDisk disk, BoxContext context, params ClaimInput[] inputs) =>
        BoxPlan.PlanClaims(inputs, disk.Attributes, context, disk.Exists);

    [Fact]
    public void Same_path_in_two_boxes_is_planned_once()
    {
        var disk = new FakeBoxDisk().File($@"{Desktop}\a.pdf").File($@"{Public}\Edge.lnk");

        var steps = Plan(disk, Context,
            new ClaimInput("kutu", Box, [$@"{Desktop}\a.pdf", $@"{Public}\Edge.lnk"]),
            new ClaimInput("halka", Ring, [$@"{Public}\EDGE.lnk", $@"{Desktop}\a.pdf"]));

        Assert.Equal(2, steps.Count);
        Assert.All(steps, s => Assert.Equal("kutu", s.BoxId));
        Assert.Equal([$@"{Box}\a.pdf", $@"{Box}\Edge.lnk"], steps.Select(s => s.Target));
        Assert.True(steps[1].OnPublic);
        Assert.False(steps[0].OnPublic);
    }

    [Fact]
    public void Targets_are_reserved_for_the_whole_batch()
    {
        // Kutu klasöründe "x.lnk" zaten var; işte üç ayrı "x.lnk" (kullanıcının, ortak, başka bir kutunun aynı klasörü).
        var disk = new FakeBoxDisk().File($@"{Box}\x.lnk").File($@"{Desktop}\x.lnk").File($@"{Public}\x.lnk");

        var steps = Plan(disk, Context, new ClaimInput("k", Box, [$@"{Public}\x.lnk", $@"{Desktop}\x.lnk"]));

        Assert.Equal([$@"{Box}\x (1).lnk", $@"{Box}\x (2).lnk"], steps.Select(s => s.Target));
    }

    [Fact]
    public void Folders_and_protected_items_are_links()
    {
        var disk = new FakeBoxDisk().File($@"{Desktop}\desktop.ini");
        disk.Folders.Add($@"{Desktop}\Projeler");

        var steps = Plan(disk, Context, new ClaimInput("k", Box, [$@"{Desktop}\Projeler", $@"{Desktop}\desktop.ini", $@"{Desktop}\yok.pdf"]));

        Assert.Equal([BoxLinkReason.Folder, BoxLinkReason.Protected, BoxLinkReason.Missing], steps.Select(s => s.Reason));
        Assert.All(steps, s => Assert.Null(s.Target));
    }

    /// <summary>Store sürümü ve izin verilmemiş ortak masaüstü: öğe yalnızca bağlantı olur.</summary>
    [Fact]
    public void Public_items_need_the_opt_in()
    {
        var disk = new FakeBoxDisk().File($@"{Public}\Edge.lnk");

        var step = Assert.Single(Plan(disk, Context with { IncludePublic = false }, new ClaimInput("k", Box, [$@"{Public}\Edge.lnk"])));

        Assert.Equal(BoxLinkReason.PublicDesktop, step.Reason);
        Assert.Null(step.Target);
    }

    [Fact]
    public void Paths_off_the_desktop_are_never_examined()
    {
        var disk = new FakeBoxDisk().File($@"{Desktop}\a.pdf");

        var steps = Plan(disk, Context, new ClaimInput("k", Box,
            [@"\\çevrimdışı\paylaşım\x.exe", @"C:\Program Files\y.exe", "::{645FF040-5081-101B-9F08-00AA002F954E}", $@"{Desktop}\a.pdf"]));

        Assert.Equal([$@"{Desktop}\a.pdf"], disk.AttributeCalls);
        Assert.Equal(3, steps.Count(s => s.Reason == BoxLinkReason.NotOnDesktop));
    }

    /// <summary>Gerçek dosyalarla (geçici klasör): ortak masaüstü öğesi önce, aynı adlı kullanıcı öğesi sonra; hiçbiri ezilmez.</summary>
    [Fact]
    public void Real_files_are_never_overwritten_in_a_mixed_batch()
    {
        using var t = new TempDesktop();
        var publicDesktop = Directory.CreateDirectory(Path.Combine(t.Root, "Public")).FullName;
        var root = BoxPlan.RootFor(t.Desktop, t.Root);
        var folder = Path.Combine(root, "Kutu");
        File.WriteAllText(Path.Combine(publicDesktop, "Chrome.lnk"), "ortak");
        var user = t.File("Chrome.lnk", "kullanıcı");
        var disk = new RealDiskWithAdminFolder(publicDesktop);
        var log = new BoxMoveLog(Path.Combine(t.Root, "box-moves.json"));
        var engine = new BoxEngine(disk, log);

        var steps = BoxPlan.PlanClaims([new ClaimInput("k", folder, [Path.Combine(publicDesktop, "Chrome.lnk"), user])], disk.Attributes,
            new BoxContext(t.Desktop, [publicDesktop], IncludePublic: true), disk.Exists);
        var outcomes = engine.Claim(steps, allowElevation: true);

        Assert.All(outcomes, o => Assert.True(o.Moved));
        Assert.Equal("ortak", File.ReadAllText(Path.Combine(folder, "Chrome.lnk")));
        Assert.Equal("kullanıcı", File.ReadAllText(Path.Combine(folder, "Chrome (1).lnk")));

        // Geri: ikisi de kendi masaüstüne, yine hiçbir şey ezilmeden; boşalan NestDesk klasörü silinir.
        var returned = engine.Return(log.Snapshot(), new ReturnRequest([t.Desktop, publicDesktop], t.Desktop, [publicDesktop], Interactive: true, Reclaim: false));
        Assert.All(returned, r => Assert.Equal(ReturnState.Restored, r.State));
        Assert.Equal("ortak", File.ReadAllText(Path.Combine(publicDesktop, "Chrome.lnk")));
        Assert.Equal("kullanıcı", File.ReadAllText(user));
        Assert.False(Directory.Exists(root));
        Assert.Equal(0, log.ActiveCount);
    }

    /// <summary>Gerçek disk; verilen klasöre düz taşıma izin hatası verir, "yönetici onaylı" toplu taşıma izinlidir (gerçek UAC yok).</summary>
    private sealed class RealDiskWithAdminFolder(string adminOnly) : IBoxDisk
    {
        public FileAttributes? Attributes(string path) => BoxFiles.Attributes(path);
        public bool Exists(string path) => BoxFiles.Exists(path);

        public void Move(string from, string to)
        {
            if (BoxPlan.IsDirectlyIn(from, [adminOnly]) || BoxPlan.IsDirectlyIn(to, [adminOnly])) throw new UnauthorizedAccessException("admin");
            BoxFiles.Move(from, to);
        }

        public void MoveElevated(IReadOnlyList<(string From, string To)> moves)
        {
            foreach (var (from, to) in moves) BoxFiles.Move(from, to);
        }

        public void RemoveIfEmpty(string folder, string root) => BoxFiles.RemoveIfEmpty(folder, root);
        public bool SameContent(string a, string b) => BoxFiles.SameContent(a, b);
        public void Recycle(string path) => File.Delete(path);
    }
}
