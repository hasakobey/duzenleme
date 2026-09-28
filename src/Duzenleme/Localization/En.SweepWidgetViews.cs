using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.1 yerelleştirme taraması, widget görünümleri: Widgets/{FenceView, LauncherView, NoteView, TileStyles.xaml, BoxMover,
// ShellIcons}, Core/{BoxMoves, AppSettings}, Desktop/{DesktopSystemIcons, ShellFileOperations}. Önceki paketlerde çevrilmiş
// ortak metinler (Aç, Geri al, Yeniden adlandır, Widget'tan kaldır, {0} öğe…) kendi tablolarında kalır.
// Terimler: bölme = panel, kısayol kutusu = shortcut box.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddSweepWidgetViews() => Add(
    [
        // Ortak kaldırma düğmesi (×), her widget'ta
        new("Widget'ı kaldır", "Remove widget"),
        new("Widget'ı kaldır (geri getirilebilir)", "Remove widget (you can bring it back)"),

        // Bölme (FenceView): durumlar ve arama
        new("Ara…  (Enter: aç)", "Search…  (Enter: open)"),
        new("Bırak, klasöre taşınsın", "Drop to move into the folder"),
        new("Masaüstü klasörü bulunamadı.", "Couldn't find the desktop folder."),
        new("Masaüstü şu an okunamıyor.", "Can't read the desktop right now."),
        new("Masaüstünde \"{0}\" klasörü yok.", "There's no \"{0}\" folder on the desktop."),
        new("\"{0}\" şu an okunamıyor.", "Can't read \"{0}\" right now."),
        new("\"{0}\" bulunamadı.", "Couldn't find \"{0}\"."),
        new("Masaüstünde kısayol yok.", "There are no shortcuts on the desktop."),
        new("Masaüstünde dosya kalmadı.\nHepsi yerli yerinde!", "No files left on the desktop.\nEverything is in its place!"),
        new("Masaüstü boş.", "The desktop is empty."),
        new("Klasör boş.\nDosyaları buraya sürükle.", "This folder is empty.\nDrag files here."),

        // Bölme: öğe menüsü
        new("Dosyaya dokunulmaz, yalnızca bu widget'ta görünmez.\nGeri getirmek için: widget'a sağ tık → Gizlenen öğeler",
            "The file isn't touched; it's just hidden in this widget.\nTo bring it back: right-click the widget → Hidden items"),
        new("Bölme ayarları…", "Panel settings…"),
        new("Klasörde göster", "Show in folder"),
        new("Bu klasörü ayrı bölme yap", "Make this folder its own panel"),
        new("Masaüstüne geri taşı", "Move back to the desktop"),

        // Kısayol kutusu (LauncherView)
        new("Uygulama, kısayol, dosya ya da klasörü buraya sürükle. Tek tıkla açılır.",
            "Drag an app, shortcut, file or folder here. It opens with a single click."),
        new("Bırak, kısayol olarak eklensin", "Drop to add as a shortcut"),
        new("Sağ tık: yeniden adlandır, taşı, sil", "Right-click to rename, move or delete"),
        new("Sola taşı", "Move left"),
        new("Sağa taşı", "Move right"),
        new("Sekmeyi sil", "Delete tab"),
        new("Sekme ekle…", "Add tab…"),
        new("Sekmeye taşı", "Move to tab"),
        new("\"{0}\" kutudan çıkarıldı ve masaüstüne geri konuyor.", "\"{0}\" was removed from the box and is going back to the desktop."),
        new("Geri almak için buraya tıkla.", "Click here to undo."),
        new("Geri al: \"{0}\" listeye dönsün", "Undo: put \"{0}\" back in the list"),
        new("Masaüstüne geri koy", "Put back on the desktop"),
        new("Öğe masaüstüne döner ve kutuda kalır.", "The item goes back to the desktop and stays in the box."),
        new("Kutudan çıkar (masaüstüne döner)", "Remove from box (goes back to the desktop)"),
        new("Öğe masaüstüne geri konur ve kutudan çıkar.\nGeri almak için: kutuya sağ tık → Geri al",
            "The item goes back to the desktop and leaves the box.\nTo undo: right-click the box → Undo"),
        new("Yalnızca kısayol kutudan çıkar; dosyaya dokunulmaz.\nGeri almak için: kutuya sağ tık → Geri al",
            "Only the shortcut leaves the box; the file isn't touched.\nTo undo: right-click the box → Undo"),
        new("Masaüstünden kaldır (kutuya taşı)", "Remove from desktop (move to the box)"),
        new("Öğe {0} klasörüne taşınır ve kutuda durur.", "The item moves to the {0} folder and stays in the box."),
        new("Yönetici olarak çalıştır", "Run as administrator"),
        new("Dosya konumunu aç", "Open file location"),
        new("Kutu ayarları…", "Box settings…"),
        new("Kısayol kutusuna ekle", "Add to shortcut box"),
        new("Uygulamalar ve kısayollar|*.exe;*.lnk;*.url;*.appref-ms;*.bat;*.cmd|Tüm dosyalar|*.*",
            "Apps and shortcuts|*.exe;*.lnk;*.url;*.appref-ms;*.bat;*.cmd|All files|*.*"),

        // Not ve yapılacaklar (NoteView)
        new("Bir şeyler yaz…", "Write something…"),
        new("Yeni madde ekle…", "Add an item…"),
        new("Yeni madde ekle", "Add an item"),
        new("Madde", "Item"),
        new("Not ayarları…", "Note settings…"),
        new("Liste ayarları…", "List settings…"),
        new("Bitenleri temizle ({0})", "Clear checked items ({0})"),
        new("Geri al: temizlenen maddeler geri gelsin", "Undo: bring back cleared items"),
        new("Düz nota çevir", "Convert to plain note"),
        new("Onay kutulu listeye çevir", "Convert to checklist"),
        new("Notu temizle", "Clear note"),
        new("Panoya kopyala", "Copy to clipboard"),
        new("Maddeyi sil", "Delete item"),
        new("Kes", "Cut"),
        new("Kopyala", "Copy"),
        new("Yapıştır", "Paste"),
        new("Tümünü seç", "Select all"),

        // Kutulara taşıma bildirimleri (BoxMover)
        new("izin yok", "access denied"),
        new("\"{0}\" masaüstünden kalktı; {1} klasörüne taşındı, kutuda duruyor.",
            "\"{0}\" left the desktop: it was moved to the {1} folder and stays in the box."),
        new("{0} öğe masaüstünden kalktı; {1} klasörüne taşındı, kutuda duruyor.",
            "{0} item left the desktop: it was moved to the {1} folder and stays in the box.",
            "{0} items left the desktop: they were moved to the {1} folder and stay in the box."),
        new("\"{0}\" taşınamadı ({1}); kutuya bağlantı olarak eklendi.",
            "Couldn't move \"{0}\" ({1}), so it was added to the box as a link."),
        new("{0} öğe taşınamadı (kullanımda ya da izin yok); kutuya bağlantı olarak eklendi.",
            "Couldn't move {0} item (in use or access denied), so it was added to the box as a link.",
            "Couldn't move {0} items (in use or access denied), so they were added to the box as links."),
        new("\"{0}\" klasörünü bir kural ya da bölme kullandığı için masaüstünde kaldı.",
            "\"{0}\" stayed on the desktop because a rule or panel uses this folder."),
        new("Ortak masaüstündeki öğeler bu bilgisayardaki tüm hesaplarda görünür; kutuya yalnızca bağlantı olarak eklendi (Ayarlar > Masaüstü'nden değiştirilebilir).",
            "Items on the Public Desktop appear in every account on this PC, so they were added to the box only as links (you can change this in Settings > Desktop)."),
        new("\"{0}\" masaüstüne geri kondu.", "\"{0}\" was put back on the desktop."),
        new("{0} öğe masaüstüne geri kondu.", "{0} item was put back on the desktop.", "{0} items were put back on the desktop."),
        new("{0} öğe geri konamadı ({1}); {2} klasöründe duruyor.",
            "Couldn't put back {0} item ({1}); it's still in the {2} folder.",
            "Couldn't put back {0} items ({1}); they're still in the {2} folder."),

        // Kutu klasörünün adı (BoxPlan.FolderNameFor): aygıt adı (CON, NUL…) tek başına klasör adı olamaz
        new("{0} kutusu", "{0} box"),

        // Kabuk dosya işlemi hatası (bölmeye bırakma, Geri Dönüşüm Kutusu'na gönderme)
        new("\"{0}\" için dosya işlemi başarısız oldu (kod 0x{1:X}).", "The file operation on \"{0}\" failed (code 0x{1:X})."),

        // Windows masaüstü simgeleri (DesktopSystemIcons; bölme kutucuğu ve Ayarlar'daki liste). Adlar Windows'unkiler.
        new("Bu Bilgisayar", "This PC"),
        new("Sürücüler ve cihazlar", "Drives and devices"),
        new("Silinen dosyalar", "Deleted files"),
        new("Kullanıcı dosyaları", "User's Files"),
        new("Belgeler, Resimler, İndirilenler…", "Documents, Pictures, Downloads…"),
        new("Ağ", "Network"),
        new("Ağdaki bilgisayarlar ve cihazlar", "Computers and devices on your network"),
        new("Denetim Masası", "Control Panel"),
        new("Klasik Windows ayarları", "Classic Windows settings"),
    ]);
}
