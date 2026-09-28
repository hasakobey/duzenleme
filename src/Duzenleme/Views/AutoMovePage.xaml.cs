using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Wpf.Ui.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace Duzenleme.Views;

/// <summary>
/// Kural düzenleme satırı; değişiklikler anında ayarlara yazılır. Durum (klasör masaüstünde var mı, kaç dosya bekliyor)
/// sayfa tarafından <see cref="SetStatus"/> ile verilir: satır diske kendisi bakmaz.
/// </summary>
public sealed class RuleRow(Rule rule, int index, Action<RuleRow> changed) : INotifyPropertyChanged
{
    private static readonly string[] StatusProperties =
    [
        nameof(StatusIcon), nameof(StatusFilled), nameof(StatusBrush), nameof(StatusText), nameof(CreateVisibility),
        nameof(RowOpacity), nameof(EnabledName), nameof(DeleteName), nameof(CreateName),
    ];

    private bool _exists;
    private int _pending;
    private bool _showPending = true;

    public Rule Rule { get; } = rule;
    public event PropertyChangedEventHandler? PropertyChanged;

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

    public string TargetFolder
    {
        get => Rule.TargetFolder;
        set
        {
            var clean = string.Concat(value.Trim().Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            if (clean != Rule.TargetFolder)
            {
                Rule.TargetFolder = clean;
                changed(this);
            }
            // Ayıklanan karakterler kutudan da silinsin.
            Raise(nameof(TargetFolder));
        }
    }

    public string ExtensionsText
    {
        get => string.Join(", ", Rule.Extensions);
        set
        {
            var list = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(Rule.NormalizeExtension).Where(e => e.Length > 0).Distinct().ToList();
            if (!list.SequenceEqual(Rule.Extensions))
            {
                Rule.Extensions = list;
                changed(this);
            }
            // Kutuda düzgün biçim görünsün ("*.PDF;.Png" → "pdf, png").
            Raise(nameof(ExtensionsText));
        }
    }

    public int Pending => _pending;

    private bool HasFolderName => !string.IsNullOrWhiteSpace(Rule.TargetFolder);

    public SymbolRegular StatusIcon => _exists ? SymbolRegular.CheckmarkCircle24 : SymbolRegular.Circle24;
    public bool StatusFilled => _exists;
    public Brush StatusBrush => (Brush)Application.Current.FindResource(_exists ? "SystemFillColorSuccessBrush" : "TextFillColorTertiaryBrush");

    public string StatusText =>
        !Rule.Enabled ? "Kapalı"
        : !HasFolderName ? "Klasör adı yok; hiçbir dosya taşınmaz"
        : Rule.Extensions.Count == 0 ? "Uzantı yok; hiçbir dosya taşınmaz"
        : _exists ? "Masaüstünde var"
        : _showPending && _pending > 0 ? $"Masaüstünde yok · {_pending} dosya bekliyor"
        : "Masaüstünde yok";

    public Visibility CreateVisibility => _exists || !HasFolderName ? Visibility.Collapsed : Visibility.Visible;
    public double RowOpacity => Rule.Enabled ? 1 : 0.6;

    // UI Automation: her satırın denetimleri sırasıyla bulunabilsin (Rule.0.Folder, Rule.0.Delete…).
    public string EnabledId => $"Rule.{index}.Enabled";
    public string FolderId => $"Rule.{index}.Folder";
    public string ExtensionsId => $"Rule.{index}.Extensions";
    public string DeleteId => $"Rule.{index}.Delete";
    public string CreateId => $"Rule.{index}.CreateFolder";
    public string StatusId => $"Rule.{index}.Status";
    public string EnabledName => $"\"{Rule.TargetFolder}\" kuralı";
    public string DeleteName => $"Kuralı sil: {Rule.TargetFolder}";
    public string CreateName => $"Klasörü oluştur: {Rule.TargetFolder}";

    /// <summary>Klasörün masaüstündeki durumu ve bekleyen dosya sayısı (showPending: "Klasör yoksa oluştur" kapalıyken).</summary>
    internal void SetStatus(bool exists, int pending, bool showPending)
    {
        _exists = exists;
        _pending = pending;
        _showPending = showPending;
        // Metin kutularının bağları yenilenmez: kullanıcı o an yazıyorsa yazdığı silinmesin.
        foreach (var name in StatusProperties) Raise(name);
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Otomatik taşıma: durum, kurallar (klasör var/yok, bekleyen dosya), son taşınanlar ve gelişmiş ayarlar. Eski Kurallar
/// ve Geçmiş sayfalarının yerini alır.
/// </summary>
public partial class AutoMovePage : Page
{
    private const int HistoryLimit = 10;

    // "Geçmişi gör": sayfa açılınca (ya da zaten açıksa hemen) Son taşınanlar'a kaydırılır.
    private static AutoMovePage? _current;
    private static bool _pendingHistory;

    private readonly DispatcherTimer _recountDelay = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private List<RuleRow> _rows = [];
    /// <summary>Satırların kurulduğu liste: ayarlardaki liste başka yerden yenisiyle değiştirilince satırlar yeniden kurulur.</summary>
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
        // Ana pencere gizliyken sayfa kayıtlara, taşımalara ve pencere etkinleşmesine tepki vermez (masaüstünü de saymaz);
        // yeniden görününce bir kez güncellenir.
        PageLife.WhileShown(this,
            attach: () =>
            {
                AppHost.SettingsChanged += OnSettingsChanged;
                AppHost.Journal.Changed += OnJournalChanged;
                WatchHostActivation(true);
            },
            detach: () =>
            {
                AppHost.SettingsChanged -= OnSettingsChanged;
                AppHost.Journal.Changed -= OnJournalChanged;
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

    // Kullanıcı Gezgin'de klasör açıp geri dönünce durumlar güncellensin (masaüstü burada izlenmiyor).
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

    // ---- Durum kartı ----

    // Ayarlar her kaydedildiğinde (widget taşımak da kaydeder): kart; kural listesi yalnızca başka yerden yenisiyle
    // değiştirildiyse yeniden kurulur (satır içi düzenlemede odak kaybolmasın).
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
    }

    private void AutoMoveToggle_Click(object sender, RoutedEventArgs e) => AppHost.SetPaused(AutoMoveToggle.IsChecked != true);

    private async void OrganizeNow_Click(object sender, RoutedEventArgs e)
    {
        OrganizeButton.IsEnabled = false;
        try { await MoveActions.OrganizeNowAsync(); }
        finally { OrganizeButton.IsEnabled = true; }
    }

    // ---- Kurallar ----

    private void BuildRules()
    {
        _builtFrom = AppHost.Settings.Rules;
        _rows = _builtFrom.Select((rule, i) => new RuleRow(rule, i, OnRuleChanged)).ToList();
        RefreshStatuses();
        RuleList.ItemsSource = _rows;
        var empty = _rows.Count == 0;
        NoRules.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        RuleHeader.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Kural listesi bu sayfa dışında yenisiyle değiştirildiyse (ör. karşılamanın "Bitti"si) satırları yeniden kurar: eski
    /// satırlar artık listede olmayan kurallara yazar, değişiklikler sessizce kaybolurdu. Yeniden kurulduysa true.
    /// </summary>
    private bool RebuildIfRulesReplaced()
    {
        if (ReferenceEquals(_builtFrom, AppHost.Settings.Rules)) return false;
        BuildRules();
        ScheduleRecount();
        return true;
    }

    /// <summary>Satır değişti: hemen kaydedilir; bekleyen sayısı kısa bir aradan sonra yeniden hesaplanır.</summary>
    private void OnRuleChanged(RuleRow row)
    {
        AppHost.SaveSettings();
        RefreshStatuses();
        ScheduleRecount();
    }

    /// <summary>Masaüstündeki klasörler: diskten değil, arka planda güncel tutulan anlık görüntüden.</summary>
    private static List<string> DesktopFolders() => AppHost.DesktopFolders();

    /// <summary>Satırların durumunu (klasör var mı, kaç dosya bekliyor) son hesaba göre günceller. Ucuz: klasörlerin içine bakmaz.</summary>
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

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        // Liste yerine yenisi atanır: izleyici iş parçacığı eski listeyi güvenle okumaya devam eder.
        AppHost.Settings.Rules = [.. AppHost.Settings.Rules, new Rule { TargetFolder = "Yeni klasör", Extensions = [] }];
        BuildRules();   // kaydetmeden önce: SettingsChanged satırları ikinci kez kurmasın
        AppHost.SaveSettings();
        var row = _rows[^1];
        Dispatcher.BeginInvoke(() => FocusFolderBox(row), DispatcherPriority.ContextIdle);
    }

    /// <summary>Yeni kuralın klasör kutusunu odaklar ve metnini seçer: kullanıcı hemen adını yazabilsin.</summary>
    private void FocusFolderBox(RuleRow row)
    {
        if (RuleList.ItemContainerGenerator.ContainerFromItem(row) is not ContentPresenter presenter) return;
        presenter.ApplyTemplate();
        if (presenter.ContentTemplate?.FindName("FolderBox", presenter) is not TextBox box) return;
        box.BringIntoView();
        box.Focus();
        box.SelectAll();
    }

    private void AddDefaults_Click(object sender, RoutedEventArgs e)
    {
        // Masaüstünde öteki dildeki klasör varsa (ör. İngilizce arayüzde "Resimler") kural ona yönelir.
        AppHost.Settings.Rules = Rule.Defaults(L.Current, DesktopFolders());
        BuildRules();   // kaydetmeden önce: SettingsChanged satırları ikinci kez kurmasın
        AppHost.SaveSettings();
        Recount();
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RuleRow row) return;
        var rule = row.Rule;
        var index = AppHost.Settings.Rules.IndexOf(rule);
        AppHost.Settings.Rules = AppHost.Settings.Rules.Where(r => r != rule).ToList();
        BuildRules();   // kaydetmeden önce: SettingsChanged satırları ikinci kez kurmasın
        AppHost.SaveSettings();
        ScheduleRecount();
        var name = rule.TargetFolder.Trim();
        Notice.Show(name.Length > 0 ? $"\"{name}\" kuralı silindi." : "Kural silindi.", NoticeKind.Info, "Geri al", () => RestoreRule(rule, index));
    }

    /// <summary>Silinen kuralı eski sırasına geri koyar (yeni liste atanır).</summary>
    private void RestoreRule(Rule rule, int index)
    {
        var rules = AppHost.Settings.Rules;
        if (rules.Contains(rule)) return;
        var list = new List<Rule>(rules);
        list.Insert(Math.Clamp(index, 0, list.Count), rule);
        AppHost.Settings.Rules = list;
        if (IsLoaded) BuildRules();   // kaydetmeden önce: SettingsChanged satırları ikinci kez kurmasın
        AppHost.SaveSettings();
        if (IsLoaded) ScheduleRecount();
    }

    private void CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RuleRow row) return;
        var name = row.Rule.TargetFolder.Trim();
        if (name.Length == 0) return;
        var waiting = row.Pending;
        var path = Path.Combine(AppHost.DesktopDirectory, name);
        try
        {
            // Uygulamanın açtığı klasör için "simge ver" balonu çıkmaz.
            AppHost.MarkQuietFolder(path);
            Directory.CreateDirectory(path);
            AppHost.NoteFolderCreated(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            try { AppHost.ConsumeQuietFolder(path); }
            catch (ArgumentException) { }
            Notice.Show($"\"{name}\" klasörü oluşturulamadı: {ex.Message}", NoticeKind.Error);
            return;
        }
        RefreshCard();
        RefreshStatuses();
        ScheduleRecount();
        if (!AppHost.Settings.Paused)
        {
            AppHost.OrganizeIfActive();
            return;
        }
        // Otomatik taşıma kapalı: klasör açıldı ama dosyalar yerinde; nedenini söyle, istenirse şimdi taşınsın.
        if (waiting > 0)
            Notice.Show($"\"{name}\" klasörü oluşturuldu. Otomatik taşıma kapalı olduğu için {waiting} dosya masaüstünde bekliyor.",
                NoticeKind.Info, "Şimdi düzenle", () => _ = MoveActions.OrganizeNowAsync());
    }

    // ---- Son taşınanlar ----

    private void RefreshHistory()
    {
        var entries = AppHost.Journal.Snapshot();
        var shown = _showAll ? entries : entries.Take(HistoryLimit);
        HistoryList.ItemsSource = shown.Select(e => new MoveRow(e)).ToList();
        ShowAllButton.Content = $"Tümünü göster ({entries.Count})";
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

    private void UndoLast_Click(object sender, RoutedEventArgs e) => MoveActions.Undo(AppHost.Journal.LastActive());

    // ---- Gelişmiş ----

    private void CreateMissing_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.CreateMissingFolders = CreateMissing.IsChecked == true;
        AppHost.SaveSettings();
        RefreshStatuses();
        if (AppHost.Settings.CreateMissingFolders) AppHost.OrganizeIfActive();
    }

    private void ResetRules_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm.Ask(Window.GetWindow(this), "Kurallar varsayılana dönsün mü?",
                "Eklediğin ya da değiştirdiğin kurallar silinir; PDF, Resimler, Belgeler, Arşivler, Videolar ve Müzik kuralları geri gelir. Dosyalarına dokunulmaz.",
                "Varsayılana döndür"))
            return;
        AppHost.Settings.Rules = Rule.Defaults(L.Current, DesktopFolders());
        BuildRules();   // kaydetmeden önce: SettingsChanged satırları ikinci kez kurmasın
        AppHost.SaveSettings();
        Recount();
        Notice.Show("Kurallar varsayılana döndü.", NoticeKind.Success);
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (!Confirm.Ask(Window.GetWindow(this), "Geçmiş temizlensin mi?",
                "Listedeki taşımalar silinir ve artık buradan geri alınamaz. Dosyaların bulundukları klasörde kalır. Geri aldığın dosyaların kaydı korunur; onlar yine taşınmaz.",
                "Temizle"))
            return;
        AppHost.Journal.Clear();
        Notice.Show("Geçmiş temizlendi.", NoticeKind.Success);
    }
}
