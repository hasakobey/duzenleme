using System.IO;
using System.Threading.Tasks;
using Duzenleme.Core;
using Duzenleme.Views;

namespace Duzenleme.Widgets;

/// <summary>
/// "Kutulara eklediklerim masaüstünden kalksın": kısayol kutusuna eklenen masaüstü öğesi görünür, sıradan bir klasöre
/// (masaüstünün yanındaki NestDesk\&lt;kutu&gt;) taşınır; kutu yeni yolu gösterir, taşıma kaydedilir ve geri alınabilir.
/// Kutudan ya da kutunun kendisi kaldırılınca öğe masaüstüne döner; kutu geri gelirse yeniden taşınır.
/// Dosya işleri arka planda, sırayla (tek kuyruk) yapılır; kutuların ayarları yalnızca arayüz iş parçacığında değişir.
/// Gizli dosya özniteliği kullanılmaz: Store (MSIX) sürümünün kaldırma adımı yoktur, gizlenen dosyalar görünmez kalırdı.
/// </summary>
internal static class BoxMover
{
    /// <summary>Taşıma ya da geri koyma bitti, kutuların yolları güncellendi (arayüz iş parçacığında).</summary>
    public static event Action? Changed;

    /// <summary>Kip açık mı? Bölmeler masaüstünü yönetirken (Windows simgeleri zaten gizli) öğeler taşınmaz.</summary>
    public static bool Active => AppHost.Settings.BoxItemsLeaveDesktop && !AppHost.Settings.FencesReplaceIcons;

    /// <summary>Taşınan öğelerin kök klasörü (ör. C:\Users\ad\NestDesk; test örneğinde --desktop klasörünün yanında).</summary>
    public static string Root =>
        BoxPlan.RootFor(AppHost.DesktopDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>NestDesk klasöründe duran (kutuya taşınmış) öğe sayısı.</summary>
    public static int MovedCount => AppHost.BoxMoves.ActiveCount;

    /// <summary>Bu kutu öğesi masaüstünden taşınmış mı (NestDesk klasöründe mi)?</summary>
    public static bool IsMoved(string path) => AppHost.BoxMoves.IsMoved(path);

    /// <summary>
    /// Kutulardaki masaüstü öğeleri (kip açılınca taşınabilecekler; diske bakılmaz). Ortak masaüstündekiler de: ayar kapalıysa
    /// taşınmazlar ama bildirim onları "Masaüstünden kaldır" ile sunar (çoğu uygulamanın kısayolu oradadır).
    /// </summary>
    public static List<(WidgetConfig Box, List<string> Paths)> DesktopItemsInBoxes()
    {
        var dirs = AppHost.DesktopDirectories;
        var result = new List<(WidgetConfig, List<string>)>();
        foreach (var box in Launchers())
        {
            var pinned = BoxPlan.PinnedDesktopPaths([box], dirs);
            if (pinned.Count > 0) result.Add((box, pinned.ToList()));
        }
        return result;
    }

    /// <summary>Yalnızca bu kutunun gösterdiği taşınmış öğe sayısı (kutu kaldırılınca masaüstüne dönecekler).</summary>
    public static int MovedOnlyIn(WidgetConfig box)
    {
        if (box.Kind != WidgetKind.Launcher || MovedCount == 0) return 0;
        var others = BoxPlan.Referenced(AppHost.Settings.Widgets.Where(w => w.Id != box.Id));
        return BoxPlan.Referenced([box]).Count(p => !others.Contains(p) && IsMoved(p));
    }

    private static IEnumerable<WidgetConfig> Launchers() => AppHost.Settings.Widgets.Where(w => w.Kind == WidgetKind.Launcher);

    // --- Tek kuyruk: işler sırayla; her biri arayüzde başlar, diski arka planda yapar, sonucu arayüzde uygular. ---
    private static Task _tail = Task.CompletedTask;

    private static void Enqueue(Func<Task> job) => _tail = Chain(_tail, job);

    private static async Task Chain(Task previous, Func<Task> job)
    {
        await previous.ConfigureAwait(false);
        if (System.Windows.Application.Current?.Dispatcher is not { } dispatcher) return;
        try
        {
            // İş arayüz iş parçacığında başlar; içindeki "await Task.Run" sonrası da oraya döner.
            await dispatcher.InvokeAsync(job).Task.Unwrap().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Bir işin hatası kuyruğu durdurmasın.
            DebugLog.Write($"kutu işi: {ex}");
            AppLog.Error("box", "job-failed", ex);
        }
    }

    // ---------------------------------------------------------------- taşıma

    private sealed record ClaimRequest(WidgetConfig Box, string Folder, List<string> Paths);

    /// <param name="Pending">Ortak masaüstündeki öğe izin yüzünden taşınamadı: yönetici onayıyla toplu taşınacak.</param>
    private sealed record ClaimResult(string Original, string? Current, BoxLinkReason Reason, string? Error, BoxMove? Pending = null);

    /// <summary>
    /// Kutudaki öğelerden masaüstünde olanları kutunun klasörüne taşır (kip açıksa). notify: sonucu bildir ("Geri al" ile).
    /// justAdded: öğeler az önce eklendi; "Geri al" onları kutudan da çıkarır (yoksa yalnızca masaüstüne geri koyar).
    /// </summary>
    public static void Claim(WidgetConfig box, IReadOnlyCollection<string> paths, bool justAdded, bool notify = true) =>
        ClaimMany([(box, paths.ToList())], notify, justAdded);

    /// <summary>Birden çok kutunun öğelerini tek işte taşır (kip açılırken "kutudakiler de taşınsın").</summary>
    public static void ClaimMany(IReadOnlyList<(WidgetConfig Box, List<string> Paths)> items, bool notify = true, bool justAdded = false)
    {
        if (items.All(i => i.Paths.Count == 0)) return;
        Enqueue(async () =>
        {
            if (!Active) return;
            var root = Root;
            var requests = new List<ClaimRequest>();
            foreach (var (box, paths) in items)
            {
                // Kutu bu arada kaldırıldıysa ya da öğe kutudan çıkarıldıysa taşınmaz.
                if (!AppHost.Settings.Widgets.Contains(box)) continue;
                var referenced = BoxPlan.Referenced([box]);
                var wanted = paths.Where(referenced.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (wanted.Count == 0) continue;
                // Klasör adı ilk taşımada başlıktan alınır ve sonra değişmez (elle düzenlenmiş ayar da güvenli bir ada iner).
                box.BoxFolder = BoxPlan.FolderNameFor(box.BoxFolder ?? box.Title);
                var folder = Path.Combine(root, box.BoxFolder);
                // Klasörü biz açıyoruz; masaüstünün içindeyse "simge ver" balonu çıkmasın (olağan durumda masaüstünün yanında).
                if (AppHost.DesktopDirectories.Any(d => IsUnder(folder, d))) AppHost.MarkQuietFolder(folder);
                requests.Add(new ClaimRequest(box, folder, wanted));
            }
            if (requests.Count == 0) return;

            var userDesktop = AppHost.DesktopDirectory;
            var publics = AppHost.DesktopDirectories.Skip(1).ToList();
            var includePublic = AppHost.Settings.BoxIncludesPublicDesktop;
            var used = UsedFolders();
            var log = AppHost.BoxMoves;

            ClaimResult MoveOne(ClaimRequest request, string path)
            {
                var reason = BoxPlan.Decide(path, BoxFiles.Attributes(path), userDesktop, publics, includePublic, used);
                if (reason != BoxLinkReason.None) return new ClaimResult(path, null, reason, null);
                var record = new BoxMove { WidgetId = request.Box.Id, Original = path };
                try
                {
                    record.Current = BoxFiles.TargetIn(request.Folder, path);
                    // Önce kayıt, sonra taşıma: arada kesilirse açılışta uzlaştırma kutuyu yeni yola bağlar.
                    log.Add(record);
                    try { BoxFiles.Move(path, record.Current); }
                    catch
                    {
                        log.Remove(record.Id);
                        throw;
                    }
                    return new ClaimResult(path, record.Current, BoxLinkReason.None, null);
                }
                catch (UnauthorizedAccessException) when (record.Current.Length > 0 && IsOnAny(path, publics))
                {
                    // Ortak masaüstü yalnızca yöneticiyle değişir: aşağıda Windows'un onayıyla, hepsi birlikte taşınır.
                    return new ClaimResult(path, null, BoxLinkReason.None, null, record);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    DebugLog.Write($"kutuya taşınamadı {path}: {ex.Message}");
                    if (FileLocks.IsSharingViolation(ex)) AppLog.Count("box.locked");
                    else AppLog.Warn("box", "move-to-box-failed", ex);
                    return new ClaimResult(path, null, BoxLinkReason.None, ex is UnauthorizedAccessException ? L.T("izin yok") : ex.Message);
                }
            }

            var results = await Task.Run(() =>
                requests.SelectMany(request => request.Paths.Select(path => (Request: request, Result: MoveOne(request, path)))).ToList());

            if (results.Any(x => x.Result.Pending is not null))
            {
                var pending = results.Select(x => x.Result.Pending).OfType<BoxMove>().ToList();
                // Önce kayıt, sonra taşıma (yukarıdaki gibi); taşınmayanların kaydı silinir.
                var done = await MoveWithPermission(pending.Select(r => (r.Original, r.Current)).ToList(),
                    before: () => pending.ForEach(log.Add),
                    failed: moves => pending.Where(r => moves.Contains(r.Original)).ToList().ForEach(r => log.Remove(r.Id)));
                results = results.Select(x => x.Result.Pending is not { } record ? x
                    : (x.Request, done.Contains(record.Original)
                        ? new ClaimResult(record.Original, record.Current, BoxLinkReason.None, null)
                        : new ClaimResult(record.Original, null, BoxLinkReason.None, L.T("izin verilmedi")))).ToList();
            }

            var moved = results.Where(x => x.Result.Current is not null).ToList();
            if (moved.Count > 0)
            {
                Replace(moved.Select(x => (x.Result.Original, x.Result.Current!)));
                AppHost.SaveSettings();
                AppHost.RefreshPinnedPaths();
                Changed?.Invoke();
            }
            if (notify) NotifyClaimed(results, justAdded);
        });
    }

    private static void NotifyClaimed(List<(ClaimRequest Request, ClaimResult Result)> results, bool justAdded)
    {
        var moved = results.Where(x => x.Result.Current is not null).ToList();
        var failed = results.Where(x => x.Result.Error is not null).ToList();
        var used = results.Where(x => x.Result.Reason == BoxLinkReason.UsedFolder).ToList();
        var publicSkipped = results.Where(x => x.Result.Reason == BoxLinkReason.PublicDesktop).ToList();

        var parts = new List<string>();
        if (moved.Count > 0)
        {
            var folders = moved.Select(x => $"{BoxPlan.RootFolderName}\\{Path.GetFileName(x.Request.Folder)}").Distinct().ToList();
            // Birden çok kutunun klasörüne gittiyse ortak kök (NestDesk) söylenir.
            var where = folders.Count == 1 ? folders[0] : BoxPlan.RootFolderName;
            parts.Add(moved.Count == 1
                ? L.F("\"{0}\" masaüstünden kalktı; {1} klasörüne taşındı, kutuda duruyor.", TileItem.DisplayName(moved[0].Result.Original), where)
                : L.P(moved.Count, "{0} öğe masaüstünden kalktı; {1} klasörüne taşındı, kutuda duruyor.", where));
        }
        if (failed.Count > 0)
            parts.Add(failed.Count == 1
                ? L.F("\"{0}\" taşınamadı ({1}); kutuya bağlantı olarak eklendi.", TileItem.DisplayName(failed[0].Result.Original), failed[0].Result.Error)
                : L.P(failed.Count, "{0} öğe taşınamadı (kullanımda ya da izin yok); kutuya bağlantı olarak eklendi."));
        foreach (var x in used)
            parts.Add(L.F("\"{0}\" klasörünü bir kural ya da bölme kullandığı için masaüstünde kaldı.", TileItem.DisplayName(x.Result.Original)));
        if (publicSkipped.Count > 0)
        {
            parts.Add(publicSkipped.Count == 1
                ? L.F("\"{0}\" bu bilgisayardaki tüm hesapların ortak masaüstünde, bu yüzden masaüstünde kaldı. Kaldırmak için yönetici izni gerekir.",
                    TileItem.DisplayName(publicSkipped[0].Result.Original))
                : L.P(publicSkipped.Count, "{0} öğe bu bilgisayardaki tüm hesapların ortak masaüstünde, bu yüzden masaüstünde kaldı. Kaldırmak için yönetici izni gerekir."));
        }
        if (parts.Count == 0) return;

        var text = string.Join(" ", parts);
        if (publicSkipped.Count > 0 && moved.Count == 0)
        {
            // "Masaüstünden kaldır": bundan sonra ortak masaüstündekiler de taşınır (Windows her seferinde onay ister).
            var items = publicSkipped.GroupBy(x => x.Request.Box).Select(g => (g.Key, g.Select(x => x.Result.Original).ToList())).ToList();
            Notice.Show(text, NoticeKind.Info, L.T("Masaüstünden kaldır"), () =>
            {
                AppHost.Settings.BoxIncludesPublicDesktop = true;
                AppHost.SaveSettings();
                ClaimMany(items, notify: true, justAdded);
            });
        }
        else if (moved.Count > 0)
        {
            var undo = moved.Select(x => (x.Request.Box, x.Result.Current!)).ToList();
            // Az önce eklenenler kutudan da çıkar; zaten kutuda olanlar (kip açılırken taşınanlar) yalnızca masaüstüne döner.
            Action action = justAdded ? () => UndoAdd(undo) : () => Return(undo.Select(u => u.Item2).ToList());
            Notice.Show(text, failed.Count > 0 ? NoticeKind.Warning : NoticeKind.Success, L.T("Geri al"), action,
                L.T("Geri almak için buraya tıkla."));
        }
        else Notice.Show(text, failed.Count > 0 ? NoticeKind.Warning : NoticeKind.Info);
    }

    /// <summary>Az önceki eklemeyi geri alır: öğeler kutudan çıkar ve masaüstüne döner.</summary>
    private static void UndoAdd(List<(WidgetConfig Box, string Current)> items)
    {
        foreach (var group in items.GroupBy(i => i.Box))
            foreach (var tab in group.Key.Tabs)
                tab.Items.RemoveAll(p => group.Any(i => string.Equals(i.Current, p, StringComparison.OrdinalIgnoreCase)));
        AppHost.SaveSettings();
        AppHost.RefreshPinnedPaths();
        Changed?.Invoke();
        Reconcile();
    }

    // ---------------------------------------------------------------- geri koyma

    /// <param name="PendingTarget">Ortak masaüstüne dönüş izin yüzünden olmadı: yönetici onayıyla bu yola taşınacak.</param>
    private sealed record ReturnResult(BoxMove Record, string? Restored, string? Error, string? PendingTarget = null);

    /// <summary>"Masaüstüne geri koy": öğeler masaüstüne döner, kutuda kalır (kutu masaüstündeki yolu gösterir).</summary>
    public static void Return(IReadOnlyCollection<string> currentPaths, bool notify = true) =>
        Enqueue(async () =>
        {
            var set = new HashSet<string>(currentPaths, StringComparer.OrdinalIgnoreCase);
            var records = AppHost.BoxMoves.Snapshot().Where(r => r.Active && set.Contains(r.Current)).ToList();
            if (records.Count == 0) return;
            // Kutuda kalacaklar: masaüstüne döndükleri anda kurallarla taşınmasınlar (küme dönüşten sonra kesinleşir).
            AppHost.Organizer.Pinned = new HashSet<string>(AppHost.Organizer.Pinned.Concat(records.Select(r => r.Original)),
                StringComparer.OrdinalIgnoreCase);
            var results = await ReturnInBackground(records, reclaim: false);
            ApplyReturns(results);
            if (notify) NotifyReturned(results);
        });

    /// <summary>Kutulara taşınan her şeyi masaüstüne geri koyar (kip kapatılırken istenirse).</summary>
    public static void ReturnAll() =>
        Return(AppHost.BoxMoves.Snapshot().Where(r => r.Active).Select(r => r.Current).ToList());

    /// <summary>
    /// Kayıtları kutularla uzlaştırır (kutu/öğe/sekme kaldırıldı, "Geri al", düzen uygulandı, açılış): hiçbir kutunun
    /// göstermediği taşınmış öğe masaüstüne döner; kutu geri geldiyse öğeleri yeniden bağlanır (kip açıksa yeniden taşınır).
    /// </summary>
    public static void Reconcile()
    {
        if (AppHost.BoxMoves is null) return;
        Enqueue(async () =>
        {
            var records = AppHost.BoxMoves.Snapshot();
            if (records.Count == 0) return;
            var referenced = BoxPlan.Referenced(AppHost.Settings.Widgets);
            var journal = AppHost.Journal;
            // Geri konan öğe masaüstünden bir kuralla taşınmışsa (ör. önceki sürümde) kutu onu geçmişten bulur.
            var plan = await Task.Run(() => BoxPlan.Reconcile(records, referenced, BoxFiles.Exists, BoxPlan.MovedByRule(journal.Snapshot())));
            var results = plan.Return.Count > 0 ? await ReturnInBackground(plan.Return, reclaim: true) : [];
            ApplyReturns(results);
            if (plan.Remap.Count == 0) return;

            Replace(plan.Remap.Select(r => (r.From, r.To)));
            AppHost.SaveSettings();
            AppHost.RefreshPinnedPaths();
            Changed?.Invoke();
            // Kutu geri geldi (Geri al, kayıtlı düzen): masaüstüne dönen öğeleri kip açıksa yeniden kutuya taşı.
            var reclaim = plan.Remap.Where(r => r.Reclaim).Select(r => r.To).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (reclaim.Count == 0 || !Active) return;
            var items = Launchers()
                .Select(box => (box, BoxPlan.Referenced([box]).Where(reclaim.Contains).ToList()))
                .Where(x => x.Item2.Count > 0).ToList();
            ClaimMany(items, notify: false);
        });
    }

    private static async Task<List<ReturnResult>> ReturnInBackground(List<BoxMove> records, bool reclaim)
    {
        var desktop = AppHost.DesktopDirectory;
        var publics = AppHost.DesktopDirectories.Skip(1).ToList();
        var root = Root;
        var log = AppHost.BoxMoves;
        var organizer = AppHost.Organizer;
        var results = await Task.Run(() => records.Select(record => ReturnOne(record)).ToList());

        // Ortak masaüstüne dönecekler (yalnızca yönetici yazabilir): Windows'un onayıyla, hepsi birlikte.
        if (results.Any(r => r.PendingTarget is not null))
        {
            var pending = results.Where(r => r.PendingTarget is not null).ToList();
            var done = await MoveWithPermission(pending.Select(r => (r.Record.Current, r.PendingTarget!)).ToList());
            results = await Task.Run(() => results.Select(r =>
            {
                if (r.PendingTarget is not { } target) return r;
                if (!done.Contains(r.Record.Current)) return r with { Error = L.T("izin verilmedi"), PendingTarget = null };
                log.MarkReturned(r.Record.Id, target, reclaim);
                if (reclaim) KeepOnDesktop(organizer, target, r.Record.Current);
                return new ReturnResult(r.Record, target, null);
            }).ToList());
        }

        await Task.Run(() =>
        {
            // Boşalan kutu klasörleri (ve NestDesk klasörü) kalmasın; içinde kullanıcının koyduğu bir şey varsa dokunulmaz.
            foreach (var folder in results.Where(r => r.Restored is not null).Select(r => BoxPlan.ParentOf(r.Record.Current))
                         .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
                BoxFiles.RemoveIfEmpty(folder, root);
        });
        return results;

        ReturnResult ReturnOne(BoxMove record)
        {
            try
            {
                if (!BoxFiles.Exists(record.Current))
                {
                    // Kullanıcı NestDesk klasöründen silmiş ya da taşımış: kayıt kapanır, dosyaya dokunulmaz.
                    log.MarkReturned(record.Id, record.Original, reclaim: false);
                    return new ReturnResult(record, null, NotFound);
                }
                // Kutuda kalan öğe masaüstünde de kurallarla taşınmaz (DesktopOrganizer.Pinned, izleyici 2 sn bekler).
                var restored = BoxFiles.MoveBack(record.Current, record.Original, desktop);
                log.MarkReturned(record.Id, restored, reclaim);
                if (reclaim) KeepOnDesktop(organizer, restored, record.Current);
                return new ReturnResult(record, restored, null);
            }
            catch (UnauthorizedAccessException) when (IsOnAny(record.Original, publics))
            {
                var directory = BoxPlan.ParentOf(record.Original)!;
                return new ReturnResult(record, null, null,
                    FileMover.UniquePath(directory, Path.GetFileName(Path.TrimEndingDirectorySeparator(record.Original))));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                DebugLog.Write($"masaüstüne geri konamadı {record.Current}: {ex.Message}");
                if (FileLocks.IsSharingViolation(ex)) AppLog.Count("box.locked");
                else AppLog.Warn("box", "return-failed", ex);
                return new ReturnResult(record, null, ex is UnauthorizedAccessException ? L.T("izin yok") : ex.Message);
            }
        }
    }

    /// <summary>
    /// Kutudan (ya da kutu kaldırıldığı için) masaüstüne dönen öğe artık hiçbir kutuda değil, ama kullanıcı onu bilerek masaüstüne
    /// geri aldı: bildirim "masaüstüne geri konuyor" der, kutunun "Geri al"ı onu orada arar. Otomatik taşıma açıkken bir kural
    /// onu hemen taşıyacaksa bölmedeki "Masaüstüne geri taşı" gibi "geri alındı" yazılır ve bir daha otomatik taşınmaz. İzleyici
    /// yeni dosyaya ~2 sn sonra bakar; kayıt ondan önce, eşzamanlı yazılır. Taşıma kapalıyken dokunulmaz: sonra açılınca her
    /// masaüstü dosyası gibi taşınır (kutu geri gelirse taşıma geçmişinden bulunur). Arka planda çağrılır.
    /// </summary>
    private static void KeepOnDesktop(DesktopOrganizer organizer, string restored, string boxPath)
    {
        try
        {
            if (AppHost.Settings.Paused || !organizer.WouldMove(restored)) return;
            AppHost.Journal.Add(new MoveEntry { Source = restored, Destination = boxPath, Undone = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DebugLog.Write($"geri konan öğe korunamadı {restored}: {ex.Message}");
            AppLog.Warn("box", "keep-on-desktop-failed", ex);
        }
    }

    /// <summary>İç işaret: öğe NestDesk klasöründe artık yok (kullanıcı silmiş/taşımış); bildirimde hata sayılmaz, gösterilmez.</summary>
    private const string NotFound = "\u0000not-found";

    private static void ApplyReturns(List<ReturnResult> results)
    {
        var restored = results.Where(r => r.Restored is not null).ToList();
        if (restored.Count > 0)
        {
            // Hâlâ kutuda duruyorsa kutu masaüstündeki yolu göstersin.
            Replace(restored.Select(r => (r.Record.Current, r.Restored!)));
            AppHost.SaveSettings();
            AppHost.RefreshPinnedPaths();
        }
        if (results.Count > 0) Changed?.Invoke();
    }

    private static void NotifyReturned(List<ReturnResult> results)
    {
        var done = results.Count(r => r.Restored is not null);
        var failed = results.Where(r => r.Restored is null && r.Error != NotFound).ToList();
        var parts = new List<string>();
        if (done > 0)
            parts.Add(done == 1
                ? L.F("\"{0}\" masaüstüne geri kondu.", TileItem.DisplayName(results.First(r => r.Restored is not null).Restored!))
                : L.P(done, "{0} öğe masaüstüne geri kondu."));
        if (failed.Count > 0)
            parts.Add(L.P(failed.Count, "{0} öğe geri konamadı ({1}); {2} klasöründe duruyor.", failed[0].Error, BoxPlan.RootFolderName));
        if (parts.Count > 0) Notice.Show(string.Join(" ", parts), failed.Count > 0 ? NoticeKind.Warning : NoticeKind.Success);
    }

    // ---------------------------------------------------------------- yardımcılar

    /// <summary>
    /// İzin isteyen taşımalar (ortak masaüstü): Windows'un onay penceresiyle tek işlemde. Kaynak yolları gerçekten taşınanlar
    /// döner (onay verilmezse boş). <paramref name="before"/> taşımadan önce, <paramref name="failed"/> taşınmayan kaynaklarla
    /// arka planda çağrılır.
    /// </summary>
    private static async Task<HashSet<string>> MoveWithPermission(List<(string From, string To)> moves, Action? before = null,
        Action<HashSet<string>>? failed = null)
    {
        await Task.Run(() =>
        {
            foreach (var folder in moves.Select(m => Path.GetDirectoryName(m.To)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
                Directory.CreateDirectory(folder);
            before?.Invoke();
        });
        try
        {
            await Desktop.ShellFileOperations.RunSta(() => Desktop.ShellFileOperations.MoveManyWithShell(moves, IntPtr.Zero));
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or UnauthorizedAccessException)
        {
            DebugLog.Write($"izinli taşıma: {ex.Message}");
            if (ex is OperationCanceledException) AppLog.Info("box", "elevated-move-cancelled");
            else AppLog.Warn("box", "elevated-move-failed", ex);
        }
        return await Task.Run(() =>
        {
            var done = moves.Where(m => BoxFiles.Exists(m.To) && !BoxFiles.Exists(m.From)).Select(m => m.From)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var notDone = moves.Select(m => m.From).Where(f => !done.Contains(f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (notDone.Count > 0) failed?.Invoke(notDone);
            return done;
        });
    }

    private static bool IsOnAny(string path, IEnumerable<string> directories) =>
        BoxPlan.ParentOf(path) is { } parent && directories.Any(d =>
            string.Equals(Path.TrimEndingDirectorySeparator(d), parent, StringComparison.OrdinalIgnoreCase));

    /// <summary>Kutulardaki yolları değiştirir (bütün kutular, bütün sekmeler).</summary>
    private static void Replace(IEnumerable<(string From, string To)> changes)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (from, to) in changes) map[from] = to;
        foreach (var box in Launchers())
        {
            foreach (var tab in box.Tabs)
            {
                for (var i = 0; i < tab.Items.Count; i++)
                    if (map.TryGetValue(tab.Items[i], out var to)) tab.Items[i] = to;
                // Aynı öğe iki kez görünmesin (ör. taşınmış hâli zaten sekmedeydi).
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                tab.Items.RemoveAll(p => !seen.Add(p));
            }
            // Öğenin kullanıcı adı ve simgesi yeni yolla gider.
            foreach (var (from, to) in map) ItemLooks.Move(box, from, to);
        }
    }

    /// <summary>Kuralların ve klasör bölmelerinin kullandığı masaüstü klasörleri: kutuya alınınca taşınmaz.</summary>
    private static List<string> UsedFolders() =>
        AppHost.Settings.Rules.Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.TargetFolder)).Select(r => r.TargetFolder.Trim())
            .Concat(AppHost.Settings.Widgets
                .Where(w => w.Kind == WidgetKind.Fence && w.Filter == DesktopFilter.None && !string.IsNullOrWhiteSpace(w.FolderName))
                .Select(w => w.FolderName!))
            .ToList();

    private static bool IsUnder(string path, string directory)
    {
        var dir = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return path.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
    }
}
