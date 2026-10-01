// Halka geometrisinin bir bölümü Kando'dan C#'a taşındı:
//   computeItemAngles, computeItemWedges, isAngleBetween, normalizeConsequtiveAngles, getAngle, getDirection
//     ← src/common/math/index.ts
//   changeHoveredItem (ok tuşlarıyla gezinme) ← src/menu-renderer/menu.ts
// Kando — the cross-platform pie menu. Copyright (c) Simon Schneegans <code@simonschneegans.de>. MIT License.
// https://github.com/kando-menu/kando  (lisans metni: THIRD-PARTY-NOTICES.txt)

namespace Duzenleme.Core;

/// <summary>
/// Halka menünün saf geometrisi (testlenir; arayüzden bağımsız). Açılar derece: 0° üstte, saat yönünde artar (90° sağ,
/// 180° alt); WPF'in y ekseni aşağı baktığından Kando'nun DOM koordinatlarıyla aynıdır. İsabet testi ve klavye gezinmesi
/// yalnızca açı hesabıdır: halka büyüyüp küçülürken (animasyon) de doğru çalışır, girdi animasyonu beklemez.
/// </summary>
public static class RingGeometry
{
    /// <summary>Üst öğenin (geri) dilimi için <see cref="Hit.Index"/> ve <see cref="Navigate"/> değeri.</summary>
    public const int ParentIndex = -1;

    /// <summary>Öğeler arası boşluk (DIP; <see cref="Radius"/>).</summary>
    public const double ItemGapDip = 8;

    /// <summary>Tuşu bırakınca seçimde imlecin en az bu kadar (DIP) kayması gerekir; yoksa tıklama kipinde kalınır.</summary>
    public const double DragThresholdDip = 15;

    /// <summary>Orta ölü bölgenin yarıçapa oranı: ortaya yakın imleç hiçbir öğeyi seçmez.</summary>
    public const double DeadZoneRatio = 0.45;

    /// <summary>Bir dilim: <see cref="Start"/> &lt; <see cref="End"/>; Start eksi, End 360'tan büyük olabilir.</summary>
    public readonly record struct Wedge(double Start, double End)
    {
        public double Center => (Start + End) / 2;
        public double Sweep => End - Start;
        public bool Contains(double angle) => IsAngleBetween(angle, Start, End);
    }

    /// <summary>Öğelerin dilimleri (öğe sırasıyla) ve varsa üst öğenin (geri) dilimi.</summary>
    public sealed record WedgeLayout(IReadOnlyList<Wedge> Items, Wedge? Parent);

    public enum HitKind { None, Center, Item, Parent }

    /// <summary>İsabet: <see cref="HitKind.Item"/> ise <see cref="Index"/> öğenin sırası, Parent'ta <see cref="ParentIndex"/>.</summary>
    public readonly record struct Hit(HitKind Kind, int Index = 0)
    {
        public static Hit None => new(HitKind.None, 0);
        public static Hit Center => new(HitKind.Center, 0);
    }

    /// <summary>Ok tuşu (kalıcı değil).</summary>
    public enum Arrow { Left, Up, Right, Down }

    // ---------------------------------------------------------------- temel açı işlemleri

    /// <summary>Vektörün açısı (0° üst, saat yönü; y aşağı). Sonuç [0, 360).</summary>
    public static double AngleOf(double dx, double dy)
    {
        var angle = (Math.Atan2(dy, dx) * 180 / Math.PI + 90) % 360;
        return angle < 0 ? angle + 360 : angle;
    }

    /// <summary>Açının yönündeki vektör (0° üst, saat yönü; y aşağı).</summary>
    public static (double X, double Y) Direction(double angle, double length = 1)
    {
        var radians = (angle - 90) * Math.PI / 180;
        return (Math.Cos(radians) * length, Math.Sin(radians) * length);
    }

    /// <summary>İki açı arasındaki en kısa fark, 0–180°.</summary>
    public static double AngularDifference(double a, double b)
    {
        var diff = Math.Abs(Mod360(a) - Mod360(b));
        return Math.Min(diff, 360 - diff);
    }

    /// <summary>Açı (start, end] aralığında mı; açılar eksi ya da 360'tan büyük olabilir (Kando isAngleBetween).</summary>
    public static bool IsAngleBetween(double angle, double start, double end) =>
        (angle > start && angle <= end) || (angle - 360 > start && angle - 360 <= end) || (angle + 360 > start && angle + 360 <= end);

    private static double Mod360(double a)
    {
        var m = a % 360;
        return m < 0 ? m + 360 : m;
    }

    private static double EquivalentSmallerThan(double angle, double than) => angle + Math.Floor((than - angle) / 360) * 360;

    private static double EquivalentLargerThan(double angle, double than) => angle + Math.Ceiling((than - angle) / 360) * 360;

    /// <summary>start ≤ center ≤ end olacak biçimde 360'ın katlarıyla kaydırır (Kando normalizeConsequtiveAngles).</summary>
    private static (double Start, double Center, double End) Consecutive(double start, double center, double end)
    {
        center %= 360;
        return (EquivalentSmallerThan(start, center), center, EquivalentLargerThan(end, center));
    }

    // ---------------------------------------------------------------- açılar ve dilimler

    /// <summary>
    /// <paramref name="count"/> öğenin yön açıları (Kando computeItemAngles). Üst öğe yoksa ilk öğe tam üstte (0°) ve hepsi
    /// eşit aralıklı; varsa üst öğenin yönüne bir öğelik boşluk bırakılır. <paramref name="fixedAngles"/> (isteğe bağlı,
    /// öğe başına; null = serbest) artan sırada olmalı; azalan açı yok sayılır.
    /// </summary>
    public static double[] Angles(int count, double? parentAngle = null, IReadOnlyList<double?>? fixedAngles = null)
    {
        if (count <= 0) return [];
        var angles = new double[count];

        var fixeds = new List<(double Angle, int Index)>();
        for (var i = 0; i < count && fixedAngles is not null && i < fixedAngles.Count; i++)
            if (fixedAngles[i] is { } a && a >= 0) fixeds.Add((a, i));

        // Üst öğe bir sabit açıyla çakışmasın.
        if (parentAngle is { } p0)
            for (var i = 0; i < fixeds.Count; i++)
                if (Math.Abs(fixeds[i].Angle - p0) < 0.0001) fixeds[i] = (fixeds[i].Angle + 0.1, fixeds[i].Index);

        // Sabit açılar artarak gitmeli; büyükten sonra gelen küçük açı yok sayılır.
        for (var i = 0; i < fixeds.Count - 1;)
        {
            if (fixeds[i].Angle > fixeds[i + 1].Angle) fixeds.RemoveAt(i + 1);
            else i++;
        }

        if (fixeds.Count == 0)
        {
            double first = 0;
            if (parentAngle is { } p)
            {
                var wedgeSize = 360.0 / (count + 1);
                var min = 360.0;
                for (var i = 0; i < count; i++) min = Math.Min(min, (p + (i + 1) * wedgeSize) % 360);
                first = min;
            }
            fixeds.Add((first, 0));
            angles[0] = first;
        }

        double? parent = parentAngle;
        for (var i = 0; i < fixeds.Count; i++)
        {
            var beginIndex = fixeds[i].Index;
            var beginAngle = fixeds[i].Angle;
            var endIndex = fixeds[(i + 1) % fixeds.Count].Index;
            var endAngle = fixeds[(i + 1) % fixeds.Count].Angle;

            angles[beginIndex] = beginAngle;
            if (endAngle <= beginAngle) endAngle += 360;

            var itemCount = (endIndex - beginIndex - 1 + count) % count;
            var parentInWedge = false;
            if (parent is { } pa)
            {
                // Üst öğe bu aralıkta ama bir tur geride olabilir.
                if (pa < beginAngle) parent = pa += 360;
                parentInWedge = pa > beginAngle && pa < endAngle;
                if (parentInWedge) itemCount++;
            }

            var gap = (endAngle - beginAngle) / (itemCount + 1);
            var index = (beginIndex + 1) % count;
            var step = 1;
            var parentGapRequired = parentInWedge;
            while (index != endIndex)
            {
                var angle = beginAngle + gap * step;
                if (parentGapRequired && angle + gap / 2 - parent!.Value > 0)
                {
                    step++;
                    angle = beginAngle + gap * step;
                    parentGapRequired = false;
                }
                angles[index] = angle % 360;
                index = (index + 1) % count;
                step++;
            }
        }
        return angles;
    }

    /// <summary>
    /// Öğe dilimleri (Kando computeItemWedges): her dilim komşu öğelerin açılarının ortasından ortasına uzanır; üst öğe
    /// varsa onun yönünde ayrı bir dilim kalır. Dilimler 360°'yi boşluksuz ve üst üste binmeden kaplar.
    /// </summary>
    public static WedgeLayout Wedges(IReadOnlyList<double> angles, double? parentAngle = null)
    {
        if (angles.Count == 0)
            return parentAngle is { } only ? new WedgeLayout([], new Wedge(only - 180, only + 180)) : new WedgeLayout([], null);

        if (angles.Count == 1 && parentAngle is null) return new WedgeLayout([new Wedge(0, 360)], null);

        if (angles.Count == 1 && parentAngle is { } p1)
        {
            var (start, center, end) = Consecutive(p1, angles[0], p1 + 360);
            (start, end) = Scale(start, center, end);
            return new WedgeLayout([new Wedge(start, end)], new Wedge(end, start + 360));
        }

        var items = new List<Wedge>(angles.Count);
        double? parentStart = null, parentEnd = null;
        for (var i = 0; i < angles.Count; i++)
        {
            var (start, center, end) = Consecutive(angles[(i + angles.Count - 1) % angles.Count], angles[i], angles[(i + 1) % angles.Count]);
            if (parentAngle is { } pa)
            {
                if (IsAngleBetween(pa, start, center)) parentStart = start;
                else if (IsAngleBetween(pa, center, end)) parentEnd = end;
                if (IsAngleBetween(pa, start, center)) start = pa;
                if (IsAngleBetween(pa, center, end)) end = pa;
                (start, center, end) = Consecutive(start, center, end);
            }
            (start, end) = Scale(start, center, end);
            items.Add(new Wedge(start, end));
        }

        if (parentAngle is { } pp && parentStart is { } ps && parentEnd is { } pe)
        {
            var (start, center, end) = Consecutive(ps, pp, pe);
            (start, end) = Scale(start, center, end);
            return new WedgeLayout(items, new Wedge(start, end));
        }
        return new WedgeLayout(items, null);
    }

    private static (double Start, double End) Scale(double start, double center, double end) =>
        (center - (center - start) * 0.5, center + (end - center) * 0.5);

    // ---------------------------------------------------------------- isabet, gezinme, bırakma, boyut

    /// <summary>
    /// İmlecin (merkeze göre <paramref name="dx"/>, <paramref name="dy"/>) üzerinde olduğu öğe. Ölü bölgenin içi Center;
    /// <paramref name="maxRadius"/> &gt; 0 ise o uzaklığın dışı None; yoksa açının düştüğü öğe dilimi, o da yoksa üst öğe.
    /// </summary>
    public static Hit HitTest(double dx, double dy, WedgeLayout wedges, double deadZone, double maxRadius = 0)
    {
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < deadZone) return Hit.Center;
        if (maxRadius > 0 && distance > maxRadius) return Hit.None;
        var angle = AngleOf(dx, dy);
        for (var i = 0; i < wedges.Items.Count; i++)
            if (wedges.Items[i].Contains(angle)) return new Hit(HitKind.Item, i);
        return wedges.Parent is not null ? new Hit(HitKind.Parent, ParentIndex) : Hit.None;
    }

    /// <summary>
    /// Ok tuşuyla vurgulanacak öğe (Kando changeHoveredItem). <paramref name="current"/>: vurgulanan öğe (0…n-1,
    /// <see cref="ParentIndex"/>) ya da null (hiçbiri/orta). Önce yöndeki en uç öğe aranır (45° koni); hiçbir şey
    /// vurgulanmamışsa o seçilir. Vurgulanan öğe yönün tersindeyse (20°) en uç öğeye atlanır; değilse yöne yaklaştıran
    /// komşuya (saat yönü/tersi) geçilir. Değişiklik yoksa <paramref name="current"/> döner.
    /// </summary>
    public static int? Navigate(IReadOnlyList<double> angles, int? current, Arrow arrow, double? parentAngle = null)
    {
        var direction = arrow switch
        {
            Arrow.Left => (X: -1.0, Y: 0.0),
            Arrow.Up => (X: 0.0, Y: -1.0),
            Arrow.Right => (X: 1.0, Y: 0.0),
            _ => (X: 0.0, Y: 1.0),
        };

        var candidates = new List<(int Index, double Angle, double Dot)>();
        for (var i = 0; i < angles.Count; i++) candidates.Add((i, angles[i], Dot(direction, Direction(angles[i]))));
        if (parentAngle is { } p) candidates.Add((ParentIndex, Mod360(p), Dot(direction, Direction(p))));
        if (candidates.Count == 0) return null;
        // Artık olmayan öğe (liste değişti) vurgulanmamış sayılır.
        if (current is { } stale && !candidates.Any(c => c.Index == stale)) current = null;

        (int Index, double Angle, double Dot)? extreme = null;
        var cone = Math.Cos(45 * Math.PI / 180);
        foreach (var c in candidates)
            if (c.Dot > cone && (extreme is null || c.Dot > extreme.Value.Dot)) extreme = c;

        if (extreme is { } e0 && current == e0.Index) return current;

        if (current is not { } hovered) return extreme?.Index;

        var hoveredAngle = candidates.First(c => c.Index == hovered).Angle;
        var hoveredDir = Direction(hoveredAngle);

        // Yönün tersindeki öğe vurgulanmışsa (ör. en soldaki, sağ ok): karşıdaki en uç öğeye atla.
        if (extreme is { } e1 && Dot((-direction.X, -direction.Y), hoveredDir) > Math.Cos(20 * Math.PI / 180)) return e1.Index;

        if (candidates.Count < 2) return current;
        var clockwise = Cross(hoveredDir, direction);
        (int Index, double Angle, double Dot)? best = null;
        var bestDistance = 360.0;
        var opposite = Math.Cos(160 * Math.PI / 180);
        foreach (var c in candidates)
        {
            if (c.Index == hovered) continue;
            var dir = Direction(c.Angle);
            var diff = AngularDifference(hoveredAngle, c.Angle);
            if ((Cross(hoveredDir, dir) == clockwise || Dot(hoveredDir, dir) < opposite) && diff < bestDistance)
            {
                best = c;
                bestDistance = diff;
            }
        }
        return best?.Index ?? current;
    }

    private static double Dot((double X, double Y) a, (double X, double Y) b) => a.X * b.X + a.Y * b.Y;

    // Kando isClockwise: ikinci vektör birincinin hangi yarısında (yalnızca karşılaştırmada kullanılır).
    private static bool Cross((double X, double Y) a, (double X, double Y) b) => a.X * b.Y - a.Y * b.X < 0;

    /// <summary>
    /// Bırakılan öğenin ekleneceği sıra (0…n): imlecin açısından önce gelen (ilk öğeden saat yönünde ölçülen) öğe sayısı.
    /// İlk öğenin hemen solundaki bırakma sona eklenir (çizimde yine ilk öğenin solunda durur).
    /// </summary>
    public static int DropIndex(IReadOnlyList<double> angles, double pointerAngle)
    {
        if (angles.Count == 0) return 0;
        var origin = angles[0];
        var pointer = Mod360(pointerAngle - origin);
        var index = 0;
        for (var i = 0; i < angles.Count; i++)
            if (Mod360(angles[i] - origin) < pointer) index = i + 1;
        return index;
    }

    /// <summary>
    /// Öğe merkezlerinin yarıçapı (DIP): <paramref name="count"/> öğe (<paramref name="itemDip"/> çaplı, aralarında
    /// <see cref="ItemGapDip"/>) çembere sığsın; en az <paramref name="min"/>.
    /// </summary>
    public static double Radius(int count, double itemDip, double min) =>
        Math.Max(min, Math.Max(0, count) * (itemDip + ItemGapDip) / (2 * Math.PI));
}
