using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.1 yerelleştirme taraması, taşıma grubu: Views/{HomePage, AutoMovePage, MoveActions, UiText}, Core/FileMover, TrayIcon.
// Ortak metinler (Ana sayfa, Otomatik taşıma, Şimdi düzenle, Geri al…) En.Common.cs'te. Terim: "bölme" = "panel",
// "Otomatik taşıma" = "Auto-move".
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddSweepMoves() => Add(
    [
        // Ana sayfa
        new("Saat, not, bölme…", "Clocks, notes, panels…"),
        new("Masaüstündeki dosyaları taşı", "Move desktop files to their folders"),
        new("Klasörleri ayarla", "Set up folders"),
        new("Son taşınanlar", "Recently moved"),
        new("Tümünü gör", "See all"),
        new("Henüz taşınan dosya yok. Masaüstüne düşen bir dosya klasörüne gidince burada görünür.",
            "No files have been moved yet. When a file that lands on the desktop goes to its folder, it shows up here."),
        new("Geri alınacak taşıma yok", "No moves to undo"),

        // Otomatik taşıma durum kartı (Ana sayfa ve Otomatik taşıma sayfası)
        new("Otomatik taşıma kapalı", "Auto-move is off"),
        new("Yeni dosyalar masaüstünde kalır. Açarsan masaüstündeki uygun dosyalar da klasörlerine taşınır; her taşıma geri alınabilir.",
            "New files stay on the desktop. If you turn it on, matching files already on the desktop also move to their folders; you can undo every move."),
        new("Otomatik taşıma açık", "Auto-move is on"),
        new("Bugün {0} dosya yerine taşındı · toplam {1}",
            "{0} file moved to its folder today · {1} in total", "{0} files moved to their folders today · {1} in total"),
        new("Masaüstüne düşen dosyalar klasörlerine gider.", "Files that land on the desktop go to their folders."),
        new("Masaüstünde henüz hedef klasör yok. Örneğin masaüstünde \"PDF\" adında bir klasör açınca PDF'ler oraya gider.",
            "There are no target folders on the desktop yet. For example, create a folder named \"PDF\" on the desktop and PDFs will go there."),

        // Otomatik taşıma sayfası
        new("Masaüstüne düşen dosyalar türüne uyan klasöre kendiliğinden taşınır. Her taşıma geri alınabilir.",
            "Files that land on the desktop move automatically to the folder for their type. You can undo every move."),
        new("Masaüstündeki uygun dosyaları şimdi klasörlerine taşır", "Moves matching files on the desktop to their folders now"),
        new("Hangi dosya nereye gitsin?", "Which files go where?"),
        new("KLASÖR", "FOLDER"),
        new("UZANTILAR", "EXTENSIONS"),
        new("Kuralı sil", "Delete rule"),
        new("Kural açık/kapalı", "Turn the rule on or off"),
        new("Uzantılar", "Extensions"),
        new("Klasörü masaüstünde açar; otomatik taşıma açıksa uygun dosyalar oraya taşınır",
            "Creates the folder on the desktop; if auto-move is on, matching files move there"),
        new("Hiç kural yok; bu yüzden hiçbir dosya taşınmaz.", "There are no rules, so no files will be moved."),
        new("Hazır kuralları ekle", "Add the default rules"),
        new("Kural ekle", "Add rule"),
        new("Klasör masaüstünde yoksa dosya olduğu yerde kalır. Büyük/küçük harf ve Türkçe karakter fark etmez (\"Arşivler\" = \"arsivler\"). Kısayollar, sistem dosyaları ve inmekte olan dosyalar hiç taşınmaz.",
            "If the folder isn't on the desktop, the file stays where it is. Folder names aren't case-sensitive (\"Archives\" = \"archives\"). Shortcuts, system files and files that are still downloading are never moved."),
        new("Henüz taşınan dosya yok. Masaüstüne düşen bir dosya klasörüne gidince burada görünür ve buradan geri alınabilir.",
            "No files have been moved yet. When a file that lands on the desktop goes to its folder, it shows up here and you can undo the move from here."),
        new("Nadiren gereken ayarlar", "Settings you'll rarely need"),
        new("Klasör yoksa oluştur", "Create missing folders"),
        new("Kapalıyken yalnızca senin masaüstünde oluşturduğun klasörler kullanılır.",
            "When this is off, only folders you've created on the desktop are used."),
        new("Kuralları varsayılana döndür", "Reset rules to defaults"),
        new("Eklediğin ya da değiştirdiğin kurallar silinir; hazır altı kural geri gelir.",
            "Rules you've added or changed are removed and the six default rules come back."),
        new("Geçmişi temizle", "Clear history"),
        new("Listedeki taşımalar silinir; dosyalar bulundukları klasörde kalır.",
            "The moves in the list are removed; files stay in the folders they're in."),
        new("Temizle", "Clear"),

        // Kural satırı
        new("Kapalı", "Off"),
        new("Klasör adı yok; hiçbir dosya taşınmaz", "No folder name; no files will be moved"),
        new("Uzantı yok; hiçbir dosya taşınmaz", "No extensions; no files will be moved"),
        new("Masaüstünde yok · {0} dosya bekliyor", "Not on the desktop · {0} file waiting", "Not on the desktop · {0} files waiting"),
        new("Masaüstünde yok", "Not on the desktop"),
        new("\"{0}\" kuralı", "\"{0}\" rule"),
        new("Kuralı sil: {0}", "Delete rule: {0}"),
        new("Klasörü oluştur: {0}", "Create folder: {0}"),

        // Otomatik taşıma sayfasının bildirimleri ve soruları
        new("\"{0}\" kuralı silindi.", "The \"{0}\" rule was deleted."),
        new("Kural silindi.", "The rule was deleted."),
        new("\"{0}\" klasörü oluşturulamadı: {1}", "Couldn't create the \"{0}\" folder: {1}"),
        new("\"{1}\" klasörü oluşturuldu. Otomatik taşıma kapalı olduğu için {0} dosya masaüstünde bekliyor.",
            "The \"{1}\" folder was created. Auto-move is off, so {0} file is still waiting on the desktop.",
            "The \"{1}\" folder was created. Auto-move is off, so {0} files are still waiting on the desktop."),
        new("Tümünü göster ({0})", "Show all ({0})"),
        new("Kurallar varsayılana dönsün mü?", "Reset rules to defaults?"),
        new("Eklediğin ya da değiştirdiğin kurallar silinir; {0} kuralları geri gelir. Dosyalarına dokunulmaz.",
            "Rules you've added or changed will be removed, and the {0} rules will come back. Your files won't be touched."),
        new("Kurallar varsayılana döndü.", "Rules were reset to defaults."),
        new("Geçmiş temizlensin mi?", "Clear history?"),
        new("Listedeki taşımalar silinir ve artık buradan geri alınamaz. Dosyaların bulundukları klasörde kalır. Geri aldığın dosyaların kaydı korunur; onlar yine taşınmaz.",
            "The moves in the list will be removed and can't be undone from here anymore. Your files stay in the folders they're in. Files you've undone are still remembered and won't be moved again."),
        new("Geçmiş temizlendi.", "History cleared."),

        // Taşıma komutları (MoveActions)
        new("Masaüstü düzenlenemedi: {0}", "Couldn't tidy up the desktop: {0}"),
        new("{0} dosya yerine taşındı.", "{0} file was moved to its folder.", "{0} files were moved to their folders."),
        new("Geçmişi gör", "View history"),
        new("Taşınacak dosya bulunamadı.", "There were no files to move."),
        new("Geri alınacak taşıma yok.", "There's no move to undo."),
        new("{0} masaüstüne döndü. Bir daha otomatik taşınmayacak.", "{0} is back on the desktop. It won't be moved automatically again."),
        new("Geri alınamadı: {0}", "Couldn't undo: {0}"),
        new("Dosya taşındığı klasörde artık yok.", "The file is no longer in the folder it was moved to."),

        // Taşıma listesi (UiText, MoveRow)
        new("az önce", "just now"),
        new("{0} dk önce", "{0} min ago"),
        new("bugün {0}", "today {0}"),
        new("dün {0}", "yesterday {0}"),
        new("{0}  →  Masaüstü (geri alındı)", "{0}  →  Desktop (undone)"),
        new("Geri al: {0}", "Undo: {0}"),

        // Tepsi simgesi: menü, balonlar, ipucu
        new("Widget ekle…", "Add widget…"),
        new("Masaüstünü gizle", "Hide desktop"),
        new("Son kaldırılan widget'ı geri getir", "Bring back the last removed widget"),
        new("Geri getir: {0}", "Bring back: {0}"),
        new("Çıkış", "Exit"),
        new("{0} dosya → {1}", "{0} file → {1}", "{0} files → {1}"),
        new("Dosya taşındı", "File moved"),
        new("{0} dosya düzenlendi", "{0} file organized", "{0} files organized"),
        new("“{0}” klasörüne simge ver", "Give the “{0}” folder an icon"),
        new("Önerilen: {0}. Seçmek için tıkla.", "Suggested: {0}. Click to choose."),
        new("Geri alınacak bir şey yok", "Nothing to undo"),
        new("Henüz taşınmış bir dosya yok.", "No files have been moved yet."),
        new("{0} masaüstüne döndü.", "{0} is back on the desktop."),
        new("Geri alınamadı", "Couldn't undo"),
        new("{0} — Windows masaüstüne göz atılıyor", "{0} — peeking at the Windows desktop"),
        new("{0} — otomatik taşıma kapalı", "{0} — auto-move is off"),
        new("{0} — otomatik taşıma açık", "{0} — auto-move is on"),
    ]);
}
