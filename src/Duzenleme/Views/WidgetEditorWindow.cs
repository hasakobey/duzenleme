using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

/// <summary>
/// "Düzenle" panelinin kendi penceresi (widget menüsünün ilk satırı "Düzenle…" ve kartın ⋯ düğmesi, <see cref="CardButtons"/>): 360 DIP
/// genişliğinde, kartın yanında ve onun monitöründe (<see cref="AnchorPlacement.NextTo"/>), ekrandan uzunsa içi kayar. Aynı anda
/// tek panel açıktır; başka bir kartta "Düzenle…" aynı paneli o karta geçirir. Kip değildir ve odak gidince kapanmaz (simge ve
/// renk seçici, karta tıklamak onu kapatmasın): Esc, "Bitti" (Enter), başlıktaki ×, widget kaldırılınca ya da kapanınca ve
/// uygulamadan çıkınca kapanır. Açılırken yalnızca kısa bir solma (<see cref="Motion.Enter"/>, Kapalı'da hemen).
/// </summary>
internal sealed class WidgetEditorWindow : FluentWindow
{
    /// <summary>Panelin genişliği (DIP).</summary>
    public const double PanelWidth = 360;

    private static WidgetEditorWindow? _current;

    private readonly WidgetEditor _editor;
    private readonly Grid _root;
    private WidgetWindow _widget;
    private bool _closing;

    /// <summary>Paneli bu widget için açar; açıksa ona geçirir ve öne getirir.</summary>
    public static void ShowFor(WidgetWindow widget)
    {
        if (_current is { _closing: false } open)
        {
            open.Retarget(widget);
            open.Activate();
            return;
        }
        var window = new WidgetEditorWindow(widget);
        _current = window;
        window.Closed += (_, _) => { if (_current == window) _current = null; };
        window.Show();
    }

    /// <summary>Panel şu an bu widget'ı mı düzenliyor?</summary>
    internal static bool IsEditing(WidgetWindow widget) => _current is { _closing: false } open && ReferenceEquals(open._widget, widget);

    /// <summary>
    /// Widget'tan açılan küçük pencerenin (simge ve renk seçici) yanına yerleşeceği alan: kart ve (bu kart düzenleniyorsa)
    /// panelin birleşimi; ikisini de örtmesin. Kart görünmüyorsa null.
    /// </summary>
    internal static Box? AnchorBox(WidgetWindow widget)
    {
        var card = widget.VisibleCardBox;
        if (!IsEditing(widget) || _current!.PixelBox() is not { } panel) return card;
        if (card is not { } c) return panel;
        return new Box(Math.Min(c.Left, panel.Left), Math.Min(c.Top, panel.Top), Math.Max(c.Right, panel.Right), Math.Max(c.Bottom, panel.Bottom));
    }

    private WidgetEditorWindow(WidgetWindow widget)
    {
        _widget = widget;
        // WPF ilk açılan pencereyi Application.MainWindow yapar; tema değişikliği (WPF-UI) bu küçük pencereye bağlanmasın.
        if (Application.Current?.MainWindow == this) Application.Current.MainWindow = null;

        Title = L.T("Düzenle");
        Width = PanelWidth;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.Mica;
        ShowInTaskbar = false;
        MinWidth = 0;
        MinHeight = 0;
        AutomationProperties.SetAutomationId(this, "Editor");
        // Çerçeve ilk ölçümden sonra kalkar (yoksa altta boşluk kalır); pencere en baştan kartın monitöründe oluşur ve o ekranın
        // çalışma alanından uzun olamaz (içi kayar, başlık ve alttaki düğmeler hep görünür).
        WindowFit.FitChromeToContent(this);
        var point = widget.CenterPoint;
        WindowFit.StartOn(this, point);
        MaxHeight = MaxHeightAt(point);

        _editor = new WidgetEditor { DoneIsDefault = true };
        _editor.DoneRequested += Close;
        _root = new Grid();
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition());
        _root.Children.Add(new TitleBar { Title = L.T("Düzenle"), ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(_editor, 1);
        _root.Children.Add(_editor);
        Content = _root;
        _editor.Edit(widget);
        Hook(widget);

        Loaded += (_, _) =>
        {
            PlaceNextToCard();
            // Yalnızca kısa bir solma (en çok 200 ms; Kapalı'da hemen). Girdi beklemez.
            Motion.Enter(_root, MotionKind.Fade);
            Dispatcher.BeginInvoke(() => _editor.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)), DispatcherPriority.Input);
        };
        // Ölçeği farklı bir monitöre geçince (WPF boyutu değiştirir) yer bir kez daha hesaplanır; boyu değişince ekranda kalır.
        DpiChanged += (_, _) => { if (IsLoaded) Dispatcher.BeginInvoke(PlaceNextToCard); };
        SizeChanged += (_, _) => { if (IsLoaded) Dispatcher.BeginInvoke(KeepOnScreen); };
        // Esc kabarcıkla gelir: önce ad kutusu (yazılanı geri alır) ve açık açılır liste (kapanır) işler.
        KeyDown += OnKey;
        // Başka bir yerden (sağ tık menüsü, Ctrl + tekerlek, renk seçici) değişen ayar panele dönünce görünür.
        Activated += (_, _) => _editor.Refresh();
        Closing += (_, _) =>
        {
            _closing = true;
            _editor.CommitPending();
        };
        Closed += (_, _) =>
        {
            Unhook(_widget);
            _editor.Release();
        };
    }

    /// <summary>Başka bir kartta "Düzenle…": panel o karta geçer (yeni "Geri al" noktası) ve onun yanına taşınır.</summary>
    private void Retarget(WidgetWindow widget)
    {
        if (ReferenceEquals(widget, _widget)) return;
        Unhook(_widget);
        _widget = widget;
        Hook(widget);
        MaxHeight = MaxHeightAt(widget.CenterPoint);
        _editor.Edit(widget);
        if (IsLoaded) Dispatcher.BeginInvoke(PlaceNextToCard, DispatcherPriority.Loaded);
    }

    private void Hook(WidgetWindow widget)
    {
        widget.Closed += OnWidgetClosed;
        if (AppHost.Widgets is { } widgets) widgets.Changed += OnWidgetsChanged;
    }

    private void Unhook(WidgetWindow widget)
    {
        widget.Closed -= OnWidgetClosed;
        if (AppHost.Widgets is { } widgets) widgets.Changed -= OnWidgetsChanged;
    }

    /// <summary>Widget'ın penceresi kapandı (kaldırıldı, düzen uygulandı, uygulama kapanıyor): panel de kapanır.</summary>
    private void OnWidgetClosed(object? sender, EventArgs e)
    {
        if (!_closing) Close();
    }

    /// <summary>Widget listesi değişti: düzenlenen widget kaldırıldıysa panel kapanır, yoksa satırlar tazelenir.</summary>
    private void OnWidgetsChanged()
    {
        if (_closing) return;
        if (!AppHost.Settings.Widgets.Contains(_widget.Config))
        {
            Close();
            return;
        }
        _editor.Refresh();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        // Esc: ad kutusu yazılanı geri aldıysa (işlendi) panel açık kalır; değilse kapanır. Açık açılır liste Esc'i kendisi işler.
        if (e.Key == Key.Escape && !e.Handled)
        {
            e.Handled = true;
            Close();
        }
    }

    private static double MaxHeightAt(NativeMethods.POINT point) =>
        Math.Max(320, NativeMethods.WorkAreaAt(point).Height / NativeMethods.ScaleAt(point) - 16);

    /// <summary>Pencerenin ekrandaki dikdörtgeni (fiziksel piksel); yoksa null.</summary>
    private Box? PixelBox()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        return hwnd != IntPtr.Zero && IsVisible && NativeMethods.GetWindowRect(hwnd, out var r) ? new Box(r.Left, r.Top, r.Right, r.Bottom) : null;
    }

    /// <summary>Kartın yanına (sağına, sığmazsa soluna, altına, üstüne) aynı monitörde yerleştirir.</summary>
    private void PlaceNextToCard()
    {
        if (_closing) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(hwnd, out var r)) return;
        var point = _widget.CenterPoint;
        var work = NativeMethods.WorkAreaAt(point);
        var area = new Box(work.Left, work.Top, work.Right, work.Bottom);
        var card = _widget.VisibleCardBox ?? new Box(point.X, point.Y, point.X, point.Y);
        var gap = (int)Math.Round(12 * NativeMethods.ScaleAt(point));
        var (x, y) = AnchorPlacement.NextTo(card, r.Width, r.Height, area, gap);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
    }

    /// <summary>Boyu değişen pencere ekranın içinde kalır (yalnızca taşıyorsa, en kısa yoldan; canlandırma yok).</summary>
    private void KeepOnScreen()
    {
        if (_closing) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(hwnd, out var r)) return;
        var work = NativeMethods.WorkAreaAt(new NativeMethods.POINT { X = r.Left + r.Width / 2, Y = r.Top + Math.Min(r.Height / 2, 40) });
        var x = Math.Clamp(r.Left, work.Left, Math.Max(work.Left, work.Right - r.Width));
        var y = Math.Clamp(r.Top, work.Top, Math.Max(work.Top, work.Bottom - r.Height));
        if (x == r.Left && y == r.Top) return;
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }
}
