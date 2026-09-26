using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

public sealed record FolderChip(string Name, SymbolRegular Icon, Brush IconBrush, string Detail);

public partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppHost.SettingsChanged += Refresh;
            AppHost.Journal.Changed += RefreshAsync;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            AppHost.SettingsChanged -= Refresh;
            AppHost.Journal.Changed -= RefreshAsync;
        };
    }

    private void RefreshAsync() => Dispatcher.BeginInvoke(Refresh);

    private void Refresh()
    {
        var s = AppHost.Settings;
        Hello.Text = ClockGreeting() + (string.IsNullOrEmpty(Environment.UserName) ? "" : ", " + Environment.UserName);

        var paused = s.Paused;
        WatchToggle.IsChecked = !paused;
        StatusTitle.Text = paused ? "İzleme duraklatıldı" : "Masaüstü izleniyor";
        StatusText.Text = paused ? "Yeni dosyalar olduğu yerde kalır." : AppHost.DesktopDirectory;
        StatusIcon.Symbol = paused ? SymbolRegular.Pause24 : SymbolRegular.Desktop24;
        StatusCard.Background = paused
            ? new LinearGradientBrush(Color.FromRgb(0x4B, 0x4B, 0x57), Color.FromRgb(0x33, 0x33, 0x3D), 0)
            : new LinearGradientBrush(Color.FromRgb(0x63, 0x66, 0xF1), Color.FromRgb(0xA8, 0x55, 0xF7), 0);

        var entries = AppHost.Journal.Snapshot().Where(e => !e.Undone).ToList();
        TodayCount.Text = entries.Count(e => e.Time.Date == DateTime.Today).ToString();
        TotalCount.Text = entries.Count.ToString();

        var folders = AppHost.Organizer.ExistingFolders().ToList();
        var chips = new List<FolderChip>();
        var ready = 0;
        foreach (var rule in s.Rules.Where(r => r.Enabled))
        {
            var match = folders.FirstOrDefault(f => FolderName.Equal(f, rule.TargetFolder));
            if (match is not null)
            {
                ready++;
                var count = SafeCount(Path.Combine(AppHost.DesktopDirectory, match));
                chips.Add(new FolderChip(match, SymbolRegular.Folder24, (Brush)FindResource("SystemFillColorSuccessBrush"), $"{count} öğe · .{string.Join(" .", rule.Extensions.Take(3))}"));
            }
        }
        foreach (var folder in folders.Where(f => !chips.Any(c => c.Name == f)))
            chips.Add(new FolderChip(folder, SymbolRegular.Folder24, (Brush)FindResource("TextFillColorTertiaryBrush"), "kural yok"));

        ReadyCount.Text = ready.ToString();
        NoFolderInfo.IsOpen = ready == 0;
        NoFolderInfo.Visibility = ready == 0 ? Visibility.Visible : Visibility.Collapsed;
        Folders.ItemsSource = chips;

        var recent = AppHost.Journal.Snapshot().Take(6).Select(e => new MoveRow(e)).ToList();
        Recent.ItemsSource = recent;
        RecentEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static int SafeCount(string dir)
    {
        try { return Directory.EnumerateFileSystemEntries(dir).Count(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }

    private static string ClockGreeting() => Widgets.ClockView.Greeting(DateTime.Now.Hour);

    private void WatchToggle_Click(object sender, RoutedEventArgs e) => AppHost.SetPaused(WatchToggle.IsChecked != true);

    private void OrganizeNow_Click(object sender, RoutedEventArgs e) => AppHost.OrganizeNowInBackground();

    private void QuickAdd_Click(object sender, RoutedEventArgs e) => (Application.Current as App)?.ShowQuickAdd();

    private void UndoLast_Click(object sender, RoutedEventArgs e) => HistoryPage.UndoWithFeedback(AppHost.Journal.LastActive());

    private void OpenDesktop_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppHost.DesktopDirectory) { UseShellExecute = true });
}
