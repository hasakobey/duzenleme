using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Duzenleme.Widgets;

/// <summary>
/// Yeni eklenen widget'ın kartının çevresinde durağan vurgu ışıması: kartın dışında, gölge payının içinde, saydamlığı dışa
/// doğru azalan birkaç yuvarlak çerçeve. Efekt (DropShadowEffect) ve animasyon kullanılmaz; birkaç saniye sonra kaldırılır.
/// </summary>
internal sealed class RevealGlow : Adorner
{
    // (kart kenarından uzaklık, çizgi kalınlığı, saydamlık); en dıştaki gölge payının (14 DIP) içinde kalır.
    private static readonly (double Offset, double Thickness, byte Alpha)[] Rings = [(1.5, 3, 150), (4.5, 3, 85), (8, 4, 38)];

    private readonly Pen[] _pens;
    private readonly double _radius;

    private RevealGlow(UIElement card, Color accent, double cornerRadius) : base(card)
    {
        IsHitTestVisible = false;
        _radius = cornerRadius;
        _pens = Rings.Select(r =>
        {
            var brush = new SolidColorBrush(Color.FromArgb(r.Alpha, accent.R, accent.G, accent.B));
            brush.Freeze();
            var pen = new Pen(brush, r.Thickness);
            pen.Freeze();
            return pen;
        }).ToArray();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(AdornedElement.RenderSize);
        for (var i = 0; i < Rings.Length; i++)
        {
            var d = Rings[i].Offset + Rings[i].Thickness / 2;
            var rect = new Rect(bounds.X - d, bounds.Y - d, bounds.Width + 2 * d, bounds.Height + 2 * d);
            dc.DrawRoundedRectangle(null, _pens[i], rect, _radius + d, _radius + d);
        }
    }

    /// <summary>Kartın çevresine ışıma ekler; eklenemezse (süsleme katmanı yok) null.</summary>
    public static RevealGlow? Show(FrameworkElement card, Brush accent, double cornerRadius)
    {
        if (AdornerLayer.GetAdornerLayer(card) is not { } layer) return null;
        var color = accent is SolidColorBrush solid ? solid.Color : Color.FromRgb(0xA7, 0x8B, 0xFA);
        var glow = new RevealGlow(card, color, cornerRadius);
        layer.Add(glow);
        return glow;
    }

    public void Remove()
    {
        if (AdornerLayer.GetAdornerLayer(AdornedElement) is { } layer) layer.Remove(this);
    }
}
