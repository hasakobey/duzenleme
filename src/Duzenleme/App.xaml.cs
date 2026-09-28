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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
    private MainWindow? _mainWindow;
    private NewFolderWatcher? _newFolders;
    private bool _exiting;

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
        var args = ParseArgs(e.Args);

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

        // Store (MSIX) sürümü: başlangıç görevi exe'yi argümansız açar; --minimized gibi tepside sessizce başlasın.
        if (e.Args.Length == 0 && PackageInfo.LaunchedByStartupTask()) args = args with { Minimized = true };

        // Tek örnek: ikinci açılış ilk örneğin penceresini öne getirir.
        var id = AppInfo.InstanceIdPrefix + Environment.UserName + (args.Desktop is null ? "" : ".test");

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

        _instanceMutex = new Mutex(true, id, out var isFirst);
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
                AllowSetForegroundWindow(ASFW_ANY);
                (args.Add ? _addSignal : _showSignal).Set();
            }
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => Dispatcher.BeginInvoke(ShowMainWindow), null, -1, false);
        ThreadPool.RegisterWaitForSingleObject(_addSignal, (_, _) => Dispatcher.BeginInvoke(ShowQuickAdd), null, -1, false);
        ThreadPool.RegisterWaitForSingleObject(_peekSignal, (_, _) => Dispatcher.BeginInvoke(() => TogglePeek(AppHost.PeekOrigin.Command)), null, -1, false);
        _exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, id + ".exit");
        ThreadPool.RegisterWaitForSingleObject(_exitSignal, (_, _) => Dispatcher.BeginInvoke(ExitApp), null, -1, true);

        DispatcherUnhandledException += OnUnhandledException;
        SessionEnding += (_, _) => AppHost.RestoreDesktopOnExit();

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
            System.Windows.MessageBox.Show(StartupErrorMessage(ex), $"{AppInfo.Name} başlatılamadı",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            _exiting = true;
            try { AppHost.DoubleClick?.Dispose(); AppHost.Hotkeys?.Dispose(); AppHost.Tray?.Dispose(); } catch { }
            Shutdown(1);
        }
    }

    private void StartServices(Args args)
    {
        AppHost.Initialize(args.Desktop, args.Data);
        // Yeni kullanıcı: karşılamada onay verene dek hiçbir dosya taşınmaz. (SetPaused kullanılmaz: false'ta taşıma başlatır.)
        if (Onboarding.PrepareNewUser(AppHost.Settings)) AppHost.SaveSettings();
        ApplyTheme(AppHost.Settings.Theme);
        // Windows teması, yüksek karşıtlık ya da vurgu rengi değişince uygulama da uyum sağlasın.
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;

        AppHost.Tray = new TrayIcon(ShowMainWindow, ShowQuickAdd, ExitApp);
        AppHost.Hotkeys = new HotkeyManager(OnHotkey);
        AppHost.Hotkeys.Apply(AppHost.Settings.Hotkeys);
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
        _newFolders = new NewFolderWatcher(AppHost.DesktopDirectory, folder => Dispatcher.BeginInvoke(() =>
        {
            // Uygulamanın kendi açtığı klasör (bölme, "Klasörü oluştur", karşılama) için "simge ver" balonu çıkmaz.
            var quiet = AppHost.ConsumeQuietFolder(folder);
            if (AppHost.Settings.SuggestFolderIcons && !quiet) AppHost.Tray?.SuggestFolderIcon(folder);
        }));
        AppHost.Watcher.Start();
        if (!AppHost.Settings.Paused) AppHost.OrganizeNowInBackground();
        // Eski sürümün mutlak yollu klasör simgelerini onar; arka planda, başlangıcı geciktirmeden ve düşürmeden.
        var desktop = AppHost.DesktopDirectory;
        Task.Run(() =>
        {
            try { Icons.FolderIconService.RepairDesktopFolders(desktop); }
            catch (Exception ex) { DebugLog.Write($"klasör simgesi onarımı: {ex}"); }
        });

        _started = true;
        if (args.Peek) Dispatcher.BeginInvoke(() => TogglePeek(AppHost.PeekOrigin.Command), DispatcherPriority.ApplicationIdle);
        if (args.Add) Dispatcher.BeginInvoke(ShowQuickAdd, DispatcherPriority.ApplicationIdle);
        else if (args.Welcome) ShowWelcome(rerun: AppHost.Settings.FirstRunDone);
        else if (!AppHost.Settings.FirstRunDone)
        {
            // Windows ile sessizce başladıysa karşılama kendiliğinden açılmaz; balondan açılır.
            if (args.Minimized)
                AppHost.Tray?.Notify($"{AppInfo.Name} kuruluma hazır", "Masaüstünü birkaç adımda düzenlemek için buraya tıkla.", () => ShowWelcome());
            else ShowWelcome();
        }
        else if (!args.Minimized) ShowMainWindow();

        if (AppHost.Settings.FirstRunDone && !AppHost.Settings.RenameNoticeShown)
        {
            AppHost.Settings.RenameNoticeShown = true;
            AppHost.Settings.CloseToTrayHintShown = true;   // mevcut kullanıcı tepsiyi zaten biliyor; ilk gün iki balon görmesin
            AppHost.SaveSettings();
            Dispatcher.BeginInvoke(() => AppHost.Tray?.Notify($"{AppInfo.FormerName} artık {AppInfo.Name}",
                "Adı ve ana penceresi yenilendi; ayarların, widget'ların ve kuralların olduğu gibi duruyor. Açmak için tıkla.",
                ShowMainWindow), DispatcherPriority.ApplicationIdle);
        }
    }

    private static string StartupErrorMessage(Exception ex) => ex switch
    {
        UnauthorizedAccessException or IOException when AppHost.DataDirectory.Length > 0 =>
            $"Ayar klasörüne erişilemiyor:\n{AppHost.DataDirectory}\n\n{ex.Message}\n\n" +
            "Taşınabilir sürümü kullanıyorsan programı yazılabilir bir klasöre (ör. Belgeler) çıkar.",
        _ => $"Beklenmeyen bir hata oluştu:\n{ex.Message}",
    };

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color
            or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Accessibility)
            Dispatcher.BeginInvoke(() => ApplyTheme(AppHost.Settings.Theme));
    }

    private void OnSystemParameterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
            Dispatcher.BeginInvoke(() => ApplyTheme(AppHost.Settings.Theme));
    }

    public static void ApplyTheme(AppTheme theme)
    {
        // WPF-UI temayı Application.MainWindow'a uygular; bu bir widget olursa saydam zemini opaklaşır.
        if (Current.MainWindow is Widgets.WidgetWindow) Current.MainWindow = null;
        SystemThemeManager.UpdateSystemThemeCache();
        var highContrast = SystemParameters.HighContrast;
        var system = ApplicationThemeManager.GetSystemTheme();
        var resolved = highContrast ? ApplicationTheme.HighContrast : theme switch
        {
            AppTheme.Dark => ApplicationTheme.Dark,
            AppTheme.Light => ApplicationTheme.Light,
            // Windows 11'in "Sunrise", "Flow" gibi açık temaları da açık sayılsın.
            _ => system is SystemTheme.Dark or SystemTheme.Glow or SystemTheme.CapturedMotion ? ApplicationTheme.Dark : ApplicationTheme.Light,
        };
        var backdrop = highContrast ? WindowBackdropType.None : WindowBackdropType.Mica;
        ApplicationThemeManager.Apply(resolved, backdrop, updateAccent: false);
        if (!highContrast) ApplicationAccentColorManager.Apply(Brand, resolved);

        // Apply yalnızca Application.MainWindow'u günceller; açık diğer pencereler (simge seçici vb.) de güncellensin.
        // Widget pencerelerine dokunulmaz: onlar kendi saydam zeminlerini kullanır.
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
            // Açılış süresi ölçümü (DUZENLEME_DEBUGLOG açıksa): oluşturmadan ilk Loaded'a dek.
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

    private void OnHotkey(HotkeyAction action)
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
            _newFolders?.Dispose();
            AppHost.Hotkeys?.Dispose();
            AppHost.Watcher?.Dispose();
            AppHost.Widgets?.CloseAll();
            AppHost.Tray?.Dispose();
            _mainWindow?.CloseForReal();
        }
        finally
        {
            // Temizlikte bir şey ters gitse de süreç kapanmalı (kurulum programı --exit ile bekliyor olabilir).
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
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

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Tek bir hata tüm uygulamayı (ve masaüstü izlemeyi) kapatmasın.
        DebugLog.Write("UNHANDLED " + e.Exception);
        System.Windows.MessageBox.Show(e.Exception.Message, $"{AppInfo.Name} — beklenmeyen hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        e.Handled = true;
    }

    private sealed record Args(string? Desktop, string? Data, bool Minimized, bool Exit, bool Add, bool Welcome, bool Peek);

    /// <summary>
    /// --desktop ve --data test için gerçek masaüstü yerine başka klasör kullandırır; --exit çalışan örneği kapatır;
    /// --welcome karşılamayı açar (ilk açılış tamamlandıysa yeniden kurulum olarak); --peek Windows masaüstüne göz atar
    /// (göz atılıyorsa NestDesk'e döner).
    /// </summary>
    private static Args ParseArgs(string[] args)
    {
        string? desktop = null, data = null;
        bool minimized = false, exit = false, add = false, welcome = false, peek = false;
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
                case "--peek": peek = true; break;
            }
        }
        return new Args(desktop, data, minimized, exit, add, welcome, peek);
    }
}
