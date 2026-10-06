using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>
/// Kısayolla imlecin yanında açılan halka (Kando'nun imzası). Masaüstündeki halka widget'ının (<see cref="RingTargets"/>)
/// öğelerini aynı çizimle (<see cref="RingView"/>) gösterir.
/// <para>Pencere: düz, kenarlıksız, saydam zeminli, en üstte, görev çubuğunda ve Alt+Tab'da görünmez (WS_EX_TOOLWINDOW);
/// masaüstüne sahipli değildir ve etkinleşir (klavye 1–9, oklar, Esc). Boyutu 2 × (çember + nişan alanı); nişan alanının zemini
/// neredeyse saydamdır (#01000000): "tuşu bırakınca seç" ve basıp sürükleme orada da yön verir; görünen halkanın dışına
/// (nişan alanına) tıklamak halkayı kapatır. Gölge degrade ile çizilir (Effect yok); renkler hedef widget'ın paletinden. Alt
/// bırakılınca menü kipine girilmez (SC_KEYMENU yutulur).</para>
/// <para>Hız: kısayol açıksa pencere açılıştan sonra boşta önceden kurulur (tutamaç, halka, simgeler); kapatınca yalnızca
/// gizlenir. Ayar, kutu taşıması, ad değişikliği ya da Windows'un görünüşü (<see cref="SystemLookChanged"/>) içeriği eskitir;
/// yeniden kurulum kısayol yolunda değil boşta yapılır.
/// Açılış: önceki pencere ve imleç okunur (yalnızca bellekte), pencere gizliyken imlecin monitörüne (önce taşınıp ölçeği
/// düzeltilerek; WPF gizli pencerede ölçeğe geçmezse pencere o monitörde yeniden kurulur) <see cref="RingPlacement.ForPopup"/>
/// ile yerleştirilir, gösterilir ve öne getirilir; hareket <see cref="Motion.RingOpen"/>. Girdi hareketi beklemez (isabet
/// testi açıyla).</para>
/// <para>Kapanma: kısayol yeniden, Esc, sağ tık, nişan alanına tıklama ya da kökte ortaya basmak kapatır ve önceki pencereyi
/// öne getirir; öğe seçmek kapatır (öğe kendi penceresini açar); başka pencereye tıklamak (etkinlik kaybı) yalnızca kapatır.
/// Halka öne gelemediyse (ör. kısayol iletisi öne geçme izni getirmedi) öndeki pencere kısa aralıkla izlenir: başka bir pencere
/// öne gelince ya da 10 sn içinde halkaya dokunulmazsa (imleç üstünde değilse) kapanır. Tuş ve metin öğeleri halka gerçekten
/// kapandıktan sonra (<see cref="Motion.RingClose"/> geri çağırması) önceki pencereye gönderilir (<see cref="RingRunner.SendToTargetAsync"/>).</para>
/// <para>"Tuşu bırakınca seç" (<see cref="AppSettings.RingHoldSelectOff"/> kapalıyken): kısayolun tuşları açılışta hâlâ basılıysa
/// halka kurulur; vurgu imlecin açılıştan bu yana gittiği yöndendir (halka ekran kenarında kaydırılmış olabilir); bırakılınca
/// imleç en az <see cref="RingGeometry.DragThresholdDip"/> kadar gittiyse ve bir öğe vurguluysa o seçilir, yoksa halka tıklama
/// kipinde kalır. Bırakma WPF'in KeyUp'ından anlaşılır; yalnızca halka öne gelemediyse ve halka açıkken, en çok 10 sn ve
/// yalnızca kısayolun tuşları GetAsyncKeyState ile yoklanır (kanca yok). Kısayolun tuşları hâlâ basılıyken Ctrl+Shift ile
/// seçmek "yönetici olarak" sayılmaz (kısayolun kendisi Ctrl+Shift içerebilir).</para>
/// </summary>
internal sealed class RingPopup : Window
{
    /// <summary>
    /// Çemberin dışındaki nişan alanı (DIP): "tuşu bırakınca seç" ve basıp sürükleme orada da yön verir; oraya tıklamak
    /// halkayı kapatır (görünen halkanın dışı).
    /// </summary>
    public const double AimDip = 72;

    /// <summary>
    /// Halka öne gelemediyse: "tuşu bırakınca seç" için kısayolun tuşlarının yoklanma sınırı ve dokunulmayan halkanın en çok
    /// açık kalma süresi.
    /// </summary>
    private static readonly TimeSpan InactiveLimit = TimeSpan.FromSeconds(10);

    private static RingPopup? _instance;
    private static bool _syncQueued, _exiting;

    private readonly Canvas _root;
    private readonly RingBackdrop _backdrop = new();
    private readonly RingView _ring = new() { IsPopup = true, StableSize = true };
    // Halkanın altında hep yerinde: "Halkayı düzenle" (Ring.Edit); üstünde gerektiğinde soluk öğelerin açıklaması.
    private readonly Button _edit;
    private readonly TextBlock _footer;
    private RingOrigin _origin = RingOrigin.Hotkey;

    private WidgetConfig? _config;
    private ulong? _signature;
    private bool _dirty = true, _rebuildQueued, _forReal;

    // Kurulduğu andaki Windows görünüşü (açık/koyu, yüksek karşıtlık ve renkleri): App.ApplyTheme değişikliği bildirir
    // (SystemLookChanged), boşta yeniden kurulur; Open yalnızca bildirim boşa düşmeden basıldıysa kurar.
    private WidgetPalette.SystemLook _builtLook;

    // Açıkken; yalnızca bellekte (hiçbir yere yazılmaz, günlüğe girmez). Kapanınca bırakılır.
    private IntPtr _previous;
    private NativeMethods.POINT _anchor, _openCursor;
    private double _scale = 1;
    private bool _closing, _armed, _hotkeyHeld, _rootPressed;
    private Action? _afterClose;
    private List<int> _hotkeyKeys = [];
    private DispatcherTimer? _watch;
    private long _watchUntil;

    // ---------------------------------------------------------------- dışarıya açık (arayüz iş parçacığında)

    /// <summary>NESTDESK_TEST_RING=1 (yalnızca test masaüstünde): kısayol kaydetmeden halka yolu açık.</summary>
    public static bool TestRing => RingHotkey.TestOverride(AppHost.IsTestDesktop, AppEnvironment.Get("TEST_RING"));

    /// <summary>Halka kısayolu açık mı: kısayol atanmış ya da test. Yalnızca kısayolu yönetir; çift tıklama ve tepsi her zaman açar.</summary>
    public static bool Enabled => RingShortcut.IsOn || TestRing;

    /// <summary>Halka boşta önceden kurulsun mu: kısayol ya da çift tıklama onu açabiliyor.</summary>
    private static bool Prebuild => Enabled || DesktopState.ResolveDoubleClick(AppHost.Settings.DoubleClickAction,
        AppHost.Settings.DoubleClickHidesDesktop, AppHost.Settings.FencesReplaceIcons) == DoubleClickEffect.Ring;

    /// <summary>
    /// Ayara uyar (açılışta StartDeferredWork'ten sonra ve kısayol değişince): yol açıksa pencere boşta önceden kurulur,
    /// kapalıysa bütünüyle kapanır (bellek geri verilir). Birden çok çağrı tek işe iner.
    /// </summary>
    public static void Sync()
    {
        if (_syncQueued || _exiting || Application.Current is not { } app) return;
        _syncQueued = true;
        app.Dispatcher.BeginInvoke(() =>
        {
            _syncQueued = false;
            if (_exiting) return;
            if (Prebuild)
            {
                _instance ??= new RingPopup();
                _instance.RebuildIfDirty();
            }
            else if (_instance is { IsVisible: false } popup)
            {
                _instance = null;
                popup.CloseForRealCore();
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Halka kısayolu (WM_HOTKEY id 8): kapalıysa açar, açıksa kapatır ve önceki pencereyi öne getirir.</summary>
    public static void Toggle()
    {
        if (_exiting || !Enabled) return;
        OpenOrClose(null, RingOrigin.Hotkey);
    }

    /// <summary>
    /// Halkayı kısayolsuz açar (boş masaüstüne çift tıklama, tepsi): <paramref name="point"/> (fiziksel piksel) noktasında,
    /// tıklama kipinde ("tuşu bırakınca seç" yok); tuş ve metin öğeleri soluk çizilir ve çalışmaz (önceki pencere yazılacak
    /// yer değildir). Açıksa kapatır. Pencere ilk gerekince kurulur.
    /// </summary>
    public static void OpenAt(NativeMethods.POINT point, RingOrigin origin)
    {
        if (_exiting) return;
        OpenOrClose(point, origin);
    }

    /// <summary>Tepsi menüsündeki "Halkayı aç": imlecin (tepsinin) ekranının çalışma alanının ortasında.</summary>
    public static void OpenFromTray()
    {
        NativeMethods.GetCursorPos(out var cursor);
        var work = NativeMethods.WorkAreaAt(cursor);
        OpenAt(new NativeMethods.POINT { X = (work.Left + work.Right) / 2, Y = (work.Top + work.Bottom) / 2 }, RingOrigin.Tray);
    }

    private static void OpenOrClose(NativeMethods.POINT? point, RingOrigin origin)
    {
        var at = PerfLog.Now();
        if (_instance is { IsVisible: true, _closing: false } open)
        {
            open.Dismiss();
            return;
        }
        (_instance ??= new RingPopup()).Open(at, origin, point);
    }

    /// <summary>
    /// Windows'un görünüşü değişti (App.ApplyTheme: açık/koyu kip, yüksek karşıtlık açıldı/kapandı ya da bir yüksek karşıtlık
    /// temasından ötekine geçildi): önceden kurulmuş halka eskidi, boşta yeniden kurulur (kısayol yolunda kurulum olmasın;
    /// açıksa kapanınca). Halka ve zemini Windows'un renklerini dondurulmuş fırçalarda tutar.
    /// </summary>
    public static void SystemLookChanged()
    {
        if (_exiting || _instance is not { _forReal: false } popup) return;
        popup.MarkDirty();
    }

    /// <summary>Uygulama kapanıyor: pencere gerçekten kapanır.</summary>
    public static void CloseForReal()
    {
        _exiting = true;
        var popup = _instance;
        _instance = null;
        popup?.CloseForRealCore();
    }

    // ---------------------------------------------------------------- pencere

    private RingPopup()
    {
        // WPF ilk açılan pencereyi Application.MainWindow yapar; tema değişikliği (WPF-UI) bu pencereye uygulanmasın.
        if (Application.Current?.MainWindow == this) Application.Current.MainWindow = null;
        Title = L.T("Halka menü");
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = true;
        Topmost = true;
        SizeToContent = SizeToContent.Manual;
        UseLayoutRounding = true;
        AutomationProperties.SetAutomationId(this, "Ring.Popup");
        AutomationProperties.SetName(this, L.T("Halka menü"));

        // Nişan alanı: neredeyse saydam zemin (tamamen saydam katmanlı pencere pikseli fareyi alttaki pencereye geçirir).
        _root = new Canvas { Background = new SolidColorBrush(Color.FromArgb(0x01, 0, 0, 0)), ClipToBounds = true };
        _root.Children.Add(_backdrop);
        _root.Children.Add(_ring);
        _footer = new TextBlock
        {
            Text = L.T("Bu öğe yalnızca tuşla açılan halkada çalışır"), FontSize = 12, TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, IsHitTestVisible = false,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), Padding = new Thickness(8, 2, 8, 3),
        };
        AutomationProperties.SetAutomationId(_footer, "Ring.Footer");
        _edit = EditButton();
        _edit.Click += (_, _) => EditRing();
        _root.Children.Add(_footer);
        _root.Children.Add(_edit);
        KeyboardNavigation.SetTabNavigation(_root, KeyboardNavigationMode.Cycle);
        Content = _root;

        // Pencere en baştan imlecin (ya da NESTDESK_RING_AT'in) ekranında oluşur: ölçeği doğru başlasın.
        WindowFit.StartOn(this, AnchorNow());
        var side = 2 * AimDip;
        Width = Height = side;

        _ring.ItemInvoked += OnItemInvoked;
        _ring.BackRequested += Dismiss;
        _ring.CloseRequested += Dismiss;
        _ring.Navigated += () =>
        {
            UpdateFooter();
            Motion.RingSwap(_ring.CenterElement, _ring.ItemElements, _ring.TowardCenter);
        };
        _ring.Dims = slot => slot.Action is { } a && !RingAvailability.Available(a.Type, _origin);

        PreviewMouseMove += (_, _) => OnPointerMove();
        PreviewMouseDown += (_, e) =>
        {
            // Fareyle seçen kullanıcı tıklama kipindedir.
            Disarm();
            if (e.ChangedButton == MouseButton.Right) e.Handled = true;
        };
        // Diskin içindeki her tıklamayı halka (RingView) alır; buraya gelen tıklama görünen halkanın dışında, nişan alanındadır:
        // halka kapanır (görünmeyen bir alana tıklamak bir öğeyi, ör. bir komutu, çalıştırmasın).
        _root.MouseLeftButtonDown += (_, e) => { _rootPressed = true; e.Handled = true; };
        _root.MouseLeftButtonUp += (_, e) =>
        {
            if (!_rootPressed || e.Handled || _closing) return;
            _rootPressed = false;
            e.Handled = true;
            Dismiss();
        };
        // Sağ tık kapatır (bırakınca: bırakma alttaki pencerede menü açmasın).
        PreviewMouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            Dismiss();
        };
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += (_, _) =>
        {
            if ((!_armed && !_hotkeyHeld) || !AllReleased()) return;
            _hotkeyHeld = false;
            if (_armed) Released();
        };
        Activated += (_, _) =>
        {
            // Sonradan (ör. halkaya tıklayınca) öne geldi: klavye halkada, izlemeye ve yoklamaya gerek kalmaz.
            StopWatch();
            if (IsVisible && !_closing) FocusRing();
        };
        // Dışarı tıklandı (başka pencere öne geldi): yalnızca kapanır.
        Deactivated += (_, _) => { if (IsVisible && !_closing) CloseWith(restore: false, after: null); };
        // Açıkken ekranın ölçeği değişti: yeniden yerleşir (simgeler RingView'de yeniden istenir).
        DpiChanged += (_, _) => { if (IsVisible && !_closing) Dispatcher.BeginInvoke(() => Place(requireScale: false)); };

        AppHost.SettingsChanged += OnSettingsChanged;
        AppHost.Widgets.Changed += OnSettingsChanged;
        BoxMover.Changed += OnPathsChanged;
        AppHost.PathRenamed += OnPathRenamed;

        new WindowInteropHelper(this).EnsureHandle();
    }

    private IntPtr Handle => new WindowInteropHelper(this).Handle;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = Handle;
        // Görev çubuğunda ve Alt+Tab'da görünmez; etkinleşebilir (WS_EX_NOACTIVATE yok), masaüstüne sahipli değil.
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex = (ex | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    private const int WM_SYSCOMMAND = 0x0112, SC_KEYMENU = 0xF100;

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Kısayoldaki Alt bırakılınca Windows menü kipine girmesin (halkanın klavyesi kaybolurdu).
        if (msg == WM_SYSCOMMAND && (wParam.ToInt64() & 0xFFF0) == SC_KEYMENU) handled = true;
        return IntPtr.Zero;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_forReal) return;
        // Alt+F4 ya da sistem: yalnızca gizlenir (önceden kurulmuş kalır).
        e.Cancel = true;
        Dismiss();
    }

    // ---------------------------------------------------------------- içerik

    /// <summary>Hedef halka değişti ya da eskidiyse yeniden kurar (yalnızca gizliyken; açıkken kapanınca).</summary>
    private void RebuildIfDirty()
    {
        if (_forReal || IsVisible || !_dirty) return;
        var config = RingTargets.Pick(AppHost.Settings);
        if (config is null)
        {
            _dirty = false;
            _config = null;
            _signature = null;
            _ring.Release();
            return;
        }
        Build(config);
    }

    /// <summary>
    /// Halkayı kurar ve ısıtır: palet, yerler ve simgeler (simgeler arka planda gelir), ölçü ve ilk çizim (pencere gizliyken
    /// ölçülüp yerleştirilir: ilk açılışta ölçüm ve çizim kodu hazır).
    /// </summary>
    private void Build(WidgetConfig config)
    {
        _dirty = false;
        _config = config;
        _signature = Signature(config);
        _builtLook = LookNow();
        var palette = RingView.PaletteFor(config);
        // Önce renkler (eski halka yeniden kurulmaz), sonra halka bir kez: simgeler yeni renkle istenir.
        _ring.ApplyPalette(palette, rebuild: false);
        _ring.CenterShowsIcon = !config.Shows(WidgetNameLine.PartKey);
        _ring.SetRing(config, 0);
        _backdrop.Apply(palette, _ring.Metrics.OuterRadius, _ring.Style);
        PaintChrome(palette);
        var side = 2 * (_ring.Metrics.OuterRadius + AimDip);
        Arrange(side / 2, side / 2);
        _root.Measure(new Size(side, side));
        _root.Arrange(new Rect(0, 0, side, side));
    }

    /// <summary>Şu anki Windows görünüşü (yüksek karşıtlıkta Windows'un renkleri de; App.ApplyTheme'in baktığıyla aynı).</summary>
    private static WidgetPalette.SystemLook LookNow() =>
        WidgetPalette.SystemLook.Current(WidgetPalette.SystemDark, SystemParameters.HighContrast);

    /// <summary>Halka ve gölgesi, ortaları (<paramref name="x"/>, <paramref name="y"/>) DIP'te olacak biçimde.</summary>
    private void Arrange(double x, double y)
    {
        var outer = _ring.Metrics.OuterRadius;
        Canvas.SetLeft(_ring, x - outer);
        Canvas.SetTop(_ring, y - outer);
        Canvas.SetLeft(_backdrop, x - outer - RingBackdrop.ShadowDip);
        Canvas.SetTop(_backdrop, y - outer - RingBackdrop.ShadowDip);
        // Düğme çemberin altında, açıklama üstünde; ikisi de nişan alanının içinde (pencere monitörde kalır: görünürler).
        var width = 2 * (outer + AimDip) - 16;
        _edit.Measure(new Size(width, AimDip));
        Canvas.SetLeft(_edit, x - _edit.DesiredSize.Width / 2);
        Canvas.SetTop(_edit, y + outer + 10);
        _footer.MaxWidth = Math.Max(120, width);
        _footer.Measure(new Size(_footer.MaxWidth, AimDip));
        Canvas.SetLeft(_footer, x - _footer.DesiredSize.Width / 2);
        Canvas.SetTop(_footer, y - outer - 10 - _footer.DesiredSize.Height);
    }

    /// <summary>
    /// Halkanın görünüşünü belirleyen ayarların özeti: ilgisiz ayar değişiklikleri (konum, başka widget) yeniden kurdurmaz.
    /// Tema ve özel vurgu rengi de girer (2.2: "Otomatik"/"Klasik" Style/Accent'i değiştirmeyebilir; açılır halka eski temada
    /// kalıyordu), Windows'un açık/koyu kipi de (Otomatik ve Okyanus ona uyar). Yalnızca bellekte, karşılaştırma için. Her
    /// kayıtta arayüz iş parçacığında hesaplanır: serileştirme yok (<see cref="RingFingerprint"/>).
    /// </summary>
    internal static ulong Signature(WidgetConfig config) =>
        RingFingerprint.Of(config, SystemParameters.HighContrast, WidgetPalette.SystemDark);

    private void OnSettingsChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(OnSettingsChanged);
            return;
        }
        if (_forReal || _dirty) return;
        var config = RingTargets.Pick(AppHost.Settings);
        if (ReferenceEquals(config, _config) && config is not null && Signature(config) == _signature) return;
        MarkDirty();
    }

    private void OnPathsChanged() => MarkDirty();

    private void OnPathRenamed(string oldPath, string newPath, bool isDirectory) => MarkDirty();

    /// <summary>İçerik eskidi: gizliyse boşta yeniden kurulur, açıksa kapanınca.</summary>
    private void MarkDirty()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(MarkDirty);
            return;
        }
        _dirty = true;
        if (IsVisible || _rebuildQueued || _forReal) return;
        _rebuildQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _rebuildQueued = false;
            RebuildIfDirty();
        }, DispatcherPriority.ApplicationIdle);
    }

    // ---------------------------------------------------------------- açılış

    /// <summary>NESTDESK_RING_AT (test, fiziksel piksel) ya da imleç.</summary>
    private static NativeMethods.POINT AnchorNow()
    {
        if (NativeMethods.PointFromEnvironment("RING_AT") is { } at) return at;
        NativeMethods.GetCursorPos(out var cursor);
        return cursor;
    }

    /// <param name="origin">Halkayı ne açtı: kısayol dışında tıklama kipi ve tuş/metin öğeleri soluk.</param>
    /// <param name="point">Açılacağı nokta (fiziksel piksel); yoksa NESTDESK_RING_AT ya da imleç.</param>
    /// <param name="previousOverride">Pencere yeniden kurulduysa eskisinin okuduğu önceki pencere.</param>
    /// <param name="mayRecreate">Gizli pencere imlecin monitörünün ölçeğine geçmezse yeniden kurulabilir mi (bir kez).</param>
    private void Open(double pressedAt, RingOrigin origin, NativeMethods.POINT? point, IntPtr? previousOverride = null, bool mayRecreate = true)
    {
        // Önceki pencere ve imleç yalnızca şimdi okunur ve yalnızca bellekte tutulur. Kapanırken yeniden açıldıysa öndeki
        // pencere halkanın kendisidir: önceki hedef korunur.
        var foreground = ForegroundInfo.Window();
        var previous = previousOverride ?? (foreground == Handle ? _previous : foreground);
        NativeMethods.GetCursorPos(out _openCursor);
        _anchor = point ?? AnchorNow();
        _origin = origin;
        if (_closing)
        {
            // Kapanırken yeniden açıldı: kapanış (ve bekleyen işi) bırakılır.
            Motion.Stop(_root);
            _afterClose = null;
            HideNow();
        }
        _previous = previous;

        // Masaüstünde halka widget'ı yoksa NestDesk halkası (ilk kez gerekince kurulur ve kaydedilir).
        var config = RingTargets.Pick(AppHost.Settings);
        if (config is null)
        {
            config = RingTargets.EnsureHub(AppHost.Settings);
            AppHost.SaveSettings();
        }
        // Değişiklikler boşta işlenir (OnSettingsChanged); burada yalnızca hâlâ eskiyse (ör. boşa düşmeden basıldıysa).
        if (_dirty || !ReferenceEquals(config, _config) || _builtLook != LookNow()) Build(config);
        else if (_ring.InSubRing) _ring.SetRing(config, 0);
        if (_ring.Slots.Count == 0)
        {
            _previous = IntPtr.Zero;
            var empty = config;
            Notice.Show(L.T("Halka menün boş."), NoticeKind.Info, L.T("Halkayı düzenle"), () => RingEditor.Open(empty),
                L.T("Halkaya öğe eklemek için buraya tıkla."));
            return;
        }
        ApplyOrigin();

        if (!Place(requireScale: mayRecreate))
        {
            Recreate(pressedAt, origin, previous);
            return;
        }
        PerfLog.Mark("Ring.hotkey", atMs: pressedAt);
        if (PerfLog.Enabled) CompositionTarget.Rendering += OnFirstFrame;
        _ring.ClearHighlight();
        _rootPressed = false;
        Arm();
        Motion.RingOpen(_root, ShowAndActivate, _ring.CenterElement, _ring.ItemElements, _ring.TowardCenter);
    }

    /// <summary>
    /// WPF gizli pencerede imlecin monitörünün ölçeğine geçmedi (WM_DPICHANGED gelmedi): halka yanlış ölçekte çizilmesin diye
    /// pencere o monitörde yeniden kurulur (kurucu imlecin ekranında oluşturur) ve oradan açılır; eskisi kapanır.
    /// </summary>
    private void Recreate(double pressedAt, RingOrigin origin, IntPtr previous)
    {
        DebugLog.Write("RingPopup: gizli pencere ekranın ölçeğine geçmedi; yeniden kuruluyor");
        _previous = IntPtr.Zero;
        var fresh = new RingPopup();
        if (ReferenceEquals(_instance, this)) _instance = fresh;
        CloseForRealCore();
        fresh.Open(pressedAt, origin, _anchor, previous, mayRecreate: false);
    }

    private static void OnFirstFrame(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnFirstFrame;
        PerfLog.Mark("Ring.firstFrame");
    }

    /// <summary>
    /// Gizli pencereyi yerleştirir: ölçeği imlecin monitöründen farklıysa önce o monitöre taşınır (WPF yeni ölçeğe geçer), sonra
    /// pencerenin gerçek ölçeğiyle <see cref="RingPlacement.ForPopup"/>; halka pencerede o noktaya çizilir.
    /// </summary>
    /// <param name="requireScale">true: taşıdıktan sonra ölçek hâlâ farklıysa yerleştirmeden false döner (pencere yeniden
    /// kurulur); false: pencerenin ölçeğiyle yine de yerleşir.</param>
    private bool Place(bool requireScale)
    {
        var hwnd = Handle;
        if (hwnd == IntPtr.Zero) return true;
        var work = NativeMethods.WorkAreaAt(_anchor);
        var target = NativeMethods.ScaleAt(_anchor);
        if (!IsVisible && !ScaleIs(target))
        {
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, work.Left, work.Top, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            if (requireScale && !ScaleIs(target)) return false;
        }
        _scale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var monitor = NativeMethods.MonitorAreaAt(_anchor);
        var popup = RingPlacement.ForPopup(new Box(monitor.Left, monitor.Top, monitor.Right, monitor.Bottom),
            new Box(work.Left, work.Top, work.Right, work.Bottom), _anchor.X, _anchor.Y, _ring.Metrics.OuterRadius, AimDip, _scale);
        Width = Height = popup.SidePx / _scale;
        Arrange(popup.CenterXDip, popup.CenterYDip);
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, popup.Place.Left, popup.Place.Top, popup.SidePx, popup.SidePx,
            NativeMethods.SWP_NOACTIVATE);
        return true;
    }

    private bool ScaleIs(double scale) => Math.Abs(VisualTreeHelper.GetDpi(this).PixelsPerDip - scale) <= 0.001;

    /// <summary>
    /// Gösterir ve öne getirir: WM_HOTKEY'i alan süreç öne geçebilir; olmazsa SetForegroundWindow (iş parçacığı girdisi
    /// bağlanmaz). Yine olmazsa fare çalışır ve halka öndeki pencereyi sınırlı süre izler (<see cref="StartWatch"/>).
    /// </summary>
    private void ShowAndActivate()
    {
        Show();
        Activate();
        var hwnd = Handle;
        if (ForegroundInfo.Window() != hwnd) NativeMethods.SetForegroundWindow(hwnd);
        FocusRing();
        if (ForegroundInfo.Window() != hwnd) StartWatch();
    }

    private void FocusRing()
    {
        _ring.Focus();
        Keyboard.Focus(_ring);
    }

    // ---------------------------------------------------------------- "tuşu bırakınca seç"

    /// <summary>
    /// Kısayolun tuşları hâlâ basılı mı (bırakılınca, halkanın kendi tuş olaylarıyla anlaşılır); basılıysa (ve ayar kapalı
    /// değilse) bırakınca seçmeye hazırlanır. Basılı tuşlar Ctrl+Shift ile "yönetici olarak" seçmeyi de engeller.
    /// </summary>
    private void Arm()
    {
        Disarm();
        // Kısayolsuz açılan halka (çift tıklama, tepsi) hep tıklama kipindedir.
        if (_origin != RingOrigin.Hotkey)
        {
            _hotkeyKeys = [];
            _hotkeyHeld = false;
            return;
        }
        _hotkeyKeys = Hotkey.TryParse(AppHost.Settings.Hotkeys.Ring, out var hotkey) ? KeySender.VirtualKeysOf(hotkey) : [];
        _hotkeyHeld = _hotkeyKeys.Count > 0 && !AllReleased();
        _armed = !AppHost.Settings.RingHoldSelectOff && _hotkeyHeld;
        // Bırakınca seç sürerken vurgu imlecin gittiği yönden (OnPointerMove).
        _ring.PointerFromOwner = _armed;
    }

    private void Disarm()
    {
        _armed = false;
        _ring.PointerFromOwner = false;
    }

    private bool AllReleased() => _hotkeyKeys.All(vk => NativeMethods.GetAsyncKeyState(vk) >= 0);

    /// <summary>
    /// Fare halkanın penceresinde gitti: "tuşu bırakınca seç" sürerken vurgu imlecin açılıştan bu yana gittiği yönden (nişan
    /// alanında da); öğeye basılıp sürüklenirken imlecin yerinden. Öteki durumlarda halka (RingView) yalnızca diskin üstünde
    /// vurgular: nişan alanı tıklamayla kapatır, bir öğeyi vurgulamaz.
    /// </summary>
    private void OnPointerMove()
    {
        if (_closing || !IsVisible) return;
        if (_armed)
        {
            NativeMethods.GetCursorPos(out var now);
            _ring.HighlightAt(ArmedPoint(now));
        }
        else if (_ring.IsMouseCaptured) _ring.HighlightAt(Mouse.GetPosition(_ring));
    }

    /// <summary>
    /// İmlecin açılıştan bu yana gittiği yön, halkanın kendi koordinatında (orta + yer değiştirme): halka ekran kenarında
    /// imleçten kaydırılmış olsa da küçük bir hareket o yöndeki öğeyi seçer.
    /// </summary>
    private Point ArmedPoint(NativeMethods.POINT now)
    {
        var c = _ring.Metrics.OuterRadius;
        return new Point(c + (now.X - _openCursor.X) / _scale, c + (now.Y - _openCursor.Y) / _scale);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        // Halka odakta değilse (ör. öne gelemedi, sonra tıklandı) Esc yine kapatır.
        if (key == Key.Escape && !_ring.IsKeyboardFocusWithin && !_closing)
        {
            e.Handled = true;
            Dismiss();
            return;
        }
        if (!_armed || key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
                or Key.LWin or Key.RWin) return;
        // Kısayolun dışında bir tuşa basan klavyeyle seçiyor: bırakınca seçme biter.
        if (!_hotkeyKeys.Contains(KeyInterop.VirtualKeyFromKey(key))) Disarm();
    }

    /// <summary>
    /// Kısayol bırakıldı: imleç yeterince gittiyse ve bir öğeye bakıyorsa o seçilir (imleç pencerenin dışına çıkmış olabilir:
    /// yön imlecin açılıştan bu yana gittiği yönden), yoksa halka tıklama kipinde kalır.
    /// </summary>
    private void Released()
    {
        Disarm();
        _hotkeyHeld = false;
        if (!IsVisible || _closing) return;
        NativeMethods.GetCursorPos(out var now);
        var dx = now.X - _openCursor.X;
        var dy = now.Y - _openCursor.Y;
        if (Math.Sqrt((double)dx * dx + (double)dy * dy) < RingGeometry.DragThresholdDip * _scale)
        {
            // Tıklama kipi: imlecin yönünden kalan vurgu silinir (halka fareyle vurgular).
            _ring.ClearHighlight();
            return;
        }
        _ring.HighlightAt(ArmedPoint(now));
        if (_ring.HighlightedSlot is not null) _ring.CommitHighlight();
    }

    /// <summary>
    /// Yalnızca halka öne gelemediyse (klavye olayları halkaya gelmez, Deactivated hiç gelmez): öndeki pencere kısa aralıkla
    /// izlenir; başka bir pencere öne gelince ya da en çok 10 sn içinde halkaya dokunulmazsa (imleç üstünde değilse) halka
    /// kapanır. "Tuşu bırakınca seç" sürüyorsa aynı sürede yalnızca kısayolun tuşları (başka tuş değil) yoklanır. Halka öne
    /// gelince (tıklanınca) izleme biter.
    /// </summary>
    private void StartWatch()
    {
        _watchUntil = Environment.TickCount64 + (long)InactiveLimit.TotalMilliseconds;
        if (_watch is null)
        {
            _watch = new DispatcherTimer(DispatcherPriority.Input);
            _watch.Tick += OnWatchTick;
        }
        _watch.Interval = TimeSpan.FromMilliseconds(_armed ? 30 : 100);
        _watch.Start();
    }

    private void StopWatch() => _watch?.Stop();

    private void OnWatchTick(object? sender, EventArgs e)
    {
        var hwnd = Handle;
        if (!IsVisible || _closing || hwnd == IntPtr.Zero)
        {
            StopWatch();
            return;
        }
        var foreground = ForegroundInfo.Window();
        if (foreground == hwnd)
        {
            // Öne geldi: klavye ve Deactivated halkada.
            StopWatch();
            return;
        }
        var expired = Environment.TickCount64 > _watchUntil;
        if (_armed)
        {
            if (expired) Disarm();
            else if (AllReleased())
            {
                Released();
                if (_closing || !IsVisible) return;
            }
            if (!_armed && _watch is { } watch) watch.Interval = TimeSpan.FromMilliseconds(100);
        }
        // Başka bir pencere öne geldi (kullanıcı oraya geçti ya da tıkladı): halka yalnızca kapanır.
        if (foreground != IntPtr.Zero && foreground != _previous) CloseWith(restore: false, after: null);
        // Süre doldu ve imleç halkada değil: etkin olmayan en üstteki halka ekranda kalmasın.
        else if (expired && !IsMouseOver) CloseWith(restore: false, after: null);
    }

    // ---------------------------------------------------------------- seçim ve kapanış

    private void OnItemInvoked(RingSlot slot)
    {
        if (_closing || !IsVisible) return;
        // Tuş ve metin yalnızca kısayolla açılan halkada (açıklama halkanın üstünde yazar).
        if (slot.Action is { } dimmed && !RingAvailability.Available(dimmed.Type, _origin)) return;
        if (RingRunner.NeedsTarget(slot) && slot.Action is { } action)
        {
            // Tuş ve metin: halka gerçekten kapanınca, halka açılmadan önce öndeki pencereye.
            var target = _previous;
            var keys = _hotkeyKeys.ToList();
            CloseWith(restore: false, after: () => _ = RingRunner.SendToTargetAsync(action, target, keys));
            return;
        }
        // Öbürleri halka hâlâ öndeyken başlar (açılan pencere öne gelebilsin); Ctrl+Shift: yönetici olarak (Windows sorar).
        // Kısayolun tuşları hâlâ basılıysa Ctrl+Shift onlardan olabilir (ör. Ctrl+Shift+Boşluk): yanlışlıkla yönetici olmasın.
        var admin = Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && !_hotkeyHeld;
        RingRunner.RunFromPopup(slot, admin);
        CloseWith(restore: false, after: null);
    }

    /// <summary>Kısayol yeniden, Esc, sağ tık, kökte orta: kapanır; kısayolla açıldıysa önceki pencere öne gelir.</summary>
    private void Dismiss() => CloseWith(restore: RingAvailability.RestoresPrevious(_origin), after: null);

    private void CloseWith(bool restore, Action? after)
    {
        if (!IsVisible || _closing) return;
        _closing = true;
        Disarm();
        StopWatch();
        var previous = _previous;
        _afterClose = () =>
        {
            if (restore && previous != IntPtr.Zero && NativeMethods.IsWindow(previous)) NativeMethods.SetForegroundWindow(previous);
            after?.Invoke();
        };
        Motion.RingClose(_root, HideNow, _ring.CenterElement);
    }

    /// <summary>Pencere gizlenir (kapanış hareketi bitince ya da hemen), sonra bekleyen iş (önceki pencere, tuş/metin).</summary>
    private void HideNow()
    {
        var after = _afterClose;
        _afterClose = null;
        Hide();
        _closing = false;
        _previous = IntPtr.Zero;
        _rootPressed = false;
        _hotkeyHeld = false;
        _ring.ClearHighlight();
        after?.Invoke();
        // Kısayol (ve çift tıklama) halka açıkken kapatıldı (Sync açık pencereye dokunmaz): şimdi bütünüyle kapanır.
        if (!Prebuild)
        {
            Sync();
            return;
        }
        // Açıkken gelen değişiklik ya da alt halka: sonraki açılış için boşta.
        if (_ring.InSubRing) _dirty = true;
        if (_dirty) MarkDirty();
    }

    // ---------------------------------------------------------------- açılışa göre ve "Halkayı düzenle"

    /// <summary>Açılışa göre soluk öğeler (yeniden çizilir) ve açıklama satırı.</summary>
    private void ApplyOrigin()
    {
        _ring.Redraw();
        UpdateFooter();
    }

    /// <summary>Gösterilen halkada soluk (yalnızca tuşla açılan halkada çalışan) öğe varsa açıklama halkanın üstünde yazar.</summary>
    private void UpdateFooter()
    {
        var any = _ring.Slots.Any(s => s.Action is { } a && !RingAvailability.Available(a.Type, _origin));
        _footer.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        if (_config is not null) Arrange(Canvas.GetLeft(_ring) + _ring.Metrics.OuterRadius, Canvas.GetTop(_ring) + _ring.Metrics.OuterRadius);
    }

    /// <summary>
    /// "Halkayı düzenle": efektsiz, hareketsiz düz metin düğmesi (fare zemini hemen değişir); halkanın zemin rengindeki küçük
    /// hap, her pencerenin üstünde okunur. Tab ile de ulaşılır.
    /// </summary>
    private static Button EditButton()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Chip";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        border.SetValue(Border.PaddingProperty, new Thickness(12, 4, 12, 5));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(BorderBrush)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        border.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var button = new Button
        {
            Content = L.T("Halkayı düzenle"), Template = template, FontSize = 12, Cursor = Cursors.Hand,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
        };
        AutomationProperties.SetAutomationId(button, "Ring.Edit");
        AutomationProperties.SetName(button, L.T("Halkayı düzenle"));
        return button;
    }

    private Brush _chipFill = Brushes.Transparent, _chipHover = Brushes.Transparent;

    /// <summary>Düğmenin ve açıklamanın renkleri halkanın zemininden (yüksek karşıtlıkta sistem renkleri).</summary>
    private void PaintChrome(WidgetPalette palette)
    {
        var (fill, hover, text, edge) = RingBackdrop.Chip(palette);
        _chipFill = fill;
        _chipHover = hover;
        _edit.Background = fill;
        _edit.Foreground = text;
        _edit.BorderBrush = edge;
        _footer.Background = fill;
        _footer.Foreground = text;
        _edit.MouseEnter -= OnEditHover;
        _edit.MouseLeave -= OnEditHover;
        _edit.MouseEnter += OnEditHover;
        _edit.MouseLeave += OnEditHover;
    }

    private void OnEditHover(object sender, MouseEventArgs e) => _edit.Background = _edit.IsMouseOver ? _chipHover : _chipFill;

    /// <summary>"Halkayı düzenle": halka kapanır (önceki pencereye dönülmez) ve bu halkanın düzenleyicisi açılır.</summary>
    private void EditRing()
    {
        if (_closing || !IsVisible || _config is not { } config) return;
        var tab = _ring.TabIndex;
        CloseWith(restore: false, after: () => RingEditor.Open(config, tab));
    }

    private void CloseForRealCore()
    {
        _forReal = true;
        AppHost.SettingsChanged -= OnSettingsChanged;
        AppHost.Widgets.Changed -= OnSettingsChanged;
        BoxMover.Changed -= OnPathsChanged;
        AppHost.PathRenamed -= OnPathRenamed;
        StopWatch();
        Motion.Stop(_root);
        CompositionTarget.Rendering -= OnFirstFrame;
        _ring.Release();
        try { Close(); }
        catch (InvalidOperationException) { }
    }
}

/// <summary>
/// Açılır halkanın zemini ve gölgesi (efektsiz): çemberin altında degrade gölge (dışa doğru solar, biraz aşağıda) ve paletten
/// zemin diski; Cam'da her pencerenin üstünde okunabilsin diye koyu ve neredeyse opak. Yüksek karşıtlıkta sistem renkleri,
/// gölge yok.
/// </summary>
internal sealed class RingBackdrop : FrameworkElement
{
    /// <summary>Gölgenin çemberden taşan payı (DIP; nişan alanının içinde kalır).</summary>
    public const double ShadowDip = 22;

    private Brush? _shadow;
    private Brush _plate = Brushes.Transparent;
    private Pen? _edge;
    private double _outer;

    public RingBackdrop()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <param name="style">Halkanın biçimi: Işıltı'da zeminin kenarı vurgu renginin saydamı (yüksek karşıtlıkta yok sayılır).</param>
    public void Apply(WidgetPalette palette, double outer, RingStyle? style = null)
    {
        _outer = outer;
        Width = Height = 2 * (outer + ShadowDip);
        if (SystemParameters.HighContrast)
        {
            _shadow = null;
            _plate = SystemColors.WindowBrush;
            _edge = Frozen(new Pen(SystemColors.WindowTextBrush, 2));
        }
        else
        {
            // Gölge dairesi biraz aşağıda (3 DIP) ve o kadar küçük: zeminin içinde kalır.
            var reach = outer + ShadowDip - 3;
            _shadow = Frozen(Radial(
                (Math.Max(0, outer - 12) / reach, Color.FromArgb(0x5C, 0, 0, 0)),
                (outer / reach, Color.FromArgb(0x38, 0, 0, 0)),
                ((outer + 8) / reach, Color.FromArgb(0x12, 0, 0, 0)),
                (1, Color.FromArgb(0, 0, 0, 0))));
            // Cam'da koyu, neredeyse opak zemin (RingColors simgelerin rengini buna göre de denetler).
            _plate = palette.IsOpaqueBackground ? palette.Background
                : Frozen(Radial((0, ColorOf(RingColors.PopupPlateInner)), (1, ColorOf(RingColors.PopupPlateOuter))));
            _edge = style?.Glow == true && palette.Accent is SolidColorBrush accent
                ? Frozen(new Pen(Frozen(new SolidColorBrush(RingView.WithAlpha(accent.Color, 0x66))), 1))
                : Frozen(new Pen(palette.IsOpaqueBackground ? palette.BorderBrush : WidgetPalette.Solid(0x38FFFFFF), 1));
        }
        InvalidateVisual();
    }

    /// <summary>
    /// Halkanın altındaki "Halkayı düzenle" hapının ve açıklama satırının renkleri: zemin (opak kartta kartın zemini, Cam'da
    /// açılır halkanın koyu zemini), fareyle üstündeyken (Cam'da biraz açığı), yazı ve kenar. Yüksek karşıtlıkta sistem renkleri.
    /// </summary>
    public static (Brush Fill, Brush Hover, Brush Text, Brush Edge) Chip(WidgetPalette palette)
    {
        if (SystemParameters.HighContrast)
            return (SystemColors.WindowBrush, SystemColors.WindowBrush, SystemColors.WindowTextBrush, SystemColors.WindowTextBrush);
        if (palette.IsOpaqueBackground)
            return (palette.Background, palette.Background, palette.Foreground, palette.BorderBrush);
        return (WidgetPalette.Solid(RingColors.PopupPlateOuter), WidgetPalette.Solid(RingColors.PopupPlateInner),
            WidgetPalette.Solid(0xFFF3F3F3), WidgetPalette.Solid(0x38FFFFFF));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var c = new Point(_outer + ShadowDip, _outer + ShadowDip);
        if (_shadow is not null) dc.DrawEllipse(_shadow, null, new Point(c.X, c.Y + 3), _outer + ShadowDip - 3, _outer + ShadowDip - 3);
        dc.DrawEllipse(_plate, _edge, c, _outer - 0.5, _outer - 0.5);
    }

    private static RadialGradientBrush Radial(params (double Offset, Color Color)[] stops)
    {
        var brush = new RadialGradientBrush();
        foreach (var (offset, color) in stops) brush.GradientStops.Add(new GradientStop(color, Math.Clamp(offset, 0, 1)));
        return brush;
    }

    private static Color ColorOf(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
