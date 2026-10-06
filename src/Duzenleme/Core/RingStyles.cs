namespace Duzenleme.Core;

/// <summary>
/// Halka menünün biçimi (<see cref="WidgetConfig.RingStyle"/>): yalnızca çizimin biçimini ve süslerini seçer, renkleri değil
/// (renkler hep widget'ın paletinden, <c>WidgetPalette.Resolve</c>). Saf tablo; isabet testi ve yerleşimin açıları her biçimde
/// aynıdır (<see cref="RingGeometry.HitTest"/>), yalnızca <see cref="Labels"/> halkayı adlara yer açacak kadar büyütür.
/// </summary>
/// <param name="Id">Ayarda yazılan kimlik (ASCII); Klasik'te null (yazılmaz).</param>
/// <param name="Name">Görünen ad, <see cref="L.N"/> anahtarı (<see cref="Label"/> çevirir).</param>
/// <param name="Wedges">Dilimler: boştaki dilim dolgusu, vurgulanınca dilimin degradesi.</param>
/// <param name="Separators">Dilimlerin arasında ince çizgiler.</param>
/// <param name="Rims">Dilimlerin dış kenarında yaylar (vurgulanınca vurgu renginde).</param>
/// <param name="Track">Öğeler ince bir yörüngede durur (öğelerin arasında yaylar; simgenin altından geçmez).</param>
/// <param name="Labels">Her öğenin altında adı yazan küçük etiket.</param>
/// <param name="Glow">Vurgu renginde ışıltı: dış kenarda degrade hale, öğelerin kenarı vurgu renginde, vurgulananın çevresinde
/// hale, dilim sınırlarında noktalar. Hepsi degrade ve biçimdir; efekt yok.</param>
/// <param name="Pointer">Vurgulanan öğeye ortadan ince bir gösterge çizgisi (Kando'daki gibi).</param>
public sealed record RingStyle(string? Id, string Name, bool Wedges, bool Separators, bool Rims, bool Track, bool Labels, bool Glow, bool Pointer)
{
    /// <summary>Görünen ad (arayüz dilinde).</summary>
    public string Label => L.Dyn(Name);
}

/// <summary>
/// Halka biçimleri tablosu. Klasik (null: 2.2'nin ilk çizimi), Daire (dilimsiz, yörüngede daireler), Etiketli (öğelerin altında
/// adları), Işıltı (vurgu renginde hale ve kenarlar). Tanınmayan kimlik (daha yeni bir sürümün) Klasik çizilir ve ayarda korunur;
/// yüksek karşıtlıkta her biçim Klasik'e iner (<see cref="Effective"/>).
/// </summary>
public static class RingStyles
{
    public const string Orbit = "orbit", Labeled = "labels", Glow = "glow";

    public static RingStyle Classic { get; } =
        new(null, L.N("Klasik"), Wedges: true, Separators: true, Rims: true, Track: false, Labels: false, Glow: false, Pointer: false);

    /// <summary>Panelde gösterilen sırayla.</summary>
    public static IReadOnlyList<RingStyle> All { get; } =
    [
        Classic,
        new(Orbit, L.N("Daire"), Wedges: false, Separators: false, Rims: false, Track: true, Labels: false, Glow: false, Pointer: true),
        new(Labeled, L.N("Etiketli"), Wedges: false, Separators: false, Rims: false, Track: false, Labels: true, Glow: false, Pointer: true),
        new(Glow, L.N("Işıltı"), Wedges: true, Separators: false, Rims: true, Track: false, Labels: false, Glow: true, Pointer: false),
    ];

    /// <summary>Kimliğin biçimi; null, boş ya da tanınmayan kimlik Klasik.</summary>
    public static RingStyle For(string? id) => All.FirstOrDefault(s => s.Id is not null && s.Id == id) ?? Classic;

    /// <summary>Çizilecek biçim: yüksek karşıtlıkta her biçim Klasik (sistem renkleriyle sade çizim).</summary>
    public static RingStyle Effective(string? id, bool highContrast) => highContrast ? Classic : For(id);

    /// <summary>Paneldeki seçili kart: tanınan kimlik, yoksa Klasik (tanınmayan kimlik ayarda kalır).</summary>
    public static string CurrentId(WidgetConfig c) => For(c.RingStyle).Id ?? ClassicId;

    /// <summary>Klasik kartın kimliği (AutomationId'de; ayara yazılmaz).</summary>
    public const string ClassicId = "classic";

    /// <summary>Kartı seçer: Klasik alanı siler (yazılmaz), öbürleri kimliğini yazar. Tanınmayan kimlik bir şeyi değiştirmez (false).</summary>
    public static bool Apply(WidgetConfig c, string id)
    {
        if (id == ClassicId)
        {
            c.RingStyle = null;
            return true;
        }
        if (All.All(s => s.Id != id)) return false;
        c.RingStyle = id;
        return true;
    }

    // ---------------------------------------------------------------- etiket ölçüleri (DIP; saf)

    /// <summary>Etiketin yüksekliği.</summary>
    public const double LabelHeight = 20;

    /// <summary>Öğe dairesinin kenarı ile etiket arasındaki boşluk.</summary>
    public const double LabelGap = 3;

    /// <summary>Etiketin genişliği: öğe dairesinin 1,45 katı, 68–92 DIP arası (uzun ad üç noktayla kısalır).</summary>
    public static double LabelWidth(double itemDip) => Math.Clamp(itemDip * 1.45, 68, 92);

    /// <summary>
    /// Etiket dışa bakar: halkanın üst yarısındaki öğede dairenin üstünde, öbürlerinde (yanlardakiler dahil) altında; hiçbiri
    /// ortaya doğru sarkmaz, 1–9 rozetiyle (ortaya bakan kenarda) çakışmaz.
    /// </summary>
    public static bool LabelAbove(double angle)
    {
        var (_, y) = RingGeometry.Direction(angle);
        return y < -0.2;
    }

    /// <summary>
    /// Etiketin kutusu, öğe merkezine göre (DIP): dışa bakan yanda, ortalı.
    /// </summary>
    public static (double Left, double Top, double Width, double Height) LabelBox(double angle, double itemDip)
    {
        var width = LabelWidth(itemDip);
        var top = LabelAbove(angle) ? -itemDip / 2 - LabelGap - LabelHeight : itemDip / 2 + LabelGap;
        return (-width / 2, top, width, LabelHeight);
    }

    /// <summary>Etiketler arasında ve etiketle başka öğenin dairesi arasında en az bu kadar boşluk (DIP).</summary>
    public const double LabelClearance = 2;

    /// <summary>
    /// Etiketli halkanın yerleşimi: öğe merkezlerinin yarıçapı (en az <paramref name="minRadius"/>), etiketlerin halkanın
    /// ortasına en yakın uzaklığı (orta daire bunun içinde kalır) ve en uzak köşesi (dış disk bunu kapsar). Yarıçap, eşit aralıklı
    /// <paramref name="places"/> yerin her dönüşünde (alt halkanın geri yeri de bir yer sayılır) hiçbir etiket ne öbür etiketlere
    /// ne başka öğenin dairesine binene dek 2 DIP adımlarla büyür.
    /// </summary>
    public static (double Radius, double Inner, double Reach) LabeledLayout(int places, double itemDip, double minRadius)
    {
        var radius = Math.Max(minRadius, RingGeometry.Radius(places, LabelWidth(itemDip), 0));
        for (var step = 0; ; step++)
        {
            var (fits, inner, reach) = Check(Math.Max(1, places), itemDip, radius);
            if (fits || step >= 200) return (radius, inner, reach);
            radius += 2;
        }
    }

    private static (bool Fits, double Inner, double Reach) Check(int places, double itemDip, double radius)
    {
        var r = itemDip / 2;
        var spacing = 360.0 / places;
        var fits = true;
        double inner = double.MaxValue, reach = 0;
        var boxes = new (double L, double T, double R, double B)[places];
        var centers = new (double X, double Y)[places];
        for (var rotation = 0.0; rotation < spacing; rotation += Math.Min(3, spacing))
        {
            for (var i = 0; i < places; i++)
            {
                var angle = rotation + i * spacing;
                var (x, y) = RingGeometry.Direction(angle, radius);
                var (left, top, width, height) = LabelBox(angle, itemDip);
                centers[i] = (x, y);
                boxes[i] = (x + left, y + top, x + left + width, y + top + height);
                inner = Math.Min(inner, DistanceTo(boxes[i], 0, 0));
                foreach (var (cx, cy) in new[] { (boxes[i].L, boxes[i].T), (boxes[i].R, boxes[i].T), (boxes[i].L, boxes[i].B), (boxes[i].R, boxes[i].B) })
                    reach = Math.Max(reach, Math.Sqrt(cx * cx + cy * cy));
            }
            for (var i = 0; i < places && fits; i++)
                for (var j = 0; j < places && fits; j++)
                {
                    if (i == j) continue;
                    var (a, b) = (boxes[i], boxes[j]);
                    if (a.L < b.R + LabelClearance && b.L < a.R + LabelClearance && a.T < b.B + LabelClearance && b.T < a.B + LabelClearance) fits = false;
                    else if (DistanceTo(a, centers[j].X, centers[j].Y) < r + LabelClearance) fits = false;
                }
        }
        return (fits, inner, reach);
    }

    private static double DistanceTo((double L, double T, double R, double B) box, double x, double y)
    {
        var dx = Math.Max(Math.Max(box.L - x, 0), x - box.R);
        var dy = Math.Max(Math.Max(box.T - y, 0), y - box.B);
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
