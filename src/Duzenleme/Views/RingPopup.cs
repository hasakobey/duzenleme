using System.Text.Json;
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
/// neredeyse saydamdır (#01000000): oraya gelen fare de halkanındır, dışarı tıklamak halkayı kapatır. Gölge degrade ile
/// çizilir (Effect yok); renkler hedef widget'ın paletinden. Alt bırakılınca menü kipine girilmez (SC_KEYMENU yutulur).</para>
/// <para>Hız: kısayol açıksa pencere açılıştan sonra boşta önceden kurulur (tutamaç, halka, simgeler); kapatınca yalnızca
/// gizlenir. Ayar, kutu taşıması ya da ad değişikliği içeriği eskitir; yeniden kurulum kısayol yolunda değil boşta yapılır.
/// Açılış: önceki pencere ve imleç okunur (yalnızca bellekte), pencere gizliyken imlecin monitörüne (önce taşınıp ölçeği
/// düzeltilerek) <see cref="RingPlacement.ForPopup"/> ile yerleştirilir, gösterilir ve öne getirilir; hareket
/// <see cref="Motion.RingOpen"/>. Girdi hareketi beklemez (isabet testi açıyla).</para>
/// <para>Kapanma: kısayol yeniden, Esc, sağ tık ya da kökte ortaya basmak kapatır ve önceki pencereyi öne getirir; öğe seçmek
/// kapatır (öğe kendi penceresini açar); dışarı tıklamak (etkinlik kaybı) yalnızca kapatır. Tuş ve metin öğeleri halka gerçekten
/// kapandıktan sonra (<see cref="Motion.RingClose"/> geri çağırması) önceki pencereye gönderilir (<see cref="RingRunner.SendToTargetAsync"/>).</para>
/// <para>"Tuşu bırakınca seç" (<see cref="AppSettings.RingHoldSelectOff"/> kapalıyken): kısayolun tuşları açılışta hâlâ basılıysa
/// halka kurulur; bırakılınca imleç en az <see cref="RingGeometry.DragThresholdDip"/> kadar gittiyse ve bir öğe vurguluysa o
/// seçilir, yoksa halka tıklama kipinde kalır. Bırakma WPF'in KeyUp'ından anlaşılır; yalnızca halka öne gelemediyse ve halka
/// açıkken, en çok 10 sn ve yalnızca kısayolun tuşları GetAsyncKeyState ile yoklanır (kanca yok).</para>
/// </summary>
internal sealed class RingPopup : Window
{
    /// <summary>Çemberin dışındaki nişan alanı (DIP): fare oraya da gidebilir, tıklamak vurgulanan öğeyi seçer.</summary>
    public const double AimDip = 72;

    /// <summary>"Tuşu bırakınca seç" için yoklama sınırı (yalnızca halka öne gelemediyse).</summary>
    private static readonly TimeSpan HoldPollLimit = TimeSpan.FromSeconds(10);

    private static RingPopup? _instance;
    private static bool _syncQueued, _exiting;

    private readonly Canvas _root;
    private readonly RingBackdrop _backdrop = new();
    private readonly RingView _ring = new() { IsPopup = true, StableSize = true };

    private WidgetConfig? _config;
    private string? _signature;
    private bool _dirty = true, _rebuildQueued, _forReal, _builtHighContrast;

    // Açıkken; yalnızca bellekte (hiçbir yere yazılmaz, günlüğe girmez). Kapanınca bırakılır.
    private IntPtr _previous;
    private NativeMethods.POINT _anchor, _openCursor;
    private double _scale = 1;
    private bool _closing, _armed, _rootPressed;
    private Action? _afterClose;
    private List<int> _hotkeyKeys = [];
    private DispatcherTimer? _poll;
    private long _pollUntil;

    // ---------------------------------------------------------------- dışarıya açık (arayüz iş parçacığında)

    /// <summary>NESTDESK_TEST_RING=1 (yalnızca test masaüstünde): kısayol kaydetmeden halka yolu açık.</summary>
    public static bool TestRing => RingHotkey.TestOverride(AppHost.IsTestDesktop, AppEnvironment.Get("TEST_RING"));

    /// <summary>Halka yolu açık mı: kısayol atanmış ya da test.</summary>
    public static bool Enabled => RingShortcut.IsOn || TestRing;

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
            if (Enabled)
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
        var at = PerfLog.Now();
        if (_instance is { IsVisible: true, _closing: false } open)
        {
            open.Dismiss();
            return;
        }
        (_instance ??= new RingPopup()).Open(at);
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
        KeyboardNavigation.SetTabNavigation(_root, KeyboardNavigationMode.Cycle);
        Content = _root;

        // Pencere en baştan imlecin (ya da NESTDESK_RING_AT'in) ekranında oluşur: ölçeği doğru başlasın.
        WindowFit.StartOn(this, AnchorNow());
        var side = 2 * AimDip;
        Width = Height = side;

        _ring.ItemInvoked += OnItemInvoked;
        _ring.BackRequested += Dismiss;
        _ring.CloseRequested += Dismiss;
        _ring.Navigated += () => Motion.RingSwap(_ring.CenterElement, _ring.ItemElements, _ring.TowardCenter);

        PreviewMouseMove += (_, e) => { if (!_closing) _ring.HighlightAt(e.GetPosition(_ring)); };
        PreviewMouseDown += (_, e) =>
        {
            // Fareyle seçen kullanıcı tıklama kipindedir.
            Disarm();
            if (e.ChangedButton == MouseButton.Right) e.Handled = true;
        };
        _root.MouseLeftButtonDown += (_, e) => { _rootPressed = true; e.Handled = true; };
        // Nişan alanına (ya da diskin basılmamış yerine) tıklamak vurgulanan öğeyi seçer.
        _root.MouseLeftButtonUp += (_, e) =>
        {
            if (!_rootPressed || e.Handled || _closing) return;
            _rootPressed = false;
            e.Handled = true;
            _ring.CommitHighlight();
        };
        // Sağ tık kapatır (bırakınca: bırakma alttaki pencerede menü açmasın).
        PreviewMouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            Dismiss();
        };
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += (_, _) => { if (_armed && AllReleased()) Released(); };
        Activated += (_, _) =>
        {
            // Sonradan (ör. halkaya tıklayınca) öne geldi: klavye halkada, yoklamaya gerek kalmaz.
            StopPolling();
            if (IsVisible && !_closing) FocusRing();
        };
        // Dışarı tıklandı (başka pencere öne geldi): yalnızca kapanır.
        Deactivated += (_, _) => { if (IsVisible && !_closing) CloseWith(restore: false, after: null); };
        // Açıkken ekranın ölçeği değişti: yeniden yerleşir (simgeler RingView'de yeniden istenir).
        DpiChanged += (_, _) => { if (IsVisible && !_closing) Dispatcher.BeginInvoke(Place); };

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
        var config = RingTargets.Pick(AppHost.Settings.Widgets, AppHost.Settings.RingWidgetId);
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
        _builtHighContrast = SystemParameters.HighContrast;
        var palette = RingView.PaletteFor(config);
        _ring.ApplyPalette(palette);
        _ring.CenterShowsIcon = !config.Shows(WidgetNameLine.PartKey);
        _ring.SetRing(config, 0);
        _backdrop.Apply(palette, _ring.Metrics.OuterRadius);
        var side = 2 * (_ring.Metrics.OuterRadius + AimDip);
        Arrange(side / 2, side / 2);
        _root.Measure(new Size(side, side));
        _root.Arrange(new Rect(0, 0, side, side));
    }

    /// <summary>Halka ve gölgesi, ortaları (<paramref name="x"/>, <paramref name="y"/>) DIP'te olacak biçimde.</summary>
    private void Arrange(double x, double y)
    {
        var outer = _ring.Metrics.OuterRadius;
        Canvas.SetLeft(_ring, x - outer);
        Canvas.SetTop(_ring, y - outer);
        Canvas.SetLeft(_backdrop, x - outer - RingBackdrop.ShadowDip);
        Canvas.SetTop(_backdrop, y - outer - RingBackdrop.ShadowDip);
    }

    /// <summary>
    /// Halkanın görünüşünü belirleyen ayarların özeti: ilgisiz ayar değişiklikleri (konum, başka widget) yeniden kurdurmaz.
    /// Yalnızca bellekte, karşılaştırma için.
    /// </summary>
    private static string Signature(WidgetConfig config) => JsonSerializer.Serialize(new
    {
        config.Id, config.Style, config.Accent, config.IconSize, config.Scale, config.Title, config.Icon, config.HiddenParts,
        config.Tabs, config.ItemLooks, SystemParameters.HighContrast,
    }, JsonFile.Options);

    private void OnSettingsChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(OnSettingsChanged);
            return;
        }
        if (_forReal || _dirty) return;
        var config = RingTargets.Pick(AppHost.Settings.Widgets, AppHost.Settings.RingWidgetId);
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

    private void Open(double pressedAt)
    {
        // Önceki pencere ve imleç yalnızca şimdi okunur ve yalnızca bellekte tutulur. Kapanırken yeniden açıldıysa öndeki
        // pencere halkanın kendisidir: önceki hedef korunur.
        var foreground = ForegroundInfo.Window();
        var previous = foreground == Handle ? _previous : foreground;
        NativeMethods.GetCursorPos(out _openCursor);
        _anchor = AnchorNow();
        if (_closing)
        {
            // Kapanırken yeniden açıldı: kapanış (ve bekleyen işi) bırakılır.
            Motion.Stop(_root);
            _afterClose = null;
            HideNow();
        }
        _previous = previous;

        var config = RingTargets.Pick(AppHost.Settings.Widgets, AppHost.Settings.RingWidgetId);
        if (config is null)
        {
            _previous = IntPtr.Zero;
            var near = _anchor;
            Notice.Show(L.T("Henüz halka menün yok."), NoticeKind.Info, L.T("Halka menü ekle"), () => RingShortcut.AddRing(near),
                L.T("Eklemek için buraya tıkla."));
            return;
        }
        // Değişiklikler boşta işlenir (OnSettingsChanged); burada yalnızca hâlâ eskiyse (ör. boşa düşmeden basıldıysa).
        if (_dirty || !ReferenceEquals(config, _config) || _builtHighContrast != SystemParameters.HighContrast) Build(config);
        else if (_ring.InSubRing) _ring.SetRing(config, 0);
        if (_ring.Slots.Count == 0)
        {
            _previous = IntPtr.Zero;
            Notice.Show(L.T("Halka menün boş. Masaüstündeki halkaya bir uygulama, dosya ya da klasör sürükle."), NoticeKind.Info);
            return;
        }

        Place();
        PerfLog.Mark("Ring.hotkey", atMs: pressedAt);
        if (PerfLog.Enabled) CompositionTarget.Rendering += OnFirstFrame;
        _ring.ClearHighlight();
        _rootPressed = false;
        Arm();
        Motion.RingOpen(_root, ShowAndActivate, _ring.CenterElement, _ring.ItemElements, _ring.TowardCenter);
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
    private void Place()
    {
        var hwnd = Handle;
        if (hwnd == IntPtr.Zero) return;
        var work = NativeMethods.WorkAreaAt(_anchor);
        var target = NativeMethods.ScaleAt(_anchor);
        if (!IsVisible && Math.Abs(VisualTreeHelper.GetDpi(this).PixelsPerDip - target) > 0.001)
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, work.Left, work.Top, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        _scale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var monitor = NativeMethods.MonitorAreaAt(_anchor);
        var popup = RingPlacement.ForPopup(new Box(monitor.Left, monitor.Top, monitor.Right, monitor.Bottom),
            new Box(work.Left, work.Top, work.Right, work.Bottom), _anchor.X, _anchor.Y, _ring.Metrics.OuterRadius, AimDip, _scale);
        Width = Height = popup.SidePx / _scale;
        Arrange(popup.CenterXDip, popup.CenterYDip);
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, popup.Place.Left, popup.Place.Top, popup.SidePx, popup.SidePx,
            NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>
    /// Gösterir ve öne getirir: WM_HOTKEY'i alan süreç öne geçebilir; olmazsa SetForegroundWindow (iş parçacığı girdisi
    /// bağlanmaz). Yine olmazsa fare çalışır, "tuşu bırakınca seç" kısayolun tuşlarını sınırlı süre yoklar.
    /// </summary>
    private void ShowAndActivate()
    {
        Show();
        Activate();
        var hwnd = Handle;
        if (ForegroundInfo.Window() != hwnd) NativeMethods.SetForegroundWindow(hwnd);
        FocusRing();
        if (_armed && ForegroundInfo.Window() != hwnd) StartPolling();
    }

    private void FocusRing()
    {
        _ring.Focus();
        Keyboard.Focus(_ring);
    }

    // ---------------------------------------------------------------- "tuşu bırakınca seç"

    /// <summary>Kısayolun tuşları hâlâ basılıysa (ve ayar kapalı değilse) bırakınca seçmeye hazırlanır.</summary>
    private void Arm()
    {
        Disarm();
        _hotkeyKeys = Hotkey.TryParse(AppHost.Settings.Hotkeys.Ring, out var hotkey) ? KeySender.VirtualKeysOf(hotkey) : [];
        _armed = !AppHost.Settings.RingHoldSelectOff && _hotkeyKeys.Count > 0 && !AllReleased();
    }

    private void Disarm()
    {
        _armed = false;
        StopPolling();
    }

    private bool AllReleased() => _hotkeyKeys.All(vk => NativeMethods.GetAsyncKeyState(vk) >= 0);

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
    /// yön imlecin şimdiki yerinden), yoksa halka tıklama kipinde kalır.
    /// </summary>
    private void Released()
    {
        Disarm();
        if (!IsVisible || _closing) return;
        NativeMethods.GetCursorPos(out var now);
        var dx = now.X - _openCursor.X;
        var dy = now.Y - _openCursor.Y;
        if (Math.Sqrt((double)dx * dx + (double)dy * dy) < RingGeometry.DragThresholdDip * _scale) return;
        _ring.HighlightAt(_ring.PointFromScreen(new Point(now.X, now.Y)));
        if (_ring.HighlightedSlot is not null) _ring.CommitHighlight();
    }

    /// <summary>Yalnızca halka öne gelemediyse: kısayolun tuşları (başka tuş değil) halka açıkken en çok 10 sn yoklanır.</summary>
    private void StartPolling()
    {
        _pollUntil = Environment.TickCount64 + (long)HoldPollLimit.TotalMilliseconds;
        if (_poll is null)
        {
            _poll = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(30) };
            _poll.Tick += (_, _) =>
            {
                if (!_armed || !IsVisible || _closing || Environment.TickCount64 > _pollUntil)
                {
                    Disarm();
                    return;
                }
                if (AllReleased()) Released();
            };
        }
        _poll.Start();
    }

    private void StopPolling() => _poll?.Stop();

    // ---------------------------------------------------------------- seçim ve kapanış

    private void OnItemInvoked(RingSlot slot)
    {
        if (_closing || !IsVisible) return;
        if (RingRunner.NeedsTarget(slot) && slot.Action is { } action)
        {
            // Tuş ve metin: halka gerçekten kapanınca, halka açılmadan önce öndeki pencereye.
            var target = _previous;
            var keys = _hotkeyKeys.ToList();
            CloseWith(restore: false, after: () => _ = RingRunner.SendToTargetAsync(action, target, keys));
            return;
        }
        // Öbürleri halka hâlâ öndeyken başlar (açılan pencere öne gelebilsin); Ctrl+Shift: yönetici olarak (Windows sorar).
        var admin = Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift);
        RingRunner.RunFromPopup(slot, admin);
        CloseWith(restore: false, after: null);
    }

    /// <summary>Kısayol yeniden, Esc, sağ tık, kökte orta: kapanır ve önceki pencere öne gelir.</summary>
    private void Dismiss() => CloseWith(restore: true, after: null);

    private void CloseWith(bool restore, Action? after)
    {
        if (!IsVisible || _closing) return;
        _closing = true;
        Disarm();
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
        _ring.ClearHighlight();
        after?.Invoke();
        // Açıkken gelen değişiklik ya da alt halka: sonraki açılış için boşta.
        if (_ring.InSubRing) _dirty = true;
        if (_dirty) MarkDirty();
    }

    private void CloseForRealCore()
    {
        _forReal = true;
        AppHost.SettingsChanged -= OnSettingsChanged;
        AppHost.Widgets.Changed -= OnSettingsChanged;
        BoxMover.Changed -= OnPathsChanged;
        AppHost.PathRenamed -= OnPathRenamed;
        StopPolling();
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

    public void Apply(WidgetPalette palette, double outer)
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
            _plate = palette.IsOpaqueBackground ? palette.Background
                : Frozen(Radial((0, Color.FromArgb(0xFA, 0x24, 0x22, 0x32)), (1, Color.FromArgb(0xF7, 0x13, 0x12, 0x1B))));
            _edge = Frozen(new Pen(palette.IsOpaqueBackground ? palette.BorderBrush : WidgetPalette.Solid(0x38FFFFFF), 1));
        }
        InvalidateVisual();
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

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
