using System.IO;
using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>
/// "Masaüstünden ekle": masaüstünden hangi öğeler, hangi sırayla önerilir; seçilenler nereye gider (klasör → bölme, öbürleri →
/// kutu); "Geri al" kutudan neyi çıkarır. Ayrıca "Widget ekle" penceresinin ve Widget'lar sayfasının bağlantıları.
/// </summary>
public class DesktopPicksTests
{
    private const string Desk = @"C:\Users\u\Desktop";
    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DirEntry File(string name, int minutes = 0, FileAttributes attrs = FileAttributes.Normal, string dir = Desk) =>
        new(Path.Combine(dir, name), name, false, attrs, T0.AddMinutes(minutes), 10);

    private static DirEntry Folder(string name, int minutes = 0, FileAttributes attrs = FileAttributes.Directory, string dir = Desk) =>
        new(Path.Combine(dir, name), name, true, attrs | FileAttributes.Directory, T0.AddMinutes(minutes), 0);

    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    [Fact]
    public void Shortcuts_and_folders_come_first_newest_first_then_files()
    {
        var picks = DesktopPicks.From(
        [
            File("rapor.pdf", 50), File("Chrome.lnk", 10), Folder("Projeler", 30), File("eski.txt", 1), File("Oyun.exe", 20),
        ], None, []);
        Assert.Equal(["Projeler", "Oyun", "Chrome", "rapor.pdf", "eski.txt"], picks.Select(p => p.Name));
        Assert.Equal([DesktopPickKind.Folder, DesktopPickKind.Shortcut, DesktopPickKind.Shortcut, DesktopPickKind.File, DesktopPickKind.File],
            picks.Select(p => p.Kind));
    }

    [Fact]
    public void Same_time_is_ordered_by_name_in_the_ui_language()
    {
        var picks = DesktopPicks.From([File("Zeka.lnk"), File("Çizim.lnk"), File("Ana.lnk")], None, []);
        Assert.Equal(["Ana", "Çizim", "Zeka"], picks.Select(p => p.Name));
    }

    [Fact]
    public void Hidden_system_ini_partial_pinned_shown_and_box_folder_are_left_out()
    {
        var pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(Desk, "KUTUDA.LNK") };
        var picks = DesktopPicks.From(
        [
            File("gizli.txt", attrs: FileAttributes.Hidden), File("sistem.lnk", attrs: FileAttributes.System), File("desktop.ini"),
            File("film.mp4.crdownload"), File("kutuda.lnk"), Folder("PDF"), Folder("NestDesk"), Folder("Kalan"), File("kalan.txt"),
        ], pinned, [Path.Combine(Desk, "PDF") + "\\", Path.Combine(Desk, "NestDesk")]);
        Assert.Equal(["Kalan", "kalan.txt"], picks.Select(p => p.Name));
    }

    [Fact]
    public void Same_name_on_the_public_desktop_is_listed_once()
    {
        var picks = DesktopPicks.From([File("Edge.lnk"), File("Edge.lnk", dir: @"C:\Users\Public\Desktop"), File("Zoom.lnk", dir: @"C:\Users\Public\Desktop")],
            None, []);
        Assert.Equal(["Edge", "Zoom"], picks.Select(p => p.Name).Order());
        Assert.Equal(Path.Combine(Desk, "Edge.lnk"), picks.Single(p => p.Name == "Edge").Path);
    }

    [Fact]
    public void Shortcut_names_drop_the_extension_like_explorer()
    {
        Assert.Equal("Chrome", DesktopPicks.Of(@"C:\d\Chrome.lnk", false).Name);
        Assert.Equal("Site", DesktopPicks.Of(@"C:\d\Site.url", false).Name);
        Assert.Equal("not.txt", DesktopPicks.Of(@"C:\d\not.txt", false).Name);
        Assert.Equal("Klasör.lnk", DesktopPicks.Of(@"C:\d\Klasör.lnk", true).Name);
        Assert.Equal(DesktopPickKind.Folder, DesktopPicks.Of(@"C:\d\Klasör\", true).Kind);
        Assert.Equal("Klasör", DesktopPicks.Of(@"C:\d\Klasör\", true).Name);
    }

    [Fact]
    public void Search_ignores_case_accents_and_turkish_letters()
    {
        var picks = DesktopPicks.From([File("Işık Ayarları.lnk"), File("Not.txt"), Folder("Çalışma")], None, []);
        Assert.Equal(["Işık Ayarları"], DesktopPicks.Filter(picks, "isik").Select(p => p.Name));
        Assert.Equal(["Çalışma"], DesktopPicks.Filter(picks, "calis").Select(p => p.Name));
        Assert.Equal(3, DesktopPicks.Filter(picks, "  ").Count);
        Assert.Empty(DesktopPicks.Filter(picks, "yok"));
    }

    [Fact]
    public void Folders_become_panels_capped_and_the_rest_one_box()
    {
        var picks = Enumerable.Range(1, 7).Select(i => DesktopPicks.Of($@"C:\d\K{i}", true))
            .Append(DesktopPicks.Of(@"C:\d\a.lnk", false)).Append(DesktopPicks.Of(@"C:\d\b.txt", false))
            .Append(DesktopPicks.Of(@"C:\d\A.LNK", false)) // aynı yol bir kez
            .ToList();
        var plan = DesktopPicks.Plan(picks);
        Assert.Equal(DesktopPicks.MaxPanels, plan.Panels.Count);
        Assert.Equal(["K1", "K2", "K3", "K4", "K5"], plan.Panels.Select(p => p.Name));
        Assert.Equal(2, plan.SkippedPanels);
        Assert.Equal([@"C:\d\a.lnk", @"C:\d\b.txt"], plan.BoxItems.Select(p => p.Path));
        Assert.Equal(0, DesktopPicks.Plan([DesktopPicks.Of(@"C:\d\x", true)]).SkippedPanels);
    }

    [Fact]
    public void New_box_opens_on_files_tab_only_when_everything_is_a_file()
    {
        Assert.Equal(1, DesktopPicks.NewBoxTab([DesktopPicks.Of(@"C:\d\a.pdf", false), DesktopPicks.Of(@"C:\d\b.txt", false)]));
        Assert.Equal(0, DesktopPicks.NewBoxTab([DesktopPicks.Of(@"C:\d\a.pdf", false), DesktopPicks.Of(@"C:\d\b.lnk", false)]));
        Assert.Equal(0, DesktopPicks.NewBoxTab([]));
        // Tohumun sekme sırası buna uyar: 0 Uygulamalar, 1 Dosyalar.
        var seed = WidgetSeeds.Create(WidgetSeeds.Launcher)!;
        Assert.Equal(["Uygulamalar", "Dosyalar"], seed.Tabs.Select(t => t.Name));
    }

    [Fact]
    public void Target_box_is_the_frontmost_plain_box()
    {
        Assert.Null(DesktopPicks.TargetBox([new WidgetConfig { Kind = WidgetKind.Clock }]));
        var back = new WidgetConfig { Kind = WidgetKind.Launcher, Z = 1 };
        var front = new WidgetConfig { Kind = WidgetKind.Launcher, Z = 5 };
        var ring = WidgetSeeds.Create(WidgetSeeds.Ring)!;
        ring.Z = 9;
        var bin = WidgetSeeds.Create(WidgetSeeds.RecycleBin)!;
        bin.Z = 10;
        Assert.Same(front, DesktopPicks.TargetBox([back, ring, front, bin]));
        Assert.Null(DesktopPicks.TargetBox([ring, bin]));
    }

    [Fact]
    public void Folders_already_in_a_panel_are_known_by_full_path()
    {
        var shown = DesktopPicks.ShownFolders(
        [
            new WidgetConfig { Kind = WidgetKind.Fence, FolderName = "PDF" },
            new WidgetConfig { Kind = WidgetKind.Fence, FolderName = @"D:\Fotolar" },
            new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.Folders },
            new WidgetConfig { Kind = WidgetKind.Launcher, FolderName = "Kutu" },
        ], Desk);
        Assert.Equal([Path.Combine(Desk, "PDF"), @"D:\Fotolar"], shown);
    }

    [Fact]
    public void Undo_takes_back_added_items_even_after_they_moved_into_the_box_folder()
    {
        var box = new WidgetConfig { Kind = WidgetKind.Launcher };
        var apps = new LauncherTab { Name = "Uygulamalar", Items = [@"C:\Users\u\NestDesk\Kutu\Chrome.lnk", @"C:\eski\Önceden.lnk"] };
        var files = new LauncherTab { Name = "Dosyalar", Items = [@"C:\Users\u\Desktop\rapor.pdf"] };
        box.Tabs = [apps, files];
        var moves = new[]
        {
            new BoxMove { Original = @"C:\Users\u\Desktop\Chrome.lnk", Current = @"C:\Users\u\NestDesk\Kutu\Chrome.lnk" },
            // Geri konmuş eski kayıt sayılmaz.
            new BoxMove { Original = @"C:\Users\u\Desktop\rapor.pdf", Current = @"C:\Users\u\NestDesk\Kutu\rapor.pdf", ReturnedTo = @"C:\Users\u\Desktop\rapor.pdf" },
        };
        var back = DesktopPicks.TakeBack(box, [@"C:\Users\u\Desktop\Chrome.lnk", @"c:\users\u\desktop\RAPOR.pdf"], moves);
        Assert.Equal([(apps, @"C:\Users\u\NestDesk\Kutu\Chrome.lnk"), (files, @"C:\Users\u\Desktop\rapor.pdf")], back);
    }

    [Fact]
    public void Undo_through_launcher_edits_leaves_other_items()
    {
        var box = new WidgetConfig { Kind = WidgetKind.Launcher };
        var tab = new LauncherTab { Name = "Uygulamalar", Items = [@"C:\a.lnk", @"C:\b.lnk", @"C:\c.lnk"] };
        box.Tabs = [tab];
        // Saf bölüm: Take sırayla çıkarır, başkasına dokunmaz.
        foreach (var (t, path) in DesktopPicks.TakeBack(box, [@"C:\a.lnk", @"C:\c.lnk"], []))
            Widgets.LauncherEdits.Take(box, t, LauncherEntries.IndexOfPath(t, path));
        Assert.Equal([@"C:\b.lnk"], tab.Items);
    }

    [Fact]
    public void Every_message_has_english_and_plural_forms()
    {
        using var _ = L.Use(Lang.En);
        Assert.Equal("2 panels added.", L.P(2, "{0} bölme eklendi."));
        Assert.Equal("2 widgets removed.", L.P(2, "{0} widget kaldırıldı."));
        Assert.Equal("1 item added to the shortcut box.", L.P(1, "{0} öğe kısayol kutusuna eklendi."));
        Assert.Equal("3 folders weren't added: you can add up to 5 panels at a time.",
            L.P(3, "{0} klasör eklenmedi: bir seferde en çok {1} bölme eklenir.", DesktopPicks.MaxPanels));
    }

    // ---------------------------------------------------------------- bağlantılar (kaynak denetimi)

    [Fact]
    public void Add_window_and_widgets_page_use_the_one_desktop_add_path()
    {
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        string Read(params string[] parts) => System.IO.File.ReadAllText(Path.Combine([src, .. parts]));
        var window = Read("Views", "QuickAddWindow.cs");
        foreach (var id in new[] { "\"Add.FromDesktop\"", "\"Add.FromDesktop.All\"", "\"Add.Desktop:\"", "\"Add.FromDesktop.Add\"",
                     "\"Add.FromDesktop.Search\"", "\"Add.FromDesktop.List\"" })
            Assert.Contains(id, window);
        Assert.Contains("DesktopAdd.Add(picks, near)", window);
        Assert.Contains("DesktopAdd.AddDropped(paths, near)", window);
        Assert.Contains("ShellIconImage.SetPath", window);
        Assert.Contains("DesktopAdd.AddDropped(paths, null)", Read("Views", "WidgetsPage.xaml.cs"));

        var add = Read("Views", "DesktopAdd.cs");
        // Kutuya tek kapıdan (BoxMover.Added sorusu), bölme her zamanki yoldan, öneriler anlık görüntüden; arayüzde disk yok.
        Assert.Contains("AddFromDesktop(", add);
        Assert.Contains("WidgetCatalog.AddFolder(", add);
        Assert.Contains("AppHost.Widgets.AddSeed(", add);
        Assert.Contains("Snapshots", add);
        Assert.Contains("await Task.Run(", add);
        Assert.Contains("RemoveWithUndo(", add);
        Assert.DoesNotContain("Widgets.Remove(", add);
        Assert.DoesNotContain("Directory.Enumerate", add);
        Assert.DoesNotContain("BoxMover.Added(", add);
        Assert.Contains("_edits.AddItems(() => Current, paths, done: done)", Read("Widgets", "LauncherView.xaml.cs"));
        Assert.Contains("done?.Invoke(added);", Read("Widgets", "LauncherEdits.cs"));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (System.IO.File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }
}
