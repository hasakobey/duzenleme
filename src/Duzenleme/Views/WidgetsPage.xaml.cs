using System.Windows;
using System.Windows.Controls;
using Duzenleme.Core;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

public sealed class WidgetRow(WidgetConfig config)
{
    public WidgetConfig Config { get; } = config;
    public string[] StyleNames { get; } = ["Cam", "Koyu", "Açık"];
    public string[] AccentNames { get; } = ["Mor", "Mavi", "Yeşil", "Turuncu", "Pembe"];

    /// <summary>Notlar kendi kağıt rengini kullanır.</summary>
    public bool HasStyle => Config.Kind != WidgetKind.Note;

    public string Name => Config.Kind switch
    {
        WidgetKind.Clock => "Saat",
        WidgetKind.Date => "Tarih",
        WidgetKind.Note => "Not" + (string.IsNullOrWhiteSpace(Config.NoteText) ? "" : " · " + FirstLine(Config.NoteText)),
        WidgetKind.Launcher => $"Kısayol kutusu · {Config.Title ?? "Kısayollar"} ({Config.Tabs.Sum(t => t.Items.Count)} öğe)",
        _ => $"Bölme · {Config.Title ?? Config.FolderName}",
    };

    private static string FirstLine(string text)
    {
        var line = text.Split('\n')[0].Trim();
        return line.Length > 40 ? line[..40] + "…" : line;
    }

    public SymbolRegular Icon => Config.Kind switch
    {
        WidgetKind.Clock => SymbolRegular.Clock24,
        WidgetKind.Date => SymbolRegular.CalendarLtr24,
        WidgetKind.Note => SymbolRegular.Note24,
        WidgetKind.Launcher => SymbolRegular.Apps24,
        _ => SymbolRegular.Folder24,
    };

    public int StyleIndex
    {
        get => Config.Style switch { WidgetStyle.Dark => 1, WidgetStyle.Light => 2, _ => 0 };
        set => Change(() => Config.Style = value switch { 1 => WidgetStyle.Dark, 2 => WidgetStyle.Light, _ => WidgetStyle.Glass });
    }

    public int AccentIndex
    {
        get => (int)Config.Accent;
        set => Change(() => Config.Accent = (WidgetAccent)Math.Clamp(value, 0, 4));
    }

    private void Change(Action change)
    {
        change();
        AppHost.SaveSettings();
        AppHost.Widgets.Restyle(Config.Id);
    }
}

public sealed record LayoutRow(LayoutSnapshot Layout)
{
    public string Name => Layout.Name;
    public string Detail => $"{Layout.Widgets.Count} widget · {UiText.When(Layout.Created)}";
}

public partial class WidgetsPage : Page
{
    public WidgetsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppHost.Widgets.Changed += Refresh;
            AppHost.DesktopVisibilityChanged += Refresh;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            AppHost.Widgets.Changed -= Refresh;
            AppHost.DesktopVisibilityChanged -= Refresh;
        };
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
        var selected = FolderPicker.SelectedItem as string;
        FolderPicker.ItemsSource = folders;
        FolderPicker.SelectedItem = selected is not null && folders.Contains(selected) ? selected
            : folders.FirstOrDefault(f => FolderName.Equal(f, "PDF")) ?? folders.FirstOrDefault();

        HideButton.Content = AppHost.DesktopHidden ? "Masaüstünü göster" : "Masaüstünü gizle";
        HideButton.Icon = new SymbolIcon { Symbol = AppHost.DesktopHidden ? SymbolRegular.Eye24 : SymbolRegular.EyeOff24 };
        HideHint.Text = (AppHost.Settings.DoubleClickHidesDesktop ? "Masaüstünde boş bir yere çift tıkla" : "Çift tıklama kapalı; kısayolu kullan")
                        + $" ya da {AppHost.Settings.Hotkeys.ToggleDesktop} tuşlarına bas. Simgeler (isteğe göre widget'lar da) gizlenir.";

        var rows = AppHost.Widgets.Configs.Select(c => new WidgetRow(c)).ToList();
        ActiveList.ItemsSource = rows;
        ActiveEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var layouts = AppHost.Settings.Layouts.OrderByDescending(l => l.Created).Select(l => new LayoutRow(l)).ToList();
        LayoutList.ItemsSource = layouts;
        LayoutEmpty.Visibility = layouts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddClock_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.Add(WidgetKind.Clock);

    private void AddDate_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.Add(WidgetKind.Date);

    private void AddNote_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.FocusNote(AppHost.Widgets.Add(WidgetKind.Note).Id);

    private void AddLauncher_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.Add(WidgetKind.Launcher);

    private void AddFence_Click(object sender, RoutedEventArgs e)
    {
        if (FolderPicker.SelectedItem is string folder) AppHost.Widgets.Add(WidgetKind.Fence, folder);
    }

    private void ToggleDesktop_Click(object sender, RoutedEventArgs e) => AppHost.ToggleDesktop();

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WidgetRow row) AppHost.Widgets.Remove(row.Config.Id);
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        var name = LayoutName.Text.Trim();
        if (name.Length == 0) name = "Düzen " + (AppHost.Settings.Layouts.Count + 1);
        // Aynı adlı düzen varsa üzerine yaz.
        AppHost.Settings.Layouts.RemoveAll(l => string.Equals(l.Name, name, StringComparison.CurrentCultureIgnoreCase));
        AppHost.Settings.Layouts.Add(LayoutSnapshot.Capture(name, AppHost.Settings.Widgets));
        AppHost.SaveSettings();
        LayoutName.Text = "";
        Refresh();
    }

    private void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LayoutRow row) AppHost.Widgets.ApplyLayout(row.Layout);
    }

    private void DeleteLayout_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not LayoutRow row) return;
        AppHost.Settings.Layouts.Remove(row.Layout);
        AppHost.SaveSettings();
        Refresh();
    }
}
