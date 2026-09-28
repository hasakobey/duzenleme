using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Desktop;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Duzenleme.Widgets;

/// <summary>
/// Fences tarzı bölme. İki kaynaktan birini gösterir:
/// bir masaüstü klasörünün içeriği (ör. PDF; üstüne bırakılan dosya klasöre taşınır) ya da
/// masaüstündeki belli türde öğeler (Klasörler, Kısayollar, Dosyalar). Başlığa çift tıklayınca katlanır.
/// <para>İçerik diskten değil paylaşılan anlık görüntülerden (<see cref="DirectorySnapshot"/>) gelir: aynı klasörü gösteren
/// bütün bölmeler tek izleyiciyi paylaşır, klasör arka planda okunur. Bir şey değişince liste yeniden kurulmaz; yalnızca
/// eklenen/kalkan/yer değiştiren öğeler uygulanır (diğer kutucuklar ve simgeleri yerinde kalır).</para>
/// </summary>
public partial class FenceView : UserControl, IWidgetView
{
    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _searchDelay;
    private readonly List<DirectorySnapshot> _desktopSources = [];
    private DirectorySnapshot? _folderSource;
    private string? _folderPath;

    private ObservableCollection<TileItem> _view = [];            // ekranda gösterilen (arama süzgecinden geçmiş)
    private List<TileItem> _all = [];                             // bölmenin tüm öğeleri, sıralı
    private Dictionary<string, TileItem> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private List<TileItem> _deep = [];                            // aramada alt klasörlerde bulunanlar
    private string _query = "";
    private int _generation, _searchGeneration;
    private bool _updateQueued;
    private bool _rebuildTiles;
    private string? _stamp;
    private string _panelKey = "";
    private int _systemIconsVersion;
    private bool _detached;

    // Yerinde yeniden adlandırma: başlık (F2 ya da yeni eklenen bölme) ve kutucuk (dosya/klasör, diskte).
    private readonly TitleEditor _titleEditor;
    private TileRename? _rename;
    private bool _updateDeferred;                                  // kutucuk düzenlenirken gelen güncelleme, bitince yapılır
    private string? _selectAfterRefresh;                          // yeniden adlandırılan öğe liste yenilenince seçili kalsın
    private (string Path, long Until)? _renameWhenShown;          // "Yeni klasör": listede görününce adı düzenlemeye açılır
    private WidgetPalette _palette = WidgetPalette.Glass;

    public FenceView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        _searchDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); DeepSearch(); };

        SearchIcon.Symbol = SymbolRegular.Search24;
        SearchBarIcon.Symbol = SymbolRegular.Search24;
        SearchBox.TextChanged += (_, _) => OnQueryChanged();
        SearchBox.PreviewKeyDown += OnSearchKey;

        _titleEditor = new TitleEditor(this, Header, TitleText, HeaderIconButton, () => DefaultTitle, CommitTitle, PickIcon, ApplyParts);
        Header.MouseLeftButtonDown += (_, e) =>
        {
            // Başlığa tıklamak seçimi bırakır (Gezgin gibi): F2 artık başlığı adlandırır.
            Items.SelectedItem = null;
            if (e.ClickCount == 2) { e.Handled = true; CollapseToggleRequested?.Invoke(); }
        };
        // Listenin boş yerine tıklamak da seçimi bırakır (kaydırma çubuğu hariç).
        Items.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (Menus.ItemAt(Items, e.OriginalSource) is null && !IsInScrollBar(e.OriginalSource)) Items.SelectedItem = null;
        };
        Items.KeyDown += OnItemsKey;
        PreviewKeyDown += (_, e) =>
        {
            // Ctrl+F: bu bölmede ara (arama düğmesi gizli olsa da).
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && !_titleEditor.IsEditing && _rename is null)
            {
                OpenSearch();
                e.Handled = true;
            }
        };
        Header.SizeChanged += (_, e) => { if (e.WidthChanged) ApplyParts(); };
        // Pencereye bağlanınca (ölçeği artık kesin) simgeler o ekranın piksel boyutunda istenir.
        Loaded += (_, _) => UpdateIconSizes();
        Menus.AttachItemMenu(Items, FillItemMenu);

        // Öğeyi başka bir bölmeye, Gezgin'e ya da bir uygulamaya sürükleyebilmek için.
        Menus.EnableDragOut(Items, DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
        Items.PreviewMouseLeftButtonUp += OnItemClick;
        DragEnter += OnDragEnter;
        DragOver += OnDragOver;
        DragLeave += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        Drop += OnDrop;

        Items.ItemsSource = _view;
        // Masaüstü klasörleri her bölmede izlenir: masaüstü bölmesi içeriği, klasör bölmesi klasörünün yerini buradan bilir.
        foreach (var dir in AppHost.DesktopDirectories)
        {
            var snapshot = AppHost.Snapshots.Acquire(dir);
            snapshot.Changed += ScheduleUpdate;
            _desktopSources.Add(snapshot);
        }
        Update();
    }

    public bool Resizable => true;
    // Başlık satırı gizliyse katlanınca geriye hiçbir şey kalmaz: katlama kapanır.
    public bool Collapsible => _config.Shows("header");
    public Thickness CardPadding => new(14, 12, 14, 12);
    public event Action? CollapseToggleRequested;
    public event Action? MenuRequested;
    public event Action? LayoutChanged;

    public void SetBodyVisible(bool visible) => Body.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private string FolderName => _config.FolderName ?? "PDF";

    private bool DesktopMode => _config.Filter != DesktopFilter.None;

    /// <summary>Kullanıcının masaüstünün anlık görüntüsü (DesktopDirectories'in ilki).</summary>
    private DirectorySnapshot UserDesktop => _desktopSources[0];

    /// <summary>
    /// Kullanıcının masaüstündeki klasör; adı büyük/küçük harf ve Türkçe karakterden bağımsız eşleşir. Diske dokunmaz
    /// (masaüstü anlık görüntüsünden).
    /// </summary>
    private string? ResolveFolder() =>
        UserDesktop.Entries.Where(e => e.IsDirectory && Core.FolderName.Equal(e.Name, FolderName)).Select(e => e.Path).FirstOrDefault();

    private string DefaultTitle => DesktopMode ? DesktopItems.Label(_config.Filter)
        : _folderPath is { } folder ? System.IO.Path.GetFileName(folder) : FolderName;

    private void ScheduleUpdate()
    {
        if (_updateQueued || _detached) return;
        _updateQueued = true;
        Dispatcher.BeginInvoke(Update, DispatcherPriority.Background);
    }

    /// <summary>Kaynaklar ya da ayarlar değişmemiş olsa da listeyi yeniden hesaplar.</summary>
    private void ForceUpdate()
    {
        _stamp = null;
        ScheduleUpdate();
    }

    /// <summary>"Yenile": klasörler diskten yeniden okunur, liste yeniden kurulur.</summary>
    private void Refresh()
    {
        foreach (var source in _desktopSources) source.Request();
        _folderSource?.Request();
        _rebuildTiles = true;
        ForceUpdate();
    }

    /// <summary>Klasör bölmesinin kaynağını (masaüstündeki klasörü) bulur; klasör değiştiyse izlemeyi ona geçirir.</summary>
    private void ResolveSources()
    {
        var wanted = DesktopMode ? null : ResolveFolder();
        if (string.Equals(wanted, _folderPath, StringComparison.OrdinalIgnoreCase)) return;
        if (_folderSource is not null)
        {
            _folderSource.Changed -= ScheduleUpdate;
            AppHost.Snapshots.Release(_folderSource);
            _folderSource = null;
        }
        _folderPath = wanted;
        if (wanted is not null)
        {
            _folderSource = AppHost.Snapshots.Acquire(wanted);
            _folderSource.Changed += ScheduleUpdate;
        }
        _stamp = null;
    }

    /// <summary>Hesabı etkileyen her şeyin özeti: değişmediyse liste yeniden hesaplanmaz.</summary>
    private string Stamp()
    {
        var parts = new List<string> { _config.Filter.ToString(), _config.Sort.ToString(), _config.HiddenItems.Count.ToString(), _systemIconsVersion.ToString() };
        if (DesktopMode) parts.AddRange(_desktopSources.Select(s => $"{s.State}{s.Version}"));
        else parts.Add($"{_folderPath}|{_folderSource?.State}{_folderSource?.Version}");
        return string.Join("|", parts);
    }

    /// <summary>
    /// Bölmeyi anlık görüntüden günceller. Süzme ve sıralama arka planda yapılır; arada yeni bir güncelleme başladıysa eski
    /// sonuç atılır. Sonuç listeye en az değişiklikle uygulanır.
    /// </summary>
    private async void Update()
    {
        _updateQueued = false;
        if (_detached) return;
        // Kutucuğun adı düzenlenirken liste yeniden kurulmaz (kutu ve yazılan kaybolurdu); düzenleme bitince yapılır.
        if (_rename is { IsActive: true })
        {
            _updateDeferred = true;
            return;
        }
        var generation = ++_generation;
        ResolveSources();
        TitleText.Text = !string.IsNullOrWhiteSpace(_config.Title) ? _config.Title : DefaultTitle;
        HeaderIcon.Symbol = WidgetIcons.For(_config);
        ApplyParts();
        ApplyPanel();

        // İlk okuma bitmediyse beklenir (bitince Changed gelir); "klasör yok" gibi yanlış bir durum bir an bile görünmesin,
        // Genel Masaüstü'nün kısayolları da sonradan araya girmesin.
        if (UserDesktop.State == SnapshotState.Pending) return;
        if (DesktopMode && _desktopSources.Any(s => s.State == SnapshotState.Pending)) return;
        if (DesktopMode && UserDesktop.State != SnapshotState.Ready)
        {
            ShowUnavailable(SymbolRegular.Desktop24, UserDesktop.State == SnapshotState.Missing
                ? "Masaüstü klasörü bulunamadı." : "Masaüstü şu an okunamıyor.", showCreate: false);
            return;
        }
        if (!DesktopMode)
        {
            if (_folderSource is null)
            {
                ShowUnavailable(SymbolRegular.FolderProhibited24, $"Masaüstünde \"{FolderName}\" klasörü yok.", showCreate: UserDesktop.State == SnapshotState.Ready);
                return;
            }
            if (_folderSource.State == SnapshotState.Pending) return;
            if (_folderSource.State != SnapshotState.Ready)
            {
                // Klasör tam o anda silindi/taşındı ya da erişilemiyor: hata kutusu yerine sakin bir durum göster.
                ShowUnavailable(SymbolRegular.FolderProhibited24, $"\"{TitleText.Text}\" şu an okunamıyor.", showCreate: false);
                return;
            }
        }

        var stamp = Stamp();
        if (stamp == _stamp && !_rebuildTiles) return;

        var input = new FenceInput(DesktopMode, _config.Filter, _config.Sort,
            DesktopMode ? _desktopSources.Select(s => s.Entries).ToArray() : [_folderSource!.Entries],
            new HashSet<string>(_config.HiddenItems, StringComparer.OrdinalIgnoreCase),
            // Gizleme kayıtları yalnızca okunan kendi klasöründe temizlenir (Genel Masaüstü ve alt klasörler hariç).
            DesktopMode ? AppHost.DesktopDirectory : _folderPath!, DesktopMode ? UserDesktop.Entries : _folderSource!.Entries,
            _config.Filter is DesktopFilter.Shortcuts or DesktopFilter.All);
        var clock = PerfLog.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
        FenceResult result;
        try { result = await Task.Run(() => Compute(input)); }
        catch (Exception ex)
        {
            DebugLog.Write($"bölme hesaplanamadı: {ex}");
            return;
        }
        if (generation != _generation || _detached) return;
        if (_rename is { IsActive: true })
        {
            // Hesap sürerken düzenleme başladı: sonuç sonra yeniden hesaplanır.
            _stamp = null;
            _updateDeferred = true;
            return;
        }
        var computed = clock?.Elapsed.TotalMilliseconds ?? 0;
        Apply(result, stamp);
        if (clock is not null)
        {
            PerfLog.Count("FenceUpdate");
            PerfLog.Write($"bölme güncellendi [{_config.Id[..6]}] {_all.Count} öğe, hesap {computed:0.0} ms, toplam {clock.Elapsed.TotalMilliseconds:0.0} ms");
        }
    }

    private sealed record FenceInput(bool DesktopMode, DesktopFilter Filter, FenceSort Sort, IReadOnlyList<DirEntry>[] Sources,
        HashSet<string> Hidden, string PruneDirectory, IReadOnlyList<DirEntry> PruneEntries, bool SystemIcons);

    private sealed record FenceResult(List<DirEntry> Entries, List<SystemIcon> SystemIcons, List<string> Pruned);

    /// <summary>Süzme, sıralama ve gizleme temizliği (arka planda; diske yalnızca sistem simgeleri için kayıt defterine bakar).</summary>
    private static FenceResult Compute(FenceInput input)
    {
        var entries = input.DesktopMode
            ? input.Sources.SelectMany(s => s.Where(e => DesktopItems.Matches(input.Filter, e)))
            : input.Sources[0].Where(e => !e.IsHiddenOrSystem);
        var sorted = Sort(entries.Where(e => !input.Hidden.Contains(e.Path)), input.Sort).ToList();

        // Masaüstünde gösterilen sistem simgeleri (Bu Bilgisayar, Geri Dönüşüm Kutusu…) de bölmede yer alsın:
        // Windows simgeleri gizliyken başka yerde görünmezler.
        var icons = input.SystemIcons
            ? DesktopSystemIcons.All.Where(DesktopSystemIcons.IsShown).Where(i => !input.Hidden.Contains("::" + i.Clsid)).ToList()
            : [];

        // Silinen/taşınan öğenin gizleme kaydı temizlenir; yoksa sonradan aynı adla gelen yeni dosya da gizlenirdi.
        // Listede olmaması yetmez (bölmenin türü onu süzmüş olabilir): klasörün tam okumasında da olmamalı.
        var present = new HashSet<string>(input.PruneEntries.Select(e => e.Path), StringComparer.OrdinalIgnoreCase);
        var dir = input.PruneDirectory.TrimEnd('\\', '/');
        var pruned = input.Hidden.Where(p => !TileItem.IsShellObject(p) && !present.Contains(p) &&
            string.Equals(System.IO.Path.GetDirectoryName(p), dir, StringComparison.OrdinalIgnoreCase)).ToList();
        return new FenceResult(sorted, icons, pruned);
    }

    private static IEnumerable<DirEntry> Sort(IEnumerable<DirEntry> entries, FenceSort sort)
    {
        var byName = L.Sorter;
        return sort switch
        {
            FenceSort.Name => entries.OrderBy(e => e.IsDirectory ? 0 : 1).ThenBy(e => TileItem.DisplayName(e.Path), byName),
            FenceSort.Type => entries.OrderBy(e => e.IsDirectory ? "" : System.IO.Path.GetExtension(e.Name).ToLowerInvariant()).ThenBy(e => e.Name, byName),
            _ => entries.OrderByDescending(e => e.LastWriteUtc),
        };
    }

    /// <summary>Kutucuk anahtarı: yol ve öznitelikler (klasörleşen ya da bulutta kalan öğe yeni simge alsın).</summary>
    private static string KeyOf(DirEntry entry) => $"{entry.Path}|{(int)entry.Attributes}";

    private void Apply(FenceResult result, string stamp)
    {
        var reuse = _rebuildTiles ? new Dictionary<string, TileItem>() : _byKey;
        var fresh = new Dictionary<string, TileItem>(StringComparer.OrdinalIgnoreCase);
        var all = new List<TileItem>(result.SystemIcons.Count + result.Entries.Count);
        var dpi = IconDpi;
        foreach (var icon in result.SystemIcons)
        {
            var key = "::" + icon.Clsid;
            var item = reuse.GetValueOrDefault(key) ?? TileItem.CreateShell(icon, _config, dpi);
            fresh[key] = item;
            all.Add(item);
        }
        foreach (var entry in result.Entries)
        {
            var key = KeyOf(entry);
            if (fresh.ContainsKey(key)) continue;
            var item = reuse.GetValueOrDefault(key) ?? TileItem.Create(entry.Path, _config, entry.IsDirectory, entry.Attributes, pixelsPerDip: dpi);
            fresh[key] = item;
            all.Add(item);
        }
        _byKey = fresh;
        _all = all;
        _stamp = stamp;
        _rebuildTiles = false;

        if (result.Pruned.Count > 0)
        {
            var pruned = new HashSet<string>(result.Pruned, StringComparer.OrdinalIgnoreCase);
            if (_config.HiddenItems.RemoveAll(pruned.Contains) > 0)
            {
                AppHost.SaveSettings();
                _stamp = Stamp();
            }
        }
        ShowItems();
        if (_query.Length > 0) DeepSearch();
    }

    /// <summary>Bölme gösterilecek bir şey bulamadı (klasör yok, okunamıyor): liste boşaltılır, durum yazılır.</summary>
    private void ShowUnavailable(SymbolRegular icon, string text, bool showCreate)
    {
        _all = [];
        _byKey.Clear();
        _deep = [];
        _stamp = null;
        _fillToken++;
        _view = [];
        Items.ItemsSource = _view;
        ApplyParts(); // sayı rozeti gizlenir (_stamp yok): başlık yeniden sığdırılır
        ShowEmpty(icon, text, showCreate);
    }

    private void ApplyPanel()
    {
        var key = TileItem.PanelKey(_config);
        if (key == _panelKey) return;
        _panelKey = key;
        Items.ItemsPanel = TileItem.Panel(_config);
    }

    /// <summary>
    /// Simgelerin istendiği ekran ölçeği: pencereye bağlıysa bulunduğu monitörünki, değilse (açılış) açılacağı monitörünki.
    /// </summary>
    private double IconDpi => PresentationSource.FromVisual(this) is not null
        ? VisualTreeHelper.GetDpi(this).PixelsPerDip
        : WidgetWindow.ExpectedPixelsPerDip(_config);

    /// <summary>Ekran ya da widget ölçeği değişince simgeler yeni piksel boyutunda istenir (değişmediyse bir şey yapmaz).</summary>
    private void UpdateIconSizes()
    {
        var dpi = IconDpi;
        foreach (var item in _all) item.UpdateIconSize(dpi, _config.Scale);
        foreach (var item in _deep) item.UpdateIconSize(dpi, _config.Scale);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateIconSizes();
    }

    /// <summary>Öğeleri (arama varsa süzülmüş hâliyle) gösterir; ekrandaki listeye yalnızca farkı uygular.</summary>
    private void ShowItems()
    {
        if (_rename is { IsActive: true })
        {
            _updateDeferred = true;
            return;
        }
        var q = Core.FolderName.Fold(_query);
        List<TileItem> shown;
        if (q.Length == 0) shown = _all;
        else
        {
            shown = _all.Where(i => Core.FolderName.Fold(i.Name).Contains(q, StringComparison.Ordinal)).ToList();
            var paths = new HashSet<string>(shown.Select(s => s.Path), StringComparer.OrdinalIgnoreCase);
            shown.AddRange(_deep.Where(d => paths.Add(d.Path)));
        }

        var token = ++_fillToken;
        var steps = ListDiff.Plan(_view, shown);
        if (steps.Count > Math.Max(12, shown.Count / 2))
        {
            // Değişiklik çoksa (ilk dolum, sıralama değişti) yeni liste: öğe öğe bildirimden ucuz. Çok öğeli bölmede
            // (yüzlerce dosyalı masaüstü) kutucuklar parça parça eklenir: tek seferde yüzlerce kutucuk kurmak arayüzü
            // saniyelerce kilitlerdi; böylece ilk simgeler hemen görünür, fare ve diğer widget'lar yanıt vermeye devam eder.
            _view = new ObservableCollection<TileItem>(shown.Count > FillChunk ? shown.Take(FillChunk) : shown);
            Items.ItemsSource = _view;
            if (shown.Count > FillChunk) FillLater(shown, token);
        }
        else if (steps.Count > 0) ListDiff.Apply(_view, steps, _view.Move);

        CountText.Text = q.Length == 0 ? _all.Count.ToString() : $"{shown.Count}";
        ApplyParts(); // sayı rozeti (genişliği de) değişti: başlık yeniden sığdırılır
        if (q.Length > 0 && shown.Count > 0) Items.SelectedIndex = 0;

        if (shown.Count > 0) EmptyState.Visibility = Visibility.Collapsed;
        else if (q.Length > 0) ShowEmpty(SymbolRegular.Search24, $"\"{_query}\" bulunamadı.", showCreate: false);
        else ShowEmpty(EmptyIconFor(), EmptyTextFor(), showCreate: false);
        AfterShow(shown);
    }

    /// <summary>
    /// Liste yenilendikten sonra: yeniden adlandırılan öğe seçili (ve widget etkinse odakta) kalır; "Yeni klasör" listede
    /// görününce adı düzenlemeye açılır (Gezgin gibi).
    /// </summary>
    private void AfterShow(List<TileItem> shown)
    {
        if (_renameWhenShown is { } pending)
        {
            if (shown.FirstOrDefault(i => string.Equals(i.Path, pending.Path, StringComparison.OrdinalIgnoreCase)) is { } created)
            {
                _renameWhenShown = null;
                BeginItemRename(created);
                return;
            }
            if (Environment.TickCount64 > pending.Until) _renameWhenShown = null;
        }
        if (_selectAfterRefresh is not { } path) return;
        if (shown.FirstOrDefault(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)) is not { } item) return;
        _selectAfterRefresh = null;
        Items.SelectedItem = item;
        Items.ScrollIntoView(item);
        if (Window.GetWindow(this)?.IsActive != true) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (Items.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container) container.Focus();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Tek iş dağıtıcı turunda kurulan en fazla kutucuk (yaklaşık bir karelik iş).</summary>
    private const int FillChunk = 100;

    private int _fillToken;

    /// <summary>
    /// Uzun listenin kalanını parça parça, iş dağıtıcı boşaldıkça (ContextIdle) ekler: açılışta önce bütün widget'lar
    /// görünür, sonra dolar; araya yeni bir gösterim girerse bırakır.
    /// </summary>
    private void FillLater(List<TileItem> shown, int token) => Dispatcher.BeginInvoke(() =>
    {
        if (token != _fillToken || _detached) return;
        foreach (var item in shown.Skip(_view.Count).Take(FillChunk)) _view.Add(item);
        if (_view.Count < shown.Count) FillLater(shown, token);
    }, DispatcherPriority.ContextIdle);

    /// <summary>Masaüstü sistem simgeleri (Ayarlar'dan) açılıp kapandı.</summary>
    public void RefreshSystemIcons()
    {
        _systemIconsVersion++;
        if (_config.Filter is DesktopFilter.Shortcuts or DesktopFilter.All) ScheduleUpdate();
    }

    // --- Arama ---

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        if (SearchBar.Visibility == Visibility.Visible) CloseSearch();
        else OpenSearch();
    }

    private void OpenSearch()
    {
        if (_config.Collapsed) CollapseToggleRequested?.Invoke();
        SearchBar.Visibility = Visibility.Visible;
        // Widget masaüstü katmanında ve etkin değil; yazılabilmesi için önce pencere etkinleşmeli.
        (Window.GetWindow(this) as WidgetWindow)?.ActivateForInput();
        Dispatcher.BeginInvoke(() => { SearchBox.Focus(); Keyboard.Focus(SearchBox); }, DispatcherPriority.Input);
    }

    private void CloseSearch()
    {
        SearchBox.Text = "";
        SearchBar.Visibility = Visibility.Collapsed;
    }

    private void OnQueryChanged()
    {
        _query = SearchBox.Text.Trim();
        _deep = [];
        _searchGeneration++;
        ShowItems();
        _searchDelay.Stop();
        if (_query.Length > 0) _searchDelay.Start();
    }

    private void OnSearchKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                CloseSearch();
                e.Handled = true;
                break;
            case Key.Down or Key.Up when Items.Items.Count > 0:
                Items.SelectedIndex = Math.Clamp(Items.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, Items.Items.Count - 1);
                Items.ScrollIntoView(Items.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter when (Items.SelectedItem ?? (Items.Items.Count > 0 ? Items.Items[0] : null)) is TileItem item:
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) TileItem.Reveal(item.Path);
                else TileItem.Launch(item.Path);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Alt klasörlerde arar (arka planda, sınırlı derinlik ve sayıyla): bulunan klasör ya da dosyaya Enter ile gidilir.
    /// Klasör bölmesinde klasörün içinde, "Klasörler" ve "Tümü" bölmesinde masaüstündeki klasörlerin içinde arar.
    /// </summary>
    private async void DeepSearch()
    {
        var generation = ++_searchGeneration;
        var q = Core.FolderName.Fold(_query);
        var roots = _config.Filter switch
        {
            DesktopFilter.None => _folderPath is { } folder ? [folder] : new List<string>(),
            DesktopFilter.Folders or DesktopFilter.All => _all.Where(i => i.IsDirectory && !TileItem.IsShellObject(i.Path)).Select(i => i.Path).ToList(),
            _ => [],
        };
        if (q.Length < 2 || roots.Count == 0) return;

        var hidden = new HashSet<string>(_config.HiddenItems, StringComparer.OrdinalIgnoreCase);
        var found = await Task.Run(() => FindBelow(roots, q, maxDepth: 5, maxResults: 80).Where(e => !hidden.Contains(e.Path)).ToList());
        if (generation != _searchGeneration || _detached) return;
        var dpi = IconDpi;
        _deep = found.Select(e => TileItem.Create(e.Path, _config, e.IsDirectory, e.Attributes, pixelsPerDip: dpi)).ToList();
        ShowItems();
    }

    private static List<DirEntry> FindBelow(List<string> roots, string foldedQuery, int maxDepth, int maxResults)
    {
        var result = new List<DirEntry>();
        var queue = new Queue<(string Dir, int Depth)>(roots.Select(r => (r, 0)));
        var scanned = 0;
        while (queue.Count > 0 && result.Count < maxResults && scanned++ < 4000)
        {
            var (dir, depth) = queue.Dequeue();
            List<FileSystemInfo> entries;
            try { entries = new DirectoryInfo(dir).EnumerateFileSystemInfos().ToList(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            foreach (var entry in entries)
            {
                if ((entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                if (Core.FolderName.Fold(entry.Name).Contains(foldedQuery, StringComparison.Ordinal))
                {
                    result.Add(DirEntry.From(entry));
                    if (result.Count >= maxResults) break;
                }
                // Bağlantı noktalarına (junction) girme: döngü ve yavaşlık olmasın.
                if (entry is DirectoryInfo && depth < maxDepth && (entry.Attributes & FileAttributes.ReparsePoint) == 0)
                    queue.Enqueue((entry.FullName, depth + 1));
            }
        }
        return result;
    }

    private SymbolRegular EmptyIconFor() => DesktopMode ? SymbolRegular.Sparkle24 : SymbolRegular.ArrowDownload24;

    private string EmptyTextFor() => _config.Filter switch
    {
        DesktopFilter.Folders => "Masaüstünde klasör yok.",
        DesktopFilter.Shortcuts => "Masaüstünde kısayol yok.",
        DesktopFilter.Files => "Masaüstünde dosya kalmadı.\nHepsi yerli yerinde!",
        DesktopFilter.All => "Masaüstü boş.",
        _ => "Klasör boş.\nDosyaları buraya sürükleyin.",
    };

    private void ShowEmpty(SymbolRegular icon, string text, bool showCreate)
    {
        EmptyIcon.Symbol = icon;
        EmptyText.Text = text;
        CreateFolderButton.Visibility = showCreate ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
    }

    private bool IsHidden(string path) => _config.HiddenItems.Contains(path, StringComparer.OrdinalIgnoreCase);

    /// <summary>Öğeyi bu bölmede göstermez (dosyaya dokunmaz); "Gizlenen öğeler"den geri getirilir.</summary>
    private void HideItem(TileItem item)
    {
        if (!IsHidden(item.Path)) _config.HiddenItems.Add(item.Path);
        AppHost.SaveSettings();
        _all = _all.Where(i => !string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase)).ToList();
        _deep = _deep.Where(i => !string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase)).ToList();
        _stamp = Stamp();
        ShowItems();
    }

    private void UnhideItems(IEnumerable<string> paths)
    {
        foreach (var path in paths.ToList())
            _config.HiddenItems.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        AppHost.SaveSettings();
        ForceUpdate();
    }

    private static readonly (string Key, string Label)[] FenceParts =
    [
        ("header", "Başlık satırı"), ("count", "Öğe sayısı"), ("search", "Arama düğmesi"),
        ("open", "Klasörü aç düğmesi"), ("divider", "Ayraç çizgisi"), Menus.ClosePart,
    ];

    /// <summary>
    /// Kullanıcının kapattığı parçaları gizler. Kaldırma düğmesi kilitliyken de gizlidir. Dar bölmede başlık okunsun diye
    /// sayı, "Klasörü aç", arama ve başlık simgesi (bu sırayla) geçici olarak gizlenir; kaydedilmez, genişleyince döner.
    /// </summary>
    private void ApplyParts()
    {
        Header.Visibility = _config.Shows("header") ? Visibility.Visible : Visibility.Collapsed;
        HeaderIconButton.Visibility = Visibility.Visible;
        SearchButton.Visibility = _config.Shows("search") ? Visibility.Visible : Visibility.Collapsed;
        OpenButton.Visibility = !DesktopMode && _config.Shows("open") ? Visibility.Visible : Visibility.Collapsed;
        Divider.Visibility = _config.Shows("divider") && _config.Shows("header") ? Visibility.Visible : Visibility.Collapsed;
        CountBadge.Visibility = _config.Shows("count") && _stamp is not null ? Visibility.Visible : Visibility.Collapsed;
        RemoveButton.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
        // Başlık düzenlenirken kutuya yer açılır, simge düğmesi (seçiciyi açar) gizlenmez.
        _titleEditor.Fit([CountBadge, OpenButton, SearchButton, HeaderIconButton], [RemoveButton]);
    }

    private void Items_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Boş alana ya da kaydırma çubuğuna çift tıklamak, daha önce seçilmiş bir öğeyi açmasın.
        if (!_config.SingleClick && Menus.ItemAt(Items, e.OriginalSource) is { } item) TileItem.Launch(item.Path);
    }

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (_config.SingleClick && Menus.ItemAt(Items, e.OriginalSource) is { Missing: false } item)
        {
            TileItem.Launch(item.Path);
            Items.SelectedItem = null;
        }
    }

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_folderPath is { } folder) TileItem.Launch(folder);
    }

    private void CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = System.IO.Path.Combine(AppHost.DesktopDirectory, FolderName);
        // Klasörü biz açıyoruz: "simge ver" balonu çıkmasın; otomatik taşıma kapalıysa dosya da taşınmasın.
        AppHost.MarkQuietFolder(path);
        try { Directory.CreateDirectory(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            AppHost.ConsumeQuietFolder(path);
            MessageBox.Show(ex.Message, AppInfo.Name);
            return;
        }
        AppHost.NoteFolderCreated(path);
        AppHost.OrganizeIfActive();
    }

    private void FillItemMenu(ContextMenu menu, TileItem item)
    {
        var open = Menus.Item(L.T("Aç"), () => TileItem.Launch(item.Path));
        open.InputGestureText = KeyNames.Enter;
        menu.Items.Add(open);
        var remove = Menus.Item("Widget'tan kaldır", () => HideItem(item));
        remove.ToolTip = "Dosyaya dokunulmaz, yalnızca bu widget'ta görünmez.\nGeri getirmek için: widget'a sağ tık → Gizlenen öğeler";
        menu.Items.Add(remove);
        if (TileItem.IsShellObject(item.Path))
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Menus.Item("Bölme ayarları…", () => MenuRequested?.Invoke()));
            return;
        }
        menu.Items.Add(Menus.Item("Klasörde göster", () => TileItem.Reveal(item.Path)));
        if (item.IsDirectory)
        {
            menu.Items.Add(Menus.Item("Klasör simgesi…", () => Icons.FolderIconWindow.ShowFor(item.Path)));
            // Masaüstündeki bir klasör kendi bölmesine alınabilir ("klasörleri ayrı ayrı").
            if (string.Equals(System.IO.Path.GetDirectoryName(item.Path), AppHost.DesktopDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                menu.Items.Add(Menus.Item("Bu klasörü ayrı bölme yap", () => AppHost.Widgets.Add(WidgetKind.Fence, System.IO.Path.GetFileName(item.Path))));
        }
        if (!DesktopMode && !item.IsDirectory && !item.Missing)
            menu.Items.Add(Menus.Item("Masaüstüne geri taşı", () => MoveToDesktop(item)));
        // Masaüstü simgeleri gizliyken (bölmeler yönetirken) bu işler yalnızca buradan yapılabilir.
        var rename = Menus.Item(L.T("Yeniden adlandır"), () => BeginItemRename(item));
        rename.InputGestureText = KeyNames.F2;
        menu.Items.Add(rename);
        var recycle = Menus.Item(L.T("Geri Dönüşüm Kutusu'na taşı"), () => Recycle(item));
        recycle.InputGestureText = KeyNames.Delete;
        menu.Items.Add(recycle);
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item("Bölme ayarları…", () => MenuRequested?.Invoke()));
    }

    /// <summary>Dosya işlemini arka planda yapar; hata olursa (vazgeçme dışında) kullanıcıya gösterir.</summary>
    private void RunFileOperation(Action work)
    {
        var dispatcher = Dispatcher;
        ShellFileOperations.RunSta(work).ContinueWith(t =>
        {
            if (t.Exception?.GetBaseException() is { } ex and not OperationCanceledException)
                dispatcher.BeginInvoke(() => MessageBox.Show(ex.Message, AppInfo.Name));
        }, TaskScheduler.Default);
    }

    // --- Yerinde yeniden adlandırma (F2) ---

    /// <summary>
    /// F2: seçili dosya/klasör (liste odaktayken) diskte, yoksa başlık yeniden adlandırılır. Arama kutusundayken F2 aramaya
    /// aittir; Bu Bilgisayar gibi kabuk nesneleri adlandırılmaz.
    /// </summary>
    public bool TryBeginRename()
    {
        if (_titleEditor.IsEditing || _rename is { IsActive: true }) return true;
        if (SearchBox.IsKeyboardFocusWithin) return false;
        if (Items.IsKeyboardFocusWithin && Items.SelectedItem is TileItem item)
            return !TileItem.IsShellObject(item.Path) && BeginItemRename(item);
        return BeginTitleEdit();
    }

    /// <summary>Başlığı yerinde düzenler; başlık satırı gizliyse küçük pencereyle sorar (widget'ın monitöründe).</summary>
    private bool BeginTitleEdit()
    {
        if (_titleEditor.Begin()) return true;
        var window = Window.GetWindow(this) as WidgetWindow;
        if (InputDialog.Ask(L.T("Bölmeyi yeniden adlandır"), L.T("Ad (boş bırakırsan varsayılan ad kullanılır)"), TitleText.Text,
                window?.CenterPoint) is { } title)
            CommitTitle(string.IsNullOrWhiteSpace(title) || title == DefaultTitle ? null : title);
        return true;
    }

    /// <summary>Yeni başlık (null = varsayılan): kaydedilir, başlık hemen değişir; Widget'lar listesi kayıtla güncellenir.</summary>
    private void CommitTitle(string? title)
    {
        if (string.Equals(_config.Title, title, StringComparison.Ordinal)) return;
        _config.Title = title;
        AppHost.SaveSettings();
        TitleText.Text = title ?? DefaultTitle;
        ApplyParts();
    }

    /// <summary>Simge seçici: widget'ın yanında açılır, seçilen simge başlıkta hemen görünür (kaydedilince yazılır).</summary>
    private void PickIcon()
    {
        if (Window.GetWindow(this) is not WidgetWindow window) return;
        Views.IconPicker.ForWidget(window, _config, TitleText.Text,
            preview: icon => HeaderIcon.Symbol = WidgetIcons.Symbol(icon) ?? WidgetIcons.DefaultFor(_config),
            commit: icon =>
            {
                _config.Icon = icon;
                AppHost.SaveSettings();
                HeaderIcon.Symbol = WidgetIcons.For(_config);
            });
    }

    /// <summary>
    /// Kutucuğun adını diskte yeniden adlandırmak için kutuyu açar (Gezgin gibi: gizli uzantı kutuda görünmez ve korunur,
    /// uzantısı görünen dosyada noktadan öncesi seçilir). Liste düzenleme bitene dek yenilenmez.
    /// </summary>
    private bool BeginItemRename(TileItem item)
    {
        if (TileItem.IsShellObject(item.Path) || item.Missing) return false;
        _rename?.Cancel();
        var split = FileNames.SplitForEditing(item.Path, item.IsDirectory);
        var window = Window.GetWindow(this) as WidgetWindow;
        var path = item.Path;
        var isDirectory = item.IsDirectory;
        TileRename? rename = null;
        rename = TileRename.Begin(Items, item, split.Editable, 0, split.SelectLength, FileNames.MaxName, fileName: true, _palette,
            commit: text => ItemRename.Commit(window, path, isDirectory, split, text,
                hint: message => rename?.ShowHint(message),
                renamed: newPath =>
                {
                    _selectAfterRefresh = newPath;
                    if (_query.Length > 0) DeepSearch(); // alt klasörde bulunan öğe: arama sonucu yeni adla gelsin
                    if (isDirectory) OfferRuleRetarget(path, newPath);
                }),
            ended: () =>
            {
                if (_rename == rename) _rename = null;
                if (!_updateDeferred) return;
                _updateDeferred = false;
                ForceUpdate();
            });
        _rename = rename;
        return true;
    }

    /// <summary>
    /// Otomatik taşıma kurallarının hedeflediği masaüstü klasörü yeniden adlandırıldı: kurallar klasöre adıyla bağlıdır ve
    /// artık oraya taşımaz. Kuralların yeni ada geçmesi önerilir (kendiliğinden yapılmaz).
    /// </summary>
    private static void OfferRuleRetarget(string oldPath, string newPath)
    {
        if (!string.Equals(System.IO.Path.GetDirectoryName(oldPath), AppHost.DesktopDirectory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return;
        var oldName = System.IO.Path.GetFileName(oldPath);
        var newName = System.IO.Path.GetFileName(newPath);
        if (PathRenames.RetargetRules(AppHost.Settings.Rules, oldName, newName) is null) return;
        Views.Notice.Show(L.F("Otomatik taşıma kuralları \"{0}\" klasörüne taşıyordu. Kurallar \"{1}\" klasörüne geçsin mi?", oldName, newName),
            Views.NoticeKind.Info, L.T("Kuralları güncelle"), () =>
            {
                // Yeni liste atanır (izleyici arka planda eski listeyi okuyor olabilir).
                if (PathRenames.RetargetRules(AppHost.Settings.Rules, oldName, newName) is not { } rules) return;
                AppHost.Settings.Rules = rules;
                AppHost.SaveSettings();
            }, L.T("Güncellemek için buraya tıkla."));
    }

    /// <summary>Liste odaktayken: Enter açar, Delete Geri Dönüşüm Kutusu'na gönderir (Windows'un onay ayarıyla).</summary>
    private void OnItemsKey(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is not ListBoxItem || Items.SelectedItem is not TileItem item || Keyboard.Modifiers != ModifierKeys.None) return;
        switch (e.Key)
        {
            case Key.Enter:
                TileItem.Launch(item.Path);
                e.Handled = true;
                break;
            case Key.Delete when !TileItem.IsShellObject(item.Path) && !item.Missing:
                Recycle(item);
                e.Handled = true;
                break;
        }
    }

    private static bool IsInScrollBar(object? source)
    {
        for (var d = source as DependencyObject; d is not null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is System.Windows.Controls.Primitives.ScrollBar) return true;
        return false;
    }

    /// <summary>
    /// Geri Dönüşüm Kutusu'na gönderir (arka planda). Gezgin gibi Windows'un "silme onayı" ayarına uyar; Ortak Masaüstü'ndeki
    /// öğede Windows'un yönetici onayı çıkar. Büyük klasörde Windows kendi ilerleme penceresini gösterir.
    /// </summary>
    private void Recycle(TileItem item)
    {
        var path = item.Path;
        var owner = Window.GetWindow(this) is { } window ? new System.Windows.Interop.WindowInteropHelper(window).Handle : IntPtr.Zero;
        RunFileOperation(() => ShellFileOperations.RecycleWithShell(path, owner));
    }

    /// <summary>
    /// Bölmenin gösterdiği yerde (masaüstü ya da klasör) Gezgin gibi "Yeni klasör" açar (varsa "Yeni klasör (2)") ve listede
    /// görününce adını düzenlemeye açar. Klasör arka planda oluşturulur.
    /// </summary>
    private async void NewFolder()
    {
        var parent = DesktopMode ? AppHost.DesktopDirectory : _folderPath;
        if (parent is null) return;
        var baseName = L.T("Yeni klasör");
        string path;
        try
        {
            path = await Task.Run(() =>
            {
                var name = FileNames.UniqueName(baseName, isDirectory: true,
                    n => Directory.Exists(System.IO.Path.Combine(parent, n)) || File.Exists(System.IO.Path.Combine(parent, n)));
                var created = System.IO.Path.Combine(parent, name);
                Directory.CreateDirectory(created);
                return created;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Views.Notice.Show(L.F("Klasör oluşturulamadı: {0}", ex.Message), Views.NoticeKind.Warning);
            return;
        }
        if (_detached) return;
        _renameWhenShown = (path, Environment.TickCount64 + 5000);
        AppHost.NoteFolderCreated(path);
        ForceUpdate();
    }

    private void MoveToDesktop(TileItem item)
    {
        var source = item.Path;
        RunFileOperation(() =>
        {
            var target = FileMover.UniquePath(AppHost.DesktopDirectory, System.IO.Path.GetFileName(source));
            if (FileMover.SameVolume(source, target)) File.Move(source, target);
            else ShellFileOperations.Move(source, target);
            // Kullanıcı bilerek geri çıkardı: izleyici tekrar taşımasın.
            AppHost.Journal.Add(new MoveEntry { Source = target, Destination = source, Undone = true });
        });
    }

    // --- Sürükle-bırak ---
    // Sürükleme boyunca yollar değişmez: dosya mı diye diske bir kez, arka planda bakılır; DragOver yalnızca metin karşılaştırır.
    private string[]? _dragPaths;
    private bool? _dragHasFiles;

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        _dragPaths = e.Data.GetData(DataFormats.FileDrop) as string[];
        _dragHasFiles = null;
        if (_dragPaths is { Length: > 0 } paths && !DesktopMode)
        {
            Task.Run(() => paths.Any(File.Exists)).ContinueWith(t =>
            {
                if (ReferenceEquals(_dragPaths, paths)) _dragHasFiles = t.IsCompletedSuccessfully && t.Result;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        OnDragOver(sender, e);
    }

    /// <summary>Bırakılırsa klasöre taşınacak yollar (zaten o klasördekiler hariç); diske dokunmaz.</summary>
    private List<string> DropCandidates(string folder) =>
        (_dragPaths ?? []).Where(p => !string.Equals(System.IO.Path.GetDirectoryName(p), folder, StringComparison.OrdinalIgnoreCase)).ToList();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        // Masaüstü türü bölmesi bir klasör değildir; üstüne bırakılan dosyanın gideceği yer yok.
        // Kısayol kutusundan gelen öğeler yalnızca bağlantıdır (taşımaya izin vermez): asıl dosyalar yerinden oynamasın.
        var folder = DesktopMode ? null : _folderPath;
        var ok = folder is not null && e.AllowedEffects.HasFlag(DragDropEffects.Move)
                 && _dragHasFiles != false && DropCandidates(folder).Count > 0;
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        DropOverlay.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (DesktopMode || !e.AllowedEffects.HasFlag(DragDropEffects.Move) || _folderPath is not { } folder) return;
        _dragPaths ??= e.Data.GetData(DataFormats.FileDrop) as string[];
        var paths = DropCandidates(folder);
        _dragPaths = null;
        if (paths.Count == 0) return;

        // Taşıma arka planda: başka sürücüden gelen büyük dosyada Windows ilerleme gösterir, widget'lar donmaz.
        RunFileOperation(() =>
        {
            var errors = new List<string>();
            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue; // klasörler ve kaybolanlar taşınmaz
                try
                {
                    AppHost.Organizer.MoveManually(path, folder, FileMover.SameVolume(path, folder) ? null : ShellFileOperations.Move);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errors.Add(ex.Message);
                }
            }
            if (errors.Count > 0) throw new IOException(string.Join("\n", errors.Distinct()));
        });
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        _titleEditor.ApplyPalette(palette);
        Foreground = palette.Foreground;
        HeaderIcon.Foreground = palette.Accent;
        CountBadge.Background = palette.Accent;
        CountText.Foreground = palette.AccentForeground;
        Divider.Fill = palette.BorderBrush;
        EmptyText.Foreground = palette.Secondary;
        EmptyIcon.Foreground = palette.Secondary;
        OpenButton.Foreground = palette.Secondary;
        SearchButton.Foreground = palette.Secondary;
        SearchBarIcon.Foreground = palette.Secondary;
        SearchBar.BorderBrush = palette.BorderBrush;
        SearchBar.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
        SearchBox.Foreground = palette.Foreground;
        SearchBox.CaretBrush = palette.Foreground;
        DropOverlay.BorderBrush = palette.Accent;
        DropOverlay.Background = new SolidColorBrush(Color.FromArgb(0x55, 0x10, 0x0C, 0x20));
        RemoveButton.Foreground = palette.Foreground;
        ApplyParts(); // kilit kaldırma düğmesini gizleyebilir: başlık yeniden sığdırılır
        UpdateIconSizes(); // ölçek değiştiyse simgeler yeni piksel boyutunda
    }

    /// <summary>Menüden/tekerlekten gelen görünüm ya da içerik ayarı: kaydedilir, kutucuklar yeni ayarla yeniden kurulur.</summary>
    private void Set(Action change)
    {
        change();
        AppHost.SaveSettings();
        _rebuildTiles = true;
        ForceUpdate();
    }

    public bool OnCtrlWheel(int delta)
    {
        Menus.StepIconSize(_config, delta, Set);
        return true;
    }

    public void AddMenuItems(WidgetMenu menu)
    {
        var folder = DesktopMode ? null : _folderPath;
        if (folder is not null)
            menu.Primary.Add(Menus.Item("Klasörü aç", () => TileItem.Launch(folder)));
        if (_config.Filter is DesktopFilter.None or DesktopFilter.Folders or DesktopFilter.All)
            menu.Primary.Add(Menus.Item(L.T("Yeni klasör"), NewFolder));
        var search = Menus.Item(L.T("Bu bölmede ara"), OpenSearch);
        search.InputGestureText = KeyNames.Find;
        menu.Primary.Add(search);

        var pick = new MenuItem { Header = "Ne gösterilsin?" };
        pick.Items.Add(Menus.Hint("Masaüstünden"));
        foreach (var filter in DesktopItems.Filters)
            pick.Items.Add(Menus.Toggle(DesktopItems.Description(filter), _config.Filter == filter,
                () => Set(() => { _config.Filter = filter; _config.Title = null; })));
        var folders = AppHost.DesktopFolders().OrderBy(n => n, L.Sorter).ToList();
        if (folders.Count > 0)
        {
            pick.Items.Add(new Separator());
            pick.Items.Add(Menus.Hint("Bir klasörün içi"));
            foreach (var name in folders)
                pick.Items.Add(Menus.Toggle(name, !DesktopMode && Core.FolderName.Equal(name, FolderName),
                    () => Set(() => { _config.Filter = DesktopFilter.None; _config.FolderName = name; _config.Title = null; })));
        }
        menu.Primary.Add(pick);

        var rename = Menus.Item(L.T("Yeniden adlandır"), () => BeginTitleEdit());
        rename.InputGestureText = KeyNames.F2;
        menu.Primary.Add(rename);
        menu.Primary.Add(Menus.Item(L.T("Simgeyi değiştir…"), PickIcon));
        var sort = Menus.Choice("Sırala", _config.Sort,
            [(FenceSort.Newest, "En yeni üstte"), (FenceSort.Name, "Ada göre"), (FenceSort.Type, "Türe göre")],
            v => Set(() => _config.Sort = v));
        menu.Primary.Add(Menus.TileOptions(_config, Set, singleClickOption: true, first: sort));
        if (_config.HiddenItems.Count > 0)
        {
            var hidden = new MenuItem { Header = $"Gizlenen öğeler ({_config.HiddenItems.Count})" };
            hidden.Items.Add(Menus.Item("Hepsini yeniden göster", () => UnhideItems(_config.HiddenItems)));
            hidden.Items.Add(new Separator());
            foreach (var path in _config.HiddenItems.ToList())
            {
                var label = TileItem.IsShellObject(path)
                    ? Desktop.DesktopSystemIcons.All.FirstOrDefault(i => "::" + i.Clsid == path)?.Name ?? path
                    : TileItem.DisplayName(path);
                hidden.Items.Add(Menus.Item(label + " — göster", () => UnhideItems([path])));
            }
            menu.Primary.Add(hidden);
        }

        menu.Appearance.Add(Menus.Parts(_config, FenceParts, () => { ApplyParts(); LayoutChanged?.Invoke(); }));

        if (folder is not null)
            menu.More.Add(Menus.Item("Klasör simgesi…", () => Icons.FolderIconWindow.ShowFor(folder)));
        menu.More.Add(Menus.Item("Yenile", Refresh));
        // Ayarlar'daki "Windows masaüstü simgeleri" seçimiyle aynı yol (Views.DesktopModes).
        var fencesOnly = Views.DesktopModes.Current == Views.IconMode.FencesOnly;
        menu.More.Add(Menus.Toggle("Masaüstü simgelerini yalnızca bölmelerde göster", fencesOnly,
            () => Views.DesktopModes.Set(fencesOnly ? Views.IconMode.ShowAll : Views.IconMode.FencesOnly, null, null)));
    }

    public void Detach()
    {
        _detached = true;
        _rename?.Cancel();
        _titleEditor.Cancel();
        _searchDelay.Stop();
        foreach (var source in _desktopSources)
        {
            source.Changed -= ScheduleUpdate;
            AppHost.Snapshots.Release(source);
        }
        _desktopSources.Clear();
        if (_folderSource is not null)
        {
            _folderSource.Changed -= ScheduleUpdate;
            AppHost.Snapshots.Release(_folderSource);
            _folderSource = null;
        }
    }
}
