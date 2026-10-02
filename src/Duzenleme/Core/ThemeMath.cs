using System.Globalization;

namespace Duzenleme.Core;

/// <summary>
/// Widget renkleri için saf hesaplar (UI'sız, testli): "#RRGGBB" okuma/yazma, WCAG karşıtlığı, okunurluk için açıp
/// koyulaştırma (<see cref="EnsureContrast"/>) ve bir rengin 2.0/2.1'deki en yakın vurgu rengi (<see cref="NearestAccent"/>).
/// Renkler ARGB <c>uint</c>'tir (0xAARRGGBB).
/// </summary>
public static class ThemeMath
{
    /// <summary>Widget'ın vurgu rengiyle zemini arasında aranan en az karşıtlık (simge, kenarlık, ilerleme çubuğu).</summary>
    public const double AccentContrast = 3.0;

    /// <summary>Yazı ile zemini arasında aranan en az karşıtlık (WCAG AA, olağan yazı).</summary>
    public const double TextContrast = 4.5;

    /// <summary>
    /// Vurgu renginin üstündeki yazı: siyah ya da beyaz. Saf siyahla ikisinden biri her renkte en az 4,58:1 verir (koyu gri
    /// orta tonlarda 4,5'in altında kalırdı).
    /// </summary>
    public const uint DarkInk = 0xFF000000, WhiteInk = 0xFFFFFFFF;

    // ---------------------------------------------------------------- metin

    /// <summary>"#RRGGBB" (ya da "#AARRGGBB") okur; "#" isteğe bağlı, büyük/küçük harf fark etmez. 6 haneli renk opaktır.</summary>
    public static bool TryParse(string? text, out uint argb)
    {
        argb = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length is not (6 or 8) || !s.All(Uri.IsHexDigit)) return false;
        var value = uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        argb = s.Length == 6 ? 0xFF000000 | value : value;
        return true;
    }

    /// <summary>Kaydedilecek biçim: yalnızca opak "#RRGGBB" (büyük harf); okunamayan ya da saydam renk null.</summary>
    public static string? NormalizeHex(string? text) =>
        TryParse(text, out var argb) && argb >> 24 == 0xFF ? ToHex(argb) : null;

    /// <summary>"#RRGGBB" (saydamlık yazılmaz).</summary>
    public static string ToHex(uint argb) => "#" + (argb & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------- karşıtlık

    private static double Channel(uint c)
    {
        var v = c / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    /// <summary>WCAG göreli parlaklığı (0 siyah, 1 beyaz); saydamlık yok sayılır.</summary>
    public static double Luminance(uint argb) =>
        0.2126 * Channel((argb >> 16) & 0xFF) + 0.7152 * Channel((argb >> 8) & 0xFF) + 0.0722 * Channel(argb & 0xFF);

    /// <summary>WCAG karşıtlık oranı (1–21); iki renk de opak sayılır.</summary>
    public static double Contrast(uint a, uint b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    /// <summary><paramref name="over"/>'ı <paramref name="under"/>'ın üstüne saydamlığıyla karıştırır (sonuç opak).</summary>
    public static uint Composite(uint over, uint under)
    {
        var a = (over >> 24) / 255.0;
        byte Mix(int shift) => (byte)Math.Round(((over >> shift) & 0xFF) * a + ((under >> shift) & 0xFF) * (1 - a));
        return 0xFF000000 | (uint)Mix(16) << 16 | (uint)Mix(8) << 8 | Mix(0);
    }

    /// <summary>Saydam zemin koyu cam gibi okunur: karşıtlık siyahın üstündeki hâline göre hesaplanır.</summary>
    public static uint Opaque(uint argb) => argb >> 24 == 0xFF ? argb : Composite(argb, 0xFF000000);

    /// <summary>
    /// Rengi, bütün zeminlere karşı en az <paramref name="min"/> karşıtlığa varana dek açar (koyu zeminde) ya da koyulaştırır
    /// (açık zeminde); tonu ve doygunluğu korunur. Zaten yetiyorsa aynen döner. Hiçbir yönde yetmezse en iyisi döner.
    /// </summary>
    public static uint EnsureContrast(uint color, IReadOnlyList<uint> backgrounds, double min)
    {
        color |= 0xFF000000;
        // Zeminlerin parlaklığı bir kez hesaplanır (zemin listesi uzun olabilir: halkanın katmanları).
        var grounds = backgrounds.Select(b => Luminance(Opaque(b))).ToArray();
        if (grounds.Length == 0) return color;
        double Worst(uint c)
        {
            var lc = Luminance(c);
            var worst = double.MaxValue;
            foreach (var lb in grounds) worst = Math.Min(worst, (Math.Max(lc, lb) + 0.05) / (Math.Min(lc, lb) + 0.05));
            return worst;
        }
        if (Worst(color) >= min) return color;

        var (h, s, l) = ToHsl(color);
        var darkBackground = grounds.Average() < 0.18;
        uint best = color;
        var bestScore = Worst(color);
        foreach (var lighten in darkBackground ? new[] { true, false } : [false, true])
        {
            for (var step = 1; step <= 50; step++)
            {
                var lightness = Math.Clamp(l + (lighten ? step : -step) * 0.02, 0, 1);
                var candidate = FromHsl(h, s, lightness);
                var score = Worst(candidate);
                if (score >= min) return candidate;
                if (score > bestScore) (best, bestScore) = (candidate, score);
                if (lightness is 0 or 1) break;
            }
        }
        return best;
    }

    /// <summary>Vurgu renginin üstündeki yazı: beyaz ya da koyu, hangisi daha okunursa.</summary>
    public static uint TextOn(uint background) =>
        Contrast(WhiteInk, Opaque(background)) >= Contrast(DarkInk, Opaque(background)) ? WhiteInk : DarkInk;

    // ---------------------------------------------------------------- vurgu renkleri

    /// <summary>2.0'dan beri kayıtlı beş vurgu rengi: koyu zeminde açık ton, açık zeminde koyu ton.</summary>
    public static uint AccentArgb(WidgetAccent accent, bool onLight) => accent switch
    {
        WidgetAccent.Blue => onLight ? 0xFF2563EB : 0xFF93C5FD,
        WidgetAccent.Green => onLight ? 0xFF16A34A : 0xFF86EFAC,
        WidgetAccent.Orange => onLight ? 0xFFEA580C : 0xFFFDBA74,
        WidgetAccent.Pink => onLight ? 0xFFDB2777 : 0xFFF9A8D4,
        _ => onLight ? 0xFF7C3AED : 0xFFC4B5FD,
    };

    /// <summary>
    /// Rengin tonuna en yakın kayıtlı vurgu rengi: 2.1 özel rengi ya da temanın rengini tanımaz, bunu gösterir. Gri tonlar
    /// (doygunluk çok düşük) varsayılan mor olur.
    /// </summary>
    public static WidgetAccent NearestAccent(uint argb)
    {
        var (h, s, _) = ToHsl(argb);
        if (s < 0.12) return WidgetAccent.Violet;
        return Enum.GetValues<WidgetAccent>()
            .OrderBy(a => HueDistance(h, ToHsl(AccentArgb(a, onLight: true)).H))
            .First();
    }

    private static double HueDistance(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }

    // ---------------------------------------------------------------- HSL

    /// <summary>Ton (0–360), doygunluk ve açıklık (0–1).</summary>
    public static (double H, double S, double L) ToHsl(uint argb)
    {
        double r = ((argb >> 16) & 0xFF) / 255.0, g = ((argb >> 8) & 0xFF) / 255.0, b = (argb & 0xFF) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (max - min < 1e-9) return (0, 0, l);
        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h;
        if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g) h = (b - r) / d + 2;
        else h = (r - g) / d + 4;
        return (h * 60, s, l);
    }

    /// <summary>HSL'den opak ARGB.</summary>
    public static uint FromHsl(double h, double s, double l)
    {
        double r, g, b;
        if (s <= 0) r = g = b = l;
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            var hk = h / 360;
            r = Hue(p, q, hk + 1.0 / 3);
            g = Hue(p, q, hk);
            b = Hue(p, q, hk - 1.0 / 3);
        }
        static uint Byte(double v) => (uint)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return 0xFF000000 | Byte(r) << 16 | Byte(g) << 8 | Byte(b);
    }

    private static double Hue(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
