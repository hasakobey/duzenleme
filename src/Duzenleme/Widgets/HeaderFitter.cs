using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Dar widget'ın başlık satırı: başlık yazısı "…" olmasın diye isteğe bağlı parçaları (sayı, düğmeler, simge) verilen
/// sırayla geçici olarak gizler (<see cref="HeaderFit"/>). Parçaların görünürlüğü her çağrıdan önce kullanıcının
/// seçimine (Göster ▸) göre yeniden kurulmalıdır; gizleme hiçbir yere kaydedilmez.
/// </summary>
internal static class HeaderFitter
{
    private static readonly Size Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);

    /// <param name="header">Başlık satırı (genişliği üst öğeden gelir, içeriğinden değil).</param>
    /// <param name="title">Genişleyen (*) sütundaki başlık yazısı.</param>
    /// <param name="optional">Gizlenebilecek parçalar, ilk gizlenecek önce.</param>
    /// <param name="fixedParts">Hep yerinde kalan parçalar (kaldırma düğmesi ×).</param>
    public static void Fit(FrameworkElement header, TextBlock title, IReadOnlyList<UIElement> optional, IReadOnlyList<UIElement> fixedParts) =>
        Fit(header, NaturalWidth(title), optional, fixedParts);

    /// <summary>
    /// Başlık yazısı yerine verilen genişlikle sığdırır (ör. başlık yerinde düzenlenirken: kutuya en az
    /// <paramref name="titleMin"/> kalsın).
    /// </summary>
    public static void Fit(FrameworkElement header, double titleNatural, IReadOnlyList<UIElement> optional, IReadOnlyList<UIElement> fixedParts,
        double titleMin = HeaderFit.TitleMin)
    {
        if (header.Visibility != Visibility.Visible || header.ActualWidth <= 0) return;
        var visible = optional.Where(e => e.Visibility == Visibility.Visible).ToList();
        foreach (var part in visible) part.Measure(Unbounded);
        var fixedWidth = 0.0;
        foreach (var part in fixedParts.Where(e => e.Visibility == Visibility.Visible))
        {
            part.Measure(Unbounded);
            fixedWidth += part.DesiredSize.Width;
        }
        var hide = HeaderFit.PartsToHide(header.ActualWidth, fixedWidth, titleNatural, visible.Select(e => e.DesiredSize.Width).ToList(), titleMin);
        for (var i = 0; i < hide; i++) visible[i].Visibility = Visibility.Collapsed;
    }

    /// <summary>Yazının kısaltılmamış genişliği (yerleşimi bozmadan, kendi yazı tipiyle ölçülür).</summary>
    public static double NaturalWidth(TextBlock text)
    {
        if (string.IsNullOrEmpty(text.Text)) return 0;
        var formatted = new FormattedText(text.Text, CultureInfo.CurrentUICulture, text.FlowDirection,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, Brushes.Black,
            null, TextOptions.GetTextFormattingMode(text), VisualTreeHelper.GetDpi(text).PixelsPerDip);
        return Math.Ceiling(formatted.WidthIncludingTrailingWhitespace) + text.Margin.Left + text.Margin.Right + text.Padding.Left + text.Padding.Right;
    }
}
