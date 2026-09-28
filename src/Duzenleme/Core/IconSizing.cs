namespace Duzenleme.Core;

/// <summary>
/// Simgelerin ekrandaki gerçek piksel boyutu. Simge tam bu boyutta istenir ve 1:1 çizilir: iki kat büyük istenip WPF'e
/// küçülttürmek (ya da sistemin 32 piksellik simgesini büyütmek) %125/%150/%175 ölçekte bulanık görünür.
/// </summary>
public static class IconSizing
{
    /// <summary>İstenecek en büyük simge (kabuğun en büyük simge karesi 256).</summary>
    public const int MaxPixels = 256;

    /// <summary>
    /// <paramref name="dip"/> boyutunda (DIP) gösterilecek simgenin cihaz pikseli: ekran ölçeği (pixelsPerDip) × widget
    /// ölçeği. WPF'in yerleşim yuvarlamasıyla aynı yuvarlanır (Math.Round).
    /// </summary>
    public static int DevicePixels(double dip, double pixelsPerDip, double widgetScale = 1)
    {
        if (double.IsNaN(pixelsPerDip) || pixelsPerDip <= 0) pixelsPerDip = 1;
        if (double.IsNaN(widgetScale) || widgetScale <= 0) widgetScale = 1;
        var pixels = Math.Round(Math.Max(0, dip) * pixelsPerDip * Math.Clamp(widgetScale, 0.5, 2.5));
        return (int)Math.Clamp(pixels, 1, MaxPixels);
    }
}
