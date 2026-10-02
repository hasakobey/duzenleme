using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 "Kutuya koyduğun simge masaüstünden kalkar" (BoxMover, BoxEngine): taşınamayan, geri konamayan ve Windows'un onay
// vermediği öğelerin bildirimleri. Terimler: kısayol kutusu = shortcut box, halka = ring, herkesin masaüstü (Ortak
// Masaüstü) = everyone's desktop.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddBoxLeave() => Add(
    [
        // Taşıma (kutuya ya da halkaya eklenince)
        new("\"{0}\" açık olduğu için masaüstünden kalkamadı; kutuya eklendi, sonra yeniden denenecek.",
            "\"{0}\" is open, so it couldn't leave the desktop. It was added to the box and will be tried again later."),
        new("\"{0}\" açık olduğu için masaüstünden kalkamadı; halkaya eklendi, sonra yeniden denenecek.",
            "\"{0}\" is open, so it couldn't leave the desktop. It was added to the ring and will be tried again later."),
        new("Klasörler masaüstünden kalkmaz; \"{0}\" kutuda kısayol olarak duruyor.",
            "Folders don't leave the desktop; \"{0}\" stays in the box as a shortcut."),
        new("Klasörler masaüstünden kalkmaz; \"{0}\" halkada kısayol olarak duruyor.",
            "Folders don't leave the desktop; \"{0}\" stays in the ring as a shortcut."),
        new("Tekrar dene", "Try again"),
        new("kullanımda", "in use"),

        // Geri koyma
        new("\"{0}\" masaüstüne konamadı ({1}); {2} klasöründe duruyor.",
            "Couldn't put \"{0}\" back on the desktop ({1}); it's still in the {2} folder."),
        new("\"{0}\" zaten masaüstünde.", "\"{0}\" is already on the desktop."),
        new("\"{0}\" {1} klasöründe bulunamadı; taşınmış ya da silinmiş olabilir.",
            "Couldn't find \"{0}\" in the {1} folder. It may have been moved or deleted."),

        // Herkesin masaüstü (yalnızca yönetici yazar)
        new("Windows izin vermediği için \"{0}\" senin masaüstüne kondu.",
            "Windows didn't give permission, so \"{0}\" was put on your own desktop."),
        new("Windows izin vermediği için {0} simge senin masaüstüne kondu.",
            "Windows didn't give permission, so {0} icon was put on your own desktop.",
            "Windows didn't give permission, so {0} icons were put on your own desktop."),
        new("Herkesin masaüstüne taşı", "Move to everyone's desktop"),
        new("\"{0}\" herkesin masaüstüne kondu.", "\"{0}\" was put on everyone's desktop."),
        new("Kutudan çıkan {0} simge herkesin masaüstüne geri konacak; Windows yönetici izni ister.",
            "{0} icon taken out of a box goes back to everyone's desktop; Windows asks for administrator permission.",
            "{0} icons taken out of boxes go back to everyone's desktop; Windows asks for administrator permission."),
        new("Geri koy", "Put back"),
        new("Benim masaüstüme koy", "Put on my desktop"),
    ]);
}
