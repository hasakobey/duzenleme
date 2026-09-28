namespace Duzenleme.Core;

/// <summary>
/// Dar widget başlığının sığdırılması: başlık yazısı "…" olmasın diye isteğe bağlı parçalar (öğe sayısı, "Klasörü aç",
/// arama, başlık simgesi) verilen sırayla geçici olarak gizlenir; yazıya en az <see cref="TitleMin"/> DIP (ya da tamamı
/// daha kısaysa o kadar) kalana dek. Gizleme kaydedilmez: widget genişleyince parçalar geri gelir.
/// </summary>
public static class HeaderFit
{
    /// <summary>Başlık yazısına bırakılmak istenen en az genişlik (DIP): kısa bir ad ya da uzun adın başı okunur.</summary>
    public const double TitleMin = 72;

    /// <summary>
    /// Kaç isteğe bağlı parçanın (listenin başından) gizlenmesi gerektiği.
    /// </summary>
    /// <param name="headerWidth">Başlık satırının genişliği.</param>
    /// <param name="fixedWidth">Hep görünen parçaların (ör. kaldırma düğmesi ×) toplam genişliği.</param>
    /// <param name="titleNatural">Başlık yazısının kısaltılmamış genişliği.</param>
    /// <param name="optionalWidths">Görünen isteğe bağlı parçaların genişlikleri, gizlenme sırasıyla.</param>
    public static int PartsToHide(double headerWidth, double fixedWidth, double titleNatural, IReadOnlyList<double> optionalWidths,
        double titleMin = TitleMin)
    {
        var want = Math.Min(Math.Max(0, titleNatural), titleMin);
        var used = fixedWidth + optionalWidths.Sum();
        var hide = 0;
        // Küçük pay: yuvarlama yüzünden sığan bir başlık için parça gizlenmesin.
        while (hide < optionalWidths.Count && headerWidth - used < want - 0.5)
            used -= optionalWidths[hide++];
        return hide;
    }
}
