using System.Windows;
using System.Windows.Controls;
using Duzenleme.Core;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

public sealed class WidgetRow(WidgetConfig config)
{
    public WidgetConfig Config { get; } = config;
    public string[] StyleNames { get; } = ["Cam", "Koyu", "Açık"];

    public string Name => Config.Kind switch
    {
        WidgetKind.Clock => "Saat",
        WidgetKind.Date => "Tarih",
        _ => $"Bölme · {Config.FolderName}",
    };

    public SymbolRegular Icon => Config.Kind switch
    {
        WidgetKind.Clock => SymbolRegular.Clock24,
        WidgetKind.Date => SymbolRegular.CalendarLtr24,
        _ => SymbolRegular.Folder24,
    };

    public int StyleIndex
    {
        get => Config.Style switch { WidgetStyle.Dark => 1, WidgetStyle.Light => 2, _ => 0 };
        set
        {
            Config.Style = value switch { 1 => WidgetStyle.Dark, 2 => WidgetStyle.Light, _ => WidgetStyle.Glass };
            AppHost.SaveSettings();
            AppHost.Widgets.Restyle(Config.Id);
        }
    }
}

public partial class WidgetsPage : Page
{
    public WidgetsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppHost.Widgets.Changed += Refresh;
            Refresh();
        };
        Unloaded += (_, _) => AppHost.Widgets.Changed -= Refresh;
    }

    private void Refresh()
    {
        var now = DateTime.Now;
        ClockPreview.Text = now.ToString("HH:mm", UiText.Tr);
        DayPreview.Text = now.Day.ToString(UiText.Tr);
        MonthPreview.Text = now.ToString("MMMM", UiText.Tr);
        WeekdayPreview.Text = now.ToString("dddd", UiText.Tr);

        var folders = AppHost.Organizer.ExistingFolders().OrderBy(f => f).ToList();
        // Kurallardaki klasör adlarını da öner: bölme, klasör oluşturulunca kendiliğinden dolar.
        foreach (var rule in AppHost.Settings.Rules.Where(r => r.Enabled))
            if (!folders.Any(f => FolderName.Equal(f, rule.TargetFolder))) folders.Add(rule.TargetFolder);
        FolderPicker.ItemsSource = folders;
        FolderPicker.SelectedItem = folders.FirstOrDefault(f => FolderName.Equal(f, "PDF")) ?? folders.FirstOrDefault();

        var rows = AppHost.Widgets.Configs.Select(c => new WidgetRow(c)).ToList();
        ActiveList.ItemsSource = rows;
        ActiveEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddClock_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.Add(WidgetKind.Clock);

    private void AddDate_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.Add(WidgetKind.Date);

    private void AddFence_Click(object sender, RoutedEventArgs e)
    {
        if (FolderPicker.SelectedItem is string folder) AppHost.Widgets.Add(WidgetKind.Fence, folder);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WidgetRow row) AppHost.Widgets.Remove(row.Config.Id);
    }
}
