using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>
/// Pencereyi açıldığı monitörün çalışma alanına sığdırır ve ortalar. 1366×768 gibi laptop ekranlarında
/// 720 piksellik pencerenin başlık çubuğu ekranın üstüne taşmasın (sonra tutulup taşınamaz).
/// Test için DUZENLEME_WINDOW_AT="x,y" (fiziksel piksel) verilirse pencere o noktanın monitöründe açılır.
/// </summary>
public static class WindowFit
{
    private const double Margin = 16;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    /// <summary>
    /// Hedef nokta: DUZENLEME_WINDOW_AT varsa o, yoksa onCursorMonitor ise imlecin yeri; ikisi de yoksa pencerenin
    /// açıldığı monitör. Pencere gösterilmeden (kurucuda) çağrılır.
    /// </summary>
    public static void Attach(Window window, bool onCursorMonitor = false)
    {
        var target = TestPoint();
        if (target is null && onCursorMonitor && NativeMethods.GetCursorPos(out var cursor)) target = cursor;
        if (target is not { } point)
        {
            window.SourceInitialized += (_, _) => Fit(window);
            return;
        }

        StartOn(window, point);
        window.SourceInitialized += (_, _) => FitAt(window, point);
    }

    /// <summary>
    /// Pencereyi en baştan noktanın monitöründe oluşturur (yalnızca başlangıç yeri; gösterilmeden çağrılır). Sonradan ölçeği
    /// (DPI) farklı bir monitöre taşınırsa WPF onu yeniden boyutlar ve önceden okunan boyut yanlış kalır. Kesin fiziksel yeri
    /// çağıran sonra verir.
    /// </summary>
    internal static void StartOn(Window window, NativeMethods.POINT point)
    {
        var area = NativeMethods.WorkAreaAt(point);
        StartAt(window, area.Left + 8, area.Top + 8);
    }

    /// <summary>
    /// Pencerenin başlangıç yerini fiziksel pikselle verir (gösterilmeden). WPF pencereyi oluştururken Left/Top'u noktanın
    /// monitörünün KENDİ ölçeğiyle ekrana çevirir (sistem ölçeğiyle değil) ve noktayı bir monitörde bulamazsa pencereyi
    /// birincil ekranda açıp sonra taşır: ölçeği farklı monitöre geçen pencere yeniden boyutlanır (widget'ı etkinleştirir,
    /// açılışı yavaşlatır). Nokta bu yüzden monitörün içine kısılır ve o monitörün ölçeğiyle çevrilir.
    /// </summary>
    internal static void StartAt(Window window, int x, int y)
    {
        var point = new NativeMethods.POINT { X = x, Y = y };
        var monitor = NativeMethods.MonitorAreaAt(point);
        point.X = Math.Clamp(x, monitor.Left, Math.Max(monitor.Left, monitor.Right - 1));
        point.Y = Math.Clamp(y, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - 1));
        var scale = NativeMethods.ScaleAt(point);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = point.X / scale;
        window.Top = point.Y / scale;
    }

    /// <summary>
    /// İçeriğe göre boyutlanan (SizeToContent) FluentWindow'un altında ve sağında kalan boşluğu giderir. WPF pencereyi ilk kez
    /// ölçerken Windows'un standart çerçevesini (başlık çubuğu ve kenarlar, %125'te ~70 piksel) içerik boyutuna ekler;
    /// FluentWindow bu çerçeveyi (WindowChrome) ancak OnSourceInitialized'da kaldırır ve içerik değişmediği için pencere o
    /// kadar büyük kalırdı. Çerçeve kalkınca pencere yeniden ölçülür; WPF'in ortaladığı pencere (CenterScreen/CenterOwner)
    /// ortada kalır. Gösterilmeden, öteki SourceInitialized işleyicilerinden (<see cref="CenterOn"/>) önce çağrılır.
    /// </summary>
    public static void FitChromeToContent(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            if (window.SizeToContent == SizeToContent.Manual) return;
            var hwnd = new WindowInteropHelper(window).Handle;
            if (!NativeMethods.GetWindowRect(hwnd, out var before)) return;
            window.InvalidateMeasure();
            window.UpdateLayout();
            if (!NativeMethods.GetWindowRect(hwnd, out var after) || (after.Width == before.Width && after.Height == before.Height)) return;
            if (window.WindowStartupLocation == WindowStartupLocation.Manual) return;
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero,
                after.Left + (before.Width - after.Width) / 2, after.Top + (before.Height - after.Height) / 2, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        };
    }

    /// <summary>
    /// Küçük pencereyi (soru, ad kutusu) noktanın monitöründe açar ve o monitörün ortasına koyar: widget'tan açılan pencere
    /// birincil monitörde, widget'tan uzakta açılmasın. Gösterilmeden çağrılır.
    /// </summary>
    internal static void CenterOn(Window window, NativeMethods.POINT point)
    {
        StartOn(window, point);
        window.SourceInitialized += (_, _) => FitAt(window, point);
        // Boyutu içerikten gelen pencere ilk yerleşimde büyüyebilir: ortalama bir kez daha, gerçek boyutla.
        window.Loaded += (_, _) => FitAt(window, point);
    }

    /// <summary>NESTDESK_WINDOW_AT (ya da DUZENLEME_WINDOW_AT) ="x,y" (fiziksel piksel); yoksa ya da okunamazsa null.</summary>
    private static NativeMethods.POINT? TestPoint() =>
        Core.AppEnvironment.Get("WINDOW_AT")?.Split(',') is [var x, var y] &&
        int.TryParse(x.Trim(), out var px) && int.TryParse(y.Trim(), out var py)
            ? new NativeMethods.POINT { X = px, Y = py }
            : null;

    private static void Fit(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */), ref info)) return;

        var dpi = VisualTreeHelper.GetDpi(window);
        var left = info.rcWork.Left / dpi.DpiScaleX;
        var top = info.rcWork.Top / dpi.DpiScaleY;
        var width = (info.rcWork.Right - info.rcWork.Left) / dpi.DpiScaleX;
        var height = (info.rcWork.Bottom - info.rcWork.Top) / dpi.DpiScaleY;

        ClampSize(window, width, height);
        var w = double.IsNaN(window.Width) ? window.ActualWidth : window.Width;
        var h = double.IsNaN(window.Height) ? window.ActualHeight : window.Height;
        window.Left = left + Math.Max(0, (width - w) / 2);
        window.Top = Math.Max(top, top + (height - h) / 2);
    }

    /// <summary>Pencereyi noktanın monitörüne sığdırıp ortalar (QuickAddWindow.PlaceNearAnchor gibi fiziksel pikselle).</summary>
    private static void FitAt(Window window, NativeMethods.POINT point)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var area = NativeMethods.WorkAreaAt(point);
        var scale = NativeMethods.ScaleAt(point);
        ClampSize(window, area.Width / scale, area.Height / scale);

        var wDip = double.IsNaN(window.Width) ? window.ActualWidth : window.Width;
        var hDip = double.IsNaN(window.Height) ? window.ActualHeight : window.Height;
        int w, h;
        var flags = NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE;
        if (wDip > 0 && hDip > 0)
        {
            w = (int)Math.Round(wDip * scale);
            h = (int)Math.Round(hDip * scale);
        }
        else
        {
            // Boyutu içerikten gelen pencere: yalnızca yeri ayarlanır.
            if (!NativeMethods.GetWindowRect(hwnd, out var r)) return;
            (w, h) = (r.Width, r.Height);
            flags |= NativeMethods.SWP_NOSIZE;
        }
        var x = area.Left + Math.Max(0, (area.Width - w) / 2);
        var y = area.Top + Math.Max(0, (area.Height - h) / 2);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, flags);
    }

    /// <summary>En küçük ve istenen boyutu çalışma alanına (DIP) sığdırır.</summary>
    private static void ClampSize(Window window, double width, double height)
    {
        window.MinWidth = Math.Min(window.MinWidth, width - Margin);
        window.MinHeight = Math.Min(window.MinHeight, height - Margin);
        if (!double.IsNaN(window.Width)) window.Width = Math.Min(window.Width, width - Margin);
        if (!double.IsNaN(window.Height)) window.Height = Math.Min(window.Height, height - Margin);
    }
}
