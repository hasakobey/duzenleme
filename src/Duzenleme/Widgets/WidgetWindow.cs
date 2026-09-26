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

    private readonly Border _card;
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
        Title = "Düzenleme widget";

        _card = new Border
        {
            CornerRadius = new CornerRadius(20),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 16, 20, 18),
            Margin = new Thickness(ShadowMargin),
            Child = (UIElement)view,
            Effect = new DropShadowEffect { BlurRadius = 26, ShadowDepth = 4, Direction = 270, Opacity = 0.32, Color = Colors.Black },
        };
        Content = _card;

        _card.MouseLeftButtonDown += (_, e) =>
        {
            if (Config.Locked || e.ButtonState != MouseButtonState.Pressed || e.ClickCount > 1) return;
            try { DragMove(); } catch (InvalidOperationException) { }
        };

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveBounds(); };
        LocationChanged += (_, _) => QueueSave();
        SizeChanged += (_, _) => QueueSave();
        View.CollapseToggleRequested += () => SetCollapsed(!Config.Collapsed);

        ApplyStyle();
        ApplyLayoutMode();
        _card.ContextMenu = Menus.Dynamic(FillMenu);
        Loaded += (_, _) => PlaceOnScreen();
    }

    public void ApplyStyle()
    {
        var palette = View.AdjustPalette(WidgetPalette.For(Config.Style, Config.Accent));
        _card.Background = palette.Background;
        _card.BorderBrush = palette.BorderBrush;
        TextElement.SetForeground(_card, palette.Foreground);
        _card.LayoutTransform = Math.Abs(Config.Scale - 1) < 0.01 ? Transform.Identity : new ScaleTransform(Config.Scale, Config.Scale);
        Opacity = Math.Clamp(Config.Opacity, 0.4, 1);
        View.ApplyPalette(palette);
    }

    /// <summary>Katlanmış bölme yalnızca başlık kadar yer kaplar ve yeniden boyutlandırılamaz.</summary>
    private void ApplyLayoutMode()
    {
        var collapsed = View.Collapsible && Config.Collapsed;
        View.SetBodyVisible(!collapsed);
        if (View.Resizable && !collapsed)
        {
            SizeToContent = SizeToContent.Manual;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            MinWidth = 240;
            MinHeight = 170;
            if (_positionReady)
            {
                Height = double.IsNaN(Config.Height) ? 300 : Config.Height;
            }
        }
        else
        {
            ResizeMode = ResizeMode.NoResize;
            MinHeight = 0;
            if (View.Resizable)
            {
                // Genişlik korunur, yükseklik başlığa iner (SizeToContent.Height genişliği bozduğu için elle ölçülür).
                SizeToContent = SizeToContent.Manual;
                MinWidth = 240;
                if (_positionReady) FitCollapsedHeight();
            }
            else
            {
                SizeToContent = SizeToContent.WidthAndHeight;
                MinWidth = 0;
            }
        }
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

    private void FillMenu(ContextMenu menu)
    {
        View.AddMenuItems(menu);
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());

        if (View.Collapsible)
            menu.Items.Add(Menus.Toggle("Başlığa katla", Config.Collapsed, () => SetCollapsed(!Config.Collapsed)));

        var custom = new MenuItem { Header = "Özelleştir" };
        custom.Items.Add(Menus.Choice("Görünüm", Config.Style,
            [(WidgetStyle.Glass, "Cam"), (WidgetStyle.Dark, "Koyu"), (WidgetStyle.Light, "Açık")],
            v => Update(() => Config.Style = v)));
        custom.Items.Add(Menus.Choice("Vurgu rengi", Config.Accent,
            [(WidgetAccent.Violet, "Mor"), (WidgetAccent.Blue, "Mavi"), (WidgetAccent.Green, "Yeşil"), (WidgetAccent.Orange, "Turuncu"), (WidgetAccent.Pink, "Pembe")],
            v => Update(() => Config.Accent = v)));
        custom.Items.Add(Menus.Choice("Boyut", Math.Round(Config.Scale, 2),
            [(0.8, "Küçük (%80)"), (1.0, "Normal"), (1.25, "Büyük (%125)"), (1.5, "Çok büyük (%150)")],
            v => Update(() => Config.Scale = v)));
        custom.Items.Add(Menus.Choice("Saydamlık", Math.Round(Config.Opacity, 2),
            [(1.0, "Yok"), (0.85, "%15"), (0.7, "%30"), (0.55, "%45")],
            v => Update(() => Config.Opacity = v)));
        menu.Items.Add(custom);

        menu.Items.Add(Menus.Toggle("Konumu kilitle", Config.Locked, () => { Config.Locked = !Config.Locked; AppHost.SaveSettings(); }));
        menu.Items.Add(Menus.Item("Çoğalt", () => AppHost.Widgets.Duplicate(Config.Id)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item("Kaldır", () => AppHost.Widgets.Remove(Config.Id)));
    }

    private void Update(Action change)
    {
        change();
        ApplyStyle();
        AppHost.SaveSettings();
    }

    private void PlaceOnScreen()
    {
        var virtualScreen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

        if (View.Resizable)
        {
            Width = double.IsNaN(Config.Width) ? 400 : Config.Width;
            if (View.Collapsible && Config.Collapsed) FitCollapsedHeight();
            else Height = double.IsNaN(Config.Height) ? 300 : Config.Height;
        }

        var visible = !double.IsNaN(Config.Left) && !double.IsNaN(Config.Top) &&
                      virtualScreen.IntersectsWith(new Rect(Config.Left + 40, Config.Top + 40, 60, 60));
        if (visible)
        {
            Left = Config.Left;
            Top = Config.Top;
        }
        else
        {
            var p = AppHost.Widgets.DefaultPosition(Config.Kind, ActualWidth);
            Left = p.X;
            Top = p.Y;
        }
        _positionReady = true;
        SaveBounds();
    }

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
        if (View.Resizable)
        {
            // Width/Height ayarlandıktan hemen sonra ActualWidth henüz güncellenmemiş olabilir.
            Config.Width = double.IsNaN(Width) ? ActualWidth : Width;
            if (!(View.Collapsible && Config.Collapsed)) Config.Height = double.IsNaN(Height) ? ActualHeight : Height;
        }
        AppHost.SaveSettings();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex = (ex | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));

        // Masaüstü penceresine (Progman) bağla: Win+D ile gizlenmez.
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWLP_HWNDPARENT, progman);

        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        SendToBack(hwnd);
    }

    private static void SendToBack(IntPtr hwnd) =>
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_WINDOWPOSCHANGING)
        {
            // Pencere öne alınmaya çalışılsa bile en altta kalsın.
            var pos = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(lParam);
            if ((pos.flags & NativeMethods.SWP_NOZORDER) == 0)
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
        View.Detach();
        base.OnClosed(e);
    }
}
