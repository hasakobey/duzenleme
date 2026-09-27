namespace Duzenleme.Core;

/// <summary>Ekran üzerinde bir dikdörtgen (fiziksel piksel).</summary>
public readonly record struct Box(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public Box Offset(int dx, int dy) => new(Left + dx, Top + dy, Right + dx, Bottom + dy);

    public bool Overlaps(Box other) => Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;

    public bool Inside(Box area) => Left >= area.Left && Top >= area.Top && Right <= area.Right && Bottom <= area.Bottom;
}

/// <summary>
/// Widget kartlarının yerleşim kuralları: sürüklerken mıknatıs (komşunun altına/üstüne/yanına ve ekran kenarına
/// aralıklı yapışma, kenar hizalama), bırakınca çakışmayı çözme ve kenardan büyütürken komşuya girmeme.
/// </summary>
public static class WidgetLayout
{
    /// <summary>
    /// Sürüklenen kartı eşik içindeki en yakın hizaya çeker. Yatay ve dikey ayrı ayrı değerlendirilir;
    /// aday yoksa (0, 0).
    /// </summary>
    public static (int Dx, int Dy) Snap(Box card, IReadOnlyList<Box> others, Box work, int threshold, int gap)
    {
        int? dx = null, dy = null;
        void X(int target, int current)
        {
            var d = target - current;
            if (Math.Abs(d) <= threshold && (dx is null || Math.Abs(d) < Math.Abs(dx.Value))) dx = d;
        }
        void Y(int target, int current)
        {
            var d = target - current;
            if (Math.Abs(d) <= threshold && (dy is null || Math.Abs(d) < Math.Abs(dy.Value))) dy = d;
        }

        // Ekran (çalışma alanı) kenarları.
        X(work.Left + gap, card.Left);
        X(work.Right - gap, card.Right);
        Y(work.Top + gap, card.Top);
        Y(work.Bottom - gap, card.Bottom);

        foreach (var o in others)
        {
            var besideVertically = card.Top < o.Bottom + threshold && o.Top < card.Bottom + threshold;
            var besideHorizontally = card.Left < o.Right + threshold && o.Left < card.Right + threshold;
            if (besideVertically)
            {
                // Yan yana: sağına ya da soluna.
                X(o.Right + gap, card.Left);
                X(o.Left - gap, card.Right);
                var horizontalGap = Math.Max(o.Left - card.Right, card.Left - o.Right);
                if (horizontalGap <= gap + threshold * 3)
                {
                    Y(o.Top, card.Top);
                    Y(o.Bottom, card.Bottom);
                }
            }
            if (besideHorizontally)
            {
                // Alt alta: altına ya da üstüne.
                Y(o.Bottom + gap, card.Top);
                Y(o.Top - gap, card.Bottom);
                // Sol/sağ kenar hizası yalnızca yakın komşuyla (uzaktaki bir widget'a çekilmesin).
                var verticalGap = Math.Max(o.Top - card.Bottom, card.Top - o.Bottom);
                if (verticalGap <= gap + threshold * 3)
                {
                    X(o.Left, card.Left);
                    X(o.Right, card.Right);
                }
            }
        }
        return (dx ?? 0, dy ?? 0);
    }

    /// <summary>
    /// Kart başka kartlarla çakışıyorsa en az kaydırmayla çakışmadığı bir yere taşır: çakıştığı kartın sağına,
    /// soluna, altına ya da üstüne (aralıklı); o da doluysa birkaç adım daha denenir. Ekrana sığmayan adaylar
    /// elenir. Çakışma yoksa ya da yer bulunamazsa null.
    /// </summary>
    public static Box? Separate(Box card, IReadOnlyList<Box> others, Box work, int gap)
    {
        if (!others.Any(o => o.Overlaps(card))) return null;
        // Ekrandan taşan kart önce içeri alınır (sığıyorsa); yoksa hiçbir aday "ekranda" sayılmazdı.
        if (card.Width <= work.Width && card.Height <= work.Height)
            card = card.Offset(
                Math.Clamp(card.Left, work.Left, work.Right - card.Width) - card.Left,
                Math.Clamp(card.Top, work.Top, work.Bottom - card.Height) - card.Top);
        if (!others.Any(o => o.Overlaps(card))) return card;
        Box? best = null;
        var bestDistance = long.MaxValue;
        var seen = new HashSet<Box> { card };
        var frontier = new List<Box> { card };
        for (var depth = 0; depth < 3 && frontier.Count > 0; depth++)
        {
            var next = new List<Box>();
            foreach (var c in frontier)
            {
                foreach (var o in others.Where(o => o.Overlaps(c)))
                {
                    Box[] candidates =
                    [
                        c.Offset(o.Right + gap - c.Left, 0), c.Offset(o.Left - gap - c.Right, 0),
                        c.Offset(0, o.Bottom + gap - c.Top), c.Offset(0, o.Top - gap - c.Bottom),
                    ];
                    foreach (var candidate in candidates)
                    {
                        if (!seen.Add(candidate) || !candidate.Inside(work)) continue;
                        if (others.Any(x => x.Overlaps(candidate)))
                        {
                            next.Add(candidate);
                            continue;
                        }
                        long ddx = candidate.Left - card.Left, ddy = candidate.Top - card.Top;
                        var distance = ddx * ddx + ddy * ddy;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = candidate;
                        }
                    }
                }
            }
            frontier = next.Count > 64 ? next.Take(64).ToList() : next;
        }
        return best;
    }

    /// <summary>
    /// Kenardan büyütürken çekilen kenar, başlangıçta o tarafta duran komşunun kenarında (aralıklı) durur.
    /// Başlangıçta zaten çakışan komşular dikkate alınmaz.
    /// </summary>
    public static Box ClampResize(Box start, Box proposed, bool left, bool top, bool right, bool bottom, IReadOnlyList<Box> others, int gap)
    {
        var (l, t, r, b) = (proposed.Left, proposed.Top, proposed.Right, proposed.Bottom);
        foreach (var o in others.Where(o => !o.Overlaps(start)))
        {
            if (!new Box(l, t, r, b).Overlaps(o)) continue;
            if (right && o.Left >= start.Right) r = Math.Min(r, o.Left - gap);
            if (left && o.Right <= start.Left) l = Math.Max(l, o.Right + gap);
            if (bottom && o.Top >= start.Bottom) b = Math.Min(b, o.Top - gap);
            if (top && o.Bottom <= start.Top) t = Math.Max(t, o.Bottom + gap);
        }
        // Duvar, başlangıç boyutunun gerisine itmesin.
        if (right) r = Math.Max(r, Math.Min(start.Right, proposed.Right));
        if (left) l = Math.Min(l, Math.Max(start.Left, proposed.Left));
        if (bottom) b = Math.Max(b, Math.Min(start.Bottom, proposed.Bottom));
        if (top) t = Math.Min(t, Math.Max(start.Top, proposed.Top));
        return new Box(l, t, r, b);
    }
}
