using System.Buffers.Binary;

namespace Duzenleme.Core;

/// <summary>
/// Kabuğun "Shell IDList Array" (CIDA) sürükleme verisi: önce öğe sayısı, sonra (sayı + 1) uzaklık; ilk uzaklık üst klasörün,
/// diğerleri öğelerin (üst klasöre göre) kimlik listesini (PIDL) gösterir. Her liste <c>{ushort cb; byte[cb-2]}</c> öğelerinden
/// oluşur ve cb = 0 ile biter. Ayrıştırma saf ve sınırlıdır: bozuk ya da aşırı büyük veri null döner. Kimlik listesini kabuk
/// öğesine çevirmek (uygulama adı, dosya yolu) STA'da kabuğun işidir (<c>Desktop.ShellDropItems</c>). Testlenir.
/// </summary>
public static class ShellIdList
{
    /// <summary>Sürükleme verisinin biçim adı (CFSTR_SHELLIDLIST).</summary>
    public const string Format = "Shell IDList Array";

    /// <summary>Bir bırakmada en çok kaç öğe ele alınır.</summary>
    public const int MaxItems = 64;

    /// <summary>Okunacak verinin üst sınırı; çıkarılan tam kimlik listelerinin toplamı da bunu aşamaz.</summary>
    public const int MaxBytes = 1 << 20;

    /// <summary>
    /// Tek bir kimlik listesinin üst sınırı (gerçek listeler birkaç yüz bayttır). Uzaklıklar aynı yeri gösterebildiği için
    /// sınırsız listeler 1 MB'lık veriden çok daha fazlasını ayırtabilirdi.
    /// </summary>
    public const int MaxListBytes = 64 * 1024;

    /// <summary>
    /// Öğelerin masaüstüne göre tam kimlik listeleri (üst klasörünkü + öğeninki + 2 baytlık sonlandırıcı): kabuğa
    /// (SHCreateItemFromIDList) olduğu gibi verilir. Veri bozuksa, boşsa ya da sınırı aşıyorsa null.
    /// </summary>
    public static IReadOnlyList<byte[]>? Parse(ReadOnlySpan<byte> cida)
    {
        if (Header(cida) is not { } count) return null;
        var parent = List(cida, 0, count);
        if (parent is not { } p) return null;
        var items = new List<byte[]>(count);
        var total = 0;
        for (var i = 1; i <= count; i++)
        {
            if (List(cida, i, count) is not { } child) return null;
            total += p.Length + child.Length + 2;
            if (total > MaxBytes) return null;
            var full = new byte[p.Length + child.Length + 2];
            cida.Slice(p.Start, p.Length).CopyTo(full);
            cida.Slice(child.Start, child.Length).CopyTo(full.AsSpan(p.Length));
            items.Add(full);
        }
        return items;
    }

    /// <summary>Üst klasörün kimlik listesi (sonlandırıcısız); veri bozuksa null.</summary>
    public static byte[]? Parent(ReadOnlySpan<byte> cida) =>
        Header(cida) is { } count && List(cida, 0, count) is { } p ? cida.Slice(p.Start, p.Length).ToArray() : null;

    /// <summary>
    /// Kimlik listesi Uygulamalar klasörünün (Başlat menüsü uygulamaları) kendisi mi? Masaüstünün altındaki kök öğe biçimi:
    /// cb = 0x14, tür baytı 0x1F, sıra baytı, ardından sınıf kimliği; tek öğe (ardından ya sonlandırıcı ya hiçbir şey).
    /// </summary>
    public static bool IsAppsFolder(ReadOnlySpan<byte> pidl)
    {
        const int RootItem = 20;
        if (pidl.Length < RootItem) return false;
        if (BinaryPrimitives.ReadUInt16LittleEndian(pidl) != RootItem || pidl[2] != 0x1F) return false;
        if (pidl.Length > RootItem && (pidl.Length < RootItem + 2 || BinaryPrimitives.ReadUInt16LittleEndian(pidl[RootItem..]) != 0)) return false;
        return new Guid(pidl.Slice(4, 16)) == StartApps.FolderId;
    }

    /// <summary>Öğe sayısı (başlık ve uzaklık tablosu sığıyorsa); değilse null.</summary>
    private static int? Header(ReadOnlySpan<byte> cida)
    {
        if (cida.Length < 12 || cida.Length > MaxBytes) return null;
        var count = BinaryPrimitives.ReadUInt32LittleEndian(cida);
        if (count is 0 or > MaxItems) return null;
        return cida.Length < 4 + 4 * ((int)count + 1) ? null : (int)count;
    }

    /// <summary><paramref name="index"/>'inci listenin başı ve (sonlandırıcısız) uzunluğu; liste bozuksa null.</summary>
    private static (int Start, int Length)? List(ReadOnlySpan<byte> cida, int index, int count)
    {
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(cida[(4 + 4 * index)..]);
        var tableEnd = 4 + 4 * (count + 1);
        if (offset < tableEnd || offset > cida.Length - 2) return null;
        var start = (int)offset;
        var pos = start;
        while (true)
        {
            if (pos + 2 > cida.Length) return null;
            var cb = BinaryPrimitives.ReadUInt16LittleEndian(cida[pos..]);
            if (cb == 0) return (start, pos - start);
            if (cb < 2 || pos + cb > cida.Length || pos + cb - start > MaxListBytes) return null;
            pos += cb;
        }
    }
}
