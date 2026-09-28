namespace Duzenleme.Core;

/// <summary>
/// Widget kartının efektsiz gölgesinin geçiş durakları. Gölge, kartın bulanıklaştırılmış (Gauss) silueti gibi kenardan
/// dışarı doğru azalır: kenardan x uzaklıktaki koyuluk opaklık·Φ(−x/σ). Kart, WPF'in DropShadowEffect'i yerine bu
/// duraklarla çizilen 4 köşe (dairesel) + 4 kenar (doğrusal) degrade ve düz bir orta dolguyla gölgelenir: yazılımla
/// çizilen katmanlı pencerede efekt, kartın içindeki her küçük değişiklikte (fare üstüne gelme, saniye, imleç) bütün kartı
/// yeniden bulanıklaştırıyordu; degrade dikdörtgenler ise bir kez çizilir.
/// </summary>
public static class ShadowGradient
{
    /// <summary>
    /// Gölgenin köşe yuvarlaklığı en az bu kadar σ: bulanık keskin köşe de yuvarlak görünür. Küçük köşe yarıçapında
    /// (Köşeli, Hafif yuvarlak) köşe hücresi bulanıklığın iç kısmını da kapsasın; yoksa orta dolguyla arasında basamak kalır.
    /// </summary>
    public const double MinRadiusInSigmas = 2.5;

    /// <summary>Φ(−z): Gauss'la bulanıklaştırılmış bir kenarın, kenardan z·σ dışarıda kalan koyuluk oranı (0,5 kenarda).</summary>
    public static double Tail(double z)
    {
        // Abramowitz–Stegun 7.1.26 erf yaklaşımı (hata < 1,5e-7).
        var x = z / Math.Sqrt(2);
        var t = 1 / (1 + 0.3275911 * Math.Abs(x));
        var erf = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return 0.5 * (1 - (x < 0 ? -erf : erf));
    }

    /// <summary>Bulanıklık payından (gölgenin kart kenarından taşma genişliği) σ: payın üçte biri.</summary>
    public static double Sigma(double pad) => pad / 3;

    /// <summary>Kartın köşe yarıçapından gölgenin köşe yarıçapı (en az <see cref="MinRadiusInSigmas"/>·σ).</summary>
    public static double ShadowRadius(double cardRadius, double pad) => Math.Max(Math.Max(0, cardRadius), MinRadiusInSigmas * Sigma(pad));

    /// <summary>
    /// Geçiş durakları (konum 0–1, koyuluk 0–1). Hücrenin boyu pad + radius'tur.
    /// <paramref name="radial"/>: köşe hücresi, konum köşe yayının merkezinden dışa doğru ölçülür (0 merkez, 1 dış sınır).
    /// Değilse kenar hücresi, konum dış sınırdan içe doğru ölçülür (0 dış sınır, 1 iç sınır).
    /// </summary>
    public static IReadOnlyList<(double Offset, double Alpha)> Stops(double opacity, double pad, double radius, bool radial)
    {
        if (pad <= 0) throw new ArgumentOutOfRangeException(nameof(pad));
        radius = Math.Max(0, radius);
        var sigma = Sigma(pad);
        var total = pad + radius;
        var step = Math.Max(1, sigma / 2);
        var stops = new List<(double, double)>();
        // x: kenardan dışarı uzaklık; −radius (yayın merkezi / iç sınır) ile +pad (dış sınır, gölge bitti) arası.
        for (var i = 0; ; i++)
        {
            var x = Math.Min(-radius + i * step, pad);
            var alpha = Math.Clamp(opacity * Tail(x / sigma), 0, 1);
            var offset = radial ? (radius + x) / total : (pad - x) / total;
            stops.Add((Math.Clamp(offset, 0, 1), alpha));
            if (x >= pad) break;
        }
        if (!radial) stops.Reverse(); // doğrusal fırça 0'dan 1'e sıralı dursun
        return stops;
    }
}
