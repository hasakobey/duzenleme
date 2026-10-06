using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>"Kutulara eklediklerim masaüstünden kalksın": taşıma planı, kayıtlar ve dosya işleri.</summary>
public class BoxMovesTests
{
    private const string Desktop = @"C:\Users\ali\Desktop";
    private const string Public = @"C:\Users\Public\Desktop";
    private static readonly string[] Publics = [Public];

    private static BoxLinkReason Decide(string path, FileAttributes? attrs = FileAttributes.Normal, bool includePublic = false) =>
        BoxPlan.Decide(path, attrs, Desktop, Publics, includePublic);

    // Kök ve klasör adı

    [Theory]
    [InlineData(@"C:\Users\ali\Desktop", @"C:\Users\ali\NestDesk")]
    [InlineData(@"C:\Users\ali\Desktop\", @"C:\Users\ali\NestDesk")]
    [InlineData(@"C:\Users\ali\OneDrive\Masaüstü", @"C:\Users\ali\OneDrive\NestDesk")]
    [InlineData(@"C:\Temp\nd-test\desk", @"C:\Temp\nd-test\NestDesk")]
    [InlineData(@"D:\", @"C:\Users\ali\NestDesk")]
    public void Root_sits_next_to_the_desktop(string desktop, string expected) =>
        Assert.Equal(expected, BoxPlan.RootFor(desktop, @"C:\Users\ali"));

    [Theory]
    [InlineData(null, "Kısayol kutusu")]
    [InlineData("", "Kısayol kutusu")]
    [InlineData("  Oyunlar  ", "Oyunlar")]
    [InlineData("İş: Q3/Q4?", "İş Q3Q4")]
    [InlineData("Belgeler...", "Belgeler")]
    [InlineData("con", "con kutusu")]
    [InlineData("NUL.txt", "NUL.txt kutusu")]
    [InlineData("???", "Kısayol kutusu")]
    public void Box_folder_name_is_a_safe_file_name(string? title, string expected)
    {
        var name = BoxPlan.FolderNameFor(title);
        Assert.Equal(expected, name);
        Assert.True(name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
        Assert.True(name.Length <= 70);
    }

    [Fact]
    public void Long_titles_are_shortened() =>
        Assert.Equal(60, BoxPlan.FolderNameFor(new string('a', 200)).Length);

    // Taşınır mı?

    [Fact]
    public void Desktop_file_and_shortcut_are_moved()
    {
        Assert.Equal(BoxLinkReason.None, Decide(@"C:\Users\ali\Desktop\rapor.pdf"));
        Assert.Equal(BoxLinkReason.None, Decide(@"c:\users\ALI\desktop\Oyun.lnk"));
        Assert.Equal(BoxLinkReason.None, Decide(@"C:\Users\ali\Desktop\Oyun.exe"));
    }

    [Fact]
    public void Items_that_are_not_on_a_desktop_are_never_moved()
    {
        Assert.Equal(BoxLinkReason.NotOnDesktop, Decide(@"C:\Program Files\App\app.exe"));
        Assert.Equal(BoxLinkReason.NotOnDesktop, Decide(@"C:\Users\ali\Desktop\PDF\a.pdf")); // alt klasör
        Assert.Equal(BoxLinkReason.NotOnDesktop, Decide(@"C:\Users\ali\NestDesk\Kısayol kutusu\a.pdf"));
        Assert.Equal(BoxLinkReason.NotOnDesktop, Decide("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"));
        Assert.Equal(BoxLinkReason.NotOnDesktop, Decide(@"\\sunucu\paylasim\a.pdf"));
    }

    /// <summary>Test örneği (--desktop) yalnızca kendi klasörüne dokunur: kutu gerçek masaüstünü gösterse de taşınmaz.</summary>
    [Fact]
    public void Test_desktop_never_moves_a_real_desktop_item()
    {
        var reason = BoxPlan.Decide(@"C:\Users\ali\Desktop\rapor.pdf", FileAttributes.Normal, @"C:\Temp\nd\desk", [], includePublic: true);
        Assert.Equal(BoxLinkReason.NotOnDesktop, reason);
        Assert.Equal(BoxLinkReason.NotOnDesktop,
            BoxPlan.Decide(@"C:\Users\Public\Desktop\Chrome.lnk", FileAttributes.Normal, @"C:\Temp\nd\desk", [], includePublic: true));
    }

    [Fact]
    public void Public_desktop_needs_the_opt_in()
    {
        Assert.Equal(BoxLinkReason.PublicDesktop, Decide(@"C:\Users\Public\Desktop\Chrome.lnk"));
        Assert.Equal(BoxLinkReason.None, Decide(@"C:\Users\Public\Desktop\Chrome.lnk", includePublic: true));
    }

    [Fact]
    public void Protected_and_missing_items_stay()
    {
        Assert.Equal(BoxLinkReason.Protected, Decide(@"C:\Users\ali\Desktop\desktop.ini"));
        Assert.Equal(BoxLinkReason.Protected, Decide(@"C:\Users\ali\Desktop\gizli.txt", FileAttributes.Hidden));
        Assert.Equal(BoxLinkReason.Protected, Decide(@"C:\Users\ali\Desktop\sistem.dat", FileAttributes.System | FileAttributes.Archive));
        Assert.Equal(BoxLinkReason.Protected, Decide(@"C:\Users\ali\Desktop\~$rapor.docx"));
        Assert.Equal(BoxLinkReason.Missing, Decide(@"C:\Users\ali\Desktop\silindi.pdf", attrs: null));
    }

    /// <summary>
    /// B6: klasör hiç taşınmaz (içindeki yolları gösteren kutu öğeleri, portallar, halka komutları, kurallar ve başka
    /// programlar bozulurdu); kutuda bağlantı olarak durur. Aynı adlı dosya klasör değildir: taşınır.
    /// </summary>
    [Fact]
    public void Folders_are_links_and_never_move()
    {
        Assert.Equal(BoxLinkReason.Folder, Decide(@"C:\Users\ali\Desktop\Projeler", FileAttributes.Directory));
        Assert.Equal(BoxLinkReason.Folder, Decide(@"C:\Users\ali\Desktop\PDF", FileAttributes.Directory | FileAttributes.ReadOnly));
        Assert.Equal(BoxLinkReason.Folder, Decide(@"C:\Users\Public\Desktop\Ortak", FileAttributes.Directory, includePublic: true));
        Assert.Equal(BoxLinkReason.None, Decide(@"C:\Users\ali\Desktop\PDF", FileAttributes.Archive));
        // Gizli klasör zaten korunur.
        Assert.Equal(BoxLinkReason.Protected, Decide(@"C:\Users\ali\Desktop\gizli", FileAttributes.Directory | FileAttributes.Hidden));
    }

    // Kutulardaki masaüstü öğeleri (kurallar taşımasın)

    [Fact]
    public void Pinned_paths_are_the_box_items_on_a_desktop()
    {
        List<WidgetConfig> widgets =
        [
            new() { Kind = WidgetKind.Launcher, Tabs = [new() { Items = [@"C:\Users\ali\Desktop\a.pdf", @"C:\Program Files\x.exe"] },
                                                         new() { Items = [@"C:\Users\Public\Desktop\Chrome.lnk", "::{645FF040-5081-101B-9F08-00AA002F954E}"] }] },
            new() { Kind = WidgetKind.Launcher, Tabs = [new() { Items = [@"C:\Users\ali\Desktop\b.zip", @"C:\Users\ali\Desktop\PDF\c.pdf"] }] },
            new() { Kind = WidgetKind.Fence, FolderName = "PDF", HiddenItems = [@"C:\Users\ali\Desktop\d.pdf"] },
        ];

        var pinned = BoxPlan.PinnedDesktopPaths(widgets, [Desktop, Public]);

        Assert.Equal(3, pinned.Count);
        Assert.Contains(@"c:\users\ali\desktop\A.PDF", pinned);
        Assert.Contains(@"C:\Users\Public\Desktop\Chrome.lnk", pinned);
        Assert.Contains(@"C:\Users\ali\Desktop\b.zip", pinned);
        Assert.Empty(BoxPlan.PinnedDesktopPaths(widgets, [@"C:\Temp\desk"]));
    }

    // Uzlaştırma

    private static BoxMove Active(string original, string current) => new() { Original = original, Current = current };

    private static BoxMove Returned(string original, string current, string returnedTo, bool reclaim) =>
        new() { Original = original, Current = current, ReturnedTo = returnedTo, Reclaim = reclaim };

    private const string Box = @"C:\Users\ali\NestDesk\Kısayol kutusu";

    [Fact]
    public void Unreferenced_moved_item_goes_back_to_the_desktop()
    {
        var kept = Active(@$"{Desktop}\a.pdf", @$"{Box}\a.pdf");
        var orphan = Active(@$"{Desktop}\b.pdf", @$"{Box}\b.pdf");
        var done = Returned(@$"{Desktop}\c.pdf", @$"{Box}\c.pdf", @$"{Desktop}\c.pdf", reclaim: true);
        var referenced = new HashSet<string>([@$"{Box}\A.pdf"], StringComparer.OrdinalIgnoreCase);

        var plan = BoxPlan.Reconcile([kept, orphan, done], referenced, _ => true);

        Assert.Same(orphan, Assert.Single(plan.Return));
        Assert.Empty(plan.Remap);
    }

    [Fact]
    public void Interrupted_move_is_linked_to_the_new_place()
    {
        // Kayıt yazıldı ve dosya taşındı ama kutu güncellenemeden uygulama kapandı: kutu eski yolu gösteriyor.
        var record = Active(@$"{Desktop}\a.pdf", @$"{Box}\a.pdf");
        var referenced = new HashSet<string>([@$"{Desktop}\a.pdf"], StringComparer.OrdinalIgnoreCase);

        var plan = BoxPlan.Reconcile([record], referenced, p => p == @$"{Box}\a.pdf");

        // Yeni yola bağlanır; masaüstüne geri konmaz ve yeniden taşınmaz.
        var (from, to, reclaim) = Assert.Single(plan.Remap);
        Assert.Equal((@$"{Desktop}\a.pdf", @$"{Box}\a.pdf", false), (from, to, reclaim));
        Assert.Empty(plan.Return);
    }

    [Fact]
    public void Move_that_never_happened_is_given_back()
    {
        // Kayıt yazıldı ama dosya taşınamadan kesildi: kutu masaüstündeki dosyayı gösteriyor, dosya yerinde.
        var record = Active(@$"{Desktop}\a.pdf", @$"{Box}\a.pdf");
        var referenced = new HashSet<string>([@$"{Desktop}\a.pdf"], StringComparer.OrdinalIgnoreCase);

        var plan = BoxPlan.Reconcile([record], referenced, p => p == @$"{Desktop}\a.pdf");

        // Kayıt kapanır (geri koyma dosyayı bulamaz, kaydı kapatır); kutuya dokunulmaz.
        Assert.Empty(plan.Remap);
        Assert.Same(record, Assert.Single(plan.Return));
    }

    [Fact]
    public void Box_that_comes_back_finds_its_returned_items()
    {
        var removed = Returned(@$"{Desktop}\a.pdf", @$"{Box}\a.pdf", @$"{Desktop}\a (1).pdf", reclaim: true);
        var byUser = Returned(@$"{Desktop}\b.pdf", @$"{Box}\b.pdf", @$"{Desktop}\b.pdf", reclaim: false);
        var referenced = new HashSet<string>([@$"{Box}\a.pdf", @$"{Box}\b.pdf"], StringComparer.OrdinalIgnoreCase);
        var onDisk = new HashSet<string>([@$"{Desktop}\a (1).pdf", @$"{Desktop}\b.pdf"], StringComparer.OrdinalIgnoreCase);

        var plan = BoxPlan.Reconcile([removed, byUser], referenced, onDisk.Contains);

        Assert.Empty(plan.Return);
        Assert.Contains((@$"{Box}\a.pdf", @$"{Desktop}\a (1).pdf", true), plan.Remap);
        Assert.Contains((@$"{Box}\b.pdf", @$"{Desktop}\b.pdf", false), plan.Remap);
    }

    [Fact]
    public void Box_that_comes_back_finds_an_item_a_rule_moved_after_it_was_returned()
    {
        // Kutu kaldırıldı, öğe masaüstüne döndü, sonra bir kural onu PDF klasörüne taşıdı; kutu "Geri al" ile geldi.
        var returnedAt = new DateTime(2026, 9, 28, 18, 8, 0);
        var removed = Returned(@$"{Desktop}\rapor.pdf", @$"{Box}\rapor.pdf", @$"{Desktop}\rapor.pdf", reclaim: true);
        removed.Time = returnedAt;
        var referenced = new HashSet<string>([@$"{Box}\rapor.pdf"], StringComparer.OrdinalIgnoreCase);
        var journal = new List<MoveEntry>
        {
            // Aynı adlı başka bir dosyanın eski taşıması ve geri alınmış bir kayıt sayılmaz.
            new() { Source = @$"{Desktop}\rapor.pdf", Destination = @$"{Desktop}\Eski\rapor.pdf", Time = returnedAt.AddDays(-3) },
            new() { Source = @$"{Desktop}\rapor.pdf", Destination = @$"{Desktop}\Geri\rapor.pdf", Time = returnedAt.AddSeconds(1), Undone = true },
            new() { Source = @$"{Desktop}\rapor.pdf", Destination = @$"{Desktop}\PDF\rapor.pdf", Time = returnedAt.AddSeconds(2) },
        };
        var onDisk = new HashSet<string>([@$"{Desktop}\PDF\rapor.pdf", @$"{Desktop}\Eski\rapor.pdf", @$"{Desktop}\Geri\rapor.pdf"],
            StringComparer.OrdinalIgnoreCase);

        var plan = BoxPlan.Reconcile([removed], referenced, onDisk.Contains, BoxPlan.MovedByRule(journal));

        Assert.Equal((@$"{Box}\rapor.pdf", @$"{Desktop}\PDF\rapor.pdf", true), Assert.Single(plan.Remap));
        Assert.Empty(plan.Return);
    }

    [Fact]
    public void Journal_moves_before_the_return_are_not_taken_for_the_returned_item()
    {
        var returnedAt = new DateTime(2026, 9, 28, 18, 8, 0);
        var removed = Returned(@$"{Desktop}\rapor.pdf", @$"{Box}\rapor.pdf", @$"{Desktop}\rapor.pdf", reclaim: true);
        removed.Time = returnedAt;
        var referenced = new HashSet<string>([@$"{Box}\rapor.pdf"], StringComparer.OrdinalIgnoreCase);
        List<MoveEntry> journal = [new() { Source = @$"{Desktop}\rapor.pdf", Destination = @$"{Desktop}\Eski\rapor.pdf", Time = returnedAt.AddDays(-3) }];

        var plan = BoxPlan.Reconcile([removed], referenced, p => p == @$"{Desktop}\Eski\rapor.pdf", BoxPlan.MovedByRule(journal));

        // Başka bir dosyaya (aynı adlı eski taşıma) bağlanmaz: öğe kutuda "bulunamadı" kalır.
        Assert.Empty(plan.Remap);
    }

    [Fact]
    public void Reconcile_looks_at_the_disk_only_for_box_move_paths()
    {
        var record = Active(@$"{Desktop}\a.pdf", @$"{Box}\a.pdf");
        var referenced = new HashSet<string>([@$"{Box}\a.pdf", @"\\çevrimdışı\paylaşım\x.exe", @"C:\Program Files\y.exe"],
            StringComparer.OrdinalIgnoreCase);
        var asked = new List<string>();

        var plan = BoxPlan.Reconcile([record], referenced, p => { asked.Add(p); return false; });

        // Çevrimdışı ağ yolu gibi ilgisiz öğeler için diske gidilmez (arka planda bile saniyelerce bekletebilir).
        Assert.DoesNotContain(@"\\çevrimdışı\paylaşım\x.exe", asked);
        Assert.DoesNotContain(@"C:\Program Files\y.exe", asked);
        Assert.All(asked, p => Assert.True(p == record.Current || p == record.Original, p));
        Assert.Empty(plan.Return);
        // Kutunun gösterdiği NestDesk öğesi ne orada ne masaüstünde: kayıt kapanır, kutu "bulunamadı" gösterir (B8).
        var (closed, remapTo, reason) = Assert.Single(plan.Close);
        Assert.Same(record, closed);
        Assert.Null(remapTo);
        Assert.Equal(BoxCloseReason.Gone, reason);
        Assert.Empty(plan.Remap);
    }

    // Dışarıda değişenler (B8) ve yarıda kalanlar

    private static HashSet<string> Refs(params string[] paths) => new(paths, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Item_put_back_by_hand_is_linked_and_closed()
    {
        // Kullanıcı NestDesk\…\a.pdf'i Gezgin'de masaüstüne geri sürükledi; kutu hâlâ NestDesk yolunu gösteriyor.
        var record = Active(@$"{Desktop}\a.pdf", @$"{Box}\a.pdf");

        var plan = BoxPlan.Reconcile([record], Refs(@$"{Box}\a.pdf"), p => p == @$"{Desktop}\a.pdf");

        Assert.Equal((@$"{Box}\a.pdf", @$"{Desktop}\a.pdf", false), Assert.Single(plan.Remap));
        Assert.Equal((record, @$"{Desktop}\a.pdf", BoxCloseReason.BackOnDesktop), Assert.Single(plan.Close));
        Assert.Empty(plan.Return);
    }

    [Fact]
    public void Item_deleted_from_box_folder_closes_record()
    {
        var record = Active(@$"{Desktop}\a.pdf", @$"{Box}\a.pdf");

        var plan = BoxPlan.Reconcile([record], Refs(@$"{Box}\a.pdf"), _ => false);

        Assert.Equal((record, (string?)null, BoxCloseReason.Gone), Assert.Single(plan.Close));
        Assert.Empty(plan.Remap);
        Assert.Empty(plan.Return);
    }

    [Fact]
    public void Interrupted_return_is_finished()
    {
        // Geri koyma taşımadan sonra, kayıt kapanmadan kesildi (çökme): öğe hedefinde.
        var record = Active(@$"{Desktop}\a.pdf", @$"{Box}\a.pdf");
        record.ReturningTo = @$"{Desktop}\a (1).pdf";

        var plan = BoxPlan.Reconcile([record], Refs(@$"{Box}\a.pdf"), p => p == @$"{Desktop}\a (1).pdf");

        Assert.Equal((@$"{Box}\a.pdf", @$"{Desktop}\a (1).pdf", false), Assert.Single(plan.Remap));
        Assert.Equal(BoxCloseReason.Finished, Assert.Single(plan.Close).Reason);
    }

    [Fact]
    public void Interrupted_cross_volume_copy_keeps_original()
    {
        // Birim arası taşıma kopyaladı ama kaynağı silemeden kesildi: öğe iki yerde, kutu masaüstündekini gösteriyor.
        var record = Active(@$"{Desktop}\a.pdf", @"D:\NestDesk\Kutu\a.pdf");

        var plan = BoxPlan.Reconcile([record], Refs(@$"{Desktop}\a.pdf"), _ => true);

        Assert.Equal((record, (string?)null, BoxCloseReason.CopyLeft), Assert.Single(plan.Close));
        Assert.Empty(plan.Return); // geri koymak masaüstünde "a (1).pdf" kopyası üretirdi
        Assert.Empty(plan.Remap);
    }

    [Fact]
    public void Copy_is_kept_while_a_box_shows_it()
    {
        // Masaüstündeki yeni öğe ve kutuya taşınmış eskisi aynı kutuda: kayıt açık kalır.
        var record = Active(@$"{Desktop}\a.pdf", @$"{Box}\a.pdf");

        var plan = BoxPlan.Reconcile([record], Refs(@$"{Desktop}\a.pdf", @$"{Box}\a.pdf"), _ => true);

        Assert.Empty(plan.Close);
        Assert.Empty(plan.Return);
    }

    [Fact]
    public void ForItem_record_is_referenced_while_its_app_is()
    {
        const string app = "::{4234D49B-0245-4DF3-B780-3893943456E1}\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
        var record = Active(@$"{Desktop}\Hesap Makinesi.lnk", @$"{Box}\Hesap Makinesi.lnk");
        record.ForItem = app;

        Assert.Empty(BoxPlan.Reconcile([record], Refs(app), _ => true).Return);
        Assert.Same(record, Assert.Single(BoxPlan.Reconcile([record], Refs(), _ => true).Return));
    }

    // Masaüstü taşındıysa (B17), klasör adları (B22)

    [Fact]
    public void Return_goes_to_current_desktop_after_relocation()
    {
        string[] dirs = [@"C:\Users\ali\OneDrive\Desktop", Public];
        Assert.Equal(@"C:\Users\ali\OneDrive\Desktop", BoxPlan.ReturnDirectory(@"C:\Users\ali\Desktop\a.pdf", dirs, dirs[0]));
        Assert.Equal(Public, BoxPlan.ReturnDirectory(@"C:\Users\Public\Desktop\Edge.lnk", dirs, dirs[0]));
        Assert.Equal(@"C:\Users\ali\OneDrive\Desktop", BoxPlan.ReturnDirectory(@"c:\users\ali\onedrive\DESKTOP\b.pdf", dirs, dirs[0] + "\\"));
    }

    [Fact]
    public void Root_of_a_record_is_its_own_NestDesk_folder()
    {
        Assert.Equal(@"C:\Users\ali\NestDesk", BoxPlan.RootOf(@"C:\Users\ali\NestDesk\Kutu\a.pdf"));
        Assert.Equal(@"C:\Users\ali\OneDrive\NestDesk", BoxPlan.RootOf(@"C:\Users\ali\OneDrive\NestDesk\Kutu\a.pdf"));
        Assert.Null(BoxPlan.RootOf(@"C:\Users\ali\Desktop\a.pdf"));
        Assert.Null(BoxPlan.RootOf(@"C:\NestDesk\a\b\c\d.pdf")); // yalnızca iki üst klasöre bakılır
    }

    [Fact]
    public void Old_root_is_cleaned_after_relocation()
    {
        using var t = new TempDesktop();
        var oldRoot = Path.Combine(t.Root, "eski", "NestDesk");
        var oldFolder = Directory.CreateDirectory(Path.Combine(oldRoot, "Kutu")).FullName;
        var current = Path.Combine(oldFolder, "a.pdf");
        File.WriteAllText(current, "x");
        var log = new BoxMoveLog(Path.Combine(t.Root, "box-moves.json"));
        var record = new BoxMove { Original = Path.Combine(t.Root, "eski", "Desktop", "a.pdf"), Current = current };
        log.Add(record);
        var engine = new BoxEngine(new RealDisk(), log);

        var outcome = Assert.Single(engine.Return([record], new ReturnRequest([t.Desktop], t.Desktop, [], Interactive: true, Reclaim: false)));

        Assert.Equal(Path.Combine(t.Desktop, "a.pdf"), outcome.Path);
        Assert.True(File.Exists(outcome.Path));
        Assert.False(Directory.Exists(oldRoot)); // bugünkü kök değil, kaydın kendi kökü temizlendi
        Assert.True(Directory.Exists(Path.Combine(t.Root, "eski")));
    }

    [Fact]
    public void Unique_folder_names_per_launcher()
    {
        Assert.Equal("Oyunlar", BoxPlan.UniqueFolderName("Oyunlar", ["İş", null]));
        Assert.Equal("Oyunlar (2)", BoxPlan.UniqueFolderName("Oyunlar", ["oyunlar", "İş"]));
        Assert.Equal("Oyunlar (3)", BoxPlan.UniqueFolderName("Oyunlar", ["Oyunlar", "Oyunlar (2)"]));
        var name = BoxPlan.UniqueFolderName(new string('a', 60), [new string('a', 60)]);
        Assert.Equal(60, name.Length);
        Assert.EndsWith(" (2)", name, StringComparison.Ordinal);
    }

    [Fact]
    public void Ring_without_a_title_gets_its_own_folder_name()
    {
        var ring = new WidgetConfig { Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring, Title = " " };
        var box = new WidgetConfig { Kind = WidgetKind.Launcher };
        Assert.Equal("Halka menü", BoxPlan.FolderNameFor(BoxPlan.FolderTitleOf(ring)));
        Assert.Equal("Kısayol kutusu", BoxPlan.FolderNameFor(BoxPlan.FolderTitleOf(box)));
    }

    [Fact]
    public void Duplicate_does_not_share_the_box_folder()
    {
        var copy = new WidgetConfig { Kind = WidgetKind.Launcher, Title = "Oyunlar", BoxFolder = "Oyunlar" };
        WidgetSeeds.PrepareDuplicate(copy);
        Assert.Null(copy.BoxFolder);
    }

    // Yeniden deneme (B15): açık olduğu için taşınamayanlar sonraki açılışta da denensin

    private static ClaimOutcome Outcome(string path, BoxFailure failure = BoxFailure.None, BoxLinkReason reason = BoxLinkReason.None,
        bool moved = false) =>
        new(new ClaimStep("k", path, reason == BoxLinkReason.None ? path + ".x" : null, reason, OnPublic: false), moved ? path + ".x" : null, failure);

    [Fact]
    public void Locked_claims_are_kept_for_retry_and_dropped_once_moved()
    {
        var a = @$"{Desktop}\a.docx";
        var b = @$"{Desktop}\b.pdf";
        var list = BoxPlan.UpdateRetry(null, [Outcome(a, BoxFailure.Locked), Outcome(b, BoxFailure.Other), Outcome(@$"{Desktop}\c.pdf", moved: true)]);
        Assert.Equal([a, b], list);

        // Değişiklik yoksa aynı liste (ayar yeniden yazılmaz).
        Assert.Same(list, BoxPlan.UpdateRetry(list, [Outcome(a.ToUpperInvariant(), BoxFailure.Locked)]));

        // Taşınan, artık taşınmayacak (silinmiş, klasör) ya da Windows onayı gerekmiş olan çıkar.
        list = BoxPlan.UpdateRetry(list, [Outcome(a, moved: true)]);
        Assert.Equal([b], list);
        Assert.Null(BoxPlan.UpdateRetry(list, [Outcome(b, reason: BoxLinkReason.Missing)]));
        Assert.Null(BoxPlan.UpdateRetry([a], [Outcome(a, BoxFailure.Declined)]));
    }

    // Kayıtlar kaybolunca (B23)

    [Fact]
    public void Adopt_rebuilds_records_from_box_paths()
    {
        const string root = @"C:\Users\ali\NestDesk";
        var known = Active(@$"{Desktop}\eski.pdf", @$"{Box}\eski.pdf");
        (string, string)[] items =
        [
            ("kutu", @$"{Box}\a.pdf"),
            ("halka", @$"{Box}\A.PDF"),                         // aynı öğe ikinci kutuda: bir kayıt
            ("kutu", @$"{Box}\eski.pdf"),                       // kaydı var
            ("kutu", @$"{Box}\silinmiş.pdf"),                   // diskte yok
            ("kutu", @$"{root}\kökte.pdf"),                     // kutu klasöründe değil
            ("kutu", @$"{Box}\alt\derin.pdf"),                  // kutu klasörünün altında
            ("kutu", @"C:\Program Files\x.exe"),
            ("kutu", "::{645FF040-5081-101B-9F08-00AA002F954E}"),
        ];
        var onDisk = Refs(@$"{Box}\a.pdf", @$"{Box}\eski.pdf", @$"{root}\kökte.pdf", @$"{Box}\alt\derin.pdf", @"C:\Program Files\x.exe");

        var adopted = BoxPlan.Adopt([known], items, root + "\\", Desktop, onDisk.Contains);

        var record = Assert.Single(adopted);
        Assert.Equal(("kutu", @$"{Box}\a.pdf", @$"{Desktop}\a.pdf"), (record.WidgetId, record.Current, record.Original));
        Assert.True(record.Active);
    }

    [Fact]
    public void Lost_log_is_rebuilt_from_box_folders()
    {
        using var t = new TempDesktop();
        var root = BoxPlan.RootFor(t.Desktop, t.Root);
        var folder = Directory.CreateDirectory(Path.Combine(root, "Kutu")).FullName;
        var item = Path.Combine(folder, "a.pdf");
        File.WriteAllText(item, "x");
        var path = Path.Combine(t.Root, "box-moves.json");
        File.WriteAllText(path, "{ bozuk");
        File.WriteAllText(path + ".bak", "[ bozuk");

        var log = new BoxMoveLog(path);
        Assert.Equal(0, log.ActiveCount);
        log.AddMany(BoxPlan.Adopt(log.Snapshot(), [("kutu", item)], root, t.Desktop, BoxFiles.Exists));

        var reloaded = new BoxMoveLog(path);
        Assert.True(reloaded.IsMoved(item));
        Assert.Equal(1, reloaded.ActiveCount);
        Assert.Equal(Path.Combine(t.Desktop, "a.pdf"), reloaded.Snapshot().Single().Original);
    }

    // Kayıt dosyası

    [Fact]
    public void Returning_and_app_link_fields_survive_a_restart()
    {
        using var t = new TempDesktop();
        var path = Path.Combine(t.Root, "box-moves.json");
        var log = new BoxMoveLog(path);
        var record = new BoxMove { Original = @$"{Desktop}\a.lnk", Current = @$"{Box}\a.lnk", ForItem = "::{X}\\app" };
        log.Add(record);
        log.MarkReturning(record.Id, @$"{Desktop}\a.lnk");

        var reloaded = new BoxMoveLog(path).Snapshot().Single();
        Assert.Equal(@$"{Desktop}\a.lnk", reloaded.ReturningTo);
        Assert.Equal("::{X}\\app", reloaded.ForItem);

        log.MarkReturned(record.Id, @$"{Desktop}\a.lnk", reclaim: true);
        var closed = new BoxMoveLog(path).Snapshot().Single();
        Assert.Null(closed.ReturningTo);
        Assert.False(closed.Active);
        // Boş alanlar yazılmaz (2.1 dosyası gibi kalır).
        Assert.DoesNotContain("ReturningTo", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_record_fields_from_a_newer_version_survive()
    {
        using var t = new TempDesktop();
        var path = Path.Combine(t.Root, "box-moves.json");
        File.WriteAllText(path, $$"""[ { "Id": "a", "Original": "{{Desktop.Replace("\\", "\\\\")}}\\a.pdf", "Current": "{{Box.Replace("\\", "\\\\")}}\\a.pdf", "Zz_Future": { "x": 1 } } ]""");

        var log = new BoxMoveLog(path);
        log.MarkReturned("a", @$"{Desktop}\a.pdf", reclaim: false);

        Assert.Contains("Zz_Future", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(t.Root, "box-moves.json.bozuk-*"));
    }

    [Fact]
    public void Same_content_compares_bytes()
    {
        using var t = new TempDesktop();
        var a = t.File("a.lnk", "aynı");
        var b = t.File("b.lnk", "aynı");
        var c = t.File("c.lnk", "farklı");
        Assert.True(BoxFiles.SameContent(a, b));
        Assert.False(BoxFiles.SameContent(a, c));
        Assert.False(BoxFiles.SameContent(a, Path.Combine(t.Desktop, "yok.lnk")));
        Assert.False(BoxFiles.SameContent(t.Desktop, t.Desktop)); // klasör
    }

    private sealed class RealDisk : IBoxDisk
    {
        public FileAttributes? Attributes(string path) => BoxFiles.Attributes(path);
        public bool Exists(string path) => BoxFiles.Exists(path);
        public void Move(string from, string to) => BoxFiles.Move(from, to);
        public void MoveElevated(IReadOnlyList<(string From, string To)> moves) => throw new InvalidOperationException("yönetici onayı istenmemeli");
        public void RemoveIfEmpty(string folder, string root) => BoxFiles.RemoveIfEmpty(folder, root);
        public bool SameContent(string a, string b) => BoxFiles.SameContent(a, b);
        public void Recycle(string path) => File.Delete(path);
    }

    [Fact]
    public void Log_survives_a_restart_and_tracks_returns()
    {
        using var t = new TempDesktop();
        var path = Path.Combine(t.Root, "box-moves.json");
        var log = new BoxMoveLog(path);
        var a = new BoxMove { WidgetId = "w1", Original = @$"{Desktop}\a.pdf", Current = @$"{Box}\a.pdf" };
        var b = new BoxMove { WidgetId = "w1", Original = @$"{Desktop}\b.pdf", Current = @$"{Box}\b.pdf" };
        log.Add(a);
        log.Add(b);
        log.MarkReturned(b.Id, @$"{Desktop}\b.pdf", reclaim: true);

        var reloaded = new BoxMoveLog(path);

        Assert.Equal(1, reloaded.ActiveCount);
        Assert.True(reloaded.IsMoved(@$"{Box}\A.PDF"));
        Assert.False(reloaded.IsMoved(@$"{Box}\b.pdf"));
        var returned = reloaded.Snapshot().Single(r => !r.Active);
        Assert.True(returned.Reclaim);
        Assert.Equal(@$"{Desktop}\b.pdf", returned.ReturnedTo);

        reloaded.Remove(a.Id);
        Assert.Equal(0, new BoxMoveLog(path).ActiveCount);
    }

    [Fact]
    public void Log_keeps_every_active_item_and_trims_old_returns()
    {
        using var t = new TempDesktop();
        var log = new BoxMoveLog(Path.Combine(t.Root, "box-moves.json"));
        var active = new BoxMove { Original = @$"{Desktop}\keep.pdf", Current = @$"{Box}\keep.pdf" };
        log.Add(active);
        for (var i = 0; i < BoxMoveLog.MaxReturned + 20; i++)
        {
            var r = new BoxMove { Original = @$"{Desktop}\{i}.pdf", Current = @$"{Box}\{i}.pdf" };
            log.Add(r);
            log.MarkReturned(r.Id, r.Original, reclaim: false);
        }

        var all = log.Snapshot();
        Assert.Equal(BoxMoveLog.MaxReturned, all.Count(r => !r.Active));
        Assert.True(log.IsMoved(active.Current));
        Assert.DoesNotContain(all, r => r.Original.EndsWith(@"\0.pdf", StringComparison.Ordinal));
    }

    [Fact]
    public void Broken_log_file_does_not_break_anything()
    {
        using var t = new TempDesktop();
        var path = Path.Combine(t.Root, "box-moves.json");
        File.WriteAllText(path, "{ bozuk");

        var log = new BoxMoveLog(path);

        Assert.Equal(0, log.ActiveCount);
        Assert.NotEmpty(Directory.GetFiles(t.Root, "box-moves.json.bozuk-*"));
    }

    // Dosya işleri (geçici klasörde)

    [Fact]
    public void Files_and_folders_move_into_the_box_folder_and_back()
    {
        using var t = new TempDesktop();
        var root = BoxPlan.RootFor(t.Desktop, t.Root);
        var folder = Path.Combine(root, BoxPlan.FolderNameFor("Oyunlar"));
        var file = t.File("oyun.lnk", "kısayol");
        var dir = t.Folder("Projeler");
        File.WriteAllText(Path.Combine(dir, "not.txt"), "x");

        var fileTarget = BoxFiles.TargetIn(folder, file);
        BoxFiles.Move(file, fileTarget);
        var dirTarget = BoxFiles.TargetIn(folder, dir);
        BoxFiles.Move(dir, dirTarget);

        Assert.Equal(Path.Combine(t.Root, "NestDesk", "Oyunlar", "oyun.lnk"), fileTarget);
        Assert.False(File.Exists(file));
        Assert.Equal("kısayol", File.ReadAllText(fileTarget));
        Assert.True(File.Exists(Path.Combine(dirTarget, "not.txt")));
        Assert.False(BoxFiles.Attributes(fileTarget)!.Value.HasFlag(FileAttributes.Hidden)); // gizlenmez, görünür klasörde

        // Masaüstünde aynı adda yeni bir dosya varsa geri konan "(1)" alır; hiçbir şeyin üzerine yazılmaz.
        t.File("oyun.lnk", "yeni");
        var back = BoxFiles.MoveBack(fileTarget, file, t.Desktop);
        Assert.Equal(Path.Combine(t.Desktop, "oyun (1).lnk"), back);
        Assert.Equal("yeni", File.ReadAllText(file));
        Assert.Equal("kısayol", File.ReadAllText(back));

        var dirBack = BoxFiles.MoveBack(dirTarget, dir, t.Desktop);
        Assert.Equal(dir, dirBack);
        Assert.True(File.Exists(Path.Combine(dir, "not.txt")));
    }

    [Fact]
    public void Item_goes_to_the_desktop_when_its_old_folder_is_gone()
    {
        using var t = new TempDesktop();
        var folder = Path.Combine(t.Root, "NestDesk", "Kutu");
        var target = Path.Combine(folder, "a.pdf");
        Directory.CreateDirectory(folder);
        File.WriteAllText(target, "x");

        var back = BoxFiles.MoveBack(target, Path.Combine(t.Root, "silinen-klasör", "a.pdf"), t.Desktop);

        Assert.Equal(Path.Combine(t.Desktop, "a.pdf"), back);
        Assert.True(File.Exists(back));
    }

    [Fact]
    public void Empty_box_folders_are_cleaned_up_but_nothing_else()
    {
        using var t = new TempDesktop();
        var root = BoxPlan.RootFor(t.Desktop, t.Root);
        var empty = Directory.CreateDirectory(Path.Combine(root, "Boş kutu")).FullName;
        var used = Directory.CreateDirectory(Path.Combine(root, "Dolu kutu")).FullName;
        File.WriteAllText(Path.Combine(used, "kullanıcının.txt"), "x");

        BoxFiles.RemoveIfEmpty(empty, root);
        Assert.False(Directory.Exists(empty));
        Assert.True(Directory.Exists(used));
        Assert.True(Directory.Exists(root)); // içinde hâlâ bir klasör var

        File.Delete(Path.Combine(used, "kullanıcının.txt"));
        BoxFiles.RemoveIfEmpty(used, root);
        Assert.False(Directory.Exists(root));
        Assert.True(Directory.Exists(t.Root)); // kökün dışına çıkılmaz

        // Kökün dışındaki boş klasöre (ör. masaüstü) asla dokunulmaz.
        var outside = Directory.CreateDirectory(Path.Combine(t.Root, "baska")).FullName;
        BoxFiles.RemoveIfEmpty(outside, root);
        Assert.True(Directory.Exists(outside));
        BoxFiles.RemoveIfEmpty(Path.Combine(t.Root, "NestDesk2"), root);
    }

    [Fact]
    public void Target_name_avoids_existing_items()
    {
        using var t = new TempDesktop();
        var folder = Directory.CreateDirectory(Path.Combine(t.Root, "NestDesk", "Kutu")).FullName;
        File.WriteAllText(Path.Combine(folder, "a.pdf"), "eski");

        Assert.Equal(Path.Combine(folder, "a (1).pdf"), BoxFiles.TargetIn(folder, t.File("a.pdf")));
        Assert.Null(BoxFiles.Attributes(Path.Combine(t.Desktop, "yok.pdf")));
        Assert.Null(BoxFiles.Attributes("::{645FF040-5081-101B-9F08-00AA002F954E}"));
    }
}
