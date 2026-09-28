using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Uygulamanın kendi yaptığı yeniden adlandırmadan (bölmede F2) sonra: taşıma geçmişi, kutu kayıtları ve widget ayarları
/// yeni yolu izler (AppHost.NotePathRenamed bunları çağırır).
/// </summary>
public class RenameTests
{
    [Theory]
    [InlineData(@"C:\M\a.pdf", @"C:\M\a.pdf", @"C:\M\b.pdf", false, @"C:\M\b.pdf")]
    [InlineData(@"C:\M\A.PDF", @"C:\M\a.pdf", @"C:\M\b.pdf", false, @"C:\M\b.pdf")]
    [InlineData(@"C:\M\Proje\x.pdf", @"C:\M\Proje", @"C:\M\Proje 2", true, @"C:\M\Proje 2\x.pdf")]
    [InlineData(@"C:\M\Proje\", @"C:\M\Proje", @"C:\M\Yeni", true, @"C:\M\Yeni")]
    [InlineData(@"C:\M\Proje\x.pdf", @"C:\M\Proje", @"C:\M\Proje 2", false, null)]
    [InlineData(@"C:\M\Projeler\x.pdf", @"C:\M\Proje", @"C:\M\Proje 2", true, null)]
    [InlineData(@"C:\M\c.pdf", @"C:\M\a.pdf", @"C:\M\b.pdf", false, null)]
    public void Map_follows_the_item_and_everything_under_a_folder(string path, string from, string to, bool isDirectory, string? expected) =>
        Assert.Equal(expected, PathRenames.Map(path, from, to, isDirectory));

    [Fact]
    public void Rewrite_keeps_each_path_once()
    {
        var paths = new List<string> { @"C:\M\a.pdf", @"C:\M\b.pdf", @"C:\M\c.pdf" };
        Assert.True(PathRenames.Rewrite(paths, @"C:\M\a.pdf", @"C:\M\B.pdf", false));
        Assert.Equal([@"C:\M\B.pdf", @"C:\M\c.pdf"], paths);
        Assert.False(PathRenames.Rewrite(paths, @"C:\M\x.pdf", @"C:\M\y.pdf", false));
    }

    [Fact]
    public void Manual_order_and_folder_portals_follow_a_rename()
    {
        const string desktop = @"C:\Users\ali\Desktop";
        var sorted = new WidgetConfig
        {
            Kind = WidgetKind.Fence, Filter = DesktopFilter.Files, SortBy = FenceOrder.Manual,
            ItemOrder = [desktop + @"\b.txt", desktop + @"\eski.txt", desktop + @"\c.txt"],
        };
        var portal = WidgetSeeds.Portal(@"D:\Arşiv\Faturalar", null, "Faturalar");
        var other = WidgetSeeds.Portal(@"D:\Arşivler", null, "Arşivler");

        Assert.True(PathRenames.Apply([sorted], desktop + @"\eski.txt", desktop + @"\yeni.txt", false, desktop));
        Assert.Equal([desktop + @"\b.txt", desktop + @"\yeni.txt", desktop + @"\c.txt"], sorted.ItemOrder);

        Assert.True(PathRenames.Apply([portal, other], @"D:\Arşiv", @"D:\Eski arşiv", true, desktop));
        Assert.Equal(@"D:\Eski arşiv\Faturalar", portal.FolderName);
        Assert.Equal(@"D:\Arşivler", other.FolderName);
        Assert.True(WidgetVariants.IsPortal(portal));
    }

    [Fact]
    public void Widget_settings_follow_a_renamed_file()
    {
        const string desktop = @"C:\Users\ali\Desktop";
        var fence = new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.Files, HiddenItems = [desktop + @"\eski.txt"] };
        var box = new WidgetConfig
        {
            Kind = WidgetKind.Launcher,
            Tabs = [new LauncherTab { Items = [desktop + @"\eski.txt"] }, new LauncherTab { Items = [@"C:\Program Files\x.exe"] }],
        };
        ItemLooks.SetName(box, desktop + @"\eski.txt", "Notlarım", "eski.txt");

        Assert.True(PathRenames.Apply([fence, box], desktop + @"\eski.txt", desktop + @"\yeni.txt", false, desktop));

        Assert.Equal([desktop + @"\yeni.txt"], fence.HiddenItems);
        Assert.Equal([desktop + @"\yeni.txt"], box.Tabs[0].Items);
        Assert.Equal([@"C:\Program Files\x.exe"], box.Tabs[1].Items);
        Assert.Equal("Notlarım", ItemLooks.Get(box, desktop + @"\yeni.txt")!.Name);
    }

    [Fact]
    public void Folder_fence_follows_its_renamed_desktop_folder_but_not_other_folders()
    {
        const string desktop = @"C:\Users\ali\Desktop";
        var projects = new WidgetConfig { Kind = WidgetKind.Fence, FolderName = "Projeler" };
        var nested = new WidgetConfig { Kind = WidgetKind.Fence, FolderName = "Arşiv" };
        var files = new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.Folders, FolderName = "Projeler" };

        Assert.True(PathRenames.Apply([projects, nested, files], desktop + @"\PROJELER", desktop + @"\Projeler 2026", true, desktop));
        Assert.Equal("Projeler 2026", projects.FolderName);
        Assert.Equal("Projeler", files.FolderName); // masaüstü türü bölmesi klasör adına bağlı değil

        // Masaüstünde olmayan (ör. PDF klasörünün içindeki) "Arşiv" yeniden adlandırılınca bölme ona bağlanmaz.
        Assert.False(PathRenames.Apply([nested], desktop + @"\PDF\Arşiv", desktop + @"\PDF\Eski", true, desktop));
        Assert.Equal("Arşiv", nested.FolderName);
    }

    [Fact]
    public void Rules_can_follow_a_renamed_target_folder_as_a_new_list()
    {
        var rules = new List<Rule>
        {
            new() { TargetFolder = "PDF", Extensions = ["pdf"] },
            new() { TargetFolder = "Resimler", Extensions = ["png"], Enabled = false },
        };
        Assert.Null(PathRenames.RetargetRules(rules, "Belgeler", "Evraklar"));

        var moved = PathRenames.RetargetRules(rules, "pdf", "PDF Belgeler")!;
        Assert.NotSame(rules, moved);
        Assert.Equal(["PDF Belgeler", "Resimler"], moved.Select(r => r.TargetFolder));
        Assert.False(moved[1].Enabled);
        Assert.Equal("PDF", rules[0].TargetFolder); // izleyicinin okuduğu eski liste değişmez
        moved[0].Extensions.Add("x");
        Assert.Single(rules[0].Extensions);
    }

    [Fact]
    public void Undone_file_stays_undone_after_rename()
    {
        // "Geri alınan dosya bir daha otomatik taşınmaz": yeni adıyla da.
        using var t = new TempDesktop();
        t.Folder("PDF");
        var file = t.File("fatura.pdf");
        var entry = t.Organizer.Organize(file)!;
        t.Organizer.Undo(entry);
        Assert.True(File.Exists(file));

        var renamed = Path.Combine(t.Desktop, "Fatura Ekim.pdf");
        File.Move(file, renamed);
        Assert.True(t.Journal.NoteRename(file, renamed, isDirectory: false));

        Assert.True(t.Journal.WasUndone(renamed));
        Assert.False(t.Organizer.WouldMove(renamed));
        Assert.Null(t.Organizer.Organize(renamed));
        Assert.True(File.Exists(renamed));
        // Kararı hemen diske yazılır (çökmede de korunur).
        using var reloaded = new MoveJournal(Path.Combine(t.Root, "journal.json"));
        Assert.True(reloaded.WasUndone(renamed));
    }

    [Fact]
    public void Undo_after_renaming_the_moved_file_returns_it_with_the_new_name()
    {
        using var t = new TempDesktop();
        var pdf = t.Folder("PDF");
        var file = t.File("fatura.pdf");
        var entry = t.Organizer.Organize(file)!;

        // Kullanıcı PDF bölmesinde F2 ile yeniden adlandırdı.
        var renamed = Path.Combine(pdf, "Fatura Ekim.pdf");
        File.Move(entry.Destination, renamed);
        t.Journal.NoteRename(entry.Destination, renamed, isDirectory: false);

        var active = t.Journal.LastActive()!;
        Assert.Equal(renamed, active.Destination);
        Assert.Equal(Path.Combine(t.Desktop, "Fatura Ekim.pdf"), active.Source);
        t.Organizer.Undo(active);
        Assert.True(File.Exists(Path.Combine(t.Desktop, "Fatura Ekim.pdf")));
    }

    [Fact]
    public void Renaming_a_target_folder_keeps_undo_working()
    {
        using var t = new TempDesktop();
        var pdf = t.Folder("PDF");
        var entry = t.Organizer.Organize(t.File("a.pdf"))!;
        var moved = Path.Combine(t.Desktop, "Belgeler PDF");
        Directory.Move(pdf, moved);
        t.Journal.NoteRename(pdf, moved, isDirectory: true);

        var active = t.Journal.LastActive()!;
        Assert.Equal(Path.Combine(moved, "a.pdf"), active.Destination);
        Assert.Equal(entry.Source, active.Source); // dosyanın adı değişmedi
        t.Organizer.Undo(active);
        Assert.True(File.Exists(Path.Combine(t.Desktop, "a.pdf")));
    }

    [Fact]
    public void Unrelated_rename_changes_nothing()
    {
        using var t = new TempDesktop();
        t.Folder("PDF");
        t.Organizer.Organize(t.File("a.pdf"));
        Assert.False(t.Journal.NoteRename(Path.Combine(t.Desktop, "b.txt"), Path.Combine(t.Desktop, "c.txt"), false));
    }

    [Fact]
    public void Box_records_follow_renames()
    {
        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nestdesk-box-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var path = Path.Combine(dir, "box-moves.json");
            var log = new BoxMoveLog(path);
            log.Add(new BoxMove { Id = "1", Original = @"C:\D\oyun.lnk", Current = @"C:\N\Kutu\oyun.lnk" });
            log.Add(new BoxMove { Id = "2", Original = @"C:\D\not.txt", Current = @"C:\N\Kutu\not.txt" });
            log.MarkReturned("2", @"C:\D\not.txt", reclaim: true);

            // Kutu klasörü yeniden adlandırıldı (klasör portalı gibi bir görünümden): taşınan öğe hâlâ "taşınmış" sayılır.
            Assert.True(log.NoteRename(@"C:\N\Kutu", @"C:\N\Oyunlar", isDirectory: true));
            Assert.True(log.IsMoved(@"C:\N\Oyunlar\oyun.lnk"));
            Assert.False(log.IsMoved(@"C:\N\Kutu\oyun.lnk"));

            // Masaüstüne geri konan öğe masaüstünde yeniden adlandırıldı: kutu geri gelince yeni adıyla bulunur.
            Assert.True(log.NoteRename(@"C:\D\not.txt", @"C:\D\notlar.txt", isDirectory: false));
            var returned = log.Snapshot().Single(e => e.Id == "2");
            Assert.Equal(@"C:\D\notlar.txt", returned.ReturnedTo);
            Assert.Equal(@"C:\D\notlar.txt", returned.Original);

            Assert.False(log.NoteRename(@"C:\başka.txt", @"C:\yeni.txt", false));
            // Kalıcı.
            Assert.True(new BoxMoveLog(path).IsMoved(@"C:\N\Oyunlar\oyun.lnk"));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Picker_opens_next_to_the_widget_on_its_monitor()
    {
        var work = new Box(-1920, 0, 0, 1040);
        // Sağında yer var.
        Assert.Equal((-1500 + 12, 100), AnchorPlacement.NextTo(new Box(-1800, 100, -1500, 400), 400, 500, work, 12));
        // Sağ kenarda: soluna.
        Assert.Equal((-300 - 12 - 400, 100), AnchorPlacement.NextTo(new Box(-300, 100, -10, 400), 400, 500, work, 12));
        // Alt kenara yakın: yukarı kaydırılır, monitörde kalır.
        Assert.Equal((-1500 + 12, 1040 - 500), AnchorPlacement.NextTo(new Box(-1800, 900, -1500, 1000), 400, 500, work, 12));
        // Monitörü kaplayan widget: pencere onun üstünde, çalışma alanının içinde.
        var (x, y) = AnchorPlacement.NextTo(new Box(-1920, 0, 0, 1040), 400, 500, work, 12);
        Assert.InRange(x, -1920, -400);
        Assert.InRange(y, 0, 540);
    }
}
