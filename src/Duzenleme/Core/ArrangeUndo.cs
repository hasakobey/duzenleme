namespace Duzenleme.Core;

/// <summary>"Widget'ları hizala"nın taşıdığı bir widget: kimliği, önceki ve hizalanmış fiziksel konumu (pencerenin sol üstü).</summary>
public sealed record ArrangeMove(string Id, int FromX, int FromY, int ToX, int ToY);

/// <summary>
/// "Widget'ları hizala"nın geri alınması (saf; testlenir). Yalnızca konumlar geri gelir: bu arada eklenen widget'lar ve
/// başka değişiklikler kalır (kayıtlı yedeğin bütünüyle uygulanması onları silerdi; yedek yine "Kayıtlı yerleşimler"de durur).
/// Hizalamadan sonra kullanıcının taşıdığı ya da kapanmış widget yerinde bırakılır.
/// </summary>
public static class ArrangeUndo
{
    /// <summary>Geri taşınacaklar: gerçekten yer değiştirmiş ve hâlâ hizalamanın koyduğu yerde duran widget'lar.</summary>
    /// <param name="current">Widget'ın şimdiki konumu; açık değilse null.</param>
    public static IReadOnlyList<ArrangeMove> Pending(IEnumerable<ArrangeMove> moves, Func<string, (int X, int Y)?> current) =>
        moves.Where(m => (m.FromX != m.ToX || m.FromY != m.ToY) && current(m.Id) is { } p && p.X == m.ToX && p.Y == m.ToY).ToList();
}
