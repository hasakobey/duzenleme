using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Duzenleme.Widgets;

/// <summary>
/// Fences tarzı bölme. İki kaynaktan birini gösterir:
/// bir masaüstü klasörünün içeriği (ör. PDF; üstüne bırakılan dosya klasöre taşınır) ya da
/// masaüstündeki belli türde öğeler (Klasörler, Kısayollar, Dosyalar). Başlığa çift tıklayınca katlanır.
/// </summary>
public partial class FenceView : UserControl, IWidgetView
{
    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _sourcePoll;
    private readonly List<ResilientWatcher> _watchers = [];
    private readonly DispatcherTimer _searchDelay;
    private string _watchedKey = "";

    private List<TileItem> _all = [];      // bölmenin tüm öğeleri
    private List<TileItem> _deep = [];     // aramada alt klasörlerde bulunanlar
    private string _query = "";
    private int _generation, _searchGeneration;

    public FenceView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _refreshTimer.Tick += (_, _) => { _refreshTimer.Stop(); Refresh(); };
        // Klasör masaüstünde sonradan oluşturulur/silinir/yeniden adlandırılırsa fark et.
        _sourcePoll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _sourcePoll.Tick += (_, _) => { if (WatchKey(Sources()) != _watchedKey) Refresh(); };
        _sourcePoll.Start();
        _searchDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); DeepSearch(); };

        SearchIcon.Symbol = SymbolRegular.Search24;
        SearchBarIcon.Symbol = SymbolRegular.Search24;
        SearchBox.TextChanged += (_, _) => OnQueryChanged();
        SearchBox.PreviewKeyDown += OnSearchKey;

        Header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { e.Handled = true; CollapseToggleRequested?.Invoke(); }
        };
        Menus.AttachItemMenu(Items, FillItemMenu);
        // Öğeyi başka bir bölmeye, Gezgin'e ya da bir uygulamaya sürükleyebilmek için.
        Menus.EnableDragOut(Items, DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
        Items.PreviewMouseLeftButtonUp += OnItemClick;
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        Drop += OnDrop;
        AppHost.Organizer.FileMoved += OnAnyMove;
        AppHost.Journal.Changed += OnJournalChanged;

        Refresh();
    }

    public bool Resizable => true;
    public bool Collapsible => true;
    public Thickness CardPadding => new(14, 12, 14, 12);
    public event Action? CollapseToggleRequested;
    public event Action? MenuRequested;

    public void SetBodyVisible(bool visible) => Body.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private string FolderName => _config.FolderName ?? "PDF";

    private bool DesktopMode => _config.Filter != DesktopFilter.None;

    /// <summary>Kullanıcının masaüstündeki klasörü; adı büyük/küçük harf ve Türkçe karakterden bağımsız eşleşir.</summary>
    private string? ResolveFolder() =>
        AppHost.Organizer.ExistingFolders()
            .Where(f => Core.FolderName.Equal(f, FolderName))
            .Select(f => System.IO.Path.Combine(AppHost.DesktopDirectory, f))
            .FirstOrDefault();

    /// <summary>Bölmenin okuduğu klasörler: masaüstü türü için masaüstü (+ Genel Masaüstü), yoksa seçilen klasör.</summary>
    private List<string> Sources()
    {
        if (DesktopMode) return AppHost.DesktopDirectories.Where(Directory.Exists).ToList();
        return ResolveFolder() is { } folder ? [folder] : [];
    }

    private static string WatchKey(List<string> sources) => string.Join("|", sources);

    private string DefaultTitle => DesktopMode ? DesktopItems.Label(_config.Filter)
        : ResolveFolder() is { } folder ? System.IO.Path.GetFileName(folder) : FolderName;

    /// <summary>
    /// Bölmeyi yeniden doldurur. Klasör taraması arka planda yapılır (büyük/ağ klasörlerinde arayüz donmasın);
    /// arada yeni bir yenileme başladıysa eski sonuç atılır.
    /// </summary>
    private async void Refresh()
    {
        var generation = ++_generation;
        var sources = Sources();
        TitleText.Text = !string.IsNullOrWhiteSpace(_config.Title) ? _config.Title : DefaultTitle;
        HeaderIcon.Symbol = _config.Filter switch
        {
            DesktopFilter.Shortcuts => SymbolRegular.Apps24,
            DesktopFilter.Files => SymbolRegular.DocumentMultiple24,
            DesktopFilter.All => SymbolRegular.Desktop24,
            _ => SymbolRegular.Folder24,
        };
        OpenButton.Visibility = DesktopMode ? Visibility.Collapsed : Visibility.Visible;
        EnsureWatchers(sources);
        Items.ItemsPanel = TileItem.Panel(_config);

        if (sources.Count == 0)
        {
            _all = [];
            Items.ItemsSource = null;
            CountBadge.Visibility = Visibility.Collapsed;
            if (DesktopMode) ShowEmpty(SymbolRegular.Desktop24, "Masaüstü klasörü bulunamadı.", showCreate: false);
            else ShowEmpty(SymbolRegular.FolderProhibited24, $"Masaüstünde \"{FolderName}\" klasörü yok.", showCreate: true);
            return;
        }

        List<string> paths;
        try
        {
            paths = await Task.Run(() => Sort(Enumerate(sources)).Select(i => i.FullName).ToList());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (generation != _generation) return;
            // Klasör tam o anda silindi/taşındı ya da erişilemiyor: hata kutusu yerine sakin bir durum göster.
            _all = [];
            Items.ItemsSource = null;
            CountBadge.Visibility = Visibility.Collapsed;
            ShowEmpty(SymbolRegular.FolderProhibited24, $"\"{TitleText.Text}\" şu an okunamıyor.", showCreate: false);
            return;
        }
        if (generation != _generation) return;

        _all = paths.Select(p => TileItem.Create(p, _config)).ToList();
        CountBadge.Visibility = Visibility.Visible;
        ShowItems();
        if (_query.Length > 0) DeepSearch();
    }

    /// <summary>Öğeleri (arama varsa süzülmüş hâliyle) gösterir.</summary>
    private void ShowItems()
    {
        var q = Core.FolderName.Fold(_query);
        List<TileItem> shown;
        if (q.Length == 0) shown = _all;
        else
        {
            shown = _all.Where(i => Core.FolderName.Fold(i.Name).Contains(q, StringComparison.Ordinal)).ToList();
            shown.AddRange(_deep.Where(d => !shown.Any(s => string.Equals(s.Path, d.Path, StringComparison.OrdinalIgnoreCase))));
        }

        Items.ItemsSource = shown;
        CountText.Text = q.Length == 0 ? _all.Count.ToString() : $"{shown.Count}";
        if (q.Length > 0 && shown.Count > 0) Items.SelectedIndex = 0;

        if (shown.Count > 0) EmptyState.Visibility = Visibility.Collapsed;
        else if (q.Length > 0) ShowEmpty(SymbolRegular.Search24, $"\"{_query}\" bulunamadı.", showCreate: false);
        else ShowEmpty(EmptyIconFor(), EmptyTextFor(), showCreate: false);
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
            DesktopFilter.None => Sources(),
            DesktopFilter.Folders or DesktopFilter.All => _all.Where(i => Directory.Exists(i.Path)).Select(i => i.Path).ToList(),
            _ => [],
        };
        if (q.Length < 2 || roots.Count == 0) return;

        var found = await Task.Run(() => FindBelow(roots, q, maxDepth: 5, maxResults: 80));
        if (generation != _searchGeneration) return;
        _deep = found.Select(p => TileItem.Create(p, _config)).ToList();
        ShowItems();
    }

    private static List<string> FindBelow(List<string> roots, string foldedQuery, int maxDepth, int maxResults)
    {
        var result = new List<string>();
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
                    result.Add(entry.FullName);
                    if (result.Count >= maxResults) break;
                }
                // Bağlantı noktalarına (junction) girme: döngü ve yavaşlık olmasın.
                if (entry is DirectoryInfo && depth < maxDepth && (entry.Attributes & FileAttributes.ReparsePoint) == 0)
                    queue.Enqueue((entry.FullName, depth + 1));
            }
        }
        return result;
    }

    private IEnumerable<FileSystemInfo> Enumerate(List<string> sources)
    {
        if (!DesktopMode)
            return new DirectoryInfo(sources[0]).EnumerateFileSystemInfos()
                .Where(i => (i.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .ToList();

        var result = new List<FileSystemInfo>();
        foreach (var dir in sources)
        {
            // Genel Masaüstü okunamazsa kullanıcının masaüstü yine gösterilsin.
            try { result.AddRange(new DirectoryInfo(dir).EnumerateFileSystemInfos().Where(i => DesktopItems.Matches(_config.Filter, i))); }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && dir != AppHost.DesktopDirectory) { }
        }
        return result;
    }

    private IEnumerable<FileSystemInfo> Sort(IEnumerable<FileSystemInfo> entries)
    {
        var byName = StringComparer.Create(Views.UiText.Tr, true);
        return _config.Sort switch
        {
            FenceSort.Name => entries.OrderBy(i => i is DirectoryInfo ? 0 : 1).ThenBy(i => TileItem.DisplayName(i.FullName), byName),
            FenceSort.Type => entries.OrderBy(i => i is DirectoryInfo ? "" : i.Extension.ToLowerInvariant()).ThenBy(i => i.Name, byName),
            _ => entries.OrderByDescending(i => i.LastWriteTime),
        };
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

    private void EnsureWatchers(List<string> sources)
    {
        var key = WatchKey(sources);
        if (key == _watchedKey) return;
        foreach (var watcher in _watchers) watcher.Dispose();
        _watchers.Clear();
        _watchedKey = key;
        foreach (var dir in sources)
        {
            var watcher = new ResilientWatcher(dir, NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                _ => QueueRefresh(), onOverflow: QueueRefresh, onRecovered: QueueRefresh);
            watcher.Start();
            _watchers.Add(watcher);
        }
    }

    private void QueueRefresh() => Dispatcher.BeginInvoke(() => { _refreshTimer.Stop(); _refreshTimer.Start(); });

    private void OnAnyMove(MoveEntry _) => QueueRefresh();

    private void OnJournalChanged() => QueueRefresh();

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

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ResolveFolder() is { } folder) TileItem.Launch(folder);
    }

    private void CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(System.IO.Path.Combine(AppHost.DesktopDirectory, FolderName));
        Refresh();
        AppHost.OrganizeNowInBackground();
    }

    private void FillItemMenu(ContextMenu menu, TileItem item)
    {
        menu.Items.Add(Menus.Item("Aç", () => TileItem.Launch(item.Path)));
        menu.Items.Add(Menus.Item("Klasörde göster", () => TileItem.Reveal(item.Path)));
        if (Directory.Exists(item.Path))
        {
            menu.Items.Add(Menus.Item("Klasör simgesi…", () => Icons.FolderIconWindow.ShowFor(item.Path)));
            // Masaüstündeki bir klasör kendi bölmesine alınabilir ("klasörleri ayrı ayrı").
            if (string.Equals(System.IO.Path.GetDirectoryName(item.Path), AppHost.DesktopDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                menu.Items.Add(Menus.Item("Bu klasörü ayrı bölme yap", () => AppHost.Widgets.Add(WidgetKind.Fence, System.IO.Path.GetFileName(item.Path))));
        }
        if (!DesktopMode && File.Exists(item.Path))
            menu.Items.Add(Menus.Item("Masaüstüne geri taşı", () => MoveToDesktop(item)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item("Bölme ayarları…", () => MenuRequested?.Invoke()));
    }

    private static void MoveToDesktop(TileItem item)
    {
        try
        {
            var target = FileMover.UniquePath(AppHost.DesktopDirectory, System.IO.Path.GetFileName(item.Path));
            File.Move(item.Path, target);
            // Kullanıcı bilerek geri çıkardı: izleyici tekrar taşımasın.
            AppHost.Journal.Add(new MoveEntry { Source = target, Destination = item.Path, Undone = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "Düzenleme");
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        // Masaüstü türü bölmesi bir klasör değildir; üstüne bırakılan dosyanın gideceği yer yok.
        // Kısayol kutusundan gelen öğeler yalnızca bağlantıdır (taşımaya izin vermez): asıl dosyalar yerinden oynamasın.
        var folder = DesktopMode ? null : ResolveFolder();
        var ok = folder is not null && e.AllowedEffects.HasFlag(DragDropEffects.Move)
                 && e.Data.GetData(DataFormats.FileDrop) is string[] paths
                 && paths.Any(p => File.Exists(p) && !string.Equals(System.IO.Path.GetDirectoryName(p), folder, StringComparison.OrdinalIgnoreCase));
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        DropOverlay.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (DesktopMode || !e.AllowedEffects.HasFlag(DragDropEffects.Move) || ResolveFolder() is not { } folder
            || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        foreach (var path in paths.Where(File.Exists))
        {
            if (string.Equals(System.IO.Path.GetDirectoryName(path), folder, StringComparison.OrdinalIgnoreCase)) continue;
            try { AppHost.Organizer.MoveManually(path, folder); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(ex.Message, "Düzenleme");
            }
        }
        Refresh();
    }

    public void ApplyPalette(WidgetPalette palette)
    {
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
    }

    private void Set(Action change)
    {
        change();
        AppHost.SaveSettings();
        Refresh();
    }

    public bool OnCtrlWheel(int delta)
    {
        Menus.StepIconSize(_config, delta, Set);
        return true;
    }

    public void AddMenuItems(ContextMenu menu)
    {
        if (!DesktopMode && ResolveFolder() is { } folder)
        {
            menu.Items.Add(Menus.Item("Klasörü aç", () => TileItem.Launch(folder)));
            menu.Items.Add(Menus.Item("Klasör simgesi…", () => Icons.FolderIconWindow.ShowFor(folder)));
        }
        menu.Items.Add(Menus.Item("Başlığı değiştir…", () =>
        {
            if (InputDialog.Ask("Bölme başlığı", "Başlık (boş bırakırsan varsayılan ad kullanılır)", TitleText.Text) is { } title)
                Set(() => _config.Title = string.IsNullOrWhiteSpace(title) || title == DefaultTitle ? null : title);
        }));

        var pick = new MenuItem { Header = "Ne gösterilsin?" };
        pick.Items.Add(Menus.Hint("Masaüstünden"));
        foreach (var filter in DesktopItems.Filters)
            pick.Items.Add(Menus.Toggle(DesktopItems.Description(filter), _config.Filter == filter,
                () => Set(() => { _config.Filter = filter; _config.Title = null; })));
        var folders = AppHost.Organizer.ExistingFolders().OrderBy(n => n, StringComparer.Create(Views.UiText.Tr, true)).ToList();
        if (folders.Count > 0)
        {
            pick.Items.Add(new Separator());
            pick.Items.Add(Menus.Hint("Bir klasörün içi"));
            foreach (var name in folders)
                pick.Items.Add(Menus.Toggle(name, !DesktopMode && Core.FolderName.Equal(name, FolderName),
                    () => Set(() => { _config.Filter = DesktopFilter.None; _config.FolderName = name; _config.Title = null; })));
        }
        menu.Items.Add(pick);

        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Choice("Sırala", _config.Sort,
            [(FenceSort.Newest, "En yeni üstte"), (FenceSort.Name, "Ada göre"), (FenceSort.Type, "Türe göre")],
            v => Set(() => _config.Sort = v)));
        menu.Items.Add(Menus.TileOptions(_config, Set, singleClickOption: true));
        menu.Items.Add(Menus.Item("Yenile", Refresh));
    }

    public void Detach()
    {
        _refreshTimer.Stop();
        _sourcePoll.Stop();
        foreach (var watcher in _watchers) watcher.Dispose();
        _watchers.Clear();
        AppHost.Organizer.FileMoved -= OnAnyMove;
        AppHost.Journal.Changed -= OnJournalChanged;
    }
}
