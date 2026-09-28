using System.IO;
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

    /// <summary>Ayarlar kaydedildiğinde (UI iş parçacığında) tetiklenir.</summary>
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

        Settings = JsonFile.Load(SettingsPath, () => new AppSettings());
        BackupSettingsDaily();
        Journal = new MoveJournal(Path.Combine(DataDirectory, "journal.json"));
        Organizer = new DesktopOrganizer(DesktopDirectory, () => Settings, Journal);
        Watcher = new DesktopWatcher(Organizer, () => Settings.Paused);
        BoxMoves = new BoxMoveLog(Path.Combine(DataDirectory, "box-moves.json"));
        Widgets = new WidgetManager();
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

    public static void SaveSettings()
    {
        JsonFile.Save(SettingsPath, Settings);
        SettingsChanged?.Invoke();
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
    /// <see cref="DesktopVisibilityChanged"/>.
    /// </summary>
    public static void ApplyDesktopState()
    {
        var view = CurrentView;
        // Test klasörüyle (--desktop) çalışan örnek kullanıcının gerçek masaüstü simgelerine dokunmaz.
        if (IsTestDesktop) DebugLog.Write($"simgeler {(view.IconsHidden ? "gizlenecekti" : "gösterilecekti")} (test masaüstü)");
        else DesktopIcons.SetVisible(!view.IconsHidden);
        Widgets.SetHidden(view.WidgetsHidden);
        // Hemen diske yazılır: uygulama zorla kapatılırsa kurulum/kaldırma ve sonraki açılış simgeleri geri açabilsin.
        if (Settings.IconsHiddenByApp != view.IconsHidden)
        {
            Settings.IconsHiddenByApp = view.IconsHidden;
            SaveSettings();
        }
        DesktopVisibilityChanged?.Invoke();
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
                Tray?.Notify("Windows masaüstü açıldı",
                    $"Geri dönmek için yeniden çift tıkla ya da üstteki \"{AppInfo.Name}'e dön\"e bas. Çift tıklamanın ne yapacağını Ayarlar > Masaüstü'nden seçebilirsin.",
                    () => EndPeek());
                break;
            case DoubleClickEffect.ToggleDesktop:
                ToggleDesktop();
                if (!DesktopHidden || Settings.DoubleClickHintsShown >= 3) return;
                Settings.DoubleClickHintsShown++;
                SaveSettings();
                Tray?.Notify(Widgets.Hidden ? "Widget'lar ve simgeler gizlendi" : "Masaüstü simgeleri gizlendi",
                    "Masaüstüne yeniden çift tıkla ya da buraya tıkla, geri gelsin. Bu özellik Ayarlar'dan kapatılabilir.",
                    () => SetDesktopHidden(false));
                break;
        }
    }

    // --- Windows masaüstüne göz at ---

    /// <summary>Göz atmayı neyin başlattığı: çift tıklamada pencereler küçültülmez (masaüstü zaten öndedir).</summary>
    public enum PeekOrigin { Hotkey, Tray, Menu, Command, DoubleClick, Settings }

    /// <summary>Göz atma açık pencereleri küçülterek başladıysa (Win+D gibi); bitince yeniden açılır.</summary>
    private static bool _peekMinimized;

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
        Peeking = true;
        DesktopHidden = false;
        // "Açık pencereleri küçült": masaüstü zaten öndeyse (çift tıklama) yapılmaz. Test örneği gerçek pencereleri küçültmez.
        _peekMinimized = Settings.PeekShowsDesktop && origin != PeekOrigin.DoubleClick && !IsTestDesktop &&
                         !DesktopIcons.IsDesktopSurface(NativeMethods.GetForegroundWindow());
        if (_peekMinimized) ShellDesktop.ToggleInBackground();
        ApplyDesktopState();
        Views.PeekBar.Open(DesktopState.NormalizePeekMinutes(Settings.PeekMinutes));
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
        DebugLog.Write("göz atma bitti");
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
        var name = config.Kind switch
        {
            WidgetKind.Note => config.NoteChecklist ? "Yapılacaklar listesi" : "Yeni not",
            WidgetKind.Clock => "Saat",
            WidgetKind.Date => "Tarih",
            WidgetKind.Launcher => "Kısayol kutusu",
            _ => "Bölme",
        };
        var where = PlaceModes.Parse(Settings.NewWidgetPlacement) switch
        {
            PlaceMode.Center => "ekranın ortasına",
            PlaceMode.Corner => config.Kind is WidgetKind.Clock or WidgetKind.Date or WidgetKind.Note ? "ekranın sağ üstüne" : "ekranın üst ortasına",
            _ => "imlecin yanına",
        };
        var reveal = Settings.Hotkeys.PeekWidgets;
        Tray?.Notify($"{name} {where} eklendi",
            "Yerini Widget'lar sayfasındaki \"Yeni widget'ların yeri\"nden değiştirebilirsin." +
            (string.IsNullOrWhiteSpace(reveal) ? "" : $" Widget'ları pencerelerin önüne getirmek için {reveal}."),
            () => (System.Windows.Application.Current as App)?.ShowPage(typeof(Views.WidgetsPage)));
    }

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
            Tray?.Notify("Masaüstü simgeleri yeniden gösteriliyor",
                "Bir bölme kaldırıldığı için bazı masaüstü öğeleri hiçbir bölmede görünmüyordu. İstersen Widget'lar sayfasından yeniden aç.",
                () => (System.Windows.Application.Current as App)?.ShowPage(typeof(Views.WidgetsPage)));
        return true;
    }

    /// <summary>
    /// Çift tıklama algılayıcısı yalnızca bir işe yarayacaksa çalışır. Test örneği (--desktop) kullanıcının gerçek
    /// masaüstündeki çift tıklamalara tepki vermez (DUZENLEME_TEST_DOUBLECLICK=1 ile açılır).
    /// </summary>
    public static void ApplyDoubleClickSetting()
    {
        var wanted = DesktopState.DoubleClickChoice(Settings.DoubleClickAction, Settings.DoubleClickHidesDesktop) != DesktopState.DoubleClickNone
                     && (!IsTestDesktop || Environment.GetEnvironmentVariable("DUZENLEME_TEST_DOUBLECLICK") == "1");
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
        if (Settings.FencesReplaceIcons) DesktopIcons.SetVisible(false);
        else if (DesktopHidden)
        {
            DesktopHidden = false;
            ApplyDesktopState();
        }
    }

    /// <summary>
    /// Günde bir kez ayarların (widget düzeni, notlar, kurallar) kopyası "yedekler" klasörüne alınır; son 7 gün tutulur.
    /// Bir şey ters giderse (bozuk dosya, yanlışlıkla silinen not) oradan geri dönülebilir.
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
        // Mod ayarı kalır (sonraki açılışta yeniden gizlenir); uygulama kapalıyken simgeler görünür olmalı. Göz atarken
        // simgeler zaten görünür (küçültülen pencereler kullanıcıya kalır: masaüstü önde, bir şey kaybolmaz).
        if (!CurrentView.IconsHidden) return;
        if (!IsTestDesktop) DesktopIcons.SetVisible(true);
        Settings.IconsHiddenByApp = false;
        try { JsonFile.Save(SettingsPath, Settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
