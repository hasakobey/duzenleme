using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Kutu taşımalarının disk tarafı (testte sahtesi verilir). Bütün çağrılar arka plandaki iş parçacığından yapılır.
/// </summary>
public interface IBoxDisk
{
    /// <summary>Öğenin öznitelikleri; yoksa ya da okunamıyorsa null.</summary>
    FileAttributes? Attributes(string path);

    bool Exists(string path);

    /// <summary>Taşır (hedef klasörü yoksa açılır, hedef doluysa hata). İzin yoksa <see cref="UnauthorizedAccessException"/>.</summary>
    void Move(string from, string to);

    /// <summary>
    /// Yönetici onayıyla tek işlemde taşır (Windows'un onay penceresi bir kez). Hedef klasörler açılır, hedef adlar boştur
    /// (<see cref="BoxEngine"/> denetler). Kullanıcı vazgeçerse <see cref="OperationCanceledException"/>. Hangi taşımanın
    /// olduğunu çağıran diske bakarak öğrenir.
    /// </summary>
    void MoveElevated(IReadOnlyList<(string From, string To)> moves);

    /// <summary>Boşalan kutu klasörünü (ve boşaldıysa kökü) siler; kökün dışına çıkmaz.</summary>
    void RemoveIfEmpty(string folder, string root);

    /// <summary>İki dosya aynı mı (masaüstüne yeniden konmuş aynı kısayol)?</summary>
    bool SameContent(string a, string b);

    /// <summary>Geri Dönüşüm Kutusu'na gönderir (geri alınabilir).</summary>
    void Recycle(string path);
}

/// <summary>Taşıma ya da geri koyma neden olmadı (yalnızca çalışma zamanı).</summary>
public enum BoxFailure
{
    None,

    /// <summary>Öğe şu an açık (başka bir program kullanıyor): sonra yeniden denenir.</summary>
    Locked,

    /// <summary>İzin yok (yönetici onayı gerekir ama istenmedi).</summary>
    Denied,

    /// <summary>Windows'un yönetici onayı verilmedi (vazgeçildi).</summary>
    Declined,

    /// <summary>Başka bir G/Ç hatası (ileti <c>Error</c>'da; yalnızca günlük için, kullanıcıya gösterilmez).</summary>
    Other,

    /// <summary>NestDesk klasörünün kendisine şu an ulaşılamıyor (çevrimdışı sürücü, ağ, OneDrive): kayıt açık kalır.</summary>
    Unavailable,
}

/// <summary>Taşıma adımının sonucu: <see cref="Current"/> doluysa öğe NestDesk klasöründe, değilse kutuda bağlantı olarak kaldı.</summary>
public sealed record ClaimOutcome(ClaimStep Step, string? Current, BoxFailure Failure = BoxFailure.None, string? Error = null)
{
    public bool Moved => Current is not null;
}

/// <summary>Geri koymanın sonucu (yalnızca çalışma zamanı).</summary>
public enum ReturnState
{
    /// <summary>Masaüstüne geri kondu (<c>Path</c>).</summary>
    Restored,

    /// <summary>Ortak masaüstüne konamadı (Windows izin vermedi): kullanıcının kendi masaüstüne kondu (<c>Path</c>).</summary>
    FellBack,

    /// <summary>Masaüstünde aynısı zaten vardı: NestDesk'teki kopya Geri Dönüşüm Kutusu'na gitti, kayıt oradakine kapandı.</summary>
    Absorbed,

    /// <summary>NestDesk'te yoktu ama eski yerinde (ya da yarıda kalan dönüşün hedefinde) duruyordu: kayıt oraya kapandı.</summary>
    AlreadyThere,

    /// <summary>Hiçbir yerde bulunamadı: kayıt kapandı, dosyaya dokunulmadı.</summary>
    NotFound,

    /// <summary>Ortak masaüstüne dönecek, yönetici onayı gerekir ama arka planda sorulmaz: kayıt açık kalır.</summary>
    Waiting,

    /// <summary>Taşınamadı (<c>Failure</c>); kayıt açık kalır, öğe NestDesk'te.</summary>
    Failed,
}

public sealed record ReturnOutcome(BoxMove Record, ReturnState State, string? Path = null, BoxFailure Failure = BoxFailure.None,
    string? Error = null)
{
    /// <summary>Öğe şimdi masaüstünde (kutu bu yolu göstermeli).</summary>
    public bool OnDesktop => State is ReturnState.Restored or ReturnState.FellBack or ReturnState.Absorbed or ReturnState.AlreadyThere;
}

/// <summary>Geri koyma işinin ortamı.</summary>
/// <param name="DesktopDirs">Bugünkü masaüstü klasörleri (kullanıcınınki önce).</param>
/// <param name="PublicDesktops">Ortak masaüstleri (yalnızca yönetici yazar).</param>
/// <param name="Interactive">Kullanıcı istedi (kaldırma, Geri al, "Masaüstüne geri koy"): ortak masaüstüne dönüş için Windows
/// onay ister; vazgeçilirse öğe kullanıcının masaüstüne konur. false (açılış, arka plandaki uzlaştırma): asla sorulmaz.</param>
/// <param name="Reclaim">Kutudan kaldırıldığı için geri konuyor (kutu geri gelirse yeniden taşınır).</param>
/// <param name="ToUserDesktop">Eski yerine değil, kullanıcının masaüstüne ("Benim masaüstüme koy").</param>
public sealed record ReturnRequest(IReadOnlyList<string> DesktopDirs, string UserDesktop, IReadOnlyList<string> PublicDesktops,
    bool Interactive, bool Reclaim, bool ToUserDesktop = false);

/// <summary>
/// Kutu taşımalarını yürütür (arka planda, tek iş sırasında). Önce kayıt, sonra taşıma: taşıma kaydı yazılmadan dosyaya
/// dokunulmaz, geri koyarken hedef önce yazılır (<see cref="BoxMove.ReturningTo"/>). Hiçbir şeyin üzerine yazılmaz: hedef
/// adlar iş boyunca ayrılır ve yönetici onaylı toplu taşımaya yalnızca kaynağı ve hedefi tek, hedefi boş taşımalar girer.
/// Sonuçlar kayda (kimliğine) göre verilir: aynı kaynak iki kez taşınmaya kalksa da biri ötekinin sonucunu almaz. Saf
/// karar <see cref="BoxPlan"/>'dadır; burada yalnızca sıra ve hata yönetimi var. Testlenir (sahte disk).
/// </summary>
public sealed class BoxEngine(IBoxDisk disk, BoxMoveLog log)
{
    /// <summary>Geri konan öğe masaüstüne vardı (arka planda, kayıt kapanmadan önce): kurallar onu hemen taşımasın diye.</summary>
    public Action<string, string>? Restored { get; init; }

    // ------------------------------------------------------------------------------------------------------------ taşıma

    /// <summary>
    /// Plandaki öğeleri NestDesk klasörüne taşır. Ortak masaüstündeki öğe izin yüzünden taşınamazsa
    /// <paramref name="allowElevation"/> ise hepsi tek yönetici onayıyla taşınır; değilse bağlantı olarak kalır (Denied).
    /// </summary>
    public List<ClaimOutcome> Claim(IReadOnlyList<ClaimStep> steps, bool allowElevation)
    {
        var outcomes = new ClaimOutcome?[steps.Count];
        var planned = new List<(int Index, BoxMove Record)>();
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Reason != BoxLinkReason.None || step.Target is not { } target)
            {
                outcomes[i] = new ClaimOutcome(step, null);
                continue;
            }
            planned.Add((i, new BoxMove { WidgetId = step.BoxId, Original = step.Original, Current = target, ForItem = step.ForItem }));
        }
        if (planned.Count == 0) return Results(outcomes, steps);

        // Önce kayıt (işin bütün öğeleri tek yazmayla), sonra taşıma: yazılamazsa hiçbir dosyaya dokunulmaz.
        try { log.AddMany(planned.Select(p => p.Record).ToList()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("box", "claim-record-failed", ex);
            foreach (var (index, _) in planned) outcomes[index] = new ClaimOutcome(steps[index], null, BoxFailure.Other, ex.Message);
            return Results(outcomes, steps);
        }

        var failed = new List<string>();
        var pending = new List<(int Index, BoxMove Record)>();
        foreach (var (i, record) in planned)
        {
            var step = steps[i];
            try
            {
                disk.Move(step.Original, record.Current);
                outcomes[i] = new ClaimOutcome(step, record.Current);
            }
            catch (Exception ex) when (IsDiskError(ex))
            {
                if (ex is UnauthorizedAccessException && step.OnPublic)
                {
                    // Ortak masaüstü yalnızca yöneticiyle değişir: aşağıda Windows'un onayıyla, hepsi birlikte (kayıt yazılı kalır).
                    if (allowElevation)
                    {
                        pending.Add((i, record));
                        continue;
                    }
                    outcomes[i] = new ClaimOutcome(step, null, BoxFailure.Denied);
                }
                else outcomes[i] = Failed(step, ex);
                failed.Add(record.Id);
            }
        }

        if (pending.Count > 0)
        {
            var (go, dropped) = Guard(pending.Select(p => (p.Record.Original, p.Record.Current)).ToList());
            foreach (var p in pending.Where(p => dropped.Contains((p.Record.Original, p.Record.Current))))
            {
                outcomes[p.Index] = new ClaimOutcome(steps[p.Index], null, BoxFailure.Other);
                failed.Add(p.Record.Id);
            }
            var moving = pending.Where(p => !dropped.Contains((p.Record.Original, p.Record.Current))).ToList();
            var failure = moving.Count > 0 ? Elevate(go) : BoxFailure.None;
            foreach (var (index, record) in moving)
            {
                if (disk.Exists(record.Current) && !disk.Exists(record.Original))
                    outcomes[index] = new ClaimOutcome(steps[index], record.Current);
                else
                {
                    failed.Add(record.Id);
                    outcomes[index] = new ClaimOutcome(steps[index], null, failure == BoxFailure.None ? BoxFailure.Declined : failure);
                }
            }
        }
        // Taşınamayanların kayıtları tek yazmayla silinir.
        log.RemoveMany(failed);
        return Results(outcomes, steps);
    }

    private static List<ClaimOutcome> Results(ClaimOutcome?[] outcomes, IReadOnlyList<ClaimStep> steps) =>
        outcomes.Select((o, i) => o ?? new ClaimOutcome(steps[i], null, BoxFailure.Other)).ToList();

    private static ClaimOutcome Failed(ClaimStep step, Exception ex)
    {
        var failure = Classify(ex);
        if (failure == BoxFailure.Other) AppLog.Warn("box", "claim-move-failed", ex);
        return new ClaimOutcome(step, null, failure, ex.Message);
    }

    // ------------------------------------------------------------------------------------------------------- geri koyma

    /// <summary>
    /// Kayıtlardaki öğeleri masaüstüne geri koyar. Hedef adlar iş boyunca ayrılır; masaüstünde aynı içerikli kopya varsa
    /// "ad (1)" yerine NestDesk'teki kopya Geri Dönüşüm Kutusu'na gider. Ortak masaüstüne dönüş yalnızca
    /// <see cref="ReturnRequest.Interactive"/> iken yönetici onayı ister; onay verilmezse öğe kullanıcının masaüstüne konur
    /// (kayıt kapanır, bir daha sorulmaz). Arka planda sorulmaz: <see cref="ReturnState.Waiting"/>.
    /// </summary>
    public List<ReturnOutcome> Return(IReadOnlyList<BoxMove> records, ReturnRequest request)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outcomes = new ReturnOutcome?[records.Count];
        var pending = new List<(int Index, string Target)>();
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            if (!disk.Exists(record.Current))
            {
                outcomes[i] = Vanished(record, request.Reclaim);
                continue;
            }
            var directory = request.ToUserDesktop
                ? Path.TrimEndingDirectorySeparator(request.UserDesktop)
                : BoxPlan.ReturnDirectory(record.Original, request.DesktopDirs, request.UserDesktop);
            var name = BoxPlan.NameOf(record.Original);
            var natural = Path.Combine(directory, name);
            if (!reserved.Contains(natural) && disk.Exists(natural) && disk.SameContent(record.Current, natural) && TryRecycle(record.Current))
            {
                // Masaüstünde aynısı zaten var (ör. güncelleme kısayolu yeniden oluşturdu): "ad (1)" kopyası çıkmaz.
                reserved.Add(natural);
                Close(record, natural, request.Reclaim);
                outcomes[i] = new ReturnOutcome(record, ReturnState.Absorbed, natural);
                continue;
            }
            var target = FileMover.UniquePath(directory, name, reserved, disk.Exists);
            reserved.Add(target);
            outcomes[i] = MoveBack(record, target, request.Reclaim, out var denied);
            if (denied && BoxPlan.IsDirectlyIn(target, request.PublicDesktops))
            {
                if (request.Interactive) pending.Add((i, target));
                else outcomes[i] = new ReturnOutcome(record, ReturnState.Waiting, target, BoxFailure.Denied);
            }
        }

        if (pending.Count > 0)
        {
            // Ortak masaüstüne dönecekler (yalnızca yönetici yazar): Windows'un onayıyla, hepsi birlikte.
            var moves = pending.Select(p => (records[p.Index].Current, p.Target)).ToList();
            var (go, dropped) = Guard(moves);
            var marked = new List<(int Index, string Target)>();
            foreach (var p in pending)
            {
                var record = records[p.Index];
                if (dropped.Contains((record.Current, p.Target)))
                {
                    outcomes[p.Index] = new ReturnOutcome(record, ReturnState.Failed, null, BoxFailure.Other);
                    continue;
                }
                try
                {
                    log.MarkReturning(record.Id, p.Target);
                    marked.Add(p);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    outcomes[p.Index] = new ReturnOutcome(record, ReturnState.Failed, null, BoxFailure.Other, ex.Message);
                }
            }
            var failure = marked.Count > 0 ? Elevate(go.Where(m => marked.Any(p => records[p.Index].Current == m.From)).ToList()) : BoxFailure.None;
            foreach (var (index, target) in marked)
            {
                var record = records[index];
                if (disk.Exists(target) && !disk.Exists(record.Current))
                {
                    Close(record, target, request.Reclaim);
                    outcomes[index] = new ReturnOutcome(record, ReturnState.Restored, target);
                    continue;
                }
                log.ClearReturning(record.Id);
                if (failure is BoxFailure.Declined or BoxFailure.None)
                {
                    // Windows izin vermedi: öğe kullanıcının kendi masaüstüne konur (kayıt kapanır, bir daha sorulmaz).
                    var fallback = FileMover.UniquePath(Path.TrimEndingDirectorySeparator(request.UserDesktop), BoxPlan.NameOf(record.Original),
                        reserved, disk.Exists);
                    reserved.Add(fallback);
                    var moved = MoveBack(record, fallback, request.Reclaim, out _);
                    outcomes[index] = moved.State == ReturnState.Restored ? moved with { State = ReturnState.FellBack } : moved;
                }
                else outcomes[index] = new ReturnOutcome(record, ReturnState.Failed, null, failure);
            }
        }

        var results = outcomes.Select((o, i) => o ?? new ReturnOutcome(records[i], ReturnState.Failed, null, BoxFailure.Other)).ToList();
        // Boşalan kutu klasörleri (ve kökleri) kalmasın; içinde kullanıcının koyduğu bir şey varsa dokunulmaz. Her kayıt kendi
        // kökünü kullanır: masaüstü sonradan taşındıysa eski NestDesk klasörü de temizlenir.
        foreach (var folder in results.Where(r => r.State is not (ReturnState.Waiting or ReturnState.Failed))
                     .Select(r => (Folder: BoxPlan.ParentOf(r.Record.Current), Root: BoxPlan.RootOf(r.Record.Current)))
                     .Where(x => x.Folder is not null && x.Root is not null).Distinct())
            disk.RemoveIfEmpty(folder.Folder!, folder.Root!);
        return results;
    }

    /// <summary>Kaydın NestDesk'teki öğesi yok: dışarıda geri taşınmış, yarıda kalmış ya da silinmiş. Dosyaya dokunulmaz.</summary>
    private ReturnOutcome Vanished(BoxMove record, bool reclaim)
    {
        if (record.ReturningTo is { } returning && disk.Exists(returning))
        {
            Close(record, returning, reclaim);
            return new ReturnOutcome(record, ReturnState.AlreadyThere, returning);
        }
        if (disk.Exists(record.Original))
        {
            Close(record, record.Original, reclaim: false);
            Restored?.Invoke(record.Original, record.Current);
            return new ReturnOutcome(record, ReturnState.AlreadyThere, record.Original);
        }
        // NestDesk klasörünün bulunduğu yere şu an hiç ulaşılamıyor (çıkarılmış sürücü, ağ): öğe silinmiş sayılmaz, kayıt açık kalır.
        if (BoxPlan.RootUnavailable(record.Current, disk.Exists))
            return new ReturnOutcome(record, ReturnState.Failed, null, BoxFailure.Unavailable);
        log.MarkReturned(record.Id, record.Original, reclaim: false);
        return new ReturnOutcome(record, ReturnState.NotFound);
    }

    /// <summary>Hedef önce yazılır, sonra taşınır. <paramref name="denied"/>: izin hatası (ortak masaüstüne dönüş).</summary>
    private ReturnOutcome MoveBack(BoxMove record, string target, bool reclaim, out bool denied)
    {
        denied = false;
        try { log.MarkReturning(record.Id, target); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ReturnOutcome(record, ReturnState.Failed, null, BoxFailure.Other, ex.Message);
        }
        try
        {
            disk.Move(record.Current, target);
        }
        catch (Exception ex) when (IsDiskError(ex))
        {
            log.ClearReturning(record.Id);
            denied = ex is UnauthorizedAccessException;
            return new ReturnOutcome(record, ReturnState.Failed, null, Classify(ex), ex.Message);
        }
        Close(record, target, reclaim);
        return new ReturnOutcome(record, ReturnState.Restored, target);
    }

    /// <summary>Öğe masaüstünde: kurallar ona hemen dokunmasın, sonra kayıt kapanır (yazılamasa da bellekte kapanır).</summary>
    private void Close(BoxMove record, string restored, bool reclaim)
    {
        if (reclaim) Restored?.Invoke(restored, record.Current);
        log.MarkReturned(record.Id, restored, reclaim);
    }

    private bool TryRecycle(string path)
    {
        try
        {
            disk.Recycle(path);
            return !disk.Exists(path);
        }
        catch (Exception ex) when (IsDiskError(ex) || ex is OperationCanceledException)
        {
            return false;
        }
    }

    // ---------------------------------------------------------------------------------------------------- yönetici onayı

    /// <summary>
    /// Yönetici onaylı toplu taşımaya girebilecekler: kaynağı ve hedefi işte tek, hedefi boş olanlar (Windows'un taşıması
    /// "Tümüne evet" ile çalışır; böylece hiçbir dosyanın üzerine yazılamaz). Ötekiler <c>Dropped</c>.
    /// </summary>
    public (List<(string From, string To)> Go, HashSet<(string, string)> Dropped) Guard(IReadOnlyList<(string From, string To)> moves)
    {
        var sources = moves.GroupBy(m => m.From, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targets = moves.GroupBy(m => m.To, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var go = new List<(string From, string To)>();
        var dropped = new HashSet<(string, string)>();
        foreach (var move in moves)
        {
            if (sources.Contains(move.From) || targets.Contains(move.To) || disk.Exists(move.To)) dropped.Add(move);
            else go.Add(move);
        }
        return (go, dropped);
    }

    /// <summary>Toplu yönetici onaylı taşıma; vazgeçildiyse Declined, başka hatada Other, yoksa None.</summary>
    private BoxFailure Elevate(IReadOnlyList<(string From, string To)> moves)
    {
        if (moves.Count == 0) return BoxFailure.None;
        try
        {
            disk.MoveElevated(moves);
            return BoxFailure.None;
        }
        catch (OperationCanceledException)
        {
            AppLog.Info("box", "elevated-move-cancelled");
            return BoxFailure.Declined;
        }
        catch (Exception ex) when (IsDiskError(ex))
        {
            AppLog.Warn("box", "elevated-move-failed", ex);
            return BoxFailure.Other;
        }
    }

    private static bool IsDiskError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    private static BoxFailure Classify(Exception ex) =>
        FileLocks.IsSharingViolation(ex) ? BoxFailure.Locked
        : ex is UnauthorizedAccessException ? BoxFailure.Denied
        : BoxFailure.Other;
}
