using System.Windows;
using System.Windows.Interop;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>
/// Küçük iletişim kutularını (geri sayım, şehir seçimi) isteğin geldiği ekranda ortalar: "Widget ekle" penceresinin ya da
/// widget'ın bulunduğu monitör; yoksa imlecin monitörü. Pencere en baştan o monitörde oluşur (ölçeği farklıysa sonradan
/// büyüyüp taşmasın), boyu çalışma alanını geçmez.
/// </summary>
internal static class DialogPlacement
{
    public static void CenterOn(Window window, NativeMethods.POINT? near)
    {
        NativeMethods.POINT anchor;
        if (near is { } point) anchor = point;
        else NativeMethods.GetCursorPos(out anchor);
        WindowFit.StartOn(window, anchor);
        window.MaxHeight = Math.Max(240, NativeMethods.WorkAreaAt(anchor).Height / NativeMethods.ScaleAt(anchor) - 16);
        window.Loaded += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (!NativeMethods.GetWindowRect(hwnd, out var r)) return;
            var work = NativeMethods.WorkAreaAt(anchor);
            var x = work.Left + Math.Max(0, (work.Width - r.Width) / 2);
            var y = work.Top + Math.Max(0, (work.Height - r.Height) / 3);
            NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
        };
    }
}
