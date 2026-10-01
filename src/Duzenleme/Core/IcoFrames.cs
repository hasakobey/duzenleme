namespace Duzenleme.Core;

/// <summary>Bir .ico dosyasındaki tek kare: görüntü verisinin dosyadaki yeri ve dizindeki boyutu.</summary>
public readonly record struct IcoFrame(int Offset, int Length, int Width, int Height, int BitDepth);

/// <summary>
/// .ico dosyasından kare seçimi (tepsi simgesi): istenen boyuta en yakın kare, eşitlikte ekranın renk derinliğine (32 bit)
/// en yakın, ondan yüksek olmayan en derin kare; o da yoksa ilki. 2.1'e kadar tepsi simgesini kuran WinForms simge
/// sınıfının seçimiyle birebir aynıdır (dizindeki 0 genişlik 256 sayılmaz, 0 kalır), böylece tepside aynı kare görünür.
/// Diske ve kabuğa dokunmaz.
/// </summary>
public static class IcoFrames
{
    /// <summary>Ekran renk derinliği varsayımı (günümüzde her ekran 32 bit).</summary>
    private const int ScreenBitDepth = 32;

    /// <summary>
    /// <paramref name="pixels"/> boyutu için seçilen kare; dosya bir simge değilse ya da kare dosyanın dışına taşıyorsa null.
    /// </summary>
    public static IcoFrame? Pick(ReadOnlySpan<byte> ico, int pixels)
    {
        // ICONDIR: idReserved (0), idType (1 = simge), idCount; ardından 16 baytlık ICONDIRENTRY'ler.
        if (ico.Length < 6 || U16(ico, 0) != 0 || U16(ico, 2) != 1) return null;
        int count = U16(ico, 4);
        if (count == 0 || 6 + 16 * count > ico.Length) return null;

        IcoFrame? best = null;
        for (var i = 0; i < count; i++)
        {
            var entry = ico.Slice(6 + 16 * i, 16);
            int width = entry[0], height = entry[1], colors = entry[2];
            int bitDepth = colors != 0 ? (colors < 0x10 ? 1 : 4) : U16(entry, 6);
            if (bitDepth == 0) bitDepth = 8;
            var length = U32(entry, 8);
            var offset = U32(entry, 12);
            var candidate = new IcoFrame((int)Math.Min(offset, int.MaxValue), (int)Math.Min(length, int.MaxValue), width, height, bitDepth);

            if (best is not { } current || current.Length == 0)
            {
                best = candidate;
                continue;
            }
            var bestDelta = Math.Abs(current.Width - pixels) + Math.Abs(current.Height - pixels);
            var delta = Math.Abs(width - pixels) + Math.Abs(height - pixels);
            if (delta < bestDelta
                || (delta == bestDelta && ((bitDepth <= ScreenBitDepth && bitDepth > current.BitDepth)
                                           || (current.BitDepth > ScreenBitDepth && bitDepth < current.BitDepth))))
                best = candidate;
        }

        if (best is not { } chosen || chosen.Length == 0 || (long)chosen.Offset + chosen.Length > ico.Length) return null;
        return chosen;
    }

    /// <summary>Karenin görüntü verisi (BMP ya da PNG), simge tutamacı kurmak için.</summary>
    public static byte[] Bytes(ReadOnlySpan<byte> ico, IcoFrame frame) => ico.Slice(frame.Offset, frame.Length).ToArray();

    private static int U16(ReadOnlySpan<byte> data, int at) => data[at] | (data[at + 1] << 8);

    private static uint U32(ReadOnlySpan<byte> data, int at) =>
        (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24));
}
