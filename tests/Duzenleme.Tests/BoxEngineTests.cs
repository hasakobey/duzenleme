using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Kutu taşımalarının yürütülmesi (<see cref="BoxEngine"/>, sahte diskle): toplu işte hiçbir şeyin üzerine yazılmaz, sonuçlar
/// kayda göre verilir, ortak masaüstü için yönetici onayı yalnızca kullanıcının isteğiyle ve bir kez istenir, onay
/// verilmezse öğe kullanıcının masaüstüne konur, açık dosya söylenir, geri koyma çökmeye dayanıklıdır, aynı kopya "(1)"
/// üretmez, taşınmış masaüstüne dönülür. Gerçek UAC yok.
/// </summary>
public sealed class BoxEngineTests : IDisposable
{
    private const string Desktop = @"C:\Users\ali\Desktop";
    private const string Public = @"C:\Users\Public\Desktop";
    private const string Root = @"C:\Users\ali\NestDesk";
    private const string Box = Root + @"\Kısayol kutusu";
    private const string Ring = Root + @"\Halka menü";

    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nd-box-engine-" + Guid.NewGuid().ToString("N"))).FullName;
    private readonly FakeBoxDisk _disk = new();
    private readonly BoxMoveLog _log;
    private readonly BoxEngine _engine;

    public BoxEngineTests()
    {
        _disk.AdminOnly.Add(Public);
        _log = new BoxMoveLog(Path.Combine(_dir, "box-moves.json"));
        _engine = new BoxEngine(_disk, _log);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    private static readonly BoxContext WithPublic = new(Desktop, [Public], IncludePublic: true);

    private List<ClaimStep> Plan(BoxContext context, params ClaimInput[] inputs) =>
        BoxPlan.PlanClaims(inputs, _disk.Attributes, context, _disk.Exists);

    private static ReturnRequest Request(bool interactive, bool reclaim = true) =>
        new([Desktop, Public], Desktop, [Public], interactive, reclaim);

    private BoxMove Moved(string original, string current, string content = "x")
    {
        _disk.File(current, content);
        var record = new BoxMove { Original = original, Current = current };
        _log.Add(record);
        return record;
    }

    private BoxMove Record(string id) => _log.Snapshot().Single(r => r.Id == id);

    // ------------------------------------------------------------------------------------------------------- taşıma

    [Fact]
    public void Claim_writes_the_record_before_moving()
    {
        _disk.File($@"{Desktop}\rapor.pdf", "rapor");
        _disk.BeforeMove = (from, to) => Assert.True(_log.IsMoved(to), "kayıt taşımadan önce yazılmalı");

        var outcome = Assert.Single(_engine.Claim(Plan(WithPublic, new ClaimInput("k", Box, [$@"{Desktop}\rapor.pdf"])), allowElevation: true));

        Assert.Equal($@"{Box}\rapor.pdf", outcome.Current);
        Assert.Equal("rapor", _disk.Files[$@"{Box}\rapor.pdf"]);
        Assert.False(_disk.Exists($@"{Desktop}\rapor.pdf"));
        Assert.Equal("k", Assert.Single(_log.Snapshot()).WidgetId);
    }

    /// <summary>B1: aynı ortak masaüstü öğesi bir kutuda ve bir halkada: bir kez taşınır, tek kayıt kalır, hedefi gerçekten vardır.</summary>
    [Fact]
    public void Same_public_path_in_box_and_ring_is_claimed_once()
    {
        _disk.File($@"{Public}\Edge.lnk", "edge");

        var outcomes = _engine.Claim(Plan(WithPublic,
            new ClaimInput("kutu", Box, [$@"{Public}\Edge.lnk"]),
            new ClaimInput("halka", Ring, [$@"{Public}\Edge.lnk"])), allowElevation: true);

        var moved = Assert.Single(outcomes, o => o.Moved);
        Assert.Equal($@"{Box}\Edge.lnk", moved.Current);
        Assert.Equal(1, _disk.ElevatedCalls);
        Assert.Single(_disk.Elevated);
        Assert.Equal("edge", _disk.Files[moved.Current!]);
        var record = Assert.Single(_log.Snapshot());
        Assert.Equal(moved.Current, record.Current);
        Assert.False(_disk.Exists($@"{Ring}\Edge.lnk"));
    }

    /// <summary>
    /// B3: önce ortak masaüstündeki Chrome.lnk (yönetici onayı bekler), sonra kullanıcınınki: ikincisi bekleyen hedefi alamaz;
    /// Windows'un "Tümüne evet"li taşıması hiçbir şeyin üzerine yazmaz, iki içerik de kalır.
    /// </summary>
    [Fact]
    public void Pending_public_target_is_reserved_before_later_user_items()
    {
        _disk.File($@"{Public}\Chrome.lnk", "ortak").File($@"{Desktop}\Chrome.lnk", "kullanıcı");

        var outcomes = _engine.Claim(Plan(WithPublic, new ClaimInput("k", Box, [$@"{Public}\Chrome.lnk", $@"{Desktop}\Chrome.lnk"])),
            allowElevation: true);

        Assert.All(outcomes, o => Assert.True(o.Moved));
        Assert.Equal("ortak", _disk.Files[$@"{Box}\Chrome.lnk"]);
        Assert.Equal("kullanıcı", _disk.Files[$@"{Box}\Chrome (1).lnk"]);
        var records = _log.Snapshot();
        Assert.Equal(2, records.Select(r => r.Current).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(records, r => r.Original == $@"{Public}\Chrome.lnk" && r.Current == $@"{Box}\Chrome.lnk");
        Assert.Contains(records, r => r.Original == $@"{Desktop}\Chrome.lnk" && r.Current == $@"{Box}\Chrome (1).lnk");
    }

    [Fact]
    public void Elevated_guard_drops_moves_onto_existing_or_repeated_targets()
    {
        _disk.File(@"C:\a\dolu.lnk");
        (string, string)[] moves =
        [
            (@"C:\p\1.lnk", @"C:\a\dolu.lnk"),     // hedef dolu
            (@"C:\p\2.lnk", @"C:\a\x.lnk"),        // aynı hedef iki kez
            (@"C:\p\3.lnk", @"C:\a\X.LNK"),
            (@"C:\p\4.lnk", @"C:\a\y.lnk"),        // aynı kaynak iki kez
            (@"C:\P\4.LNK", @"C:\a\z.lnk"),
            (@"C:\p\5.lnk", @"C:\a\tamam.lnk"),
        ];

        var (go, dropped) = _engine.Guard(moves);

        Assert.Equal((@"C:\p\5.lnk", @"C:\a\tamam.lnk"), Assert.Single(go));
        Assert.Equal(5, dropped.Count);
    }

    [Fact]
    public void Declined_public_claim_leaves_a_link_and_no_record()
    {
        _disk.File($@"{Public}\Edge.lnk");
        _disk.Elevation = "deny";

        var outcome = Assert.Single(_engine.Claim(Plan(WithPublic, new ClaimInput("k", Box, [$@"{Public}\Edge.lnk"])), allowElevation: true));

        Assert.False(outcome.Moved);
        Assert.Equal(BoxFailure.Declined, outcome.Failure);
        Assert.Empty(_log.Snapshot());
        Assert.True(_disk.Exists($@"{Public}\Edge.lnk"));
    }

    /// <summary>Arka plandaki taşıma (kutu geri geldi, yeniden deneme) ortak masaüstü için asla yönetici onayı istemez.</summary>
    [Fact]
    public void Background_claim_never_requests_elevation()
    {
        _disk.File($@"{Public}\Edge.lnk");

        var outcome = Assert.Single(_engine.Claim(Plan(WithPublic, new ClaimInput("k", Box, [$@"{Public}\Edge.lnk"])), allowElevation: false));

        Assert.Equal(BoxFailure.Denied, outcome.Failure);
        Assert.Equal(0, _disk.ElevatedCalls);
        Assert.Empty(_log.Snapshot());
    }

    [Fact]
    public void Claim_reports_a_file_that_is_open()
    {
        _disk.File($@"{Desktop}\rapor.docx");
        _disk.Locked.Add($@"{Desktop}\rapor.docx");

        var outcome = Assert.Single(_engine.Claim(Plan(WithPublic, new ClaimInput("k", Box, [$@"{Desktop}\rapor.docx"])), allowElevation: true));

        Assert.Equal(BoxFailure.Locked, outcome.Failure);
        Assert.Empty(_log.Snapshot());                  // önceden yazılan kayıt silindi
        Assert.True(_disk.Exists($@"{Desktop}\rapor.docx"));
    }

    // -------------------------------------------------------------------------------------------------- geri koyma

    /// <summary>B2: Windows onay vermezse öğe kullanıcının masaüstüne konur, kayıt kapanır; sonraki uzlaştırma bir daha sormaz.</summary>
    [Fact]
    public void Declined_public_return_lands_on_user_desktop_and_closes()
    {
        var record = Moved($@"{Public}\Edge.lnk", $@"{Box}\Edge.lnk", "edge");
        _disk.Elevation = "deny";

        var outcome = Assert.Single(_engine.Return([record], Request(interactive: true)));

        Assert.Equal(ReturnState.FellBack, outcome.State);
        Assert.Equal($@"{Desktop}\Edge.lnk", outcome.Path);
        Assert.Equal("edge", _disk.Files[$@"{Desktop}\Edge.lnk"]);
        Assert.Equal(1, _disk.ElevatedCalls);
        Assert.False(Record(record.Id).Active);
        Assert.Null(Record(record.Id).ReturningTo);

        var again = BoxPlan.Reconcile(_log.Snapshot(), new HashSet<string>(StringComparer.OrdinalIgnoreCase), _disk.Exists);
        Assert.Empty(again.Return);
        Assert.Equal(1, _disk.ElevatedCalls);
    }

    [Fact]
    public void Approved_public_return_goes_back_to_everyones_desktop()
    {
        var record = Moved($@"{Public}\Edge.lnk", $@"{Box}\Edge.lnk", "edge");

        var outcome = Assert.Single(_engine.Return([record], Request(interactive: true)));

        Assert.Equal(ReturnState.Restored, outcome.State);
        Assert.Equal("edge", _disk.Files[$@"{Public}\Edge.lnk"]);
        Assert.Equal($@"{Public}\Edge.lnk", Record(record.Id).ReturnedTo);
    }

    /// <summary>B2: açılışta ve arka plandaki uzlaştırmada yönetici onayı istenmez; kayıt açık kalır, öğe bekler.</summary>
    [Fact]
    public void Background_return_never_requests_elevation()
    {
        var record = Moved($@"{Public}\Edge.lnk", $@"{Box}\Edge.lnk");

        var outcome = Assert.Single(_engine.Return([record], Request(interactive: false)));

        Assert.Equal(ReturnState.Waiting, outcome.State);
        Assert.Equal(0, _disk.ElevatedCalls);
        Assert.True(Record(record.Id).Active);
        Assert.Null(Record(record.Id).ReturningTo);
        Assert.True(_disk.Exists($@"{Box}\Edge.lnk"));
    }

    [Fact]
    public void Put_on_my_desktop_skips_everyones_desktop()
    {
        var record = Moved($@"{Public}\Edge.lnk", $@"{Box}\Edge.lnk");

        var outcome = Assert.Single(_engine.Return([record], Request(interactive: true) with { ToUserDesktop = true }));

        Assert.Equal(ReturnState.Restored, outcome.State);
        Assert.Equal($@"{Desktop}\Edge.lnk", outcome.Path);
        Assert.Equal(0, _disk.ElevatedCalls);
    }

    /// <summary>B3: aynı ortak masaüstü adına dönen iki kayıt ayrı hedef alır; ikisi de korunur.</summary>
    [Fact]
    public void Two_returns_to_one_public_name_get_distinct_targets()
    {
        var a = Moved($@"{Public}\Chrome.lnk", $@"{Box}\Chrome.lnk", "eski");
        var b = Moved($@"{Public}\Chrome.lnk", $@"{Box}\Chrome (1).lnk", "yeni");

        var outcomes = _engine.Return([a, b], Request(interactive: true));

        Assert.All(outcomes, o => Assert.Equal(ReturnState.Restored, o.State));
        Assert.Equal(1, _disk.ElevatedCalls);
        Assert.Equal("eski", _disk.Files[$@"{Public}\Chrome.lnk"]);
        Assert.Equal("yeni", _disk.Files[$@"{Public}\Chrome (1).lnk"]);
        Assert.NotEqual(Record(a.Id).ReturnedTo, Record(b.Id).ReturnedTo);
    }

    /// <summary>B7: açık dosya geri konamaz: sonuç söylenir, kayıt açık kalır (yeniden denenebilir), dosyaya dokunulmaz.</summary>
    [Fact]
    public void Locked_return_is_reported_and_stays_active()
    {
        var record = Moved($@"{Desktop}\rapor.docx", $@"{Box}\rapor.docx");
        _disk.Locked.Add($@"{Box}\rapor.docx");

        var outcome = Assert.Single(_engine.Return([record], Request(interactive: true)));

        Assert.Equal(ReturnState.Failed, outcome.State);
        Assert.Equal(BoxFailure.Locked, outcome.Failure);
        Assert.True(Record(record.Id).Active);
        Assert.Null(Record(record.Id).ReturningTo);
        Assert.Empty(_disk.Cleaned);
    }

    /// <summary>B8: geri koymada hedef taşımadan önce yazılır (arada kesilirse açılışta öğe orada bulunur).</summary>
    [Fact]
    public void Return_marks_returning_before_move()
    {
        var record = Moved($@"{Desktop}\a.pdf", $@"{Box}\a.pdf");
        string? seen = null;
        _disk.BeforeMove = (_, to) => seen = Record(record.Id).ReturningTo;

        Assert.Equal(ReturnState.Restored, Assert.Single(_engine.Return([record], Request(interactive: true))).State);

        Assert.Equal($@"{Desktop}\a.pdf", seen);
        Assert.Null(Record(record.Id).ReturningTo);
        Assert.Equal($@"{Desktop}\a.pdf", Record(record.Id).ReturnedTo);
        Assert.Equal((Box, Root), Assert.Single(_disk.Cleaned));
    }

    /// <summary>B8: kullanıcı öğeyi Gezgin'de masaüstüne geri taşımış: "Masaüstüne geri koy" sessiz kalmaz, kutu oradakini gösterir.</summary>
    [Fact]
    public void Item_moved_back_by_hand_is_already_there()
    {
        var record = Moved($@"{Desktop}\a.pdf", $@"{Box}\a.pdf");
        _disk.Files.Remove($@"{Box}\a.pdf");
        _disk.File($@"{Desktop}\a.pdf");

        var outcome = Assert.Single(_engine.Return([record], Request(interactive: true, reclaim: false)));

        Assert.Equal(ReturnState.AlreadyThere, outcome.State);
        Assert.Equal($@"{Desktop}\a.pdf", outcome.Path);
        Assert.True(outcome.OnDesktop);
        Assert.False(Record(record.Id).Active);
    }

    [Fact]
    public void Item_gone_everywhere_is_reported_not_found()
    {
        var record = Moved($@"{Desktop}\a.pdf", $@"{Box}\a.pdf");
        _disk.Files.Remove($@"{Box}\a.pdf");

        var outcome = Assert.Single(_engine.Return([record], Request(interactive: true, reclaim: false)));

        Assert.Equal(ReturnState.NotFound, outcome.State);
        Assert.False(outcome.OnDesktop);
        Assert.False(Record(record.Id).Active);
    }

    [Fact]
    public void Interrupted_return_is_finished_where_it_landed()
    {
        var record = Moved($@"{Desktop}\a.pdf", $@"{Box}\a.pdf");
        _log.MarkReturning(record.Id, $@"{Desktop}\a (1).pdf");
        _disk.Files.Remove($@"{Box}\a.pdf");
        _disk.File($@"{Desktop}\a (1).pdf");

        var outcome = Assert.Single(_engine.Return([Record(record.Id)], Request(interactive: false)));

        Assert.Equal(ReturnState.AlreadyThere, outcome.State);
        Assert.Equal($@"{Desktop}\a (1).pdf", outcome.Path);
        Assert.Equal($@"{Desktop}\a (1).pdf", Record(record.Id).ReturnedTo);
    }

    /// <summary>B15: masaüstünde bayt bayt aynı kopya (güncelleme kısayolu yeniden oluşturdu): "ad (1)" çıkmaz, bizimki Geri Dönüşüm Kutusu'na gider.</summary>
    [Fact]
    public void Identical_copy_on_the_desktop_is_absorbed()
    {
        var record = Moved($@"{Desktop}\Discord.lnk", $@"{Box}\Discord.lnk", "aynı");
        _disk.File($@"{Desktop}\Discord.lnk", "aynı");

        var outcome = Assert.Single(_engine.Return([record], Request(interactive: true)));

        Assert.Equal(ReturnState.Absorbed, outcome.State);
        Assert.Equal($@"{Desktop}\Discord.lnk", outcome.Path);
        Assert.Equal([$@"{Box}\Discord.lnk"], _disk.Recycled);
        Assert.False(_disk.Exists($@"{Desktop}\Discord (1).lnk"));
        Assert.Equal($@"{Desktop}\Discord.lnk", Record(record.Id).ReturnedTo);
    }

    [Fact]
    public void Different_copy_on_the_desktop_keeps_both()
    {
        var record = Moved($@"{Desktop}\not.txt", $@"{Box}\not.txt", "eski");
        _disk.File($@"{Desktop}\not.txt", "yeni");

        var outcome = Assert.Single(_engine.Return([record], Request(interactive: true)));

        Assert.Equal(ReturnState.Restored, outcome.State);
        Assert.Equal($@"{Desktop}\not (1).txt", outcome.Path);
        Assert.Equal("yeni", _disk.Files[$@"{Desktop}\not.txt"]);
        Assert.Equal("eski", _disk.Files[$@"{Desktop}\not (1).txt"]);
        Assert.Empty(_disk.Recycled);
    }

    /// <summary>B17: masaüstü sonradan (OneDrive) taşındı: öğe bugünkü masaüstüne döner, eski kökün boşalan klasörü de temizlenir.</summary>
    [Fact]
    public void Return_goes_to_current_desktop_after_relocation()
    {
        var record = Moved(@"C:\Users\ali\EskiMasaüstü\a.pdf", @"C:\Users\ali\NestDesk\Kutu\a.pdf");
        const string newDesktop = @"C:\Users\ali\OneDrive\Desktop";

        var outcome = Assert.Single(_engine.Return([record], new ReturnRequest([newDesktop], newDesktop, [], Interactive: true, Reclaim: true)));

        Assert.Equal($@"{newDesktop}\a.pdf", outcome.Path);
        Assert.Equal((@"C:\Users\ali\NestDesk\Kutu", @"C:\Users\ali\NestDesk"), Assert.Single(_disk.Cleaned));
    }

    [Fact]
    public void Returned_items_reach_the_rule_guard_before_the_record_closes()
    {
        var restored = new List<(string Path, string BoxPath)>();
        var engine = new BoxEngine(_disk, _log) { Restored = (p, b) => restored.Add((p, b)) };
        var removed = Moved($@"{Desktop}\a.pdf", $@"{Box}\a.pdf");
        var kept = Moved($@"{Desktop}\b.pdf", $@"{Box}\b.pdf");

        engine.Return([removed], Request(interactive: true, reclaim: true));
        engine.Return([kept], Request(interactive: true, reclaim: false));

        // Yalnızca kutudan çıkan (reclaim) öğe "kurallar taşımasın" kaydı alır; kutuda kalanı kutu zaten korur.
        Assert.Equal([($@"{Desktop}\a.pdf", $@"{Box}\a.pdf")], restored);
    }
}
