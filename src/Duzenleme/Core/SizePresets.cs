namespace Duzenleme.Core;

/// <summary>Düzenle panelindeki boyut seçeneği: kimlik (AutomationId'de: Editor.Size.Small…), ölçek ve adı (<see cref="L.N"/>).</summary>
public sealed record SizePreset(string Id, double Scale, string Name)
{
    /// <summary>Görünen ad (arayüz dilinde).</summary>
    public string Label => L.Dyn(Name);
}

/// <summary>
/// "Boyut: Küçük · Orta · Büyük" (Kolay NestDesk): menüdeki altı ölçek yüzdesi üç seçeneğe iner ve <see cref="WidgetConfig.Scale"/>'e
/// 0,85 / 1 / 1,25 yazılır. Kayıtlı başka bir ölçek (eski menüden %150, Ctrl + tekerlekten %110…) kaybolmaz: panelde seçili
/// "Özel (%150)" seçeneği olarak görünür. Saf, testlenir.
/// </summary>
public static class SizePresets
{
    public const string Small = "Small", Medium = "Medium", Large = "Large";

    public static readonly IReadOnlyList<SizePreset> All =
    [
        new(Small, 0.85, L.N("Küçük")),
        new(Medium, 1.0, L.N("Orta")),
        new(Large, 1.25, L.N("Büyük")),
    ];

    /// <summary>Ölçeğe denk gelen seçenek (yuvarlama payıyla); başka bir ölçekte null.</summary>
    public static SizePreset? Match(double scale) => All.FirstOrDefault(p => Math.Abs(p.Scale - scale) < 0.005);

    /// <summary>Seçeneklerden biri değilse "Özel (%150)" (arayüz dilinin yüzde biçimiyle); seçeneklerden biriyse null.</summary>
    public static string? CustomLabel(double scale) =>
        Match(scale) is null ? L.F("Özel ({0})", L.Percent(Math.Round(scale, 2))) : null;
}
