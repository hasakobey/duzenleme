using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Wpf.Ui.Controls;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Duzenleme.Views;

/// <summary>
/// Kural kartı ("PDF ← .pdf"): klasör, uzantı çipleri, durum ("Masaüstünde yok · Oluştur"), anahtar ve ⋯. Kartta yazı
/// kutusu yok: klasör ve uzantılar "Değiştir…" ile (<see cref="RuleDialog"/>) değişir. Durum (klasör masaüstünde var mı,
/// kaç dosya bekliyor) sayfa tarafından <see cref="SetStatus"/> ile verilir: kart diske kendisi bakmaz.
/// </summary>
public sealed class RuleRow(Rule rule, int index, Action<RuleRow> changed) : INotifyPropertyChanged
{
    private static readonly string[] StatusProperties =
    [
        nameof(Exists), nameof(FolderIcon), nameof(IconBrush), nameof(StatusText), nameof(CreateVisibility), nameof(RowOpacity),
    ];

    private bool _exists;
    private int _pending;
    private bool _showPending = true;

    public Rule Rule { get; } = rule;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Açık/kapalı: yerinde yazılır (tek bool; izleyici aynı kuralı okurken yarım değer görmez) ve hemen kaydedilir.</summary>
    public bool Enabled
    {
        get => Rule.Enabled;
        set
        {
            if (Rule.Enabled == value) return;
            Rule.Enabled = value;
            changed(this);
        }
    }

    private bool HasFolderName => !string.IsNullOrWhiteSpace(Rule.TargetFolder);

    public string FolderText => HasFolderName ? Rule.TargetFolder.Trim() : L.T("Klasör adı yok");

    /// <summary>Uzantı çipleri: ".pdf", ".xps".</summary>
    public IReadOnlyList<string> Chips =>
        Rule.Extensions.Select(RuleTemplates.Normalize).Where(e => e.Length > 0).Distinct().Select(e => "." + e).ToList();

    public int Pending => _pending;
    public bool Exists => _exists;
    public SymbolRegular FolderIcon => SymbolRegular.Folder24;
    public Brush IconBrush => (Brush)Application.Current.FindResource(_exists ? "AccentTextFillColorPrimaryBrush" : "TextFillColorTertiaryBrush");

    public string StatusText =>
        !Rule.Enabled ? L.T("Kapalı")
        : !HasFolderName ? L.T("Klasör adı yok; hiçbir dosya taşınmaz")
        : Rule.Extensions.Count == 0 ? L.T("Uzantı yok; hiçbir dosya taşınmaz")
        : _exists ? L.T("Masaüstünde var")
        : _showPending && _pending > 0 ? L.P(_pending, "Masaüstünde yok · {0} dosya bekliyor")
        : L.T("Masaüstünde yok");

    public Visibility CreateVisibility => _exists || !HasFolderName ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Kapalı kuralın klasörü ve çipleri soluk (anahtar ve ⋯ değil).</summary>
    public double RowOpacity => Rule.Enabled ? 1 : 0.6;

    // UI Automation: her kartın denetimleri sırasıyla bulunabilsin (Rule.0.Enabled, Rule.0.More…).
    public string EnabledId => $"Rule.{index}.Enabled";
    public string ChipsId => $"Rule.{index}.Extensions";
    public string MoreId => $"Rule.{index}.More";
    public string CreateId => $"Rule.{index}.CreateFolder";
    public string StatusId => $"Rule.{index}.Status";
    public string EnabledName => L.F("\"{0}\" kuralı", Rule.TargetFolder);
    public string MoreName => L.F("Kural seçenekleri: {0}", Rule.TargetFolder);
    public string CreateName => L.F("Klasörü oluştur: {0}", Rule.TargetFolder);

    /// <summary>Klasörün masaüstündeki durumu ve bekleyen dosya sayısı (showPending: "Klasör yoksa oluştur" kapalıyken).</summary>
    internal void SetStatus(bool exists, int pending, bool showPending)
    {
        _exists = exists;
        _pending = pending;
        _showPending = showPending;
        foreach (var name in StatusProperties) Raise(name);
    }

    /// <summary>Açık/kapalı değişti: durum ve soluk görünüm yenilenir.</summary>
    internal void RaiseEnabled()
    {
        Raise(nameof(Enabled));
        foreach (var name in StatusProperties) Raise(name);
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Otomatik taşıma: durum kartı (anahtar, "Dosyaları şimdi taşı", "Son taşımayı geri al"), kural kartları ("+ Kural ekle" →
/// <see cref="RuleDialog"/>; ⋯ → "Değiştir…" / "Sil"), son taşınanlar ve katlanır "Gelişmiş".
/// </summary>
public partial class AutoMovePage : Page
{
    private const int HistoryLimit = 10;

    // "Geçmişi gör": sayfa açılınca (ya da zaten açıksa hemen) Son taşınanlar'a kaydırılır.
    private static AutoMovePage? _current;
    private static bool _pendingHistory;

    private readonly DispatcherTimer _recountDelay = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private List<RuleRow> _rows = [];
    /// <summary>Kartların kurulduğu liste: ayarlardaki liste başka yerden yenisiyle değiştirilince kartlar yeniden kurulur.</summary>
    private List<Rule>? _builtFrom;
    private Dictionary<string, int> _pending = [];
    private int _recountVersion;
    private int _journalQueued;
    private bool _showAll;
    private Window? _host;

    public AutoMovePage()
    {
        InitializeComponent();
        Fold.Attach(AdvancedToggle, AdvancedContent);
        _recountDelay.Tick += (_, _) => Recount();
        Loaded += (_, _) =>
        {
            _current = this;
            CreateMissing.IsChecked = AppHost.Settings.CreateMissingFolders;
            RefreshCard();
            BuildRules();
            RefreshHistory();
            Recount();
            // Yeni satırlar yerleştikten sonra kaydırılır (Loaded önceliği yerleşimden sonra gelir).
            Dispatcher.BeginInvoke(ConsumePendingHistory, DispatcherPriority.Loaded);
        };
        Unloaded += (_, _) =>
        {
            if (_current == this) _current = null;
        };
        // Ana pencere gizliyken sayfa kayıtlara, taşımalara, masaüstüne ve pencere etkinleşmesine tepki vermez;
        // yeniden görününce bir kez güncellenir.
        PageLife.WhileShown(this,
            attach: () =>
            {
                AppHost.SettingsChanged += OnSettingsChanged;
                AppHost.Journal.Changed += OnJournalChanged;
                AppHost.DesktopSnapshot.Changed += OnDesktopChanged;
                WatchHostActivation(true);
            },
            detach: () =>
            {
                AppHost.SettingsChanged -= OnSettingsChanged;
                AppHost.Journal.Changed -= OnJournalChanged;
                AppHost.DesktopSnapshot.Changed -= OnDesktopChanged;
                WatchHostActivation(false);
                _recountDelay.Stop();
            },
            refresh: () =>
            {
                CreateMissing.IsChecked = AppHost.Settings.CreateMissingFolders;
                RefreshCard();
                if (!RebuildIfRulesReplaced()) RefreshStatuses();
                RefreshHistory();
                ScheduleRecount();
            });
    }

    /// <summary>Ana pencerede Otomatik taşıma → Son taşınanlar'ı açar ("Geçmişi gör", Ana sayfadaki "Tümünü gör").</summary>
    internal static void ShowHistory()
    {
        _pendingHistory = true;
        (Application.Current as App)?.ShowPage(typeof(AutoMovePage));
        // Sayfa zaten açıksa Loaded yeniden gelmez; yeni açıldıysa Loaded (daha yüksek öncelikte) önce davranır.
        Application.Current?.Dispatcher.BeginInvoke(() => _current?.ConsumePendingHistory(), DispatcherPriority.Background);
    }

    private void ConsumePendingHistory()
    {
        if (!_pendingHistory || !IsLoaded) return;
        _pendingHistory = false;
        // Başlık ve ilk satırlar görünsün; bölüm uzunsa sonuna kadar kaydırılmasın.
        HistorySection.BringIntoView(new Rect(0, 0, HistorySection.ActualWidth, 320));
    }

    // Kullanıcı Gezgin'den geri dönünce bekleyen dosya sayıları da yeniden sayılsın (klasör var/yok durumu zaten canlı).
    private void WatchHostActivation(bool watch)
    {
        if (_host is not null) _host.Activated -= Host_Activated;
        _host = watch ? Window.GetWindow(this) : null;
        if (_host is not null) _host.Activated += Host_Activated;
    }

    private void Host_Activated(object? sender, EventArgs e)
    {
        RefreshCard();
        if (RebuildIfRulesReplaced()) return;
        RefreshStatuses();
        ScheduleRecount();
    }

    /// <summary>Geçmiş değişti (taşıyıcının iş parçacığından da gelir); toplu taşımadaki art arda bildirimler birleşir.</summary>
    private void OnJournalChanged()
    {
        if (Interlocked.Exchange(ref _journalQueued, 1) == 1) return;
        Dispatcher.BeginInvoke(() =>
        {
            Volatile.Write(ref _journalQueued, 0);
            if (!IsLoaded) return;
            RefreshCard();
            RefreshHistory();
            // "Klasör yoksa oluştur" açıkken taşıma klasör de açmış olabilir.
            RefreshStatuses();
            ScheduleRecount();
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Masaüstünde klasör açıldı/silindi: kart ve kuralların "klasör var/yok" durumu hemen güncellensin (anlık görüntüden,
    /// diske dokunmadan). Bekleyen dosya sayıları pencere etkinleşince ya da taşımada yeniden sayılır.
    /// </summary>
    private void OnDesktopChanged()
    {
        if (!IsLoaded) return;
        RefreshCard();
        RefreshStatuses();
    }

    // ---- Durum kartı ----

    // Ayarlar her kaydedildiğinde (widget taşımak da kaydeder): kart; kural listesi yalnızca başka yerden yenisiyle
    // değiştirildiyse yeniden kurulur.
    private void OnSettingsChanged()
    {
        RefreshCard();
        RebuildIfRulesReplaced();
    }

    private void RefreshCard()
    {
        var on = !AppHost.Settings.Paused;
        var (title, text) = AutoMoveStatus.Describe(on, AutoMoveStatus.ReadyRules());
        AutoMoveToggle.IsChecked = on;
        StatusTitle.Text = title;
        StatusText.Text = text;
        StatusIcon.Symbol = on ? SymbolRegular.FolderArrowRight24 : SymbolRegular.Pause24;
        StatusIcon.SetResourceReference(IconElement.ForegroundProperty, on ? "AccentTextFillColorPrimaryBrush" : "TextFillColorTertiaryBrush");
        UndoLastButton.IsEnabled = AppHost.Journal.LastActive() is not null;
    }

    // Checked/Unchecked (Click değil): ekran okuyucunun "Aç/Kapat"ı (UI Automation Toggle) da uygulasın. Koddan gösterilen
    // durum ayarla zaten aynıdır: yalnızca kullanıcının değişikliği taşımayı açar/kapatır.
    private void AutoMoveToggle_Changed(object sender, RoutedEventArgs e)
    {
        var on = AutoMoveToggle.IsChecked == true;
        if (on != !AppHost.Settings.Paused) AppHost.SetPaused(!on);
    }

    private async void OrganizeNow_Click(object sender, RoutedEventArgs e)
    {
        OrganizeButton.IsEnabled = false;
        try { await MoveActions.OrganizeNowAsync(); }
        finally { OrganizeButton.IsEnabled = true; }
    }

    private void UndoLast_Click(object sender, RoutedEventArgs e) => MoveActions.Undo(AppHost.Journal.LastActive());

    // ---- Kurallar ----

    private void BuildRules()
    {
        _builtFrom = AppHost.Settings.Rules;
        _rows = _builtFrom.Select((rule, i) => new RuleRow(rule, i, OnRuleChanged)).ToList();
        RefreshStatuses();
        RuleList.ItemsSource = _rows;
        NoRules.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Kural listesi bu sayfa dışında yenisiyle değiştirildiyse (ör. karşılamanın "Bitti"si, "Kural ekle" penceresi) kartları
    /// yeniden kurar: eski kartlar artık listede olmayan kurallara yazardı. Yeniden kurulduysa true.
    /// </summary>
    private bool RebuildIfRulesReplaced()
    {
        if (ReferenceEquals(_builtFrom, AppHost.Settings.Rules)) return false;
        BuildRules();
        ScheduleRecount();
        return true;
    }

    /// <summary>Kural açıldı/kapandı: hemen kaydedilir; bekleyen sayısı kısa bir aradan sonra yeniden hesaplanır.</summary>
    private void OnRuleChanged(RuleRow row)
    {
        AppHost.SaveSettings();
        row.RaiseEnabled();
        RefreshCard();
        ScheduleRecount();
    }

    /// <summary>Masaüstündeki klasörler: diskten değil, arka planda güncel tutulan anlık görüntüden.</summary>
    private static List<string> DesktopFolders() => AppHost.DesktopFolders();

    /// <summary>Kartların durumunu (klasör var mı, kaç dosya bekliyor) son hesaba göre günceller. Ucuz: klasörlerin içine bakmaz.</summary>
    private void RefreshStatuses()
    {
        var folders = DesktopFolders();
        var showPending = !AppHost.Settings.CreateMissingFolders;
        foreach (var row in _rows)
        {
            var name = row.Rule.TargetFolder;
            var exists = !string.IsNullOrWhiteSpace(name) && folders.Any(f => FolderName.Equal(f, name));
            row.SetStatus(exists, _pending.GetValueOrDefault(FolderName.Fold(name)), showPending);
        }
    }

    private void ScheduleRecount()
    {
        _recountDelay.Stop();
        _recountDelay.Start();
    }

    /// <summary>
    /// Klasörü masaüstünde olmayan kurallar için bekleyen dosya sayısı: klasör olsaydı şimdi taşınacak dosyalar.
    /// Arka planda hesaplanır; yalnızca en son isteğin sonucu gösterilir.
    /// </summary>
    private async void Recount()
    {
        _recountDelay.Stop();
        var version = ++_recountVersion;
        var rules = AppHost.Settings.Rules;
        Dictionary<string, int>? result = null;
        try
        {
            result = await Task.Run(() =>
            {
                var files = AppHost.Organizer.SnapshotFiles();
                var plan = MovePlan.Build(files, AppHost.Organizer.ExistingFolders(), rules, rules.Select(r => r.TargetFolder));
                return MovePlan.ByFolder(plan.Where(m => !m.FolderExists))
                    .ToDictionary(x => FolderName.Fold(x.Folder), x => x.Count);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DebugLog.Write($"bekleyen dosyalar sayılamadı: {ex.Message}");
        }
        if (version != _recountVersion || !IsLoaded || result is null) return;
        _pending = result;
        RefreshStatuses();
    }

    /// <summary>"+ Kural ekle": iki adımlı pencere ("Hangi dosyalar?" → "Nereye?"); kuralı o ekler, kartlar burada yenilenir.</summary>
    private async void AddRule_Click(object sender, RoutedEventArgs e)
    {
        await RuleDialog.AddAsync(Window.GetWindow(this));
        AfterRulesChanged();
    }

    private void AfterRulesChanged()
    {
        if (!IsLoaded) return;
        RebuildIfRulesReplaced();
        RefreshCard();
        Recount();
    }

    private void AddDefaults_Click(object sender, RoutedEventArgs e)
    {
        // Masaüstünde öteki dildeki klasör varsa (ör. İngilizce arayüzde "Resimler") kural ona yönelir.
        SetRules(Rule.Defaults(L.Current, DesktopFolders()));
    }

    /// <summary>⋯: "Değiştir…" ve "Sil" (geri alınabilir). Menü düğmenin altında açılır (klavyeyle de).</summary>
    private void RuleMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: RuleRow row } button) return;
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        var edit = new MenuItem { Header = L.T("Değiştir…") };
        AutomationProperties.SetAutomationId(edit, "Rule.Edit");
        edit.Click += async (_, _) =>
        {
            await RuleDialog.EditAsync(Window.GetWindow(this), row.Rule);
            AfterRulesChanged();
        };
        var delete = new MenuItem { Header = L.T("Sil") };
        AutomationProperties.SetAutomationId(delete, "Rule.Delete");
        delete.Click += (_, _) => DeleteRule(row.Rule);
        menu.Items.Add(edit);
        menu.Items.Add(delete);
        menu.IsOpen = true;
    }

    /// <summary>Kuralı siler (soru yok): bildirimdeki "Geri al" onu eski sırasına geri koyar.</summary>
    private void DeleteRule(Rule rule)
    {
        var index = AppHost.Settings.Rules.IndexOf(rule);
        if (index < 0) return;
        SetRules(AppHost.Settings.Rules.Where(r => r != rule).ToList());
        var name = rule.TargetFolder.Trim();
        Notice.Show(name.Length > 0 ? L.F("\"{0}\" kuralı silindi.", name) : L.T("Kural silindi."), NoticeKind.Info, L.T("Geri al"),
            () => RestoreRule(rule, index));
    }

    /// <summary>Silinen kuralı eski sırasına geri koyar (yeni liste atanır).</summary>
    private void RestoreRule(Rule rule, int index)
    {
        var rules = AppHost.Settings.Rules;
        if (rules.Contains(rule)) return;
        var list = new List<Rule>(rules);
        list.Insert(Math.Clamp(index, 0, list.Count), rule);
        SetRules(list);
    }

    /// <summary>Kartın "Oluştur"u: kuralın klasörünü masaüstünde açar (otomatik taşıma açıksa dosyalar hemen gider).</summary>
    private async void CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: RuleRow row } button) return;
        var name = row.Rule.TargetFolder.Trim();
        if (name.Length == 0) return;
        var waiting = row.Pending;
        // Klasör arka planda açılır: bu arada ikinci tık ikinci kez açmaya kalkmasın.
        button.IsEnabled = false;
        bool ok;
        try { ok = (await RuleDialog.CreateDesktopFolderAsync(name)).Ok; }
        finally { button.IsEnabled = true; }
        if (!ok) return;
        if (IsLoaded)
        {
            RefreshCard();
            RefreshStatuses();
            ScheduleRecount();
        }
        if (!AppHost.Settings.Paused)
        {
            AppHost.OrganizeIfActive();
            return;
        }
        // Otomatik taşıma kapalı: klasör açıldı ama dosyalar yerinde; nedenini söyle, istenirse şimdi taşınsın.
        if (waiting > 0)
            Notice.Show(L.P(waiting, "\"{1}\" klasörü oluşturuldu. Otomatik taşıma kapalı olduğu için {0} dosya masaüstünde bekliyor.", name),
                NoticeKind.Info, L.T("Dosyaları şimdi taşı"), () => _ = MoveActions.OrganizeNowAsync());
    }

    // ---- Son taşınanlar ----

    private void RefreshHistory()
    {
        var entries = AppHost.Journal.Snapshot();
        var shown = _showAll ? entries : entries.Take(HistoryLimit);
        HistoryList.ItemsSource = shown.Select(e => new MoveRow(e)).ToList();
        ShowAllButton.Content = L.F("Tümünü göster ({0})", entries.Count);
        ShowAllButton.Visibility = !_showAll && entries.Count > HistoryLimit ? Visibility.Visible : Visibility.Collapsed;
        HistoryEmpty.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UndoLastButton.IsEnabled = AppHost.Journal.LastActive() is not null;
    }

    private void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        _showAll = true;
        RefreshHistory();
    }

    private void UndoRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is MoveRow row) MoveActions.Undo(row.Entry);
    }

    // ---- Gelişmiş ----

    private void CreateMissing_Changed(object sender, RoutedEventArgs e)
    {
        if ((CreateMissing.IsChecked == true) == AppHost.Settings.CreateMissingFolders) return; // koddan gösterilen durum
        AppHost.Settings.CreateMissingFolders = CreateMissing.IsChecked == true;
        AppHost.SaveSettings();
        RefreshStatuses();
        if (AppHost.Settings.CreateMissingFolders) AppHost.OrganizeIfActive();
    }

    /// <summary>
    /// "Varsayılan kurallara dön" (soru yok; dosyalara dokunulmaz): masaüstünde öteki dildeki klasör varsa (ör. İngilizce
    /// arayüzde "Resimler") kural ona yönelir. Bildirimdeki "Geri al" önceki kuralları geri koyar (yeni liste atanır).
    /// </summary>
    private void ResetRules_Click(object sender, RoutedEventArgs e)
    {
        var before = AppHost.Settings.Rules;
        var defaults = Rule.Defaults(L.Current, DesktopFolders());
        SetRules(defaults);
        Notice.Show(L.T("Kurallar varsayılana döndü."), NoticeKind.Success, L.T("Geri al"), () =>
        {
            // Bu arada kurallar başka yerden değiştiyse (ör. kural eklendi) geri alma onu ezmez.
            if (ReferenceEquals(AppHost.Settings.Rules, defaults)) SetRules(before);
        });
    }

    /// <summary>Kural listesini değiştirir (yeni liste atanır: izleyici arka planda eskisini okuyor olabilir).</summary>
    private void SetRules(List<Rule> rules)
    {
        AppHost.Settings.Rules = rules;
        if (IsLoaded) BuildRules();   // kaydetmeden önce: SettingsChanged kartları ikinci kez kurmasın
        AppHost.SaveSettings();
        if (IsLoaded)
        {
            RefreshCard();
            Recount();
        }
    }

    /// <summary>
    /// Geçmiş temizlenir (soru yok): yalnızca geri alınmamış taşıma kayıtları silinir; geri alınanların kaydı ("bir daha
    /// taşıma") kalır, dosyalar yerinde durur. Bildirimdeki "Geri al" silinen kayıtları geri koyar.
    /// </summary>
    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var removed = AppHost.Journal.Clear();
        if (removed.Count == 0) Notice.Show(L.T("Geçmiş temizlendi."), NoticeKind.Success);
        else Notice.Show(L.T("Geçmiş temizlendi."), NoticeKind.Success, L.T("Geri al"), () => AppHost.Journal.Restore(removed));
    }
}
