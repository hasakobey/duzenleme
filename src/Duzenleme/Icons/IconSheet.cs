using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Duzenleme.Core;

namespace Duzenleme.Icons;

/// <summary>Simge kütüphanesinin önizleme sayfasını PNG olarak yazar (--export-icon-sheet).</summary>
public static class IconSheet
{
    public static void Export(string path)
    {
        const int cell = 132, cols = 6, icon = 88;
        var glyphs = FolderIconCatalog.Glyphs;
        var rows = (int)Math.Ceiling(glyphs.Length / (double)cols);
        var width = cols * cell + 40;
        var height = rows * cell + 150;
        var face = new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x1B, 0x1B, 0x26), Color.FromRgb(0x12, 0x12, 0x1A), 90), null, new Rect(0, 0, width, height));
            dc.DrawText(Text($"{AppInfo.Name} · Klasör simgeleri", face, 26, Brushes.White), new Point(24, 22));
            dc.DrawText(Text("Hazır kütüphane (API anahtarı gerekmez) — klasör adına göre otomatik önerilir, 10 renkle değiştirilebilir",
                face, 13, new SolidColorBrush(Color.FromArgb(170, 255, 255, 255))), new Point(24, 60));

            // Renk şeridi
            for (var i = 0; i < FolderIconCatalog.Colors.Length; i++)
            {
                var c = FolderIconCatalog.Colors[i];
                var img = FolderIconRenderer.Render(FolderIconRenderer.FolderDrawing(c, null), 34);
                dc.DrawImage(img, new Rect(24 + i * 40, 84, 34, 34));
            }

            for (var i = 0; i < glyphs.Length; i++)
            {
                var g = glyphs[i];
                var x = 20 + (i % cols) * cell;
                var y = 130 + (i / cols) * cell;
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)), null, new Rect(x + 4, y, cell - 8, cell - 8), 12, 12);
                var img = FolderIconRenderer.Render(FolderIconRenderer.FolderDrawing(FolderIconCatalog.ColorFor(g), g.Glyph), icon);
                dc.DrawImage(img, new Rect(x + (cell - icon) / 2, y + 6, icon, icon));
                var label = Text(g.Label, face, 12.5, new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)));
                dc.DrawText(label, new Point(x + (cell - label.Width) / 2, y + icon + 8));
            }
        }

        var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }

    private static FormattedText Text(string s, Typeface face, double size, Brush brush) =>
        new(s, CultureInfo.GetCultureInfo("tr-TR"), FlowDirection.LeftToRight, face, size, brush, 1.0);
}
