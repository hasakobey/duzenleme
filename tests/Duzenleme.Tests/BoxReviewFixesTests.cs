using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>
/// "Kutuya koyduğum simgeler masaüstünden kalksın" inceleme düzeltmeleri: kayıt yazılırken arayüz beklemez ve bir taşıma işi
/// kaydı tek yazmayla yazar; eklemesi geri alınan öğe kurallardan saklanmaz; ayar dosyası yokken de kaldırma simgeleri geri koyar;
/// uygulama kısayolu kayıtları geri alınır ve kapanır; hata iletisi kullanıcıya gösterilmez; ulaşılamayan NestDesk klasörü
/// kayıtları kapatmaz; yarıda kalan geri koyma hedefi silinir.
/// </summary>
public sealed class BoxReviewFixesTests : IDisposable
{
    private const string Desktop = @"C:\Users\ali\Desktop";
    private const string Profile = @"C:\Users\ali";
    private const string Box = @"C:\Users\ali\NestDesk\Kısayol kutusu";
    private const string App = @"::{4234D49B-0245-4DF3-B780-3893943456E1}\Contoso.App";

    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-box-review-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    private BoxMoveLog NewLog() => new(Path.Combine(_dir, BoxMoveLog.FileName));

    private static WidgetConfig Launcher(string id, params string[] items) =>
        new() { Id = id, Kind = WidgetKind.Launcher, Tabs = [new LauncherTab { Items = [.. items] }] };

    private static HashSet<string> Refs(params string[] paths) => new(paths, StringComparer.OrdinalIgnoreCase);

    // ------------------------------------------------------------------- 1: kayıt yazılırken arayüz beklemez

    [Fact]
    public void Readers_are_not_blocked_while_the_log_is_written()
    {
        var log = NewLog();
        var blocked = 0;
        log.BeforeWrite = () =>
        {
            // Arayüz iş parçacığının okumaları (IsMoved, ActiveCount, Snapshot) diske yazma sürerken beklememeli.
            var read = Task.Run(() => (log.IsMoved(@$"{Box}\a.pdf"), log.ActiveCount, log.Snapshot().Count));
            if (!read.Wait(TimeSpan.FromSeconds(5))) blocked++;
        };

        var record = new BoxMove { Original = @$"{Desktop}\a.pdf", Current = @$"{Box}\a.pdf" };
        log.Add(record);
        log.MarkReturning(record.Id, @$"{Desktop}\a.pdf");
        log.ClearReturning(record.Id);
        log.NoteRename(@$"{Box}\a.pdf", @$"{Box}\b.pdf", isDirectory: false);
        log.MarkReturned(record.Id, @$"{Desktop}\b.pdf", reclaim: false);

        Assert.Equal(0, blocked);
        Assert.Equal(5, log.Writes);
        var reloaded = Assert.Single(NewLog().Snapshot());
        Assert.Equal(@$"{Box}\b.pdf", reloaded.Current);
        Assert.False(reloaded.Active);
    }

    [Fact]
    public void Claim_writes_all_records_once_before_moving_and_drops_failures_in_one_write()
    {
        var log = NewLog();
        var disk = new FakeBoxDisk();
        disk.Folders.Add(Profile);
        var paths = Enumerable.Range(1, 5).Select(i => @$"{Desktop}\{i}.pdf").ToList();
        foreach (var path in paths) disk.File(path);
        disk.Locked.Add(paths[2]);
        var writesAtFirstMove = -1;
        disk.BeforeMove = (_, _) =>
        {
            if (writesAtFirstMove < 0) writesAtFirstMove = log.Writes;
            // Bütün öğelerin kaydı ilk taşımadan önce diskte.
            Assert.Equal(paths.Count, NewLog().ActiveCount);
        };
        var engine = new BoxEngine(disk, log);
        var context = new BoxContext(Desktop, [], IncludePublic: false);

        var outcomes = engine.Claim(BoxPlan.PlanClaims([new ClaimInput("k", Box, paths)], disk.Attributes, context, disk.Exists),
            allowElevation: false);

        Assert.Equal(1, writesAtFirstMove);
        Assert.Equal(4, outcomes.Count(o => o.Moved));
        Assert.Equal(BoxFailure.Locked, outcomes.Single(o => !o.Moved).Failure);
        // Tek ekleme + taşınamayanın tek silinmesi (öğe başına yazma yok).
        Assert.Equal(2, log.Writes);
        var records = NewLog().Snapshot();
        Assert.Equal(4, records.Count);
        Assert.DoesNotContain(records, r => r.Original == paths[2]);
    }

    // ------------------------------------------------------------------- 2: eklemesi geri alınan öğe

    [Fact]
    public void Items_that_left_every_box_are_reported_for_unpinning()
    {
        var undone = new BoxMove { Original = @$"{Desktop}\a.pdf", Current = @$"{Box}\a.pdf" };
        var kept = new BoxMove { Original = @$"{Desktop}\b.pdf", Current = @$"{Box}\b.pdf" };
        var failed = new BoxMove { Original = @$"{Desktop}\c.pdf", Current = @$"{Box}\c.pdf" };
        List<ReturnOutcome> outcomes =
        [
            new(undone, ReturnState.Restored, @$"{Desktop}\a.pdf"),
            new(kept, ReturnState.Restored, @$"{Desktop}\b.pdf"),
            new(failed, ReturnState.Failed, null, BoxFailure.Locked),
        ];

        // "Geri al" a.pdf'i kutudan çıkardı; b.pdf başka bir kutuda duruyor.
        var loose = BoxPlan.LeftBoxes(outcomes, Refs(@$"{Desktop}\b.pdf"));

        Assert.Equal((@$"{Desktop}\a.pdf", @$"{Box}\a.pdf"), Assert.Single(loose));
    }

    // ------------------------------------------------------------------- 3: ayar dosyası yokken kaldırma

    [Fact]
    public void Uninstall_finds_box_logs_without_a_settings_file()
    {
        var roaming = @"C:\Users\ali\AppData\Roaming";
        var candidates = DataFolderLocator.SettingsCandidates(null, @"D:\NestDesk\data", roaming);
        var legacyLog = Path.Combine(roaming, "Duzenleme", BoxMoveLog.FileName);
        var portableLog = Path.Combine(@"D:\NestDesk\data", BoxMoveLog.FileName);

        // Hiçbir settings.json yok; iki klasörde kutu kaydı var.
        var dirs = DataFolderLocator.BoxMoveDirectories(candidates, p => p == legacyLog || p == portableLog);

        Assert.Equal([@"D:\NestDesk\data", Path.Combine(roaming, "Duzenleme")], dirs);
        Assert.Empty(DataFolderLocator.BoxMoveDirectories(candidates, _ => false));
    }

    // ------------------------------------------------------------------- 4: uygulama kısayolu kayıtları

    [Fact]
    public void Undo_of_return_all_reclaims_app_shortcuts_with_their_app()
    {
        var box = Launcher("kutu", App, @$"{Desktop}\a.pdf");
        var other = Launcher("başka", @$"{Desktop}\x.pdf");
        var shortcut = new BoxMove
        {
            WidgetId = "kutu", Original = @$"{Desktop}\Contoso.lnk", Current = @$"{Box}\Contoso.lnk", ForItem = App,
            ReturnedTo = @$"{Desktop}\Contoso.lnk",
        };
        var orphan = new BoxMove
        {
            Original = @$"{Desktop}\Eski.lnk", Current = @$"{Box}\Eski.lnk", ForItem = @"::{4234D49B-0245-4DF3-B780-3893943456E1}\Gone.App",
            ReturnedTo = @$"{Desktop}\Eski.lnk",
        };

        var batches = BoxPlan.ReclaimBatches([@$"{Desktop}\a.pdf", @$"{Desktop}\Contoso.lnk", @$"{Desktop}\Eski.lnk"], [box, other],
            [shortcut, orphan]);

        Assert.Equal(2, batches.Count);
        Assert.Contains(batches, b => b.BoxId == "kutu" && b.ForItem is null && b.Paths.SequenceEqual([@$"{Desktop}\a.pdf"]));
        // Kısayol kutuda değil: kaydından bulunur ve uygulamasıyla birlikte yeniden kalkar; uygulaması hiçbir kutuda olmayan dönmez.
        Assert.Contains(batches, b => b.BoxId == "kutu" && b.ForItem == App && b.Paths.SequenceEqual([@$"{Desktop}\Contoso.lnk"]));
    }

    [Fact]
    public void App_shortcut_record_closes_when_its_file_is_gone()
    {
        var gone = new BoxMove { Original = @$"{Desktop}\Gone.lnk", Current = @$"{Box}\Gone.lnk", ForItem = App };
        var back = new BoxMove { Original = @$"{Desktop}\Back.lnk", Current = @$"{Box}\Back.lnk", ForItem = App };
        var here = new BoxMove { Original = @$"{Desktop}\Here.lnk", Current = @$"{Box}\Here.lnk", ForItem = App };
        var onDisk = Refs(Profile, @$"{Desktop}\Back.lnk", here.Current);

        var plan = BoxPlan.Reconcile([gone, back, here], Refs(App), onDisk.Contains);

        Assert.Empty(plan.Return);
        Assert.Empty(plan.Remap); // kısayol kutuda değil
        Assert.Equal(2, plan.Close.Count);
        Assert.Contains((gone, (string?)null, BoxCloseReason.Gone), plan.Close);
        Assert.Contains((back, @$"{Desktop}\Back.lnk", BoxCloseReason.BackOnDesktop), plan.Close);
    }

    // ------------------------------------------------------------------- 5: hata iletisi gösterilmez

    [Fact]
    public void Failure_text_never_shows_the_exception_message()
    {
        Assert.Equal(L.T("beklenmeyen bir dosya hatası"), BoxChoices.FailureText(BoxFailure.Other));
        Assert.Equal(L.T("kullanımda"), BoxChoices.FailureText(BoxFailure.Locked));
        using var _ = L.Use(Lang.En);
        Assert.Equal("unexpected file error", BoxChoices.FailureText(BoxFailure.Other));
        Assert.Equal("the folder can't be reached right now", BoxChoices.FailureText(BoxFailure.Unavailable));
    }

    // ------------------------------------------------------------------- 6: ulaşılamayan NestDesk klasörü

    [Fact]
    public void Unreachable_box_root_closes_nothing()
    {
        // Masaüstü ve NestDesk klasörü çıkarılmış bir sürücüde: hiçbiri yok.
        const string desktop = @"E:\Masaüstü";
        const string box = @"E:\NestDesk\Kutu";
        var shown = new BoxMove { Original = @$"{desktop}\a.pdf", Current = @$"{box}\a.pdf" };
        var app = new BoxMove { Original = @$"{desktop}\b.lnk", Current = @$"{box}\b.lnk", ForItem = App };

        var plan = BoxPlan.Reconcile([shown, app], Refs(shown.Current, App), _ => false);

        Assert.Empty(plan.Close);
        Assert.Empty(plan.Remap);
        Assert.Empty(plan.Return);
        Assert.True(BoxPlan.RootUnavailable(shown.Current, _ => false));
        // NestDesk klasörü silinmiş ama sürücü duruyor: öğe gerçekten yok.
        Assert.False(BoxPlan.RootUnavailable(shown.Current, p => p == @"E:\"));
    }

    [Fact]
    public void Return_keeps_the_record_when_the_box_root_is_unreachable()
    {
        var log = NewLog();
        var disk = new FakeBoxDisk(); // kullanıcı klasörü bile yok: sürücü çıkarılmış
        var record = new BoxMove { Original = @$"{Desktop}\a.pdf", Current = @$"{Box}\a.pdf" };
        log.Add(record);

        var outcome = Assert.Single(new BoxEngine(disk, log).Return([record],
            new ReturnRequest([Desktop], Desktop, [], Interactive: true, Reclaim: false)));

        Assert.Equal(ReturnState.Failed, outcome.State);
        Assert.Equal(BoxFailure.Unavailable, outcome.Failure);
        Assert.True(Assert.Single(NewLog().Snapshot()).Active);
        Assert.Empty(disk.Cleaned);
    }

    // ------------------------------------------------------------------- 7: yarıda kalan geri koyma hedefi

    [Fact]
    public void Stale_returning_target_is_cleared_when_the_item_is_still_in_the_box_folder()
    {
        var log = NewLog();
        var stale = new BoxMove { Original = @$"{Desktop}\a.pdf", Current = @$"{Box}\a.pdf", ReturningTo = @$"{Desktop}\a (1).pdf" };
        var unrelated = new BoxMove { Original = @$"{Desktop}\b.pdf", Current = @$"{Box}\b.pdf" };
        log.AddMany([stale, unrelated]);

        // Geri koyma taşımadan önce kesildi: öğe hâlâ NestDesk'te, kutu onu gösteriyor.
        var plan = BoxPlan.Reconcile(log.Snapshot(), Refs(stale.Current, unrelated.Current), Refs(Profile, stale.Current, unrelated.Current).Contains);

        Assert.Equal([stale.Id], plan.StaleReturning);
        Assert.Empty(plan.Close);
        log.ClearReturningMany(plan.StaleReturning);
        Assert.All(NewLog().Snapshot(), r => Assert.Null(r.ReturningTo));

        // Öğe sonra kaybolursa masaüstünde o adla duran başka bir dosyaya ("Finished") bağlanmaz.
        var later = BoxPlan.Reconcile(log.Snapshot(), Refs(stale.Current), Refs(Profile, @$"{Desktop}\a (1).pdf").Contains);
        Assert.Equal(BoxCloseReason.Gone, later.Close.Single(c => c.Record.Id == stale.Id).Reason);
    }
}
