using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Duzenleme.Views;

/// <summary>
/// Pencereyi açıldığı monitörün çalışma alanına sığdırır ve ortalar. 1366×768 gibi laptop ekranlarında
/// 720 piksellik pencerenin başlık çubuğu ekranın üstüne taşmasın (sonra tutulup taşınamaz).
/// </summary>
public static class WindowFit
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    public static void Attach(Window window) => window.SourceInitialized += (_, _) => Fit(window);

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
        const double margin = 16;

        window.MinWidth = Math.Min(window.MinWidth, width - margin);
        window.MinHeight = Math.Min(window.MinHeight, height - margin);
        if (!double.IsNaN(window.Width)) window.Width = Math.Min(window.Width, width - margin);
        if (!double.IsNaN(window.Height)) window.Height = Math.Min(window.Height, height - margin);

        var w = double.IsNaN(window.Width) ? window.ActualWidth : window.Width;
        var h = double.IsNaN(window.Height) ? window.ActualHeight : window.Height;
        window.Left = left + Math.Max(0, (width - w) / 2);
        window.Top = Math.Max(top, top + (height - h) / 2);
    }
}
