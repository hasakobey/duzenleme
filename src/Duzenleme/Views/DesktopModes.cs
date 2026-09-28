using System.Windows;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>Windows'un masaüstü simgeleri için seçim (kalıcı değil: ayarlarda iki bool olarak durur).</summary>
internal enum IconMode
{
    /// <summary>Hepsi masaüstünde görünür (kutular ve bölmeler yalnızca bağlantı gösterir).</summary>
    ShowAll,

    /// <summary>Kutuya eklenen masaüstü öğesi masaüstünden kalkar (NestDesk klasörüne taşınır). AppSettings.BoxItemsLeaveDesktop.</summary>
    BoxItemsLeave,

    /// <summary>Simgeler yalnızca bölmelerde; Windows'un simgeleri gizlenir. AppSettings.FencesReplaceIcons.</summary>
    FencesOnly,
}

/// <summary>
/// "Windows masaüstü simgeleri" seçimi: Ayarlar, Widget'lar sayfası, tepsi, bölme ve kutu menüleri aynı metinleri ve aynı
/// davranışı buradan alır. Dosya taşıyan ya da geri koyan adımlar her zaman sorulur.
/// </summary>
internal static class DesktopModes
{
    public static IconMode Current =>
        AppHost.Settings.FencesReplaceIcons ? IconMode.FencesOnly
        : AppHost.Settings.BoxItemsLeaveDesktop ? IconMode.BoxItemsLeave
        : IconMode.ShowAll;

    /// <summary>Seçim kutularındaki sıra.</summary>
    public static readonly IconMode[] Choices = [IconMode.ShowAll, IconMode.BoxItemsLeave, IconMode.FencesOnly];

    public static string Label(IconMode mode) => mode switch
    {
        IconMode.BoxItemsLeave => "Kutulara eklediklerim masaüstünden kalksın",
        IconMode.FencesOnly => "Yalnızca bölmelerde göster",
        _ => "Hepsi masaüstünde görünsün",
    };

    public static string Description(IconMode mode) => mode switch
    {
        IconMode.BoxItemsLeave =>
            "Windows simgeleri görünür. Kısayol kutusuna eklediğin masaüstü öğesi masaüstünden kalkar: masaüstünün yanındaki " +
            $"{Core.BoxPlan.RootFolderName} klasörüne taşınır ve kutuda durur. Masaüstüne sonradan gelenler görünür kalır; " +
            "kutudan çıkarınca masaüstüne geri döner.",
        IconMode.FencesOnly =>
            "Masaüstündeki her şey bölmelerde toplanır; Windows'un kendi simgeleri gizlenir. Dosyalarına dokunulmaz; " +
            "kapatınca simgeler geri gelir.",
        _ => "Windows'un masaüstü simgeleri her zamanki gibi görünür; kutular ve bölmeler dosyalara dokunmadan kısayol gösterir.",
    };

    /// <summary>"Yeni widget'ların yeri" seçenekleri (Ayarlar ve Widget'lar sayfası).</summary>
    public static string PlaceLabel(Core.PlaceMode mode) => mode switch
    {
        Core.PlaceMode.Center => "Etkin ekranın ortasına",
        Core.PlaceMode.Corner => "Köşeye (türüne göre)",
        _ => "İmlecin yanına",
    };

    /// <summary>Ayarı yazar; açık sayfalar SettingsChanged ile güncellenir.</summary>
    public static void SetPlacement(Core.PlaceMode mode)
    {
        AppHost.Settings.NewWidgetPlacement = Core.PlaceModes.ToSetting(mode);
        AppHost.SaveSettings();
    }

    /// <summary>
    /// Seçimi uygular. Kutulara taşınmış öğeler varken bu kipten çıkılırsa masaüstüne geri konmaları önerilir; kipe
    /// girilirken kutularda duran masaüstü öğelerinin de taşınması önerilir (ikisi de sorulur, dosyaya sorulmadan dokunulmaz).
    /// near: bölmeler eklenecekse hangi ekrana. owner: soru penceresinin sahibi (yoksa ekranın ortasında, en üstte).
    /// </summary>
    public static void Set(IconMode mode, NativeMethods.POINT? near, Window? owner)
    {
        var current = Current;
        if (mode == current) return;

        if (current == IconMode.BoxItemsLeave && BoxMover.MovedCount is var moved and > 0 &&
            Confirm.Ask(owner, "Kutulardaki öğeler masaüstüne geri konsun mu?",
                $"{moved} öğe kutuya eklendiği için {BoxMover.Root} klasöründe duruyor. Geri konunca masaüstünde yine görünür " +
                "ve kutularda kalır. \"Klasörde kalsın\" dersen kutular onları oradan açmaya devam eder.",
                "Masaüstüne geri koy", danger: false, cancelText: "Klasörde kalsın"))
            BoxMover.ReturnAll();

        switch (mode)
        {
            case IconMode.FencesOnly:
                // Kutu kipi bilerek kapanır: bölmeler sonradan kapanınca dosya taşıma sessizce geri gelmesin.
                AppHost.Settings.BoxItemsLeaveDesktop = false;
                DesktopFences.TurnOnWithNotice(allStarters: false, near);
                break;

            case IconMode.BoxItemsLeave:
                AppHost.Settings.BoxItemsLeaveDesktop = true;
                if (AppHost.Settings.FencesReplaceIcons) DesktopFences.TurnOff();
                else AppHost.SaveSettings();
                var existing = BoxMover.DesktopItemsInBoxes();
                var count = existing.Sum(e => e.Paths.Count);
                if (count > 0 && Confirm.Ask(owner, "Kutulardaki masaüstü öğeleri de taşınsın mı?",
                        $"Kutularında masaüstünde duran {count} öğe var. Taşınırsa masaüstünden kalkar, {BoxMover.Root} klasöründe " +
                        "durur ve kutularda görünmeye devam eder.",
                        "Taşı", danger: false, cancelText: "Şimdi değil"))
                    BoxMover.ClaimMany(existing);
                else
                    Notice.Show("Bundan sonra kısayol kutusuna eklediğin masaüstü öğeleri masaüstünden kalkar ve kutuda durur.", NoticeKind.Info);
                break;

            default:
                AppHost.Settings.BoxItemsLeaveDesktop = false;
                if (AppHost.Settings.FencesReplaceIcons)
                {
                    DesktopFences.TurnOff();
                    Notice.Show("Masaüstü simgeleri yeniden gösteriliyor. Bölmelerin yerinde duruyor.", NoticeKind.Info);
                }
                else AppHost.SaveSettings();
                break;
        }
    }
}
