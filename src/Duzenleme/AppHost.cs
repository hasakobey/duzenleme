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

    /// <summary>Masaüstü simgeleri gizlenip gösterildiğinde tetiklenir.</summary>
    public static event Action? DesktopVisibilityChanged;

    public static bool DesktopHidden { get; private set; }

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

    public static void Initialize(string? desktopOverride, string? dataOverride)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        // Store (MSIX) paketinin klasörü salt okunurdur ve portable.txt taşımaz: paketliyken taşınabilir mod denenmez.
        var portable = dataOverride is null && !PackageInfo.IsPackaged ? PortableDataDirectory() : null;
        IsPortable = portable is not null;
        DataDirectory = dataOverride ?? portable ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppInfo.DataFolderName);
        DesktopDirectory = desktopOverride ?? ResolveDesktop();
        IsTestDesktop = desktopOverride is not null;
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

        Settings = JsonFile.Load(SettingsPath, () => new AppSettings());
        _store = new SettingsStore(new DurableFile(SettingsPath), () => JsonFile.Serialize(Settings),
            tick => new DispatcherOwnerTimer(dispatcher, tick));
        _store.File.Failed += OnSettingsWriteFailed;
        _store.File.Recovered += () => DebugLog.Write("ayarlar yeniden yazılabiliyor");
        Journal = new MoveJournal(Path.Combine(DataDirectory, "journal.json"));
        Journal.WriteFailed += ex => DebugLog.Write($"geçmiş yazılamadı: {ex.Message}");
        Organizer = new DesktopOrganizer(DesktopDirectory, () => Settings, Journal);
        Watcher = new DesktopWatcher(Organizer, () => Settings.Paused);
        Widgets = new WidgetManager();
        BackgroundIo.Run($"{AppInfo.Name} ayar yedeği", BackupSettingsDaily);

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
    public static bool SaveSettingsNow(TimeSpan? timeout = null)
    {
        if (_store is null) return false;
        var sw = PerfLog.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
        var ok = _store.FlushNow(timeout ?? TimeSpan.FromSeconds(2));
        if (sw is not null) PerfLog.Write($"SaveSettingsNow {sw.Elapsed.TotalMilliseconds:0.0} ms ok={ok}");
        return ok;
    }

    /// <summary>Çıkış ve oturum kapanışı: ayarları ve taşıma geçmişini diske indirir.</summary>
    public static void FlushAll()
    {
        SaveSettingsNow(TimeSpan.FromSeconds(3));
        if (PerfLog.Enabled)
            PerfLog.Write($"sayaçlar: {PerfLog.Summary()} ayar yazma={_store?.File.WriteCount} geçmiş yazma={Journal?.WriteCount}");
        try { Journal?.Flush(TimeSpan.FromSeconds(2)); }
        catch (Exception ex) { DebugLog.Write($"geçmiş yazılamadı: {ex.Message}"); }
    }

    /// <summary>Arayüz iş parçacığında değilse işi oraya aktarır ve false döner.</summary>
    private static bool OnUiThread(Action action)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess()) return true;
        _dispatcher.BeginInvoke(action);
        return false;
    }

    private static int _writeWarnings;

    private static void OnSettingsWriteFailed(Exception ex)
    {
        DebugLog.Write($"ayarlar yazılamadı: {ex}");
        // Kullanıcıya bir oturumda en fazla iki kez söylenir; değişiklikler bellekte durur, yazma arka planda yeniden denenir.
        if (Interlocked.Increment(ref _writeWarnings) > 2) return;
        _dispatcher?.BeginInvoke(() => Views.Notice.Show(
            "Ayarlar şu an kaydedilemedi (dosyayı başka bir program kullanıyor olabilir). Değişikliklerin duruyor; kayıt birazdan yeniden denenecek.",
            Views.NoticeKind.Warning));
    }

    /// <summary>
    /// Çökme yolu (herhangi bir iş parçacığından): simgeleri bizim gizlediğimiz hâlde bırakma ve ayarları diske indir.
    /// Süreç sonlanmıyorsa (gözlenmemiş görev hatası) simgelere dokunulmaz.
    /// </summary>
    public static void OnCrash(bool terminating)
    {
        try
        {
            if (terminating && IconsShouldBeHidden && !IsTestDesktop)
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
        BackgroundIo.Run($"{AppInfo.Name} açılış taraması", () =>
        {
            if (!Settings.Paused)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var moved = Organizer.OrganizeAll();
                PerfLog.Write($"açılış taraması {sw.ElapsedMilliseconds} ms, {moved.Count} dosya taşındı");
            }
            // Eski sürümün mutlak yollu klasör simgelerini onar.
            Icons.FolderIconService.RepairDesktopFolders(desktop);
        }, autostart ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(3),
            ex => DebugLog.Write($"açılış taraması: {ex}"));
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

    /// <summary>Masaüstü simgelerini (ve ayara göre widget'ları) gizler ya da gösterir.</summary>
    public static void ToggleDesktop() => SetDesktopHidden(!DesktopHidden);

    /// <summary>
    /// Boş masaüstüne çift tıklama. İlk birkaç seferde nasıl geri getirileceği söylenir: bilmeden çift tıklayan
    /// kullanıcı widget'larının kaybolduğunu sanmasın.
    /// </summary>
    public static void ToggleDesktopByDoubleClick()
    {
        ToggleDesktop();
        if (!DesktopHidden || Settings.DoubleClickHintsShown >= 3) return;
        Settings.DoubleClickHintsShown++;
        SaveSettings();
        Tray?.Notify(Widgets.Hidden ? "Widget'lar ve simgeler gizlendi" : "Masaüstü simgeleri gizlendi",
            "Masaüstüne yeniden çift tıkla ya da buraya tıkla, geri gelsin. Bu özellik Ayarlar'dan kapatılabilir.",
            () => SetDesktopHidden(false));
    }

    public static void SetDesktopHidden(bool hidden)
    {
        DesktopHidden = hidden;
        ApplyIconVisibility();
        // Bölmeler masaüstünü yönetirken Windows simgeleri zaten gizli: "gizle" bölmeleri (tüm widget'ları) gizler.
        Widgets.SetHidden(hidden && (Settings.HideWidgetsWithIcons || Settings.FencesReplaceIcons));
        SaveSettings();
        DesktopVisibilityChanged?.Invoke();
    }

    /// <summary>Windows'un masaüstü simgeleri şu an bizim tarafımızdan gizli olmalı mı?</summary>
    private static bool IconsShouldBeHidden => DesktopHidden || Settings.FencesReplaceIcons;

    /// <summary>
    /// Simge görünürlüğünü duruma uygular. <see cref="AppSettings.IconsHiddenByApp"/> eşzamanlı yazılır ve gizlerken
    /// simgelerden ÖNCE: uygulama o an zorla kapatılsa da sonraki açılış ve kaldırma programı simgeleri geri açabilsin.
    /// </summary>
    public static void ApplyIconVisibility()
    {
        var hide = IconsShouldBeHidden;
        if (hide && !Settings.IconsHiddenByApp)
        {
            Settings.IconsHiddenByApp = true;
            SaveSettingsNow();
        }
        // Test klasörüyle (--desktop) çalışan örnek kullanıcının gerçek masaüstü simgelerine dokunmaz.
        if (IsTestDesktop) DebugLog.Write($"simgeler {(hide ? "gizlenecekti" : "gösterilecekti")} (test masaüstü)");
        else DesktopIcons.SetVisible(!hide);
        if (!hide && Settings.IconsHiddenByApp)
        {
            // Gösterirken bayrak sonra düşer: arada kapanırsa sonraki açılış simgeleri (zaten görünür) bir kez daha açar.
            Settings.IconsHiddenByApp = false;
            SaveSettingsNow();
        }
    }

    /// <summary>
    /// "Masaüstünü bölmeler yönetsin": açılınca eksik Klasörler/Kısayollar/Dosyalar bölmeleri eklenir (hiçbir öğe
    /// görünmez kalmasın) ve Windows'un masaüstü simgeleri gizlenir. Kapatınca simgeler geri gelir.
    /// </summary>
    public static void SetFencesManageDesktop(bool on)
    {
        if (on) Widgets.EnsureDesktopCoverage();
        Settings.FencesReplaceIcons = on;
        if (DesktopHidden)
        {
            DesktopHidden = false;
            Widgets.SetHidden(false);
        }
        ApplyIconVisibility();
        SaveSettings();
        DesktopVisibilityChanged?.Invoke();
    }

    /// <summary>Mod açıkken bir türü gösteren son bölme kaldırılırsa o öğeler hiçbir yerde görünmez: mod kapatılır.</summary>
    public static bool EnsureNothingInvisible(bool notify = true)
    {
        if (!Settings.FencesReplaceIcons || Widgets.CoversDesktop()) return false;
        SetFencesManageDesktop(false);
        if (notify)
            Tray?.Notify("Masaüstü simgeleri yeniden gösteriliyor",
                "Bir bölme kaldırıldığı için bazı masaüstü öğeleri hiçbir bölmede görünmüyordu. İstersen Widget'lar sayfasından yeniden aç.",
                () => (System.Windows.Application.Current as App)?.ShowPage(typeof(Views.WidgetsPage)));
        return true;
    }

    public static void ApplyDoubleClickSetting()
    {
        if (Settings.DoubleClickHidesDesktop) DoubleClick?.Enable();
        else DoubleClick?.Disable();
    }

    /// <summary>
    /// Explorer yeniden başlarsa simgeler kendiliğinden yeniden görünür; uygulama hâlâ "gizli" sanmasın.
    /// </summary>
    public static void ReconcileDesktopState()
    {
        if (DesktopIcons.ChangePending) return;
        if (Settings.FencesReplaceIcons)
        {
            // Explorer yeniden başlayınca simgeler kendiliğinden geri gelir: bölmeler yönetirken yeniden gizle.
            if (!IsTestDesktop && DesktopIcons.AreVisible) DesktopIcons.SetVisible(false);
            return;
        }
        if (!DesktopHidden || !DesktopIcons.AreVisible) return;
        DesktopHidden = false;
        Widgets.SetHidden(false);
        Settings.IconsHiddenByApp = false;
        SaveSettings();
        DesktopVisibilityChanged?.Invoke();
    }

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

    /// <summary>Uygulama kapanırken masaüstünü kullanıcıya gizli bırakma.</summary>
    public static void RestoreDesktopOnExit()
    {
        // Mod ayarı kalır (sonraki açılışta yeniden gizlenir); uygulama kapalıyken simgeler görünür olmalı.
        if (!IconsShouldBeHidden) return;
        if (!IsTestDesktop) DesktopIcons.SetVisible(true);
        Settings.IconsHiddenByApp = false;
        _store?.MarkDirty();
        SaveSettingsNow(TimeSpan.FromSeconds(3));
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
                if (sw is not null) PerfLog.Write($"ayarlar yazmaya verildi: anlık görüntü {sw.Elapsed.TotalMilliseconds:0.00} ms");
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
