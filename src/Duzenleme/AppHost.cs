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

    /// <summary>Masaüstü simgeleri gizlenip gösterildiğinde tetiklenir.</summary>
    public static event Action? DesktopVisibilityChanged;

    public static bool DesktopHidden { get; private set; }

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

    public static void Initialize(string? desktopOverride, string? dataOverride)
    {
        IsTestDesktop = desktopOverride is not null;
        // Store (MSIX) paketinin klasörü salt okunurdur ve portable.txt taşımaz: paketliyken taşınabilir mod denenmez.
        var portable = dataOverride is null && !PackageInfo.IsPackaged ? PortableDataDirectory() : null;
        IsPortable = portable is not null;
        // 2.1: %AppData%\Duzenleme → %AppData%\NestDesk. Tek örnek kilidi burada zaten tutuluyor (App.OnStartup): klasöre
        // aynı anda yazan başka bir NestDesk/Düzenleme yok. Taşınamazsa bu oturum eski klasörle çalışır.
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

        Settings = JsonFile.Load(SettingsPath, () => new AppSettings());
        BackupSettingsDaily();
        Journal = new MoveJournal(Path.Combine(DataDirectory, "journal.json"));
        Organizer = new DesktopOrganizer(DesktopDirectory, () => Settings, Journal);
        Watcher = new DesktopWatcher(Organizer, () => Settings.Paused);
        Widgets = new WidgetManager();

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

    /// <summary>Simge görünürlüğünü duruma uygular (çökme sonrası geri açılabilsin diye ayara da yazılır).</summary>
    public static void ApplyIconVisibility()
    {
        // Test klasörüyle (--desktop) çalışan örnek kullanıcının gerçek masaüstü simgelerine dokunmaz.
        if (IsTestDesktop) DebugLog.Write($"simgeler {(IconsShouldBeHidden ? "gizlenecekti" : "gösterilecekti")} (test masaüstü)");
        else DesktopIcons.SetVisible(!IconsShouldBeHidden);
        // Hemen diske yazılır: uygulama zorla kapatılırsa kurulum/kaldırma ve sonraki açılış simgeleri geri açabilsin.
        if (Settings.IconsHiddenByApp == IconsShouldBeHidden) return;
        Settings.IconsHiddenByApp = IconsShouldBeHidden;
        SaveSettings();
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

    /// <summary>
    /// --restore-desktop (kaldırma programı çağırır; tek örnek kilidi gerekmez, çalışan örneğe dokunmaz): ayarlar uygulamanın
    /// Windows masaüstü simgelerini gizli bıraktığını söylüyorsa simgeleri geri açar. Hiçbir dosya yazılmaz ya da taşınmaz;
    /// test masaüstüyle (--desktop) yalnızca günlüğe yazar.
    /// </summary>
    public static void RestoreDesktopForUninstall(string? desktopOverride, string? dataOverride)
    {
        var testDesktop = desktopOverride is not null;
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
        // Mod ayarı kalır (sonraki açılışta yeniden gizlenir); uygulama kapalıyken simgeler görünür olmalı.
        if (!IconsShouldBeHidden) return;
        if (!IsTestDesktop) DesktopIcons.SetVisible(true);
        Settings.IconsHiddenByApp = false;
        try { JsonFile.Save(SettingsPath, Settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
