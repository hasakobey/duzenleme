using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Duzenleme;

public partial class App : Application
{
    public static readonly Color Brand = Color.FromRgb(0x8B, 0x5C, 0xF6);

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private EventWaitHandle? _exitSignal;
    private EventWaitHandle? _addSignal;
    private EventWaitHandle? _peekSignal;

    private MainWindow? _mainWindow;
    private NewFolderSuggestions? _newFolders;
    private UiWatchdog? _watchdog;
    private bool _exiting;
    // İşlenmeyen arayüz hatalarının şeridi: imza başına bir kez, oturumda en çok üç (bkz. ErrorNoticeLimiter).
    private readonly ErrorNoticeLimiter _uiErrors = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Koruma: App'i başka bir süreç (ör. test çalıştırıcısı) oluşturduysa WPF kurucusu OnStartup'ı kuyruğa koyar ve
        // hizmetler o sürecin argümanlarıyla, yani gerçek masaüstü ve gerçek ayarlar üzerinde başlardı.
        if (System.Reflection.Assembly.GetEntryAssembly() != typeof(App).Assembly)
        {
            Shutdown();
            return;
        }
        // App.xaml'ın kaynakları (WPF-UI sözlükleri, QuietStyles) InitializeComponent'te, OnStartup'tan önce yüklendi. Zaman
        // burada alınır, işaret tek örnek denetiminden sonra yazılır (yardımcı süreçler --exit/--peek günlüğü kirletmesin).
        var resourcesAt = PerfLog.Now();
        // Dil: önce Windows'unki (başlangıç hataları ve yeni kullanıcının varsayılan ayarları için); ayarlar okununca yeniden.
        L.Init(null);
        var args = ParseArgs(e.Args);
        _args = args;

        // Geliştirme: simge kütüphanesinin önizlemesini üret ve çık.
        if (e.Args.Length == 2 && e.Args[0] == "--export-icon-sheet")
        {
            Icons.IconSheet.Export(e.Args[1]);
            Shutdown();
            return;
        }
        // Geliştirme: SVG'yi (yapay zekâ çıktısıyla aynı yoldan) temizleyip çiz, PNG ve ICO yaz.
        if (e.Args.Length == 3 && e.Args[0] == "--render-svg")
        {
            var drawing = Icons.AiIconGenerator.ToDrawing(Icons.AiIconGenerator.Sanitize(System.IO.File.ReadAllText(e.Args[1])));
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(Icons.FolderIconRenderer.Render(drawing, 256)));
            using (var fs = System.IO.File.Create(e.Args[2])) png.Save(fs);
            System.IO.File.WriteAllBytes(System.IO.Path.ChangeExtension(e.Args[2], ".ico"), Icons.FolderIconRenderer.ToIco(drawing));
            Shutdown();
            return;
        }

        // Kaldırma programı için: uygulama zorla kapatılıp masaüstü simgelerini gizli bıraktıysa geri aç. Tek örnek kilidi
        // alınmaz ve çalışan örneğe sinyal gönderilmez.
        if (args.RestoreDesktop)
        {
            AppHost.RestoreDesktopForUninstall(args.Desktop, args.Data);
            Shutdown();
            return;
        }

        // Store (MSIX) sürümü: başlangıç görevi exe'yi argümansız açar; --minimized gibi tepside sessizce başlasın.
        if (e.Args.Length == 0 && PackageInfo.LaunchedByStartupTask()) args = args with { Minimized = true };

        // Tek örnek: ikinci açılış ilk örneğin penceresini öne getirir. Test örnekleri (--desktop) test masaüstü başına
        // ayrıdır: aynı anda birden çok test klasörü denenebilir, "--desktop <aynı> --exit" doğru örneği kapatır.
        var id = AppInfo.InstanceIdPrefix + Environment.UserName + (args.Desktop is null ? "" : ".test." + DataFolderLocator.TestDesktopKey(args.Desktop));

        // Kurulum/kaldırma programı için: çalışan örneği düzgünce kapat (masaüstü simgeleri geri açılır) ve kapanmasını bekle.
        if (args.Exit)
        {
            using (var exit = new EventWaitHandle(false, EventResetMode.AutoReset, id + ".exit")) exit.Set();
            try
            {
                using var running = Mutex.OpenExisting(id);
                try { running.WaitOne(TimeSpan.FromSeconds(15)); }
                catch (AbandonedMutexException) { }
            }
            catch (WaitHandleCannotBeOpenedException) { }
            Shutdown();
            return;
        }

        // Store sürümü: Başlat'taki "Widget ekle" girişi ayrı bir uygulama kimliğidir (PFN!AddWidget). Uygulama kapalıyken
        // oradan açıldıysa tepsi, bildirimler ve görev çubuğu oturum boyunca o kimlikle çalışmasın: ana uygulamayı (PFN!NestDesk)
        // "--add" ile başlat ve kapan. Çalışan bir örnek varsa aşağıdaki olağan yol isteği ona iletir; başlatılamazsa bu süreç
        // eskisi gibi devam eder.
        if (PackagedApp.MainAppUserModelIdFor(PackageInfo.ApplicationUserModelId) is { } main && !InstanceRunning(id)
            && PackageInfo.ActivateApplication(main, "--add"))
        {
            Shutdown();
            return;
        }

        // "Şimdi yeniden başlat" (dil): eski örnek kapanıp tek örnek kilidini bırakınca kilit doğrudan bu örneğe geçer.
        var takenOver = args.Restart ? TakeOverInstanceLock(id) : null;
        var isFirst = takenOver is not null;
        _instanceMutex = takenOver ?? new Mutex(true, id, out isFirst);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, id + ".show");
        _addSignal = new EventWaitHandle(false, EventResetMode.AutoReset, id + ".add");
        _peekSignal = new EventWaitHandle(false, EventResetMode.AutoReset, id + ".peek");
        if (!isFirst)
        {
            // --peek: çalışan örnekte Windows masaüstüne göz at / NestDesk'e dön (öne getirilecek pencere yok).
            if (args.Peek)
            {
                _peekSignal.Set();
                Shutdown();
                return;
            }
            // Store sürümünün arka plan açılışları (başlangıç görevi, güncelleme sonrası yeniden başlatma) zaten çalışan
            // örneğin penceresini öne getirmesin.
            if (!(PackageInfo.IsPackaged && args.Minimized))
            {
                // Çalışan örnek penceresini öne getirebilsin (yoksa Windows yalnızca görev çubuğunda yanıp söndürür).
                const int ASFW_ANY = -1;
                Widgets.NativeMethods.AllowSetForegroundWindow(ASFW_ANY);
                (args.Add ? _addSignal : _showSignal).Set();
            }
            Shutdown();
            return;
        }
        PerfLog.Mark("App.resources", atMs: resourcesAt);
        ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => Dispatcher.BeginInvoke(ShowMainWindow), null, -1, false);
        ThreadPool.RegisterWaitForSingleObject(_addSignal, (_, _) => Dispatcher.BeginInvoke(ShowQuickAdd), null, -1, false);
        ThreadPool.RegisterWaitForSingleObject(_peekSignal, (_, _) => Dispatcher.BeginInvoke(() => TogglePeek(AppHost.PeekOrigin.Command)), null, -1, false);
        _exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, id + ".exit");
        ThreadPool.RegisterWaitForSingleObject(_exitSignal, (_, _) => Dispatcher.BeginInvoke(ExitApp), null, -1, true);

        DispatcherUnhandledException += OnUnhandledException;
        // Başka iş parçacığındaki yakalanmamış hata süreci sonlandırır: simgeler gizli, ayarlar yazılmamış kalmasın.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            DebugLog.Write("CRASH " + e.ExceptionObject);
            var exception = e.ExceptionObject as Exception;
            AppLog.Error("crash", "unhandled-exception", exception);
            // Sıra: simgeler, ayarlar, geçmiş, çökme raporu (bu iş parçacığında, eşzamanlı), günlük.
            AppHost.OnCrash(e.IsTerminating, exception);
        };
        // Gözlenmemiş görev hatası süreci sonlandırmaz (.NET): yalnızca günlüğe yazılır, ayarlar arayüz iş parçacığında diske indirilir.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            DebugLog.Write("UNOBSERVED " + e.Exception);
            AppLog.Warn("task", "unobserved-task-exception", e.Exception);
            e.SetObserved();
            Dispatcher.BeginInvoke(() => AppHost.SaveSettingsNow());
        };
        // Oturum kapanırken süreç Exit'e varmadan sonlandırılabilir: simgeler ve bekleyen kayıtlar şimdi. Oturum nöbetçisi
        // silinmez ("kapanıyor" işaretlenir): kapanış iptal edilirse uygulama sürer.
        SessionEnding += (_, _) =>
        {
            AppHost.RestoreDesktopOnExit();
            AppHost.FlushAll();
            AppHost.MarkSessionEnding();
        };

        try
        {
            StartServices(args);
            // Store sürümü: güncelleme uygulamayı kapatınca Windows onu tepside yeniden başlatsın (paketsizken bir şey yapmaz).
            PackageInfo.RegisterRestartAfterUpdate();
        }
        catch (Exception ex)
        {
            // Yarım başlamış, görünmez bir örnek tek-örnek kilidini tutup sonraki açılışları engellemesin.
            DebugLog.Write("STARTUP " + ex);
            // Kutu açıkken kullanıcı süreci kapatabilir: önce hata günlüğüne.
            AppLog.Error("startup", "startup-failed", ex);
            AppLog.FlushSync(TimeSpan.FromMilliseconds(500));
            System.Windows.MessageBox.Show(StartupErrorMessage(ex), L.F("{0} başlatılamadı", AppInfo.Name),
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            _exiting = true;
            try { AppHost.DoubleClick?.Dispose(); AppHost.Hotkeys?.Dispose(); AppHost.Tray?.Dispose(); } catch { }
            Shutdown(1);
        }
    }

    private void StartServices(Args args)
    {
        AppHost.Initialize(args.Desktop, args.Data);
        // Ayardaki dil (Windows ile aynı / Türkçe / English): ilk pencereden ve widget'tan önce.
        L.Init(AppHost.Settings.Language);
        // Yeni kullanıcı: karşılamada onay verene dek hiçbir dosya taşınmaz. (SetPaused kullanılmaz: false'ta taşıma başlatır.)
        // Hazır kurallar masaüstünde zaten olan klasörlere uyar (ör. İngilizce Windows'ta "Resimler" klasörü varsa o).
        if (!AppHost.Settings.FirstRunDone && Onboarding.PrepareNewUser(AppHost.Settings, DesktopFoldersOrNone())) AppHost.SaveSettings();
        ApplyTheme(AppHost.Settings.Theme);
        // Kısa solma geçişleri: Windows'un "Animasyon efektleri" ve Ayarlar'daki "Animasyonlar" (ilk pencereden önce).
        Views.Motion.Initialize();
        // Windows teması, yüksek karşıtlık ya da vurgu rengi değişince uygulama da uyum sağlasın.
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;

        var tray = new TrayIcon(ShowMainWindow, ShowQuickAdd, ExitApp);
        AppHost.Tray = tray;
        PerfLog.Mark("Tray.created");
        // Gezgin yeniden başladı: widget'lar 3 sn'lik nöbetçiyi beklemeden yeni masaüstüne bağlansın.
        tray.TaskbarCreated += AppHost.Widgets.OnShellRestarted;
        // NESTDESK_TEST_RING=1 (yalnızca --desktop): halka kısayolsuz da açılır (ölçüm, arayüz denemesi; gerçek tuş kapılmaz).
        AppHost.Hotkeys = new HotkeyManager(OnHotkey) { TestRing = Views.RingPopup.TestRing };
        AppHost.Hotkeys.Apply(AppHost.Settings.Hotkeys);
        NoticeRingHotkeyFailure();
        AppHost.DoubleClick = new DesktopDoubleClick(Dispatcher, AppHost.OnDesktopDoubleClick);
        AppHost.ApplyDoubleClickSetting();
        AppHost.Widgets.RestoreAll();
        if (AppHost.Settings.FencesReplaceIcons)
        {
            // Hiçbir öğe görünmez kalmasın: eksik Klasörler/Kısayollar/Dosyalar bölmesi varsa ekle.
            AppHost.Widgets.EnsureDesktopCoverage();
            AppHost.ApplyDesktopState();
        }
        // Kutulara taşınan öğeler: önceki oturum yarıda kaldıysa ya da kutu dışarıdan silindiyse kayıtlar kutularla uzlaşır.
        Widgets.BoxMover.Reconcile();
        // Yeni klasöre "simge ver" önerisi: ayrı izleyici yok, masaüstünün anlık görüntüsünden; yalnızca öneri açıkken dinlenir.
        _newFolders = new NewFolderSuggestions(AppHost.DesktopSnapshot, folder => Dispatcher.BeginInvoke(() =>
        {
            // Uygulamanın kendi açtığı klasör (bölme, "Klasörü oluştur", karşılama) için "simge ver" balonu çıkmaz.
            var quiet = AppHost.ConsumeQuietFolder(folder);
            if (AppHost.Settings.SuggestFolderIcons && !quiet) AppHost.Tray?.SuggestFolderIcon(folder);
        })) { Enabled = AppHost.Settings.SuggestFolderIcons };
        AppHost.SettingsChanged += () =>
        {
            if (!_exiting && _newFolders is { } suggestions) suggestions.Enabled = AppHost.Settings.SuggestFolderIcons;
        };
        // Masaüstü yalnızca otomatik taşıma açıkken izlenir; izleyici arka planda kurulur (ağdaki masaüstünde açılış beklemez).
        if (!AppHost.Settings.Paused) AppHost.Watcher.Resume();
        // İlk masaüstü taraması (otomatik taşıma açıksa) ve klasör simgesi onarımı: widget'lar göründükten sonra, düşük
        // disk önceliğiyle; Windows ile başlarken oturum açılışıyla yarışmasın diye daha geç.
        AppHost.StartDeferredWork(autostart: args.Minimized);
        // Kısayolla açılan halka: kısayol açıksa pencere boşta önceden kurulur (ilk basışta beklenmesin).
        Views.RingPopup.Sync();
        MigrateFromLegacyNameInBackground();
        // Hata günlüğü: oturum başı kaydı (sürüm, kanal, Windows derlemesi, ekranlar, tema; kişisel bilgi yok) ve arayüz nöbetçisi.
        AppLog.Session(Views.Diagnostics.CurrentEnvironment());
        _watchdog = new UiWatchdog(Dispatcher, AppHost.SetSessionHang);
        using (var self = System.Diagnostics.Process.GetCurrentProcess())
            AppHost.StartupMs = (long)(DateTime.Now - self.StartTime).TotalMilliseconds;
        if (PerfLog.Enabled)
            PerfLog.Write($"StartServices bitti: süreç {AppHost.StartupMs} ms"); // l10n: çevrilmez
        PerfLog.Mark("StartServices.end");
        StartTestCrashIfAsked();

        _started = true;
        if (args.Peek) Dispatcher.BeginInvoke(() => TogglePeek(AppHost.PeekOrigin.Command), DispatcherPriority.ApplicationIdle);
        if (args.Add) Dispatcher.BeginInvoke(ShowQuickAdd, DispatcherPriority.ApplicationIdle);
        else if (args.Welcome) ShowWelcome(rerun: AppHost.Settings.FirstRunDone);
        else if (!AppHost.Settings.FirstRunDone)
        {
            // Windows ile sessizce başladıysa karşılama kendiliğinden açılmaz; balondan açılır.
            if (args.Minimized)
                AppHost.Tray?.Notify(L.F("{0} kuruluma hazır", AppInfo.Name), L.T("Masaüstünü birkaç adımda düzenlemek için buraya tıkla."),
                    () => ShowWelcome());
            else ShowWelcome();
        }
        else if (args.Restart) ShowPage(typeof(Views.SettingsPage));   // dilin değiştirildiği yere dönülür
        else if (!args.Minimized) ShowMainWindow();

        if (AppHost.Settings.FirstRunDone && !AppHost.Settings.RenameNoticeShown)
        {
            AppHost.Settings.RenameNoticeShown = true;
            AppHost.Settings.CloseToTrayHintShown = true;   // mevcut kullanıcı tepsiyi zaten biliyor; ilk gün iki balon görmesin
            AppHost.SaveSettings();
            Dispatcher.BeginInvoke(() => AppHost.Tray?.Notify(L.F("{0} artık {1}", AppInfo.FormerName, AppInfo.Name),
                L.T("Adı ve ana penceresi yenilendi; ayarların, widget'ların ve kuralların olduğu gibi duruyor. Açmak için tıkla."),
                ShowMainWindow), DispatcherPriority.ApplicationIdle);
        }
        else if (AppHost.Settings.FirstRunDone && AppHost.DataFolderSource == DataFolderSource.Moved)
        {
            // 2.0'dan güncelleme (veri klasörü bu açılışta taşındı; bir kez olur). Windows tepsi simgesi tercihini ve görev
            // çubuğu sabitlemesini exe yoluna göre tutar: program dosyasının adı değiştiği için ikisi de sıfırlanmış olabilir.
            // Ana pencere açıksa şeritte, değilse balonda.
            Dispatcher.BeginInvoke(() => Views.Notice.Show(
                L.F("{0} güncellendi. Program dosyasının adı değiştiği için tepsi simgesi saatin yanındaki ^ okunun altına geçmiş olabilir; oradan görev çubuğuna sürükleyebilirsin. Görev çubuğuna sabitlediysen yeniden sabitle.",
                    AppInfo.Name),
                Views.NoticeKind.Info), DispatcherPriority.ApplicationIdle);
        }

        // Ayarlar daha yeni bir sürümle kaydedilmiş (kullanıcı eski sürüme döndü): bir kez, kalıcı şeritte ya da balonda.
        // Dosyanın önceki hâli açılışta yedeklendiyse (SettingsVersion.BackupBeforeChange) yedekler klasörü açılabilir.
        if (AppHost.SettingsFromNewerVersion is { } newer)
            Dispatcher.BeginInvoke(() =>
            {
                if (newer.BackedUp)
                    Views.Notice.Show(
                        L.F("Ayarların {0} {1} ile kaydedilmiş. Bu sürümün bilmediği ayarların çoğu korunur; bazı yeni özellikler burada görünmez. Önceki hâli yedekler klasöründe.",
                            AppInfo.Name, newer.Version),
                        Views.NoticeKind.Info, L.T("Yedekler klasörünü aç"), () => AppHost.OpenDataSubfolder(SettingsVersion.BackupFolder),
                        trayHint: L.T("Yedekler klasörünü açmak için buraya tıkla."));
                else
                    Views.Notice.Show(
                        L.F("Ayarların {0} {1} ile kaydedilmiş. Bu sürümün bilmediği ayarların çoğu korunur; bazı yeni özellikler burada görünmez.",
                            AppInfo.Name, newer.Version),
                        Views.NoticeKind.Info);
            }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Halka menü kısayolu açılışta kaydedilemediyse (başka bir uygulama kullanıyor, AltGr çakışması…) oturumda bir kez söylenir;
    /// kısayol Ayarlar'dan değiştirilebilir.
    /// </summary>
    private void NoticeRingHotkeyFailure()
    {
        if (AppHost.Hotkeys?.Failures.TryGetValue(HotkeyAction.Ring, out var failure) != true) return;
        var hotkey = AppHost.Settings.Hotkeys.Ring;
        Dispatcher.BeginInvoke(() => Views.Notice.Show(
                L.F("Halka menü kısayolu ({0}) kaydedilemedi: {1}. Ayarlar'dan başka bir birleşim seçebilirsin.", hotkey ?? "", failure),
                Views.NoticeKind.Warning, L.T("Ayarları aç"), () => ShowPage(typeof(Views.SettingsPage)),
                L.T("Kısayolu seçmek için buraya tıkla.")),
            DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Arayüz denemesi (yalnızca --desktop ile): NESTDESK_TEST_CRASH=1 ise açılıştan birkaç saniye sonra arka plan iş
    /// parçacığında yakalanmayan bir hata atılır ve süreç sonlanır (çökme yolu, rapor, sonraki açılıştaki şerit denensin).
    /// </summary>
    private void StartTestCrashIfAsked()
    {
        if (!AppHost.IsTestDesktop || AppEnvironment.Get("TEST_CRASH") != "1") return;
        Dispatcher.BeginInvoke(() => new Thread(() =>
        {
            Thread.Sleep(2000);
            throw new InvalidOperationException("NESTDESK_TEST_CRASH");
        })
        { IsBackground = true, Name = "test crash" }.Start(), DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// 2.0 ve öncesinden (Duzenleme.exe) kalanlar, arka planda: "Windows ile başlat" değerinin yeni adı ve taşınabilir klasörün
    /// üzerine açılan 2.1'in yanında kalan eski program dosyaları. Test örneği gerçek kayıt defterine ve dosyalara dokunmaz.
    /// </summary>
    private static void MigrateFromLegacyNameInBackground()
    {
        if (AppHost.IsTestDesktop || PackageInfo.IsPackaged) return;
        var portable = AppHost.IsPortable;
        Task.Run(() =>
        {
            StartupRegistration.MigrateLegacyRunValue();
            if (!portable) return;
            var current = typeof(App).Assembly.GetName().Version ?? new Version(0, 0);
            var files = LegacyFiles.ProgramFilesToDelete(AppContext.BaseDirectory, current, System.IO.File.Exists, FileVersionOf);
            foreach (var file in files)
            {
                try { System.IO.File.Delete(file); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { DebugLog.Write($"eski dosya silinemedi: {file} {ex.Message}"); }
            }
            if (files.Count > 0) DebugLog.Write($"taşınabilir klasörden eski program dosyaları silindi: {files.Count}");
        });
    }

    private static Version? FileVersionOf(string path)
    {
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            // Sürüm bilgisi olmayan dosya 0.0.0.0 döner: bilinmiyor sayılır (silinmez).
            return info.FileMajorPart == 0 && info.FileMinorPart == 0 && info.FileBuildPart == 0 ? null
                : new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private static string StartupErrorMessage(Exception ex) => ex switch
    {
        UnauthorizedAccessException or IOException when AppHost.DataDirectory.Length > 0 =>
            L.F("Ayar klasörüne erişilemiyor:\n{0}\n\n{1}\n\nTaşınabilir sürümü kullanıyorsan programı yazılabilir bir klasöre (ör. Belgeler) çıkar.",
                AppHost.DataDirectory, ex.Message),
        _ => L.F("Beklenmeyen bir hata oluştu:\n{0}", ex.Message),
    };

    /// <summary>Masaüstündeki klasörler (anlık görüntüden; ilk okuma bitmediyse bir kez diskten, hata olursa boş).</summary>
    private static List<string> DesktopFoldersOrNone() => AppHost.DesktopFolders();

    /// <summary>
    /// --restart: önceki örneğin tek örnek kilidini bırakmasını en çok 15 sn bekler ve kilidi alır (bu örnek ilk örnek
    /// olur). Önceki örnek yoksa ya da kapanmadıysa null: olağan yol devam eder (kapanmadıysa onun penceresi öne gelir).
    /// </summary>
    private static Mutex? TakeOverInstanceLock(string id)
    {
        Mutex running;
        try { running = Mutex.OpenExisting(id); }
        catch (WaitHandleCannotBeOpenedException) { return null; }
        try
        {
            if (running.WaitOne(TimeSpan.FromSeconds(15))) return running;
        }
        catch (AbandonedMutexException)
        {
            return running;   // önceki örnek kilidi bırakmadan sonlandı; kilit yine de bu örneğe geçti
        }
        running.Dispose();
        return null;
    }

    /// <summary>
    /// Uygulamayı yeniden başlatır (Ayarlar → Dil → "Şimdi yeniden başlat"): yeni süreç --restart ile açılır, bu örnek
    /// kapanıp kilidi bırakınca onu devralır ve Ayarlar sayfasını açar. Test klasörleri (--desktop/--data) aynen geçer.
    /// </summary>
    public void Restart()
    {
        if (_exiting || Environment.ProcessPath is not { } exe) return;
        var start = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false };
        start.ArgumentList.Add("--restart");
        if (_args?.Desktop is { } desktop) { start.ArgumentList.Add("--desktop"); start.ArgumentList.Add(desktop); }
        if (_args?.Data is { } data) { start.ArgumentList.Add("--data"); start.ArgumentList.Add(data); }
        try
        {
            using var process = System.Diagnostics.Process.Start(start);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            DebugLog.Write("yeniden başlatma: " + ex);
            Views.Notice.Show(L.F("Yeniden başlatılamadı: {0}", ex.Message), Views.NoticeKind.Error);
            return;
        }
        ExitApp();
    }

    // Bazı sistemler/programlar WM_SETTINGCHANGE'i sık yayınlar: tema yarım saniyelik sessizlikten sonra ve yalnızca
    // gerçekten değiştiyse yeniden uygulanır (her yayında bütün pencereler yeniden boyanmasın).
    private DispatcherTimer? _themeDelay;
    private static (ApplicationTheme Theme, bool HighContrast)? _appliedTheme;

    // Widget'ların baktığı Windows durumu (açık/koyu kip, yüksek karşıtlık ve renkleri); uygulamanın kendi temasından ayrı izlenir.
    private static Widgets.WidgetPalette.SystemLook? _widgetLook;

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color
            or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Accessibility)
            Dispatcher.BeginInvoke(QueueThemeCheck);
    }

    private void OnSystemParameterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
            Dispatcher.BeginInvoke(QueueThemeCheck);
    }

    private void QueueThemeCheck()
    {
        if (_themeDelay is null)
        {
            _themeDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _themeDelay.Tick += (_, _) =>
            {
                _themeDelay.Stop();
                if (!_exiting) ApplyTheme(AppHost.Settings.Theme, onlyIfChanged: true);
            };
        }
        _themeDelay.Stop();
        _themeDelay.Start();
    }

    public static void ApplyTheme(AppTheme theme) => ApplyTheme(theme, onlyIfChanged: false);

    private static void ApplyTheme(AppTheme theme, bool onlyIfChanged)
    {
        // WPF-UI temayı Application.MainWindow'a uygular; bu bir widget olursa saydam zemini opaklaşır.
        if (Current.MainWindow is Widgets.WidgetWindow) Current.MainWindow = null;
        SystemThemeManager.UpdateSystemThemeCache();
        var highContrast = SystemParameters.HighContrast;
        var system = ApplicationThemeManager.GetSystemTheme();
        // Windows 11'in "Sunrise", "Flow" gibi açık temaları da açık sayılsın.
        var systemDark = system is SystemTheme.Dark or SystemTheme.Glow or SystemTheme.CapturedMotion;
        var resolved = highContrast ? ApplicationTheme.HighContrast : theme switch
        {
            AppTheme.Dark => ApplicationTheme.Dark,
            AppTheme.Light => ApplicationTheme.Light,
            _ => systemDark ? ApplicationTheme.Dark : ApplicationTheme.Light,
        };
        // Widget'lar uygulamanın temasına değil Windows'a bakar ("Otomatik" ve iki hâlli temalar, yüksek karşıtlık): uygulamanın
        // teması Koyu/Açık'a sabitlenmiş olsa da (aşağıdaki erken dönüş) Windows değişince yeniden çizilirler.
        // Bir yüksek karşıtlık temasından ötekine geçiş de sayılır (yalnızca Windows'un renkleri değişir).
        var look = Widgets.WidgetPalette.SystemLook.Current(systemDark, highContrast);
        if (_widgetLook != look)
        {
            var (restyle, all) = Widgets.WidgetPalette.SystemLook.Change(_widgetLook, look);
            _widgetLook = look;
            Widgets.WidgetPalette.SystemDark = systemDark;
            if (restyle) AppHost.Widgets?.RestyleAll(all);
        }
        if (onlyIfChanged && _appliedTheme == (resolved, highContrast)) return;
        _appliedTheme = (resolved, highContrast);
        var backdrop = highContrast ? WindowBackdropType.None : WindowBackdropType.Mica;
        ApplicationThemeManager.Apply(resolved, backdrop, updateAccent: false);
        if (!highContrast) ApplicationAccentColorManager.Apply(Brand, resolved);

        // Apply yalnızca Application.MainWindow'u günceller; açık diğer pencereler (simge seçici vb.) de güncellensin.
        // Widget pencereleri (FluentWindow değiller) burada atlanır: kendi paletlerini kullanırlar ve Windows'un açık/koyu kipi ya da
        // yüksek karşıtlık değişince yukarıda WidgetManager.RestyleAll ile yeniden çizilirler.
        foreach (var window in Current.Windows.OfType<FluentWindow>())
            if (!ReferenceEquals(window, Current.MainWindow))
                WindowBackgroundManager.UpdateBackground(window, resolved, backdrop);
    }

    private bool _started;

    /// <summary>Tek tıkla widget/bölme ekleme penceresi.</summary>
    public void ShowQuickAdd()
    {
        if (!_started || _exiting) return;
        Views.QuickAddWindow.ShowNearCursor();
    }

    /// <summary>Karşılamayı açar. rerun: var olanları silmeden yeniden kurulum (Ayarlar → Yardım → Karşılama, --welcome).</summary>
    public void ShowWelcome(bool rerun = false)
    {
        if (!_started || _exiting) return;
        Views.Welcome.Show(rerun || AppHost.Settings.FirstRunDone);
    }

    /// <summary>Ana pencereyi açıp verilen sayfaya geçer (karşılama bitmediyse önce karşılama gelir).</summary>
    public void ShowPage(Type page)
    {
        ShowMainWindow();
        if (AppHost.Settings.FirstRunDone) _mainWindow?.NavigateTo(page);
    }

    public void ShowMainWindow()
    {
        // Karşılama tamamlanmadan ana pencere açılmaz: tepsi, kısayol ve ikinci örnek de önce karşılamayı gösterir.
        if (!AppHost.Settings.FirstRunDone) { ShowWelcome(); return; }
        if (!_started || _exiting) return;
        if (_mainWindow is null)
        {
            // Açılış süresi ölçümü (NESTDESK_DEBUGLOG açıksa): oluşturmadan ilk Loaded'a dek.
            var opening = System.Diagnostics.Stopwatch.StartNew();
            var window = new MainWindow();
            RoutedEventHandler? onLoaded = null;
            onLoaded = (_, _) =>
            {
                window.Loaded -= onLoaded;
                DebugLog.Write($"ana pencere açıldı: {opening.ElapsedMilliseconds} ms");
            };
            window.Loaded += onLoaded;
            _mainWindow = window;
        }
        // WPF ilk oluşturulan pencereyi (bir widget) ana pencere yapar; tema değişikliği yanlış pencereye gitmesin.
        MainWindow = _mainWindow;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    /// <summary>
    /// Kısayolun işini yapar (kısayol ve halka menünün NestDesk eylemleri; bkz. Core.RingAppIds). Halkanın kendisi burada
    /// değildir (halkadan halka açılmaz): yalnızca kısayol açar (<see cref="OnHotkey"/>).
    /// </summary>
    internal void RunAction(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.ToggleDesktop: AppHost.ToggleDesktop(); break;
            case HotkeyAction.OrganizeNow: AppHost.OrganizeNowInBackground(); break;
            case HotkeyAction.OpenApp: ShowMainWindow(); break;
            case HotkeyAction.NewNote: NewNote(); break;
            case HotkeyAction.PeekWidgets: AppHost.Widgets.RevealAll(); break;
            case HotkeyAction.QuickAdd: ShowQuickAdd(); break;
            case HotkeyAction.PeekDesktop: TogglePeek(AppHost.PeekOrigin.Hotkey); break;
        }
    }

    private void OnHotkey(HotkeyAction action)
    {
        if (action == HotkeyAction.Ring)
        {
            if (_started && !_exiting) Views.RingPopup.Toggle();
            return;
        }
        RunAction(action);
    }

    /// <summary>
    /// Kısayolla yeni not: gizli masaüstü ya da göz atma biter (widget'lar ve simgeler durumla birlikte geri gelir), not
    /// ayardaki yere eklenir ve yazmaya hazırdır; ilk seferlerde nereye geldiği söylenir.
    /// </summary>
    private void NewNote()
    {
        if (!_started || _exiting) return;
        AppHost.EnsureWidgetsShown();
        var config = AppHost.Widgets.Add(WidgetKind.Note);
        AppHost.Widgets.FocusNote(config.Id);
        AppHost.ShowNewWidgetHint(config);
    }

    /// <summary>Windows masaüstüne göz at ya da NestDesk'e dön (kısayol, --peek).</summary>
    private void TogglePeek(AppHost.PeekOrigin origin)
    {
        if (!_started || _exiting) return;
        AppHost.TogglePeek(origin);
    }

    public void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;
        try
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
            AppHost.RestoreDesktopOnExit();
            Views.PeekBar.CloseBar();
            AppHost.DoubleClick?.Dispose();
            Views.RingPopup.CloseForReal();
            _watchdog?.Dispose();
            _newFolders?.Dispose();
            AppHost.Hotkeys?.Dispose();
            AppHost.Watcher?.Dispose();
            AppHost.Widgets?.CloseAll();
            AppHost.Snapshots?.Dispose();
            AppHost.Tray?.Dispose();
            _mainWindow?.CloseForReal();
        }
        finally
        {
            // Kapanan widget'ların son hâli (notun son yazılanları) ve bekleyen kayıtlar diske insin.
            AppHost.FlushAll();
            // Temizlikte bir şey ters gitse de süreç kapanmalı (kurulum programı --exit ile bekliyor olabilir).
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Başka bir yoldan kapanıyorsa (ör. Shutdown) bekleyen kayıtlar kaybolmasın; temizse bir şey yazmaz.
        AppHost.FlushAll();
        // Temiz çıkış: oturum nöbetçisi silinir (yalnızca asıl örnek; kilit bırakılmadan önce, --restart'la gelen yeni örnek
        // kendi nöbetçisini yazmadan önce).
        AppHost.EndSession();
        // --exit ile bekleyen kurulum programı hemen devam edebilsin.
        try { _instanceMutex?.ReleaseMutex(); }
        catch (ApplicationException) { }
        _instanceMutex?.Dispose();
        _showSignal?.Dispose();
        _exitSignal?.Dispose();
        _addSignal?.Dispose();
        _peekSignal?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Arayüz iş parçacığındaki yakalanmamış hata: tek bir hata tüm uygulamayı (ve masaüstü izlemeyi) kapatmasın. İleti kutusu
    /// yok; hata günlüğüne yazılır, ayarlar diske iner ve şeritte (pencere gizliyse balonda) kısaca söylenir. "Ayrıntıları
    /// kopyala" yalnızca temizlenmiş metni panoya koyar. Aynı imza oturumda bir kez, toplam en çok üç kez bildirilir; dakikada
    /// 20'den çok hata günlüğe tek bir "exception-storm" olarak da yazılır.
    /// </summary>
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DebugLog.Write("UNHANDLED " + e.Exception);
        e.Handled = true;
        try
        {
            AppLog.Error("ui", "unhandled-ui-exception", e.Exception);
            AppHost.SaveSettingsNow();
            var error = AppLog.Channel.Scrubber.Error(e.Exception);
            var decision = _uiErrors.Note(LogEntry.SignatureOf(AppLogLevel.Error, "ui", "unhandled-ui-exception", error), Environment.TickCount64);
            if (decision.Storm) AppLog.Warn("ui", "exception-storm");
            if (!decision.Notify || _exiting) return;
            var details = ErrorDetails.Text(error, AppInfo.Version, AppLog.Channel.Session);
            Views.Notice.Show(L.F("Beklenmeyen bir hata oldu; {0} çalışmaya devam ediyor.", AppInfo.Name), Views.NoticeKind.Warning,
                L.T("Ayrıntıları kopyala"), () => Views.Diagnostics.CopyDetails(details),
                trayHint: L.T("Ayrıntıları panoya kopyalamak için tıkla."));
        }
        catch (Exception ex)
        {
            // Bildirim yolunun kendisi hata verirse yeni bir döngü başlamasın.
            DebugLog.Write("UNHANDLED (bildirim) " + ex);
        }
    }

    private sealed record Args(string? Desktop, string? Data, bool Minimized, bool Exit, bool Add, bool Welcome, bool RestoreDesktop,
        bool Restart, bool Peek);

    private Args? _args;

    /// <summary>
    /// --desktop ve --data test için gerçek masaüstü yerine başka klasör kullandırır; --exit çalışan örneği kapatır;
    /// --welcome karşılamayı açar (ilk açılış tamamlandıysa yeniden kurulum olarak); --restore-desktop gizli bırakılmış
    /// masaüstü simgelerini açıp çıkar (kaldırma programı); --restart önceki örneğin kapanmasını bekleyip onun yerine
    /// başlar (dil değişikliği); --peek Windows masaüstüne göz atar (göz atılıyorsa NestDesk'e döner).
    /// </summary>
    private static Args ParseArgs(string[] args)
    {
        string? desktop = null, data = null;
        bool minimized = false, exit = false, add = false, welcome = false, restoreDesktop = false, restart = false, peek = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--desktop" when i + 1 < args.Length: desktop = args[++i]; break;
                case "--data" when i + 1 < args.Length: data = args[++i]; break;
                case "--minimized": minimized = true; break;
                case "--exit": exit = true; break;
                case "--add": add = true; break;
                case "--welcome": welcome = true; break;
                case "--restore-desktop": restoreDesktop = true; break;
                case "--restart": restart = true; break;
                case "--peek": peek = true; break;
            }
        }
        return new Args(desktop, data, minimized, exit, add, welcome, restoreDesktop, restart, peek);
    }

    /// <summary>Bu kullanıcının (ya da test örneğinin) çalışan bir NestDesk'i var mı? (Tek örnek kilidi alınmadan bakılır.)</summary>
    private static bool InstanceRunning(string id)
    {
        try
        {
            if (!Mutex.TryOpenExisting(id, out var running)) return false;
            running.Dispose();
            return true;
        }
        catch (UnauthorizedAccessException) { return true; }   // var ama açılamıyor: çalışıyor say
    }
}
