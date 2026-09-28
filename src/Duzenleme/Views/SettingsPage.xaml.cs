using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Icons;
using Duzenleme.Widgets;
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

/// <summary>Windows'un bir masaüstü simgesi (Bu Bilgisayar, Geri Dönüşüm Kutusu…): açık/kapalı durumu kayıt defterinden okunur.</summary>
public sealed class SystemIconRow(SystemIcon icon) : INotifyPropertyChanged
{
    public SystemIcon Item { get; } = icon;
    public string Name => Item.Name;
    public string Description => Item.Description;
    public ImageSource? Icon => ShellIcons.ForShellObject("::" + Item.Clsid);
    public string OpenName => $"Aç: {Item.Name}";
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Store sürümünde anahtar yalnızca durumu gösterir (bkz. DesktopSystemIcons.CanChange). Test örneği (--desktop)
    /// de kullanıcının gerçek masaüstü ayarına dokunmaz.
    /// </summary>
    public bool CanChange => DesktopSystemIcons.CanChange && !AppHost.IsTestDesktop;

    public bool Shown
    {
        get => DesktopSystemIcons.IsShown(Item);
        set
        {
            if (CanChange) DesktopSystemIcons.SetShown(Item, value);
            Reload();
        }
    }

    /// <summary>Durumu yeniden okur (ör. kullanıcı Windows ayarından dönünce).</summary>
    public void Reload() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shown)));
}

/// <summary>Masaüstündeki bir klasör ve simgesi ("Bir klasörün simgesini değiştir" listesi).</summary>
public sealed class FolderIconRow : INotifyPropertyChanged
{
    public FolderIconRow(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        Reload();
    }

    public string Path { get; }
    public string Name { get; }
    public string ChooseName => $"Simge seç: {Name}";
    public ImageSource? Icon { get; private set; }
    public Visibility CustomVisibility { get; private set; } = Visibility.Collapsed;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>"Özel simge" durumunu yeniden okur; simge arka planda yüklenir (ağ/OneDrive yolunda yavaş olabilir).</summary>
    public void Reload()
    {
        CustomVisibility = FolderIconService.HasCustomIcon(Path) ? Visibility.Visible : Visibility.Collapsed;
        Raise(nameof(CustomVisibility));
        ShellIcons.Request(Path, 0, false, icon =>
        {
            Icon = icon;
            Raise(nameof(Icon));
        });
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Ayarlar: Genel, Masaüstü (eski Masaüstü sayfasının ayarları), Klasör simgeleri, Gelişmiş (katlanır), Yardım, Hakkında.
/// Katlanır bölümlerin içeriği ilk açılışta yüklenir.
/// </summary>
public partial class SettingsPage : Page
{
    private static readonly (HotkeyAction Action, string Label, SymbolRegular Icon)[] HotkeyActions =
    [
        (HotkeyAction.ToggleDesktop, "Masaüstünü gizle / göster", SymbolRegular.EyeOff24),
        (HotkeyAction.OrganizeNow, "Masaüstünü şimdi düzenle", SymbolRegular.Sparkle24),
        (HotkeyAction.OpenApp, $"{AppInfo.Name}'i aç", SymbolRegular.WindowNew24),
        (HotkeyAction.NewNote, "Yeni not", SymbolRegular.NoteAdd24),
        (HotkeyAction.PeekWidgets, "Widget'ları öne getir (5 sn)", SymbolRegular.Eye24),
        (HotkeyAction.QuickAdd, "Widget ekle penceresi", SymbolRegular.Add24),
    ];

    private bool _loading;

    private Window? _host;

    // Store sürümü: başlangıç görevi isteklerinin sırası ve bir değişikliğin sürüp sürmediği.
    private int _startupQuery;
    private bool _startupChanging;

    // Katlanır bölümler açılınca dolar.
    private List<SystemIconRow> _systemIcons = [];
    private List<FolderIconRow>? _folderIcons;

    public SettingsPage()
    {
        InitializeComponent();
        Subtitle.Text = $"{AppInfo.Name}'in nasıl çalışacağını seç.";
        AiText.Text = "Anahtarı girersen klasör simgesi penceresinde yazdığın açıklamaya göre yeni simge üretebilirsin. Anahtar yalnızca " +
                      "bu bilgisayarda, Windows hesabına bağlı şifrelenerek saklanır. Üretim başına küçük bir API ücreti (birkaç sent) " +
                      "Anthropic hesabına yansır. Anahtar zorunlu değil; hazır simgeler anahtarsız çalışır. " +
                      $"{AppInfo.Name} internete yalnızca anahtarı doğrularken ve sen \"Üret\"e bastığında Anthropic'e bağlanır; " +
                      "simge için yalnızca klasör adı ve yazdığın açıklama gönderilir.";
        LoadAbout();

        Fold.Attach(SystemIconsFold, SystemIconsContent, LoadSystemIcons);
        Fold.Attach(FolderIconsFold, FolderIconsContent, LoadFolderIcons);
        Fold.Attach(HotkeysFold, HotkeysContent);
        Fold.Attach(AiFold, AiContent);
        Fold.Attach(LocationsFold, LocationsContent);

        Loaded += (_, _) =>
        {
            AppHost.DesktopVisibilityChanged += UpdateHideNow;
            AppHost.SettingsChanged += UpdateFencesNote;
            FolderIconWindow.IconChanged += OnFolderIconChanged;
            Load();
        };
        Unloaded += (_, _) =>
        {
            AppHost.DesktopVisibilityChanged -= UpdateHideNow;
            AppHost.SettingsChanged -= UpdateFencesNote;
            FolderIconWindow.IconChanged -= OnFolderIconChanged;
            WatchHostActivation(false);
        };
    }

    private void Load()
    {
        _loading = true;
        var s = AppHost.Settings;
        LoadLanguage();
        ThemeBox.SelectedIndex = s.Theme switch { AppTheme.Dark => 1, AppTheme.Light => 2, _ => 0 };
        NotifyToggle.IsChecked = s.ShowNotifications;
        DoubleClickToggle.IsChecked = s.DoubleClickHidesDesktop;
        HideWidgetsToggle.IsChecked = s.HideWidgetsWithIcons;
        SuggestToggle.IsChecked = s.SuggestFolderIcons;
        DesktopPath.Text = AppHost.DesktopDirectory;
        LoadDataFolder();
        LoadHotkeys();
        UpdateKeyStatus();
        UpdateHideNow();
        UpdateFencesNote();
        SystemIconsStoreNote.Visibility = DesktopSystemIcons.CanChange ? Visibility.Collapsed : Visibility.Visible;
        SystemIconsTestNote.Visibility = DesktopSystemIcons.CanChange && AppHost.IsTestDesktop ? Visibility.Visible : Visibility.Collapsed;
        // Kullanıcı Windows ayarlarından ya da Görev Yöneticisi'nden dönünce durumlar yenilensin.
        WatchHostActivation(true);
        LoadStartup();
        _loading = false;
    }

    // ---- Genel ----

    /// <summary>Dil kutusunun seçenekleri: AppSettings.Language değerleri (null = Windows ile aynı).</summary>
    private static readonly string?[] LanguageValues = [null, "tr", "en"];

    private void LoadLanguage()
    {
        // Seçenek adları kendi dillerinde (çevrilmez): yanlış dilde kalan kullanıcı da kendi dilini bulsun.
        if (LanguageBox.Items.Count == 0)
            foreach (var name in new[] { L.SystemChoiceName, L.NativeName(Lang.Tr), L.NativeName(Lang.En) })
                LanguageBox.Items.Add(new ComboBoxItem { Content = name });
        var current = AppHost.Settings.Language?.Trim().ToLowerInvariant();
        LanguageBox.SelectedIndex = Math.Max(0, Array.IndexOf(LanguageValues, current));
        UpdateRestart();
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedIndex < 0) return;
        AppHost.Settings.Language = LanguageValues[LanguageBox.SelectedIndex];
        AppHost.SaveSettings();
        UpdateRestart();
    }

    /// <summary>"Şimdi yeniden başlat" yalnızca seçilen dil çalışan dilden farklıysa görünür.</summary>
    private void UpdateRestart() =>
        RestartButton.Visibility = L.NeedsRestart(AppHost.Settings.Language) ? Visibility.Visible : Visibility.Collapsed;

    private void Restart_Click(object sender, RoutedEventArgs e) => (Application.Current as App)?.Restart();

    private void LoadStartup()
    {
        if (!PackageInfo.IsPackaged)
        {
            StartupToggle.IsChecked = StartupRegistration.IsEnabled;
            StartupToggle.IsEnabled = !AppHost.IsTestDesktop;
            ShowStartupNote(null);
            return;
        }
        // Store sürümü: manifestteki başlangıç görevi. Durum gelene dek anahtar kapalı ve dokunulmaz.
        StartupToggle.IsEnabled = false;
        UpdateStartupTask(enable: null);
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
        StartupToggle.IsEnabled = !AppHost.IsTestDesktop;
        StartupToggle.Visibility = view.ShowToggle ? Visibility.Visible : Visibility.Collapsed;
        StartupSettingsButton.Visibility = view.ShowToggle ? Visibility.Collapsed : Visibility.Visible;
        ShowStartupNote(view.Note);
    }

    /// <summary>Anahtarın altındaki not. Test örneği (--desktop) gerçek başlangıç kaydını değiştirmez; bunu söyler.</summary>
    private void ShowStartupNote(string? note)
    {
        if (AppHost.IsTestDesktop) note = "Test örneğinde değiştirilmez.";
        StartupState.Text = note ?? "";
        StartupState.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void WatchHostActivation(bool watch)
    {
        if (_host is not null) _host.Activated -= Host_Activated;
        _host = watch ? Window.GetWindow(this) : null;
        if (_host is not null) _host.Activated += Host_Activated;
    }

    private void Host_Activated(object? sender, EventArgs e)
    {
        if (PackageInfo.IsPackaged) UpdateStartupTask(enable: null);
        _systemIcons.ForEach(row => row.Reload());
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

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        AppHost.Settings.Theme = ThemeBox.SelectedIndex switch { 1 => AppTheme.Dark, 2 => AppTheme.Light, _ => AppTheme.System };
        AppHost.SaveSettings();
        App.ApplyTheme(AppHost.Settings.Theme);
    }

    // ---- Masaüstü ----

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

    /// <summary>"Masaüstünü şimdi gizle" satırı masaüstünün şu anki durumunu ve atanmış kısayolu gösterir.</summary>
    private void UpdateHideNow()
    {
        var hidden = AppHost.DesktopHidden;
        HideNowIcon.Symbol = hidden ? SymbolRegular.Eye24 : SymbolRegular.EyeOff24;
        HideNowTitle.Text = hidden ? "Masaüstünü göster" : "Masaüstünü şimdi gizle";
        HideNowButton.Content = hidden ? "Göster" : "Gizle";
        System.Windows.Automation.AutomationProperties.SetName(HideNowButton, HideNowTitle.Text);
        var shortcut = AppHost.Settings.Hotkeys.ToggleDesktop;
        HideNowShortcut.Text = string.IsNullOrWhiteSpace(shortcut) ? "Kısayol atanmamış" : $"Kısayol: {shortcut}";
    }

    private void HideNow_Click(object sender, RoutedEventArgs e) => AppHost.ToggleDesktop();

    private void LoadSystemIcons()
    {
        _systemIcons = DesktopSystemIcons.All.Select(i => new SystemIconRow(i)).ToList();
        SystemIconList.ItemsSource = _systemIcons;
    }

    // Bölmeler masaüstünü yönetirken Windows simgeleri gizlidir; açılan simge "Kısayollar" bölmesinde görünür.
    private void UpdateFencesNote() =>
        FencesNote.Visibility = AppHost.Settings.FencesReplaceIcons ? Visibility.Visible : Visibility.Collapsed;

    private void SystemIconOpen_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SystemIconRow row) TileItem.Launch(DesktopSystemIcons.ShellPath(row.Item));
    }

    private void OpenIconSettings_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(DesktopSystemIcons.SettingsUri) { UseShellExecute = true });

    // ---- Klasör simgeleri ----

    private void SuggestToggle_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.SuggestFolderIcons = SuggestToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    private static List<string> DesktopFolders()
    {
        try { return AppHost.Organizer.ExistingFolders().ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    private void BeautifyAll_Click(object sender, RoutedEventArgs e)
    {
        var folders = DesktopFolders();
        if (folders.Count == 0)
        {
            Notice.Show("Masaüstünde klasör yok.", NoticeKind.Info);
            return;
        }
        var done = 0;
        foreach (var folder in folders)
        {
            var path = Path.Combine(AppHost.DesktopDirectory, folder);
            if (FolderIconService.HasCustomIcon(path)) continue;
            var (glyph, color) = FolderIconCatalog.Suggest(folder);
            try
            {
                FolderIconService.Apply(path, FolderIconRenderer.FolderDrawing(color, glyph.Glyph));
                ShellIcons.Forget(path);
                done++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { }
        }
        if (done > 0)
            Notice.Show($"{done} klasöre adına uygun simge verildi. Beğenmediğini \"Bir klasörün simgesini değiştir\"den değiştirebilirsin.");
        else
            Notice.Show("Değişiklik yok: tüm klasörlerin zaten özel simgesi var.", NoticeKind.Info);
        if (_folderIcons is not null) LoadFolderIcons();
    }

    /// <summary>Masaüstündeki klasörler, arayüz dilinin alfabe sırasıyla (katlanır bölüm ilk açıldığında ve simgeler toplu verilince).</summary>
    private void LoadFolderIcons()
    {
        var order = L.Sorter;
        _folderIcons = DesktopFolders().Order(order)
            .Select(name => new FolderIconRow(Path.Combine(AppHost.DesktopDirectory, name)))
            .ToList();
        FolderIconList.ItemsSource = _folderIcons;
        FolderIconsEmpty.Visibility = _folderIcons.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnFolderIconChanged(string folder) => Dispatcher.BeginInvoke(() =>
    {
        ShellIcons.Forget(folder);
        _folderIcons?.FirstOrDefault(r => string.Equals(r.Path, folder, StringComparison.OrdinalIgnoreCase))?.Reload();
    }, DispatcherPriority.Background);

    private void ChooseIcon_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is FolderIconRow row) FolderIconWindow.ShowFor(row.Path);
    }

    // ---- Gelişmiş: kısayollar ----

    private void LoadHotkeys()
    {
        var failures = AppHost.Hotkeys?.Failures ?? [];
        HotkeyList.ItemsSource = HotkeyActions
            .Select(a => new HotkeyRow(a.Action, a.Label, a.Icon, AppHost.Settings.Hotkeys.Get(a.Action), failures.GetValueOrDefault(a.Action)))
            .ToList();
    }

    private void ApplyHotkeys()
    {
        AppHost.SaveSettings();
        AppHost.Hotkeys?.Apply(AppHost.Settings.Hotkeys);
        LoadHotkeys();
        UpdateHideNow();
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
    }

    private void ResetHotkeys_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.Hotkeys = new HotkeySettings();
        ApplyHotkeys();
    }

    // ---- Gelişmiş: yapay zekâ ----

    private void UpdateKeyStatus() =>
        KeyStatus.Text = ApiKeyStore.HasKey ? $"Kayıtlı ({ApiKeyStore.Masked()})" : "Eklenmedi";

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
            var error = await AiIconGenerator.ValidateKeyAsync(key);
            if (error is not null)
            {
                ShowKey("Kaydedilmedi", error, InfoBarSeverity.Error);
                return;
            }
            ApiKeyStore.Save(key);
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
        ApiKeyStore.Save(null);
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

    // ---- Gelişmiş: dosya konumları ----

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
                ? $"portable.txt var ama programın klasörüne yazılamıyor; ayarlar şimdilik %AppData%\\{AppInfo.DataFolderName}'de. Programı yazılabilir bir klasöre taşı."
                : "Taşınabilir kullanım için exe'nin yanına boş bir portable.txt dosyası koy.";
    }

    private void OpenDesktop_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppHost.DesktopDirectory) { UseShellExecute = true });

    private void OpenData_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppHost.DataDirectoryOnDisk) { UseShellExecute = true });

    // ---- Yardım ve Hakkında ----

    private void Welcome_Click(object sender, RoutedEventArgs e) => (Application.Current as App)?.ShowWelcome(rerun: true);

    /// <summary>Sürüm, kanal (Store / taşınabilir / kurulum) ve nasıl güncelleneceği. Uygulama kendisi güncelleme denetlemez.</summary>
    private void LoadAbout()
    {
        VersionText.Text = $"{AppInfo.Name} {AppInfo.Version} · eski adıyla {AppInfo.FormerName}";
        TaglineText.Text = AppInfo.Tagline;
        ChannelText.Text = PackageInfo.IsPackaged
            ? "Microsoft Store sürümü · Güncellemeler Microsoft Store'dan kendiliğinden gelir."
            : AppHost.IsPortable
                ? "Taşınabilir sürüm · Güncellemek için yeni zip'i bu klasörün üzerine çıkar; data klasörün korunur."
                : "Kurulum sürümü · Güncellemek için indirme sayfasından yeni kurulum dosyasını çalıştır; ayarların korunur.";
        PrivacyText.Text = $"{AppInfo.Name} güncelleme denetlemez ve kendiliğinden internete bağlanmaz. Ayarların ve notların yalnızca bu bilgisayarda.";
        // Store sürümünü Store günceller; indirme sayfası yalnızca kurulum/taşınabilir sürüm için.
        ReleasesButton.Visibility = PackageInfo.IsPackaged ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Releases_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppInfo.ReleasesUrl) { UseShellExecute = true });

    private void Exit_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).ExitApp();
}
