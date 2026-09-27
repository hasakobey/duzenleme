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

    /// <summary>
    /// TurnOn'u geri alır: turnModeOff ise (mod TurnOn'dan önce kapalıydı) önce SetFencesManageDesktop(false); sonra her id
    /// için Widgets.Remove(id, notify: false). Mod önceden açıksa açık kalır, yalnızca eklenen bölmeler kalkar.
    /// </summary>
    public static void Undo(IReadOnlyList<string> addedIds, bool turnModeOff)
    {
        if (turnModeOff) AppHost.SetFencesManageDesktop(false);
        foreach (var id in addedIds) AppHost.Widgets.Remove(id, notify: false);
    }

    /// <summary>
    /// TurnOn ve sonucunu söyleyen bildirim. "Geri al" yalnızca geri alınacak bir şey varsa çıkar (mod zaten açıksa ve bölme
    /// eklenmediyse TurnOn hiçbir şeyi değiştirmedi) ve modu yalnızca bu işlem açtıysa kapatır.
    /// </summary>
    public static void TurnOnWithNotice(bool allStarters, NativeMethods.POINT? near, string? trayHint = null)
    {
        var wasOn = AppHost.Settings.FencesReplaceIcons;
        var ids = TurnOn(allStarters, near);
        var text = Describe(ids.Count, wasOn);
        if (ids.Count > 0 || !wasOn)
            Notice.Show(text, NoticeKind.Success, "Geri al", () => Undo(ids, turnModeOff: !wasOn), trayHint);
        else
            Notice.Show(text, NoticeKind.Info);
    }

    public static void TurnOff() => AppHost.SetFencesManageDesktop(false);

    /// <summary>
    /// Mod önceden kapalıydı: n>0 "{n} bölme eklendi. Masaüstü simgeleri artık yalnızca bölmelerde.", n=0 "Masaüstü simgeleri
    /// artık yalnızca bölmelerde." Mod zaten açıktı: n>0 "{n} bölme eklendi.", n=0 "Bölmelerin zaten hazır; ...".
    /// </summary>
    public static string Describe(int added, bool modeWasOn = false) => (added, modeWasOn) switch
    {
        (> 0, false) => $"{added} bölme eklendi. Masaüstü simgeleri artık yalnızca bölmelerde.",
        (_, false) => "Masaüstü simgeleri artık yalnızca bölmelerde.",
        (> 0, true) => $"{added} bölme eklendi.",
        _ => "Bölmelerin zaten hazır; masaüstü simgeleri yalnızca bölmelerde.",
    };
}
