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

    /// <summary>Simgesi arka planda, ekranın piksel boyutunda yüklenir (ShellIconImage).</summary>
    public string IconPath => "::" + Item.Clsid;
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
            // Bu simgeleri gösteren bölmeler (Kısayollar, Tümü) güncellensin; masaüstü klasörü değişmediği için kendileri fark etmez.
            AppHost.Widgets.RefreshSystemIcons();
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

    /// <summary>Simgesi arka planda, ekranın piksel boyutunda yüklenir (ShellIconImage); simge değişince sürüm artar.</summary>
    public string IconPath => Path;
    public int IconVersion { get; private set; }
    public Visibility CustomVisibility { get; private set; } = Visibility.Collapsed;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>"Özel simge" durumunu yeniden okur ve simgeyi yeniden istetir (ağ/OneDrive yolunda yavaş olabilir: arka planda).</summary>
    public void Reload()
    {
        CustomVisibility = FolderIconService.HasCustomIcon(Path) ? Visibility.Visible : Visibility.Collapsed;
        Raise(nameof(CustomVisibility));
        IconVersion++;
        Raise(nameof(IconVersion));
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
        (HotkeyAction.PeekDesktop, "Windows masaüstüne göz at", SymbolRegular.Glance24),
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
        RemoveIconsText.Text = $"Masaüstündeki klasörlere {AppInfo.Name}'in verdiği simgeleri kaldırır. {AppInfo.Name}'i kaldırmadan " +
                               "önce kullanabilirsin: simgeler klasörlerin içinde durduğu için kendiliğinden silinmez.";
        LoadAbout();

        Fold.Attach(SystemIconsFold, SystemIconsContent, LoadSystemIcons);
        Fold.Attach(FolderIconsFold, FolderIconsContent, LoadFolderIcons);
        Fold.Attach(HotkeysFold, HotkeysContent);
        Fold.Attach(AiFold, AiContent);
        Fold.Attach(LocationsFold, LocationsContent);

        foreach (var mode in DesktopModes.Choices)
            IconModeBox.Items.Add(new ComboBoxItem { Content = DesktopModes.Label(mode), Tag = mode });
        foreach (var mode in PlaceModes.Choices)
            PlacementBox.Items.Add(new ComboBoxItem { Content = DesktopModes.PlaceLabel(mode), Tag = mode });

        Loaded += (_, _) => Load();
        Unloaded += (_, _) => WatchHostActivation(false);
        // Ana pencere gizliyken sayfa kayıtlara tepki vermez; yeniden görününce bir kez güncellenir.
        PageLife.WhileShown(this,
            attach: () =>
            {
                AppHost.DesktopVisibilityChanged += OnDesktopStateChanged;
                AppHost.SettingsChanged += OnSettingsChanged;
                BoxMover.Changed += UpdateBoxPanel;
                FolderIconWindow.IconChanged += OnFolderIconChanged;
            },
            detach: () =>
            {
                AppHost.DesktopVisibilityChanged -= OnDesktopStateChanged;
                AppHost.SettingsChanged -= OnSettingsChanged;
                BoxMover.Changed -= UpdateBoxPanel;
                FolderIconWindow.IconChanged -= OnFolderIconChanged;
            },
            refresh: Load);
    }

    private void OnDesktopStateChanged()
    {
        UpdateHideNow();
        UpdatePeek();
    }

    /// <summary>Ayarlar başka yerden (tepsi, widget menüsü, Widget'lar sayfası) değişince masaüstü satırları eşitlenir.</summary>
    private void OnSettingsChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(OnSettingsChanged);
            return;
        }
        UpdateFencesNote();
        LoadDesktopChoices();
    }

    private void Load()
    {
        _loading = true;
        var s = AppHost.Settings;
        LoadLanguage();
        ThemeBox.SelectedIndex = s.Theme switch { AppTheme.Dark => 1, AppTheme.Light => 2, _ => 0 };
        NotifyToggle.IsChecked = s.ShowNotifications;
        HideWidgetsToggle.IsChecked = s.HideWidgetsWithIcons;
        SuggestToggle.IsChecked = s.SuggestFolderIcons;
        DesktopPath.Text = AppHost.DesktopDirectory;
        LoadDataFolder();
        LoadHotkeys();
        UpdateKeyStatus();
        UpdateHideNow();
        UpdatePeek();
        UpdateFencesNote();
        LoadDesktopChoices();
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
        var loading = _loading;
        _loading = true; // koddan gösterilen durum görevi yeniden değiştirmesin (StartupToggle_Changed)
        try { StartupToggle.IsChecked = view.IsOn; }
        finally { _loading = loading; }
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
        // Kullanıcı Windows'un "Masaüstü simgesi ayarları"ndan dönmüş olabilir.
        if (_systemIcons.Count > 0) AppHost.Widgets.RefreshSystemIcons();
    }

    // Anahtarlar Click değil Checked/Unchecked dinler: ekran okuyucunun "Aç/Kapat"ı (UI Automation Toggle) Click olayını
    // tetiklemez, ayar değişmezdi. Koddan yapılan atamalar _loading / _loadingDesktop ile ayrılır.
    private void StartupToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (PackageInfo.IsPackaged) UpdateStartupTask(StartupToggle.IsChecked == true);
        else StartupRegistration.Set(StartupToggle.IsChecked == true);
    }

    private void OpenStartupSettings_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(StartupRegistration.StartupAppsSettingsUri) { UseShellExecute = true });

    private void NotifyToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
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

    // Kutular koddan güncellenirken SelectionChanged ayarı yeniden yazmasın.
    private bool _loadingDesktop;

    /// <summary>Simge kipi, çift tıklama, göz atma ve yeni widget yeri kutularını ayarlardan doldurur.</summary>
    private void LoadDesktopChoices()
    {
        _loadingDesktop = true;
        try
        {
            var s = AppHost.Settings;
            var mode = DesktopModes.Current;
            IconModeBox.SelectedIndex = Array.IndexOf(DesktopModes.Choices, mode);
            IconModeText.Text = DesktopModes.Description(mode);
            PlacementBox.SelectedIndex = Array.IndexOf(PlaceModes.Choices, PlaceModes.Parse(s.NewWidgetPlacement));
            var doubleClick = DesktopState.DoubleClickChoice(s.DoubleClickAction, s.DoubleClickHidesDesktop);
            DoubleClickBox.SelectedIndex = Array.IndexOf(DesktopState.DoubleClickChoices, doubleClick);
            DoubleClickText.Text = doubleClick switch
            {
                DesktopState.DoubleClickToggle => "Simgeler (ve ayara göre widget'lar) gizlenir; yeniden çift tıklayınca geri gelir.",
                DesktopState.DoubleClickPeek => "Windows'un simgeleri görünür, widget'lar kısa süre çekilir; yeniden çift tıklayınca dönülür.",
                DesktopState.DoubleClickNone => "Çift tıklama yalnızca Windows'un kendi işini yapar.",
                _ => "Bölmeler masaüstünü yönetirken Windows masaüstüne göz atar, yoksa masaüstünü gizler/gösterir. Simgeye çift tıklamak yine dosyayı açar.",
            };
            PeekHidesWidgetsToggle.IsChecked = s.PeekHidesWidgets;
            PeekShowsDesktopToggle.IsChecked = s.PeekShowsDesktop;
            // Test örneği gerçek pencereleri küçültmez (AppHost.StartPeek); anahtar orada salt okunur.
            PeekShowsDesktopToggle.IsEnabled = !AppHost.IsTestDesktop;
            PeekMinutesBox.SelectedIndex = Array.IndexOf(DesktopState.PeekMinuteChoices, DesktopState.NormalizePeekMinutes(s.PeekMinutes));
            PublicBoxToggle.IsChecked = s.BoxIncludesPublicDesktop;
            UpdateBoxPanel();
        }
        finally
        {
            _loadingDesktop = false;
        }
    }

    private void IconModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingDesktop || IconModeBox.SelectedItem is not ComboBoxItem { Tag: IconMode mode }) return;
        DesktopModes.Set(mode, WindowCenter(), Window.GetWindow(this));
        // Soru penceresinden sonra ya da ayar değişmediyse de kutu gerçek durumu göstersin.
        LoadDesktopChoices();
    }

    /// <summary>Kutulara taşınan öğeler: sayı ve klasör. Kip açıkken ya da taşınmış öğe kaldıysa görünür.</summary>
    private void UpdateBoxPanel()
    {
        var moved = BoxMover.MovedCount;
        var active = DesktopModes.Current == IconMode.BoxItemsLeave;
        BoxPanel.Visibility = active || moved > 0 ? Visibility.Visible : Visibility.Collapsed;
        BoxCountText.Text = moved == 0
            ? $"Henüz kutuya taşınan öğe yok. Taşınanlar {BoxMover.Root} klasöründe durur."
            : $"{moved} öğe kutularda; dosyalar {BoxMover.Root} klasöründe.";
        ReturnAllButton.IsEnabled = moved > 0;
        OpenBoxFolderButton.IsEnabled = moved > 0;
        PublicBoxRow.Visibility = active && !AppHost.IsTestDesktop ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenBoxFolder_Click(object sender, RoutedEventArgs e) => TileItem.Launch(BoxMover.Root);

    private void ReturnAll_Click(object sender, RoutedEventArgs e)
    {
        var moved = BoxMover.MovedCount;
        if (moved == 0) return;
        if (Confirm.Ask(Window.GetWindow(this), "Kutulardaki öğeler masaüstüne geri konsun mu?",
                $"{moved} öğe masaüstüne döner ve kutularda kalır.", "Masaüstüne geri koy", danger: false))
            BoxMover.ReturnAll();
    }

    private void PublicBoxToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingDesktop) return;
        AppHost.Settings.BoxIncludesPublicDesktop = PublicBoxToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    /// <summary>"Windows masaüstüne göz at" satırı şu anki durumu ve kısayolu gösterir.</summary>
    private void UpdatePeek()
    {
        var peeking = AppHost.Peeking;
        PeekTitle.Text = peeking ? "Windows masaüstüne göz atılıyor" : "Windows masaüstüne göz at";
        PeekButton.Content = peeking ? $"{AppInfo.Name}'e dön" : "Göz at";
        System.Windows.Automation.AutomationProperties.SetName(PeekButton, peeking ? $"{AppInfo.Name}'e dön" : "Windows masaüstüne göz at");
        var shortcut = AppHost.Settings.Hotkeys.PeekDesktop;
        PeekText.Text = "Windows'un masaüstü simgeleri görünür, widget'lar kısa süre çekilir; ekranın üstündeki çubuktan dönülür. " +
                        (string.IsNullOrWhiteSpace(shortcut) ? "Kısayol atanmamış." : $"Kısayol: {shortcut}");
    }

    private void Peek_Click(object sender, RoutedEventArgs e) => AppHost.TogglePeek(AppHost.PeekOrigin.Settings);

    private void PeekHidesWidgets_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingDesktop) return;
        AppHost.Settings.PeekHidesWidgets = PeekHidesWidgetsToggle.IsChecked == true;
        AppHost.SaveSettings();
        if (AppHost.Peeking) AppHost.ApplyDesktopState();
    }

    private void PeekShowsDesktop_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingDesktop) return;
        AppHost.Settings.PeekShowsDesktop = PeekShowsDesktopToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    private void PeekMinutesBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingDesktop || PeekMinutesBox.SelectedIndex < 0) return;
        AppHost.Settings.PeekMinutes = DesktopState.PeekMinuteChoices[PeekMinutesBox.SelectedIndex];
        AppHost.SaveSettings();
    }

    private void DoubleClickBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingDesktop || DoubleClickBox.SelectedIndex < 0) return;
        AppHost.SetDoubleClickAction(DesktopState.DoubleClickChoices[DoubleClickBox.SelectedIndex]);
    }

    private void PlacementBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingDesktop || PlacementBox.SelectedItem is not ComboBoxItem { Tag: PlaceMode mode }) return;
        DesktopModes.SetPlacement(mode);
    }

    /// <summary>Ana pencerenin ortası: eklenecek bölmeler bu pencerenin ekranına yerleşir.</summary>
    private NativeMethods.POINT? WindowCenter()
    {
        if (Window.GetWindow(this) is not { } window) return null;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        return hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(hwnd, out var r)
            ? new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 }
            : null;
    }

    private void HideWidgetsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
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

    private void SuggestToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppHost.Settings.SuggestFolderIcons = SuggestToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    /// <summary>Masaüstündeki klasörler: diskten değil, arka planda güncel tutulan anlık görüntüden.</summary>
    private static List<string> DesktopFolders() => AppHost.DesktopFolders();

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
        UpdatePeek();
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
        Browser.Open(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    // ---- Gelişmiş: klasör simgelerinin hepsini kaldır ----

    /// <summary>
    /// Store sürümünde kaldırma programı olmadığı için (Store 10.2.7) klasör simgelerini toplu kaldırmanın uygulama içi yolu.
    /// Diske dokunan iş arka planda; bitince sonuç şeritte.
    /// </summary>
    private async void RemoveAllFolderIcons_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm.Ask(Window.GetWindow(this), "Klasör simgelerinin hepsi kaldırılsın mı?",
                $"{AppInfo.Name}'in verdiği bütün klasör simgeleri silinir ve klasörler Windows'un varsayılan simgesine döner. " +
                "Klasörlere ve içlerindeki dosyalara dokunulmaz. Simgeleri yeniden vermek istersen \"Klasör simgeleri\" bölümünü kullan.",
                "Hepsini kaldır"))
            return;
        RemoveIconsButton.IsEnabled = false;
        try
        {
            var roots = AppHost.DesktopDirectories.ToList();
            // Klasör portalları masaüstü dışındaki klasörleri gösterir: onların (ve bir alt düzeyin) simgeleri de kaldırılır.
            var portals = AppHost.Settings.Widgets.Where(WidgetVariants.IsPortal).Select(w => w.FolderName!).ToList();
            var (removed, failed) = await Task.Run(() =>
            {
                var result = FolderIconService.RemoveAllOwnIcons(roots);
                if (portals.Count == 0) return result;
                var extra = FolderIconService.RemoveAllOwnIcons(portals, depth: 1, includeRoots: true);
                return (result.Removed.Concat(extra.Removed).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), result.Failed + extra.Failed);
            });
            removed.ForEach(ShellIcons.Forget);
            if (_folderIcons is not null) LoadFolderIcons();
            var text = removed.Count > 0
                ? $"{removed.Count} klasörün simgesi kaldırıldı; masaüstü birkaç saniye içinde yenilenir."
                : $"Kaldırılacak simge yok: {AppInfo.Name}'in verdiği bir klasör simgesi bulunamadı.";
            if (failed > 0) text += $" {failed} klasöre erişilemediği için dokunulamadı.";
            Notice.Show(text, failed > 0 ? NoticeKind.Warning : removed.Count > 0 ? NoticeKind.Success : NoticeKind.Info);
        }
        finally
        {
            RemoveIconsButton.IsEnabled = true;
        }
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
                ? "portable.txt var ama programın klasörüne yazılamıyor; ayarlar şimdilik yukarıdaki klasörde. Programı yazılabilir bir klasöre taşı."
                : AppHost.DataFolderSource == DataFolderSource.MoveFailed
                    ? $"Klasör o sırada kullanımda olduğu için {AppInfo.DataFolderName} adıyla yeniden adlandırılamadı; bir sonraki açılışta yeniden denenecek."
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
        VersionText.Text = $"{AppInfo.Name} {AppInfo.Version}";
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

    private void Releases_Click(object sender, RoutedEventArgs e) => Browser.Open(AppInfo.ReleasesUrl);

    /// <summary>Gizlilik politikası (Store 10.5.1: uygulama içinden erişilebilir olmalı); Hakkında ve yapay zekâ bölümünde.</summary>
    private void Privacy_Click(object sender, RoutedEventArgs e) => Browser.Open(AppInfo.PrivacyUrl);

    private void Exit_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).ExitApp();
}
