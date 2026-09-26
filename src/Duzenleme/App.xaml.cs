using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Duzenleme;

public partial class App : Application
{
    public static readonly Color Brand = Color.FromRgb(0x8B, 0x5C, 0xF6);

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private MainWindow? _mainWindow;
    private NewFolderWatcher? _newFolders;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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

        // Tek örnek: ikinci açılış ilk örneğin penceresini öne getirir.
        var id = "Duzenleme." + Environment.UserName + (args.Desktop is null ? "" : ".test");
        _instanceMutex = new Mutex(true, id, out var isFirst);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, id + ".show");
        if (!isFirst)
        {
            _showSignal.Set();
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => Dispatcher.BeginInvoke(ShowMainWindow), null, -1, false);

        DispatcherUnhandledException += OnUnhandledException;
        SessionEnding += (_, _) => AppHost.RestoreDesktopOnExit();

        AppHost.Initialize(args.Desktop, args.Data);
        ApplyTheme(AppHost.Settings.Theme);

        AppHost.Tray = new TrayIcon(ShowMainWindow, ExitApp);
        AppHost.Hotkeys = new HotkeyManager(OnHotkey);
        AppHost.Hotkeys.Apply(AppHost.Settings.Hotkeys);
        AppHost.DoubleClick = new DesktopDoubleClick(Dispatcher, AppHost.ToggleDesktop);
        AppHost.ApplyDoubleClickSetting();
        AppHost.Widgets.RestoreAll();
        _newFolders = new NewFolderWatcher(AppHost.DesktopDirectory, folder => Dispatcher.BeginInvoke(() =>
        {
            if (AppHost.Settings.SuggestFolderIcons) AppHost.Tray?.SuggestFolderIcon(folder);
        }));
        AppHost.Watcher.Start();
        if (!AppHost.Settings.Paused) AppHost.OrganizeNowInBackground();

        if (!AppHost.Settings.FirstRunDone)
        {
            // İlk açılışta saat ve tarih widget'larını hazır getir.
            AppHost.Widgets.Add(WidgetKind.Clock);
            AppHost.Widgets.Add(WidgetKind.Date);
            AppHost.Settings.FirstRunDone = true;
            AppHost.SaveSettings();
        }

        if (!args.Minimized) ShowMainWindow();
    }

    public static void ApplyTheme(AppTheme theme)
    {
        var resolved = theme switch
        {
            AppTheme.Dark => ApplicationTheme.Dark,
            AppTheme.Light => ApplicationTheme.Light,
            _ => ApplicationThemeManager.GetSystemTheme() == SystemTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark,
        };
        ApplicationThemeManager.Apply(resolved, WindowBackdropType.Mica, updateAccent: false);
        ApplicationAccentColorManager.Apply(Brand, resolved);
    }

    public void ShowMainWindow()
    {
        _mainWindow ??= new MainWindow();
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
            case HotkeyAction.NewNote: AppHost.Widgets.FocusNote(AppHost.Widgets.Add(WidgetKind.Note).Id); break;
        }
    }

    public void ExitApp()
    {
        AppHost.RestoreDesktopOnExit();
        AppHost.DoubleClick?.Dispose();
        _newFolders?.Dispose();
        AppHost.Hotkeys?.Dispose();
        AppHost.Watcher.Dispose();
        AppHost.Widgets.CloseAll();
        AppHost.Tray?.Dispose();
        _mainWindow?.CloseForReal();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.Dispose();
        _showSignal?.Dispose();
        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Tek bir hata tüm uygulamayı (ve masaüstü izlemeyi) kapatmasın.
        System.Windows.MessageBox.Show(e.Exception.Message, "Düzenleme — beklenmeyen hata", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        e.Handled = true;
    }

    private sealed record Args(string? Desktop, string? Data, bool Minimized);

    /// <summary>--desktop ve --data test için gerçek masaüstü yerine başka klasör kullandırır.</summary>
    private static Args ParseArgs(string[] args)
    {
        string? desktop = null, data = null;
        var minimized = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--desktop" when i + 1 < args.Length: desktop = args[++i]; break;
                case "--data" when i + 1 < args.Length: data = args[++i]; break;
                case "--minimized": minimized = true; break;
            }
        }
        return new Args(desktop, data, minimized);
    }
}
