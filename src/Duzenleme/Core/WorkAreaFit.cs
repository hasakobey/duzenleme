namespace Duzenleme.Core;

/// <summary>
/// Widget'ı monitörün çalışma alanına (görev çubuğu hariç) sığdırma kuralları, fiziksel pikselle. Pencere dikdörtgeni
/// kartın çevresindeki saydam gölge payını (<c>margin</c>) da içerir; sığması gereken karttır.
/// Kayıtlı yükseklik başka bir ekranda (daha büyük monitör, %100 ölçek, başka bilgisayar) seçilmiş olabilir: görünen
/// yükseklik kısılır, kayıtlı olana dokunulmaz.
/// </summary>
public static class WorkAreaFit
{
    /// <summary>Kartı çalışma alanının tamamını kaplayan pencerenin yüksekliği: çalışma alanı + iki gölge payı.</summary>
    public static int MaxWindowHeight(Box work, int margin) => Math.Max(0, work.Height + 2 * margin);

    /// <summary>
    /// Kartı alttan taşıyorsa yukarı kaydırılmış pencere üst kenarı (kartın üstü çalışma alanının üstünü geçmez);
    /// taşmıyorsa <paramref name="top"/> aynen döner. Aşağı hiç kaydırılmaz.
    /// </summary>
    public static int KeepBottomInside(int top, int windowHeight, Box work, int margin)
    {
        var cardBottom = top + windowHeight - margin;
        if (cardBottom <= work.Bottom) return top;
        return Math.Min(top, Math.Max(work.Bottom + margin - windowHeight, work.Top - margin));
    }

    /// <summary>
    /// Kenardan boyutlandırırken kart çalışma alanının üstünden/altından dışarı büyümez. Başlangıçta zaten dışarıdaysa
    /// o kenar geri çekilmez (yalnızca daha da dışarı gidemez). Kutular kart dikdörtgenidir (gölge payı hariç).
    /// </summary>
    public static Box ClampResize(Box start, Box proposed, bool top, bool bottom, Box work)
    {
        var (t, b) = (proposed.Top, proposed.Bottom);
        if (bottom) b = Math.Min(b, Math.Max(work.Bottom, start.Bottom));
        if (top) t = Math.Max(t, Math.Min(work.Top, start.Top));
        return new Box(proposed.Left, t, proposed.Right, b);
    }
}
