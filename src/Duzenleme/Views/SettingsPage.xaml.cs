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
    ];

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
        DoubleClickToggle.IsChecked = s.DoubleClickHidesDesktop;
        HideWidgetsToggle.IsChecked = s.HideWidgetsWithIcons;
        DesktopPath.Text = AppHost.DesktopDirectory;
        DataPath.Text = AppHost.DataDirectory;
        PortableText.Text = AppHost.IsPortable
            ? "Taşınabilir mod açık: ayarlar exe'nin yanındaki data klasöründe."
            : "Taşınabilir kullanım için exe'nin yanına boş bir portable.txt dosyası koy.";
        VersionText.Text = "Düzenleme " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3);
        LoadHotkeys();
        UpdateKeyStatus();
        _loading = false;
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
        ShowKey("Silindi", "API anahtarı bu bilgisayardan kaldırıldı.", InfoBarSeverity.Informational);
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
