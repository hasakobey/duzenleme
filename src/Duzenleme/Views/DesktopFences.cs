using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>
/// "Masaüstü simgelerini yalnızca bölmelerde göster" modunu açıp kapatır ve açarken eklenen bölmeleri geri alınabilir
/// tutar ("Masaüstümü bölmelere ayır" düğmesi, Widget'lar sayfasındaki anahtar).
/// </summary>
internal static class DesktopFences
{
    /// <summary>allStarters=true: AddStarterFences (her zaman; "Masaüstümü bölmelere ayır" düğmesi).
    /// false: hiç Fence yoksa AddStarterFences (anahtar). Sonra SetFencesManageDesktop(true) (EnsureDesktopCoverage eksikleri ekler).
    /// near verilirse eklenen bölmeler o noktanın monitörüne yerleşir.
    /// Bu çağrıda eklenen widget kimliklerini döner (önceki/sonraki Id kümesi farkı).</summary>
    public static List<string> TurnOn(bool allStarters, NativeMethods.POINT? near = null)
    {
        var before = AppHost.Settings.Widgets.Select(w => w.Id).ToHashSet();
        if (allStarters || !AppHost.Settings.Widgets.Any(w => w.Kind == WidgetKind.Fence))
            AppHost.Widgets.AddStarterFences(near);
        // Eksik türler burada (yer ipucuyla) eklenir; SetFencesManageDesktop'taki denetim sonra bir şey bulmaz.
        AppHost.Widgets.EnsureDesktopCoverage(near);
        AppHost.SetFencesManageDesktop(true);
        return AppHost.Settings.Widgets.Where(w => !before.Contains(w.Id)).Select(w => w.Id).ToList();
    }

    /// <summary>Önce SetFencesManageDesktop(false), sonra her id için Widgets.Remove(id, notify: false).</summary>
    public static void Undo(IReadOnlyList<string> addedIds)
    {
        AppHost.SetFencesManageDesktop(false);
        foreach (var id in addedIds) AppHost.Widgets.Remove(id, notify: false);
    }

    public static void TurnOff() => AppHost.SetFencesManageDesktop(false);

    /// <summary>n>0: "{n} bölme eklendi. Masaüstü simgeleri artık yalnızca bölmelerde."  n=0: "Masaüstü simgeleri artık yalnızca bölmelerde."</summary>
    public static string Describe(int added) =>
        added > 0
            ? $"{added} bölme eklendi. Masaüstü simgeleri artık yalnızca bölmelerde."
            : "Masaüstü simgeleri artık yalnızca bölmelerde.";
}
