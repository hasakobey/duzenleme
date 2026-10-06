using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 "Kutuya koyduğun simge masaüstünden kalkar; çıkarınca geri gelir" (BoxMover, BoxEngine, BoxChoices, DesktopModes,
// Ayarlar'daki satır, öğe menüleri, bırakma kaplamaları): sorular, bildirimler ve açıklamalar. Terimler: kısayol kutusu =
// shortcut box (kısaca box), halka = ring, bölme = panel, herkesin masaüstü (Ortak Masaüstü) = everyone's desktop, simge = icon.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddBoxLeave() => Add(
    [
        // Ayarlar > Masaüstü (SettingsPage, DesktopModes)
        new("Kutuya koyduğum simgeler masaüstünden kalksın", "Icons I put in boxes leave the desktop"),
        new("Kutudaki ve halkadaki simgeler masaüstünde de görünür.", "Icons in boxes and rings also stay on the desktop."),
        new("Kutuya ya da halkaya koyduğun masaüstü simgesi masaüstünden kalkar; çıkarınca geri gelir. Silinmez, {0} klasöründe durur.",
            "A desktop icon you put in a box or ring leaves the desktop; take it out and it comes back. It isn't deleted; it's kept in the {0} folder."),
        new("Kutuya ya da halkaya koyduğun simge bölmelerde görünmez; çıkarınca geri gelir. Dosyalara dokunulmaz.",
            "An icon you put in a box or ring doesn't show in your panels; take it out and it comes back. Your files aren't touched."),
        new("Masaüstün OneDrive'da: kalkan simgeler öbür bilgisayarlarında da masaüstünden kalkar.",
            "Your desktop is in OneDrive: icons that leave it also leave the desktop on your other PCs."),
        new("Windows'un masaüstü simgeleri her zamanki gibi görünür; bölmeler dosyalara dokunmadan kısayol gösterir.",
            "Windows desktop icons show as usual; panels show shortcuts without touching your files."),
        new("Henüz masaüstünden kalkan simge yok. Kalkanlar {0} klasöründe durur.",
            "No icons have left the desktop yet. Icons that leave are kept in the {0} folder."),
        new("{0} simge kutularda; dosyalar {1} klasöründe.",
            "{0} icon is in a box; the file is in the {1} folder.",
            "{0} icons are in boxes; the files are in the {1} folder."),
        new("Herkesin masaüstündeki simgeler de kalksın", "Icons on everyone's desktop leave too"),
        new("Çoğu uygulamanın kısayolu bu bilgisayardaki herkesin masaüstündedir. Açarsan onlar da herkesin masaüstünden kalkar; Windows bir kez yönetici izni ister.",
            "Most apps put their shortcuts on the desktop of everyone on this PC. If you turn this on, those leave everyone's desktop too; Windows asks for administrator permission once."),
        new("Uygulama eklenince masaüstündeki kısayolu da kalksın", "When I add an app, its desktop shortcut leaves too"),
        new("Kutuya ya da halkaya bir uygulama eklediğinde masaüstünde aynı uygulamanın kısayolu varsa o da kalkar.",
            "When you add an app to a box or ring and the same app has a shortcut on the desktop, the shortcut leaves too."),

        // Anahtar ya da bölmeler değişince (DesktopModes)
        new("Kutudaki simgeler de masaüstünden kalksın mı?", "Remove the icons in your boxes from the desktop too?"),
        new("Kutularında ve halkalarında masaüstünde de duran {0} simge var. Masaüstünden kalkar, {1} klasöründe durur ve kutularda görünmeye devam eder.",
            "Your boxes and rings have {0} icon that's also on the desktop. It leaves the desktop, is kept in the {1} folder and still shows in its box.",
            "Your boxes and rings have {0} icons that are also on the desktop. They leave the desktop, are kept in the {1} folder and still show in your boxes."),
        new("Herkesin masaüstündeki {0} simge kalır; onlar için ayrı izin gerekir.",
            "{0} icon on everyone's desktop stays; it needs separate permission.",
            "{0} icons on everyone's desktop stay; they need separate permission."),
        new("Herkesin masaüstündeki simgeler de kalksın mı?", "Remove icons from everyone's desktop too?"),
        new("Kutularında herkesin masaüstünde duran {0} simge var. Kaldırırsam bu bilgisayardaki herkesin masaüstünden kalkar; Windows bir kez yönetici izni ister.",
            "Your boxes have {0} icon that's on everyone's desktop. If I remove it, it leaves the desktop for everyone on this PC; Windows asks for administrator permission once.",
            "Your boxes have {0} icons that are on everyone's desktop. If I remove them, they leave the desktop for everyone on this PC; Windows asks for administrator permission once."),
        new("Seçmek için tıkla.", "Click here to choose."),

        // İlk soru (kutuya ya da halkaya ilk masaüstü simgesi konunca)
        new("Masaüstündeki simge de kalksın mı?", "Remove the icon from the desktop too?"),
        new("\"{0}\" kutuya eklendi. İstersen masaüstünden kalkar: silinmez, {1} klasörüne konur ve kutudan açılır. Kutudan çıkarınca masaüstüne geri döner. Bundan sonra eklediğin simgeler için de böyle olur.",
            "\"{0}\" was added to the box. If you like, it can leave the desktop: it isn't deleted, it goes into the {1} folder and opens from the box. Take it out of the box and it goes back to the desktop. Icons you add later work the same way."),
        new("\"{0}\" halkaya eklendi. İstersen masaüstünden kalkar: silinmez, {1} klasörüne konur ve halkadan açılır. Halkadan çıkarınca masaüstüne geri döner. Bundan sonra eklediğin simgeler için de böyle olur.",
            "\"{0}\" was added to the ring. If you like, it can leave the desktop: it isn't deleted, it goes into the {1} folder and opens from the ring. Take it out of the ring and it goes back to the desktop. Icons you add later work the same way."),
        new("\"{0}\" kutuya eklendi. İstersen bölmelerde görünmez, yalnızca kutuda durur; dosyaya dokunulmaz. Kutudan çıkarınca bölmede yeniden görünür. Bundan sonra eklediğin simgeler için de böyle olur.",
            "\"{0}\" was added to the box. If you like, it won't show in your panels, only in the box; the file isn't touched. Take it out of the box and it shows in the panel again. Icons you add later work the same way."),
        new("\"{0}\" halkaya eklendi. İstersen bölmelerde görünmez, yalnızca halkada durur; dosyaya dokunulmaz. Halkadan çıkarınca bölmede yeniden görünür. Bundan sonra eklediğin simgeler için de böyle olur.",
            "\"{0}\" was added to the ring. If you like, it won't show in your panels, only in the ring; the file isn't touched. Take it out of the ring and it shows in the panel again. Icons you add later work the same way."),
        new("Evet, kalksın", "Yes, remove it"),
        new("Hayır, kalsın", "No, keep it"),

        // Herkesin masaüstü (yalnızca yönetici yazar)
        new("Herkesin masaüstündeki simge de kalksın mı?", "Remove it from everyone's desktop too?"),
        new("\"{0}\" bu bilgisayardaki herkesin masaüstünde. Kaldırırsam herkesin masaüstünden kalkar ve {1} klasörüne konur; Windows bunun için bir kez yönetici izni ister. Kutudan çıkarınca geri döner.",
            "\"{0}\" is on the desktop of everyone on this PC. If I remove it, it leaves everyone's desktop and goes into the {1} folder; Windows asks for administrator permission once. Take it out of the box and it comes back."),
        new("\"{0}\" bu bilgisayardaki herkesin masaüstünde. Kaldırırsam herkesin masaüstünden kalkar ve {1} klasörüne konur; Windows bunun için bir kez yönetici izni ister. Halkadan çıkarınca geri döner.",
            "\"{0}\" is on the desktop of everyone on this PC. If I remove it, it leaves everyone's desktop and goes into the {1} folder; Windows asks for administrator permission once. Take it out of the ring and it comes back."),
        new("Masaüstünde kalsın", "Keep on desktop"),
        new("\"{0}\" bu bilgisayardaki herkesin masaüstünde; Store sürümü onu taşımaz. Simgeleri bölmelerde toplarsan yalnızca kutuda görünür.",
            "\"{0}\" is on the desktop of everyone on this PC, and the Store version doesn't move it. If you gather your icons into panels, it only shows in the box."),
        new("Bölmelerde topla", "Gather into panels"),
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

        // Uygulamanın masaüstündeki kısayolu
        new("Masaüstündeki kısayolu da kalksın mı?", "Remove its desktop shortcut too?"),
        new("Masaüstünde \"{0}\" uygulamasının bir kısayolu da var. O da masaüstünden kalkar ve {1} klasörüne konur; uygulamayı kutudan ya da halkadan çıkarınca geri gelir. Bundan sonra eklediğin uygulamalar için de böyle olur.",
            "There's also a shortcut to \"{0}\" on the desktop. It leaves the desktop too and goes into the {1} folder; take the app out of the box or ring and it comes back. Apps you add later work the same way."),
        new("Masaüstünde \"{0}\" uygulamasının bir kısayolu da var. O da bölmelerde görünmez, dosyaya dokunulmaz; uygulamayı kutudan ya da halkadan çıkarınca yeniden görünür. Bundan sonra eklediğin uygulamalar için de böyle olur.",
            "There's also a shortcut to \"{0}\" on the desktop. It won't show in your panels either, and the file isn't touched; take the app out of the box or ring and it shows again. Apps you add later work the same way."),
        new("Evet, hep böyle yap", "Yes, always do this"),

        // Taşıma (kutuya ya da halkaya eklenince)
        new("\"{0}\" masaüstünden kalktı, kutuda duruyor.", "\"{0}\" left the desktop; it's in the box."),
        new("\"{0}\" masaüstünden kalktı, halkada duruyor.", "\"{0}\" left the desktop; it's in the ring."),
        new("{0} simge masaüstünden kalktı.", "{0} icon left the desktop.", "{0} icons left the desktop."),
        new("\"{0}\" artık bölmelerde görünmüyor, kutuda duruyor.", "\"{0}\" no longer shows in your panels; it's in the box."),
        new("\"{0}\" artık bölmelerde görünmüyor, halkada duruyor.", "\"{0}\" no longer shows in your panels; it's in the ring."),
        new("{0} simge artık bölmelerde görünmüyor.", "{0} icon no longer shows in your panels.", "{0} icons no longer show in your panels."),
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
        new("klasöre şu an ulaşılamıyor", "the folder can't be reached right now"),
        new("beklenmeyen bir dosya hatası", "unexpected file error"),

        // Öğe menüsü (BoxChoices.MenuRows; kutu ve halka)
        new("Simge masaüstüne geri döner.", "The icon goes back to the desktop."),
        new("Başka bir kutuda ya da halkada da duruyor; yalnızca buradan kalkar.", "It's also in another box or ring; it's only removed from here."),
        new("Yalnızca halkadan çıkar; dosyaya dokunulmaz.", "It's only removed from the ring; the file isn't touched."),
        new("Simge masaüstünden kalkar, kutuda durur.", "The icon leaves the desktop and stays in the box."),
        new("Simge masaüstünden kalkar, halkada durur.", "The icon leaves the desktop and stays in the ring."),
        new("Masaüstünden kaldır (yönetici izni)", "Remove from desktop (administrator permission)"),
        new("Bu bilgisayardaki herkesin masaüstünden kalkar; Windows bir kez yönetici izni ister.",
            "It leaves the desktop for everyone on this PC; Windows asks for administrator permission once."),

        // Kaldırma ve geri koyma
        new("\"{0}\" kutudan kaldırıldı.", "\"{0}\" was removed from the box."),
        new("\"{0}\" kutudan kaldırıldı; başka bir kutuda ya da halkada da durduğu için masaüstüne dönmedi.",
            "\"{0}\" was removed from the box. It's also in another box or ring, so it didn't go back to the desktop."),
        new("\"{0}\" halkadan kaldırıldı; başka bir kutuda ya da halkada da durduğu için masaüstüne dönmedi.",
            "\"{0}\" was removed from the ring. It's also in another box or ring, so it didn't go back to the desktop."),
        new("Masaüstünden kalkan {0} simge geri dönüyor.",
            "{0} icon that left the desktop is going back.", "{0} icons that left the desktop are going back."),
        new("\"{0}\" masaüstüne geri döndü.", "\"{0}\" is back on the desktop."),
        new("{0} simge masaüstüne geri döndü.", "{0} icon is back on the desktop.", "{0} icons are back on the desktop."),
        new("\"{0}\" kutudan çıktı.", "\"{0}\" left the box."),
        new("\"{0}\" halkadan çıktı.", "\"{0}\" left the ring."),
        new("\"{0}\" masaüstüne konamadı ({1}); {2} klasöründe duruyor.",
            "Couldn't put \"{0}\" back on the desktop ({1}); it's still in the {2} folder."),
        new("\"{0}\" zaten masaüstünde.", "\"{0}\" is already on the desktop."),
        new("\"{0}\" {1} klasöründe bulunamadı; taşınmış ya da silinmiş olabilir.",
            "Couldn't find \"{0}\" in the {1} folder. It may have been moved or deleted."),

        // Bırakma kaplamaları
        new("Bırak, kutuya girsin; masaüstünden kalkar", "Drop to add to the box; it leaves the desktop"),
        new("Bırak, kutuya girsin; bölmeden kalkar", "Drop to add to the box; it leaves the panel"),
        new("Bırak, halkaya girsin; masaüstünden kalkar", "Drop to add to the ring; it leaves the desktop"),
        new("Bırak, halkaya girsin; bölmeden kalkar", "Drop to add to the ring; it leaves the panel"),
        new("Bırak, bu kutudan çıkarılsın", "Drop to take it out of this box"),

        // NestDesk kökündeki açıklama dosyası (BoxPlan.BreadcrumbText)
        new("Bu klasör nedir.txt", "What is this folder.txt"),
        new("Bu klasördeki dosyalar, {0}'te kutulara ve halkalara koyduğun masaüstü simgeleridir. {0} açıkken bir simgeyi kutudan çıkarınca ya da Ayarlar → Masaüstü → \"Hepsini masaüstüne geri koy\" ile masaüstüne geri döner. {0}'i kaldırdıysan dosyaları aşağıdaki eski klasörlerine kendin taşıyabilirsin; {0} onları silmez.",
            "The files in this folder are desktop icons you put in boxes and rings in {0}. While {0} is running, an icon goes back to the desktop when you take it out of its box, or with Settings → Desktop → \"Put all back on the desktop\". If you uninstalled {0}, you can move the files back to their old folders listed below yourself; {0} doesn't delete them."),
        new("Simge ← eski klasörü:", "Icon ← its old folder:"),
    ]);
}
