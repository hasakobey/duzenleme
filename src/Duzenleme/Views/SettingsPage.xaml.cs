using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Duzenleme.Core;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

public sealed class HotkeyRow(HotkeyAction action, string label, SymbolRegular icon, string text, string? failure)
{
    public HotkeyAction Action { get; } = action;
    public string Label { get; } = label;
    public SymbolRegular Icon { get; } = icon;
    public string Text { get; } = string.IsNullOrEmpty(text) ? "Yok" : text;
    public string Status => failure ?? "Etkin";
    public Brush StatusBrush => (Brush)Application.Current.FindResource(failure is null ? "TextFillColorTertiaryBrush" : "SystemFillColorCriticalBrush");
}

public partial class SettingsPage : Page
{
    private static readonly (HotkeyAction Action, string Label, SymbolRegular Icon)[] HotkeyActions =
    [
        (HotkeyAction.ToggleDesktop, "Masaüstünü gizle / göster", SymbolRegular.EyeOff24),
        (HotkeyAction.OrganizeNow, "Masaüstünü şimdi düzenle", SymbolRegular.Sparkle24),
        (HotkeyAction.OpenApp, "Düzenleme'yi aç", SymbolRegular.WindowNew24),
        (HotkeyAction.NewNote, "Yeni not", SymbolRegular.NoteAdd24),
        (HotkeyAction.PeekWidgets, "Widget'ları öne getir (5 sn)", SymbolRegular.Eye24),
        (HotkeyAction.QuickAdd, "Widget ekle penceresi", SymbolRegular.Add24),
    ];

    private bool _loading;

    private Window? _host;

    // Store sürümü: başlangıç görevi isteklerinin sırası ve bir değişikliğin sürüp sürmediği.
    private int _startupQuery;
    private bool _startupChanging;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
        Unloaded += (_, _) => WatchHostActivation(false);
    }

    private void Load()
    {
        _loading = true;
        var s = AppHost.Settings;
        ThemeBox.SelectedIndex = s.Theme switch { AppTheme.Dark => 1, AppTheme.Light => 2, _ => 0 };
        LoadStartup();
        NotifyToggle.IsChecked = s.ShowNotifications;
        DoubleClickToggle.IsChecked = s.DoubleClickHidesDesktop;
        HideWidgetsToggle.IsChecked = s.HideWidgetsWithIcons;
        DesktopPath.Text = AppHost.DesktopDirectory;
        LoadDataFolder();
        VersionText.Text = "Düzenleme " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        LoadHotkeys();
        UpdateKeyStatus();
        _loading = false;
    }

    private void LoadStartup()
    {
        if (!PackageInfo.IsPackaged)
        {
            StartupToggle.IsChecked = StartupRegistration.IsEnabled;
            return;
        }
        // Store sürümü: manifestteki başlangıç görevi. Durum gelene dek anahtar kapalı ve dokunulmaz.
        StartupToggle.IsEnabled = false;
        UpdateStartupTask(enable: null);
        // Kullanıcı Ayarlar'dan ya da Görev Yöneticisi'nden dönünce durum yenilensin.
        WatchHostActivation(true);
    }

    /// <summary>
    /// Store sürümü: görevin durumunu okur (enable null) ya da değiştirir, sonra kartı gösterir. Yalnızca son isteğin
    /// sonucu gösterilir; değişiklik sürerken pencere etkinleşmesiyle gelen okuma eski durumu göstermesin diye atlanır.
    /// </summary>
    private async void UpdateStartupTask(bool? enable)
    {
        if (enable is null && _startupChanging) return;
        var query = ++_startupQuery;
        if (enable is not null)
        {
            _startupChanging = true;
            StartupToggle.IsEnabled = false;
        }
        try
        {
            var state = enable is { } value
                ? await StartupRegistration.SetTaskEnabledAsync(value)
                : await StartupRegistration.GetTaskStateAsync();
            if (query == _startupQuery) ShowStartupTask(state);
        }
        finally
        {
            if (enable is not null) _startupChanging = false;
        }
    }

    private void ShowStartupTask(StartupTaskState? state)
    {
        var view = PackagedApp.DescribeStartupTask(state);
        StartupToggle.IsChecked = view.IsOn;
        StartupToggle.IsEnabled = true;
        StartupToggle.Visibility = view.ShowToggle ? Visibility.Visible : Visibility.Collapsed;
        StartupSettingsButton.Visibility = view.ShowToggle ? Visibility.Collapsed : Visibility.Visible;
        StartupState.Text = view.Note ?? "";
        StartupState.Visibility = view.Note is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void WatchHostActivation(bool watch)
    {
        if (_host is not null) _host.Activated -= Host_Activated;
        _host = watch ? Window.GetWindow(this) : null;
        if (_host is not null) _host.Activated += Host_Activated;
    }

    private void Host_Activated(object? sender, EventArgs e) => UpdateStartupTask(enable: null);

    private void LoadDataFolder()
    {
        // Store sürümünde ayarlar Gezgin'in gördüğü pakete özel klasörde durur (bkz. AppHost.DataDirectoryOnDisk).
        var folder = AppHost.DataDirectoryOnDisk;
        DataPath.Text = folder;
        if (PackageInfo.IsPackaged)
        {
            // Taşınabilir mod paketliyken yoktur. Pakete ayrılmış klasör uygulamayla birlikte silinir: kullanıcı bilsin.
            var own = !string.Equals(folder, AppHost.DataDirectory, StringComparison.OrdinalIgnoreCase);
            PortableText.Text = "Microsoft Store sürümüne ayrılmış klasör; uygulama kaldırılınca Windows içindekileri de siler.";
            PortableText.Visibility = own ? Visibility.Visible : Visibility.Collapsed;
            return;
        }
        PortableText.Text = AppHost.IsPortable
            ? "Taşınabilir mod açık: ayarlar exe'nin yanındaki data klasöründe."
            : AppHost.PortableFallback
                ? "portable.txt var ama programın klasörüne yazılamıyor; ayarlar şimdilik %AppData%\\Duzenleme'de. Programı yazılabilir bir klasöre taşı."
                : "Taşınabilir kullanım için exe'nin yanına boş bir portable.txt dosyası koy.";
    }

    private void LoadHotkeys()
    {
        var failures = AppHost.Hotkeys?.Failures ?? [];
        HotkeyList.ItemsSource = HotkeyActions
            .Select(a => new HotkeyRow(a.Action, a.Label, a.Icon, AppHost.Settings.Hotkeys.Get(a.Action), failures.GetValueOrDefault(a.Action)))
            .ToList();
    }

    private static void ApplyHotkeys()
    {
        AppHost.SaveSettings();
        AppHost.Hotkeys?.Apply(AppHost.Settings.Hotkeys);
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not HotkeyRow row) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Back or Key.Delete)
        {
            AppHost.Settings.Hotkeys.Set(row.Action, "");
            ApplyHotkeys();
            LoadHotkeys();
            return;
        }
        // Yalnızca değiştirici tuşa basıldıysa birleşimin tamamlanmasını bekle.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.Tab or Key.Escape) return;

        var mods = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Ctrl;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mods |= HotkeyModifiers.Win;
        if (mods == HotkeyModifiers.None) return;

        var keyName = key is >= Key.D0 and <= Key.D9 ? ((int)(key - Key.D0)).ToString() : key.ToString();
        AppHost.Settings.Hotkeys.Set(row.Action, new Hotkey(mods, keyName).ToString());
        ApplyHotkeys();
        LoadHotkeys();
    }

    private void ResetHotkeys_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.Hotkeys = new HotkeySettings();
        ApplyHotkeys();
        LoadHotkeys();
    }

    private void UpdateKeyStatus() =>
        KeyStatus.Text = Icons.ApiKeyStore.HasKey ? $"Kayıtlı ({Icons.ApiKeyStore.Masked()})" : "Eklenmedi";

    private async void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        var key = KeyBox.Password.Trim();
        if (key.Length == 0)
        {
            ShowKey("Anahtar boş", "Önce anahtarı yapıştır.", InfoBarSeverity.Warning);
            return;
        }
        SaveKeyButton.IsEnabled = false;
        try
        {
            var error = await Icons.AiIconGenerator.ValidateKeyAsync(key);
            if (error is not null)
            {
                ShowKey("Kaydedilmedi", error, InfoBarSeverity.Error);
                return;
            }
            Icons.ApiKeyStore.Save(key);
            KeyBox.Password = "";
            UpdateKeyStatus();
            ShowKey("Kaydedildi", "Anahtar doğrulandı. Klasör simgesi penceresinde \"Üret\" artık kullanılabilir.", InfoBarSeverity.Success);
        }
        finally
        {
            SaveKeyButton.IsEnabled = true;
        }
    }

    private void DeleteKey_Click(object sender, RoutedEventArgs e)
    {
        Icons.ApiKeyStore.Save(null);
        UpdateKeyStatus();
        ShowKey("Silindi", "API anahtarı ayarlardan kaldırıldı. Eski ayar yedeklerinde (yedekler klasörü) şifreli kopyası kalabilir.", InfoBarSeverity.Informational);
    }

    private void ShowKey(string title, string message, InfoBarSeverity severity)
    {
        KeyFeedback.Title = title;
        KeyFeedback.Message = message;
        KeyFeedback.Severity = severity;
        KeyFeedback.IsOpen = true;
    }

    private void Link_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void DoubleClickToggle_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.DoubleClickHidesDesktop = DoubleClickToggle.IsChecked == true;
        AppHost.SaveSettings();
        AppHost.ApplyDoubleClickSetting();
    }

    private void HideWidgetsToggle_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.HideWidgetsWithIcons = HideWidgetsToggle.IsChecked == true;
        AppHost.SaveSettings();
        if (AppHost.DesktopHidden) AppHost.SetDesktopHidden(true);
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        AppHost.Settings.Theme = ThemeBox.SelectedIndex switch { 1 => AppTheme.Dark, 2 => AppTheme.Light, _ => AppTheme.System };
        AppHost.SaveSettings();
        App.ApplyTheme(AppHost.Settings.Theme);
    }

    private void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        if (PackageInfo.IsPackaged) UpdateStartupTask(StartupToggle.IsChecked == true);
        else StartupRegistration.Set(StartupToggle.IsChecked == true);
    }

    private void OpenStartupSettings_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(StartupRegistration.StartupAppsSettingsUri) { UseShellExecute = true });

    private void NotifyToggle_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.ShowNotifications = NotifyToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    private void OpenDesktop_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppHost.DesktopDirectory) { UseShellExecute = true });

    private void OpenData_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppHost.DataDirectoryOnDisk) { UseShellExecute = true });

    private void Exit_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).ExitApp();
}
