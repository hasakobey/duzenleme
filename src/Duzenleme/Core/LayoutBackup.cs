namespace Duzenleme.Core;

/// <summary>
/// Otomatik düzen yedekleri ("Düzen uygulanmadan önce", "Otomatik yerleştirmeden önce"): Kayıtlı düzenler'de adıyla durur
/// ve adıyla tanınır. Yeni yedek arayüz dilindeki adı alır; dil değiştiyse eski dildeki yedek de tanınır ve yerine yazılır
/// (iki yedek birikmez, yedeğin kendisi uygulanırken yeni yedek alınmaz).
/// </summary>
public static class LayoutBackup
{
    public static string ApplyName => L.T("Düzen uygulanmadan önce");
    public static string ArrangeName => L.T("Otomatik yerleştirmeden önce");

    public static bool IsApply(string name) => L.Variants("Düzen uygulanmadan önce").Contains(name);
    public static bool IsArrange(string name) => L.Variants("Otomatik yerleştirmeden önce").Contains(name);

    /// <summary>Düzen uygulanmadan önceki yerleşimi saklar (varsa eski yedeğin yerine).</summary>
    public static void SaveBeforeApply(List<LayoutSnapshot> layouts, IEnumerable<WidgetConfig> widgets) =>
        Replace(layouts, IsApply, ApplyName, widgets);

    /// <summary>Otomatik yerleştirmeden önceki yerleşimi saklar (varsa eski yedeğin yerine).</summary>
    public static void SaveBeforeArrange(List<LayoutSnapshot> layouts, IEnumerable<WidgetConfig> widgets) =>
        Replace(layouts, IsArrange, ArrangeName, widgets);

    private static void Replace(List<LayoutSnapshot> layouts, Func<string, bool> isBackup, string name, IEnumerable<WidgetConfig> widgets)
    {
        layouts.RemoveAll(l => isBackup(l.Name));
        layouts.Add(LayoutSnapshot.Capture(name, widgets));
    }
}
