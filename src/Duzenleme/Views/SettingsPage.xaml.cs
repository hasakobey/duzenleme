using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Duzenleme.Core;

namespace Duzenleme.Views;

public partial class SettingsPage : Page
{
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        _loading = true;
        var s = AppHost.Settings;
        ThemeBox.SelectedIndex = s.Theme switch { AppTheme.Dark => 1, AppTheme.Light => 2, _ => 0 };
        StartupToggle.IsChecked = StartupRegistration.IsEnabled;
        NotifyToggle.IsChecked = s.ShowNotifications;
        DesktopPath.Text = AppHost.DesktopDirectory;
        DataPath.Text = AppHost.DataDirectory;
        VersionText.Text = "Düzenleme " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        _loading = false;
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        AppHost.Settings.Theme = ThemeBox.SelectedIndex switch { 1 => AppTheme.Dark, 2 => AppTheme.Light, _ => AppTheme.System };
        AppHost.SaveSettings();
        App.ApplyTheme(AppHost.Settings.Theme);
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e) => StartupRegistration.Set(StartupToggle.IsChecked == true);

    private void NotifyToggle_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.ShowNotifications = NotifyToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    private void OpenDesktop_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppHost.DesktopDirectory) { UseShellExecute = true });

    private void OpenData_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppHost.DataDirectory) { UseShellExecute = true });

    private void Exit_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).ExitApp();
}
