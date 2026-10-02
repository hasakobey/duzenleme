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
/// "Widget ekle": her ekleme yolunun tek penceresi (tepsi, Ctrl+Alt+B, halkadaki "Widget ekle", ana pencere ve Başlat
/// menüsündeki "Widget ekle" kısayolu). İlk sayfa "Ne eklemek istersin?": 4 × 2 kutucuk (<see cref="WidgetCatalog.Featured"/>;
/// adın altında kısa açıklama), altında "Masaüstümü bölmelere ayır" ve "Hepsini göster (6)" (<see cref="WidgetCatalog.More"/>
/// aynı sayfada açılır). "Bölme ›" bölme sayfasını açar ("‹ Geri"): "Masaüstünden" türler, "Bir klasörü göster" klasörler,
/// küçük "Yeni bölme…" bağlantısı. Kutucuğa basınca pencere kapanır, widget ayardaki yere ("Yeni widget'ların yeri") gelir.
/// </summary>
internal sealed class QuickAddWindow : FluentWindow
{
    private static QuickAddWindow? _current;
    private readonly NativeMethods.POINT _anchor;
    private bool _closing;

    private const double BodyWidth = 560;

    private readonly ScrollViewer _scroller;
    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;
    private readonly Button _back;
    private readonly StackPanel _home;
    private readonly WrapPanel _featured;
    private readonly WrapPanel _more;
    private readonly Button _showMore;
    private readonly Button _newFence;

    /// <summary>İmlecin (ya da testte NESTDESK_QUICKADD_AT="x,y" noktasının; eski adı DUZENLEME_QUICKADD_AT) bulunduğu yerde açar.</summary>
    public static void ShowNearCursor()
    {
        if (_current is not null)
        {
            _current.Activate();
            return;
        }
        NativeMethods.GetCursorPos(out var anchor);
        if (NativeMethods.PointFromEnvironment("QUICKADD_AT") is { } test) anchor = test;
        _current = new QuickAddWindow(anchor);
        _current.Closed += (_, _) => _current = null;
        _current.Show();
    }

    private QuickAddWindow(NativeMethods.POINT anchor)
    {
        _anchor = anchor;
        Title = L.T("Widget ekle");
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
        WindowFit.FitChromeToContent(this);
        WindowFit.StartOn(this, anchor);
        MaxHeight = Math.Max(240, NativeMethods.WorkAreaAt(anchor).Height / NativeMethods.ScaleAt(anchor) - 16);

        // Başlık: ilk sayfada yalnızca soru; bölme sayfasında solunda "‹ Geri" ve altında tek satır açıklama.
        _back = new Button
        {
            Content = L.T("‹ Geri"), Appearance = ControlAppearance.Transparent, Margin = new Thickness(-8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed,
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(_back, "Add.Back");
        System.Windows.Automation.AutomationProperties.SetName(_back, L.T("Geri"));
        _back.Click += (_, _) => ShowHome();
        _title = new TextBlock
        {
            FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
        };
        _title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        var titleRow = new DockPanel();
        DockPanel.SetDock(_back, Dock.Left);
        titleRow.Children.Add(_back);
        titleRow.Children.Add(_title);
        _subtitle = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        _subtitle.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var header = new StackPanel { Margin = new Thickness(22, 2, 22, 12), Width = BodyWidth };
        header.Children.Add(titleRow);
        header.Children.Add(_subtitle);

        // İlk sayfa: 8 kutucuk (4 × 2) ve "Hepsini göster" ile altında açılan 6 araç.
        _featured = new WrapPanel();
        WidgetCatalog.AddTiles(_featured, WidgetCatalog.Featured, Run);
        _more = new WrapPanel { Visibility = Visibility.Collapsed };
        WidgetCatalog.AddTiles(_more, WidgetCatalog.More, Run);
        _home = new StackPanel { Margin = new Thickness(22, 0, 22, 0), Width = BodyWidth };
        _home.Children.Add(_featured);
        _home.Children.Add(_more);

        _scroller = new ScrollViewer
        {
            Content = _home, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false,
        };

        // Alt satır: solda "Masaüstümü bölmelere ayır" (iki sayfada da), sağda ilk sayfada "Hepsini göster (6)", bölme
        // sayfasında "Yeni bölme…".
        var footer = new DockPanel { Margin = new Thickness(22, 10, 22, 20), Width = BodyWidth, LastChildFill = false };
        var split = new Button
        {
            Content = L.T("Masaüstümü bölmelere ayır"), Appearance = ControlAppearance.Primary,
            Icon = new SymbolIcon { Symbol = SymbolRegular.Sparkle24 },
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(split, "Add.SplitDesktop");
        System.Windows.Automation.AutomationProperties.SetHelpText(split,
            L.T("Klasörler, Kısayollar, Dosyalar ve PDF gibi klasörler için bölme kurar; masaüstü simgeleri yalnızca bölmelerde görünür"));
        split.Click += (_, _) => SplitDesktop();
        _showMore = new Button { Content = L.F("Hepsini göster ({0})", WidgetCatalog.MoreKeys.Count), Appearance = ControlAppearance.Transparent };
        System.Windows.Automation.AutomationProperties.SetAutomationId(_showMore, "Add.More");
        _showMore.Click += (_, _) => ShowMore();
        var newFence = WidgetCatalog.NewFence;
        _newFence = new Button { Content = newFence.Label, Appearance = ControlAppearance.Transparent, Visibility = Visibility.Collapsed };
        System.Windows.Automation.AutomationProperties.SetAutomationId(_newFence, "Add." + newFence.Key);
        System.Windows.Automation.AutomationProperties.SetHelpText(_newFence, newFence.Tip);
        _newFence.Click += (_, _) => Run(newFence);
        DockPanel.SetDock(_showMore, Dock.Right);
        DockPanel.SetDock(_newFence, Dock.Right);
        footer.Children.Add(split);
        footer.Children.Add(_showMore);
        footer.Children.Add(_newFence);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TitleBar { Title = L.T("Widget ekle"), ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(header, 1);
        root.Children.Add(header);
        Grid.SetRow(_scroller, 2);
        root.Children.Add(_scroller);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);
        Content = root;
        System.Windows.Automation.AutomationProperties.SetAutomationId(this, "Add");
        ShowHome(cascade: false);

        Loaded += (_, _) =>
        {
            PlaceNearAnchor();
            // Kutucuklar kısa bir kademeyle hafifçe büyüyerek gelir (Views.Motion: bütünü en çok 300 ms, Sakin'de birlikte
            // solar, Kapalı'da hemen yerinde). Girdi beklemez: kutucuğa hemen basılabilir.
            Cascade(_featured);
        };
        // Yine de ölçeği farklı bir monitöre geçerse (WPF boyutu değiştirir) yer yeni boyuta göre bir kez daha hesaplanır.
        DpiChanged += (_, _) => { if (IsLoaded) Dispatcher.BeginInvoke(PlaceNearAnchor); };
        // Sayfa değişince ya da "Hepsini göster" ile boyu değişince pencere ekranın içinde kalır (yeri hareketsiz değişir).
        SizeChanged += (_, _) => { if (IsLoaded) Dispatcher.BeginInvoke(KeepOnScreen); };
        PreviewKeyDown += OnKey;
        // Açılır menü gibi: başka yere tıklanınca kapanır.
        Deactivated += (_, _) => { if (!_closing) Close(); };
        Closing += (_, _) => _closing = true;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        // Bölme sayfasından geri: Alt+Sol ve Geri tuşu (tarayıcı ve Ayarlar gibi).
        else if (_back.Visibility == Visibility.Visible &&
                 ((key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt) || (key == Key.Back && Keyboard.Modifiers == ModifierKeys.None)))
        {
            e.Handled = true;
            ShowHome();
        }
    }

    /// <summary>İlk sayfa: "Ne eklemek istersin?" ve 8 kutucuk (açıldıysa altında öbür 6).</summary>
    private void ShowHome(bool cascade = true)
    {
        _title.Text = L.T("Ne eklemek istersin?");
        _subtitle.Visibility = Visibility.Collapsed;
        _back.Visibility = Visibility.Collapsed;
        _showMore.Visibility = _more.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        _newFence.Visibility = Visibility.Collapsed;
        _scroller.Content = _home;
        _scroller.ScrollToTop();
        if (cascade) Cascade(_featured, _more);
        FocusFirst(_featured);
    }

    /// <summary>"Hepsini göster": öbür altı araç aynı sayfada, sekiz kutucuğun altında (bağlantı kalkar).</summary>
    private void ShowMore()
    {
        _more.Visibility = Visibility.Visible;
        _showMore.Visibility = Visibility.Collapsed;
        Cascade(_more);
        FocusFirst(_more);
    }

    /// <summary>
    /// Bölme sayfası: "Masaüstünden" (Klasörler, Kısayollar, Dosyalar, Tüm masaüstü) ve "Bir klasörü göster" (masaüstündeki
    /// klasörler, İndirilenler/Belgeler/Resimler, eklenince açılacak kural klasörleri, "Başka klasör…"). Her açılışta yeniden
    /// kurulur (klasörler değişebilir; bellekten, diske dokunmadan).
    /// </summary>
    private void ShowFences()
    {
        var page = new StackPanel { Margin = new Thickness(22, 0, 22, 0), Width = BodyWidth };
        page.Children.Add(Section(L.T("Masaüstünden"), top: 0));
        var desktop = new WrapPanel();
        WidgetCatalog.AddTiles(desktop, WidgetCatalog.DesktopFences(), Run);
        page.Children.Add(desktop);
        page.Children.Add(Section(L.T("Bir klasörü göster"), top: 10));
        var folders = new WrapPanel();
        WidgetCatalog.AddTiles(folders, WidgetCatalog.FolderFences(), Run);
        page.Children.Add(folders);

        _title.Text = L.T("Bölme ekle");
        _subtitle.Text = L.T("Bölme, masaüstündeki öğeleri ya da bir klasörün içini gösterir.");
        _subtitle.Visibility = Visibility.Visible;
        _back.Visibility = Visibility.Visible;
        _showMore.Visibility = Visibility.Collapsed;
        _newFence.Visibility = Visibility.Visible;
        _scroller.Content = page;
        _scroller.ScrollToTop();
        Cascade(desktop, folders);
        FocusFirst(desktop);
    }

    private static TextBlock Section(string text, double top) => new()
    {
        Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, top, 0, 8),
    };

    /// <summary>Görünen kutucuklar kısa bir kademeyle gelir (Views.Motion; en çok 10 kutucuk oynar, kalanı hemen yerinde).</summary>
    private static void Cascade(params Panel[] panels) =>
        Motion.Cascade(panels.Where(p => p.Visibility == Visibility.Visible)
            .SelectMany(p => p.Children.OfType<UIElement>()).Where(t => t.Visibility == Visibility.Visible).ToList(), MotionKind.Pop);

    /// <summary>Klavyeyle gelen: odak sayfanın ilk kutucuğunda (Sekme ve oklar kutucuklarda gezer).</summary>
    private void FocusFirst(Panel panel)
    {
        if (!IsLoaded) return;
        Dispatcher.BeginInvoke(() => panel.Children.OfType<UIElement>().FirstOrDefault(t => t.Visibility == Visibility.Visible)?.Focus(),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>Bu pencerenin ortası: yeni widget'lar bu pencerenin bulunduğu ekrana yerleşir.</summary>
    private NativeMethods.POINT? Center()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        return NativeMethods.GetWindowRect(hwnd, out var r)
            ? new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 }
            : null;
    }

    /// <summary>
    /// Kutucuk: "Bölme ›" bölme sayfasını açar; öbürleri pencereyi kapatıp widget'ı ayardaki yere (varsayılan: bu pencerenin
    /// olduğu yer, imlecin yanı) ekler. Widget'lar gizliyse ya da göz atılıyorsa geri gelir; eklendiği söylenir
    /// (<see cref="WidgetCatalog.AddAndAnnounce"/>).
    /// </summary>
    private void Run(WidgetChoice choice)
    {
        if (choice.Key == WidgetCatalog.FenceKey)
        {
            ShowFences();
            return;
        }
        var near = Center();
        _closing = true;
        Close();
        WidgetCatalog.AddAndAnnounce(choice, near);
    }

    /// <summary>"Masaüstümü bölmelere ayır": başlangıç bölmelerini kurup modu açar; bildirimden geri alınabilir.</summary>
    private void SplitDesktop()
    {
        var near = Center();
        _closing = true;
        Close();
        // Ayarlar ve karşılamayla aynı yol: kutu kipi kapanır (bölmeler sonra kapanınca dosya taşıma sessizce geri gelmesin) ve
        // kutulara taşınmış öğeler varsa masaüstüne geri konmaları sorulur. Mod zaten açıksa "Geri al" onu kapatmaz; yalnızca
        // eklenen bölmeleri kaldırır.
        DesktopModes.Set(IconMode.FencesOnly, near, owner: null, allStarters: true, trayHint: L.T("Geri almak için buraya tıkla."));
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

    /// <summary>Boyu değişen pencere ekranın içinde kalır (yalnızca taşıyorsa, en kısa yoldan; canlandırma yok).</summary>
    private void KeepOnScreen()
    {
        if (_closing) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(hwnd, out var r)) return;
        // Pencerenin şimdi bulunduğu ekran (başlığından sürüklenmiş olabilir).
        var work = NativeMethods.WorkAreaAt(new NativeMethods.POINT { X = r.Left + r.Width / 2, Y = r.Top + Math.Min(r.Height / 2, 40) });
        var x = Math.Clamp(r.Left, work.Left, Math.Max(work.Left, work.Right - r.Width));
        var y = Math.Clamp(r.Top, work.Top, Math.Max(work.Top, work.Bottom - r.Height));
        if (x == r.Left && y == r.Top) return;
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }
}
