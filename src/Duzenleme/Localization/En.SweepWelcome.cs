using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.1 yerelleştirme taraması, Welcome grubu: Views/{WelcomeWindow.xaml(.cs), WelcomeSetup, WidgetCatalog, QuickAddWindow,
// DesktopFences}, Core/Onboarding. Terimler: bölme = panel, Otomatik taşıma = Auto-move, tepsi = notification area.
// Ortak metinler (Klasörler, Kısayollar, Dosyalar, Vazgeç, Geri al, Widget ekle, Masaüstünde var…) En.Common.cs'te.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddSweepWelcome() => Add(
    [
        // Karşılama: pencere, adımlar, alt şerit
        new("{0} kurulumu", "{0} setup"),
        new("{0}'e hoş geldin", "Welcome to {0}"),
        new("Şimdilik atla", "Skip for now"),
        new("Masaüstünü yeniden kuralım", "Let's set up your desktop again"),
        new("Dosyalar kendiliğinden yerine gitsin mi?", "Should files move into place on their own?"),
        new("Masaüstüne küçük araçlar ekleyelim mi?", "Want to add some small tools to your desktop?"),
        new("Adım {0} / 3", "Step {0} of 3"),
        new("Geri", "Back"),
        new("İleri", "Next"),
        new("Bitti", "Done"),
        new("Devam etmek için bir seçenek işaretle.", "Choose an option to continue."),
        new("Şimdilik hayır", "Not now"),

        // 1. adım: bölmeler
        new("{0} masaüstünü derli toplu tutar: simgeleri bölmelerde toplar, gelen dosyaları klasörlerine taşır, saat ve not gibi küçük araçlar ekler.",
            "{0} keeps your desktop tidy: it gathers icons into panels, moves new files into their folders and adds small tools like a clock and notes."),
        new("Var olan bölmelerin, widget'ların ve dosyaların silinmez; yalnızca eksikler eklenir.",
            "Your existing panels, widgets and files won't be deleted; only what's missing is added."),
        new("Masaüstünü bölmelere ayıralım mı?", "Want to sort your desktop into panels?"),
        new("önce", "before"),
        new("sonra", "after"),
        new("Evet, bölmelere ayır", "Yes, sort into panels"),
        new("Önerilen", "Recommended"),
        new("Bu açıkken yeni klasör açmak ya da ad değiştirmek için bölmeye sağ tıkla.",
            "While this is on, right-click a panel to create a folder or rename something."),
        new("Masaüstün olduğu gibi kalır. Bölmeleri sonra \"Widget ekle\"den ekleyebilirsin.",
            "Your desktop stays as it is. You can add panels later from \"Add widget\"."),

        // Bölme planı (Onboarding.FencePlanText): her durum tam bir cümle
        new("Bölmelerin zaten hazır; yeni bölme eklenmez.", "Your panels are already set up; no new panels will be added."),
        new("{0} klasörü için bir bölme eklenir.", "Adds a panel for the {0} folder."),
        new("{0} klasörleri için birer bölme eklenir.", "Adds a panel for each of the {0} folders."),
        new("{0} için bir bölme eklenir.", "Adds a panel for {0}."),
        new("{0} için bir bölme eklenir; {1} klasörü için de bir bölme.", "Adds a panel for {0}, plus one for the {1} folder."),
        new("{0} için bir bölme eklenir; {1} klasörleri için de birer bölme.", "Adds a panel for {0}, plus one each for the {1} folders."),
        new("{0} için birer bölme eklenir.", "Adds panels for {0}."),
        new("{0} için birer bölme eklenir; {1} klasörü için de bir bölme.", "Adds panels for {0}, plus one for the {1} folder."),
        new("{0} için birer bölme eklenir; {1} klasörleri için de birer bölme.", "Adds panels for {0}, plus one each for the {1} folders."),

        // 2. adım: dosya taşıma
        new("Masaüstüne bir dosya geldiğinde türüne uygun klasöre taşınır: PDF'ler \"PDF\" klasörüne, fotoğraflar \"Resimler\" klasörüne… Klasörler masaüstünde durur.",
            "When a file lands on your desktop, it's moved to the folder for its type: PDFs to the \"PDF\" folder, photos to the \"Pictures\" folder… The folders stay on your desktop."),
        new("Hangi klasörler kullanılsın?", "Which folders should be used?"),
        new("İşaretlediğin klasörler masaüstünde yoksa oluşturulur.", "Folders you select are created on the desktop if they don't exist yet."),
        new("Henüz taşıma kuralı yok; kuralları {0}'teki \"Otomatik taşıma\" sayfasından ekleyebilirsin.",
            "There are no move rules yet; you can add them on the \"Auto-move\" page in {0}."),
        new("\"Klasör yoksa oluştur\" açık: işaretlediğin klasörler, oraya gidecek ilk dosya gelince oluşturulur.",
            "\"Create missing folders\" is on: the folders you select are created when the first file for them arrives."),
        new("Uzantılar: {0}", "Extensions: {0}"),
        new("kullanılmayacak", "won't be used"),
        new("masaüstünde var", "on the desktop"),
        new("oluşturulacak", "will be created"),
        new("Evet, dosyalarımı yerine taşı", "Yes, move my files into place"),
        new("Otomatik taşıma kapatılır; hiçbir dosyaya dokunulmaz.", "Auto-move is turned off; no files are touched."),
        new("Hiçbir dosyaya dokunulmaz. İstediğin zaman {0}'teki \"Otomatik taşıma\" sayfasından açabilirsin.",
            "No files are touched. You can turn it on anytime on the \"Auto-move\" page in {0}."),
        new("Yukarıdan klasör seçersen uygun dosyalar oraya taşınır.", "If you select folders above, matching files are moved there."),
        new("Masaüstündeki ve bundan sonra gelen uygun dosyalar seçtiğin klasörlere taşınır.",
            "Matching files on your desktop, and any that arrive later, are moved to the folders you selected."),
        new("Bundan sonra gelen dosyalar taşınır.", "Files that arrive from now on will be moved."),
        new("{0} dosya taşınacak.", "{0} file will be moved.", "{0} files will be moved."),
        new("Masaüstü inceleniyor…", "Checking your desktop…"),
        new("Hiç klasör seçmedin; hiçbir dosya taşınmaz.", "You haven't selected any folders, so no files will be moved."),
        new("Masaüstü okunamadı; taşınacak dosyalar şimdi gösterilemiyor.",
            "Couldn't read the desktop, so the files to be moved can't be shown right now."),
        new("Şu an masaüstünde taşınacak dosya yok. Bundan sonra gelen dosyalar taşınır.",
            "There are no files on your desktop to move right now. Files that arrive from now on will be moved."),
        new("Şu an masaüstünde duran {0} dosya da taşınacak:",
            "{0} file already on your desktop will also be moved:", "{0} files already on your desktop will also be moved:"),
        new("Her taşıma {0}'teki \"Otomatik taşıma\" sayfasından geri alınabilir; geri aldığın dosya bir daha taşınmaz. Kısayollar, klasörler ve inmekte olan dosyalar hiçbir zaman taşınmaz.",
            "You can undo any move on the \"Auto-move\" page in {0}, and a file you move back won't be moved again. Shortcuts, folders and files that are still downloading are never moved."),

        // Önizleme satırları (Onboarding.MovePreviewLines)
        new("{0} dosya → {1}", "{0} file → {1}", "{0} files → {1}"),
        new("+ {0} dosya daha, {1}", "+ {0} more file {1}", "+ {0} more files {1}"),
        new("{0} klasöre", "in {0} folder", "in {0} folders"),

        // 3. adım: araçlar ve başlangıç
        new("İstediklerine tıkla. Sonra sürükleyerek yerini değiştirebilir, sağ tıklayarak ayarlayabilirsin.",
            "Click the ones you want. Later you can drag them around and right-click to change their settings."),
        new("Hiçbirini seçmezsen masaüstüne araç eklenmez.", "If you don't pick any, no tools are added to your desktop."),
        new("Windows açılınca {0} de başlasın", "Start {0} when Windows starts"),
        new("Tepside sessizce başlar; bölmelerin, saatin ve otomatik taşıma hep hazır olur.",
            "Starts quietly in the notification area, so your panels, clock and auto-move are always ready."),
        new("Taşınabilir sürüm: program bu klasörden başlatılır; klasörü taşırsan bu ayarı yeniden aç.",
            "Portable version: the app starts from this folder. If you move the folder, turn this setting on again."),
        new("İpucu: {0} Windows ile başlamazsa masaüstü simgelerin, sen {0}'i açana dek her zamanki gibi görünür.",
            "Tip: If {0} doesn't start with Windows, your desktop icons appear as usual until you open {0}."),
        new("{0} saatin yanındaki simgede (tepside) çalışır. Yeni bir şey eklemek için {1} tuşlarına bas ya da o simgeye sağ tıkla → Widget ekle.",
            "{0} runs from the icon next to the clock (in the notification area). To add something new, press {1} or right-click that icon → Add widget."),
        new("{0} saatin yanındaki simgede (tepside) çalışır. Yeni bir şey eklemek için o simgeye sağ tıkla → Widget ekle.",
            "{0} runs from the icon next to the clock (in the notification area). To add something new, right-click that icon → Add widget."),

        // Karşılama uygulanırken (tepsi balonu)
        new("Bazı klasörler oluşturulamadı", "Some folders couldn't be created"),
        new("Windows ile başlatma değiştirilemedi", "Couldn't change the startup setting"),
        new("Ayarı Windows'un Başlangıç uygulamaları sayfasından değiştirebilirsin.", "You can change it in Settings > Apps > Startup."),

        // "Widget ekle" penceresi ve ekleme kataloğu
        new("Masaüstüne ne eklemek istersin?", "What would you like to add to your desktop?"),
        new("Birine tıkla, hemen masaüstüne gelsin. Sonra sürükleyerek taşı, kenarından büyüt, sağ tıklayarak ayarla.",
            "Click one and it appears on your desktop right away. Then drag to move it, drag an edge to resize it and right-click to adjust it."),
        new("Masaüstümü bölmelere ayır", "Sort my desktop into panels"),
        new("Klasörler, Kısayollar, Dosyalar ve PDF gibi klasörler için bölme kurar; masaüstü simgeleri yalnızca bölmelerde görünür",
            "Sets up panels for Folders, Shortcuts, Files and folders like PDF; desktop icons then appear only in panels"),
        new("Widget'ları yönet…", "Manage widgets…"),
        new("Geri almak için buraya tıkla.", "Click here to undo."),
        new("Uygulama kısayolları, Bu Bilgisayar, Geri Dönüşüm Kutusu", "App shortcuts, This PC, Recycle Bin"),
        new("Masaüstünde duran dosyalar", "Files on the desktop"),
        new("Tüm masaüstü", "Whole desktop"),
        new("Masaüstündeki her şey tek bölmede", "Everything on the desktop in one panel"),
        new("\"{0}\" klasörünün içi", "Contents of the \"{0}\" folder"),
        new("Masaüstünde \"{0}\" klasörü açılır; otomatik taşıma açıksa uygun dosyalar oraya taşınır",
            "Creates a \"{0}\" folder on the desktop; if auto-move is on, matching files are moved there"),
        new("yeni klasör", "new folder"),
        new("Diğer klasörler ({0})", "More folders ({0})"),

        // "Masaüstümü bölmelere ayır" / bölme kipi bildirimi (DesktopFences)
        new("{0} bölme eklendi. Masaüstü simgeleri artık yalnızca bölmelerde.",
            "{0} panel added. Desktop icons now appear only in panels.", "{0} panels added. Desktop icons now appear only in panels."),
        new("Masaüstü simgeleri artık yalnızca bölmelerde.", "Desktop icons now appear only in panels."),
        new("{0} bölme eklendi.", "{0} panel added.", "{0} panels added."),
        new("Bölmelerin zaten hazır; masaüstü simgeleri yalnızca bölmelerde.", "Your panels are already set up; desktop icons appear only in panels."),
    ]);
}
