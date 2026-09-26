using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Icons;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

public sealed class SystemIconRow(SystemIcon icon) : INotifyPropertyChanged
{
    public SystemIcon Icon_ { get; } = icon;
    public string Name => Icon_.Name;
    public string Description => Icon_.Description;
    public ImageSource? Icon => ShellIcons.ForShellObject("::" + Icon_.Clsid);
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Shown
    {
        get => DesktopSystemIcons.IsShown(Icon_);
        set
        {
            DesktopSystemIcons.SetShown(Icon_, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Shown)));
        }
    }
}

/// <summary>Masaüstündeki bir öğe; kategori kurallardan türetilir.</summary>
public sealed class DesktopItemRow
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string Category { get; init; }
    public required string Detail { get; init; }
    public ImageSource? Icon { get; init; }
    public bool IsFolder { get; init; }
    public string? OrganizeTarget { get; init; }
    public Visibility IconVisibility => IsFolder ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OrganizeVisibility => OrganizeTarget is null ? Visibility.Collapsed : Visibility.Visible;
    public string OrganizeTip => $"\"{OrganizeTarget}\" klasörüne taşı";
}

public partial class DesktopPage : Page
{
    private const string All = "Tümü";
    private List<DesktopItemRow> _items = [];
    private string _category = All;
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public DesktopPage()
    {
        InitializeComponent();
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); ApplyFilter(); };
        Loaded += (_, _) =>
        {
            SystemIcons.ItemsSource = DesktopSystemIcons.All.Select(i => new SystemIconRow(i)).ToList();
            SuggestToggle.IsChecked = AppHost.Settings.SuggestFolderIcons;
            AppHost.DesktopVisibilityChanged += UpdateHideButton;
            AppHost.Journal.Changed += ReloadAsync;
            FolderIconWindow.IconChanged += OnIconChanged;
            UpdateHideButton();
            Reload();
        };
        Unloaded += (_, _) =>
        {
            AppHost.DesktopVisibilityChanged -= UpdateHideButton;
            AppHost.Journal.Changed -= ReloadAsync;
            FolderIconWindow.IconChanged -= OnIconChanged;
        };
    }

    private void OnIconChanged(string _) => Dispatcher.BeginInvoke(Reload, DispatcherPriority.Background);

    private void ReloadAsync() => Dispatcher.BeginInvoke(Reload);

    private void UpdateHideButton()
    {
        HideButton.Content = AppHost.DesktopHidden ? "Tüm simgeleri göster" : "Tüm simgeleri gizle";
        HideButton.Icon = new SymbolIcon { Symbol = AppHost.DesktopHidden ? SymbolRegular.Eye24 : SymbolRegular.EyeOff24 };
    }

    private void Reload()
    {
        var desktop = AppHost.DesktopDirectory;
        var rules = AppHost.Settings.Rules.Where(r => r.Enabled).ToList();
        var folders = AppHost.Organizer.ExistingFolders().ToList();
        var rows = new List<DesktopItemRow>();

        if (Directory.Exists(desktop))
        {
            foreach (var info in new DirectoryInfo(desktop).EnumerateFileSystemInfos())
            {
                if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                if (info is DirectoryInfo dir)
                {
                    int count;
                    try { count = dir.EnumerateFileSystemInfos().Count(i => (i.Attributes & FileAttributes.Hidden) == 0); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { count = 0; }
                    rows.Add(new DesktopItemRow
                    {
                        Name = dir.Name, Path = dir.FullName, Category = "Klasörler", IsFolder = true, Icon = ShellIcons.For(dir.FullName),
                        Detail = $"{count} öğe" + (FolderIconService.HasCustomIcon(dir.FullName) ? " · özel simge" : ""),
                    });
                    continue;
                }

                var file = (FileInfo)info;
                var ext = file.Extension.ToLowerInvariant();
                string category;
                string? target = null;
                if (ext is ".lnk" or ".url" or ".appref-ms") category = "Kısayollar";
                else if (rules.FirstOrDefault(r => r.Matches(ext)) is { } rule)
                {
                    category = rule.TargetFolder;
                    target = folders.FirstOrDefault(f => FolderName.Equal(f, rule.TargetFolder)) ?? (AppHost.Settings.CreateMissingFolders ? rule.TargetFolder : null);
                }
                else category = "Diğer";

                rows.Add(new DesktopItemRow
                {
                    Name = category == "Kısayollar" ? System.IO.Path.GetFileNameWithoutExtension(file.Name) : file.Name,
                    Path = file.FullName, Category = category, Icon = ShellIcons.For(file.FullName), OrganizeTarget = target,
                    Detail = $"{Size(file.Length)} · {UiText.When(file.LastWriteTime)}",
                });
            }
        }

        _items = rows.OrderByDescending(r => r.IsFolder).ThenBy(r => r.Name, StringComparer.Create(UiText.Tr, true)).ToList();
        BuildChips();
        ApplyFilter();
    }

    private static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.#} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:0.##} GB",
    };

    /// <summary>Filtre çipleri: yalnızca masaüstünde gerçekten bulunan kategoriler, sayılarıyla.</summary>
    private void BuildChips()
    {
        var groups = _items.GroupBy(i => i.Category).OrderBy(g => g.Key == "Klasörler" ? 0 : g.Key == "Kısayollar" ? 1 : g.Key == "Diğer" ? 3 : 2).ThenBy(g => g.Key);
        var chips = new List<(string Name, int Count)> { (All, _items.Count) };
        chips.AddRange(groups.Select(g => (g.Key, g.Count())));
        if (chips.All(c => c.Name != _category)) _category = All;

        Chips.Items.Clear();
        foreach (var (name, count) in chips)
        {
            var chip = new ToggleButton
            {
                IsChecked = name == _category,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(12, 5, 12, 5),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = name },
                        new TextBlock { Text = count.ToString(), Margin = new Thickness(8, 0, 0, 0), Opacity = 0.65 },
                    },
                },
            };
            chip.Click += (_, _) => { _category = name; BuildChips(); ApplyFilter(); };
            Chips.Items.Add(chip);
        }
    }

    private void ApplyFilter()
    {
        var q = Search.Text.Trim();
        var visible = _items
            .Where(i => _category == All || i.Category == _category)
            .Where(i => q.Length == 0 || FolderName.Fold(i.Name).Contains(FolderName.Fold(q), StringComparison.Ordinal))
            .ToList();
        Items.ItemsSource = visible;
        Empty.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    private static DesktopItemRow? RowOf(object sender) => (sender as FrameworkElement)?.DataContext as DesktopItemRow;

    private void Open_Click(object sender, RoutedEventArgs e) { if (RowOf(sender) is { } r) TileItem.Launch(r.Path); }

    private void Reveal_Click(object sender, RoutedEventArgs e) { if (RowOf(sender) is { } r) TileItem.Reveal(r.Path); }

    private void Icon_Click(object sender, RoutedEventArgs e) { if (RowOf(sender) is { } r) FolderIconWindow.ShowFor(r.Path); }

    private void Organize_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        var entry = AppHost.Organizer.Organize(row.Path);
        Show(entry is null ? "Taşınamadı" : "Taşındı",
             entry is null ? "Dosya kullanımda olabilir ya da daha önce geri alınmış." : $"{entry.FileName} → {entry.FolderName}",
             entry is null ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        Reload();
    }

    private void SystemIcon_Open(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SystemIconRow row) TileItem.Launch(DesktopSystemIcons.ShellPath(row.Icon_));
    }

    private void ToggleDesktop_Click(object sender, RoutedEventArgs e) => AppHost.ToggleDesktop();

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        DesktopSystemIcons.Refresh();
        Reload();
    }

    private void BeautifyAll_Click(object sender, RoutedEventArgs e)
    {
        var done = 0;
        foreach (var folder in AppHost.Organizer.ExistingFolders())
        {
            var path = System.IO.Path.Combine(AppHost.DesktopDirectory, folder);
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
        Show(done > 0 ? "Simgeler verildi" : "Değişiklik yok",
             done > 0 ? $"{done} klasöre adına uygun simge atandı. Beğenmediğini \"Simge\" düğmesinden değiştirebilirsin." : "Tüm klasörlerin zaten özel simgesi var.",
             done > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Informational);
        Reload();
    }

    private void SuggestToggle_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.SuggestFolderIcons = SuggestToggle.IsChecked == true;
        AppHost.SaveSettings();
    }

    private void Show(string title, string message, InfoBarSeverity severity)
    {
        Feedback.Title = title;
        Feedback.Message = message;
        Feedback.Severity = severity;
        Feedback.IsOpen = true;
    }
}
