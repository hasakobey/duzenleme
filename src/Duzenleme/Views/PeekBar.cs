using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Windows masaüstüne göz atarken ekranın üst ortasındaki küçük çubuk: "Windows masaüstü · 1:42 · [+5 dk] [NestDesk'e dön]".
/// Katmanlı değil (ucuz; belirirken yalnızca içeriği kısa solar), hiç etkinleşmez (odağı çalmaz; tıklamalar yine çalışır) ve widget'lar gibi masaüstüne
/// sahiplidir: Win+D'de kaybolmaz, masaüstünden açılan uygulamanın üstünde kalmaz. Süre dolunca, kullanıcı masaüstünde
/// çalışmıyorsa (sürükleme, yazma) NestDesk'e dönülür.
/// </summary>
internal sealed class PeekBar : Window
{
    private static PeekBar? _current;

    private readonly NativeMethods.POINT _anchor;
    private readonly PeekClock _clock;
    private readonly DispatcherTimer _tick;
    private readonly TextBlock _time;
    private readonly Button _more;
    private readonly Button _return;

    /// <summary>
    /// Çubuğun açılacağı ekran: kullanıcının çalıştığı yer, yani etkin pencerenin ekranı (kısayol yazılan uygulamanın, widget
    /// menüsünün, Ayarlar'ın ekranı); etkin pencere yoksa ya da masaüstünün kendisiyse (çift tıklama) imlecin ekranı. Testte
    /// NESTDESK_PEEK_AT="x,y". Göz atma pencereleri küçültmeden önce okunur.
    /// </summary>
    public static NativeMethods.POINT Anchor() =>
        NativeMethods.PointFromEnvironment("PEEK_AT") ?? NativeMethods.ActiveMonitorCenter() ?? CursorPoint();

    /// <summary>Çubuğu <paramref name="anchor"/> noktasının (verilmezse <see cref="Anchor"/>) ekranında açar; açık olanı kapatır.</summary>
    public static void Open(int minutes, NativeMethods.POINT? anchor = null)
    {
        CloseBar();
        Show(anchor ?? Anchor(), new PeekClock(DateTime.UtcNow, minutes));
    }

    private static void Show(NativeMethods.POINT anchor, PeekClock clock)
    {
        _current = new PeekBar(anchor, clock);
        Widgets.QuietShow.Show(_current);
    }

    public static void CloseBar()
    {
        var bar = _current;
        _current = null;
        // Explorer yeniden başlarken sahibiyle birlikte zaten kapanmış olabilir.
        try { bar?.Close(); }
        catch (InvalidOperationException) { }
    }

    private static NativeMethods.POINT CursorPoint()
    {
        NativeMethods.GetCursorPos(out var cursor);
        return cursor;
    }

    private PeekBar(NativeMethods.POINT anchor, PeekClock clock)
    {
        _anchor = anchor;
        _clock = clock;
        // WPF ilk açılan pencereyi Application.MainWindow yapar; tema değişikliği (WPF-UI) bu çubuğa uygulanmasın.
        if (Application.Current?.MainWindow == this) Application.Current.MainWindow = null;
        Title = $"{AppInfo.Name} · Windows masaüstü";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, "SolidBackgroundFillColorTertiaryBrush");
        AutomationProperties.SetAutomationId(this, "Peek.Bar");
        AutomationProperties.SetName(this, "Windows masaüstü");
        // Pencere en baştan çubuğun ekranında oluşur: ölçeği farklı bir ekrana sonradan taşınıp yeniden boyutlanmasın.
        WindowFit.StartOn(this, anchor);

        var icon = new SymbolIcon { Symbol = SymbolRegular.Glance24, FontSize = 18, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        icon.SetResourceReference(ForegroundProperty, "AccentTextFillColorPrimaryBrush");
        var label = new TextBlock { Text = "Windows masaüstü", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _time = new TextBlock { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, MinWidth = 48 };
        _time.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        AutomationProperties.SetAutomationId(_time, "Peek.Time");

        _more = new Button
        {
            Content = "+5 dk", Appearance = ControlAppearance.Transparent, Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center, ToolTip = "Beş dakika daha göz at",
        };
        AutomationProperties.SetAutomationId(_more, "Peek.More");
        AutomationProperties.SetName(_more, "Beş dakika daha göz at");
        _more.Click += (_, _) =>
        {
            _clock.Extend(DateTime.UtcNow, PeekClock.Extension);
            UpdateTime();
        };

        _return = new Button
        {
            Content = $"{AppInfo.Name}'e dön", Appearance = ControlAppearance.Primary, Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center, Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowHookUpLeft24 },
        };
        AutomationProperties.SetAutomationId(_return, "Peek.Return");
        AutomationProperties.SetName(_return, $"{AppInfo.Name}'e dön");
        var hotkey = AppHost.Settings.Hotkeys.PeekDesktop;
        _return.ToolTip = string.IsNullOrWhiteSpace(hotkey)
            ? "Widget'lar geri gelir"
            : $"Widget'lar geri gelir. Kısayol: {hotkey}";
        _return.Click += (_, _) => AppHost.EndPeek();

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(icon);
        row.Children.Add(label);
        row.Children.Add(_time);
        row.Children.Add(_more);
        row.Children.Add(_return);
        var border = new Border { Padding = new Thickness(14, 6, 8, 6), BorderThickness = new Thickness(1), Child = row };
        border.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        Content = border;

        _tick = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => OnTick();
        UpdateTime();

        Loaded += (_, _) =>
        {
            Place();
            // İçerik kısa bir solmayla belirir (geçişler açıksa; pencere katmanlı değil, zemini hemen görünür).
            Motion.FadeIn(border);
            if (_clock.AutoReturn) _tick.Start();
        };
        // Ölçeği farklı bir ekrana geçerse boyut değişir: ortalama yeniden yapılır.
        DpiChanged += (_, _) => { if (IsLoaded) Dispatcher.BeginInvoke(Place); };
        Closed += (_, _) =>
        {
            _tick.Stop();
            if (_current != this) return;
            // Beklenmedik kapanma: masaüstü (sahibi) Explorer yeniden başlarken yok oldu. Göz atma sürüyorsa çubuk, Explorer
            // toparlanınca aynı geri sayımla yeniden açılır; yoksa kullanıcının dönüş yolu kaybolurdu.
            _current = null;
            var reopen = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            reopen.Tick += (_, _) =>
            {
                reopen.Stop();
                if (AppHost.Peeking && _current is null) Show(_anchor, _clock);
            };
            reopen.Start();
        };
    }

    private void OnTick()
    {
        if (_current != this) return;
        if (_clock.Expired(DateTime.UtcNow, UserBusyOnDesktop()))
        {
            DebugLog.Write("göz atma süresi doldu");
            AppHost.EndPeek();
            return;
        }
        UpdateTime();
    }

    /// <summary>
    /// Kullanıcı masaüstünde çalışıyor mu: sol tuş basılı (sürükleme) ya da masaüstü önde ve son 10 saniyede giriş var
    /// (yeniden adlandırma, seçim). Öyleyse dönüş ertelenir; simgeler elinin altından kaybolmasın.
    /// </summary>
    private static bool UserBusyOnDesktop()
    {
        const int VK_LBUTTON = 0x01;
        if (NativeMethods.GetAsyncKeyState(VK_LBUTTON) < 0) return true;
        return DesktopIcons.IsDesktopSurface(NativeMethods.GetForegroundWindow()) && NativeMethods.IdleTime() < TimeSpan.FromSeconds(10);
    }

    private void UpdateTime()
    {
        var remaining = _clock.Remaining(DateTime.UtcNow);
        _more.Visibility = remaining is null ? Visibility.Collapsed : Visibility.Visible;
        if (remaining is not { } left)
        {
            _time.Text = "";
            _time.Visibility = Visibility.Collapsed;
            AutomationProperties.SetHelpText(_return, "Kendiliğinden dönülmez");
            return;
        }
        _time.Text = "· " + PeekClock.Format(left);
        AutomationProperties.SetHelpText(_return, $"{PeekClock.Format(left)} sonra kendiliğinden döner");
    }

    /// <summary>Başlangıç ekranının çalışma alanında, üst ortada (12 DIP aşağıda); fiziksel pikselle.</summary>
    private void Place()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var r)) return;
        var area = NativeMethods.WorkAreaAt(_anchor);
        var scale = NativeMethods.ScaleAt(_anchor);
        var x = area.Left + Math.Max(0, (area.Width - r.Width) / 2);
        var y = area.Top + (int)Math.Round(12 * scale);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        // Hiç etkinleşmez (yazılan pencere odağı kaybetmez); görev çubuğunda ve Alt+Tab'da görünmez.
        const long WS_EX_NOACTIVATE = 0x08000000;
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex = (ex | NativeMethods.WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));
        // Masaüstüne sahipli (widget'lar gibi): Win+D'de kaybolmaz, en üstte durmaz.
        var owner = DesktopIcons.DesktopOwner();
        if (owner != IntPtr.Zero) NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWLP_HWNDPARENT, owner);
        // Windows 11'de yuvarlak köşe (daha eski sürümde yok sayılır).
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;
        var round = DWMWCP_ROUND;
        try { _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int)); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
