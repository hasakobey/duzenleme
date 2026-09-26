using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Tek tıkla widget/bölme ekleme penceresi. Tepsi menüsünden, widget'ların sağ tık menüsünden, Ctrl+Alt+B ile ve
/// Başlat menüsündeki "Widget ekle" kısayolundan açılır; bir düğmeye basınca widget eklenir, pencere kapanır.
/// </summary>
internal sealed class QuickAddWindow : FluentWindow
{
    private static QuickAddWindow? _current;
    private readonly NativeMethods.POINT _anchor;
    private bool _closing;

    /// <summary>İmlecin (ya da testte DUZENLEME_QUICKADD_AT="x,y" noktasının) bulunduğu yerde açar.</summary>
    public static void ShowNearCursor()
    {
        if (_current is not null)
        {
            _current.Activate();
            return;
        }
        NativeMethods.GetCursorPos(out var anchor);
        if (Environment.GetEnvironmentVariable("DUZENLEME_QUICKADD_AT")?.Split(',') is [var x, var y] &&
            int.TryParse(x, out var px) && int.TryParse(y, out var py))
            anchor = new NativeMethods.POINT { X = px, Y = py };
        _current = new QuickAddWindow(anchor);
        _current.Closed += (_, _) => _current = null;
        _current.Show();
    }

    private QuickAddWindow(NativeMethods.POINT anchor)
    {
        _anchor = anchor;
        Title = "Widget ekle";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.Mica;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ShowInTaskbar = false;
        Topmost = true;
        // FluentWindow'un varsayılan en küçük boyutu içerikten büyük.
        MinWidth = 0;
        MinHeight = 0;

        var body = new StackPanel { Margin = new Thickness(22, 2, 22, 20), Width = 560 };
        body.Children.Add(new TextBlock
        {
            Text = "Masaüstüne ne eklemek istersin?", FontSize = 20, FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorPrimaryBrush"),
        });
        body.Children.Add(Muted("Birine tıkla, hemen masaüstüne gelsin. Sonra sürükleyerek taşı, kenarından büyüt, sağ tıklayarak ayarla.", 4));

        body.Children.Add(Section("Bölmeler · masaüstünü bölümlere ayırır"));
        var fences = new WrapPanel();
        fences.Children.Add(Tile("Klasörlerim", SymbolRegular.Folder24, "Masaüstündeki klasörler", () => AppHost.Widgets.AddFence(DesktopFilter.Folders)));
        fences.Children.Add(Tile("Kısayollarım", SymbolRegular.Apps24, "Uygulama kısayolları, Bu Bilgisayar, Geri Dönüşüm Kutusu", () => AppHost.Widgets.AddFence(DesktopFilter.Shortcuts)));
        fences.Children.Add(Tile("Dosyalarım", SymbolRegular.DocumentMultiple24, "Masaüstünde duran dosyalar", () => AppHost.Widgets.AddFence(DesktopFilter.Files)));
        fences.Children.Add(Tile("Tüm masaüstü", SymbolRegular.Desktop24, "Masaüstündeki her şey tek bölmede", () => AppHost.Widgets.AddFence(DesktopFilter.All)));
        foreach (var (name, exists) in AppHost.Widgets.FolderFenceChoices())
        {
            var icon = name.Equals("PDF", StringComparison.OrdinalIgnoreCase) ? SymbolRegular.DocumentPdf24 : SymbolRegular.FolderOpen24;
            var tip = exists ? $"\"{name}\" klasörünün içi" : $"Masaüstünde \"{name}\" klasörü açılır; uygun dosyalar oraya taşınır";
            fences.Children.Add(Tile(name, icon, tip, () => AppHost.Widgets.AddFolderFence(name)));
        }
        body.Children.Add(fences);

        body.Children.Add(Section("Araçlar"));
        var tools = new WrapPanel();
        tools.Children.Add(Tile("Saat", SymbolRegular.Clock24, "Büyük dijital saat", () => AppHost.Widgets.Add(WidgetKind.Clock)));
        tools.Children.Add(Tile("Tarih", SymbolRegular.CalendarLtr24, "Gün, ay ve haftalık şerit", () => AppHost.Widgets.Add(WidgetKind.Date)));
        tools.Children.Add(Tile("Not", SymbolRegular.Note24, "Yapışkan not; yazdıkça kaydedilir",
            () => AppHost.Widgets.FocusNote(AppHost.Widgets.Add(WidgetKind.Note).Id)));
        tools.Children.Add(Tile("Kısayol kutusu", SymbolRegular.AppsAddIn24, "Sekmeli uygulama rafı; tek tıkla açar",
            () => AppHost.Widgets.Add(WidgetKind.Launcher)));
        body.Children.Add(tools);

        var footer = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
        var all = new Button
        {
            Content = "Hepsini kur: masaüstümü bölümlere ayır", Appearance = ControlAppearance.Primary,
            Icon = new SymbolIcon { Symbol = SymbolRegular.Sparkle24 },
            ToolTip = "Klasörler, Kısayollar, Dosyalar ve PDF gibi klasörler için bölme kurar; masaüstü simgeleri yalnızca bölmelerde görünür",
        };
        all.Click += (_, _) => Run(() =>
        {
            AppHost.Widgets.AddStarterFences();
            AppHost.SetFencesManageDesktop(true);
        });
        var more = new Button { Content = "Diğer ayarlar…", Margin = new Thickness(8, 0, 0, 0) };
        more.Click += (_, _) => { Close(); (Application.Current as App)?.ShowMainWindow(); };
        DockPanel.SetDock(more, Dock.Right);
        footer.Children.Add(all);
        footer.Children.Add(more);
        body.Children.Add(footer);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(new TitleBar { Title = "Widget ekle", ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        Content = root;

        Loaded += (_, _) => PlaceNearAnchor();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        // Açılır menü gibi: başka yere tıklanınca kapanır.
        Deactivated += (_, _) => { if (!_closing) Close(); };
        Closing += (_, _) => _closing = true;
    }

    private static TextBlock Muted(string text, double top) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 0), Opacity = 0.75,
    };

    private static TextBlock Section(string text) => new()
    {
        Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 8),
    };

    private Button Tile(string label, SymbolRegular icon, string tip, Action add)
    {
        var content = new StackPanel();
        content.Children.Add(new SymbolIcon { Symbol = icon, FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(new TextBlock
        {
            Text = label, Margin = new Thickness(0, 6, 0, 0), TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 110,
        });
        var button = new Button
        {
            Content = content, Width = 128, Height = 82, Margin = new Thickness(0, 0, 8, 8), ToolTip = tip,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        System.Windows.Automation.AutomationProperties.SetHelpText(button, tip);
        button.Click += (_, _) => Run(add);
        return button;
    }

    /// <summary>Widget'ı bu pencerenin bulunduğu ekrana ekler ve pencereyi kapatır.</summary>
    private void Run(Action add)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (NativeMethods.GetWindowRect(hwnd, out var r))
            AppHost.Widgets.PlacementHint = new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 };
        _closing = true;
        Close();
        add();
    }

    /// <summary>Pencereyi imlecin üstünde ortalar; ekrandan taşmasın.</summary>
    private void PlaceNearAnchor()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(hwnd, out var r)) return;
        var work = NativeMethods.WorkAreaAt(_anchor);
        var x = Math.Clamp(_anchor.X - r.Width / 2, work.Left, Math.Max(work.Left, work.Right - r.Width));
        var y = Math.Clamp(_anchor.Y - r.Height / 3, work.Top, Math.Max(work.Top, work.Bottom - r.Height));
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
        Activate();
    }
}
