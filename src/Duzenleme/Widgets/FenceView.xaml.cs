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

public sealed record FenceItem(string Name, string Path, ImageSource? Icon);

/// <summary>
/// Fences tarzı bölme: masaüstündeki bir klasörün (ör. PDF) içeriğini masaüstünde gösterir.
/// Dosya sürükleyip bırakınca klasöre taşır.
/// </summary>
public partial class FenceView : UserControl, IWidgetView
{
    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _folderPoll;
    private FileSystemWatcher? _watcher;
    private WidgetPalette _palette = WidgetPalette.Glass;

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

        Items.ContextMenu = BuildItemMenu();
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        Drop += OnDrop;
        AppHost.Organizer.FileMoved += OnAnyMove;
        AppHost.Journal.Changed += OnJournalChanged;

        Refresh();
    }

    public bool Resizable => true;

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
        TitleText.Text = folder is null ? FolderName : System.IO.Path.GetFileName(folder);
        EnsureWatcher(folder);

        if (folder is null)
        {
            Items.ItemsSource = null;
            CountBadge.Visibility = Visibility.Collapsed;
            ShowEmpty(SymbolRegular.FolderProhibited24, $"Masaüstünde \"{FolderName}\" klasörü yok.", showCreate: true);
            return;
        }

        var entries = new DirectoryInfo(folder).EnumerateFileSystemInfos()
            .Where(i => (i.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
            .OrderByDescending(i => i.LastWriteTime)
            .Select(i => new FenceItem(DisplayName(i), i.FullName, ShellIcons.For(i.FullName)))
            .ToList();

        Items.ItemsSource = entries;
        CountText.Text = entries.Count.ToString();
        CountBadge.Visibility = Visibility.Visible;
        if (entries.Count == 0) ShowEmpty(SymbolRegular.ArrowDownload24, "Klasör boş.\nDosyaları buraya sürükleyin.", showCreate: false);
        else EmptyState.Visibility = Visibility.Collapsed;
    }

    private static string DisplayName(FileSystemInfo info) =>
        info is FileInfo && info.Extension is ".lnk" or ".url" ? System.IO.Path.GetFileNameWithoutExtension(info.Name) : info.Name;

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

    // Klasör sonradan oluşturulursa ya da yeniden adlandırılırsa bölme kendini günceller.
    private void OnAnyMove(MoveEntry _) => QueueRefresh();

    private void OnJournalChanged() => QueueRefresh();

    private FenceItem? Selected => Items.SelectedItem as FenceItem;

    private void Items_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Selected is { } item) Shell(item.Path);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ResolveFolder() is { } folder) Shell(folder);
    }

    private void CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(System.IO.Path.Combine(AppHost.DesktopDirectory, FolderName));
        Refresh();
        AppHost.OrganizeNowInBackground();
    }

    private ContextMenu BuildItemMenu()
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) =>
        {
            menu.Items.Clear();
            if (Selected is not { } item)
            {
                menu.IsOpen = false;
                return;
            }
            menu.Items.Add(Item("Aç", () => Shell(item.Path)));
            menu.Items.Add(Item("Klasörde göster", () => Process.Start("explorer.exe", $"/select,\"{item.Path}\"")));
            if (File.Exists(item.Path))
                menu.Items.Add(Item("Masaüstüne geri taşı", () => MoveToDesktop(item)));
        };
        menu.Items.Add(new MenuItem());
        return menu;
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static void MoveToDesktop(FenceItem item)
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
            System.Windows.MessageBox.Show(ex.Message, "Düzenleme");
        }
    }

    private static void Shell(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message, "Düzenleme"); }
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
                System.Windows.MessageBox.Show(ex.Message, "Düzenleme");
            }
        }
        Refresh();
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
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

    public void AddMenuItems(ContextMenu menu)
    {
        menu.Items.Add(Item("Klasörü aç", () => OpenFolder_Click(this, new RoutedEventArgs())));
        menu.Items.Add(Item("Yenile", Refresh));

        var pick = new MenuItem { Header = "Gösterilen klasör" };
        foreach (var name in AppHost.Organizer.ExistingFolders().OrderBy(n => n))
        {
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = Core.FolderName.Equal(name, FolderName) };
            item.Click += (_, _) =>
            {
                _config.FolderName = name;
                AppHost.SaveSettings();
                Refresh();
            };
            pick.Items.Add(item);
        }
        if (pick.Items.Count > 0) menu.Items.Add(pick);
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
