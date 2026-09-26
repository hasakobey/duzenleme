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
        ResizeMode = view.Resizable ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize;
        SizeToContent = view.Resizable ? SizeToContent.Manual : SizeToContent.WidthAndHeight;
        MinWidth = view.Resizable ? 240 : 0;
        MinHeight = view.Resizable ? 170 : 0;

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
            if (Config.Locked || e.ButtonState != MouseButtonState.Pressed) return;
            try { DragMove(); } catch (InvalidOperationException) { }
        };

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveBounds(); };
        LocationChanged += (_, _) => QueueSave();
        SizeChanged += (_, _) => QueueSave();

        ApplyStyle();
        BuildMenu();
        Loaded += (_, _) => PlaceOnScreen();
    }

    public void ApplyStyle()
    {
        var palette = WidgetPalette.For(Config.Style);
        _card.Background = palette.Background;
        _card.BorderBrush = palette.BorderBrush;
        TextElement.SetForeground(_card, palette.Foreground);
        View.ApplyPalette(palette);
    }

    private void BuildMenu()
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) =>
        {
            menu.Items.Clear();
            View.AddMenuItems(menu);
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());

            var style = new MenuItem { Header = "Görünüm" };
            foreach (var (s, label) in new[] { (WidgetStyle.Glass, "Cam"), (WidgetStyle.Dark, "Koyu"), (WidgetStyle.Light, "Açık") })
            {
                var item = new MenuItem { Header = label, IsCheckable = true, IsChecked = Config.Style == s };
                item.Click += (_, _) => { Config.Style = s; ApplyStyle(); AppHost.SaveSettings(); };
                style.Items.Add(item);
            }
            menu.Items.Add(style);

            var lockItem = new MenuItem { Header = "Konumu kilitle", IsCheckable = true, IsChecked = Config.Locked };
            lockItem.Click += (_, _) => { Config.Locked = !Config.Locked; AppHost.SaveSettings(); };
            menu.Items.Add(lockItem);

            menu.Items.Add(new Separator());
            var remove = new MenuItem { Header = "Kaldır" };
            remove.Click += (_, _) => AppHost.Widgets.Remove(Config.Id);
            menu.Items.Add(remove);
        };
        menu.Items.Add(new MenuItem()); // Açılırken yeniden doldurulur.
        _card.ContextMenu = menu;
    }

    private void PlaceOnScreen()
    {
        var virtualScreen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

        if (View.Resizable)
        {
            Width = double.IsNaN(Config.Width) ? 400 : Config.Width;
            Height = double.IsNaN(Config.Height) ? 290 : Config.Height;
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
        Config.Left = Left;
        Config.Top = Top;
        if (View.Resizable)
        {
            Config.Width = ActualWidth;
            Config.Height = ActualHeight;
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
