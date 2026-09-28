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

    /// <summary>İmlecin (ya da testte NESTDESK_QUICKADD_AT="x,y" noktasının; eski adı DUZENLEME_QUICKADD_AT) bulunduğu yerde açar.</summary>
    public static void ShowNearCursor()
    {
        if (_current is not null)
        {
            _current.Activate();
            return;
        }
        NativeMethods.GetCursorPos(out var anchor);
        if (NativeMethods.PointFromEnvironment("DUZENLEME_QUICKADD_AT") is { } test) anchor = test;
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
        // Pencere en baştan imlecin monitöründe oluşur (ölçeği farklıysa sonradan büyüyüp ekrandan taşmasın) ve o ekranın
        // çalışma alanından uzun olamaz: kutucuklar kayar, başlık ve alttaki düğmeler hep görünür.
        WindowFit.StartOn(this, anchor);
        MaxHeight = Math.Max(240, NativeMethods.WorkAreaAt(anchor).Height / NativeMethods.ScaleAt(anchor) - 16);

        const double width = 560;
        var header = new StackPanel { Margin = new Thickness(22, 2, 22, 0), Width = width };
        header.Children.Add(new TextBlock
        {
            Text = "Masaüstüne ne eklemek istersin?", FontSize = 20, FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorPrimaryBrush"),
        });
        header.Children.Add(Muted("Birine tıkla, hemen masaüstüne gelsin. Sonra sürükleyerek taşı, kenarından büyüt, sağ tıklayarak ayarla.", 4));

        var body = new StackPanel { Margin = new Thickness(22, 0, 22, 0), Width = width };
        body.Children.Add(Section("Bölmeler"));
        var fences = new WrapPanel();
        WidgetCatalog.AddTiles(fences, WidgetCatalog.Fences(), Run);
        body.Children.Add(fences);

        body.Children.Add(Section("Araçlar"));
        var tools = new WrapPanel();
        WidgetCatalog.AddTiles(tools, WidgetCatalog.Tools, Run);
        body.Children.Add(tools);

        var scroller = new ScrollViewer
        {
            Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false,
        };

        var footer = new DockPanel { Margin = new Thickness(22, 14, 22, 20), Width = width, LastChildFill = false };
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

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TitleBar { Title = "Widget ekle", ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(header, 1);
        root.Children.Add(header);
        Grid.SetRow(scroller, 2);
        root.Children.Add(scroller);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);
        Content = root;

        Loaded += (_, _) => PlaceNearAnchor();
        // Yine de ölçeği farklı bir monitöre geçerse (WPF boyutu değiştirir) yer yeni boyuta göre bir kez daha hesaplanır.
        DpiChanged += (_, _) => { if (IsLoaded) Dispatcher.BeginInvoke(PlaceNearAnchor); };
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

    /// <summary>
    /// Pencereyi kapatır ve widget'ı ayardaki yere (varsayılan: bu pencerenin olduğu yer, imlecin yanı) ekler. Widget'lar
    /// gizliyse ya da göz atılıyorsa geri gelir; ilk seferlerde nereye eklendiği tepside söylenir.
    /// </summary>
    private void Run(WidgetChoice choice)
    {
        var near = Center();
        _closing = true;
        Close();
        if (WidgetCatalog.Invoke(choice, near, followSetting: true) is { } config &&
            AppHost.Settings.Widgets.Any(w => w.Id == config.Id))
            AppHost.ShowNewWidgetHint(config);
    }

    /// <summary>"Masaüstümü bölmelere ayır": başlangıç bölmelerini kurup modu açar; bildirimden geri alınabilir.</summary>
    private void SplitDesktop()
    {
        var near = Center();
        _closing = true;
        Close();
        // Mod zaten açıksa "Geri al" onu kapatmaz; yalnızca eklenen bölmeleri kaldırır.
        DesktopFences.TurnOnWithNotice(allStarters: true, near, trayHint: "Geri almak için buraya tıkla.");
    }

    /// <summary>Pencereyi imlecin üstünde ortalar; ekrandan taşmasın. Boyut pencere imlecin monitöründeyken okunur.</summary>
    private void PlaceNearAnchor()
    {
        if (_closing) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(hwnd, out var r)) return;
        var work = NativeMethods.WorkAreaAt(_anchor);
        var x = Math.Clamp(_anchor.X - r.Width / 2, work.Left, Math.Max(work.Left, work.Right - r.Width));
        var y = Math.Clamp(_anchor.Y - r.Height / 3, work.Top, Math.Max(work.Top, work.Bottom - r.Height));
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
        Activate();
    }
}
