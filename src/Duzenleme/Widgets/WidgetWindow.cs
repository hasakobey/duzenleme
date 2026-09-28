using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Masaüstüne yapışık, çerçevesiz ve yarı saydam widget penceresi.
/// Pencereler hep en altta durur, görev çubuğunda/Alt+Tab'da görünmez ve "Masaüstünü göster" ile kaybolmaz.
/// </summary>
public sealed class WidgetWindow : Window
{
    private const double ShadowMargin = 14;
    private const double MinScale = 0.5, MaxScale = 2.5;
    private const double MinResizableWidth = 190, MinResizableHeight = 140;
    private const double DefaultWidth = 360, DefaultHeight = 270;

    private readonly Border _card;
    private readonly DropShadowEffect _shadow = new() { BlurRadius = 26, ShadowDepth = 4, Direction = 270, Opacity = 0.32, Color = Colors.Black };
    private readonly DispatcherTimer _saveTimer;
    private bool _positionReady;

    public WidgetConfig Config { get; }
    public IWidgetView View { get; }

    public WidgetWindow(WidgetConfig config, IWidgetView view)
    {
        Config = config;
        View = view;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;
        Title = $"{AppInfo.Name} widget";

        // Kaldırma düğmesi (×) her widget'ın kendi görünümünde, sağ üstte hep yerinde durur (WidgetCloseButton):
        // fare widget'a gelince hiçbir şey belirip kaybolmaz.
        _card = new Border
        {
            BorderThickness = new Thickness(1),
            Padding = view.CardPadding,
            Margin = new Thickness(ShadowMargin),
            Child = (UIElement)view,
        };
        Content = _card;
        // WPF ilk açılan pencereyi Application.MainWindow yapar; widget ana pencere sayılırsa tema değişikliği
        // (WPF-UI) onun saydam zeminini opak bir dikdörtgene çevirebilir.
        if (Application.Current?.MainWindow == this) Application.Current.MainWindow = null;

        _card.MouseLeftButtonDown += (_, e) => { if (!BeginResize(e)) BeginDrag(e); };
        _card.MouseMove += (_, e) =>
        {
            if (_resizing) ContinueResize();
            else if (_dragging) ContinueDrag();
            else _card.Cursor = CursorFor(GripAt(e.GetPosition(_card)));
        };
        _card.MouseLeave += (_, _) => { if (!_resizing) _card.Cursor = null; };
        _card.MouseLeftButtonUp += (_, _) => { EndResize(); EndDrag(); };
        _card.LostMouseCapture += (_, _) => { EndResize(); EndDrag(); };
        PreviewMouseWheel += OnWheel;
        View.MenuRequested += () => Dispatcher.BeginInvoke(() =>
        {
            var menu = _card.ContextMenu;
            menu.PlacementTarget = _card;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }, DispatcherPriority.Input);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveBounds(); };
        // Konum/boyut yalnızca kullanıcı taşıyınca/boyutlandırınca kaydedilir. Monitör çıkarılınca ya da çözünürlük
        // değişince Windows'un pencereyi kaydırması kayıtlı düzeni bozmasın; ekran geri gelince eski yerine döner.
        MouseEnter += (_, _) => OnHover(true);
        MouseLeave += (_, _) => OnHover(false);
        View.LayoutChanged += () => { ApplyLayoutMode(); QueueSave(); ResolveOverlapAfterLayout(); };
        PreviewDragEnter += (_, _) => OnHover(true);
        _rollupTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _rollupTimer.Tick += (_, _) => { _rollupTimer.Stop(); TryRollUp(); };
        View.CollapseToggleRequested += () => SetCollapsed(!Config.Collapsed);

        ApplyStyle();
        ApplyLayoutMode();
        _card.ContextMenu = Menus.Dynamic(FillMenu);
        Loaded += (_, _) => PlaceOnScreen();

        // Pencereyi en baştan kayıtlı monitörde oluştur: sonradan ölçeği (DPI) farklı bir monitöre taşınırsa WPF onu
        // etkinleştirerek yeniden boyutlar (odak çalınır, açılış yavaşlar). Konum henüz pencere yokken sistem
        // ölçeğiyle çevrilir; OnSourceInitialized kesin fiziksel konuma oturtur.
        if (Config.PixelLeft is int px && Config.PixelTop is int py && IsOnSomeMonitor(px, py))
        {
            var systemScale = GetDpiForSystem() / 96.0;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = px / systemScale;
            Top = py / systemScale;
        }

        if (DebugLog.Enabled)
        {
            var tag = $"[{Config.Kind}:{Config.Id[..6]}]";
            _card.ContextMenuOpening += (_, e) => DebugLog.Write($"{tag} ContextMenuOpening src={e.OriginalSource?.GetType().Name} handled={e.Handled}");
            _card.ContextMenu.Opened += (_, _) => DebugLog.Write($"{tag} menu Opened items={_card.ContextMenu.Items.Count}");
            _card.ContextMenu.Closed += (_, _) => DebugLog.Write($"{tag} menu Closed");
            PreviewMouseRightButtonUp += (_, e) => DebugLog.Write($"{tag} PreviewMouseRightButtonUp src={e.OriginalSource?.GetType().Name}");
            PreviewMouseLeftButtonDown += (_, e) => DebugLog.Write($"{tag} PreviewMouseLeftButtonDown src={e.OriginalSource?.GetType().Name} clicks={e.ClickCount}");
            Activated += (_, _) => DebugLog.Write($"{tag} Activated");
            Deactivated += (_, _) => DebugLog.Write($"{tag} Deactivated");
            PreviewGotKeyboardFocus += (_, e) => DebugLog.Write($"{tag} GotKeyboardFocus -> {e.NewFocus?.GetType().Name}");
            PreviewKeyDown += (_, e) => DebugLog.Write($"{tag} KeyDown {e.Key}");
            LocationChanged += (_, _) => DebugLog.Write($"{tag} LocationChanged {Left:0},{Top:0}");
            SizeChanged += (_, e) => DebugLog.Write($"{tag} SizeChanged {e.NewSize.Width:0}x{e.NewSize.Height:0}");
            DpiChanged += (_, e) => DebugLog.Write($"{tag} DpiChanged {e.OldDpi.PixelsPerDip}->{e.NewDpi.PixelsPerDip}");
        }
    }

    public void ApplyStyle()
    {
        var palette = View.AdjustPalette(WidgetPalette.For(Config.Style, Config.Accent));
        _card.Background = palette.Background;
        _card.BorderBrush = palette.BorderBrush;
        _card.CornerRadius = new CornerRadius(Config.Corners switch { CornerStyle.Soft => 10, CornerStyle.Square => 3, _ => 20 });
        _card.Effect = Config.Shadow ? _shadow : null;
        TextElement.SetForeground(_card, palette.Foreground);
        var scale = Math.Clamp(Config.Scale, MinScale, MaxScale);
        _card.LayoutTransform = Math.Abs(scale - 1) < 0.01 ? Transform.Identity : new ScaleTransform(scale, scale);
        ApplyOpacity();
        View.ApplyPalette(palette);
    }

    /// <summary>
    /// Katlanmış bölme yalnızca başlık kadar yer kaplar ve yalnızca genişliği değişir.
    /// Boyutlandırma her zaman elle (kenarlardan) yapılır: sistemin boyutlandırma döngüsü Snap'i tetikleyebilir.
    /// </summary>
    private void ApplyLayoutMode()
    {
        var collapsed = IsCollapsedNow;
        View.SetBodyVisible(!collapsed);
        ResizeMode = ResizeMode.NoResize;
        if (View.Resizable && !collapsed)
        {
            SizeToContent = SizeToContent.Manual;
            MinWidth = MinResizableWidth;
            MinHeight = MinResizableHeight;
            if (_positionReady) Height = double.IsNaN(Config.Height) ? DefaultHeight : Config.Height;
        }
        else if (View.Resizable)
        {
            // Genişlik korunur, yükseklik başlığa iner (SizeToContent.Height genişliği bozduğu için elle ölçülür).
            SizeToContent = SizeToContent.Manual;
            MinWidth = MinResizableWidth;
            MinHeight = 0;
            if (_positionReady) FitCollapsedHeight();
        }
        else
        {
            SizeToContent = SizeToContent.WidthAndHeight;
            MinWidth = 0;
            MinHeight = 0;
        }
    }

    private void ApplyOpacity()
    {
        var opacity = Math.Clamp(Config.Opacity, 0.4, 1);
        Opacity = Config.FadeUntilHover && !IsMouseOver && !_revealing ? Math.Min(opacity, 0.35) : opacity;
    }

    // --- Fare üstünde değilken soluk durma ve başlığa katlanma ---
    private readonly DispatcherTimer _rollupTimer;
    private bool _rolledUp;

    /// <summary>Şu an başlığa katlı mı (kullanıcı katladı ya da fare çekildiği için otomatik katlandı)?</summary>
    private bool IsCollapsedNow => View.Collapsible && (Config.Collapsed || _rolledUp);

    private void OnHover(bool over)
    {
        if (Config.FadeUntilHover) ApplyOpacity();
        if (!Config.AutoRollup || !View.Collapsible) return;
        if (over)
        {
            _rollupTimer.Stop();
            if (_rolledUp)
            {
                _rolledUp = false;
                ApplyLayoutMode();
            }
        }
        else if (!Config.Collapsed) _rollupTimer.Start();
    }

    private void TryRollUp()
    {
        if (!Config.AutoRollup || _rolledUp || Config.Collapsed || !_positionReady) return;
        // Fare üstündeyken, sürüklerken, menü açıkken ya da içinde yazılırken (arama) katlanma.
        if (IsMouseOver || _dragging || _resizing || _card.ContextMenu?.IsOpen == true || IsKeyboardFocusWithin)
        {
            _rollupTimer.Start();
            return;
        }
        _rolledUp = true;
        ApplyLayoutMode();
    }

    /// <summary>Genişlik sabitlendikten sonra yüksekliği içeriğe bırakır (önce genişlik verilmezse WPF varsayılan genişliği kullanır).</summary>
    private void FitCollapsedHeight()
    {
        if (double.IsNaN(Width)) Width = ActualWidth;
        SizeToContent = SizeToContent.Height;
    }

    public void SetCollapsed(bool collapsed)
    {
        if (!View.Collapsible || Config.Collapsed == collapsed) return;
        if (collapsed) SaveBounds();
        Config.Collapsed = collapsed;
        ApplyLayoutMode();
        AppHost.SaveSettings();
    }

    /// <summary>
    /// Sağ tık menüsünün iskeleti: üstte widget'ın asıl işleri, altta "Görünüm ▸" ve seyrek kullanılanlar için
    /// "Diğer ▸", en altta "Kaldır". Widget'a özel parçaları görünüm sınıfı <see cref="WidgetMenu"/> bölümlerine koyar.
    /// </summary>
    private void FillMenu(ContextMenu menu)
    {
        var own = new WidgetMenu();
        View.AddMenuItems(own);

        menu.Items.Add(Menus.Item("Yeni widget ekle…", () => (Application.Current as App)?.ShowQuickAdd()));
        menu.Items.Add(new Separator());
        if (own.Primary.Count > 0)
        {
            foreach (var item in own.Primary) menu.Items.Add(item);
            menu.Items.Add(new Separator());
        }

        var look = new MenuItem { Header = "Görünüm" };
        if (own.Appearance.Count > 0)
        {
            foreach (var item in own.Appearance) look.Items.Add(item);
            look.Items.Add(new Separator());
        }
        // Not kendi kağıt rengini kullanır: arka plan ve vurgu rengi orada etkisizdir, gösterilmez.
        if (View.UsesThemeColors)
        {
            look.Items.Add(Menus.Choice("Arka plan", Config.Style,
                [(WidgetStyle.Glass, "Cam"), (WidgetStyle.Dark, "Koyu"), (WidgetStyle.Light, "Açık")],
                v => Update(() => Config.Style = v)));
            look.Items.Add(Menus.Choice("Vurgu rengi", Config.Accent,
                [(WidgetAccent.Violet, "Mor"), (WidgetAccent.Blue, "Mavi"), (WidgetAccent.Green, "Yeşil"), (WidgetAccent.Orange, "Turuncu"), (WidgetAccent.Pink, "Pembe")],
                v => Update(() => Config.Accent = v)));
        }
        look.Items.Add(Menus.Choice("Ölçek", Math.Round(Config.Scale, 2),
            [(0.7, "%70"), (0.85, "%85"), (1.0, "Normal (%100)"), (1.25, "%125"), (1.5, "%150"), (2.0, "%200")],
            v => Update(() => Config.Scale = v)));
        look.Items.Add(Menus.Choice("Saydamlık", Math.Round(Config.Opacity, 2),
            [(1.0, "Yok"), (0.85, "%15"), (0.7, "%30"), (0.55, "%45")],
            v => Update(() => Config.Opacity = v)));
        look.Items.Add(Menus.Choice("Köşeler", Config.Corners,
            [(CornerStyle.Round, "Yuvarlak"), (CornerStyle.Soft, "Hafif yuvarlak"), (CornerStyle.Square, "Köşeli")],
            v => Update(() => Config.Corners = v)));
        look.Items.Add(Menus.Toggle("Gölge", Config.Shadow, () => Update(() => Config.Shadow = !Config.Shadow)));
        look.Items.Add(Menus.Toggle("Fare üstünde değilken soluk dursun", Config.FadeUntilHover,
            () => Update(() => Config.FadeUntilHover = !Config.FadeUntilHover)));
        look.Items.Add(new Separator());
        look.Items.Add(Menus.Hint(View.Resizable
            ? "Boyut: kenarlardan sürükle · Simgeler: Ctrl + tekerlek · Izgaraya hizala: Shift"
            : "Boyut: sağ/alt kenardan sürükle ya da Ctrl + tekerlek · Izgaraya hizala: Shift"));
        menu.Items.Add(look);

        var more = new MenuItem { Header = "Diğer" };
        if (own.More.Count > 0)
        {
            foreach (var item in own.More) more.Items.Add(item);
            more.Items.Add(new Separator());
        }
        if (View.Collapsible)
        {
            more.Items.Add(Menus.Toggle("Başlığa katla", Config.Collapsed, () => SetCollapsed(!Config.Collapsed)));
            more.Items.Add(Menus.Toggle("Fare üstünde değilken başlığa katla", Config.AutoRollup, () =>
            {
                Config.AutoRollup = !Config.AutoRollup;
                AppHost.SaveSettings();
                if (!Config.AutoRollup && _rolledUp)
                {
                    _rolledUp = false;
                    ApplyLayoutMode();
                }
            }));
        }
        // Kilitli widget'ta kaldırma düğmesi de gizlenir (ApplyStyle görünümlere iletir).
        more.Items.Add(Menus.Toggle("Konumu kilitle", Config.Locked, () =>
        {
            Config.Locked = !Config.Locked;
            AppHost.SaveSettings();
            ApplyStyle();
        }));
        more.Items.Add(Menus.Toggle("Kenarlara yapışsın (mıknatıs)", AppHost.Settings.SnapWidgets, () =>
        {
            AppHost.Settings.SnapWidgets = !AppHost.Settings.SnapWidgets;
            AppHost.SaveSettings();
        }));
        more.Items.Add(Menus.Toggle("Widget'lar üst üste binmesin", AppHost.Settings.PreventOverlap, () =>
        {
            AppHost.Settings.PreventOverlap = !AppHost.Settings.PreventOverlap;
            AppHost.SaveSettings();
            if (AppHost.Settings.PreventOverlap) ResolveOverlap();
        }));
        more.Items.Add(Menus.Item("Tüm widget'ları düzenli yerleştir", () => AppHost.Widgets.ArrangeAll()));
        more.Items.Add(Menus.Item("Çoğalt", () => AppHost.Widgets.Duplicate(Config.Id)));
        more.Items.Add(new Separator());
        more.Items.Add(Menus.Hint("Taşırken Alt: yapışmadan · Shift: ızgaraya"));
        menu.Items.Add(more);

        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item("Kaldır", () => AppHost.Widgets.RemoveWithUndo(Config.Id)));
    }

    private void Update(Action change)
    {
        change();
        ApplyStyle();
        AppHost.SaveSettings();
        ResolveOverlapAfterLayout(); // ölçek büyüdüyse komşusunun üstüne binmesin
    }

    /// <summary>Kaydedilmiş fiziksel konum hâlâ bağlı bir monitörün çalışma alanında mı?</summary>
    private static bool IsOnSomeMonitor(int x, int y)
    {
        var probe = new NativeMethods.RECT { Left = x + 20, Top = y + 20, Right = x + 120, Bottom = y + 80 };
        return NativeMethods.MonitorFromRect(ref probe, NativeMethods.MONITOR_DEFAULTTONULL) != IntPtr.Zero;
    }

    private bool _placedByPixels;

    private void PlaceOnScreen()
    {
        if (View.Resizable)
        {
            Width = double.IsNaN(Config.Width) ? DefaultWidth : Math.Max(Config.Width, MinResizableWidth);
            if (View.Collapsible && Config.Collapsed) FitCollapsedHeight();
            else Height = double.IsNaN(Config.Height) ? DefaultHeight : Math.Max(Config.Height, MinResizableHeight);
        }
        // Otomatik katlanan bölme, açılışta fare üstünde değilse katlı başlar.
        if (Config.AutoRollup && View.Collapsible) _rollupTimer.Start();

        if (!_placedByPixels)
        {
            // Eski sürümün DIP konumu (tek monitör/aynı DPI için güvenilir); ekranda değilse ya da yoksa boş bir yer.
            var placed = false;
            if (!double.IsNaN(Config.Left) && !double.IsNaN(Config.Top))
            {
                Left = Config.Left;
                Top = Config.Top;
                placed = IsOnScreen();
            }
            if (!placed) MoveToFreeSpot();
        }
        _positionReady = true;
        SaveBounds();
    }

    /// <summary>Pencere bağlı bir monitörde görünüyor mu? (Fiziksel piksel; ölçeği farklı monitörlerde de doğru.)</summary>
    public bool IsOnScreen() =>
        Handle != IntPtr.Zero && NativeMethods.GetWindowRect(Handle, out var r) && IsOnSomeMonitor(r.Left, r.Top);

    /// <summary>
    /// Widget'ı kullanıcının çalıştığı monitörde (imlecin olduğu; ör. "ekle"ye basılan ana pencere) boş bir yere taşır.
    /// Hesap fiziksel pikselle yapılır: ölçeği farklı monitörlerde de doğru yere oturur.
    /// </summary>
    public void MoveToFreeSpot()
    {
        if (Handle == IntPtr.Zero) return;
        UpdateLayout();
        var p = AppHost.Widgets.FreeSpot(Config.Kind, new Size(ActualWidth, ActualHeight), this);
        NativeMethods.SetWindowPos(Handle, IntPtr.Zero, p.X, p.Y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        QueueSave();
    }

    /// <summary>
    /// Monitör düzeni değişip (monitör takıldı, uykudan uyandı, çözünürlük değişti) kayıtlı konum yeniden
    /// görünür hale geldiyse widget'ı oraya geri koyar.
    /// </summary>
    public void RestoreSavedPosition()
    {
        if (Handle == IntPtr.Zero || _dragging || _resizing || _revealing) return;
        if (Config.PixelLeft is not int px || Config.PixelTop is not int py || !IsOnSomeMonitor(px, py)) return;
        if (!NativeMethods.GetWindowRect(Handle, out var r) || (r.Left == px && r.Top == py)) return;
        NativeMethods.SetWindowPos(Handle, IntPtr.Zero, px, py, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>Gölge payı (kartın çevresindeki saydam kenar), fiziksel piksel.</summary>
    private int MarginPixels => (int)Math.Round(ShadowMargin * VisualTreeHelper.GetDpi(this).DpiScaleX);

    /// <summary>
    /// Kartın fiziksel dikdörtgeni; görünmüyorsa boş. Katlı (ya da fare çekilince katlanmış) bölme açıldığındaki
    /// boyutuyla sayılır: altına dizilen widget, bölme açılınca onun altında kalmasın.
    /// </summary>
    internal Box? CardBox
    {
        get
        {
            if (PixelBounds is not { } r) return null;
            var m = MarginPixels;
            var bottom = r.Bottom - m;
            if (IsCollapsedNow && View.Resizable)
            {
                var expanded = (int)Math.Round((double.IsNaN(Config.Height) ? DefaultHeight : Config.Height) * VisualTreeHelper.GetDpi(this).DpiScaleY);
                bottom = Math.Max(bottom, r.Top + expanded - m);
            }
            return new Box(r.Left + m, r.Top + m, r.Right - m, bottom);
        }
    }

    /// <summary>Kaldırmadan önce bekleyen konum ve içerik değişikliklerini ayarlara yazar.</summary>
    public void FlushState()
    {
        _saveTimer.Stop();
        SaveBounds();
        View.Flush();
    }

    /// <summary>Bırakılan widget başka birinin üstündeyse en yakın boş yere (komşusunun yanına) kaydırır.</summary>
    private void ResolveOverlap()
    {
        if (!AppHost.Settings.PreventOverlap || Config.Locked || CardBox is not { } card) return;
        var work = NativeMethods.WorkAreaAt(new NativeMethods.POINT { X = (card.Left + card.Right) / 2, Y = (card.Top + card.Bottom) / 2 });
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var moved = WidgetLayout.Separate(card, AppHost.Widgets.OtherCards(this),
            new Box(work.Left, work.Top, work.Right, work.Bottom), GapPixels(scale));
        if (moved is not { } target) return;
        var m = MarginPixels;
        NativeMethods.SetWindowPos(Handle, IntPtr.Zero, target.Left - m, target.Top - m, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        QueueSave();
    }

    /// <summary>
    /// Kartlar arası aralık: gölge payından (14) büyük, yoksa öndeki widget'ın gölgesi komşunun kenarındaki
    /// tıklamaları yakalar.
    /// </summary>
    private static int GapPixels(double scale) => (int)Math.Round(18 * scale);

    /// <summary>Boyutu içeriğe göre değişen widget (saat, tarih) büyüdükten sonra komşusunun üstündeyse kaydırılır.</summary>
    private void ResolveOverlapAfterLayout() =>
        Dispatcher.BeginInvoke(ResolveOverlap, DispatcherPriority.Loaded);

    /// <summary>Pencerenin fiziksel dikdörtgeni (görünmüyorsa boş).</summary>
    internal NativeMethods.RECT? PixelBounds =>
        IsVisible && Handle != IntPtr.Zero && NativeMethods.GetWindowRect(Handle, out var r) ? r : null;

    private void QueueSave()
    {
        if (!_positionReady) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveBounds()
    {
        if (!_positionReady) return;
        Config.Left = Left;
        Config.Top = Top;
        if (Handle != IntPtr.Zero && NativeMethods.GetWindowRect(Handle, out var r))
        {
            Config.PixelLeft = r.Left;
            Config.PixelTop = r.Top;
        }
        if (View.Resizable)
        {
            // Width/Height ayarlandıktan hemen sonra ActualWidth henüz güncellenmemiş olabilir.
            Config.Width = double.IsNaN(Width) ? ActualWidth : Width;
            if (!IsCollapsedNow) Config.Height = double.IsNaN(Height) ? ActualHeight : Height;
        }
        AppHost.SaveSettings();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        // WS_EX_NOACTIVATE: widget kendiliğinden etkinleşip odağı çalmaz. WPF, pencere ölçeği farklı bir monitöre
        // geçince (açılışta kayıtlı konuma yerleşirken, sürüklerken) SWP_NOACTIVATE olmadan SetWindowPos çağırır;
        // bu stil olmadan her widget sırayla etkinleşir ve kullanıcının o an yazdığı pencere odağı kaybeder.
        // Tıklamayla etkinleşme WM_MOUSEACTIVATE'te ayrıca serbest bırakılır (not yazmak, aramak için).
        const long WS_EX_NOACTIVATE = 0x08000000;
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex = (ex | NativeMethods.WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));

        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
        // Widget'lar küçük ve durağandır: yazılımla çizmek her katmanlı pencere için ayrı ekran kartı yüzeyi tutmaktan
        // kaçınır (ölçüldü: 7 widget'ta ~85 MB daha az bellek, işlemci aynı). NESTDESK_GPU=1 (ya da DUZENLEME_GPU=1) donanım çizimine döner.
        if (source?.CompositionTarget is { } target && Core.AppEnvironment.Get("GPU") != "1")
            target.RenderMode = RenderMode.SoftwareOnly;
        if (Application.Current?.MainWindow == this) Application.Current.MainWindow = null;
        AttachToDesktop();

        // Kaydedilmiş fiziksel konuma, pencere görünmeden taşı (DPI farklı monitörde WPF boyutu kendisi ölçekler).
        if (Config.PixelLeft is int px && Config.PixelTop is int py && IsOnSomeMonitor(px, py))
        {
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, px, py, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            _placedByPixels = true;
        }
    }

    /// <summary>
    /// Masaüstü simge katmanının sahibine bağlanır: Windows 11 24H2+'da Progman, daha eskilerde (duvar kağıdı slayt
    /// gösterisi, Wallpaper Engine vb. varken) simgeleri taşıyan WorkerW. Böylece widget duvar kağıdının arkasında
    /// kalmaz ve Win+D ile kaybolmaz.
    /// </summary>
    public void AttachToDesktop()
    {
        var hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;
        var owner = Desktop.DesktopIcons.DesktopOwner();
        if (owner == IntPtr.Zero) return;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWLP_HWNDPARENT, owner);
        if (!_revealing) SendToBack(hwnd);
    }

    /// <summary>Explorer yeniden başladıysa ya da masaüstü düzeni değiştiyse yeniden bağlanır.</summary>
    public void EnsureAttached(IntPtr wanted)
    {
        var hwnd = Handle;
        if (hwnd == IntPtr.Zero || _revealing) return; // öne getirme sırasında bilerek sahipsiz
        var current = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER);
        if (wanted != IntPtr.Zero && (current != wanted || !NativeMethods.IsWindow(current))) AttachToDesktop();
    }

    /// <summary>Masaüstü katmanında en alta iner (öne getirme sürüyorsa beklenir).</summary>
    public void SendToBottom()
    {
        if (!_revealing && Handle != IntPtr.Zero) SendToBack(Handle);
    }

    /// <summary>Kullanıcı düzeni için fiziksel konuma taşır ve kaydeder.</summary>
    public void MoveTo(int x, int y)
    {
        if (Handle == IntPtr.Zero) return;
        NativeMethods.SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        QueueSave();
    }

    private static void SendToBack(IntPtr hwnd) =>
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

    private IntPtr Handle => new WindowInteropHelper(this).Handle;

    // --- Sürükleme ---
    // DragMove kullanılmaz: sistemin taşıma döngüsü Windows Snap'i tetikler (widget ekranın yarısına/çeyreğine yapışıp
    // büyür) ve farklı DPI'lı monitörlerde konumu şaşırır. Bunun yerine imleç farkı fiziksel pikselle uygulanır.
    private bool _dragging;
    private NativeMethods.POINT _grabOffset; // imlecin pencere sol üstüne uzaklığı (fiziksel piksel)
    private double _grabDpi;

    private void BeginDrag(MouseButtonEventArgs e)
    {
        if (Config.Locked || e.ClickCount > 1) return;
        NativeMethods.GetCursorPos(out var cursor);
        NativeMethods.GetWindowRect(Handle, out var rect);
        _grabOffset = new NativeMethods.POINT { X = cursor.X - rect.Left, Y = cursor.Y - rect.Top };
        _grabDpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        _dragging = _card.CaptureMouse();
        e.Handled = _dragging;
    }

    private void ContinueDrag()
    {
        if (!_dragging) return;
        NativeMethods.GetCursorPos(out var p);
        NativeMethods.GetWindowRect(Handle, out var current);
        // Ölçeği farklı bir monitöre geçince pencere yeniden boyutlanır; tutma noktası da aynı oranda ölçeklenir,
        // böylece imleç kartın aynı yerinde kalır.
        var k = VisualTreeHelper.GetDpi(this).DpiScaleX / _grabDpi;
        var x = p.X - (int)Math.Round(_grabOffset.X * k);
        var y = p.Y - (int)Math.Round(_grabOffset.Y * k);

        // Widget, imlecin bulunduğu monitörün dışına kaçıp tutulamaz hale gelmesin: başlık hep görünür kalır.
        var work = NativeMethods.WorkAreaAt(p);
        var w = current.Width;

        // Shift basılıyken 20 piksellik ızgaraya hizalanır (widget'ları düzgün sıralamak için).
        const int VK_SHIFT = 0x10;
        if (GetAsyncKeyState(VK_SHIFT) < 0)
        {
            var grid = (int)Math.Round(20 * VisualTreeHelper.GetDpi(this).DpiScaleX);
            x = work.Left + (int)Math.Round((x - work.Left) / (double)grid) * grid;
            y = work.Top + (int)Math.Round((y - work.Top) / (double)grid) * grid;
        }
        // Mıknatıs: komşunun altına/üstüne/yanına ve ekran kenarına aralıklı yapışır (Alt basılıyken kapalı).
        const int VK_MENU = 0x12;
        if (AppHost.Settings.SnapWidgets && GetAsyncKeyState(VK_SHIFT) >= 0 && GetAsyncKeyState(VK_MENU) >= 0)
        {
            var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var m = MarginPixels;
            var card = new Box(x + m, y + m, x + current.Width - m, y + current.Height - m);
            var (sx, sy) = WidgetLayout.Snap(card, AppHost.Widgets.OtherCards(this),
                new Box(work.Left, work.Top, work.Right, work.Bottom), (int)Math.Round(20 * scale), GapPixels(scale));
            x += sx;
            y += sy;
        }
        const int keep = 80;
        x = Math.Clamp(x, work.Left - w + keep, Math.Max(work.Left - w + keep, work.Right - keep));
        y = Math.Clamp(y, work.Top - 10, Math.Max(work.Top - 10, work.Bottom - keep));

        if (x != current.Left || y != current.Top) _moved = true;
        NativeMethods.SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private bool _moved;

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        _card.ReleaseMouseCapture();
        // Yalnızca gerçekten taşındıysa: düz tıklama (ör. başlığa çift tıklama) widget'ı yerinden oynatmasın.
        if (_moved) ResolveOverlap();
        _moved = false;
        QueueSave();
    }

    // --- Kenarlardan boyutlandırma ---
    // Bölme/kutu/not dört kenar ve köşelerden; saat/tarih sağ-alt kenardan (ölçek) büyütülüp küçültülür.
    [Flags]
    private enum Grip { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8 }

    private const double GripBand = 9;
    private Grip _grip;
    private bool _resizing;
    private NativeMethods.POINT _resizeCursor;
    private NativeMethods.RECT _resizeStart;
    private double _resizeScale;

    private Grip GripAt(Point p)
    {
        if (Config.Locked || _card.ActualWidth <= 0) return Grip.None;
        double w = _card.ActualWidth, h = _card.ActualHeight;
        var grip = Grip.None;
        if (View.Resizable)
        {
            if (p.X <= GripBand) grip |= Grip.Left;
            else if (p.X >= w - GripBand) grip |= Grip.Right;
            if (!IsCollapsedNow)
            {
                if (p.Y <= GripBand) grip |= Grip.Top;
                else if (p.Y >= h - GripBand) grip |= Grip.Bottom;
            }
        }
        else
        {
            if (p.X >= w - GripBand) grip |= Grip.Right;
            if (p.Y >= h - GripBand) grip |= Grip.Bottom;
        }
        return grip;
    }

    private static Cursor? CursorFor(Grip grip) => grip switch
    {
        Grip.Left or Grip.Right => Cursors.SizeWE,
        Grip.Top or Grip.Bottom => Cursors.SizeNS,
        Grip.Left | Grip.Top or Grip.Right | Grip.Bottom => Cursors.SizeNWSE,
        Grip.Right | Grip.Top or Grip.Left | Grip.Bottom => Cursors.SizeNESW,
        _ => null,
    };

    private bool BeginResize(MouseButtonEventArgs e)
    {
        var grip = GripAt(e.GetPosition(_card));
        if (grip == Grip.None || e.ClickCount > 1) return false;
        _grip = grip;
        NativeMethods.GetCursorPos(out _resizeCursor);
        NativeMethods.GetWindowRect(Handle, out _resizeStart);
        _resizeScale = Config.Scale;
        _resizing = _card.CaptureMouse();
        e.Handled = _resizing;
        return _resizing;
    }

    private void ContinueResize()
    {
        NativeMethods.GetCursorPos(out var p);
        int dx = p.X - _resizeCursor.X, dy = p.Y - _resizeCursor.Y;
        var s = _resizeStart;

        if (!View.Resizable)
        {
            // Saat/tarih içeriğe göre boyutlanır: kenar çekildikçe ölçek değişir.
            var ratio = _grip.HasFlag(Grip.Right) ? (s.Width + dx) / (double)s.Width : (s.Height + dy) / (double)s.Height;
            var scale = Math.Round(Math.Clamp(_resizeScale * ratio, MinScale, MaxScale), 2);
            if (Math.Abs(scale - Config.Scale) >= 0.01)
            {
                Config.Scale = scale;
                ApplyStyle();
            }
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var minW = (int)Math.Ceiling(MinWidth * dpi.DpiScaleX);
        var minH = (int)Math.Ceiling(MinHeight * dpi.DpiScaleY);
        int left = s.Left, top = s.Top, right = s.Right, bottom = s.Bottom;
        if (_grip.HasFlag(Grip.Left)) left = Math.Min(s.Left + dx, right - minW);
        if (_grip.HasFlag(Grip.Right)) right = Math.Max(s.Right + dx, left + minW);
        if (_grip.HasFlag(Grip.Top)) top = Math.Min(s.Top + dy, bottom - minH);
        if (_grip.HasFlag(Grip.Bottom)) bottom = Math.Max(s.Bottom + dy, top + minH);
        if (AppHost.Settings.PreventOverlap)
        {
            // Çekilen kenar komşu widget'ın kenarında durur (içine girmez).
            var m = MarginPixels;
            var gap = GapPixels(dpi.DpiScaleX);
            var card = WidgetLayout.ClampResize(
                new Box(s.Left + m, s.Top + m, s.Right - m, s.Bottom - m),
                new Box(left + m, top + m, right - m, bottom - m),
                _grip.HasFlag(Grip.Left), _grip.HasFlag(Grip.Top), _grip.HasFlag(Grip.Right), _grip.HasFlag(Grip.Bottom),
                AppHost.Widgets.OtherCards(this), gap);
            (left, top, right, bottom) = (card.Left - m, card.Top - m, card.Right + m, card.Bottom + m);
        }
        // Tek çağrıda taşı + boyutlandır: sol/üst kenardan çekerken karşı kenar titremez.
        NativeMethods.SetWindowPos(Handle, IntPtr.Zero, left, top, right - left, bottom - top,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private void EndResize()
    {
        if (!_resizing) return;
        _resizing = false;
        _card.ReleaseMouseCapture();
        _card.Cursor = null;
        if (View.Resizable && NativeMethods.GetWindowRect(Handle, out var r))
        {
            // WPF'in Width/Height değerleri yeni boyutla eşitlensin (kaydedilen de bu).
            var dpi = VisualTreeHelper.GetDpi(this);
            Width = r.Width / dpi.DpiScaleX;
            if (IsCollapsedNow) FitCollapsedHeight();
            else Height = r.Height / dpi.DpiScaleY;
        }
        else
        {
            AppHost.SaveSettings();
            ResolveOverlapAfterLayout();
        }
        QueueSave();
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    /// <summary>Ctrl + tekerlek: bölme/kutuda simge boyutu, diğerlerinde widget ölçeği.</summary>
    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        // Widget etkin pencere değilken Keyboard.Modifiers güncel olmayabilir; tuşun o anki durumuna bakılır.
        const int VK_CONTROL = 0x11;
        if (GetAsyncKeyState(VK_CONTROL) >= 0) return;
        e.Handled = true;
        if (View.OnCtrlWheel(e.Delta)) return;
        var scale = Math.Round(Math.Clamp(Config.Scale + (e.Delta > 0 ? 0.1 : -0.1), MinScale, MaxScale), 2);
        if (Math.Abs(scale - Config.Scale) < 0.01) return;
        Update(() => Config.Scale = scale);
    }

    // --- Öne getirme ---
    // Widget'lar masaüstü katmanında, pencerelerin arkasında durur. Yeni eklenen ya da "Bul" denen widget
    // birkaç saniye vurgulu olarak en öne gelir; kullanıcı nereye eklendiğini görür.
    private bool _revealing;
    private DispatcherTimer? _revealTimer;

    public void Reveal(TimeSpan duration)
    {
        if (!IsVisible) return;
        _revealing = true;
        // Windows, sahibi masaüstü olan bir pencerenin "en üstte" olma isteğini sessizce yok sayar:
        // öne getirme süresince masaüstü bağı kaldırılır, bitince yeniden bağlanır.
        NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GWLP_HWNDPARENT, IntPtr.Zero);
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        DebugLog.Write($"[{Config.Kind}] Reveal exstyle=0x{NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GWL_EXSTYLE).ToInt64():X}");
        var palette = WidgetPalette.For(Config.Style, Config.Accent);
        _card.BorderBrush = palette.Accent;
        _card.BorderThickness = new Thickness(3);

        _revealTimer?.Stop();
        Deactivated -= DemoteWhenDeactivated;
        _revealTimer = new DispatcherTimer { Interval = duration };
        _revealTimer.Tick += (_, _) => EndReveal();
        _revealTimer.Start();
    }

    private void EndReveal()
    {
        _revealTimer?.Stop();
        if (!_revealing) return;
        if (IsActive)
        {
            // Kullanıcı bu widget'ta çalışıyor (ör. yeni nota yazıyor): arkaya gömme, başka pencereye geçince bırak.
            Deactivated -= DemoteWhenDeactivated;
            Deactivated += DemoteWhenDeactivated;
            return;
        }
        _revealing = false;
        _card.BorderThickness = new Thickness(1);
        ApplyStyle();
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        AttachToDesktop(); // masaüstüne yeniden bağlan ve en alta dön
        AppHost.Widgets.BringToFront(this); // ama diğer widget'ların önünde kal
    }

    private void DemoteWhenDeactivated(object? sender, EventArgs e)
    {
        Deactivated -= DemoteWhenDeactivated;
        // Etkinleştirme işlenirken sahiplik/z-sırası değiştirilmez; hemen ardından yapılır.
        Dispatcher.BeginInvoke(EndReveal, DispatcherPriority.Background);
    }

    /// <summary>Klavye girişi için widget'ı bilerek etkinleştirir (yeni not, arama kutusu).</summary>
    public void ActivateForInput() => Activate();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_MOUSEACTIVATE = 0x0021;
        if (DebugLog.Enabled && msg is 0x02E0 or 0x0021 or 0x0006 or 0x007B or 0x0204 or 0x0205 or 0x0231 or 0x0232)
            DebugLog.Write($"  hwnd={hwnd} msg=0x{msg:X4} w=0x{wParam.ToInt64():X} l=0x{lParam.ToInt64():X}");
        if (msg == WM_MOUSEACTIVATE)
        {
            // WS_EX_NOACTIVATE tıklamayla etkinleşmeyi de kapatır; tıklayınca etkinleşsin (not yazmak, aramak için).
            // Tıklanan widget diğer widget'ların önüne geçer (etkinleşince en alta itildiği için hemen ardından).
            const int MA_ACTIVATE = 1;
            Dispatcher.BeginInvoke(() => AppHost.Widgets.BringToFront(this), DispatcherPriority.Input);
            handled = true;
            return new IntPtr(MA_ACTIVATE);
        }
        if (msg == NativeMethods.WM_WINDOWPOSCHANGING && !_revealing)
        {
            // Pencere öne alınmaya çalışılsa bile en altta kalsın (öne getirme sırasında hariç).
            var pos = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(lParam);
            if (DebugLog.Enabled && (pos.flags & 0x0003) != 0x0003)
                DebugLog.Write($"  hwnd={hwnd} POSCHANGING x={pos.x} y={pos.y} cx={pos.cx} cy={pos.cy} flags=0x{pos.flags:X} after={pos.hwndInsertAfter}");
            // Sahipsiz bir widget'ı (ör. Explorer yeniden başlarken) en alta itmek onu tam ekran masaüstünün arkasına gömer.
            if ((pos.flags & NativeMethods.SWP_NOZORDER) == 0 && NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
            {
                pos.hwndInsertAfter = NativeMethods.HWND_BOTTOM;
                Marshal.StructureToPtr(pos, lParam, false);
            }
        }
        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        _saveTimer.Stop();
        _revealTimer?.Stop();
        _rollupTimer.Stop();
        Deactivated -= DemoteWhenDeactivated;
        View.Detach();
        base.OnClosed(e);
    }
}
