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
    public string Text { get; } = string.IsNullOrEmpty(text) ? L.T("Yok") : text;

    /// <summary>Kısayol boşsa "Kapalı" ("Yok"un altında "Etkin" yazıp hâlâ çalışıyormuş gibi göstermesin).</summary>
    public string Status => failure ?? (string.IsNullOrEmpty(text) ? L.T("Kapalı") : L.T("Etkin"));
    public Brush StatusBrush => (Brush)Application.Current.FindResource(failure is null ? "TextFillColorTertiaryBrush" : "SystemFillColorCriticalBrush");
}

/// <summary>"Açılacak halka" seçeneği: halka widget'ının kimliği ve görünen adı (ad TextBlock'ta düz metin olarak çizilir).</summary>
public sealed record RingChoice(string Id, string Name);

/// <summary>Windows'un bir masaüstü simgesi (Bu Bilgisayar, Geri Dönüşüm Kutusu…): açık/kapalı durumu kayıt defterinden okunur.</summary>
public sealed class SystemIconRow(SystemIcon icon) : INotifyPropertyChanged
{
    public SystemIcon Item { get; } = icon;
    public string Name => Item.Name;
    public string Description => Item.Description;

    /// <summary>Simgesi arka planda, ekranın piksel boyutunda yüklenir (ShellIconImage).</summary>
    public string IconPath => "::" + Item.Clsid;
    public string OpenName => L.F("Aç: {0}", Item.Name);
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
    public string ChooseName => L.F("Simge seç: {0}", Name);

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
/// Ayarlar (Kolay NestDesk): önce yedi satır (dil, Windows ile başlat, "Simgeleri bölmelerde topla", halka menü, yardım,
/// hakkında, "Gelişmiş ayarlar ›"); öbür her ayar aynı sayfanın ikinci görünümünde, Gelişmiş ayarlar'da (Masaüstü, Widget
/// yerleşimi, Görünüm, Klavye kısayolları, Klasör simgeleri, Kutular, Sorun giderme). Katlanır bölümlerin içeriği ilk açılışta
/// yüklenir. Sayfaya her gelişte ilk görünüm açılır (<see cref="OpenAdvanced"/> istemedikçe).
/// </summary>
public partial class SettingsPage : Page
{
    // => özelliği: etiketler her okumada arayüz dilinde (static alanda dil o anki hâlinde donardı).
    private static (HotkeyAction Action, string Label, SymbolRegular Icon)[] HotkeyActions =>
    [
        (HotkeyAction.ToggleDesktop, L.T("Masaüstünü gizle / göster"), SymbolRegular.EyeOff24),
        (HotkeyAction.OrganizeNow, L.T("Dosyaları şimdi taşı"), SymbolRegular.Sparkle24),
        (HotkeyAction.OpenApp, L.F("{0}'i aç", AppInfo.Name), SymbolRegular.WindowNew24),
        (HotkeyAction.NewNote, L.T("Yeni not"), SymbolRegular.NoteAdd24),
        (HotkeyAction.PeekWidgets, L.T("Widget'ları öne getir (5 sn)"), SymbolRegular.Eye24),
        (HotkeyAction.QuickAdd, L.T("Widget ekle penceresi"), SymbolRegular.Add24),
        (HotkeyAction.PeekDesktop, L.T("Windows masaüstüne göz at"), SymbolRegular.Glance24),
        (HotkeyAction.Ring, L.T("Halka menüyü imleçte aç"), SymbolRegular.DataPie24),
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
        Subtitle.Text = L.F("{0}'in nasıl çalışacağını seç.", AppInfo.Name);
        AiText.Text = L.F("Anahtarı girersen klasör simgesi penceresinde yazdığın açıklamaya göre yeni simge üretebilirsin. Anahtar yalnızca bu bilgisayarda, Windows hesabına bağlı şifrelenerek saklanır. Üretim başına küçük bir API ücreti (birkaç sent) Anthropic hesabına yansır. Anahtar zorunlu değil; hazır simgeler anahtarsız çalışır. {0} internete yalnızca anahtarı doğrularken ve sen \"Üret\"e bastığında Anthropic'e bağlanır; simge için yalnızca klasör adı ve yazdığın açıklama gönderilir.",
            AppInfo.Name);
        RemoveIconsText.Text = L.F("Masaüstündeki klasörlere {0}'in verdiği simgeleri kaldırır. {0}'i kaldırmadan önce kullanabilirsin: simgeler klasörlerin içinde durduğu için kendiliğinden silinmez.",
            AppInfo.Name);
        // "Kapalı" dürüst söylenir: yalnızca uygulamanın kendi hareketleri durur (WPF-UI denetimlerinin kısa yerleşik geçişleri
        // Motion'a bağlı değildir).
        MotionDescription.Text = L.F("Canlı: widget'lar ve sayfalar hafifçe büyüyerek ya da kayarak açılır. Sakin: yalnızca kısa solmalar. Kapalı: {0}'in kendi hareketleri oynamaz. Windows'un animasyon ayarına her zaman uyar.",
            AppInfo.Name);
        RingExplain.Text = L.F("Kısayola basınca halka imlecin yanında açılır. Tıkla ya da 1–9'a bas; Esc kapatır. {0} klavyeni izlemez: yalnızca bu kısayol Windows'a kaydedilir. Kilit ekranında ve yönetici onayı ekranında açılmaz.",
            AppInfo.Name);
        ReportProblemText.Text = L.F("Bir şey ters gittiyse tanılama paketini oluşturup sorun kaydına ekleyebilirsin. {0} hiçbir şeyi kendiliğinden göndermez.",
            AppInfo.Name);
        LoadAbout();

        Fold.Attach(SystemIconsFold, SystemIconsContent, LoadSystemIcons);
        Fold.Attach(FolderIconsFold, FolderIconsContent, LoadFolderIcons);
        Fold.Attach(HotkeysFold, HotkeysContent);
        Fold.Attach(RingFold, RingContent);
        Fold.Attach(AiFold, AiContent);
        Fold.Attach(LocationsFold, LocationsContent);
        Fold.Attach(ErrorLogFold, ErrorLogContent, LoadErrorLog);

        foreach (var mode in PlaceModes.Choices)
            PlacementBox.Items.Add(new ComboBoxItem { Content = DesktopModes.PlaceLabel(mode), Tag = mode });

        Loaded += (_, _) =>
        {
            _current = this;
            // Sayfaya her gelişte ilk görünüm; bildirimden "Gelişmiş ayarlar" istendiyse orası.
            ShowView(_advancedRequested, focus: false);
            _advancedRequested = false;
            Load();
        };
        Unloaded += (_, _) =>
        {
            if (_current == this) _current = null;
            WatchHostActivation(false);
        };
        // Ana pencere gizliyken sayfa kayıtlara tepki vermez; yeniden görününce bir kez güncellenir.
        PageLife.WhileShown(this,
            attach: () =>
            {
                AppHost.DesktopVisibilityChanged += OnDesktopStateChanged;
                AppHost.SettingsChanged += OnSettingsChanged;
                BoxMover.Changed += UpdateBoxPanel;
                FolderIconWindow.IconChanged += OnFolderIconChanged;
                Motion.Changed += UpdateAnimationsNote;
                Diagnostics.Changed += OnDiagnosticsChanged;
            },
            detach: () =>
            {
                AppHost.DesktopVisibilityChanged -= OnDesktopStateChanged;
                AppHost.SettingsChanged -= OnSettingsChanged;
                BoxMover.Changed -= UpdateBoxPanel;
                FolderIconWindow.IconChanged -= OnFolderIconChanged;
                Motion.Changed -= UpdateAnimationsNote;
                Diagnostics.Changed -= OnDiagnosticsChanged;
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
        // Halka widget'ı eklendi/kaldırıldı ya da adı değişti; kısayol widget menüsünden açıldı/kapatıldı.
        var loading = _loading;
        _loading = true;
        try
        {
            LoadRing();
            // Liste yalnızca halka kısayolu başka yerden değiştiyse yenilenir (kutuya yazarken odak kaybolmasın).
            if (!string.Equals(AppHost.Settings.Hotkeys.Ring, _listedRing, StringComparison.Ordinal)) LoadHotkeys();
        }
        finally { _loading = loading; }
    }

    private void Load()
    {
        _loading = true;
        // Bir adım hata verse de bayrak kalmasın: kalırsa sayfadaki anahtarlar sessizce hiçbir şey yapmaz.
        try
        {
            var s = AppHost.Settings;
            LoadLanguage();
            ThemeBox.SelectedIndex = s.Theme switch { AppTheme.Dark => 1, AppTheme.Light => 2, _ => 0 };
            LoadMotion();
            NotifyToggle.IsChecked = s.ShowNotifications;
            HideWidgetsToggle.IsChecked = s.HideWidgetsWithIcons;
            SnapToggle.IsChecked = s.SnapWidgets;
            OverlapToggle.IsChecked = s.PreventOverlap;
            SuggestToggle.IsChecked = s.SuggestFolderIcons;
            DesktopPath.Text = AppHost.DesktopDirectory;
            LoadDataFolder();
            LoadHotkeys();
            LoadRing();
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
            if (_errorLogLoaded) LoadErrorLog();
        }
        finally { _loading = false; }
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
            ShowRunValueState();
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

    /// <summary>Paketsiz sürüm: Run kaydının (ve Görev Yöneticisi seçiminin) durumu; koddan gösterilen durum kaydı değiştirmez.</summary>
    private void ShowRunValueState()
    {
        var loading = _loading;
        _loading = true;
        try { StartupToggle.IsChecked = StartupRegistration.IsEnabled; }
        finally { _loading = loading; }
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
        if (AppHost.IsTestDesktop) note = L.T("Test örneğinde değiştirilmez.");
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
        // "Windows ile başlat": Görev Yöneticisi'nden, Windows ayarından ya da karşılamadan değişmiş olabilir.
        if (PackageInfo.IsPackaged) UpdateStartupTask(enable: null);
        else ShowRunValueState();
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

    /// <summary>Animasyonlar: Canlı / Sakin / Kapalı (AnimationsOff baskın, sonra CalmMotion; bkz. MotionPolicy.Chosen).</summary>
    private void LoadMotion()
    {
        var s = AppHost.Settings;
        var button = MotionPolicy.Chosen(s.AnimationsOff, s.CalmMotion) switch
        {
            MotionLevel.Off => MotionOff,
            MotionLevel.Calm => MotionCalm,
            _ => MotionLively,
        };
        button.IsChecked = true;
        UpdateAnimationsNote();
    }

    private void MotionChoice_Checked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var choice = sender == MotionOff ? MotionLevel.Off : sender == MotionCalm ? MotionLevel.Calm : MotionLevel.Lively;
        var s = AppHost.Settings;
        var (off, calm) = MotionPolicy.Flags(choice, s.CalmMotion);
        if (off == s.AnimationsOff && calm == s.CalmMotion) return;
        s.AnimationsOff = off;
        s.CalmMotion = calm;
        AppHost.SaveSettings();
        Motion.Refresh();
        UpdateAnimationsNote();
        // Örnek: Canlı'da satırın simgesi bir kez nabız atar (Sakin ve Kapalı'da nabız yok).
        Motion.Attention(MotionIcon);
    }

    /// <summary>
    /// Windows'taki seçim düğmesi grupları gibi: grubun içinde oklarla gelinen bölüm seçilir (Sekme tuşu grupta bir kez durur,
    /// en son odaklanan bölümde; fareyle ve Sekme'yle gelinen odak seçimi değiştirmez).
    /// </summary>
    private void MotionChoice_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var fromSibling = ReferenceEquals(e.OldFocus, MotionLively) || ReferenceEquals(e.OldFocus, MotionCalm) || ReferenceEquals(e.OldFocus, MotionOff);
        var arrows = Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down);
        if (!_loading && fromSibling && arrows && ReferenceEquals(sender, e.NewFocus) && sender is System.Windows.Controls.Primitives.ToggleButton { IsChecked: not true } segment)
            segment.IsChecked = true;
    }

    /// <summary>Seçim neden şu an oynamıyor ya da sakin oynuyor: Windows'un ayarı (bağlantısıyla), enerji tasarrufu, uzak masaüstü.</summary>
    private void UpdateAnimationsNote()
    {
        var s = AppHost.Settings;
        var choice = MotionPolicy.Chosen(s.AnimationsOff, s.CalmMotion);
        var windowsOff = choice != MotionLevel.Off && !Motion.WindowsAllows;
        string? note = null;
        if (windowsOff) note = L.F("Windows'ta animasyon efektleri kapalı olduğu için şu an {0}'in kendi hareketleri oynamıyor.", AppInfo.Name);
        else if (choice == MotionLevel.Lively && Motion.EnergySaver) note = L.T("Enerji tasarrufu açıkken hareketler sakin oynar.");
        else if (choice == MotionLevel.Lively && Motion.RemoteSession) note = L.T("Uzak masaüstü bağlantısında hareketler sakin oynar.");
        AnimationsNote.Text = note ?? "";
        AnimationsNote.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
        WindowsMotionLink.Visibility = windowsOff ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Windows > Erişilebilirlik > Görsel efektler (Animasyon efektleri anahtarı orada).</summary>
    private void OpenVisualEffects_Click(object sender, RoutedEventArgs e) => Browser.Open("ms-settings:easeofaccess-visualeffects");

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
            IconsInFencesToggle.IsChecked = mode == IconMode.FencesOnly;
            IconModeText.Text = DesktopModes.Description(mode);
            PlacementBox.SelectedIndex = Array.IndexOf(PlaceModes.Choices, PlaceModes.Parse(s.NewWidgetPlacement));
            var doubleClick = DesktopState.DoubleClickChoice(s.DoubleClickAction, s.DoubleClickHidesDesktop);
            DoubleClickBox.SelectedIndex = Array.IndexOf(DesktopState.DoubleClickChoices, doubleClick);
            DoubleClickText.Text = doubleClick switch
            {
                DesktopState.DoubleClickToggle => L.T("Simgeler (ve ayara göre widget'lar) gizlenir; yeniden çift tıklayınca geri gelir."),
                DesktopState.DoubleClickPeek => L.T("Windows'un simgeleri görünür, widget'lar kısa süre çekilir; yeniden çift tıklayınca dönülür."),
                DesktopState.DoubleClickNone => L.T("Çift tıklama yalnızca Windows'un kendi işini yapar."),
                DesktopState.DoubleClickRing => L.T("Halka imlecin yanında açılır; kısayol gerekmez. Simgeye çift tıklamak yine dosyayı açar."),
                _ => L.T("Bölmeler masaüstünü yönetirken Windows masaüstüne göz atar, yoksa masaüstünü gizler/gösterir. Simgeye çift tıklamak yine dosyayı açar."),
            };
            PeekHidesWidgetsToggle.IsChecked = s.PeekHidesWidgets;
            PeekShowsDesktopToggle.IsChecked = s.PeekShowsDesktop;
            // Test örneği gerçek pencereleri küçültmez (AppHost.StartPeek); anahtar orada salt okunur.
            PeekShowsDesktopToggle.IsEnabled = !AppHost.IsTestDesktop;
            PeekMinutesBox.SelectedIndex = Array.IndexOf(DesktopState.PeekMinuteChoices, DesktopState.NormalizePeekMinutes(s.PeekMinutes));
            UpdateBoxPanel();
        }
        finally
        {
            _loadingDesktop = false;
        }
    }

    /// <summary>"Simgeleri bölmelerde topla": karşılamadaki ve "Masaüstümü bölmelere ayır"daki seçimin aynısı (DesktopModes).</summary>
    private void IconsInFences_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingDesktop) return;
        var mode = IconsInFencesToggle.IsChecked == true ? IconMode.FencesOnly : IconMode.ShowAll;
        DesktopModes.Set(mode, WindowCenter(), Window.GetWindow(this));
        // Soru penceresinden sonra ya da ayar değişmediyse de anahtar gerçek durumu göstersin.
        LoadDesktopChoices();
    }

    /// <summary>"Kutuya eklenenler masaüstünden kalkıyor." satırındaki "Değiştir": Gelişmiş ayarlar > Kutular.</summary>
    private void BoxChange_Click(object sender, RoutedEventArgs e) => ShowView(advanced: true, scrollTo: BoxHeader);

    /// <summary>
    /// "Kutuya koyduğum simgeler masaüstünden kalksın" satırı: anahtar, duruma göre açıklama (kapalı / dosyalar taşınıyor /
    /// bölme kipinde yalnızca bölmelerden kalkıyor; OneDrive'daki masaüstünde ek uyarı), taşınanlar (dosyalar taşınırken, taşınmış
    /// öğe kaldıysa ya da NestDesk klasörü duruyorsa görünür: kayıtlar kaybolsa da klasör açılabilsin; klasörün varlığına arka
    /// planda bakılır, bkz. BoxMover.RootExists; masaüstü sonradan taşındıysa öğelerin durduğu eski klasör) ve alt anahtarlar
    /// (yalnızca açıkken; herkesin masaüstü yalnızca dosyalar taşınırken, Store sürümünde ve test örneğinde, sahte ortak
    /// masaüstü verilmedikçe, gizli). Koddan atamalar <see cref="_loadingDesktop"/> ile ayrılır.
    /// </summary>
    private void UpdateBoxPanel()
    {
        var wasLoading = _loadingDesktop;
        _loadingDesktop = true;
        try
        {
            var s = AppHost.Settings;
            var moved = BoxMover.MovedCount;
            var folder = BoxMover.FolderToOpen;
            BoxLeavesToggle.IsChecked = BoxMover.On;
            BoxModeLine.Visibility = BoxMover.On ? Visibility.Visible : Visibility.Collapsed;
            BoxLeavesText.Text = DesktopModes.BoxLeavesDescription();
            BoxPanel.Visibility = BoxMover.MovesFiles || moved > 0 || BoxMover.RootExists ? Visibility.Visible : Visibility.Collapsed;
            BoxCountText.Text = moved == 0
                ? L.F("Henüz masaüstünden kalkan simge yok. Kalkanlar {0} klasöründe durur.", folder)
                : L.P(moved, "{0} simge kutularda; dosyalar {1} klasöründe.", folder);
            ReturnAllButton.IsEnabled = moved > 0;
            OpenBoxFolderButton.IsEnabled = moved > 0 || BoxMover.RootExists;
            BoxOptions.Visibility = BoxMover.On ? Visibility.Visible : Visibility.Collapsed;
            PublicBoxRow.Visibility = BoxMover.MovesFiles && !PackageInfo.IsPackaged && (!AppHost.IsTestDesktop || AppHost.TestPublicDesktop is not null)
                ? Visibility.Visible : Visibility.Collapsed;
            PublicBoxToggle.IsChecked = s.BoxIncludesPublicDesktop;
            AppShortcutsToggle.IsChecked = BoxAppShortcutChoices.Normalize(s.BoxAppShortcuts) != BoxAppShortcutChoices.Keep;
        }
        finally
        {
            _loadingDesktop = wasLoading;
        }
    }

    private void BoxLeavesToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingDesktop) return;
        DesktopModes.SetBoxLeaves(BoxLeavesToggle.IsChecked == true, WindowCenter(), Window.GetWindow(this));
        // Soru penceresinden sonra ya da ayar değişmediyse de satır gerçek durumu göstersin.
        UpdateBoxPanel();
    }

    private void OpenBoxFolder_Click(object sender, RoutedEventArgs e) => TileItem.Launch(BoxMover.FolderToOpen);

    /// <summary>Hepsi masaüstüne döner, kutularda kalır (soru yok: bildirimdeki "Geri al" yeniden kutulara taşır).</summary>
    private void ReturnAll_Click(object sender, RoutedEventArgs e)
    {
        if (BoxMover.MovedCount > 0) BoxMover.ReturnAll();
    }

    private void PublicBoxToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingDesktop) return;
        // Açarken kutudaki herkesin masaüstü simgeleri bir yönetici onayıyla taşınır; ayar yalnızca başarılı olunca açık kalır.
        DesktopModes.SetBoxPublic(PublicBoxToggle.IsChecked == true, Window.GetWindow(this), UpdateBoxPanel);
        UpdateBoxPanel();
    }

    private void AppShortcutsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingDesktop) return;
        AppHost.Settings.BoxAppShortcuts = AppShortcutsToggle.IsChecked == true ? BoxAppShortcutChoices.Leave : BoxAppShortcutChoices.Keep;
        AppHost.SaveSettings();
        BoxMover.ModeChanged();
    }

    /// <summary>"Windows masaüstüne göz at" satırı şu anki durumu ve kısayolu gösterir.</summary>
    private void UpdatePeek()
    {
        var peeking = AppHost.Peeking;
        PeekTitle.Text = peeking ? L.T("Windows masaüstüne göz atılıyor") : L.T("Windows masaüstüne göz at");
        var back = L.F("{0}'e dön", AppInfo.Name);
        PeekButton.Content = peeking ? back : L.T("Göz at");
        System.Windows.Automation.AutomationProperties.SetName(PeekButton, peeking ? back : L.T("Windows masaüstüne göz at"));
        var shortcut = AppHost.Settings.Hotkeys.PeekDesktop;
        PeekText.Text = string.IsNullOrWhiteSpace(shortcut)
            ? L.T("Windows'un masaüstü simgeleri görünür, widget'lar kısa süre çekilir; ekranın üstündeki çubuktan dönülür. Kısayol atanmamış.")
            : L.F("Windows'un masaüstü simgeleri görünür, widget'lar kısa süre çekilir; ekranın üstündeki çubuktan dönülür. Kısayol: {0}", shortcut);
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
        RingPopup.Sync();
        // Halka menü bölümündeki "Boş masaüstüne çift tıklayınca açılsın" anahtarı aynı ayarı gösterir.
        ReloadRingAndHotkeys();
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

    // ---- Widget yerleşimi ----

    private void SnapToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppHost.Settings.SnapWidgets = SnapToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    private void OverlapToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        // Yalnızca ayar yazılır: sonraki bırakma/büyütmede uygulanır, var olan widget'lar yerinden oynamaz.
        AppHost.Settings.PreventOverlap = OverlapToggle.IsChecked == true;
        AppHost.SaveSettings();
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
        HideNowTitle.Text = hidden ? L.T("Masaüstünü göster") : L.T("Masaüstünü şimdi gizle");
        HideNowButton.Content = hidden ? L.T("Göster") : L.T("Gizle");
        System.Windows.Automation.AutomationProperties.SetName(HideNowButton, HideNowTitle.Text);
        var shortcut = AppHost.Settings.Hotkeys.ToggleDesktop;
        HideNowShortcut.Text = string.IsNullOrWhiteSpace(shortcut) ? L.T("Kısayol atanmamış") : L.F("Kısayol: {0}", shortcut);
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
            Notice.Show(L.T("Masaüstünde klasör yok."), NoticeKind.Info);
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
            Notice.Show(L.P(done, "{0} klasöre adına uygun simge verildi. Beğenmediğini \"Bir klasörün simgesini değiştir\"den değiştirebilirsin."));
        else
            Notice.Show(L.T("Değişiklik yok: tüm klasörlerin zaten özel simgesi var."), NoticeKind.Info);
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

    // Listede gösterilen halka kısayolu (widget menüsünden değişince liste yenilenir).
    private string? _listedRing;

    private void LoadHotkeys()
    {
        _listedRing = AppHost.Settings.Hotkeys.Ring;
        var failures = AppHost.Hotkeys?.Failures ?? [];
        HotkeyList.ItemsSource = HotkeyActions
            .Select(a => new HotkeyRow(a.Action, a.Label, a.Icon, AppHost.Settings.Hotkeys.Get(a.Action), failures.GetValueOrDefault(a.Action)))
            .ToList();
    }

    private void ApplyHotkeys()
    {
        AppHost.SaveSettings();
        AppHost.Hotkeys?.Apply(AppHost.Settings.Hotkeys);
        // Halka kısayolu kutudan atandı ya da silindi: açılır halka önceden kurulur ya da kapanır.
        RingPopup.Sync();
        ReloadRingAndHotkeys();
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
        var ringWasOn = RingShortcut.IsOn;
        AppHost.Settings.Hotkeys.Set(row.Action, new Hotkey(mods, keyName).ToString());
        ApplyHotkeys();
        // Halka kısayolu listeden açıldı: ne yaptığı anlatılır (widget menüsünden açılınca olduğu gibi; Store matrisi 2b).
        if (row.Action == HotkeyAction.Ring && !ringWasOn && RingShortcut.Current is { } ring && RingShortcut.Failure is null)
            Notice.Show(RingShortcut.TurnedOnText(ring), NoticeKind.Info);
    }

    private void ResetHotkeys_Click(object sender, RoutedEventArgs e)
    {
        // Varsayılanlarda halka kısayolu yoktur (Store: kapalı ve atanmamış): açıksa kapanır ve bu söylenir.
        var ringWasOn = RingShortcut.IsOn;
        AppHost.Settings.Hotkeys = new HotkeySettings();
        ApplyHotkeys();
        if (ringWasOn)
            Notice.Show(L.T("Kısayollar varsayılana döndü. Halka menü kısayolu kapatıldı; istersen Halka menü bölümünden yeniden açabilirsin."),
                NoticeKind.Info);
    }

    // ---- Masaüstü: halka menü ----

    /// <summary>
    /// Halka menü bölümü: halkanın nasıl açıldığı (çift tıklama, kısayol, yoksa yalnızca tepsi), açılacak halka, "tuşu bırakınca
    /// seç", "Halkayı düzenle" (NestDesk halkası gerekirse kurulur) ve masaüstünde halka widget'ı yoksa ekleme düğmesi.
    /// </summary>
    private void LoadRing()
    {
        var hotkey = RingShortcut.Current;
        var failure = RingShortcut.Failure;
        var doubleClick = RingOpensOnDoubleClick;
        RingStatus.Text = failure is not null && hotkey is not null ? L.F("{0} kaydedilemedi: {1}", hotkey, failure)
            : doubleClick && hotkey is not null ? L.F("Boş masaüstüne çift tıkla ya da {0} tuşlarına bas", hotkey)
            : doubleClick ? L.T("Boş masaüstüne çift tıklayınca açılır")
            : hotkey is not null ? L.F("{0} ile imlecin yanında açılır", hotkey)
            : L.T("Yalnızca tepsiden açılır");
        RingStatus.Foreground = (Brush)Application.Current.FindResource(failure is null ? "TextFillColorTertiaryBrush" : "SystemFillColorCriticalBrush");
        RingDoubleClickToggle.IsChecked = doubleClick;
        RingToggle.IsChecked = hotkey is not null;
        RingHoldToggle.IsChecked = !AppHost.Settings.RingHoldSelectOff;

        var rings = Launchers.Rings(AppHost.Settings);
        var choices = rings.Select(r => new RingChoice(r.Id, WidgetText.DisplayName(r))).ToList();
        RingTargetBox.ItemsSource = choices;
        var target = RingTargets.Pick(rings, AppHost.Settings.RingWidgetId);
        RingTargetBox.SelectedItem = choices.FirstOrDefault(c => c.Id == target?.Id);
        RingTargetRow.Visibility = rings.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        // "Halkayı düzenle" her zaman (HomeRing.Edit NestDesk halkasını gerekirse kurar); masaüstünde halka widget'ı yoksa eklenebilir.
        RingAddRow.Visibility = RingTargets.All(AppHost.Settings.Widgets.Where(w => w is not null)).Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RingEditRow.Visibility = Visibility.Visible;
    }

    /// <summary>Boş masaüstüne çift tıklama halkayı mı açıyor (Gelişmiş'teki kutuda "Halkayı aç")?</summary>
    private static bool RingOpensOnDoubleClick =>
        DesktopState.DoubleClickChoice(AppHost.Settings.DoubleClickAction, AppHost.Settings.DoubleClickHidesDesktop) == DesktopState.DoubleClickRing;

    /// <summary>Halka açılmadan önceki çift tıklama seçimi (bu oturumda): anahtar kapanınca ona dönülür, yoksa "otomatik".</summary>
    private static string? _doubleClickBeforeRing;

    private void RingDoubleClick_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var on = RingDoubleClickToggle.IsChecked == true;
        if (on == RingOpensOnDoubleClick) return;
        if (on)
        {
            _doubleClickBeforeRing = DesktopState.DoubleClickChoice(AppHost.Settings.DoubleClickAction, AppHost.Settings.DoubleClickHidesDesktop);
            AppHost.SetDoubleClickAction(DesktopState.DoubleClickRing);
        }
        else AppHost.SetDoubleClickAction(_doubleClickBeforeRing is { } before && before != DesktopState.DoubleClickRing ? before : DesktopState.DoubleClickAuto);
        // Açılır halka çift tıklamayla da açılacaksa boşta önceden kurulur; artık açılmayacaksa kapanır.
        RingPopup.Sync();
        LoadDesktopChoices();
        ReloadRingAndHotkeys();
    }

    private void RingToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (RingToggle.IsChecked != true)
        {
            // Açarken çıkan "… ile imlecin yanında açılır" şeridi kapatınca ortada kalmasın (widget menüsündeki gibi söylenir).
            var wasOn = RingShortcut.IsOn;
            if (wasOn) RingShortcut.TurnOff();
            ReloadRingAndHotkeys();
            if (wasOn) Notice.Show(L.T("Halka menü kısayolu kapatıldı."), NoticeKind.Info);
            return;
        }
        if (RingShortcut.IsOn)
        {
            ReloadRingAndHotkeys();
            return;
        }
        var result = RingShortcut.TurnOn();
        ReloadRingAndHotkeys();
        if (result.Hotkey is { } hotkey)
        {
            Notice.Show(L.F("Halka menü {0} ile imlecin yanında açılır.", hotkey), NoticeKind.Success);
            return;
        }
        // Önerilerin hepsi kullanılıyor: kullanıcı kendi birleşimini seçsin (kısayol kutusu açılır ve odaklanır).
        Notice.Show(RingShortcut.NoShortcutText(result.Failure), NoticeKind.Warning);
        FocusHotkeyBox(HotkeyAction.Ring);
    }

    /// <summary>Kısayol listesi ve halka bölümü ayardan yeniden okunur (koddan atama anahtarları tetiklemez).</summary>
    private void ReloadRingAndHotkeys()
    {
        LoadHotkeys();
        UpdateHideNow();
        UpdatePeek();
        var loading = _loading;
        _loading = true;
        try { LoadRing(); }
        finally { _loading = loading; }
    }

    /// <summary>Klavye kısayolları bölümünü açar ve eylemin kutusuna odaklanır.</summary>
    private void FocusHotkeyBox(HotkeyAction action)
    {
        ShowView(advanced: true, focus: false);
        if (HotkeysContent.Visibility != Visibility.Visible)
            HotkeysFold.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, HotkeysFold));
        Dispatcher.BeginInvoke(() =>
        {
            if (HotkeyList.ItemsSource is not IEnumerable<HotkeyRow> rows) return;
            var index = rows.ToList().FindIndex(r => r.Action == action);
            if (index < 0 || HotkeyList.ItemContainerGenerator.ContainerFromIndex(index) is not DependencyObject container) return;
            if (FindChild<System.Windows.Controls.TextBox>(container) is { } box)
            {
                box.BringIntoView();
                box.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null;
    }

    private void RingTargetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || RingTargetBox.SelectedItem is not RingChoice choice || choice.Id == AppHost.Settings.RingWidgetId) return;
        AppHost.Settings.RingWidgetId = choice.Id;
        AppHost.SaveSettings();
    }

    private void RingHoldToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var off = RingHoldToggle.IsChecked != true;
        if (AppHost.Settings.RingHoldSelectOff == off) return;
        AppHost.Settings.RingHoldSelectOff = off;
        AppHost.SaveSettings();
    }

    /// <summary>"Halkayı düzenle": Ana sayfadaki düğmenin aynısı (halka öne gelir ve nasıl değiştirileceği söylenir).</summary>
    private void EditRing_Click(object sender, RoutedEventArgs e) => HomeRing.Edit(WindowCenter());

    private void RingAdd_Click(object sender, RoutedEventArgs e)
    {
        if (RingShortcut.AddRing(RingAddPoint()) is not null) ReloadRingAndHotkeys();
    }

    /// <summary>Ayarlar penceresinin ortası (fiziksel piksel): yeni halka o ekrana eklenir.</summary>
    private NativeMethods.POINT? RingAddPoint()
    {
        if (Window.GetWindow(this) is not { } window) return null;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var r)) return null;
        return new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 };
    }

    // ---- Gelişmiş: yapay zekâ ----

    private void UpdateKeyStatus() =>
        KeyStatus.Text = ApiKeyStore.HasKey ? L.F("Kayıtlı ({0})", ApiKeyStore.Masked()) : L.T("Eklenmedi");

    private async void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        var key = KeyBox.Password.Trim();
        if (key.Length == 0)
        {
            ShowKey(L.T("Anahtar boş"), L.T("Önce anahtarı yapıştır."), InfoBarSeverity.Warning);
            return;
        }
        SaveKeyButton.IsEnabled = false;
        try
        {
            var error = await AiIconGenerator.ValidateKeyAsync(key);
            if (error is not null)
            {
                ShowKey(L.T("Kaydedilmedi"), error, InfoBarSeverity.Error);
                return;
            }
            ApiKeyStore.Save(key);
            KeyBox.Password = "";
            UpdateKeyStatus();
            ShowKey(L.T("Kaydedildi"), L.T("Anahtar doğrulandı. Klasör simgesi penceresinde \"Üret\" artık kullanılabilir."), InfoBarSeverity.Success);
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
        ShowKey(L.T("Silindi"), L.T("API anahtarı ayarlardan kaldırıldı. Eski ayar yedeklerinde (yedekler klasörü) şifreli kopyası kalabilir."), InfoBarSeverity.Informational);
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
        if (!Confirm.Ask(Window.GetWindow(this), L.T("Klasör simgelerinin hepsi kaldırılsın mı?"),
                L.F("{0}'in verdiği bütün klasör simgeleri silinir ve klasörler Windows'un varsayılan simgesine döner. Klasörlere ve içlerindeki dosyalara dokunulmaz. Simgeleri yeniden vermek istersen \"Klasör simgeleri\" bölümünü kullan.",
                    AppInfo.Name),
                L.T("Hepsini kaldır")))
            return;
        RemoveIconsButton.IsEnabled = false;
        try
        {
            var roots = AppHost.DesktopDirectories.ToList();
            // Kutulara taşınan masaüstü klasörleri (NestDesk\<kutu>\<klasör>) simgeleriyle birlikte taşınmıştır.
            var boxRoot = BoxMover.Root;
            // Klasör portalları masaüstü dışındaki klasörleri gösterir: onların (ve bir alt düzeyin) simgeleri de kaldırılır.
            var portals = AppHost.Settings.Widgets.Where(WidgetVariants.IsPortal).Select(w => w.FolderName!).ToList();
            // Simge verilirken kaydedilen klasörler: derin aramayla bulunan alt klasör, kaldırılmış portalın klasörü…
            var recorded = AppHost.Settings.IconFolders?.ToList() ?? [];
            var recordedFailures = new List<string>();
            var (removed, failed) = await Task.Run(() =>
            {
                var parts = new List<(List<string> Removed, int Failed)> { FolderIconService.RemoveAllOwnIcons(roots) };
                if (Directory.Exists(boxRoot)) parts.Add(FolderIconService.RemoveAllOwnIcons([boxRoot], depth: 3));
                if (portals.Count > 0) parts.Add(FolderIconService.RemoveAllOwnIcons(portals, depth: 1, includeRoots: true));
                // Artık olmayan kayıtlı klasörlere bakılmaz (listeden düşer).
                var existing = recorded.Where(Directory.Exists).ToList();
                if (existing.Count > 0)
                    parts.Add(FolderIconService.RemoveAllOwnIcons(existing, depth: 0, includeRoots: true, failures: recordedFailures));
                return (parts.SelectMany(p => p.Removed).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), parts.Sum(p => p.Failed));
            });
            AppHost.Settings.IconFolders = FolderIconLog.AfterRemoveAll(AppHost.Settings.IconFolders, recorded, recordedFailures);
            AppHost.SaveSettingsLater();
            removed.ForEach(ShellIcons.Forget);
            if (_folderIcons is not null) LoadFolderIcons();
            var text = removed.Count > 0
                ? L.P(removed.Count, "{0} klasörün simgesi kaldırıldı; masaüstü birkaç saniye içinde yenilenir.")
                : L.F("Kaldırılacak simge yok: {0}'in verdiği bir klasör simgesi bulunamadı.", AppInfo.Name);
            if (failed > 0) text += " " + L.P(failed, "{0} klasöre erişilemediği için dokunulamadı.");
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
            PortableText.Text = L.T("Microsoft Store sürümüne ayrılmış klasör; uygulama kaldırılınca Windows içindekileri de siler.");
            PortableText.Visibility = own ? Visibility.Visible : Visibility.Collapsed;
            return;
        }
        PortableText.Text = AppHost.IsPortable
            ? L.T("Taşınabilir mod açık: ayarlar exe'nin yanındaki data klasöründe.")
            : AppHost.PortableFallback
                ? L.T("portable.txt var ama programın klasörüne yazılamıyor; ayarlar şimdilik yukarıdaki klasörde. Programı yazılabilir bir klasöre taşı.")
                : AppHost.DataFolderSource == DataFolderSource.MoveFailed
                    ? L.F("Klasör o sırada kullanımda olduğu için {0} adıyla yeniden adlandırılamadı; bir sonraki açılışta yeniden denenecek.", AppInfo.DataFolderName)
                    : L.T("Taşınabilir kullanım için exe'nin yanına boş bir portable.txt dosyası koy.");
    }

    private void OpenDesktop_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppHost.DesktopDirectory) { UseShellExecute = true });

    private void OpenData_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppHost.DataDirectoryOnDisk) { UseShellExecute = true });

    // ---- Gelişmiş: hata günlüğü (2.2 yerel tanılama) ----

    // Bölüm ilk açılınca dolar; durum satırı (diskteki günlükler) arka planda okunur.
    private bool _errorLogLoaded;
    private int _logStatusQuery;

    private void LoadErrorLog()
    {
        _errorLogLoaded = true;
        var loading = _loading;
        _loading = true;
        try
        {
            ErrorLogToggle.IsChecked = !AppHost.Settings.ErrorLogOff;
            CrashModeBox.SelectedIndex = CrashReportModes.Normalize(AppHost.Settings.CrashReportMode) switch
            {
                CrashReportModes.Always => 1,
                CrashReportModes.Never => 2,
                _ => 0,
            };
        }
        finally { _loading = loading; }
        RefreshLogStatus();
    }

    private void OnDiagnosticsChanged()
    {
        if (_errorLogLoaded) LoadErrorLog();
    }

    private async void RefreshLogStatus()
    {
        var query = ++_logStatusQuery;
        LogStatus status;
        try { status = await Diagnostics.ReadStatusAsync(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        if (query != _logStatusQuery) return;
        LogStatusText.Text = AppHost.Settings.ErrorLogOff
            ? L.F("Hata günlüğü kapalı: yeni kayıt yazılmıyor. Kayıtlı günlükler: {0}", MeasureText.Bytes(status.Bytes))
            : L.P(status.Errors, "Son 7 günde {0} hata · {1}", MeasureText.Bytes(status.Bytes));
        PendingRow.Visibility = status.Pending > 0 ? Visibility.Visible : Visibility.Collapsed;
        PendingText.Text = L.P(status.Pending, "{0} çökme raporu kaydedilmeyi bekliyor.");
    }

    private void ErrorLogToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Diagnostics.SetLogEnabled(ErrorLogToggle.IsChecked == true);
    }

    private void CrashModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CrashModeBox.SelectedIndex < 0) return;
        Diagnostics.SetCrashMode(CrashModeBox.SelectedIndex switch
        {
            1 => CrashReportModes.Always,
            2 => CrashReportModes.Never,
            _ => null,
        });
    }

    private void SavePending_Click(object sender, RoutedEventArgs e) => Diagnostics.SavePending();

    private void DiscardPending_Click(object sender, RoutedEventArgs e) => Diagnostics.DiscardPending();

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => Diagnostics.OpenLogsFolder();

    private void DeleteLogs_Click(object sender, RoutedEventArgs e) => Diagnostics.DeleteLogs(Window.GetWindow(this));

    // ---- Yardım ve Hakkında ----

    /// <summary>Ana sayfadaki "Nasıl kullanılır" kartları yeniden gösterilir ve Ana sayfa açılır.</summary>
    private void HowTo_Click(object sender, RoutedEventArgs e) => HowToCards.ShowAgain();

    private void Welcome_Click(object sender, RoutedEventArgs e) => (Application.Current as App)?.ShowWelcome(rerun: true);

    private void CreatePackage_Click(object sender, RoutedEventArgs e) => Diagnostics.CreatePackage(Window.GetWindow(this));

    private void OpenDiagnosticPage_Click(object sender, RoutedEventArgs e) => Diagnostics.OpenPage();

    /// <summary>"GitHub'da sorun aç": adres yalnızca şablonu, başlığı ve sürümü taşır (paketi kullanıcı kendisi ekler).</summary>
    private void ReportProblem_Click(object sender, RoutedEventArgs e) => Browser.Open(SupportLinks.ReportProblem(AppInfo.Version));

    /// <summary>Sürüm, kanal (Store / taşınabilir / kurulum) ve nasıl güncelleneceği. Uygulama kendisi güncelleme denetlemez.</summary>
    private void LoadAbout()
    {
        VersionText.Text = $"{AppInfo.Name} {AppInfo.Version}";
        TaglineText.Text = AppInfo.Tagline;
        ChannelText.Text = PackageInfo.IsPackaged
            ? L.T("Microsoft Store sürümü · Güncellemeler Microsoft Store'dan kendiliğinden gelir.")
            : AppHost.IsPortable
                ? L.T("Taşınabilir sürüm · Güncellemek için yeni zip'i bu klasörün üzerine çıkar; data klasörün korunur.")
                : L.T("Kurulum sürümü · Güncellemek için indirme sayfasından yeni kurulum dosyasını çalıştır; ayarların korunur.");
        PrivacyText.Text = L.F("{0} güncelleme denetlemez ve kendiliğinden internete bağlanmaz. Ayarların, notların ve hata günlüğün yalnızca bu bilgisayarda.", AppInfo.Name);
        // Store sürümünü Store günceller; indirme sayfası yalnızca kurulum/taşınabilir sürüm için.
        ReleasesButton.Visibility = PackageInfo.IsPackaged ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Releases_Click(object sender, RoutedEventArgs e) => Browser.Open(AppInfo.ReleasesUrl);

    /// <summary>Gizlilik politikası (Store 10.5.1: uygulama içinden erişilebilir olmalı); Hakkında ve yapay zekâ bölümünde.</summary>
    private void Privacy_Click(object sender, RoutedEventArgs e) => Browser.Open(AppInfo.PrivacyUrl);

    private void Exit_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).ExitApp();

    // ---- İki görünüm: Ayarlar ve Gelişmiş ayarlar ----

    /// <summary>Yüklü sayfa (bildirimden gelen "Gelişmiş ayarlar" isteği ona gider); yoksa null.</summary>
    private static SettingsPage? _current;

    /// <summary>Sayfa bir sonraki yüklenişinde Gelişmiş ayarlar'la açılsın.</summary>
    private static bool _advancedRequested;

    /// <summary>Ana pencereyi Ayarlar > Gelişmiş ayarlar'da açar (ör. "Yeni widget'ların yeri" ipucu).</summary>
    internal static void OpenAdvanced()
    {
        if (_current is { IsLoaded: true } page)
        {
            (Application.Current as App)?.ShowPage(typeof(SettingsPage));
            page.ShowView(advanced: true, focus: false);
            return;
        }
        _advancedRequested = true;
        (Application.Current as App)?.ShowPage(typeof(SettingsPage));
    }

    private void Advanced_Click(object sender, RoutedEventArgs e) => ShowView(advanced: true);

    private void Back_Click(object sender, RoutedEventArgs e) => ShowView(advanced: false);

    /// <summary>
    /// Görünümü değiştirir: sayfa başa kayar (ya da <paramref name="scrollTo"/>'ya), yeni görünüm kısa bir sayfa geçişiyle gelir
    /// (Views.Motion; Kapalı'da hemen) ve klavye odağı ilk düğmeye (geri ya da "Gelişmiş ayarlar") gider.
    /// </summary>
    private void ShowView(bool advanced, FrameworkElement? scrollTo = null, bool focus = true)
    {
        var target = advanced ? AdvancedView : MainView;
        var changed = target.Visibility != Visibility.Visible;
        MainView.Visibility = advanced ? Visibility.Collapsed : Visibility.Visible;
        AdvancedView.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
        if (changed && IsLoaded) Motion.Enter(target, MotionKind.Page);
        Dispatcher.BeginInvoke(() =>
        {
            if (scrollTo is not null) scrollTo.BringIntoView(new Rect(0, 0, 1, scrollTo.ActualHeight + 240));
            else if (changed) FindScroller(this)?.ScrollToTop();
            if (focus) (advanced ? BackButton : AdvancedButton).Focus();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Sayfayı kaydıran ScrollViewer (WPF-UI gezinme görünümünün içeriği).</summary>
    private static System.Windows.Controls.ScrollViewer? FindScroller(DependencyObject from)
    {
        for (var parent = VisualTreeHelper.GetParent(from); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is System.Windows.Controls.ScrollViewer scroller) return scroller;
        return null;
    }
}
