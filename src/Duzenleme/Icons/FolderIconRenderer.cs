using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Duzenleme.Core;

namespace Duzenleme.Icons;

/// <summary>Modern (Fluent tarzı) klasör simgesi çizer: renkli klasör + ön yüzde beyaz sembol.</summary>
public static class FolderIconRenderer
{
    private static readonly FontFamily SymbolFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly Typeface SymbolFace = new(SymbolFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static Color C(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    private static Color Shade(Color c, double f) =>
        Color.FromArgb(c.A, (byte)Math.Clamp(c.R * f, 0, 255), (byte)Math.Clamp(c.G * f, 0, 255), (byte)Math.Clamp(c.B * f, 0, 255));

    /// <summary>256×256 tasarım alanında klasör çizimi.</summary>
    public static Drawing FolderDrawing(IconColor color, char? glyph)
    {
        var front = C(color.Front);
        var back = C(color.Back);
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            // Saydam tuval: ölçek her zaman 256×256 kalsın.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 256, 256));

            // Arka yüz + sekme
            var backGeo = Geometry.Parse("M 30,40 H 92 C 100,40 105,43 110,49 L 118,58 H 226 C 235,58 242,65 242,74 V 206 C 242,215 235,222 226,222 H 30 C 21,222 14,215 14,206 V 56 C 14,47 21,40 30,40 Z");
            dc.DrawGeometry(new LinearGradientBrush(Shade(back, 1.05), Shade(back, 0.85), 90), null, backGeo);

            // İçeriden görünen kağıt
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)), null, new Rect(34, 70, 188, 70), 8, 8);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(120, 226, 232, 240)), null, new Rect(44, 80, 168, 60), 6, 6);

            // Ön yüz
            var frontRect = new Rect(14, 92, 228, 130);
            dc.DrawRoundedRectangle(new LinearGradientBrush(Shade(front, 1.08), Shade(front, 0.9), 90), null, frontRect, 16, 16);
            // Üst parlaklık çizgisi
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), null, new Rect(14, 92, 228, 3), 1.5, 1.5);

            if (glyph is { } g && g != '')
            {
                var text = new FormattedText(g.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SymbolFace, 70, Brushes.White, 1.0);
                var origin = new Point(128 - text.Width / 2, 157 - text.Height / 2);
                var shadow = new FormattedText(g.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SymbolFace, 70,
                    new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 1.0);
                dc.DrawText(shadow, new Point(origin.X, origin.Y + 3));
                dc.DrawText(text, origin);
            }
        }
        group.Freeze();
        return group;
    }

    public static BitmapSource Render(Drawing drawing, int size)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bounds = drawing.Bounds;
            var scale = size / Math.Max(bounds.Width, bounds.Height);
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.PushTransform(new TranslateTransform(-bounds.X + (Math.Max(bounds.Width, bounds.Height) - bounds.Width) / 2,
                                                    -bounds.Y + (Math.Max(bounds.Width, bounds.Height) - bounds.Height) / 2));
            dc.DrawDrawing(drawing);
        }
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    public static ImageSource Preview(IconGlyph glyph, IconColor color, int size = 96) =>
        Render(FolderDrawing(color, glyph.Glyph), size);

    /// <summary>Çok boyutlu .ico (her boyut PNG olarak; Windows Vista'dan beri desteklenir).</summary>
    public static byte[] ToIco(Drawing drawing)
    {
        int[] sizes = [256, 64, 48, 32, 24, 16];
        var frames = sizes.Select(s =>
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(Render(drawing, s)));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }).ToList();

        using var output = new MemoryStream();
        using var w = new BinaryWriter(output);
        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)sizes.Length);
        var offset = 6 + 16 * sizes.Length;
        for (var i = 0; i < sizes.Length; i++)
        {
            var dim = sizes[i] >= 256 ? 0 : sizes[i];
            w.Write((byte)dim);
            w.Write((byte)dim);
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write((uint)frames[i].Length);
            w.Write((uint)offset);
            offset += frames[i].Length;
        }
        foreach (var f in frames) w.Write(f);
        w.Flush();
        return output.ToArray();
    }
}
