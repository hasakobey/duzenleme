using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Duzenleme;

public partial class App : Application
{
    public static readonly Color Brand = Color.FromRgb(0x8B, 0x5C, 0xF6);

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = ParseArgs(e.Args);

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

        AppHost.Initialize(args.Desktop, args.Data);
        ApplyTheme(AppHost.Settings.Theme);

        AppHost.Tray = new TrayIcon(ShowMainWindow, ExitApp);
        AppHost.Widgets.RestoreAll();
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

    public void ExitApp()
    {
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
