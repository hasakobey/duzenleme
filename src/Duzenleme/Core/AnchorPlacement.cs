namespace Duzenleme.Core;

/// <summary>
/// Bir widget'tan açılan küçük pencerenin (simge seçici, soru) yeri: widget'ın bulunduğu monitörde, kartın yanında. Birincil
/// monitörün ortasında açılan pencere çok monitörlü düzende widget'tan uzakta kalırdı. Fiziksel piksel; saf, testlenir.
/// </summary>
public static class AnchorPlacement
{
    /// <summary>
    /// Pencerenin sol üstü: sığıyorsa kartın sağında, olmazsa solunda (üst kenarları hizalı), olmazsa altında, olmazsa
    /// üstünde; hiçbiri sığmıyorsa çalışma alanının içinde kartın üstüne. Pencere her zaman çalışma alanında kalır.
    /// </summary>
    /// <param name="anchor">Kartın dikdörtgeni.</param>
    /// <param name="width">Pencerenin genişliği.</param>
    /// <param name="height">Pencerenin yüksekliği.</param>
    /// <param name="work">Kartın monitörünün çalışma alanı.</param>
    /// <param name="gap">Kartla pencere arasındaki boşluk.</param>
    public static (int X, int Y) NextTo(Box anchor, int width, int height, Box work, int gap)
    {
        int ClampX(int x) => Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - width));
        int ClampY(int y) => Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - height));

        if (anchor.Right + gap + width <= work.Right) return (anchor.Right + gap, ClampY(anchor.Top));
        if (anchor.Left - gap - width >= work.Left) return (anchor.Left - gap - width, ClampY(anchor.Top));
        if (anchor.Bottom + gap + height <= work.Bottom) return (ClampX(anchor.Left), anchor.Bottom + gap);
        if (anchor.Top - gap - height >= work.Top) return (ClampX(anchor.Left), anchor.Top - gap - height);
        return (ClampX(anchor.Left + (anchor.Width - width) / 2), ClampY(anchor.Top + (anchor.Height - height) / 2));
    }
}
