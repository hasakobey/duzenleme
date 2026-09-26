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
        WidgetKind.Launcher => (Config.Title is { } title ? $"Kısayol kutusu · {title}" : "Kısayol kutusu") + $" ({Config.Tabs.Sum(t => t.Items.Count)} öğe)",
        _ => $"Bölme · {Config.Title ?? (Config.Filter != DesktopFilter.None ? DesktopItems.Label(Config.Filter) : Config.FolderName)}",
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

        AutoFencesButton.Icon = new SymbolIcon { Symbol = SymbolRegular.Sparkle24 };
        BuildFenceChoices();

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

    private void Added(string what, string where)
    {
        AddedInfo.Severity = InfoBarSeverity.Success;
        AddedInfo.Title = $"{what} masaüstüne eklendi";
        AddedInfo.Message = $"Yeri: {where}. Birkaç saniye öne getirildi; sonra masaüstü katmanına (pencerelerin arkasına) döner. " +
                            "Sürükleyerek taşı, sağ tıklayarak ayarla. Kaybolursa listeden \"Göster\"e bas.";
        AddedInfo.IsOpen = true;
    }

    private void AddClock_Click(object sender, RoutedEventArgs e) { AppHost.Widgets.Add(WidgetKind.Clock); Added("Saat", "ekranın sağ üstü"); }

    private void AddDate_Click(object sender, RoutedEventArgs e) { AppHost.Widgets.Add(WidgetKind.Date); Added("Tarih", "ekranın sağ üstü, saatin altı"); }

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Widgets.FocusNote(AppHost.Widgets.Add(WidgetKind.Note).Id);
        Added("Not", "ekranın sağ tarafı");
    }

    private void AddLauncher_Click(object sender, RoutedEventArgs e) { AppHost.Widgets.Add(WidgetKind.Launcher); Added("Kısayol kutusu", "ekranın alt ortası"); }

    /// <summary>
    /// Tek tıkla eklenecek bölmeler: masaüstü türleri (Klasörler, Kısayollar, Dosyalar, Tümü), kural klasörleri
    /// (PDF, Resimler…; masaüstünde yoksa eklenirken oluşturulur) ve kullanıcının diğer klasörleri.
    /// </summary>
    private void BuildFenceChoices()
    {
        FenceChoices.Children.Clear();
        foreach (var filter in DesktopItems.Filters)
        {
            var icon = filter switch
            {
                DesktopFilter.Folders => SymbolRegular.Folder24,
                DesktopFilter.Shortcuts => SymbolRegular.Apps24,
                DesktopFilter.Files => SymbolRegular.DocumentMultiple24,
                _ => SymbolRegular.Desktop24,
            };
            var label = filter == DesktopFilter.All ? "Tüm masaüstü" : DesktopItems.Label(filter);
            AddChoice(label, icon, DesktopItems.Description(filter), () =>
            {
                AppHost.Widgets.AddFence(filter);
                Added($"\"{label}\" bölmesi", "ekranın üst ortası");
            });
        }

        var existing = AppHost.Organizer.ExistingFolders().ToList();
        var names = new List<string>();
        foreach (var rule in AppHost.Settings.Rules.Where(r => r.Enabled))
            if (!names.Any(n => FolderName.Equal(n, rule.TargetFolder)))
                names.Add(existing.FirstOrDefault(f => FolderName.Equal(f, rule.TargetFolder)) ?? rule.TargetFolder);
        foreach (var folder in existing.OrderBy(f => f, StringComparer.Create(UiText.Tr, true)))
            if (!names.Any(n => FolderName.Equal(n, folder))) names.Add(folder);

        foreach (var name in names)
        {
            var exists = existing.Any(f => FolderName.Equal(f, name));
            AddChoice(name, name.Equals("PDF", StringComparison.OrdinalIgnoreCase) ? SymbolRegular.DocumentPdf24 : SymbolRegular.FolderOpen24,
                exists ? $"Masaüstündeki \"{name}\" klasörünün içi" : $"Masaüstünde \"{name}\" klasörü yok; eklenince oluşturulur ve uygun dosyalar oraya taşınır",
                () => AddFolderFence(name, exists));
        }
    }

    private void AddChoice(string label, SymbolRegular icon, string tip, Action add)
    {
        var button = new Wpf.Ui.Controls.Button
        {
            Content = label,
            Icon = new SymbolIcon { Symbol = icon },
            ToolTip = tip,
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(10, 5, 12, 6),
        };
        button.Click += (_, _) => add();
        FenceChoices.Children.Add(button);
    }

    private void AddFolderFence(string name, bool exists)
    {
        if (!exists)
        {
            try { System.IO.Directory.CreateDirectory(System.IO.Path.Combine(AppHost.DesktopDirectory, name)); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                System.Windows.MessageBox.Show(ex.Message, "Düzenleme");
                return;
            }
            AppHost.OrganizeNowInBackground();
        }
        AppHost.Widgets.Add(WidgetKind.Fence, name);
        Added($"\"{name}\" bölmesi", "ekranın üst ortası" + (exists ? "" : $". Masaüstünde \"{name}\" klasörü oluşturuldu"));
    }

    private void AutoFences_Click(object sender, RoutedEventArgs e)
    {
        var count = AppHost.Widgets.AddStarterFences();
        if (count == 0)
        {
            AddedInfo.Severity = InfoBarSeverity.Informational;
            AddedInfo.Title = "Bölmeler zaten hazır";
            AddedInfo.Message = "Klasörler, Kısayollar, Dosyalar ve masaüstündeki kural klasörleri için bölme zaten var.";
            AddedInfo.IsOpen = true;
            return;
        }
        Added($"{count} bölme", "ekranın üst tarafı");
        AddedInfo.Message += " İpucu: masaüstüne çift tıklayıp simgeleri gizlersen yalnızca düzenli bölmeler kalır.";
    }

    private void RevealAll_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RevealAll();

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WidgetRow row) AppHost.Widgets.Reveal(row.Config.Id);
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
