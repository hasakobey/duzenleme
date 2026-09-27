using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Tek tıkla widget/bölme ekleme penceresi. Tepsi menüsünden, widget'ların sağ tık menüsünden, Ctrl+Alt+B ile ve
/// Başlat menüsündeki "Widget ekle" kısayolundan açılır; bir kutucuğa basınca widget eklenir, pencere kapanır.
/// Kutucuklar <see cref="WidgetCatalog"/>'tan gelir (Widget'lar sayfası ve karşılamayla aynı liste).
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

        body.Children.Add(Section("Bölmeler"));
        var fences = new WrapPanel();
        WidgetCatalog.AddTiles(fences, WidgetCatalog.Fences(), Run);
        body.Children.Add(fences);

        body.Children.Add(Section("Araçlar"));
        var tools = new WrapPanel();
        WidgetCatalog.AddTiles(tools, WidgetCatalog.Tools, Run);
        body.Children.Add(tools);

        var footer = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
        var all = new Button
        {
            Content = "Masaüstümü bölmelere ayır", Appearance = ControlAppearance.Primary,
            Icon = new SymbolIcon { Symbol = SymbolRegular.Sparkle24 },
            ToolTip = "Klasörler, Kısayollar, Dosyalar ve PDF gibi klasörler için bölme kurar; masaüstü simgeleri yalnızca bölmelerde görünür",
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(all, "Add.SplitDesktop");
        all.Click += (_, _) => SplitDesktop();
        var more = new Button { Content = "Widget'ları yönet…", Margin = new Thickness(8, 0, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetAutomationId(more, "Add.Manage");
        more.Click += (_, _) =>
        {
            _closing = true;
            Close();
            (Application.Current as App)?.ShowPage(typeof(WidgetsPage));
        };
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

    /// <summary>Bu pencerenin ortası: yeni widget'lar bu pencerenin bulunduğu ekrana yerleşir.</summary>
    private NativeMethods.POINT? Center()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        return NativeMethods.GetWindowRect(hwnd, out var r)
            ? new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 }
            : null;
    }

    /// <summary>Widget'ı bu pencerenin bulunduğu ekrana ekler ve pencereyi kapatır.</summary>
    private void Run(WidgetChoice choice)
    {
        var near = Center();
        _closing = true;
        Close();
        WidgetCatalog.Invoke(choice, near);
    }

    /// <summary>"Masaüstümü bölmelere ayır": başlangıç bölmelerini kurup modu açar; bildirimden geri alınabilir.</summary>
    private void SplitDesktop()
    {
        var near = Center();
        _closing = true;
        Close();
        var ids = DesktopFences.TurnOn(allStarters: true, near);
        Notice.Show(DesktopFences.Describe(ids.Count), NoticeKind.Success, "Geri al", () => DesktopFences.Undo(ids),
            trayHint: "Geri almak için buraya tıkla.");
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
