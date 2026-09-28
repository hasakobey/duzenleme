using System.Windows;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>
/// Küçük iletişim kutularını (geri sayım, şehir seçimi) isteğin geldiği ekranda ortalar: "Widget ekle" penceresinin ya da
/// widget'ın bulunduğu monitör; yoksa imlecin monitörü. Yerleşim <see cref="WindowFit.CenterOn"/> ile (pencere en baştan
/// o monitörde oluşur, boyu çalışma alanını geçmez).
/// </summary>
internal static class DialogPlacement
{
    public static void CenterOn(Window window, NativeMethods.POINT? near)
    {
        NativeMethods.POINT anchor;
        if (near is { } point) anchor = point;
        else NativeMethods.GetCursorPos(out anchor);
        WindowFit.CenterOn(window, anchor);
    }
}
