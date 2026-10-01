using System.IO;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Widgets;

namespace Duzenleme;

/// <summary>Uygulama genelinde paylaşılan servisler.</summary>
public static class AppHost
{
    public static string DataDirectory { get; private set; } = "";
    public static string DesktopDirectory { get; private set; } = "";

    /// <summary>Masaüstünde görünen klasörler: kullanıcının masaüstü ve (test klasörü verilmediyse) Genel Masaüstü.</summary>
    public static IReadOnlyList<string> DesktopDirectories { get; private set; } = [];

    /// <summary>--desktop ile verilen test klasörü mü izleniyor?</summary>
    public static bool IsTestDesktop { get; private set; }
    public static bool IsPortable { get; private set; }
    public static AppSettings Settings { get; private set; } = new();
    public static MoveJournal Journal { get; private set; } = null!;
    public static DesktopOrganizer Organizer { get; private set; } = null!;
    public static DesktopWatcher Watcher { get; private set; } = null!;
    public static WidgetManager Widgets { get; private set; } = null!;
    public static TrayIcon? Tray { get; set; }
    public static HotkeyManager? Hotkeys { get; set; }
    public static DesktopDoubleClick? DoubleClick { get; set; }

    /// <summary>
    /// Masaüstü klasörlerinin (ve klasör bölmelerinin klasörlerinin) paylaşılan, arka planda güncel tutulan içerik
    /// listeleri. Arayüz masaüstünü diskten değil buradan okur.
    /// </summary>
    public static DirectorySnapshots Snapshots { get; private set; } = null!;

    /// <summary>Kullanıcının masaüstünün anlık görüntüsü (uygulama açık kaldıkça izlenir).</summary>
    public static DirectorySnapshot DesktopSnapshot { get; private set; } = null!;

    /// <summary>
    /// Ayarlar değişti (UI iş parçacığında). Art arda gelen kayıtlar tek bildirimde birleşir (aynı iş dağıtıcı turundaki
    /// her şey bittikten sonra, Background önceliğinde); dosyaya yazma bundan bağımsız olarak kısa bir süre sonra yapılır.
    /// </summary>
    public static event Action? SettingsChanged;

    /// <summary>Windows simgeleri, widget'lar ya da göz atma değiştiğinde tetiklenir (UI iş parçacığında).</summary>
    public static event Action? DesktopVisibilityChanged;

    /// <summary>Masaüstü gizlendi mi (Ctrl+Alt+H, çift tık, "Masaüstünü şimdi gizle")? Yalnızca bu oturumda tutulur.</summary>
    public static bool DesktopHidden { get; private set; }

    /// <summary>
    /// Windows masaüstüne göz atılıyor mu? Yalnızca bu oturumda tutulur: çökerse simgeler görünür kalır (IconsHiddenByApp
    /// göz atarken false'tur).
    /// </summary>
    public static bool Peeking { get; private set; }

    /// <summary>Kutulara taşınan masaüstü öğelerinin kaydı (box-moves.json).</summary>
    public static BoxMoveLog BoxMoves { get; private set; } = null!;

    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    private static SettingsStore? _store;
    private static Dispatcher? _dispatcher;
    private static bool _changedQueued;

    /// <summary>Kayıt zamanlayıcısı (ölçüm ve tanılama için).</summary>
    internal static SettingsStore? Store => _store;

    /// <summary>
    /// Veri klasörünün diskteki (Gezgin'in gördüğü) yeri. Store (MSIX) sürümünde Windows %AppData% altına yeni yazılan
    /// dosyaları pakete özel LocalCache klasörüne yönlendirir; paket dışındaki Gezgin onları DataDirectory'de göremez.
    /// Paketsizken DataDirectory'nin kendisi.
    /// </summary>
    public static string DataDirectoryOnDisk
    {
        get
        {
            if (PackageInfo.FamilyName is not { } family) return DataDirectory;
            var redirected = PackagedApp.RedirectedRoamingPath(DataDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), family);
            return PackagedApp.DataFolderToShow(DataDirectory, redirected, File.Exists, Directory.Exists);
        }
    }

    /// <summary>Taşınabilir mod istendi ama klasöre yazılamadığı için %AppData% kullanılıyor.</summary>
    public static bool PortableFallback { get; private set; }

    /// <summary>Exe'nin yanında "portable.txt" varsa ayarlar exe'nin yanındaki "data" klasöründe tutulur (USB bellekte taşınabilir).</summary>
    private static string? PortableDataDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        if (!File.Exists(Path.Combine(baseDir, "portable.txt"))) return null;
        var dir = Path.Combine(baseDir, "data");
        try
        {
            // Salt okunur bir klasörden (ör. zip içinden, CD'den) çalışıyorsa yazılabilir mi dene.
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".yazma-testi");
            File.WriteAllText(probe, "");
            // Yazabildiysek klasör kullanılabilir; deneme dosyası silinemese de (ör. virüs tarayıcısı açık tutuyor) sorun değil.
            try { File.Delete(probe); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return dir;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PortableFallback = true;
            return null;
        }
    }

    /// <summary>
    /// Masaüstü klasörü: yönlendirilmiş (OneDrive, ağ) olsa da yolu doğrulamadan alınır; boş dönerse varsayılan konum.
    /// </summary>
    private static string ResolveDesktop()
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
        return path;
    }

    /// <summary>Veri klasörünün nereden geldiği; bu açılışta eski %AppData%\Duzenleme'den taşındıysa Moved.</summary>
    public static DataFolderSource DataFolderSource { get; private set; }

    /// <summary>
    /// %AppData% (Roaming). Test örneğinde (--desktop) NESTDESK_APPDATA_ROOT verilirse o klasör: veri klasörü geçişi
    /// kullanıcının gerçek %AppData%'sına dokunmadan denenebilsin (--data verilmediğinde).
    /// </summary>
    private static string RoamingAppData(bool testDesktop) =>
        testDesktop && AppEnvironment.Get("APPDATA_ROOT") is { } root
            ? root
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>
    /// --desktop ile --data verilmediyse test örneğinin veri klasörü geçici klasöre düşer (bkz.
    /// <see cref="DataFolderLocator.TestDataFallback"/>): kullanıcının gerçek veri klasörü taşınmaz, üzerine yazılmaz.
    /// </summary>
    private static string? DataOverrideFor(string? desktopOverride, string? dataOverride)
    {
        if (DataFolderLocator.TestDataFallback(desktopOverride, dataOverride, AppEnvironment.Get("APPDATA_ROOT") is not null,
                Path.GetTempPath()) is not { } testData) return dataOverride;
        DebugLog.Write($"test örneği --data olmadan açıldı: veriler {testData} klasöründe"); // l10n: çevrilmez (günlük)
        return testData;
    }

    public static void Initialize(string? desktopOverride, string? dataOverride)
    {
        IsTestDesktop = desktopOverride is not null;
        dataOverride = DataOverrideFor(desktopOverride, dataOverride);
        _dispatcher = Dispatcher.CurrentDispatcher;
        // Store (MSIX) paketinin klasörü salt okunurdur ve portable.txt taşımaz: paketliyken taşınabilir mod denenmez.
        var portable = dataOverride is null && !PackageInfo.IsPackaged ? PortableDataDirectory() : null;
        IsPortable = portable is not null;
        // 2.1: %AppData%\Duzenleme → %AppData%\NestDesk. Tek örnek kilidi burada zaten tutuluyor (App.OnStartup): klasöre
        // aynı anda yazan başka bir NestDesk/Düzenleme yok. Taşınamazsa bu oturum eski klasörle çalışır. Test örneği gerçek
        // kilidi tutmaz; --data'sız açılsa da buraya gerçek %AppData% ile gelmez (DataOverrideFor).
        var data = DataFolderLocator.Apply(
            DataFolderLocator.Plan(dataOverride, portable, PackageInfo.IsPackaged, RoamingAppData(IsTestDesktop)), log: DebugLog.Write);
        DataDirectory = data.Directory;
        DataFolderSource = data.Source;
        DesktopDirectory = desktopOverride ?? ResolveDesktop();
        // Kurulan programların kısayolları çoğunlukla Genel Masaüstü'ndedir; "Kısayollar" bölmesi onları da göstersin.
        var common = desktopOverride is null
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolderOption.DoNotVerify)
            : "";
        DesktopDirectories = string.IsNullOrWhiteSpace(common) || string.Equals(common, DesktopDirectory, StringComparison.OrdinalIgnoreCase)
            ? [DesktopDirectory]
            : [DesktopDirectory, common];
        Directory.CreateDirectory(DataDirectory);

        // Masaüstü okuması (Genel Masaüstü dahil) hemen arka planda başlar: widget'lar açılırken liste büyük ihtimalle
        // hazırdır. Uygulama açık kaldıkça izlenir (klasör listesi arayüzde diske dokunmadan okunur).
        var dispatcher = _dispatcher;
        Snapshots = new DirectorySnapshots(action => dispatcher.BeginInvoke(action, DispatcherPriority.Background));
        DesktopSnapshot = Snapshots.Acquire(DesktopDirectory);
        foreach (var dir in DesktopDirectories.Skip(1)) Snapshots.Acquire(dir);

        // Tanınmayan enum değeri (daha yeni sürümden) ayarları silmez, yedek değere döner: günlüğe yazılır.
        JsonFile.Log = DebugLog.Write;
        // Okunamayan ayar/geçmiş/kutu dosyası: hata günlüğüne yazılır, arayüz hazır olunca doğru metinle söylenir.
        JsonFile.Corrupted = OnFileCorrupted;
        Settings = JsonFile.Load(SettingsPath, () => new AppSettings());
        // Hata günlüğü (DebugLog'dan ayrı, temizlenmiş): bu ana dek bellekte bekleyen kayıtlar da yazılır ("Hata günlüğü tut"
        // kapalıysa hiçbir dosya oluşmaz). Yalnızca asıl örnek buraya gelir.
        AppLog.Start(LogsDirectory, new LogScrubber(ScrubContext.ForCurrentUser(DesktopDirectories, DataDirectory, BoxMover.Root)),
            persist: !Settings.ErrorLogOff, mirror: DebugLog.Enabled ? DebugLog.Write : null);
        StartSession();
        _store = new SettingsStore(new DurableFile(SettingsPath), () => JsonFile.Serialize(Settings),
            tick => new DispatcherOwnerTimer(dispatcher, tick));
        _store.File.Failed += OnSettingsWriteFailed;
        _store.File.Recovered += () =>
        {
            DebugLog.Write("ayarlar yeniden yazılabiliyor");
            AppLog.Info("settings", "write-recovered");
        };
        Journal = new MoveJournal(Path.Combine(DataDirectory, "journal.json"));
        Journal.WriteFailed += ex =>
        {
            DebugLog.Write($"geçmiş yazılamadı: {ex.Message}");
            AppLog.Warn("journal", "write-failed", ex);
        };
        Organizer = new DesktopOrganizer(DesktopDirectory, () => Settings, Journal);
        Organizer.MoveFailed += OnMoveFailed;
        Watcher = new DesktopWatcher(Organizer, () => Settings.Paused);
        BoxMoves = new BoxMoveLog(Path.Combine(DataDirectory, "box-moves.json"));
        Widgets = new WidgetManager();
        Icons.FolderIconService.Changed += OnFolderIconChanged;
        BackgroundIo.Run($"{AppInfo.Name} ayar yedeği", BackupSettingsDaily); // l10n: çevrilmez (iş parçacığı adı)
        // Kutulardaki masaüstü öğeleri kurallarla taşınmasın; widget eklenince/kaldırılınca küme yenilenir, kaldırılan
        // kutunun taşınmış öğeleri masaüstüne döner (kutu "Geri al" ile gelirse yeniden taşınır).
        Widgets.Changed += () =>
        {
            RefreshPinnedPaths();
            BoxMover.Reconcile();
        };
        RefreshPinnedPaths();

        // Önceki oturum simgeleri gizli bırakarak kapandıysa (ör. çökme) geri aç.
        // Bölmeler masaüstünü yönetiyorsa gizli kalır; widget'lar açılınca yeniden uygulanır.
        if (Settings.IconsHiddenByApp && !Settings.FencesReplaceIcons)
        {
            if (!IsTestDesktop) DesktopIcons.SetVisible(true);
            Settings.IconsHiddenByApp = false;
            SaveSettings();
        }
    }

    /// <summary>
    /// Ayarlar değişti, kısa süre içinde kaydedilsin. Diske hemen yazmaz: ilk değişiklikten ~0,5 saniye sonra o ana dek
    /// birikenler arka planda tek seferde, atomik ve yedekli yazılır (bkz. <see cref="SettingsStore"/>).
    /// <see cref="SettingsChanged"/> birleştirilmiş olarak tetiklenir. Herhangi bir iş parçacığından çağrılabilir.
    /// </summary>
    public static void SaveSettings()
    {
        if (!OnUiThread(SaveSettings)) return;
        _store?.MarkDirty();
        if (PerfLog.Enabled)
        {
            // Kim kaydettirdi? (Yalnızca ölçüm günlüğü açıkken; yığın okumak pahalıdır.)
            var frame = new System.Diagnostics.StackTrace(1, false).GetFrame(0)?.GetMethod();
            PerfLog.Count("SaveSettings");
            PerfLog.Write($"SaveSettings ← {frame?.DeclaringType?.Name}.{frame?.Name}");
        }
        if (_changedQueued || _dispatcher is null) return;
        _changedQueued = true;
        _dispatcher.BeginInvoke(() =>
        {
            _changedQueued = false;
            SettingsChanged?.Invoke();
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Önemsiz ve sık değişen durum (ör. widget'lar arası sıra): bir sonraki kayıtla birlikte yazılır (en geç bir dakika
    /// içinde, çıkışta mutlaka). <see cref="SettingsChanged"/> tetiklenmez; tıklama başına disk yazması olmaz.
    /// </summary>
    public static void SaveSettingsLater()
    {
        if (!OnUiThread(SaveSettingsLater)) return;
        _store?.MarkDirtyLazy();
    }

    /// <summary>
    /// Bekleyen değişiklikleri hemen, eşzamanlı yazar: çıkış, oturum kapanışı, çökme ve çökmede kaybolmaması gereken
    /// bayraklar (<see cref="AppSettings.IconsHiddenByApp"/>). Yazıldıysa true; hata fırlatmaz.
    /// </summary>
    /// <param name="changed">
    /// Çağıran bir ayarı az önce değiştirdi ve o değer şimdi diskte olmalı. Verilmezse yalnızca daha önce
    /// <see cref="SaveSettings"/> ile bildirilmiş değişiklikler yazılır: kirli bir şey yoksa dosyaya hiç dokunulmaz.
    /// </param>
    public static bool SaveSettingsNow(TimeSpan? timeout = null, bool changed = false)
    {
        if (_store is null) return false;
        var sw = PerfLog.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
        var ok = _store.FlushNow(timeout ?? TimeSpan.FromSeconds(2), changed);
        if (sw is not null) PerfLog.Write($"SaveSettingsNow {sw.Elapsed.TotalMilliseconds:0.0} ms ok={ok}");
        return ok;
    }

    /// <summary>Çıkış ve oturum kapanışı: ayarları ve taşıma geçmişini diske indirir.</summary>
    public static void FlushAll()
    {
        SaveSettingsNow(TimeSpan.FromSeconds(3));
        if (PerfLog.Enabled)
            PerfLog.Write($"sayaçlar: {PerfLog.Summary()} ayar yazma={_store?.File.WriteCount} geçmiş yazma={Journal?.WriteCount}"); // l10n: çevrilmez
        try { Journal?.Flush(TimeSpan.FromSeconds(2)); }
        catch (Exception ex)
        {
            DebugLog.Write($"geçmiş yazılamadı: {ex.Message}");
            AppLog.Warn("journal", "flush-failed", ex);
        }
        AppLog.FlushSync(TimeSpan.FromMilliseconds(500));
    }

    /// <summary>Arayüz iş parçacığında değilse işi oraya aktarır ve false döner.</summary>
    private static bool OnUiThread(Action action)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess()) return true;
        _dispatcher.BeginInvoke(action);
        return false;
    }

    private static int _moveBlockedWarned;

    /// <summary>
    /// Otomatik taşıma bir dosyayı taşıyamadı (izleyici iş parçacığından gelir). Kilitli dosya olağandır, sonra yeniden
    /// denenir; yalnızca Windows yazmayı engellediyse (Denetimli klasör erişimi vb.) oturumda bir kez söylenir: yoksa
    /// dosyalar sessizce taşınmaz ve kullanıcı nedenini bilemez.
    /// </summary>
    private static void OnMoveFailed(string path, Exception ex)
    {
        DebugLog.Write($"taşınamadı: {path}: {ex.GetType().Name} {ex.Message}");
        // Kilitli dosya olağandır (indiriliyor, açık): hata sayılmaz, yalnızca sayaç. Engellenen yazma uyarıdır.
        if (FileLocks.IsSharingViolation(ex)) AppLog.Count("organizer.locked");
        else if (ex is UnauthorizedAccessException) AppLog.Warn("organizer", "move-denied", ex);
        else AppLog.Info("organizer", "move-failed", ex);
        if (ex is not UnauthorizedAccessException) return;
        if (Interlocked.Exchange(ref _moveBlockedWarned, 1) == 1) return;
        var result = FolderCreation.Explain(ex, path);
        _dispatcher?.BeginInvoke(() => Views.Notice.FolderFailed(result with
        {
            Error = L.F("\"{0}\" taşınamadı. {1}", Path.GetFileName(path), result.Error),
        }));
    }

    private static int _writeWarnings;

    private static void OnSettingsWriteFailed(Exception ex)
    {
        DebugLog.Write($"ayarlar yazılamadı: {ex}");
        AppLog.Warn("settings", "write-failed", ex);
        // Kullanıcıya bir oturumda en fazla iki kez söylenir; değişiklikler bellekte durur, yazma arka planda yeniden denenir.
        if (Interlocked.Increment(ref _writeWarnings) > 2) return;
        _dispatcher?.BeginInvoke(() => Views.Notice.Show(
            L.T("Ayarlar şu an kaydedilemedi (dosyayı başka bir program kullanıyor olabilir). Değişikliklerin duruyor; kayıt birazdan yeniden denenecek."),
            Views.NoticeKind.Warning));
    }

    /// <summary>
    /// Çökme yolu (herhangi bir iş parçacığından), sırayla: simgeleri bizim gizlediğimiz hâlde bırakma, ayarları ve taşıma
    /// geçmişini diske indir (kullanıcının verisi önce), sonra kısa ve temizlenmiş çökme raporu (çağıran iş parçacığında,
    /// eşzamanlı, süreçte bir kez; "Kaydetme" seçiliyse hiçbir şey) ve hata günlüğünün bekleyenleri. Süreç sonlanmıyorsa
    /// simgelere dokunulmaz ve rapor yazılmaz.
    /// </summary>
    public static void OnCrash(bool terminating, Exception? exception = null)
    {
        try
        {
            if (terminating && CurrentView.IconsHidden && !IsTestDesktop)
            {
                // ShowWindowAsync: iş parçacığından bağımsız, Gezgin'i beklemez.
                DesktopIcons.SetVisible(true);
                Settings.IconsHiddenByApp = false;
                _store?.MarkDirty();
            }
        }
        catch (Exception ex) { DebugLog.Write("çökme: simgeler " + ex.Message); }
        try { _store?.FlushNow(TimeSpan.FromSeconds(2)); }
        catch (Exception ex) { DebugLog.Write("çökme: ayarlar " + ex.Message); }
        try { Journal?.Flush(TimeSpan.FromSeconds(1)); }
        catch (Exception ex) { DebugLog.Write("çökme: geçmiş " + ex.Message); }
        if (terminating && DataDirectory.Length > 0)
        {
            // Ayarlar okunamadan çöktüyse varsayılan: sor.
            var settings = Settings;
            try
            {
                CrashReports.WriteOnce(CrashesDirectory, settings?.CrashReportMode, settings?.ErrorLogOff ?? false, CrashReports.KindCrash,
                    exception, AppLog.Recent(CrashReports.MaxLogTail), AppLog.Channel.Scrubber, AppLog.Channel.Session, DateTime.UtcNow);
            }
            catch (Exception ex) { DebugLog.Write("çökme: rapor " + ex.Message); }
        }
        AppLog.FlushSync(TimeSpan.FromMilliseconds(500));
    }

    public static void SetPaused(bool paused)
    {
        Settings.Paused = paused;
        SaveSettings();
        if (!paused) OrganizeNowInBackground();
    }

    /// <summary>
    /// Masaüstündeki uygun dosyaları şimdi taşır ve taşınanları döner. Açık komutlar ("Masaüstünü şimdi düzenle", tepsi,
    /// Ctrl+Alt+O) otomatik taşıma kapalıyken de çalışır.
    /// </summary>
    public static Task<List<MoveEntry>> OrganizeNowAsync() => Task.Run(() => Organizer.OrganizeAll());

    public static void OrganizeNowInBackground() => _ = OrganizeNowAsync();

    /// <summary>
    /// Otomatik taşıma açıksa masaüstünü şimdi düzenler. Klasör oluşturan kod bunu kullanır: kapalıyken yeni klasör
    /// açmak dosya taşımaya başlamamalı (yeni kullanıcı karşılamada onay verene dek hiçbir dosya taşınmaz).
    /// </summary>
    public static void OrganizeIfActive()
    {
        if (!Settings.Paused) OrganizeNowInBackground();
    }

    /// <summary>
    /// Açılıştaki acelesi olmayan disk işleri: ilk masaüstü taraması (otomatik taşıma açıksa) ve eski klasör simgelerinin
    /// onarımı. Widget'lar önce açılsın diye gecikmeli (Windows ile başlarken oturum açılışıyla yarışmasın diye daha uzun)
    /// ve düşük disk önceliğiyle çalışır. Aradaki yeni dosyaları izleyici zaten yakalar.
    /// </summary>
    public static void StartDeferredWork(bool autostart)
    {
        var desktop = DesktopDirectory;
        BackgroundIo.Run($"{AppInfo.Name} açılış taraması", () => // l10n: çevrilmez (iş parçacığı adı)
        {
            if (!Settings.Paused)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var moved = Organizer.OrganizeAll();
                PerfLog.Write($"açılış taraması {sw.ElapsedMilliseconds} ms, {moved.Count} dosya taşındı"); // l10n: çevrilmez
            }
            // Eski sürümün mutlak yollu klasör simgelerini onar.
            Icons.FolderIconService.RepairDesktopFolders(desktop);
        }, autostart ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(3),
            ex =>
            {
                DebugLog.Write($"açılış taraması: {ex}");
                AppLog.Error("startup", "deferred-scan-failed", ex);
            });
        // Tanılama bakımı ve önceki oturumun sonu: eski günlükler/raporlar silinir; çökme ya da donma raporu bekliyorsa
        // arayüz hazır olunca şeritte (pencere gizliyse balonda) sorulur.
        var logs = LogsDirectory;
        var mode = Settings.CrashReportMode;
        var logOff = Settings.ErrorLogOff;
        BackgroundIo.Run($"{AppInfo.Name} tanılama", () => CheckPreviousSession(logs, mode, logOff), // l10n: çevrilmez (iş parçacığı adı)
            autostart ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(3),
            ex => AppLog.Warn("diagnostics", "startup-check-failed", ex));
    }

    /// <summary>
    /// Masaüstündeki klasör adları; arayüz iş parçacığında diske dokunmadan (anlık görüntüden) okunur. Açılışta ilk okuma
    /// henüz bitmediyse bir kez diskten okunur.
    /// </summary>
    public static List<string> DesktopFolders()
    {
        switch (DesktopSnapshot?.State)
        {
            case SnapshotState.Ready:
                return DesktopSnapshot.Entries.Where(e => e.IsDirectory).Select(e => e.Name).ToList();
            case SnapshotState.Missing or SnapshotState.Unreadable:
                return [];
        }
        try { return Organizer.ExistingFolders().ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>
    /// Uygulamanın az önce oluşturduğu klasörü, izleyiciyi beklemeden masaüstü listesine ekler: yeni klasör bölmesi bir an
    /// "klasör yok" demez. Directory.CreateDirectory başarılı olduktan sonra çağrılır.
    /// </summary>
    public static void NoteFolderCreated(string path)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path));
        if (parent is not null) Snapshots?.Find(parent)?.NoteCreated(Path.TrimEndingDirectorySeparator(path), isDirectory: true);
    }

    // Uygulamanın kendi açtığı klasörler: masaüstünde yeni klasör görülünce çıkan "simge ver" balonu bunlar için çıkmaz.
    // Yalnızca UI iş parçacığından çağrılır (klasör izleyicisinin geri çağrısı da Dispatcher'a aktarılır).
    private static readonly HashSet<string> _quietFolders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Az sonra oluşturulacak klasörü "sessiz" işaretler (Directory.CreateDirectory'den önce çağrılır).</summary>
    public static void MarkQuietFolder(string path) => _quietFolders.Add(QuietKey(path));

    /// <summary>Klasör sessiz işaretliyse işareti kaldırır ve true döner.</summary>
    public static bool ConsumeQuietFolder(string path) => _quietFolders.Remove(QuietKey(path));

    // İzleyicinin verdiği yolla (FileSystemWatcher.FullPath) aynı biçim: tam yol, sonda ayraç yok. Diske dokunmaz.
    private static string QuietKey(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    // ------------------------------------------------------------------------------------------------------------------
    // Masaüstünün görünürlüğü: tek yerde hesaplanır (DesktopState.Compute) ve tek yerde uygulanır (ApplyDesktopState).
    // Widget'ları ya da simgeleri başka yerden gizleyip göstermek tepsiyi, Ayarlar'ı ve kısayolu durumla çelişik bırakır.

    /// <summary>Şu anki durumdan Windows simgeleri ve widget'lar gizli mi olmalı?</summary>
    public static DesktopView CurrentView => DesktopState.Compute(Settings.FencesReplaceIcons, DesktopHidden, Peeking,
        Settings.PeekHidesWidgets, Settings.HideWidgetsWithIcons);

    /// <summary>
    /// Durumu uygular: Windows simgeleri (test örneğinde dokunulmaz), widget'lar ve çökme bayrağı; sonra
    /// <see cref="DesktopVisibilityChanged"/>. <see cref="AppSettings.IconsHiddenByApp"/> eşzamanlı yazılır ve gizlerken
    /// simgelerden ÖNCE: uygulama o an zorla kapatılsa da sonraki açılış ve kaldırma programı simgeleri geri açabilsin.
    /// </summary>
    public static void ApplyDesktopState()
    {
        var view = CurrentView;
        if (view.IconsHidden) ArmIconsHiddenFlag();
        // Test klasörüyle (--desktop) çalışan örnek kullanıcının gerçek masaüstü simgelerine dokunmaz.
        if (IsTestDesktop) DebugLog.Write($"simgeler {(view.IconsHidden ? "gizlenecekti" : "gösterilecekti")} (test masaüstü)");
        else if (!DesktopIcons.SetVisible(!view.IconsHidden) && view.IconsHidden && IconsUnreachable())
            view = CurrentView;
        else if (view.IconsHidden && DesktopIcons.HiddenByWindows) WarnIconsOffInWindows();
        Widgets.SetHidden(view.WidgetsHidden);
        if (!view.IconsHidden && Settings.IconsHiddenByApp)
        {
            // Gösterirken bayrak sonra ve beklemeden değil, olağan kayıtla düşer: arada kapanırsa sonraki açılış simgeleri
            // (zaten görünür) bir kez daha açar; zararsızdır. Her göz atmada/göstermede eşzamanlı yazma olmasın.
            Settings.IconsHiddenByApp = false;
            _store?.MarkDirty();
        }
        DesktopVisibilityChanged?.Invoke();
    }

    private static bool _iconsUnreachableWarned, _iconsOffWarned;

    /// <summary>
    /// Simgeler gizlenecekti ama Windows'un simge listesi bulunamadı (Gezgin yeniden başlıyor ya da masaüstünü başka bir
    /// program yönetiyor). Kullanıcının "gizle"si geri alınır ve bir kez söylenir: arayüz "gizli" deyip hiçbir şey olmamış
    /// gibi durmasın. Bölmeler masaüstünü yönetiyorsa kip kalır; nöbetçi birkaç saniyede bir yeniden dener. Durum değiştiyse true.
    /// </summary>
    private static bool IconsUnreachable()
    {
        DebugLog.Write("simge listesi bulunamadı: " + DesktopIcons.Describe());
        if (!DesktopHidden || Settings.FencesReplaceIcons) return false;
        DesktopHidden = false;
        if (!_iconsUnreachableWarned)
        {
            _iconsUnreachableWarned = true;
            _dispatcher?.BeginInvoke(() => Views.Notice.Show(
                L.T("Windows masaüstü simgelerine şu an ulaşılamıyor, bu yüzden gizlenemediler. Gezgin yeniden başlıyorsa birkaç saniye sonra yeniden dene."),
                Views.NoticeKind.Warning));
        }
        return true;
    }

    /// <summary>Windows'un kendi "Masaüstü simgelerini göster" seçeneği kapalı: gizleme görünürde bir şey değiştirmez; bir kez söylenir.</summary>
    private static void WarnIconsOffInWindows()
    {
        DebugLog.Write("masaüstü simgeleri Windows'ta kapalı: " + DesktopIcons.Describe());
        if (_iconsOffWarned || !DesktopHidden) return;
        _iconsOffWarned = true;
        _dispatcher?.BeginInvoke(() => Views.Notice.Show(
            L.T("Windows'ta masaüstü simgeleri zaten kapalı. Açmak için masaüstüne sağ tıkla → Görünüm → Masaüstü simgelerini göster."),
            Views.NoticeKind.Info));
    }

    /// <summary>
    /// Windows simgeleri gizlenmeden hemen önce: <see cref="AppSettings.IconsHiddenByApp"/> diske eşzamanlı yazılır (başka
    /// bir değişiklik beklemeden). Uygulama sonra zorla kapatılsa da (Görev Yöneticisi, kurulum programı) sonraki açılış ve
    /// kaldırma programı (--restore-desktop) simgeleri geri açabilir. Bayrak zaten diskteyse bir şey yapmaz.
    /// </summary>
    private static void ArmIconsHiddenFlag()
    {
        if (Settings.IconsHiddenByApp) return;
        Settings.IconsHiddenByApp = true;
        SaveSettingsNow(changed: true);
    }

    /// <summary>
    /// Widget'lar gizliyse (masaüstü gizlendi ya da göz atılıyor) geri getirir: yeni widget, "Bul", "öne getir", düzen
    /// uygulama… Gizleyen durum kapanır, simgeler de ona göre döner. Widget'lar zaten görünürse bir şey yapmaz.
    /// </summary>
    public static void EnsureWidgetsShown()
    {
        if (!CurrentView.WidgetsHidden) return;
        StopPeek();
        DesktopHidden = false;
        ApplyDesktopState();
    }

    /// <summary>Masaüstü simgelerini (ve ayara göre widget'ları) gizler ya da gösterir. Göz atılıyorsa göz atma biter.</summary>
    public static void ToggleDesktop() => SetDesktopHidden(Peeking || !DesktopHidden);

    public static void SetDesktopHidden(bool hidden)
    {
        StopPeek();
        DesktopHidden = hidden;
        ApplyDesktopState();
    }

    /// <summary>
    /// Boş masaüstüne çift tıklama (ayara göre gizle/göster ya da göz at; göz atılıyorsa NestDesk'e döner). İlk birkaç seferde
    /// nasıl geri getirileceği söylenir: bilmeden çift tıklayan kullanıcı widget'larının kaybolduğunu sanmasın.
    /// </summary>
    public static void OnDesktopDoubleClick()
    {
        if (Peeking)
        {
            EndPeek();
            return;
        }
        switch (DesktopState.ResolveDoubleClick(Settings.DoubleClickAction, Settings.DoubleClickHidesDesktop, Settings.FencesReplaceIcons))
        {
            case DoubleClickEffect.Peek:
                StartPeek(PeekOrigin.DoubleClick);
                if (!Peeking || Settings.PeekHintsShown >= 3) return;
                Settings.PeekHintsShown++;
                SaveSettings();
                Tray?.Notify(L.T("Windows masaüstü açıldı"),
                    L.F("Geri dönmek için yeniden çift tıkla ya da üstteki \"{0}'e dön\"e bas. Çift tıklamanın ne yapacağını Ayarlar > Masaüstü'nden seçebilirsin.", AppInfo.Name),
                    () => EndPeek());
                break;
            case DoubleClickEffect.ToggleDesktop:
                ToggleDesktop();
                if (!DesktopHidden || Settings.DoubleClickHintsShown >= 3) return;
                Settings.DoubleClickHintsShown++;
                SaveSettings();
                Tray?.Notify(Widgets.Hidden ? L.T("Widget'lar ve simgeler gizlendi") : L.T("Masaüstü simgeleri gizlendi"),
                    L.T("Masaüstüne yeniden çift tıkla ya da buraya tıkla, geri gelsin. Bu özellik Ayarlar'dan kapatılabilir."),
                    () => SetDesktopHidden(false));
                break;
        }
    }

    // --- Windows masaüstüne göz at ---

    /// <summary>Göz atmayı neyin başlattığı: çift tıklamada pencereler küçültülmez (masaüstü zaten öndedir).</summary>
    public enum PeekOrigin { Hotkey, Tray, Menu, Command, DoubleClick, Settings }

    /// <summary>Göz atma açık pencereleri küçülterek başladıysa (Win+D gibi); bitince yeniden açılır.</summary>
    private static bool _peekMinimized;

    /// <summary>
    /// Ayarlar'daki "Göz at" NestDesk'in kendi penceresini küçülttüyse onun önceki hâli (bitince geri gelir); yoksa null.
    /// </summary>
    private static System.Windows.WindowState? _peekMainWindowState;

    public static void TogglePeek(PeekOrigin origin)
    {
        if (Peeking) EndPeek();
        else StartPeek(origin);
    }

    /// <summary>
    /// Windows masaüstüne göz at: simgeler görünür, widget'lar (ayar açıksa) çekilir, üstte "NestDesk'e dön" çubuğu çıkar ve
    /// ayardaki süre dolunca kendiliğinden dönülür. Gizli masaüstü de açılır (dönünce normal NestDesk masaüstü gelir).
    /// </summary>
    public static void StartPeek(PeekOrigin origin)
    {
        if (Peeking) return;
        // Çubuk kullanıcının çalıştığı ekranda açılır; pencereler küçültülmeden önce bakılır (sonra etkin pencere değişir).
        var barAnchor = Views.PeekBar.Anchor();
        Peeking = true;
        DesktopHidden = false;
        // "Açık pencereleri küçült": masaüstü zaten öndeyse (çift tıklama) yapılmaz. Test örneği gerçek pencereleri küçültmez.
        _peekMinimized = Settings.PeekShowsDesktop && origin != PeekOrigin.DoubleClick && !IsTestDesktop &&
                         !DesktopIcons.IsDesktopSurface(NativeMethods.GetForegroundWindow());
        if (_peekMinimized) ShellDesktop.ToggleInBackground();
        // Ayarlar'daki "Göz at": ana pencere masaüstünü kapatmasın (yoksa düğme bir şey yapmamış gibi görünür ve süre
        // kullanıcı masaüstünü görmeden işler). Yalnızca NestDesk'in kendi penceresi küçülür; başka pencerelere dokunulmaz.
        else if (origin == PeekOrigin.Settings) MinimizeMainWindowForPeek();
        ApplyDesktopState();
        Views.PeekBar.Open(DesktopState.NormalizePeekMinutes(Settings.PeekMinutes), barAnchor);
        DebugLog.Write($"göz atma başladı ({origin})");
    }

    /// <summary>NestDesk'e dön: göz atma biter, widget'lar ve (bölmeler yönetiyorsa) gizli simgeler geri gelir.</summary>
    public static void EndPeek()
    {
        if (!Peeking) return;
        StopPeek();
        ApplyDesktopState();
    }

    /// <summary>Göz atmayı durumu uygulamadan bitirir (çağıran hemen ardından uygular).</summary>
    private static void StopPeek()
    {
        if (!Peeking) return;
        Peeking = false;
        Views.PeekBar.CloseBar();
        // Küçültülen pencereler geri gelsin; kullanıcı bu arada masaüstünden bir şey açtıysa (masaüstü artık önde değil) dokunulmaz.
        if (_peekMinimized && DesktopIcons.IsDesktopSurface(NativeMethods.GetForegroundWindow())) ShellDesktop.ToggleInBackground();
        _peekMinimized = false;
        RestoreMainWindowAfterPeek();
        DebugLog.Write("göz atma bitti");
    }

    private static MainWindow? OpenMainWindow() =>
        System.Windows.Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault(w => w.IsVisible);

    private static void MinimizeMainWindowForPeek()
    {
        _peekMainWindowState = null;
        if (OpenMainWindow() is not { } window || window.WindowState == System.Windows.WindowState.Minimized) return;
        _peekMainWindowState = window.WindowState;
        window.WindowState = System.Windows.WindowState.Minimized;
    }

    /// <summary>Göz atma bitince ana pencere (göz atma onu küçülttüyse ve hâlâ küçükse) önceki hâline döner.</summary>
    private static void RestoreMainWindowAfterPeek()
    {
        if (_peekMainWindowState is not { } state) return;
        _peekMainWindowState = null;
        if (OpenMainWindow() is { WindowState: System.Windows.WindowState.Minimized } window) window.WindowState = state;
    }

    // --- Yeni widget ---

    /// <summary>
    /// Kısayolla ya da "Widget ekle" penceresiyle eklenen widget'ın nereye geldiğini ilk üç seferde söyler (ana pencere
    /// açık değilken görülmesi zor): ayar nerede, widget'lar nasıl öne getirilir.
    /// </summary>
    public static void ShowNewWidgetHint(WidgetConfig config)
    {
        if (Settings.NewWidgetHintsShown >= 3) return;
        Settings.NewWidgetHintsShown++;
        SaveSettings();
        // Yeni türlerde (takvim, zamanlayıcı…) widget'ın adı; klasik türlerde kısa ad.
        var name = WidgetVariants.Of(config) is not null ? WidgetText.DisplayName(config) : config.Kind switch
        {
            WidgetKind.Note => config.NoteChecklist ? L.T("Yapılacaklar listesi") : L.T("Yeni not"),
            WidgetKind.Clock => L.T("Saat"),
            WidgetKind.Date => L.T("Tarih"),
            WidgetKind.Launcher => L.T("Kısayol kutusu"),
            _ => L.T("Bölme"),
        };
        var where = PlaceModes.Parse(Settings.NewWidgetPlacement) switch
        {
            PlaceMode.Center => L.T("ekranın ortasına"),
            PlaceMode.Corner => WidgetVariants.Corner(config) switch
            {
                WidgetCorner.TopRight => L.T("ekranın sağ üstüne"),
                WidgetCorner.BottomRight => L.T("ekranın sağ altına"),
                _ => L.T("ekranın üst ortasına"),
            },
            _ => L.T("imlecin yanına"),
        };
        var reveal = Settings.Hotkeys.PeekWidgets;
        Tray?.Notify(L.F("{0} {1} eklendi", name, where),
            L.T("Yeni widget'ların yerini Widget'lar sayfasından değiştirebilirsin.") +
            (string.IsNullOrWhiteSpace(reveal) ? "" : L.F(" Widget'ları pencerelerin önüne getirmek için {0}.", reveal)),
            () => (System.Windows.Application.Current as App)?.ShowPage(typeof(Views.WidgetsPage)));
    }

    // --- Yeniden adlandırma ve simgeler ---

    /// <summary>
    /// Uygulama bir dosyayı ya da klasörü yeniden adlandırdı (eski yol, yeni yol, klasör mü). Ayarlar, taşıma geçmişi ve kutu
    /// kayıtları güncellendikten sonra, UI iş parçacığında tetiklenir (görünümler yeni yolu gösterebilsin).
    /// </summary>
    public static event Action<string, string, bool>? PathRenamed;

    /// <summary>
    /// Uygulamanın kendi yaptığı yeniden adlandırmayı (bölmede F2) her yere işler. Taşıma geçmişi ve kutu kayıtları hemen,
    /// çağıranın iş parçacığında güncellenir (izleyici yeni adı görmeden: geri alınmış dosya yeniden taşınmasın); widget
    /// ayarları (bölmelerin gizlenenleri, kutu öğeleri, öğe adları/simgeleri, klasörü yeniden adlandırılan bölme) UI iş
    /// parçacığında. Gezgin'de yapılan yeniden adlandırmalar izlenmez (bilinen sınır). Herhangi bir iş parçacığından çağrılır.
    /// </summary>
    public static void NotePathRenamed(string oldPath, string newPath, bool isDirectory)
    {
        try
        {
            Journal?.NoteRename(oldPath, newPath, isDirectory);
            BoxMoves?.NoteRename(oldPath, newPath, isDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DebugLog.Write($"yeniden adlandırma kaydedilemedi: {ex.Message}");
        }
        if (!OnUiThread(() => ApplyRenameToWidgets(oldPath, newPath, isDirectory))) return;
        ApplyRenameToWidgets(oldPath, newPath, isDirectory);
    }

    private static void ApplyRenameToWidgets(string oldPath, string newPath, bool isDirectory)
    {
        var changed = PathRenames.Apply(Settings.Widgets, oldPath, newPath, isDirectory, DesktopDirectory);
        // Simge verilen klasör (ya da üstü) yeniden adlandırıldı: "hepsini kaldır" onu yeni adıyla bulsun.
        if (FolderIconLog.Rename(Settings.IconFolders, oldPath, newPath, isDirectory) is var icons && !ReferenceEquals(icons, Settings.IconFolders))
        {
            Settings.IconFolders = icons;
            changed = true;
        }
        // Kutunun klasörü (NestDesk\<kutu>) yeniden adlandırıldıysa kutu yeni klasörü kullanır.
        if (isDirectory && BoxPlan.ParentOf(oldPath) is { } parent &&
            string.Equals(parent, Path.TrimEndingDirectorySeparator(BoxMover.Root), StringComparison.OrdinalIgnoreCase))
        {
            var oldName = Path.GetFileName(Path.TrimEndingDirectorySeparator(oldPath));
            foreach (var box in Settings.Widgets.Where(w => w.Kind == WidgetKind.Launcher &&
                                                            string.Equals(w.BoxFolder, oldName, StringComparison.OrdinalIgnoreCase)))
            {
                box.BoxFolder = Path.GetFileName(Path.TrimEndingDirectorySeparator(newPath));
                changed = true;
            }
        }
        if (changed)
        {
            SaveSettings();
            RefreshPinnedPaths();
        }
        PathRenamed?.Invoke(oldPath, newPath, isDirectory);
    }

    /// <summary>
    /// Klasöre simge verildi ya da kaldırıldı (herhangi bir iş parçacığından): "Klasör simgelerinin hepsini kaldır" onu
    /// masaüstünün dışında da bulsun diye kaydedilir. Önemsiz, sık olabilir (toplu simge verme): bir sonraki kayıtla yazılır.
    /// </summary>
    private static void OnFolderIconChanged(string folder, bool given)
    {
        if (!OnUiThread(() => OnFolderIconChanged(folder, given))) return;
        var next = FolderIconLog.Note(Settings.IconFolders, folder, given);
        if (ReferenceEquals(next, Settings.IconFolders)) return;
        Settings.IconFolders = next;
        SaveSettingsLater();
    }

    /// <summary>Kullanıcının seçtiği simge resimlerinin kopyaları (bkz. <see cref="IconFiles"/>).</summary>
    public static string IconsDirectory => Path.Combine(DataDirectory, IconFiles.FolderName);

    // --- Kutular ---

    /// <summary>
    /// Kısayol kutularındaki masaüstü dosyalarını taşıyıcıya bildirir: kurallar onları taşımaz (kutu "bulunamadı"
    /// göstermesin). Yeni küme atanır; izleyici arka planda okur. Kutu değişince çağrılır (ucuz, diske bakmaz).
    /// </summary>
    public static void RefreshPinnedPaths() =>
        Organizer.Pinned = BoxPlan.PinnedDesktopPaths(Settings.Widgets, DesktopDirectories);

    /// <summary>
    /// "Masaüstünü bölmeler yönetsin": açılınca eksik Klasörler/Kısayollar/Dosyalar bölmeleri eklenir (hiçbir öğe
    /// görünmez kalmasın) ve Windows'un masaüstü simgeleri gizlenir. Kapatınca simgeler geri gelir.
    /// </summary>
    public static void SetFencesManageDesktop(bool on)
    {
        if (on) Widgets.EnsureDesktopCoverage();
        Settings.FencesReplaceIcons = on;
        StopPeek();
        DesktopHidden = false;
        SaveSettings();
        ApplyDesktopState();
    }

    /// <summary>Mod açıkken bir türü gösteren son bölme kaldırılırsa o öğeler hiçbir yerde görünmez: mod kapatılır.</summary>
    public static bool EnsureNothingInvisible(bool notify = true)
    {
        if (!Settings.FencesReplaceIcons || Widgets.CoversDesktop()) return false;
        SetFencesManageDesktop(false);
        if (notify)
            Tray?.Notify(L.T("Masaüstü simgeleri yeniden gösteriliyor"),
                L.T("Bir bölme kaldırıldığı için bazı masaüstü öğeleri hiçbir bölmede görünmüyordu. İstersen Widget'lar sayfasından yeniden aç."),
                () => (System.Windows.Application.Current as App)?.ShowPage(typeof(Views.WidgetsPage)));
        return true;
    }

    /// <summary>
    /// Çift tıklama algılayıcısı yalnızca bir işe yarayacaksa çalışır. Test örneği (--desktop) kullanıcının gerçek
    /// masaüstündeki çift tıklamalara tepki vermez (NESTDESK_TEST_DOUBLECLICK=1 ya da eski DUZENLEME_ adıyla açılır).
    /// </summary>
    public static void ApplyDoubleClickSetting()
    {
        var wanted = DesktopState.DoubleClickChoice(Settings.DoubleClickAction, Settings.DoubleClickHidesDesktop) != DesktopState.DoubleClickNone
                     && (!IsTestDesktop || AppEnvironment.Get("TEST_DOUBLECLICK") == "1");
        if (wanted) DoubleClick?.Enable();
        else DoubleClick?.Disable();
    }

    /// <summary>
    /// "Boş masaüstüne çift tıklayınca" seçimi (DesktopState.DoubleClick*). Eski anahtar da yazılır: 2.0'a dönülürse
    /// "hiçbir şey" kapalı, diğerleri açık kalsın.
    /// </summary>
    public static void SetDoubleClickAction(string choice)
    {
        Settings.DoubleClickAction = DesktopState.DoubleClickChoice(choice, Settings.DoubleClickHidesDesktop);
        Settings.DoubleClickHidesDesktop = Settings.DoubleClickAction != DesktopState.DoubleClickNone;
        SaveSettings();
        ApplyDoubleClickSetting();
    }

    /// <summary>
    /// Masaüstü nöbetçisi (3 sn): Explorer yeniden başlarsa simgeler kendiliğinden yeniden görünür. Bölmeler yönetirken
    /// yeniden gizlenir; masaüstü yalnızca gizlendiyse (kullanıcı ya da Explorer simgeleri açmış) uygulama da "gösteriliyor"a
    /// döner. Göz atarken simgeler zaten görünür olmalıdır. Test örneği gerçek simgelere bakmaz.
    /// </summary>
    public static void ReconcileDesktopState()
    {
        if (IsTestDesktop || DesktopIcons.ChangePending) return;
        if (!CurrentView.IconsHidden || !DesktopIcons.AreVisible) return;
        if (Settings.FencesReplaceIcons)
        {
            // Bayrak yeniden kurulur: iptal edilen bir oturum kapanışı (RestoreDesktopOnExit) onu düşürmüş olabilir.
            ArmIconsHiddenFlag();
            DesktopIcons.SetVisible(false);
        }
        else if (DesktopHidden)
        {
            // Gezgin listeyi kendisi yeniden gösterdi (F5, ekran çözünürlüğü, masaüstündeki bir değişiklik; OneDrive'daki
            // masaüstünde sık olur). Kullanıcı gizlemişti: yeniden gizlenir. Kısa sürede üst üste olursa (başka bir program
            // hep açıyor) vazgeçilir ve "gösteriliyor"a dönülür, yoksa simgeler her birkaç saniyede bir yanıp söner.
            var now = Environment.TickCount64;
            _rehideCount = now - _lastRehide < 15000 ? _rehideCount + 1 : 1;
            _lastRehide = now;
            DebugLog.Write($"simgeler Gezgin tarafından yeniden gösterildi ({_rehideCount}): {DesktopIcons.Describe()}");
            if (_rehideCount <= 3)
            {
                DesktopIcons.SetVisible(false);
                return;
            }
            DesktopHidden = false;
            ApplyDesktopState();
        }
    }

    private static long _lastRehide;
    private static int _rehideCount;

    /// <summary>
    /// Günde bir kez ayarların (widget düzeni, notlar, kurallar) kopyası "yedekler" klasörüne alınır; son 7 gün tutulur.
    /// Bir şey ters giderse (bozuk dosya, yanlışlıkla silinen not) oradan geri dönülebilir. Arka planda çalışır.
    /// </summary>
    private static void BackupSettingsDaily()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var dir = Path.Combine(DataDirectory, "yedekler");
            var today = Path.Combine(dir, $"settings-{DateTime.Now:yyyy-MM-dd}.json");
            if (File.Exists(today)) return;
            Directory.CreateDirectory(dir);
            File.Copy(SettingsPath, today);
            foreach (var old in new DirectoryInfo(dir).GetFiles("settings-*.json").OrderByDescending(f => f.Name).Skip(7))
                old.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// --restore-desktop (kaldırma programı çağırır; tek örnek kilidi gerekmez, çalışan örneğe dokunmaz): ayarlar uygulamanın
    /// Windows masaüstü simgelerini gizli bıraktığını söylüyorsa simgeleri geri açar. Hiçbir dosya yazılmaz ya da taşınmaz;
    /// test masaüstüyle (--desktop) yalnızca günlüğe yazar.
    /// </summary>
    public static void RestoreDesktopForUninstall(string? desktopOverride, string? dataOverride)
    {
        var testDesktop = desktopOverride is not null;
        dataOverride = DataOverrideFor(desktopOverride, dataOverride);
        var portableFile = Path.Combine(AppContext.BaseDirectory, "portable.txt");
        var portable = dataOverride is null && !PackageInfo.IsPackaged && File.Exists(portableFile)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : null;
        foreach (var file in DataFolderLocator.SettingsCandidates(dataOverride, portable, RoamingAppData(testDesktop)))
        {
            string text;
            try
            {
                if (!File.Exists(file)) continue;
                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            // Yalnızca kullanılan (öncelikli) ayar dosyasına bakılır: eski klasörde kalmış eski bir kopya karar vermesin.
            var hidden = LegacyFiles.IconsLeftHidden(text);
            DebugLog.Write($"--restore-desktop: {file} → simgeler {(hidden ? "gizli bırakılmış" : "açık")}");
            if (hidden && !testDesktop) DesktopIcons.SetVisible(true);
            return;
        }
    }

    /// <summary>Uygulama kapanırken masaüstünü kullanıcıya gizli bırakma.</summary>
    public static void RestoreDesktopOnExit()
    {
        // Mod ayarı kalır (sonraki açılışta yeniden gizlenir); uygulama kapalıyken simgeler görünür olmalı. Göz atarken
        // simgeler zaten görünür (küçültülen pencereler kullanıcıya kalır: masaüstü önde, bir şey kaybolmaz).
        if (!CurrentView.IconsHidden) return;
        if (!IsTestDesktop) DesktopIcons.SetVisible(true);
        Settings.IconsHiddenByApp = false;
        SaveSettingsNow(TimeSpan.FromSeconds(3), changed: true);
    }

    // ------------------------------------------------------------------------------------------------------------------
    // 2.2 tanılama: hata günlüğü, oturum nöbetçisi, çökme raporları ve okunamayan dosya bildirimi.

    /// <summary>Hata günlüğü, çökme raporları, oturum nöbetçisi ve tanılama sayfasının klasörü (<c>&lt;veri&gt;\logs</c>).</summary>
    public static string LogsDirectory => Path.Combine(DataDirectory, LogFiles.FolderName);

    public static string CrashesDirectory => Path.Combine(LogsDirectory, CrashReports.FolderName);

    /// <summary>Açılışın süresi (süreç başından StartServices sonuna, ms); tanılama sayfasının "Başarım" bölümü için.</summary>
    public static long? StartupMs { get; set; }

    /// <summary>
    /// Bir dosyanın Gezgin'de ya da tarayıcıda açılacak yeri (yol başına; bkz. <see cref="PackagedApp.PathToShow"/>): Store
    /// sürümünde %AppData% altına yeni yazılanlar pakete özel LocalCache'tedir. Diske bakar: arka planda çağır.
    /// </summary>
    public static string PathOnDisk(string path)
    {
        if (PackageInfo.FamilyName is not { } family) return path;
        var redirected = PackagedApp.RedirectedRoamingPath(path, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), family);
        return PackagedApp.PathToShow(path, redirected, p => File.Exists(p) || Directory.Exists(p));
    }

    private static readonly object _sessionGate = new();
    // Nöbetçi (logs\session.json) yalnızca asıl örneğindir: Initialize'a yalnızca o gelir; --peek/--add gibi ikincil
    // örnekler ona hiç dokunmaz (onların veri klasörü boştur).
    private static bool _sessionOwned;
    private static SessionState? _session;
    private static SessionState? _previousSession;
    private static Task _sessionStart = Task.CompletedTask;

    /// <summary>Bu oturumun nöbetçisini yazar; öncekini (varsa) okuyup saklar. Arka planda.</summary>
    private static void StartSession()
    {
        var logs = LogsDirectory;
        lock (_sessionGate)
        {
            _sessionOwned = true;
            _session = new SessionState(AppLog.Channel.Session, AppInfo.Version, DateTime.UtcNow, Environment.ProcessId, Hang: false);
        }
        _sessionStart = BackgroundIo.Run($"{AppInfo.Name} oturum", () => // l10n: çevrilmez (iş parçacığı adı)
        {
            _previousSession = SessionMarker.Read(logs);
            WriteSessionMarker();
        }, onError: ex => AppLog.Warn("session", "marker-failed", ex));
    }

    private static void WriteSessionMarker()
    {
        lock (_sessionGate)
        {
            if (!_sessionOwned || _session is null) return;
            try
            {
                Directory.CreateDirectory(LogsDirectory);
                SessionMarker.Write(LogsDirectory, _session);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppLog.Count("session.writefailed"); }
        }
    }

    /// <summary>Arayüz uzun süre yanıt vermedi (true) ya da toparlandı (false): nöbetçiye işlenir (UiWatchdog, arka planda).</summary>
    public static void SetSessionHang(bool hung)
    {
        lock (_sessionGate)
        {
            if (!_sessionOwned || _session is null || (_session.Hang == hung && !_session.Ending)) return;
            _session = _session with { Hang = hung, Ending = false };
        }
        WriteSessionMarker();
    }

    /// <summary>Windows oturumu kapanıyor: nöbetçi silinmez (kapanış iptal edilebilir), "kapanıyor" işaretlenir.</summary>
    public static void MarkSessionEnding()
    {
        lock (_sessionGate)
        {
            if (!_sessionOwned || _session is null || _session.Ending) return;
            _session = _session with { Ending = true };
        }
        WriteSessionMarker();
    }

    /// <summary>Temiz çıkış (OnExit): nöbetçi silinir. Yalnızca asıl örnekte; tek örnek kilidi bırakılmadan önce çağrılır.</summary>
    public static void EndSession()
    {
        if (!_sessionStart.IsCompleted)
        {
            try { _sessionStart.Wait(TimeSpan.FromSeconds(1)); }
            catch (AggregateException) { }
        }
        lock (_sessionGate)
        {
            if (!_sessionOwned) return;
            _sessionOwned = false;
            SessionMarker.Delete(LogsDirectory);
        }
    }

    /// <summary>
    /// Açılıştan birkaç saniye sonra, arka planda: günlük ve rapor bakımı, önceki oturumun nasıl bittiği (yalnızca donup
    /// düzgün kapanmadıysa "hang" raporu; sürüm değiştiyse ya da donma yoksa yalnızca Info) ve sorulacak raporlar.
    /// </summary>
    private static void CheckPreviousSession(string logs, string? mode, bool logOff)
    {
        var now = DateTime.UtcNow;
        var crashes = Path.Combine(logs, CrashReports.FolderName);
        AppLog.Channel.PruneNow();
        CrashReports.Prune(crashes, now);
        try { _sessionStart.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { }
        var previous = _previousSession;
        switch (SessionMarker.Assess(previous, AppInfo.Version))
        {
            case PreviousSession.Unclean:
                AppLog.Info("session", "previous-session-unclean");
                break;
            case PreviousSession.Hang:
                AppLog.Warn("session", "previous-session-hang");
                try { CrashReports.WriteHang(logs, mode, logOff, previous!, now); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppLog.Warn("session", "hang-report-failed", ex); }
                break;
        }
        // Seçim sonradan değiştiyse bekleyenler ona uyar: "Kaydetme" siler, "Sormadan kaydet" kaydeder; soru yalnızca "Sor"da.
        switch (CrashReportModes.Normalize(mode))
        {
            case CrashReportModes.Never:
                foreach (var report in CrashReports.List(crashes, pending: true)) CrashReports.Discard(report.Path);
                return;
            case CrashReportModes.Always:
                CrashReports.SaveAll(crashes, now);
                return;
        }
        var toAsk = CrashReports.ToAsk(crashes);
        if (toAsk.Count > 0)
            _dispatcher?.BeginInvoke(() => Views.Diagnostics.AskAboutCrashes(toAsk), DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Bir veri dosyası okunamadı (Initialize içinde, JsonFile.Load): hata günlüğüne yazılır ve arayüz hazır olunca doğru metinle
    /// söylenir (.bak yüklendiyse "son sağlam kopyası", yüklenmediyse varsayılanlar). Okunamayan dosyanın kopyası silinmez.
    /// </summary>
    private static void OnFileCorrupted(string kind, string? backup, bool usedBak)
    {
        // Alan ve ileti metin sabitidir (AppLogTests taraması): dosya türü ve sonuç dallarla seçilir.
        switch (kind, usedBak)
        {
            case (JsonFile.KindSettings, true): AppLog.Warn("settings", "corrupt-file-backup-loaded"); break;
            case (JsonFile.KindSettings, false): AppLog.Warn("settings", "corrupt-file-defaults-loaded"); break;
            case (JsonFile.KindJournal, true): AppLog.Warn("journal", "corrupt-file-backup-loaded"); break;
            case (JsonFile.KindJournal, false): AppLog.Warn("journal", "corrupt-file-defaults-loaded"); break;
            case (JsonFile.KindBoxMoves, true): AppLog.Warn("box-moves", "corrupt-file-backup-loaded"); break;
            case (JsonFile.KindBoxMoves, false): AppLog.Warn("box-moves", "corrupt-file-defaults-loaded"); break;
            case (_, true): AppLog.Warn("data", "corrupt-file-backup-loaded"); break;
            default: AppLog.Warn("data", "corrupt-file-defaults-loaded"); break;
        }
        _dispatcher?.BeginInvoke(() => Views.Diagnostics.ShowCorruptFile(kind, backup, usedBak), DispatcherPriority.ApplicationIdle);
    }

    /// <summary>DispatcherTimer tabanlı tek atımlık zamanlayıcı (kayıt zamanlaması; Normal öncelik: yoğun girişte de gecikmez).</summary>
    private sealed class DispatcherOwnerTimer : IOwnerTimer
    {
        private readonly DispatcherTimer _timer;

        public DispatcherOwnerTimer(Dispatcher dispatcher, Action tick)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
            _timer.Tick += (_, _) =>
            {
                _timer.Stop();
                var sw = PerfLog.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
                tick();
                if (sw is not null) PerfLog.Write($"ayarlar yazmaya verildi: anlık görüntü {sw.Elapsed.TotalMilliseconds:0.00} ms"); // l10n: çevrilmez
            };
        }

        public void Schedule(TimeSpan due)
        {
            if (!_timer.Dispatcher.CheckAccess())
            {
                _timer.Dispatcher.BeginInvoke(() => Schedule(due));
                return;
            }
            _timer.Stop();
            _timer.Interval = due;
            _timer.Start();
        }

        public void Cancel()
        {
            // Çökme yolunda başka iş parçacığından gelebilir: orada durdurulamaz, sonradan çalışması da zararsız.
            if (_timer.Dispatcher.CheckAccess()) _timer.Stop();
        }
    }
}
