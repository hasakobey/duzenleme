using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Kolay NestDesk, Dalga 3.4: üç adımlı karşılama (bölmeler, otomatik taşıma, NestDesk halkası), "Hazır." bildirimi ve
// Ayarlar'daki Halka menü satırı (çift tıklama anahtarı). Terim: Halka menü = ring menu, bölme = panel.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddWelcome22() => Add(
    [
        // 1. adım
        new("Masaüstünü toparlayalım mı?", "Want to tidy up your desktop?"),
        new("Simgeler bölmelerde görünür. İstediğinde Ayarlar'dan geri alırsın.",
            "Icons appear in panels. You can undo this anytime in Settings."),
        new("Hayır, simgeler kalsın", "No, keep my icons"),

        // 3. adım: halka
        new("{0} halkan", "Your {0} ring"),
        new("Halkada yeni not, widget ekleme ve masaüstüne göz atma hazır. Kendi uygulamalarını sonra eklersin.",
            "The ring already has a new note, add widget and peek at desktop. You can add your own apps later."),
        new("Boş masaüstüne çift tıklayınca açılsın", "Open when I double-click the empty desktop"),
        new("Bir tuşla da açılsın", "Also open with a key"),
        new("Halka {0} ile imlecin yanında açılır. {1} klavyeni izlemez: yalnızca bu kısayol Windows'a kaydedilir.",
            "The ring opens next to the pointer with {0}. {1} doesn't watch your keyboard: only this shortcut is registered with Windows."),
        new("Masaüstüne ekle:", "Add to the desktop:"),
        new("Tepside sessizce başlar; bölmeler ve otomatik taşıma hep hazır.",
            "Starts quietly in the notification area. Panels and auto-move stay ready."),
        new("Kapalıyken simgelerin, {0}'i açana dek masaüstünde görünür.",
            "While off, your icons stay on the desktop until you open {0}."),

        // "Bitti"den sonra
        new("Hazır. Halkayı açmak için boş masaüstüne çift tıkla.", "All set. Double-click the empty desktop to open the ring."),
        new("Hazır. Halkayı açmak için {0} tuşlarına bas.", "All set. Press {0} to open the ring."),
        new("Hazır. Halkayı tepsideki {0} simgesine sağ tıklayıp \"Halkayı aç\" ile açabilirsin.",
            "All set. To open the ring, right-click the {0} icon in the notification area and select \"Open the ring\"."),
        new("Halka tuşu: {0}", "Ring key: {0}"),
        new("Şimdi göster", "Show me now"),
        new("Halkayı görmek için buraya tıkla.", "Click here to see the ring."),

        // Ayarlar > Halka menü
        new("Boş masaüstüne çift tıkla ya da {0} tuşlarına bas", "Double-click the empty desktop or press {0}"),
        new("Boş masaüstüne çift tıklayınca açılır", "Opens when you double-click the empty desktop"),
        new("Yalnızca tepsiden açılır", "Opens only from the notification area"),
        new("Halkayı masaüstüne bir widget olarak da koyabilirsin.", "You can also put a ring on the desktop as a widget."),
    ]);
}
