using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Views;

namespace Duzenleme.Widgets;

/// <summary>
/// "Kutuya koyduğum simgeler masaüstünden kalksın" (<see cref="AppSettings.BoxItemsLeaveDesktop"/>): kutuya ya da halkaya konan
/// masaüstü simgesi masaüstünden kalkar, çıkarınca geri gelir. Tek seçenek, iki yol: Windows simgeleri görünürken
/// (<see cref="MovesFiles"/>) dosya görünür, sıradan bir klasöre (masaüstünün yanındaki NestDesk\&lt;kutu&gt;) taşınır, kutu yeni
/// yolu gösterir, taşıma kaydedilir ve geri alınabilir; bölmeler masaüstünü yönetirken (<see cref="HidesFromFences"/>) hiçbir
/// dosya taşınmaz, masaüstü bölmeleri kutudaki öğeleri göstermez (<see cref="FenceExclusions"/>). Klasörler taşınmaz.
/// <para>Seçenek kapalıyken ilk masaüstü simgesi kutuya konunca bir kez, widget'ın yanında sorulur (<see cref="Added"/>);
/// herkesin masaüstü ve uygulamanın masaüstündeki kısayolu için de ayrı birer soru vardır. Kutudan ya da kutunun kendisi
/// kaldırılınca öğe masaüstüne döner; kutu geri gelirse yeniden taşınır.</para>
/// Dosya işleri arka planda, sırayla (tek kuyruk) <see cref="BoxEngine"/> ile yapılır (karar <see cref="BoxPlan"/>'da, saf);
/// kutuların ayarları yalnızca arayüz iş parçacığında değişir. Kutular kimlikle bulunur (<see cref="Launchers"/>).
/// Ortak masaüstü için Windows'un yönetici onayı yalnızca kullanıcının bir eylemiyle istenir (ekleme, kaldırma, Geri al);
/// açılışta ve arka plandaki uzlaştırmada asla: o öğeler bir bildirimle bekler. Store (MSIX) sürümü ortak masaüstündekileri
/// hiç taşımaz. Her başarısızlık bir bildirimle söylenir ("Tekrar dene", "Klasörü aç"); açık olduğu için taşınamayan dosya
/// sonra yeniden denenir. Gizli dosya özniteliği kullanılmaz: Store sürümünün kaldırma adımı yoktur, gizlenen dosyalar
/// görünmez kalırdı (NestDesk kökündeki "Bu klasör nedir.txt" ne olduklarını anlatır).
/// </summary>
internal static class BoxMover
{
    /// <summary>Taşıma ya da geri koyma bitti, kutuların yolları güncellendi ya da seçenek değişti (arayüz iş parçacığında).</summary>
    public static event Action? Changed;

    /// <summary>"Kutuya koyduğum simgeler masaüstünden kalksın" açık mı?</summary>
    public static bool On => AppHost.Settings.BoxItemsLeaveDesktop;

    /// <summary>Dosyalar taşınır mı? Bölmeler masaüstünü yönetirken (Windows simgeleri zaten gizli) öğeler taşınmaz.</summary>
    public static bool MovesFiles => On && !AppHost.Settings.FencesReplaceIcons;

    /// <summary>Bölmeler masaüstünü yönetiyor ve seçenek açık: dosyaya dokunulmaz, masaüstü bölmeleri kutudakileri göstermez.</summary>
    public static bool HidesFromFences => On && AppHost.Settings.FencesReplaceIcons;

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

    /// <summary>Taşıma kararının ortamı (kullanıcının masaüstü, ortak masaüstleri, ortak masaüstü izni).</summary>
    private static BoxContext Context(bool includePublic) =>
        new(AppHost.DesktopDirectory, AppHost.DesktopDirectories.Skip(1).ToList(), includePublic && !PackageInfo.IsPackaged);

    /// <summary>
    /// Masaüstü öğelerinin öznitelikleri, masaüstlerinin anlık görüntüsünden (arayüzde diske dokunmadan; henüz okunmamış ya da
    /// listede olmayan yol için null).
    /// </summary>
    public static Func<string, FileAttributes?> SnapshotAttributes()
    {
        var map = new Dictionary<string, FileAttributes>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in AppHost.DesktopDirectories)
            if (AppHost.Snapshots.Find(dir) is { State: SnapshotState.Ready } snapshot)
                foreach (var entry in snapshot.Entries) map.TryAdd(entry.Path, entry.Attributes);
        return path => map.TryGetValue(path, out var attributes) ? attributes : null;
    }

    /// <summary>
    /// Kutulardaki masaüstü öğeleri (diske bakılmaz). Ortak masaüstündekiler de: izin verilmediyse taşınmazlar ama sayılırlar
    /// (soru "ayrı izin gerekir" der).
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
    /// Kutulardaki masaüstü öğelerinden kaçı şimdi kalkabilir, kaçı yalnızca ayrı izinle (herkesin masaüstü): aynı öğe bir
    /// kez; klasörler, korunanlar sayılmaz (masaüstünün anlık görüntüsünden; diske bakılmaz).
    /// </summary>
    public static ClaimCount ClaimCounts() =>
        BoxPlan.ClaimCandidates(Launchers.Referenced(AppHost.Settings), SnapshotAttributes(), Context(IncludePublic));

    /// <summary>Kutulardaki, yalnızca ayrı izinle kalkabilecek herkesin masaüstü öğeleri (kutularıyla).</summary>
    public static List<(WidgetConfig Box, List<string> Paths)> PublicItemsInBoxes()
    {
        var publicOnly = new HashSet<string>(ClaimCounts().PublicOnly, StringComparer.OrdinalIgnoreCase);
        return DesktopItemsInBoxes().Select(i => (i.Box, i.Paths.Where(publicOnly.Contains).ToList())).Where(i => i.Item2.Count > 0).ToList();
    }

    /// <summary>Yalnızca bu kutunun gösterdiği taşınmış öğe sayısı (kutu kaldırılınca masaüstüne dönecekler).</summary>
    public static int MovedOnlyIn(WidgetConfig box) =>
        box.Kind != WidgetKind.Launcher || MovedCount == 0 ? 0 : ReturningIf(box.Id, null, null);

    /// <summary>
    /// Kutudan (ya da yalnızca sekmesinden, yalnızca bir öğesinden) kaldırınca masaüstüne dönecek taşınmış öğe sayısı (başka
    /// bir kutuda ya da halkada kalanlar dönmez).
    /// </summary>
    public static int ReturningIf(string boxId, LauncherTab? tab, string? path) =>
        MovedCount == 0 ? 0 : BoxPlan.ReturningIf(Launchers.All(AppHost.Settings), boxId, tab, path, IsMoved).Count;

    /// <summary>
    /// Öğe kutudan çıkınca masaüstünde yeniden görünür mü (taşınmışsa geri döner; bölme kipinde bölmede görünür)? Başka bir
    /// kutuda ya da halkada da duruyorsa hayır (bölmeye bırakma kaplamasının metni).
    /// </summary>
    public static bool WouldReappear(string path) =>
        (IsMoved(path) || HidesFromFences) && !BoxPlan.HeldElsewhere(Launchers.All(AppHost.Settings), path);

    /// <summary>Öğe menüsü için öğenin durumu (diske bakmaz: masaüstünün anlık görüntüsünden).</summary>
    public static BoxItemState ItemState(string path, bool missing)
    {
        var moved = IsMoved(path);
        var decision = MovesFiles && !moved && !missing && !TileItemPath.IsShellObject(path)
            ? BoxPlan.Decide(path, SnapshotAttributes()(path), AppHost.DesktopDirectory, AppHost.DesktopDirectories.Skip(1), IncludePublic)
            : BoxLinkReason.NotOnDesktop;
        return BoxChoices.StateOf(moved, missing, MovesFiles, decision, PackageInfo.IsPackaged);
    }

    /// <summary>Bırakılanlar kutuya girince masaüstünden kalkar mı (bırakma kaplaması; yalnızca metin karşılaştırır)?</summary>
    public static BoxDropEffect DropEffect(IEnumerable<string> paths)
    {
        if (!On) return BoxDropEffect.None;
        var attributes = SnapshotAttributes();
        return BoxChoices.DropEffect(paths, On, AppHost.Settings.FencesReplaceIcons, Context(IncludePublic),
            path => attributes(path) is { } a && (a & FileAttributes.Directory) != 0);
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
    /// taşınamamış öğeler yeniden denenir, NestDesk kökündeki açıklama dosyası yazılır. Masaüstü değişince bekleyen denemeler
    /// yinelenir; bölme kipinde uygulamaların masaüstündeki kısayolları bölmelerden gizlenir.
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
        Enqueue(BreadcrumbsAsync);
        // Ortak masaüstü de: uygulamaların kısayollarının çoğu oradadır (ilk okuması kullanıcının masaüstünden sonra bitebilir).
        foreach (var dir in AppHost.DesktopDirectories)
            if (AppHost.Snapshots.Find(dir) is { } snapshot) snapshot.Changed += OnDesktopChanged;
        RefreshRootState();
        ScanAppShortcuts();
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

    // ---------------------------------------------------------------- seçenek ve sorular

    /// <summary>
    /// Seçeneği açar ya da kapatır (dosyaya dokunmaz: kutudakileri taşımak ya da geri koymak <see cref="DesktopModes.SetBoxLeaves"/>
    /// ve ilk soru işidir). Açınca "soruldu" da yazılır.
    /// </summary>
    internal static void SetOn(bool on)
    {
        var settings = AppHost.Settings;
        if (on) settings.BoxLeaveAsked = true;
        if (settings.BoxItemsLeaveDesktop == on)
        {
            AppHost.SaveSettings();
            return;
        }
        settings.BoxItemsLeaveDesktop = on;
        AppHost.SaveSettings();
        ModeChanged();
    }

    /// <summary>
    /// Seçenek ya da bölme kipi değişti: masaüstü bölmeleri gösterdiklerini yeniler, bölme kipinde uygulamaların kısayolları
    /// yeniden bulunur, açık görünümler ve Ayarlar güncellenir.
    /// </summary>
    public static void ModeChanged()
    {
        ScanAppShortcuts();
        AppHost.NotePinnedChanged();
        Changed?.Invoke();
    }

    /// <summary>
    /// Kutuya ya da halkaya yeni eklenenler (<see cref="LauncherEdits.AddItems"/>; öğeler kutuda görünür olduktan sonra,
    /// arayüz iş parçacığında). Seçenek kapalıysa ve hiç sorulmadıysa, eklenenlerde taşınabilir bir masaüstü simgesi varsa
    /// widget'ın yanında bir kez sorulur. Açıksa: bölme kipinde bölmelerden kalktığı söylenir; değilse (gerekirse herkesin
    /// masaüstü sorusundan sonra) kutunun klasörüne taşınır. Eklenen uygulamanın masaüstündeki kısayolu da aranır.
    /// </summary>
    /// <param name="attributes">Eklenenlerin öznitelikleri (eklenirken arka planda okunan; masaüstünde olmayanlar için yok).</param>
    /// <param name="near">Soruların açılacağı yer (widget'ın ortası).</param>
    public static void Added(WidgetConfig box, IReadOnlyList<string> added, LauncherTab tab, IReadOnlyDictionary<string, FileAttributes?> attributes,
        NativeMethods.POINT? near)
    {
        if (added.Count == 0 || AppHost.BoxMoves is null) return;
        var settings = AppHost.Settings;
        FileAttributes? Attributes(string path) => attributes.TryGetValue(path, out var a) ? a : null;
        var ring = WidgetVariants.Is(box, WidgetVariants.Ring);
        if (!On)
        {
            // Ortak masaüstü izni bu soruda sayılmaz (o ayrıca sorulur); Store sürümü ortak masaüstündekileri yalnızca bölme kipinde
            // (dosyaya dokunmadan) kutuya alabilir.
            var anyContext = new BoxContext(AppHost.DesktopDirectory, AppHost.DesktopDirectories.Skip(1).ToList(),
                IncludePublic: !PackageInfo.IsPackaged || AppHost.Settings.FencesReplaceIcons);
            if (!BoxChoices.AskSwitch(BoxChoices.Asked(settings), On, added, Attributes, anyContext)) return;
            var first = added.First(p => BoxChoices.AskSwitch(false, false, [p], Attributes, anyContext));
            var yes = Confirm.Ask(null, L.T("Masaüstündeki simge de kalksın mı?"), ConsentText(ring, TileItem.DisplayName(first)),
                L.T("Evet, kalksın"), danger: false, cancelText: L.T("Hayır, kalsın"), near: near);
            settings.BoxLeaveAsked = true;
            if (!yes)
            {
                AppHost.SaveSettings();
                return;
            }
            SetOn(true);
            // Soru açıkken kutu kaldırılmış olabilir.
            if (Launchers.Find(settings, box.Id) is null) return;
        }

        if (HidesFromFences)
        {
            var hidden = added.Where(p => !TileItemPath.IsShellObject(p) && BoxPlan.IsDirectlyIn(p, AppHost.DesktopDirectories)).ToList();
            if (hidden.Count > 0) NotifyHidden(box.Id, tab, hidden, ring);
            MatchAppShortcuts(box, added, near);
            return;
        }
        if (!MovesFiles) return;

        bool? includePublic = null;
        Action<int>? publicMoved = null;
        var publicOnly = BoxChoices.PublicOnly(added, Attributes, Context(IncludePublic));
        if (BoxChoices.AskPublic(MovesFiles, settings.BoxPublicAsked, IncludePublic, PackageInfo.IsPackaged, publicOnly))
        {
            var name = TileItem.DisplayName(publicOnly[0]);
            var yes = Confirm.Ask(null, L.T("Herkesin masaüstündeki simge de kalksın mı?"),
                ring
                    ? L.F("\"{0}\" bu bilgisayardaki herkesin masaüstünde. Kaldırırsam herkesin masaüstünden kalkar ve {1} klasörüne konur; Windows bunun için bir kez yönetici izni ister. Halkadan çıkarınca geri döner.", name, Root)
                    : L.F("\"{0}\" bu bilgisayardaki herkesin masaüstünde. Kaldırırsam herkesin masaüstünden kalkar ve {1} klasörüne konur; Windows bunun için bir kez yönetici izni ister. Kutudan çıkarınca geri döner.", name, Root),
                L.T("Kaldır"), danger: false, cancelText: L.T("Masaüstünde kalsın"), near: near);
            settings.BoxPublicAsked = true;
            AppHost.SaveSettings();
            if (yes)
            {
                // İzin yalnızca Windows'un onayıyla en az bir öğe taşındıysa kalıcı olur (vazgeçilirse sorulmuş sayılır, kapalı kalır).
                includePublic = true;
                publicMoved = moved =>
                {
                    if (moved == 0 || AppHost.Settings.BoxIncludesPublicDesktop) return;
                    AppHost.Settings.BoxIncludesPublicDesktop = true;
                    AppHost.SaveSettings();
                    Changed?.Invoke();
                };
            }
        }
        var paths = added.ToList();
        Enqueue(() => ClaimJob([new ClaimBatch(box.Id, tab, paths)], notify: true, justAdded: true, interactive: true,
            includePublic: includePublic, publicMoved: publicMoved));
        MatchAppShortcuts(box, added, near);
    }

    /// <summary>İlk sorunun metni: kutu ya da halka, dosya taşınır ya da (bölme kipinde) yalnızca bölmelerden kalkar.</summary>
    private static string ConsentText(bool ring, string name) => (ring, AppHost.Settings.FencesReplaceIcons) switch
    {
        (false, false) => L.F("\"{0}\" kutuya eklendi. İstersen masaüstünden kalkar: silinmez, {1} klasörüne konur ve kutudan açılır. Kutudan çıkarınca masaüstüne geri döner. Bundan sonra eklediğin simgeler için de böyle olur.", name, Root),
        (true, false) => L.F("\"{0}\" halkaya eklendi. İstersen masaüstünden kalkar: silinmez, {1} klasörüne konur ve halkadan açılır. Halkadan çıkarınca masaüstüne geri döner. Bundan sonra eklediğin simgeler için de böyle olur.", name, Root),
        (false, true) => L.F("\"{0}\" kutuya eklendi. İstersen bölmelerde görünmez, yalnızca kutuda durur; dosyaya dokunulmaz. Kutudan çıkarınca bölmede yeniden görünür. Bundan sonra eklediğin simgeler için de böyle olur.", name),
        (true, true) => L.F("\"{0}\" halkaya eklendi. İstersen bölmelerde görünmez, yalnızca halkada durur; dosyaya dokunulmaz. Halkadan çıkarınca bölmede yeniden görünür. Bundan sonra eklediğin simgeler için de böyle olur.", name),
    };

    /// <summary>Bölme kipinde eklenenler: bölmelerden kalktı ("Geri al" onları kutudan çıkarır: bölmede yeniden görünürler).</summary>
    private static void NotifyHidden(string boxId, LauncherTab tab, List<string> paths, bool ring)
    {
        var text = paths.Count == 1
            ? ring
                ? L.F("\"{0}\" artık bölmelerde görünmüyor, halkada duruyor.", TileItem.DisplayName(paths[0]))
                : L.F("\"{0}\" artık bölmelerde görünmüyor, kutuda duruyor.", TileItem.DisplayName(paths[0]))
            : L.P(paths.Count, "{0} simge artık bölmelerde görünmüyor.");
        var box = Launchers.Find(AppHost.Settings, boxId);
        var index = box?.Tabs.IndexOf(tab) ?? -1;
        var items = paths.Select(p => new AddedItem(boxId, tab, index, tab.Name, p)).ToList();
        Notice.Show(text, NoticeKind.Success, L.T("Geri al"), () => TakeOutAdded(items), L.T("Geri almak için buraya tıkla."));
    }

    /// <summary>
    /// İstisna durumundaki öğe ("Masaüstünden kaldır" öğe menüsünde): masaüstünden kalkar, kutuda durur. asAdmin: herkesin
    /// masaüstündeki öğe için tek seferlik yönetici onayı (ayar değişmez).
    /// </summary>
    public static void LeaveDesktop(WidgetConfig box, string path, LauncherTab? tab, bool asAdmin)
    {
        if (!MovesFiles) return;
        var boxId = box.Id;
        Enqueue(() => ClaimJob([new ClaimBatch(boxId, tab, [path])], notify: true, justAdded: false, interactive: true,
            includePublic: asAdmin ? true : null));
    }

    // ---------------------------------------------------------------- taşıma

    /// <param name="Tab">Öğelerin az önce eklendiği sekme ("Geri al" onları oradan çıkarır); bilinmiyorsa null.</param>
    /// <param name="ForItem">Yollar kutuda değil, kutudaki bu öğe (uygulama) yüzünden kalkıyor (masaüstündeki kısayolu).</param>
    private sealed record ClaimBatch(string BoxId, LauncherTab? Tab, List<string> Paths, string? ForItem = null);

    /// <summary>"Geri al" için taşınan öğe: kutusu (kimlik), eklendiği sekme (nesne, yoksa sırası ve adı) ve yeni yolu.</summary>
    private sealed record AddedItem(string BoxId, LauncherTab? Tab, int TabIndex, string? TabName, string Current);

    /// <summary>
    /// Kutudaki öğelerden masaüstünde olanları kutunun klasörüne taşır (dosyalar taşınıyorsa). notify: sonucu bildir ("Geri al"
    /// ile). justAdded: öğeler az önce <paramref name="tab"/> sekmesine eklendi; "Geri al" onları kutudan da çıkarır (yoksa
    /// yalnızca masaüstüne geri koyar).
    /// </summary>
    public static void Claim(WidgetConfig box, IReadOnlyCollection<string> paths, bool justAdded, LauncherTab? tab = null, bool notify = true)
    {
        if (paths.Count == 0) return;
        Enqueue(() => ClaimJob([new ClaimBatch(box.Id, tab, paths.ToList())], notify, justAdded, interactive: true));
    }

    /// <summary>
    /// Birden çok kutunun öğelerini tek işte taşır (seçenek açılırken "kutudakiler de kalksın"). interactive: kullanıcı istedi
    /// (ortak masaüstü öğesi için Windows onay ister); false ise (kutu geri geldi, yeniden deneme) sorulmaz, bağlantı kalır.
    /// includePublic: ortak masaüstü izni bu iş için (null: ayardaki). publicMoved: ortak masaüstünden taşınan sayı.
    /// </summary>
    public static void ClaimMany(IReadOnlyList<(WidgetConfig Box, List<string> Paths)> items, bool notify = true, bool justAdded = false,
        bool interactive = true, bool? includePublic = null, Action<int>? publicMoved = null)
    {
        var batches = items.Where(i => i.Paths.Count > 0).Select(i => new ClaimBatch(i.Box.Id, null, i.Paths)).ToList();
        if (batches.Count == 0)
        {
            publicMoved?.Invoke(0);
            return;
        }
        Enqueue(() => ClaimJob(batches, notify, justAdded, interactive, includePublic: includePublic, publicMoved: publicMoved));
    }

    /// <param name="automatic">Kendiliğinden yeniden deneme (açık olduğu için taşınamayanlar): zamanlı deneme sınırlıdır.</param>
    private static async Task ClaimJob(List<ClaimBatch> batches, bool notify, bool justAdded, bool interactive, bool automatic = false,
        bool? includePublic = null, Action<int>? publicMoved = null)
    {
        if (!MovesFiles)
        {
            publicMoved?.Invoke(0);
            return;
        }
        var settings = AppHost.Settings;
        var root = Root;
        var inputs = new List<ClaimInput>();
        var tabs = new Dictionary<string, LauncherTab?>(StringComparer.Ordinal);
        var everywhere = Launchers.Referenced(settings);
        foreach (var batch in batches)
        {
            // Kutu bu arada kaldırıldıysa ya da öğe kutudan çıkarıldıysa taşınmaz (kutu kimlikle bulunur: geri getirilen kopya da).
            if (Launchers.Find(settings, batch.BoxId) is not { } box) continue;
            var referenced = BoxPlan.Referenced([box]);
            // Uygulamanın kısayolu kutuda değildir: uygulama hâlâ kutudaysa ve kısayol hiçbir kutuda değilse taşınır.
            var wanted = batch.ForItem is { } forItem
                ? referenced.Contains(forItem)
                    ? batch.Paths.Where(p => !everywhere.Contains(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    : []
                : batch.Paths.Where(referenced.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
            inputs.Add(new ClaimInput(box.Id, folder, wanted, batch.ForItem));
            tabs.TryAdd(box.Id, batch.Tab);
        }
        if (inputs.Count == 0)
        {
            publicMoved?.Invoke(0);
            return;
        }

        var context = Context(includePublic ?? IncludePublic);
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
            await BreadcrumbsAsync();
        }
        publicMoved?.Invoke(moved.Count(o => o.Step.OnPublic));
        if (outcomes.Any(o => o.Failure is BoxFailure.Locked or BoxFailure.Other)) ScheduleClaimRetry(automatic);
        if (notify) NotifyClaimed(outcomes, justAdded, tabs);
    }

    // Bu oturumda söylendi mi: klasörler taşınmaz; Store sürümü ortak masaüstündekileri taşımaz.
    private static bool _folderNoticeShown;
    private static bool _storePublicShown;

    private static void NotifyClaimed(List<ClaimOutcome> outcomes, bool justAdded, IReadOnlyDictionary<string, LauncherTab?> tabs)
    {
        var moved = outcomes.Where(o => o.Moved).ToList();
        var failed = outcomes.Where(o => !o.Moved && o.Failure != BoxFailure.None).ToList();
        var folders = outcomes.Where(o => o.Step.Reason == BoxLinkReason.Folder).ToList();
        // Store sürümü ortak masaüstündekileri hiç taşımaz: oturumda bir kez söylenir. Öbür sürümlerde soru sorulmuştu ya da
        // kullanıcı "Masaüstünde kalsın" dedi: söylenmez (öğe menüsü "Masaüstünden kaldır (yönetici izni)" sunar).
        var storePublic = PackageInfo.IsPackaged && !_storePublicShown
            ? outcomes.FirstOrDefault(o => o.Step.Reason == BoxLinkReason.PublicDesktop)
            : null;

        var parts = new List<string>();
        if (moved.Count > 0)
        {
            var name = TileItem.DisplayName(moved[0].Step.Original);
            parts.Add(moved.Count > 1 ? L.P(moved.Count, "{0} simge masaüstünden kalktı.")
                : IsRing(moved[0].Step.BoxId) ? L.F("\"{0}\" masaüstünden kalktı, halkada duruyor.", name)
                : L.F("\"{0}\" masaüstünden kalktı, kutuda duruyor.", name));
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
        if (storePublic is not null)
        {
            _storePublicShown = true;
            parts.Add(L.F("\"{0}\" bu bilgisayardaki herkesin masaüstünde; Store sürümü onu taşımaz. Simgeleri bölmelerde toplarsan yalnızca kutuda görünür.",
                TileItem.DisplayName(storePublic.Step.Original)));
        }
        if (parts.Count == 0) return;

        var text = string.Join(" ", parts);
        var choices = new List<NoticeChoice>();
        if (moved.Count > 0)
        {
            var undo = moved.Select(o => Added(o, tabs)).ToList();
            // Az önce eklenenler kutudan da çıkar; zaten kutuda olanlar (seçenek açılırken taşınanlar) yalnızca masaüstüne döner.
            choices.Add(new NoticeChoice(L.T("Geri al"), justAdded ? () => UndoAdd(undo) : () => Return(undo.Select(u => u.Current).ToList(), notify: false)));
        }
        else if (failed.Count > 0)
        {
            // Açık olan dosya kapatılınca (ya da izin verilince) yeniden denenebilir.
            var batches = failed.GroupBy(o => (o.Step.BoxId, o.Step.ForItem))
                .Select(g => new ClaimBatch(g.Key.BoxId, tabs.GetValueOrDefault(g.Key.BoxId), g.Select(o => o.Step.Original).ToList(), g.Key.ForItem)).ToList();
            choices.Add(new NoticeChoice(L.T("Tekrar dene"), () => Enqueue(() => ClaimJob(batches, notify: true, justAdded, interactive: true))));
        }
        if (storePublic is not null)
            choices.Add(new NoticeChoice(L.T("Bölmelerde topla"), () => DesktopModes.Set(IconMode.FencesOnly, near: null, owner: null)));

        var kind = failed.Count > 0 ? NoticeKind.Warning : moved.Count > 0 ? NoticeKind.Success : NoticeKind.Info;
        if (choices.Count > 1) Notice.Ask(text, kind, choices, L.T("Seçmek için tıkla."));
        else if (choices.Count == 1)
            Notice.Show(text, kind, choices[0].Text, choices[0].Action, moved.Count > 0 ? L.T("Geri almak için buraya tıkla.") : null);
        else Notice.Show(text, kind);
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
        TakeOutAdded(items);
        Return(items.Select(i => i.Current).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), notify: false);
    }

    /// <summary>Eklenen öğeleri eklendikleri sekmeden çıkarır (dosyaya dokunmaz).</summary>
    private static void TakeOutAdded(List<AddedItem> items)
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
        if (!changed) return;
        AppHost.SaveSettings();
        AppHost.RefreshPinnedPaths();
        Changed?.Invoke();
    }

    private static string ErrorText(BoxFailure failure, string? error) => failure switch
    {
        BoxFailure.Locked => L.T("kullanımda"),
        BoxFailure.Denied => L.T("izin yok"),
        BoxFailure.Declined => L.T("izin verilmedi"),
        _ => error ?? "",
    };

    // ---------------------------------------------------------------- uygulamanın masaüstündeki kısayolu

    /// <summary>
    /// Bölme kipinde kutudaki bir uygulama yüzünden bölmelerden gizlenen masaüstü kısayolları (kısayol → uygulamanın kutu
    /// yolu). Yalnızca çalışma zamanı: açılışta ve masaüstü değişince yeniden bulunur (<see cref="ScanAppShortcuts"/>).
    /// </summary>
    private static Dictionary<string, string> _appShortcuts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Masaüstü bölmelerinin göstermediği yollar (bölme kipinde; bkz. <see cref="BoxPlan.FenceExclusions"/>).</summary>
    public static HashSet<string> FenceExclusions() =>
        BoxPlan.FenceExclusions(Launchers.All(AppHost.Settings), AppHost.DesktopDirectories, _appShortcuts);

    /// <summary>Masaüstündeki aday kısayollar (anlık görüntüden; kutuda olanlar ve zaten gizlenenler hariç).</summary>
    private static List<string> ShortcutCandidates()
    {
        var dirs = AppHost.DesktopDirectories;
        var entries = dirs.Select(d => AppHost.Snapshots.Find(d)).OfType<DirectorySnapshot>()
            .Where(s => s.State == SnapshotState.Ready).SelectMany(s => s.Entries);
        var excluded = Launchers.Referenced(AppHost.Settings);
        return AppShortcutMatch.Candidates(entries, dirs, excluded);
    }

    /// <summary>
    /// Kutuya eklenen Başlat menüsü uygulamalarının masaüstündeki kısayolu (kesin eşleşme, arka planda): ilk eşleşmede bir kez
    /// sorulur ("Evet, hep böyle yap" / "Hayır"); sonra seçime göre dosyalar taşınırken kısayol kutunun klasörüne taşınır
    /// (kaydı uygulamaya bağlı: uygulama kutudan çıkınca geri gelir), bölme kipinde yalnızca bölmelerden gizlenir.
    /// </summary>
    private static void MatchAppShortcuts(WidgetConfig box, IReadOnlyList<string> added, NativeMethods.POINT? near)
    {
        var apps = added.Where(StartApps.IsApp).ToList();
        if (apps.Count == 0 || !On || BoxAppShortcutChoices.Normalize(AppHost.Settings.BoxAppShortcuts) == BoxAppShortcutChoices.Keep) return;
        var candidates = ShortcutCandidates();
        if (candidates.Count == 0) return;
        var boxId = box.Id;
        Enqueue(async () =>
        {
            var matches = await ShortcutFacts.MatchAsync(apps, candidates);
            var settings = AppHost.Settings;
            if (matches.Count == 0 || !On || Launchers.Find(settings, boxId) is not { } live) return;
            // Uygulama hâlâ bu kutuda; dosyalar taşınacaksa ortak masaüstündeki kısayol yalnızca izinle.
            var referenced = BoxPlan.Referenced([live]);
            var publics = AppHost.DesktopDirectories.Skip(1).ToList();
            matches = matches.Where(m => referenced.Contains(m.App) && !Launchers.Referenced(settings).Contains(m.Shortcut) &&
                                         (!MovesFiles || IncludePublic || !BoxPlan.IsDirectlyIn(m.Shortcut, publics))).ToList();
            if (matches.Count == 0) return;
            switch (BoxAppShortcutChoices.Normalize(settings.BoxAppShortcuts))
            {
                case BoxAppShortcutChoices.Keep:
                    return;
                case null:
                    var name = TileItem.DisplayName(matches[0].Shortcut);
                    var yes = Confirm.Ask(null, L.T("Masaüstündeki kısayolu da kalksın mı?"),
                        MovesFiles
                            ? L.F("Masaüstünde \"{0}\" uygulamasının bir kısayolu da var. O da masaüstünden kalkar ve {1} klasörüne konur; uygulamayı kutudan ya da halkadan çıkarınca geri gelir. Bundan sonra eklediğin uygulamalar için de böyle olur.", name, Root)
                            : L.F("Masaüstünde \"{0}\" uygulamasının bir kısayolu da var. O da bölmelerde görünmez, dosyaya dokunulmaz; uygulamayı kutudan ya da halkadan çıkarınca yeniden görünür. Bundan sonra eklediğin uygulamalar için de böyle olur.", name),
                        L.T("Evet, hep böyle yap"), danger: false, cancelText: L.T("Hayır"), near: near);
                    settings.BoxAppShortcuts = yes ? BoxAppShortcutChoices.Leave : BoxAppShortcutChoices.Keep;
                    AppHost.SaveSettings();
                    Changed?.Invoke();
                    if (!yes) return;
                    break;
            }
            if (MovesFiles)
            {
                var batches = matches.GroupBy(m => m.App, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new ClaimBatch(boxId, null, g.Select(m => m.Shortcut).ToList(), g.Key)).ToList();
                await ClaimJob(batches, notify: true, justAdded: false, interactive: true);
            }
            else if (HidesFromFences)
            {
                foreach (var (app, shortcut) in matches) _appShortcuts[shortcut] = app;
                AppHost.NotePinnedChanged();
            }
        });
    }

    // Son taramanın adayları: aynıysa yeniden okunmaz.
    private static string? _scannedFor;

    /// <summary>
    /// Bölme kipinde (seçenek açık, "kısayolu da kalksın" seçili) kutulardaki uygulamaların masaüstündeki kısayollarını yeniden
    /// bulur (arka planda; adaylar değişmediyse okunmaz). Öbür kiplerde liste boşalır.
    /// </summary>
    private static void ScanAppShortcuts()
    {
        var apps = HidesFromFences && BoxAppShortcutChoices.Normalize(AppHost.Settings.BoxAppShortcuts) == BoxAppShortcutChoices.Leave
            ? Launchers.Referenced(AppHost.Settings).Where(StartApps.IsApp).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
            : [];
        if (apps.Count == 0)
        {
            _scannedFor = null;
            if (_appShortcuts.Count == 0) return;
            _appShortcuts = new(StringComparer.OrdinalIgnoreCase);
            AppHost.NotePinnedChanged();
            return;
        }
        var candidates = ShortcutCandidates().Concat(_appShortcuts.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        var key = string.Join("|", apps) + "#" + string.Join("|", candidates);
        if (key == _scannedFor) return;
        _scannedFor = key;
        Enqueue(async () =>
        {
            var matches = candidates.Count == 0 ? [] : await ShortcutFacts.MatchAsync(apps, candidates);
            var next = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (app, shortcut) in matches) next[shortcut] = app;
            if (next.Count == _appShortcuts.Count && next.All(p => _appShortcuts.TryGetValue(p.Key, out var a) && a == p.Value)) return;
            _appShortcuts = next;
            AppHost.NotePinnedChanged();
        });
    }

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
        // Uygulamanın kısayolu kutuda değildir: yeniden deneme listesine girmez (sonraki eklemede yeniden bulunur).
        var next = BoxPlan.UpdateRetry(settings.BoxClaimRetry, outcomes.Where(o => o.Step.ForItem is null));
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

    /// <summary>
    /// Masaüstü değişti (ör. Word dosyayı kapatıp kilit dosyasını sildi, bir kısayol geldi): bekleyen taşımalar ve bölme kipinde
    /// uygulama kısayolları 2 sn sonra yeniden denenir.
    /// </summary>
    private static void OnDesktopChanged()
    {
        var retry = AppHost.Settings.BoxClaimRetry is { Count: > 0 } && MovesFiles;
        var scan = HidesFromFences && BoxAppShortcutChoices.Normalize(AppHost.Settings.BoxAppShortcuts) == BoxAppShortcutChoices.Leave;
        if ((!retry && !scan) || System.Windows.Application.Current?.Dispatcher is not { } dispatcher) return;
        _desktopDebounce ??= new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) =>
        {
            _desktopDebounce?.Stop();
            RetryClaims();
            ScanAppShortcuts();
        }, dispatcher);
        _desktopDebounce.Stop();
        _desktopDebounce.Start();
    }

    /// <summary>
    /// Açık olduğu için taşınamamış öğeleri sessizce yeniden dener (Windows onayı istenmez). Hiçbir kutuda kalmayan ya da
    /// dosyalar artık taşınmıyorsa liste temizlenir.
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

    /// <summary>Öğeler masaüstüne döner, kutuda kalır (kutu masaüstündeki yolu gösterir). notify: sonucu söyle (sorunlar hep söylenir).</summary>
    public static void Return(IReadOnlyCollection<string> currentPaths, bool notify = true)
    {
        var set = new HashSet<string>(currentPaths, StringComparer.OrdinalIgnoreCase);
        if (set.Count == 0) return;
        Enqueue(() => ReturnJob(records => records.Where(r => r.Active && set.Contains(r.Current)).ToList(), reclaim: false,
            interactive: true, notify ? ReturnNotice.All : ReturnNotice.Problems, keepInBoxes: true));
    }

    /// <summary>
    /// Kutulara taşınan her şeyi masaüstüne geri koyar (Ayarlar'daki "Hepsini masaüstüne geri koy", seçenek kapatılırken);
    /// öğeler kutularda kalır. Bildirimdeki "Geri al" <paramref name="undo"/>'yu masaüstüne dönen yollarla çağırır (verilmezse
    /// dosyalar taşınıyorsa onları yeniden kutulara taşır).
    /// </summary>
    public static void ReturnAll(Action<List<string>>? undo = null) =>
        Enqueue(() => ReturnJob(records => records.Where(r => r.Active).ToList(), reclaim: false, interactive: true, ReturnNotice.All,
            keepInBoxes: true, undo: undo ?? ReclaimPaths));

    /// <summary>Masaüstündeki bu yolları gösteren kutuların öğelerini yeniden kutu klasörlerine taşır ("Geri al").</summary>
    internal static void ReclaimPaths(List<string> paths)
    {
        if (!MovesFiles || paths.Count == 0) return;
        var set = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        var items = Launchers.All(AppHost.Settings)
            .Select(box => (box, BoxPlan.Referenced([box]).Where(set.Contains).ToList()))
            .Where(x => x.Item2.Count > 0).ToList();
        ClaimMany(items, notify: false, interactive: true);
    }

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

        /// <summary>Her sonuç ("Hepsini masaüstüne geri koy", seçeneği kapatmak).</summary>
        All,
    }

    private static ReturnRequest Request(bool interactive, bool reclaim, bool toUser = false) =>
        new(AppHost.DesktopDirectories, AppHost.DesktopDirectory, AppHost.DesktopDirectories.Skip(1).ToList(), interactive, reclaim, toUser);

    /// <param name="keepInBoxes">Öğeler kutuda kalacak: masaüstüne döndükleri anda kurallarla taşınmasınlar (küme dönüşten
    /// sonra kesinleşir).</param>
    /// <param name="undo">Bildirimdeki "Geri al" (masaüstüne dönen yollarla); yoksa yok.</param>
    private static async Task ReturnJob(Func<List<BoxMove>, List<BoxMove>> select, bool reclaim, bool interactive, ReturnNotice notify,
        bool keepInBoxes, bool toUser = false, Action<List<string>>? undo = null)
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
        await BreadcrumbsAsync();
        NotifyReturned(outcomes, notify, interactive, reclaim, undo);
    }

    // Uzlaştırma birleştirilir: kuyrukta en çok bir iş bekler (açılıştaki ve widget'lar açılınca gelen istek tek iş olur).
    private static bool _reconcileQueued;
    private static bool _reconcileInteractive;

    /// <summary>
    /// Kayıtları kutularla uzlaştırır (kutu/öğe/sekme kaldırıldı, "Geri al", düzen uygulandı, açılış): hiçbir kutunun
    /// göstermediği taşınmış öğe masaüstüne döner; kutu geri geldiyse öğeleri yeniden bağlanır (dosyalar taşınıyorsa yeniden
    /// taşınır); dışarıda değişen kayıtlar (Gezgin'de geri taşınmış, silinmiş, yarıda kalmış) kapanır. interactive: kullanıcının
    /// bir eyleminden (ortak masaüstüne dönüş için Windows onay ister); false ise asla sorulmaz.
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
        if (outcomes.Count > 0 || plan.Close.Count > 0) await BreadcrumbsAsync();
        NotifyReturned(outcomes, ReturnNotice.Problems, interactive, reclaim: true);

        // Kutu geri geldi (Geri al, kayıtlı düzen): masaüstüne dönen öğeleri dosyalar taşınıyorsa yeniden kutuya taşı.
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

    private static void NotifyReturned(List<ReturnOutcome> outcomes, ReturnNotice notify, bool interactive, bool reclaim,
        Action<List<string>>? undo = null)
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
                ? L.F("\"{0}\" masaüstüne geri döndü.", TileItem.DisplayName(restored[0].Path!))
                : L.P(restored.Count, "{0} simge masaüstüne geri döndü."));
        if (all && already.Count > 0)
            parts.Add(already.Count == 1
                ? L.F("\"{0}\" zaten masaüstünde.", TileItem.DisplayName(already[0].Path!))
                : L.P(already.Count, "{0} simge masaüstüne geri döndü."));
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
        var back = restored.Concat(already).Select(o => o.Path).OfType<string>().ToList();
        if (undo is not null && back.Count > 0)
        {
            Notice.Show(text, notFound.Count > 0 ? NoticeKind.Warning : NoticeKind.Success, L.T("Geri al"), () => undo(back),
                L.T("Geri almak için buraya tıkla."));
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

    // ---------------------------------------------------------------- dışarı sürükleme

    /// <summary>
    /// Taşınmış öğe kutudan ya da halkadan widget dışına (Gezgin'e, Windows masaüstüne) sürüklendi: bırakıldığı yer diskten
    /// anlaşılır (Gezgin taşımayı sürükleme bittikten sonra da bitirebilir: 2,5 sn boyunca birkaç kez bakılır). Masaüstüne
    /// döndüyse kayıt kapanır ve öğe kutudan çıkar ("Geri al" onu yeniden kutuya koyar); başka bir yere taşındıysa kutudan çıkar;
    /// kopyalandıysa bir şey yapılmaz. Yalnızca etkin kaydı olan NestDesk öğeleri için çağrılır.
    /// </summary>
    public static void DraggedOut(string boxId, LauncherTab? tab, string path) => Enqueue(async () =>
    {
        var log = AppHost.BoxMoves;
        if (log.Snapshot().LastOrDefault(r => r.Active && string.Equals(r.Current, path, StringComparison.OrdinalIgnoreCase)) is not { } record)
            return;
        var dirs = AppHost.DesktopDirectories;
        var (result, found) = (DragOutResult.Copied, (string?)null);
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (attempt > 0) await Task.Delay(500);
            (result, found) = await Task.Run(() => BoxPlan.AfterDragOut(record, Disk.Exists, dirs));
            if (result != DragOutResult.Copied) break;
        }
        if (result == DragOutResult.Copied) return;
        // Bu arada başka bir iş kaydı kapatmış olabilir.
        if (!log.IsMoved(record.Current)) return;
        var organizer = AppHost.Organizer;
        await Task.Run(() =>
        {
            log.MarkReturned(record.Id, found ?? record.Original, reclaim: false);
            if (found is not null) KeepOnDesktop(organizer, found, record.Current);
        });

        var settings = AppHost.Settings;
        LauncherEdits.Removed? removed = null;
        if (Launchers.Find(settings, boxId) is { } box)
        {
            var source = tab is not null && box.Tabs.Contains(tab) ? tab : box.Tabs.FirstOrDefault(t => t is not null && LauncherEntries.IndexOfPath(t, path) >= 0);
            if (source is not null && LauncherEntries.IndexOfPath(source, path) is >= 0 and var index) removed = LauncherEdits.Take(box, source, index);
        }
        // Başka kutular da gösteriyorsa masaüstündeki yolu göstersinler.
        if (found is not null) Launchers.ReplacePaths(settings, [(path, found, false)]);
        AppHost.SaveSettings();
        AppHost.RefreshPinnedPaths();
        Changed?.Invoke();
        RefreshRootState();
        await BreadcrumbsAsync();

        var name = TileItem.DisplayName(found ?? record.Original);
        if (found is null)
        {
            Notice.Show(IsRing(boxId) ? L.F("\"{0}\" halkadan çıktı.", name) : L.F("\"{0}\" kutudan çıktı.", name), NoticeKind.Info);
            return;
        }
        Notice.Show(L.F("\"{0}\" masaüstüne geri döndü.", name), NoticeKind.Success, L.T("Geri al"),
            () => UndoDragOut(boxId, removed, found), L.T("Geri almak için buraya tıkla."));
    });

    /// <summary>Dışarı sürüklenip masaüstüne dönen öğe yeniden kutuya (aynı sekmeye, aynı sıraya) girer ve masaüstünden kalkar.</summary>
    private static void UndoDragOut(string boxId, LauncherEdits.Removed? removed, string desktopPath)
    {
        if (Launchers.Find(AppHost.Settings, boxId) is not { } box) return;
        var tab = removed is not null && box.Tabs.Contains(removed.Tab) ? removed.Tab : box.Tabs.FirstOrDefault();
        if (tab is null) return;
        var back = new LauncherEdits.Removed(tab, removed?.Index ?? int.MaxValue, LauncherEntry.Of(desktopPath), removed?.Look);
        if (!LauncherEdits.Put(box, back) && LauncherEntries.IndexOfPath(tab, desktopPath) < 0) return;
        AppHost.SaveSettings();
        AppHost.RefreshPinnedPaths();
        Changed?.Invoke();
        if (MovesFiles) Claim(box, [desktopPath], justAdded: false, tab, notify: false);
    }

    // ---------------------------------------------------------------- "Bu klasör nedir.txt"

    /// <summary>Açıklama dosyalarını kayıtlara göre arka planda yeniler (kuyruktaki işlerin sonunda).</summary>
    private static Task BreadcrumbsAsync()
    {
        var records = AppHost.BoxMoves.Snapshot();
        var root = Root;
        return Task.Run(() => WriteBreadcrumbs(records, [root]));
    }

    /// <summary>
    /// Her NestDesk kökünde "Bu klasör nedir.txt": ne olduğunu, nasıl geri konacağını ve her öğenin eski klasörünü anlatır
    /// (Store ve taşınabilir sürümün kaldırma adımı yoktur). Anlatacak öğe kalmadıysa silinir, boşalan kök de. Disk işi: arka
    /// planda çağrılır; hata fırlatmaz.
    /// </summary>
    public static void WriteBreadcrumbs(IReadOnlyList<BoxMove> records, IEnumerable<string> roots)
    {
        var all = records.Select(r => BoxPlan.RootOf(r.Current)).OfType<string>().Concat(roots)
            .Select(Path.TrimEndingDirectorySeparator).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var root in all)
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                var text = BoxPlan.BreadcrumbText(records, root);
                var existing = Directory.EnumerateFiles(root).Where(f => BoxPlan.IsBreadcrumb(Path.GetFileName(f))).ToList();
                if (text is null)
                {
                    foreach (var file in existing) File.Delete(file);
                    BoxFiles.RemoveIfEmpty(root, root);
                    continue;
                }
                var target = Path.Combine(root, BoxPlan.BreadcrumbName);
                foreach (var old in existing.Where(f => !string.Equals(f, target, StringComparison.OrdinalIgnoreCase))) File.Delete(old);
                if (File.Exists(target) && File.ReadAllText(target) == text) continue;
                // BOM'lu UTF-8: Not Defteri Türkçe karakterleri doğru göstersin.
                File.WriteAllText(target, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLog.Warn("box", "breadcrumb-failed", ex);
            }
        }
    }

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
