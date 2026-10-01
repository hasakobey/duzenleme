namespace Duzenleme.Core;

/// <summary>
/// Kısayolla açılan halkanın yeri (fiziksel piksel; saf, testlenir). Halka imlecin üzerinde ortalanır; ortası, çember ve
/// <see cref="MarginPx"/> payı imlecin monitörünün çalışma alanına sığacak biçimde kaydırılır (görev çubuğunun altına
/// girmez). Pencere (çember + nişan alanı) monitörün içinde tutulur; bu yüzden halkanın ortası pencerenin ortası
/// olmayabilir: çizim <see cref="Result.OffsetX"/>/<see cref="Result.OffsetY"/>'yi kullanır. Sol monitördeki eksi x
/// koordinatları da doğru hesaplanır (Kando clampToMonitor'un monitör başına uyarlanmışı).
/// </summary>
public static class RingPlacement
{
    /// <summary>Çember ile çalışma alanı kenarı arasındaki en az boşluk.</summary>
    public const int MarginPx = 8;

    /// <param name="Left">Pencerenin sol üstü.</param>
    /// <param name="CenterX">Halkanın ortası (ekran).</param>
    public readonly record struct Result(int Left, int Top, int CenterX, int CenterY)
    {
        /// <summary>Halkanın ortası, pencerenin sol üstüne göre.</summary>
        public int OffsetX => CenterX - Left;
        public int OffsetY => CenterY - Top;
    }

    /// <param name="monitor">İmlecin monitörü.</param>
    /// <param name="work">O monitörün çalışma alanı (görev çubuğu hariç).</param>
    /// <param name="cursorX">İmleç (ya da NESTDESK_RING_AT).</param>
    /// <param name="radiusPx">Çemberin dış yarıçapı.</param>
    /// <param name="windowWidth">Pencerenin genişliği (çember + nişan alanı).</param>
    public static Result Place(Box monitor, Box work, int cursorX, int cursorY, int radiusPx, int windowWidth, int windowHeight)
    {
        var reach = Math.Max(0, radiusPx) + MarginPx;
        var cx = Fit(cursorX, work.Left, work.Right, reach);
        var cy = Fit(cursorY, work.Top, work.Bottom, reach);

        // Pencere ortalanır, sonra monitörün içine alınır (sığmıyorsa sol/üst kenara).
        var left = Math.Clamp(cx - windowWidth / 2, monitor.Left, Math.Max(monitor.Left, monitor.Right - windowWidth));
        var top = Math.Clamp(cy - windowHeight / 2, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - windowHeight));
        return new Result(left, top, cx, cy);
    }

    /// <summary>Değer [min + reach, max - reach] aralığına; aralık çemberden darsa alanın ortası.</summary>
    private static int Fit(int value, int min, int max, int reach)
    {
        if (max - min < 2 * reach) return FloorHalf(min + max);
        return Math.Clamp(value, min + reach, max - reach);
    }

    // Eksi koordinatta da aşağı yuvarlar ((-1919 + 0) / 2 = -960, sıfıra doğru değil).
    private static int FloorHalf(int sum) => (int)Math.Floor(sum / 2.0);
}
