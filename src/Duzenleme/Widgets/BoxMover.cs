using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Views;

namespace Duzenleme.Widgets;

/// <summary>
/// "Kutulara eklediklerim masaüstünden kalksın": kısayol kutusuna (halkaya) eklenen masaüstü dosyası görünür, sıradan bir
/// klasöre (masaüstünün yanındaki NestDesk\&lt;kutu&gt;) taşınır; kutu yeni yolu gösterir, taşıma kaydedilir ve geri alınabilir.
/// Kutudan ya da kutunun kendisi kaldırılınca öğe masaüstüne döner; kutu geri gelirse yeniden taşınır. Klasörler taşınmaz.
/// Dosya işleri arka planda, sırayla (tek kuyruk) <see cref="BoxEngine"/> ile yapılır (karar <see cref="BoxPlan"/>'da, saf);
/// kutuların ayarları yalnızca arayüz iş parçacığında değişir. Kutular kimlikle bulunur (<see cref="Launchers"/>).
/// Ortak masaüstü için Windows'un yönetici onayı yalnızca kullanıcının bir eylemiyle istenir (ekleme, kaldırma, Geri al,
/// "Masaüstüne geri koy"); açılışta ve arka plandaki uzlaştırmada asla: o öğeler bir bildirimle bekler. Her başarısızlık
/// bir bildirimle söylenir ("Tekrar dene", "Klasörü aç"); açık olduğu için taşınamayan dosya sonra yeniden denenir.
/// Gizli dosya özniteliği kullanılmaz: Store (MSIX) sürümünün kaldırma adımı yoktur, gizlenen dosyalar görünmez kalırdı.
/// </summary>
internal static class BoxMover
{
    /// <summary>Taşıma ya da geri koyma bitti, kutuların yolları güncellendi (arayüz iş parçacığında).</summary>
    public static event Action? Changed;

    /// <summary>"Kutulara eklediklerim masaüstünden kalksın" seçili mi?</summary>
    public static bool On => AppHost.Settings.BoxItemsLeaveDesktop;

    /// <summary>Dosyalar taşınır mı? Bölmeler masaüstünü yönetirken (Windows simgeleri zaten gizli) öğeler taşınmaz.</summary>
    public static bool MovesFiles => On && !AppHost.Settings.FencesReplaceIcons;

    /// <summary>Eski adı: <see cref="MovesFiles"/>.</summary>
    public static bool Active => MovesFiles;

    /// <summary>Taşınan öğelerin kök klasörü (ör. C:\Users\ad\NestDesk; test örneğinde --desktop klasörünün yanında).</summary>
    public static string Root =>
        BoxPlan.RootFor(AppHost.DesktopDirectory, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>NestDesk klasöründe duran (kutuya taşınmış) öğe sayısı.</summary>
    public static int MovedCount => AppHost.BoxMoves.ActiveCount;

    /// <summary>Bu kutu öğesi masaüstünden taşınmış mı (NestDesk klasöründe mi)?</summary>
    public static bool IsMoved(string path) => AppHost.BoxMoves.IsMoved(path);

    /// <summary>
    /// Taşınan öğelerin bulunduğu kök klasör ("Klasörü aç"): masaüstü sonradan taşındıysa (OneDrive) öğelerin durduğu eski kök
    /// olabilir. Arka planda güncellenir; henüz bakılmadıysa <see cref="Root"/>.
    /// </summary>
    public static string FolderToOpen => _folderToOpen ?? Root;

    /// <summary>Kök klasör (ya da öğelerin durduğu eski kök) diskte var mı (arka planda güncellenir; Ayarlar'daki düğme için).</summary>
    public static bool RootExists { get; private set; }

    private static string? _folderToOpen;

    /// <summary>Ortak masaüstündekiler de taşınır mı? Store sürümü ortak masaüstüne hiç dokunmaz (kaldırınca geri konamaz).</summary>
    private static bool IncludePublic => AppHost.Settings.BoxIncludesPublicDesktop && !PackageInfo.IsPackaged;

    /// <summary>
    /// Kutulardaki masaüstü öğeleri (kip açılınca taşınabilecekler; diske bakılmaz). Ortak masaüstündekiler de: ayar kapalıysa
    /// taşınmazlar ama bildirim onları "Masaüstünden kaldır" ile sunar (çoğu uygulamanın kısayolu oradadır).
    /// </summary>
    public static List<(WidgetConfig Box, List<string> Paths)> DesktopItemsInBoxes()
    {
        var dirs = AppHost.DesktopDirectories;
        var result = new List<(WidgetConfig, List<string>)>();
        foreach (var box in Launchers.All(AppHost.Settings))
        {
            var pinned = BoxPlan.PinnedDesktopPaths([box], dirs);
            if (pinned.Count > 0) result.Add((box, pinned.ToList()));
        }
        return result;
    }

    /// <summary>
    /// Kip açılınca taşınacak kutu öğelerinin sayısı (soru metni için; diske bakılmaz): aynı öğe birden çok kutuda olsa da bir
    /// kez, ortak masaüstündekiler yalnızca izin verildiyse.
    /// </summary>
    public static int ClaimableCount(IEnumerable<(WidgetConfig Box, List<string> Paths)> items)
    {
        var publics = AppHost.DesktopDirectories.Skip(1).ToList();
        return items.SelectMany(i => i.Paths).Distinct(StringComparer.OrdinalIgnoreCase)
            .Count(p => IncludePublic || !BoxPlan.IsDirectlyIn(p, publics));
    }

    /// <summary>Yalnızca bu kutunun gösterdiği taşınmış öğe sayısı (kutu kaldırılınca masaüstüne dönecekler).</summary>
    public static int MovedOnlyIn(WidgetConfig box)
    {
        if (box.Kind != WidgetKind.Launcher || MovedCount == 0) return 0;
        var others = BoxPlan.Referenced(Launchers.All(AppHost.Settings).Where(w => w.Id != box.Id));
        return BoxPlan.Referenced([box]).Count(p => !others.Contains(p) && IsMoved(p));
    }

    private static bool IsRing(string? boxId) =>
        Launchers.Find(AppHost.Settings, boxId) is { } box && WidgetVariants.Is(box, WidgetVariants.Ring);

    private static IBoxDisk Disk => BoxDisk.Instance;

    private static BoxEngine Engine()
    {
        var organizer = AppHost.Organizer;
        return new BoxEngine(Disk, AppHost.BoxMoves) { Restored = (restored, boxPath) => KeepOnDesktop(organizer, restored, boxPath) };
    }

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

    /// <summary>
    /// Açılışta (widget'lar açılırken): kayıt dosyası kaybolduysa kutulardaki NestDesk öğelerinin kayıtları yeniden kurulur,
    /// kayıtlar kutularla uzlaşır (yarıda kalanlar, dışarıda değişenler; yönetici onayı istenmez), açık olduğu için
    /// taşınamamış öğeler yeniden denenir. Masaüstü değişince bekleyen denemeler yinelenir.
    /// </summary>
    public static void Startup()
    {
        if (AppHost.BoxMoves is null) return;
        Enqueue(AdoptJob);
        Reconcile(interactive: false);
        Enqueue(() =>
        {
            RetryClaims();
            return Task.CompletedTask;
        });
        AppHost.DesktopSnapshot.Changed += OnDesktopChanged;
        RefreshRootState();
    }

    /// <summary>Kayıtları kaybolmuş kutu öğeleri (NestDesk klasöründe, kaydı yok): kutudan çıkınca masaüstüne dönebilsinler.</summary>
    private static async Task AdoptJob()
    {
        var log = AppHost.BoxMoves;
        var items = Launchers.All(AppHost.Settings)
            .SelectMany(box => box.Tabs.Where(t => t?.Items is not null).SelectMany(t => t.Items).Select(path => (box.Id, path)))
            .ToList();
        if (items.Count == 0) return;
        var root = Root;
        var desktop = AppHost.DesktopDirectory;
        var records = log.Snapshot();
        var adopted = await Task.Run(() => BoxPlan.Adopt(records, items, root, desktop, Disk.Exists));
        if (adopted.Count == 0) return;
        try { await Task.Run(() => log.AddMany(adopted)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("box", "adopt-failed", ex);
            return;
        }
        AppLog.Info("box", "records-adopted", adopted.Count);
        Changed?.Invoke();
        RefreshRootState();
    }

    // ---------------------------------------------------------------- taşıma

    /// <param name="Tab">Öğelerin az önce eklendiği sekme ("Geri al" onları oradan çıkarır); bilinmiyorsa null.</param>
    private sealed record ClaimBatch(string BoxId, LauncherTab? Tab, List<string> Paths);

    /// <summary>"Geri al" için taşınan öğe: kutusu (kimlik), eklendiği sekme (nesne, yoksa sırası ve adı) ve yeni yolu.</summary>
    private sealed record AddedItem(string BoxId, LauncherTab? Tab, int TabIndex, string? TabName, string Current);

    /// <summary>
    /// Kutudaki öğelerden masaüstünde olanları kutunun klasörüne taşır (kip açıksa). notify: sonucu bildir ("Geri al" ile).
    /// justAdded: öğeler az önce <paramref name="tab"/> sekmesine eklendi; "Geri al" onları kutudan da çıkarır (yoksa yalnızca
    /// masaüstüne geri koyar).
    /// </summary>
    public static void Claim(WidgetConfig box, IReadOnlyCollection<string> paths, bool justAdded, LauncherTab? tab = null, bool notify = true)
    {
        if (paths.Count == 0) return;
        Enqueue(() => ClaimJob([new ClaimBatch(box.Id, tab, paths.ToList())], notify, justAdded, interactive: true));
    }

    /// <summary>
    /// Birden çok kutunun öğelerini tek işte taşır (kip açılırken "kutudakiler de taşınsın"). interactive: kullanıcı istedi
    /// (ortak masaüstü öğesi için Windows onay ister); false ise (kutu geri geldi, yeniden deneme) sorulmaz, bağlantı kalır.
    /// </summary>
    public static void ClaimMany(IReadOnlyList<(WidgetConfig Box, List<string> Paths)> items, bool notify = true, bool justAdded = false,
        bool interactive = true)
    {
        var batches = items.Where(i => i.Paths.Count > 0).Select(i => new ClaimBatch(i.Box.Id, null, i.Paths)).ToList();
        if (batches.Count == 0) return;
        Enqueue(() => ClaimJob(batches, notify, justAdded, interactive));
    }

    /// <param name="automatic">Kendiliğinden yeniden deneme (açık olduğu için taşınamayanlar): zamanlı deneme sınırlıdır.</param>
    private static async Task ClaimJob(List<ClaimBatch> batches, bool notify, bool justAdded, bool interactive, bool automatic = false)
    {
        if (!MovesFiles) return;
        var settings = AppHost.Settings;
        var root = Root;
        var inputs = new List<ClaimInput>();
        var tabs = new Dictionary<string, LauncherTab?>(StringComparer.Ordinal);
        foreach (var batch in batches)
        {
            // Kutu bu arada kaldırıldıysa ya da öğe kutudan çıkarıldıysa taşınmaz (kutu kimlikle bulunur: geri getirilen kopya da).
            if (Launchers.Find(settings, batch.BoxId) is not { } box) continue;
            var referenced = BoxPlan.Referenced([box]);
            var wanted = batch.Paths.Where(referenced.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (wanted.Count == 0) continue;
            // Klasör adı ilk taşımada başlıktan alınır ve sonra değişmez (elle düzenlenmiş ayar da güvenli bir ada iner); başka
            // bir kutunun klasörüyle karışmaz (çoğaltılan kutu, aynı adlı kutular).
            box.BoxFolder = !string.IsNullOrWhiteSpace(box.BoxFolder)
                ? BoxPlan.FolderNameFor(box.BoxFolder)
                : BoxPlan.UniqueFolderName(BoxPlan.FolderNameFor(BoxPlan.FolderTitleOf(box)),
                    Launchers.All(settings).Where(w => w.Id != box.Id).Select(w => w.BoxFolder));
            var folder = Path.Combine(root, box.BoxFolder);
            // Klasörü biz açıyoruz; masaüstünün içindeyse "simge ver" balonu çıkmasın (olağan durumda masaüstünün yanında).
            if (AppHost.DesktopDirectories.Any(d => IsUnder(folder, d))) AppHost.MarkQuietFolder(folder);
            inputs.Add(new ClaimInput(box.Id, folder, wanted));
            tabs.TryAdd(box.Id, batch.Tab);
        }
        if (inputs.Count == 0) return;

        var context = new BoxContext(AppHost.DesktopDirectory, AppHost.DesktopDirectories.Skip(1).ToList(), IncludePublic);
        var engine = Engine();
        var outcomes = await Task.Run(() =>
            engine.Claim(BoxPlan.PlanClaims(inputs, Disk.Attributes, context, Disk.Exists), allowElevation: interactive));

        var moved = outcomes.Where(o => o.Moved).ToList();
        if (moved.Count > 0) Launchers.ReplacePaths(settings, moved.Select(o => (o.Step.Original, o.Current!, false)).ToList());
        var retryChanged = NoteRetries(outcomes);
        if (moved.Count > 0 || retryChanged) AppHost.SaveSettings();
        if (moved.Count > 0)
        {
            AppHost.RefreshPinnedPaths();
            Changed?.Invoke();
            RefreshRootState();
        }
        if (outcomes.Any(o => o.Failure is BoxFailure.Locked or BoxFailure.Other)) ScheduleClaimRetry(automatic);
        if (notify) NotifyClaimed(outcomes, justAdded, tabs);
    }

    // Klasörlerin taşınmadığı bu oturumda söylendi mi?
    private static bool _folderNoticeShown;

    private static void NotifyClaimed(List<ClaimOutcome> outcomes, bool justAdded, IReadOnlyDictionary<string, LauncherTab?> tabs)
    {
        var moved = outcomes.Where(o => o.Moved).ToList();
        var failed = outcomes.Where(o => !o.Moved && o.Failure != BoxFailure.None).ToList();
        var folders = outcomes.Where(o => o.Step.Reason == BoxLinkReason.Folder).ToList();
        var publicSkipped = outcomes.Where(o => o.Step.Reason == BoxLinkReason.PublicDesktop).ToList();

        var parts = new List<string>();
        if (moved.Count > 0)
        {
            var folderNames = moved.Select(o => $"{BoxPlan.RootFolderName}\\{Path.GetFileName(BoxPlan.ParentOf(o.Current!))}").Distinct().ToList();
            // Birden çok kutunun klasörüne gittiyse ortak kök (NestDesk) söylenir.
            var where = folderNames.Count == 1 ? folderNames[0] : BoxPlan.RootFolderName;
            parts.Add(moved.Count == 1
                ? L.F("\"{0}\" masaüstünden kalktı; {1} klasörüne taşındı, kutuda duruyor.", TileItem.DisplayName(moved[0].Step.Original), where)
                : L.P(moved.Count, "{0} öğe masaüstünden kalktı; {1} klasörüne taşındı, kutuda duruyor.", where));
        }
        if (failed.Count > 0)
        {
            var first = failed[0];
            parts.Add(failed.Count > 1
                ? L.P(failed.Count, "{0} öğe taşınamadı (kullanımda ya da izin yok); kutuya bağlantı olarak eklendi.")
                : first.Failure == BoxFailure.Locked
                    ? IsRing(first.Step.BoxId)
                        ? L.F("\"{0}\" açık olduğu için masaüstünden kalkamadı; halkaya eklendi, sonra yeniden denenecek.", TileItem.DisplayName(first.Step.Original))
                        : L.F("\"{0}\" açık olduğu için masaüstünden kalkamadı; kutuya eklendi, sonra yeniden denenecek.", TileItem.DisplayName(first.Step.Original))
                    : L.F("\"{0}\" taşınamadı ({1}); kutuya bağlantı olarak eklendi.", TileItem.DisplayName(first.Step.Original), ErrorText(first.Failure, first.Error)));
        }
        if (folders.Count > 0 && !_folderNoticeShown)
        {
            _folderNoticeShown = true;
            var name = TileItem.DisplayName(folders[0].Step.Original);
            parts.Add(IsRing(folders[0].Step.BoxId)
                ? L.F("Klasörler masaüstünden kalkmaz; \"{0}\" halkada kısayol olarak duruyor.", name)
                : L.F("Klasörler masaüstünden kalkmaz; \"{0}\" kutuda kısayol olarak duruyor.", name));
        }
        if (publicSkipped.Count > 0)
        {
            parts.Add(publicSkipped.Count == 1
                ? L.F("\"{0}\" bu bilgisayardaki tüm hesapların ortak masaüstünde, bu yüzden masaüstünde kaldı. Kaldırmak için yönetici izni gerekir.",
                    TileItem.DisplayName(publicSkipped[0].Step.Original))
                : L.P(publicSkipped.Count, "{0} öğe bu bilgisayardaki tüm hesapların ortak masaüstünde, bu yüzden masaüstünde kaldı. Kaldırmak için yönetici izni gerekir."));
        }
        if (parts.Count == 0) return;

        var text = string.Join(" ", parts);
        if (moved.Count > 0)
        {
            var undo = moved.Select(o => Added(o, tabs)).ToList();
            // Az önce eklenenler kutudan da çıkar; zaten kutuda olanlar (kip açılırken taşınanlar) yalnızca masaüstüne döner.
            Action action = justAdded ? () => UndoAdd(undo) : () => Return(undo.Select(u => u.Current).ToList());
            Notice.Show(text, failed.Count > 0 ? NoticeKind.Warning : NoticeKind.Success, L.T("Geri al"), action,
                L.T("Geri almak için buraya tıkla."));
        }
        else if (failed.Count > 0)
        {
            // Açık olan dosya kapatılınca (ya da izin verilince) yeniden denenebilir.
            var batches = failed.GroupBy(o => o.Step.BoxId)
                .Select(g => new ClaimBatch(g.Key, tabs.GetValueOrDefault(g.Key), g.Select(o => o.Step.Original).ToList())).ToList();
            Notice.Show(text, NoticeKind.Warning, L.T("Tekrar dene"), () => Enqueue(() => ClaimJob(batches, notify: true, justAdded, interactive: true)));
        }
        else if (publicSkipped.Count > 0 && !PackageInfo.IsPackaged)
        {
            // "Masaüstünden kaldır": bundan sonra ortak masaüstündekiler de taşınır (Windows her seferinde onay ister).
            var batches = publicSkipped.GroupBy(o => o.Step.BoxId)
                .Select(g => new ClaimBatch(g.Key, tabs.GetValueOrDefault(g.Key), g.Select(o => o.Step.Original).ToList())).ToList();
            Notice.Show(text, NoticeKind.Info, L.T("Masaüstünden kaldır"), () =>
            {
                AppHost.Settings.BoxIncludesPublicDesktop = true;
                AppHost.SaveSettings();
                Enqueue(() => ClaimJob(batches, notify: true, justAdded, interactive: true));
            });
        }
        else Notice.Show(text, NoticeKind.Info);
    }

    private static AddedItem Added(ClaimOutcome outcome, IReadOnlyDictionary<string, LauncherTab?> tabs)
    {
        var tab = tabs.GetValueOrDefault(outcome.Step.BoxId);
        var box = Launchers.Find(AppHost.Settings, outcome.Step.BoxId);
        var index = tab is not null && box is not null ? box.Tabs.IndexOf(tab) : -1;
        return new AddedItem(outcome.Step.BoxId, tab, index, tab?.Name, outcome.Current!);
    }

    /// <summary>
    /// Az önceki eklemeyi geri alır: öğe eklendiği sekmeden çıkar (yalnızca oradan; sıra LauncherEntries ile, adı ve simgesi
    /// de) ve masaüstüne döner. Başka bir sekme ya da kutu da gösteriyorsa onlar masaüstündeki yolu gösterir. Kutu kimlikle
    /// bulunur: o arada kaldırılıp geri getirildiyse ayarın yeni kopyasında.
    /// </summary>
    private static void UndoAdd(List<AddedItem> items)
    {
        var settings = AppHost.Settings;
        var changed = false;
        foreach (var item in items)
        {
            if (Launchers.Find(settings, item.BoxId) is not { } box) continue;
            var tab = item.Tab is { } t && box.Tabs.Contains(t) ? t
                : item.TabIndex >= 0 && item.TabIndex < box.Tabs.Count && string.Equals(box.Tabs[item.TabIndex]?.Name, item.TabName, StringComparison.Ordinal)
                    ? box.Tabs[item.TabIndex]
                    : item.Tab is null ? box.Tabs.FirstOrDefault(x => x is not null && LauncherEntries.IndexOfPath(x, item.Current) >= 0) : null;
            if (tab is null) continue;
            var index = LauncherEntries.IndexOfPath(tab, item.Current);
            if (index < 0) continue;
            changed |= LauncherEdits.Take(box, tab, index) is not null;
        }
        if (changed)
        {
            AppHost.SaveSettings();
            AppHost.RefreshPinnedPaths();
            Changed?.Invoke();
        }
        Return(items.Select(i => i.Current).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static string ErrorText(BoxFailure failure, string? error) => failure switch
    {
        BoxFailure.Locked => L.T("kullanımda"),
        BoxFailure.Denied => L.T("izin yok"),
        BoxFailure.Declined => L.T("izin verilmedi"),
        _ => error ?? "",
    };

    // ---------------------------------------------------------------- yeniden deneme (açık olduğu için taşınamayanlar)

    private static DispatcherTimer? _claimRetry;
    private static DispatcherTimer? _desktopDebounce;
    // Zamanlı yeniden denemeler: 3 sn sonra bir, 30 sn sonra bir; sonra yalnızca masaüstü değişince ve sonraki açılışta.
    private static int _timedRetries;

    /// <summary>
    /// Taşınamayan öğeler listeye girer (sonraki açılışta da denensin), taşınan ya da artık taşınmayacak olanlar çıkar.
    /// Değiştiyse true.
    /// </summary>
    private static bool NoteRetries(List<ClaimOutcome> outcomes)
    {
        var settings = AppHost.Settings;
        var next = BoxPlan.UpdateRetry(settings.BoxClaimRetry, outcomes);
        if (ReferenceEquals(next, settings.BoxClaimRetry)) return false;
        settings.BoxClaimRetry = next;
        return true;
    }

    private static void ScheduleClaimRetry(bool automatic)
    {
        if (!automatic) _timedRetries = 0;
        if (_claimRetry is not null || _timedRetries >= 2 || System.Windows.Application.Current?.Dispatcher is not { } dispatcher) return;
        var delay = TimeSpan.FromSeconds(_timedRetries == 0 ? 3 : 30);
        _timedRetries++;
        _claimRetry = new DispatcherTimer(delay, DispatcherPriority.Background, (_, _) =>
        {
            _claimRetry?.Stop();
            _claimRetry = null;
            RetryClaims();
        }, dispatcher);
        _claimRetry.Start();
    }

    /// <summary>Masaüstü değişti (ör. Word dosyayı kapatıp kilit dosyasını sildi): bekleyen taşımalar 2 sn sonra yeniden denenir.</summary>
    private static void OnDesktopChanged()
    {
        if (AppHost.Settings.BoxClaimRetry is not { Count: > 0 } || !MovesFiles ||
            System.Windows.Application.Current?.Dispatcher is not { } dispatcher) return;
        _desktopDebounce ??= new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) =>
        {
            _desktopDebounce?.Stop();
            RetryClaims();
        }, dispatcher);
        _desktopDebounce.Stop();
        _desktopDebounce.Start();
    }

    /// <summary>
    /// Açık olduğu için taşınamamış öğeleri sessizce yeniden dener (Windows onayı istenmez). Hiçbir kutuda kalmayan ya da kip
    /// kapandıysa liste temizlenir.
    /// </summary>
    private static void RetryClaims()
    {
        var settings = AppHost.Settings;
        if (settings.BoxClaimRetry is not { Count: > 0 } list) return;
        if (!MovesFiles)
        {
            settings.BoxClaimRetry = null;
            AppHost.SaveSettings();
            return;
        }
        var left = new List<string>();
        var batches = new Dictionary<string, ClaimBatch>(StringComparer.Ordinal);
        foreach (var path in list)
        {
            if (Launchers.All(settings).FirstOrDefault(b => BoxPlan.Referenced([b]).Contains(path)) is not { } box) continue;
            left.Add(path);
            if (!batches.TryGetValue(box.Id, out var batch)) batches[box.Id] = batch = new ClaimBatch(box.Id, null, []);
            batch.Paths.Add(path);
        }
        if (left.Count != list.Count)
        {
            settings.BoxClaimRetry = left.Count > 0 ? left : null;
            AppHost.SaveSettings();
        }
        if (batches.Count > 0)
            Enqueue(() => ClaimJob(batches.Values.ToList(), notify: false, justAdded: false, interactive: false, automatic: true));
    }

    // ---------------------------------------------------------------- geri koyma

    /// <summary>"Masaüstüne geri koy": öğeler masaüstüne döner, kutuda kalır (kutu masaüstündeki yolu gösterir).</summary>
    public static void Return(IReadOnlyCollection<string> currentPaths, bool notify = true)
    {
        var set = new HashSet<string>(currentPaths, StringComparer.OrdinalIgnoreCase);
        if (set.Count == 0) return;
        Enqueue(() => ReturnJob(records => records.Where(r => r.Active && set.Contains(r.Current)).ToList(), reclaim: false,
            interactive: true, notify ? ReturnNotice.All : ReturnNotice.Problems, keepInBoxes: true));
    }

    /// <summary>Kutulara taşınan her şeyi masaüstüne geri koyar (Ayarlar, kip kapatılırken).</summary>
    public static void ReturnAll() =>
        Enqueue(() => ReturnJob(records => records.Where(r => r.Active).ToList(), reclaim: false, interactive: true, ReturnNotice.All,
            keepInBoxes: true));

    /// <summary>Kayıtları kimlikle yeniden dener ("Tekrar dene", "Geri koy", "Benim masaüstüme koy", kendiliğinden yeniden deneme).</summary>
    private static void ReturnRecords(IReadOnlyCollection<string> ids, bool reclaim, bool interactive, ReturnNotice notify, bool toUser = false)
    {
        var set = new HashSet<string>(ids, StringComparer.Ordinal);
        if (set.Count == 0) return;
        Enqueue(() => ReturnJob(records => records.Where(r => r.Active && set.Contains(r.Id)).ToList(), reclaim, interactive, notify,
            keepInBoxes: !reclaim, toUser));
    }

    private enum ReturnNotice
    {
        /// <summary>Hiçbir şey söylenmez (sessiz yeniden deneme).</summary>
        None,

        /// <summary>Yalnızca sorunlar (başarısızlık, Windows onay vermedi, onay bekleyenler): uzlaştırma.</summary>
        Problems,

        /// <summary>Her sonuç (kullanıcının "Masaüstüne geri koy"u).</summary>
        All,
    }

    private static ReturnRequest Request(bool interactive, bool reclaim, bool toUser = false) =>
        new(AppHost.DesktopDirectories, AppHost.DesktopDirectory, AppHost.DesktopDirectories.Skip(1).ToList(), interactive, reclaim, toUser);

    /// <param name="keepInBoxes">Öğeler kutuda kalacak ("Masaüstüne geri koy"): masaüstüne döndükleri anda kurallarla
    /// taşınmasınlar (küme dönüşten sonra kesinleşir).</param>
    private static async Task ReturnJob(Func<List<BoxMove>, List<BoxMove>> select, bool reclaim, bool interactive, ReturnNotice notify,
        bool keepInBoxes, bool toUser = false)
    {
        var records = select(AppHost.BoxMoves.Snapshot());
        if (records.Count == 0) return;
        if (keepInBoxes)
            AppHost.Organizer.Pinned = new HashSet<string>(AppHost.Organizer.Pinned.Concat(records.Select(r => r.Original)),
                StringComparer.OrdinalIgnoreCase);
        var engine = Engine();
        var request = Request(interactive, reclaim, toUser);
        var outcomes = await Task.Run(() => engine.Return(records, request));
        ApplyReturns(outcomes);
        NotifyReturned(outcomes, notify, interactive, reclaim);
    }

    // Uzlaştırma birleştirilir: kuyrukta en çok bir iş bekler (açılıştaki ve widget'lar açılınca gelen istek tek iş olur).
    private static bool _reconcileQueued;
    private static bool _reconcileInteractive;

    /// <summary>
    /// Kayıtları kutularla uzlaştırır (kutu/öğe/sekme kaldırıldı, "Geri al", düzen uygulandı, açılış): hiçbir kutunun
    /// göstermediği taşınmış öğe masaüstüne döner; kutu geri geldiyse öğeleri yeniden bağlanır (kip açıksa yeniden taşınır);
    /// dışarıda değişen kayıtlar (Gezgin'de geri taşınmış, silinmiş, yarıda kalmış) kapanır. interactive: kullanıcının bir
    /// eyleminden (ortak masaüstüne dönüş için Windows onay ister); false ise asla sorulmaz.
    /// </summary>
    public static void Reconcile(bool interactive = false)
    {
        if (AppHost.BoxMoves is null) return;
        _reconcileInteractive |= interactive;
        if (_reconcileQueued) return;
        _reconcileQueued = true;
        Enqueue(ReconcileJob);
    }

    private static async Task ReconcileJob()
    {
        _reconcileQueued = false;
        var interactive = _reconcileInteractive;
        _reconcileInteractive = false;
        var log = AppHost.BoxMoves;
        var records = log.Snapshot();
        if (records.Count == 0) return;
        var settings = AppHost.Settings;
        var referenced = Launchers.Referenced(settings);
        var journal = AppHost.Journal;
        var organizer = AppHost.Organizer;
        // Geri konan öğe masaüstünden bir kuralla taşınmışsa (ör. önceki sürümde) kutu onu geçmişten bulur.
        var plan = await Task.Run(() => BoxPlan.Reconcile(records, referenced, Disk.Exists, BoxPlan.MovedByRule(journal.Snapshot())));
        if (plan.Close.Count > 0)
            await Task.Run(() =>
            {
                // Dışarıda değişenler: dosyaya dokunulmadan kayıt kapanır.
                foreach (var (record, remapTo, reason) in plan.Close)
                {
                    log.MarkReturned(record.Id, remapTo ?? record.Original, reclaim: false);
                    switch (reason)
                    {
                        case BoxCloseReason.Finished: AppLog.Count("box.closed-finished"); break;
                        case BoxCloseReason.BackOnDesktop:
                            AppLog.Count("box.closed-back-on-desktop");
                            if (remapTo is not null) KeepOnDesktop(organizer, remapTo, record.Current);
                            break;
                        case BoxCloseReason.CopyLeft: AppLog.Warn("box", "closed-copy-left"); break;
                        default: AppLog.Count("box.closed-gone"); break;
                    }
                }
            });
        var engine = Engine();
        var request = Request(interactive, reclaim: true);
        var outcomes = plan.Return.Count > 0 ? await Task.Run(() => engine.Return(plan.Return, request)) : [];

        var changes = plan.Remap.Select(r => (r.From, r.To, false)).ToList();
        if (Launchers.ReplacePaths(settings, changes))
        {
            AppHost.SaveSettings();
            AppHost.RefreshPinnedPaths();
        }
        ApplyReturns(outcomes);
        if (outcomes.Count == 0 && (plan.Remap.Count > 0 || plan.Close.Count > 0))
        {
            Changed?.Invoke();
            RefreshRootState();
        }
        NotifyReturned(outcomes, ReturnNotice.Problems, interactive, reclaim: true);

        // Kutu geri geldi (Geri al, kayıtlı düzen): masaüstüne dönen öğeleri kip açıksa yeniden kutuya taşı.
        var reclaim = plan.Remap.Where(r => r.Reclaim).Select(r => r.To).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (reclaim.Count == 0 || !MovesFiles) return;
        var items = Launchers.All(settings)
            .Select(box => (box, BoxPlan.Referenced([box]).Where(reclaim.Contains).ToList()))
            .Where(x => x.Item2.Count > 0).ToList();
        ClaimMany(items, notify: false, interactive: interactive);
    }

    /// <summary>
    /// Kutudan (ya da kutu kaldırıldığı için) masaüstüne dönen öğe artık hiçbir kutuda değil, ama kullanıcı onu bilerek masaüstüne
    /// geri aldı: bildirim "masaüstüne geri konuyor" der, kutunun "Geri al"ı onu orada arar. Otomatik taşıma açıkken bir kural
    /// onu hemen taşıyacaksa bölmedeki "Masaüstüne geri koy" gibi "geri alındı" yazılır ve bir daha otomatik taşınmaz. İzleyici
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

    private static void ApplyReturns(List<ReturnOutcome> outcomes)
    {
        if (outcomes.Count == 0) return;
        // Hâlâ kutuda duruyorsa kutu masaüstündeki yolu göstersin.
        var changes = outcomes.Where(o => o.OnDesktop && o.Path is not null).Select(o => (o.Record.Current, o.Path!, false)).ToList();
        if (Launchers.ReplacePaths(AppHost.Settings, changes))
        {
            AppHost.SaveSettings();
            AppHost.RefreshPinnedPaths();
        }
        Changed?.Invoke();
        RefreshRootState();
    }

    // Arka plandaki uzlaştırmanın bu oturumda söylediği sorunlar (aynı kayıt için bir kez) ve ortak masaüstü sorusu.
    private static readonly HashSet<string> _problemShown = new(StringComparer.Ordinal);
    private static bool _waitingShown;
    private static bool _returnRetryScheduled;

    private static void NotifyReturned(List<ReturnOutcome> outcomes, ReturnNotice notify, bool interactive, bool reclaim)
    {
        if (outcomes.Count == 0 || notify == ReturnNotice.None) return;
        var all = notify == ReturnNotice.All;
        var restored = outcomes.Where(o => o.State is ReturnState.Restored or ReturnState.Absorbed).ToList();
        var already = outcomes.Where(o => o.State == ReturnState.AlreadyThere).ToList();
        var notFound = outcomes.Where(o => o.State == ReturnState.NotFound).ToList();
        var fellBack = outcomes.Where(o => o.State == ReturnState.FellBack).ToList();
        // Arka planda aynı sorun bir oturumda bir kez söylenir; kullanıcı istediyse her zaman.
        var failed = outcomes.Where(o => o.State == ReturnState.Failed && (interactive || _problemShown.Add(o.Record.Id))).ToList();
        var waiting = outcomes.Where(o => o.State == ReturnState.Waiting).ToList();
        if (_waitingShown && !interactive) waiting.Clear();

        // Açık olduğu için konamayanlar bir kez, sessizce yeniden denenir.
        var locked = outcomes.Where(o => o.State == ReturnState.Failed && o.Failure == BoxFailure.Locked).Select(o => o.Record.Id).ToList();
        if (locked.Count > 0 && !_returnRetryScheduled)
        {
            _returnRetryScheduled = true;
            RunLater(TimeSpan.FromSeconds(30), () =>
            {
                _returnRetryScheduled = false;
                ReturnRecords(locked, reclaim, interactive: false, ReturnNotice.None);
            });
        }

        var parts = new List<string>();
        if (all && restored.Count > 0)
            parts.Add(restored.Count == 1
                ? L.F("\"{0}\" masaüstüne geri kondu.", TileItem.DisplayName(restored[0].Path!))
                : L.P(restored.Count, "{0} öğe masaüstüne geri kondu."));
        if (all && already.Count > 0)
            parts.Add(already.Count == 1
                ? L.F("\"{0}\" zaten masaüstünde.", TileItem.DisplayName(already[0].Path!))
                : L.P(already.Count, "{0} öğe masaüstüne geri kondu."));
        if (all && notFound.Count > 0)
            parts.Add(L.F("\"{0}\" {1} klasöründe bulunamadı; taşınmış ya da silinmiş olabilir.",
                TileItem.DisplayName(notFound[0].Record.Current), BoxPlan.RootFolderName));
        if (fellBack.Count > 0)
            parts.Add(fellBack.Count == 1
                ? L.F("Windows izin vermediği için \"{0}\" senin masaüstüne kondu.", TileItem.DisplayName(fellBack[0].Path!))
                : L.P(fellBack.Count, "Windows izin vermediği için {0} simge senin masaüstüne kondu."));
        if (failed.Count > 0)
            parts.Add(failed.Count == 1
                ? L.F("\"{0}\" masaüstüne konamadı ({1}); {2} klasöründe duruyor.", TileItem.DisplayName(failed[0].Record.Original),
                    ErrorText(failed[0].Failure, failed[0].Error), BoxPlan.RootFolderName)
                : L.P(failed.Count, "{0} öğe geri konamadı ({1}); {2} klasöründe duruyor.", ErrorText(failed[0].Failure, failed[0].Error),
                    BoxPlan.RootFolderName));
        if (waiting.Count > 0)
        {
            _waitingShown = true;
            parts.Add(L.P(waiting.Count, "Kutudan çıkan {0} simge herkesin masaüstüne geri konacak; Windows yönetici izni ister."));
        }
        if (parts.Count == 0) return;
        var text = string.Join(" ", parts);

        var choices = new List<NoticeChoice>();
        if (failed.Count > 0 || waiting.Count > 0)
        {
            var ids = failed.Concat(waiting).Select(o => o.Record.Id).ToList();
            choices.Add(new NoticeChoice(failed.Count > 0 ? L.T("Tekrar dene") : L.T("Geri koy"),
                () => ReturnRecords(ids, reclaim, interactive: true, ReturnNotice.All)));
        }
        if (waiting.Count > 0)
        {
            var ids = waiting.Select(o => o.Record.Id).ToList();
            choices.Add(new NoticeChoice(L.T("Benim masaüstüme koy"),
                () => ReturnRecords(ids, reclaim, interactive: true, ReturnNotice.All, toUser: true)));
        }
        if (failed.Count > 0)
        {
            var folder = BoxPlan.ParentOf(failed[0].Record.Current);
            if (folder is not null) choices.Add(new NoticeChoice(L.T("Klasörü aç"), () => TileItem.Launch(folder)));
        }
        if (choices.Count > 0)
        {
            Notice.Ask(text, NoticeKind.Warning, choices);
            return;
        }
        if (fellBack.Count > 0)
        {
            var moves = fellBack.Select(o => (o.Path!, BoxPlan.ParentOf(o.Record.Original))).Where(m => m.Item2 is not null)
                .Select(m => (Path: m.Item1, PublicDir: m.Item2!)).ToList();
            Notice.Show(text, NoticeKind.Warning, L.T("Herkesin masaüstüne taşı"), () => MoveToPublic(moves));
            return;
        }
        Notice.Show(text, notFound.Count > 0 ? NoticeKind.Warning : NoticeKind.Success);
    }

    /// <summary>
    /// Windows'un onay vermediği için kullanıcının masaüstüne konan ortak masaüstü öğelerini yeniden herkesin masaüstüne taşır
    /// (bildirimdeki düğme; Windows bir kez onay ister). Kutular yeni yolu gösterir.
    /// </summary>
    private static void MoveToPublic(List<(string Path, string PublicDir)> items) => Enqueue(async () =>
    {
        var publics = AppHost.DesktopDirectories.Skip(1).ToList();
        var engine = Engine();
        var done = await Task.Run(() =>
        {
            // Yalnızca bugün de ortak masaüstü olan klasöre (başka bir yere asla).
            var moves = items.Where(i => publics.Any(p => string.Equals(Path.TrimEndingDirectorySeparator(p),
                                                          Path.TrimEndingDirectorySeparator(i.PublicDir), StringComparison.OrdinalIgnoreCase)) &&
                                         Disk.Exists(i.Path))
                .Select(i => (From: i.Path, To: FileMover.UniquePath(i.PublicDir, BoxPlan.NameOf(i.Path)))).ToList();
            var (go, _) = engine.Guard(moves);
            if (go.Count == 0) return [];
            try { Disk.MoveElevated(go); }
            catch (OperationCanceledException) { AppLog.Info("box", "elevated-move-cancelled"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLog.Warn("box", "elevated-move-failed", ex);
            }
            var moved = go.Where(m => Disk.Exists(m.To) && !Disk.Exists(m.From)).ToList();
            foreach (var (from, to) in moved) AppHost.BoxMoves.NoteRename(from, to, isDirectory: false);
            return moved;
        });
        if (done.Count == 0) return;
        if (Launchers.ReplacePaths(AppHost.Settings, done.Select(m => (m.From, m.To, false)).ToList()))
        {
            AppHost.SaveSettings();
            AppHost.RefreshPinnedPaths();
        }
        Changed?.Invoke();
        Notice.Show(L.F("\"{0}\" herkesin masaüstüne kondu.", TileItem.DisplayName(done[0].To)), NoticeKind.Success);
    });

    // ---------------------------------------------------------------- yardımcılar

    /// <summary>Kökün (ya da öğelerin durduğu eski kökün) diskte olup olmadığına arka planda bakar; değiştiyse Changed.</summary>
    private static void RefreshRootState()
    {
        if (System.Windows.Application.Current?.Dispatcher is not { } dispatcher || AppHost.BoxMoves is null) return;
        var root = Root;
        // Öğelerin çoğunun durduğu kök önce; sonra bugünkü kök.
        var roots = AppHost.BoxMoves.Snapshot().Where(r => r.Active).Select(r => BoxPlan.RootOf(r.Current)).OfType<string>()
            .GroupBy(r => r, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).Select(g => g.Key)
            .Append(root).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Task.Run(() => roots.FirstOrDefault(Directory.Exists)).ContinueWith(task =>
        {
            var found = task.IsCompletedSuccessfully ? task.Result : null;
            dispatcher.BeginInvoke(() =>
            {
                _folderToOpen = found ?? root;
                if (RootExists == found is not null) return;
                RootExists = found is not null;
                Changed?.Invoke();
            });
        }, TaskScheduler.Default);
    }

    private static void RunLater(TimeSpan delay, Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is not { } dispatcher) return;
        DispatcherTimer? timer = null;
        timer = new DispatcherTimer(delay, DispatcherPriority.Background, (_, _) =>
        {
            timer?.Stop();
            action();
        }, dispatcher);
        timer.Start();
    }

    private static bool IsUnder(string path, string directory)
    {
        var dir = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return path.StartsWith(dir, StringComparison.OrdinalIgnoreCase);
    }
}
