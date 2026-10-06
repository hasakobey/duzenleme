using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using Image = System.Windows.Controls.Image;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Duzenleme.Views;

/// <summary>
/// "Widget ekle": her ekleme yolunun tek penceresi (tepsi, Ctrl+Alt+B, halkadaki "Widget ekle", ana pencere ve Başlat
/// menüsündeki "Widget ekle" kısayolu). İlk sayfa "Ne eklemek istersin?": 4 × 2 kutucuk (<see cref="WidgetCatalog.Featured"/>;
/// adın altında kısa açıklama), altında "Masaüstümü bölmelere ayır" ve "Hepsini göster (6)" (<see cref="WidgetCatalog.More"/>
/// aynı sayfada açılır). "Bölme ›" bölme sayfasını açar ("‹ Geri"): "Masaüstünden" türler, "Bir klasörü göster" klasörler,
/// küçük "Yeni bölme…" bağlantısı. Kutucukların altında "Masaüstünden" satırı (<see cref="DesktopAdd"/>): masaüstündeki en çok
/// altı öğe (kısayollar ve klasörler önce); klasöre basınca bölmesi, kısayol ya da dosyaya basınca kısayol kutusu eklenir.
/// Daha çoksa "Tümü…" aynı pencerede aranabilir, çok seçimli listeyi açar ("Ekle"). Pencereye masaüstünden dosya bırakmak da
/// aynısını yapar. Kutucuğa basınca pencere kapanır, widget ayardaki yere ("Yeni widget'ların yeri") gelir.
/// </summary>
internal sealed class QuickAddWindow : FluentWindow
{
    private static QuickAddWindow? _current;
    private readonly NativeMethods.POINT _anchor;
    private bool _closing;

    private const double BodyWidth = 560;

    /// <summary>Kutucuk ızgarasının genişliği (dört 128 DIP kutucuk ve aralarındaki 8 DIP).</summary>
    private const double TilesWidth = 4 * 128 + 3 * 8;

    /// <summary>"Masaüstünden" satırındaki altı kutucuk ızgarayla aynı genişlikte biter.</summary>
    private const double DesktopTileWidth = (TilesWidth - (DesktopPicks.RowCount - 1) * 8) / DesktopPicks.RowCount;

    private readonly ScrollViewer _scroller;
    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;
    private readonly Button _back;
    private readonly StackPanel _home;
    private readonly WrapPanel _featured;
    private readonly WrapPanel _more;
    private readonly Button _showMore;
    private readonly Button _newFence;
    private readonly Button _split;
    private readonly Button _addPicked;
    private readonly StackPanel _desktopSection;
    private readonly WrapPanel _desktopRow = new();
    private Action? _addSelected;

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
        _desktopSection = BuildDesktopSection();
        _home.Children.Add(_desktopSection);

        _scroller = new ScrollViewer
        {
            Content = _home, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false,
        };

        // Alt satır: solda "Masaüstümü bölmelere ayır" (iki sayfada da), sağda ilk sayfada "Hepsini göster (6)", bölme
        // sayfasında "Yeni bölme…".
        var footer = new DockPanel { Margin = new Thickness(22, 10, 22, 20), Width = BodyWidth, LastChildFill = false };
        var split = _split = new Button
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
        // "Tümü…" sayfasının birincil düğmesi: seçilenleri ekler (seçim yokken soluk).
        _addPicked = new Button
        {
            Content = L.T("Ekle"), Appearance = ControlAppearance.Primary, MinWidth = 100, IsEnabled = false, Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetAutomationId(_addPicked, "Add.FromDesktop.Add");
        _addPicked.Click += (_, _) => _addSelected?.Invoke();
        DockPanel.SetDock(_showMore, Dock.Right);
        DockPanel.SetDock(_newFence, Dock.Right);
        DockPanel.SetDock(_addPicked, Dock.Right);
        footer.Children.Add(split);
        footer.Children.Add(_showMore);
        footer.Children.Add(_newFence);
        footer.Children.Add(_addPicked);

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
            Cascade(_featured, _desktopRow);
        };
        // Yine de ölçeği farklı bir monitöre geçerse (WPF boyutu değiştirir) yer yeni boyuta göre bir kez daha hesaplanır.
        DpiChanged += (_, _) => { if (IsLoaded) Dispatcher.BeginInvoke(PlaceNearAnchor); };
        // Sayfa değişince ya da "Hepsini göster" ile boyu değişince pencere ekranın içinde kalır (yeri hareketsiz değişir).
        SizeChanged += (_, _) => { if (IsLoaded) Dispatcher.BeginInvoke(KeepOnScreen); };
        PreviewKeyDown += OnKey;
        // Açılır menü gibi: başka yere tıklanınca kapanır.
        // Masaüstündeki bir simgeye basılıp sürüklenmeye başlanmış olabilir: o zaman düğme bırakılınca kapanır (bu arada
        // pencereye bırakılırsa ekler).
        Deactivated += (_, _) => { if (!_closing) CloseWhenReleased(); };
        Closing += (_, _) => _closing = true;
        // Masaüstünden (ya da Gezgin'den) bırakılanlar: klasör bölme, öbürleri kısayol kutusu olur.
        AllowDrop = true;
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => ShowDropHint(false);
        Drop += OnDrop;
    }

    private string? _titleBeforeDrop;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DesktopDrop.EffectFor(e);
        e.Handled = true;
        ShowDropHint(e.Effects != DragDropEffects.None);
    }

    /// <summary>Bırakılabilir bir şey sürüklenirken başlık ne olacağını söyler; çıkınca eski başlık döner.</summary>
    private void ShowDropHint(bool show)
    {
        if (show && _titleBeforeDrop is null)
        {
            _titleBeforeDrop = _title.Text;
            _title.Text = L.T("Bırak: klasör bölme, öbürleri kısayol kutusu olur");
        }
        else if (!show && _titleBeforeDrop is not null)
        {
            _title.Text = _titleBeforeDrop;
            _titleBeforeDrop = null;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        ShowDropHint(false);
        if (DesktopDrop.PathsOf(e) is not { } paths) return;
        e.Effects = DesktopDrop.EffectFor(e);
        e.Handled = true;
        var near = Center();
        _closing = true;
        Close();
        DesktopAdd.AddDropped(paths, near);
    }

    /// <summary>
    /// Pencere etkinliğini kaybetti: birincil düğme basılı değilse hemen kapanır; basılıysa (masaüstünden sürükleme başlıyor
    /// olabilir) bırakıldıktan kısa süre sonra (bırakma önce işlensin). Bu arada pencereye dönülürse açık kalır.
    /// </summary>
    private void CloseWhenReleased()
    {
        static bool Held() => NativeMethods.GetAsyncKeyState(PrimaryMouseButton.VirtualKey(NativeMethods.MouseButtonsSwapped)) < 0;
        if (!Held())
        {
            Close();
            return;
        }
        var started = Environment.TickCount64;
        long released = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) =>
        {
            if (_closing || IsActive)
            {
                timer.Stop();
                return;
            }
            var now = Environment.TickCount64;
            if (released == 0 && Held() && now - started < 60_000) return;
            if (released == 0) released = now;
            if (now - released < 400) return;
            timer.Stop();
            ShowDropHint(false);
            Close();
        };
        timer.Start();
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
        else if (_back.Visibility == Visibility.Visible && e.OriginalSource is not System.Windows.Controls.Primitives.TextBoxBase &&
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
        _split.Visibility = Visibility.Visible;
        _addPicked.Visibility = Visibility.Collapsed;
        _addSelected = null;
        _scroller.Content = _home;
        _scroller.ScrollToTop();
        if (cascade) Cascade(_featured, _more, _desktopRow);
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
        _split.Visibility = Visibility.Visible;
        _addPicked.Visibility = Visibility.Collapsed;
        _addSelected = null;
        _scroller.Content = page;
        _scroller.ScrollToTop();
        Cascade(desktop, folders);
        FocusFirst(desktop);
    }

    /// <summary>
    /// "Masaüstünden": başlık (öğeler satıra sığmıyorsa sağında "Tümü…") ve masaüstündeki ilk <see cref="DesktopPicks.RowCount"/>
    /// öğe. Önerilecek bir şey yoksa bölüm hiç görünmez. Bellekten kurulur (diske dokunmaz).
    /// </summary>
    private StackPanel BuildDesktopSection()
    {
        var section = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        AutomationProperties.SetAutomationId(section, "Add.FromDesktop");
        AutomationProperties.SetName(section, L.T("Masaüstünden"));
        var picks = DesktopAdd.Current();
        if (picks.Count == 0)
        {
            section.Visibility = Visibility.Collapsed;
            return section;
        }
        var head = new DockPanel { Margin = new Thickness(0, 0, BodyWidth - TilesWidth, 6) };
        if (picks.Count > DesktopPicks.RowCount)
        {
            var all = new Button { Content = L.T("Tümü…"), Appearance = ControlAppearance.Transparent, Padding = new Thickness(8, 3, 8, 3) };
            AutomationProperties.SetAutomationId(all, "Add.FromDesktop.All");
            AutomationProperties.SetName(all, L.T("Tümü"));
            AutomationProperties.SetHelpText(all, L.T("Masaüstündeki bütün öğeler; aranabilir, birden çok seçilebilir"));
            all.Click += (_, _) => ShowDesktop();
            DockPanel.SetDock(all, Dock.Right);
            head.Children.Add(all);
        }
        var label = Section(L.T("Masaüstünden"), top: 0);
        label.Margin = new Thickness(0);
        label.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(label);
        section.Children.Add(head);
        foreach (var pick in picks.Take(DesktopPicks.RowCount)) _desktopRow.Children.Add(DesktopTile(pick));
        section.Children.Add(_desktopRow);
        return section;
    }

    /// <summary>Öğenin ne olacağı (ipucu ve ekran okuyucu): klasör bölme, öbürleri kısayol kutusu.</summary>
    private static string WhatHappens(DesktopPick pick) => pick.IsFolder ? L.T("Bölme olarak eklenir") : L.T("Kısayol kutusuna eklenir");

    /// <summary>Masaüstü öğesi kutucuğu: 32 DIP kabuk simgesi (arka planda gelir) ve adı (en çok iki satır). AutomationId "Add.Desktop:" + ad.</summary>
    private Button DesktopTile(DesktopPick pick)
    {
        var image = new Image { Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        ShellIconImage.SetPath(image, pick.Path);
        var label = new TextBlock
        {
            Text = pick.Name, FontSize = 12, Margin = new Thickness(0, 6, 0, 0), TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 74, MaxHeight = 32,
        };
        var content = new StackPanel();
        content.Children.Add(image);
        content.Children.Add(label);
        var button = new Button
        {
            // Simgeler aynı hizada dursun (ad iki satıra geçse de): içerik üstten başlar.
            Content = content, Width = DesktopTileWidth, Height = 86, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(4, 12, 4, 4),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Top,
            ToolTip = pick.Name + "\n" + WhatHappens(pick),
        };
        AutomationProperties.SetAutomationId(button, "Add.Desktop:" + pick.Name);
        AutomationProperties.SetName(button, pick.Name);
        AutomationProperties.SetHelpText(button, WhatHappens(pick));
        Motion.SetPress(button, true);
        button.Click += (_, _) => RunDesktop([pick]);
        return button;
    }

    /// <summary>Masaüstü öğelerini ekler: pencere kapanır, bölme ve kutu bu pencerenin ekranına gelir.</summary>
    private void RunDesktop(IReadOnlyList<DesktopPick> picks)
    {
        if (picks.Count == 0) return;
        var near = Center();
        _closing = true;
        Close();
        DesktopAdd.Add(picks, near);
    }

    /// <summary>"Tümü…" listesinin satırı.</summary>
    private sealed class DesktopRow(DesktopPick pick)
    {
        public string Path => pick.Path;
        public string Name => pick.Name;
        public string Where => pick.IsFolder ? L.T("Bölme") : L.T("Kısayol kutusu");
        public string Help => WhatHappens(pick);
        public string AutomationId => "Add.Desktop:" + pick.Name;
        public override string ToString() => pick.Name;
    }

    /// <summary>
    /// "Tümü…": masaüstündeki bütün öneriler, üstte arama; birden çok seçilir (tıklamak seçer/bırakır, Boşluk da), "Ekle" ya
    /// da Enter seçilenleri ekler. Klasörler bölme (en çok beş), kısayollar ve dosyalar tek kutu. "‹ Geri" ilk sayfaya döner.
    /// </summary>
    private void ShowDesktop()
    {
        var picks = DesktopAdd.Current();
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var search = new TextBox { PlaceholderText = L.T("Ara"), Margin = new Thickness(0, 0, 0, 10) };
        AutomationProperties.SetName(search, L.T("Ara"));
        AutomationProperties.SetAutomationId(search, "Add.FromDesktop.Search");
        var list = new ListBox
        {
            Height = 320, SelectionMode = SelectionMode.Multiple, ItemTemplate = DesktopRowTemplate(), ItemContainerStyle = DesktopRowStyle(),
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);
        AutomationProperties.SetName(list, L.T("Masaüstündekiler"));
        AutomationProperties.SetAutomationId(list, "Add.FromDesktop.List");
        var status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 340,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false,
        };
        status.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        AutomationProperties.SetAutomationId(status, "Add.FromDesktop.Status");
        var listArea = new Grid();
        listArea.Children.Add(list);
        listArea.Children.Add(status);

        var filling = false;
        void UpdateButton()
        {
            _addPicked.IsEnabled = selected.Count > 0;
            _addPicked.Content = selected.Count > 0 ? L.F("Ekle ({0})", selected.Count) : L.T("Ekle");
        }
        void Fill()
        {
            filling = true;
            var rows = DesktopPicks.Filter(picks, search.Text).Select(p => new DesktopRow(p)).ToList();
            list.ItemsSource = rows;
            foreach (var row in rows.Where(r => selected.Contains(r.Path))) list.SelectedItems.Add(row);
            status.Text = rows.Count > 0 ? "" : picks.Count == 0 ? L.T("Masaüstünde eklenecek bir şey yok.") : L.T("Aramaya uyan öğe yok.");
            status.Visibility = rows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            filling = false;
        }
        list.SelectionChanged += (_, e) =>
        {
            if (filling) return;
            foreach (var row in e.AddedItems.OfType<DesktopRow>()) selected.Add(row.Path);
            foreach (var row in e.RemovedItems.OfType<DesktopRow>()) selected.Remove(row.Path);
            UpdateButton();
        };
        search.TextChanged += (_, _) => Fill();
        _addSelected = () => RunDesktop(picks.Where(p => selected.Contains(p.Path)).ToList());
        search.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && list.Items.Count > 0)
            {
                e.Handled = true;
                list.ScrollIntoView(list.Items[0]);
                (list.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                _addSelected?.Invoke();
            }
        };
        list.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            _addSelected?.Invoke();
        };

        var page = new StackPanel { Margin = new Thickness(22, 0, 22, 0), Width = BodyWidth };
        page.Children.Add(search);
        page.Children.Add(listArea);

        _title.Text = L.T("Masaüstünden ekle");
        _subtitle.Text = L.T("Klasör bölme olur; kısayollar ve dosyalar kısayol kutusuna girer. Birden çok seçebilirsin.");
        _subtitle.Visibility = Visibility.Visible;
        _back.Visibility = Visibility.Visible;
        _showMore.Visibility = Visibility.Collapsed;
        _newFence.Visibility = Visibility.Collapsed;
        _split.Visibility = Visibility.Collapsed;
        _addPicked.Visibility = Visibility.Visible;
        UpdateButton();
        Fill();
        _scroller.Content = page;
        _scroller.ScrollToTop();
        Dispatcher.BeginInvoke(() => { search.Focus(); Keyboard.Focus(search); }, DispatcherPriority.Input);
    }

    /// <summary>Satır: 24 DIP simge, ad (kısaltılır), sağda gri "Bölme"/"Kısayol kutusu" ve seçiliyse onay işareti.</summary>
    private static DataTemplate DesktopRowTemplate()
    {
        var dock = new FrameworkElementFactory(typeof(DockPanel));
        dock.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 3, 0, 3));
        var check = new FrameworkElementFactory(typeof(SymbolIcon));
        check.SetValue(SymbolIcon.SymbolProperty, SymbolRegular.Checkmark24);
        check.SetValue(FrameworkElement.WidthProperty, 20.0);
        check.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 0, 0, 0));
        check.SetValue(DockPanel.DockProperty, Dock.Right);
        check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        check.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(ListBoxItem.IsSelected))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1),
            Converter = new BooleanToVisibilityConverter(),
        });
        var where = new FrameworkElementFactory(typeof(TextBlock));
        where.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopRow.Where)));
        where.SetValue(TextBlock.FontSizeProperty, 12.0);
        where.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 0, 0, 0));
        where.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        where.SetValue(DockPanel.DockProperty, Dock.Right);
        where.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var image = new FrameworkElementFactory(typeof(Image));
        image.SetValue(FrameworkElement.WidthProperty, 24.0);
        image.SetValue(FrameworkElement.HeightProperty, 24.0);
        image.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
        image.SetValue(DockPanel.DockProperty, Dock.Left);
        image.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
        image.SetBinding(ShellIconImage.PathProperty, new Binding(nameof(DesktopRow.Path)));
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding(nameof(DesktopRow.Name)));
        name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        where.Name = "Where";
        dock.AppendChild(check);
        dock.AppendChild(where);
        dock.AppendChild(image);
        dock.AppendChild(name);
        var template = new DataTemplate { VisualTree = dock };
        // Seçili satırın vurgu zemininde ikincil renk okunmaz: tür yazısı satırın kendi yazı rengini alır.
        var item = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1);
        var selected = new DataTrigger { Binding = new Binding(nameof(ListBoxItem.IsSelected)) { RelativeSource = item }, Value = true };
        selected.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding(nameof(Control.Foreground)) { RelativeSource = item }, "Where"));
        template.Triggers.Add(selected);
        return template;
    }

    /// <summary>Satırın kabı: ekran okuyucu adı ve ne olacağını, UI Automation "Add.Desktop:" + adı görür.</summary>
    private static Style DesktopRowStyle()
    {
        var style = new Style(typeof(ListBoxItem), Application.Current?.TryFindResource(typeof(ListBoxItem)) as Style);
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 4, 10, 4)));
        style.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(nameof(DesktopRow.Name))));
        style.Setters.Add(new Setter(AutomationProperties.HelpTextProperty, new Binding(nameof(DesktopRow.Help))));
        style.Setters.Add(new Setter(AutomationProperties.AutomationIdProperty, new Binding(nameof(DesktopRow.AutomationId))));
        return style;
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
