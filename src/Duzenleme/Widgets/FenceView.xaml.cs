using System.Diagnostics;
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
/// Fences tarzı bölme: masaüstündeki bir klasörün (ör. PDF) içeriğini masaüstünde gösterir.
/// Dosya sürükleyip bırakınca klasöre taşır; başlığa çift tıklayınca katlanır.
/// </summary>
public partial class FenceView : UserControl, IWidgetView
{
    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _folderPoll;
    private FileSystemWatcher? _watcher;

    public FenceView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _refreshTimer.Tick += (_, _) => { _refreshTimer.Stop(); Refresh(); };
        // Klasör masaüstünde sonradan oluşturulur/silinir/yeniden adlandırılırsa fark et.
        _folderPoll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _folderPoll.Tick += (_, _) => { if (ResolveFolder() != _watcher?.Path) Refresh(); };
        _folderPoll.Start();

        Header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { e.Handled = true; CollapseToggleRequested?.Invoke(); }
        };
        Items.ContextMenu = Menus.Dynamic(FillItemMenu);
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
    public event Action? CollapseToggleRequested;

    public void SetBodyVisible(bool visible) => Body.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private string FolderName => _config.FolderName ?? "PDF";

    /// <summary>Kullanıcının masaüstündeki klasörü; adı büyük/küçük harf ve Türkçe karakterden bağımsız eşleşir.</summary>
    private string? ResolveFolder() =>
        AppHost.Organizer.ExistingFolders()
            .Where(f => Core.FolderName.Equal(f, FolderName))
            .Select(f => System.IO.Path.Combine(AppHost.DesktopDirectory, f))
            .FirstOrDefault();

    private void Refresh()
    {
        var folder = ResolveFolder();
        TitleText.Text = !string.IsNullOrWhiteSpace(_config.Title) ? _config.Title
            : folder is null ? FolderName : System.IO.Path.GetFileName(folder);
        EnsureWatcher(folder);
        Items.ItemsPanel = (ItemsPanelTemplate)FindResource(_config.View == ItemView.List ? "TileStackPanel" : "TileWrapPanel");

        if (folder is null)
        {
            Items.ItemsSource = null;
            CountBadge.Visibility = Visibility.Collapsed;
            ShowEmpty(SymbolRegular.FolderProhibited24, $"Masaüstünde \"{FolderName}\" klasörü yok.", showCreate: true);
            return;
        }

        IEnumerable<FileSystemInfo> entries = new DirectoryInfo(folder).EnumerateFileSystemInfos()
            .Where(i => (i.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0);
        entries = _config.Sort switch
        {
            FenceSort.Name => entries.OrderBy(i => i.Name, StringComparer.Create(Views.UiText.Tr, true)),
            FenceSort.Type => entries.OrderBy(i => i is DirectoryInfo ? "" : i.Extension.ToLowerInvariant()).ThenBy(i => i.Name),
            _ => entries.OrderByDescending(i => i.LastWriteTime),
        };
        var items = entries.Select(i => TileItem.Create(i.FullName, _config.IconSize, _config.View)).ToList();

        Items.ItemsSource = items;
        CountText.Text = items.Count.ToString();
        CountBadge.Visibility = Visibility.Visible;
        if (items.Count == 0) ShowEmpty(SymbolRegular.ArrowDownload24, "Klasör boş.\nDosyaları buraya sürükleyin.", showCreate: false);
        else EmptyState.Visibility = Visibility.Collapsed;
    }

    private void ShowEmpty(SymbolRegular icon, string text, bool showCreate)
    {
        EmptyIcon.Symbol = icon;
        EmptyText.Text = text;
        CreateFolderButton.Visibility = showCreate ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
    }

    private void EnsureWatcher(string? folder)
    {
        if (_watcher?.Path == folder) return;
        _watcher?.Dispose();
        _watcher = null;
        if (folder is null) return;

        _watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = false, EnableRaisingEvents = true };
        FileSystemEventHandler changed = (_, _) => QueueRefresh();
        _watcher.Created += changed;
        _watcher.Deleted += changed;
        _watcher.Changed += changed;
        _watcher.Renamed += (_, _) => QueueRefresh();
    }

    private void QueueRefresh() => Dispatcher.BeginInvoke(() => { _refreshTimer.Stop(); _refreshTimer.Start(); });

    private void OnAnyMove(MoveEntry _) => QueueRefresh();

    private void OnJournalChanged() => QueueRefresh();

    private TileItem? Selected => Items.SelectedItem as TileItem;

    private void Items_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Selected is { } item) TileItem.Launch(item.Path);
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

    private void FillItemMenu(ContextMenu menu)
    {
        if (Selected is not { } item) return;
        menu.Items.Add(Menus.Item("Aç", () => TileItem.Launch(item.Path)));
        menu.Items.Add(Menus.Item("Klasörde göster", () => TileItem.Reveal(item.Path)));
        if (File.Exists(item.Path))
            menu.Items.Add(Menus.Item("Masaüstüne geri taşı", () => MoveToDesktop(item)));
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
        var ok = e.Data.GetDataPresent(DataFormats.FileDrop) && ResolveFolder() is not null;
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        DropOverlay.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (ResolveFolder() is not { } folder || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

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
        DropOverlay.BorderBrush = palette.Accent;
        DropOverlay.Background = new SolidColorBrush(Color.FromArgb(0x55, 0x10, 0x0C, 0x20));
    }

    private void Set(Action change)
    {
        change();
        AppHost.SaveSettings();
        Refresh();
    }

    public void AddMenuItems(ContextMenu menu)
    {
        menu.Items.Add(Menus.Item("Klasörü aç", () => OpenFolder_Click(this, new RoutedEventArgs())));
        if (ResolveFolder() is { } folder)
            menu.Items.Add(Menus.Item("Klasör simgesi…", () => Icons.FolderIconWindow.ShowFor(folder)));
        menu.Items.Add(Menus.Item("Başlığı değiştir…", () =>
        {
            if (InputDialog.Ask("Bölme başlığı", "Başlık (boş bırakırsan klasör adı kullanılır)", TitleText.Text) is { } title)
                Set(() => _config.Title = string.IsNullOrWhiteSpace(title) ? null : title);
        }));

        var pick = new MenuItem { Header = "Gösterilen klasör" };
        foreach (var name in AppHost.Organizer.ExistingFolders().OrderBy(n => n))
            pick.Items.Add(Menus.Toggle(name, Core.FolderName.Equal(name, FolderName), () => Set(() => { _config.FolderName = name; _config.Title = null; })));
        if (pick.Items.Count > 0) menu.Items.Add(pick);

        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Choice("Sırala", _config.Sort,
            [(FenceSort.Newest, "En yeni üstte"), (FenceSort.Name, "Ada göre"), (FenceSort.Type, "Türe göre")],
            v => Set(() => _config.Sort = v)));
        menu.Items.Add(Menus.Choice("Görünüm", _config.View,
            [(ItemView.Icons, "Simgeler"), (ItemView.List, "Liste")],
            v => Set(() => _config.View = v)));
        menu.Items.Add(Menus.Choice("Simge boyutu", _config.IconSize,
            [(IconSize.Small, "Küçük"), (IconSize.Medium, "Orta"), (IconSize.Large, "Büyük")],
            v => Set(() => _config.IconSize = v)));
        menu.Items.Add(Menus.Item("Yenile", Refresh));
    }

    public void Detach()
    {
        _refreshTimer.Stop();
        _folderPoll.Stop();
        _watcher?.Dispose();
        AppHost.Organizer.FileMoved -= OnAnyMove;
        AppHost.Journal.Changed -= OnJournalChanged;
    }
}
