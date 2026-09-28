using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.1 yerelleştirme taraması, WidgetChrome grubu: Views/{WidgetsPage, DesktopModes, PeekBar}, Widgets/WidgetManager,
// Desktop/{DesktopDoubleClick, ShellDesktop}. Terim: "bölme" = "panel", "Kısayol kutusu" = "shortcut box". Ortak metinler
// (Widget'lar, Ekle, Kaldır, Arka plan, renkler…) En.Common/En.P4/En.P5/En.P6/En.P7'de.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddSweepWidgetChrome() => Add(
    [
        // Widget'lar sayfası
        new("Masaüstüne saat, not ya da bölme ekle. Eklediklerini aşağıda bulur, tek tıkla kaldırırsın.",
            "Add a clock, a note or a panel to your desktop. You'll find what you add below and can remove it with one click."),
        new("İpucu: Widget'ı sürükleyerek taşı, kenarından büyüt, sağ tıklayarak ayarla.",
            "Tip: Drag a widget to move it, drag its edge to resize it, and right-click it to change its settings."),
        new("Masaüstündekiler", "On your desktop"),
        new("Masaüstündekiler ({0})", "On your desktop ({0})"),
        new("Hepsini öne getir", "Bring all to front"),
        new("Widget'lar pencerelerin arkasında kalır; hepsini birkaç saniyeliğine öne getirir",
            "Widgets stay behind your windows; this brings them all to the front for a few seconds"),
        new("Düzenli yerleştir", "Arrange"),
        new("Widget'ları bulundukları ekranda çakışmadan sağdan başlayarak dizer; önceki yerleşim kaydedilir",
            "Lines up the widgets on their screen without overlaps, starting from the right; the previous layout is saved"),
        new("Bul", "Find"),
        new("Bul: {0}", "Find: {0}"),
        new("Kaldır: {0}", "Remove: {0}"),
        new("Masaüstündeki yerini göster", "Show where it is on the desktop"),
        new("Masaüstünde henüz widget yok. Yukarıdan birine tıkla, hemen masaüstüne gelsin.",
            "There are no widgets on your desktop yet. Click one above and it appears on your desktop right away."),
        new("Tüm widget'ların görünümü", "Appearance of all widgets"),
        new("Arka plan, renk ve yerleşim", "Background, color and placement"),
        new("Yeni widget'ların yeri", "Where new widgets appear"),
        new("Kısayolla ya da \"Widget ekle\" penceresiyle eklenenler. Bu sayfadan eklenenler ekranın köşesine gelir.",
            "For widgets added with a shortcut or the \"Add widget\" window. Widgets added from this page go to a corner of the screen."),
        new("Tek bir widget'ı ayarlamak için ona sağ tıkla → Görünüm. Notlar kendi kağıt rengini kullanır.",
            "To change a single widget, right-click it → Appearance. Notes use their own paper color."),
        new("Kayıtlı düzenler", "Saved layouts"),
        new("Kayıtlı düzenler ({0})", "Saved layouts ({0})"),
        new("Yerleşimi bir adla sakla, tek tıkla geri dön", "Save your layout under a name and go back to it with one click"),
        new("Düzen adı (ör. İş)", "Layout name (e.g. Work)"),
        new("Düzen adı", "Layout name"),
        new("Şu anki düzeni kaydet", "Save current layout"),
        new("Düzeni sil", "Delete layout"),
        new("Uygula: {0}", "Apply: {0}"),
        new("Düzeni sil: {0}", "Delete layout: {0}"),
        new("{0} widget · {1}", "{0} widget · {1}", "{0} widgets · {1}"),
        new("Henüz kayıtlı düzen yok. Beğendiğin yerleşimi bir adla kaydet, sonra tek tıkla geri dön.",
            "No saved layouts yet. Save a layout you like under a name, then go back to it with one click."),

        // Widget'lar sayfası: bildirimler
        new("\"{0}\" bölmesi eklendi; masaüstünde \"{0}\" klasörü de oluşturuldu.",
            "The \"{0}\" panel was added, and a \"{0}\" folder was created on the desktop."),
        new("Yerleştirilecek widget yok.", "There are no widgets to arrange."),
        new("{0} widget düzenli yerleştirildi. Önceki yerleşim Kayıtlı düzenler'de \"{1}\" adıyla duruyor.",
            "{0} widget was arranged. The previous layout is in Saved layouts as \"{1}\".",
            "{0} widgets were arranged. The previous layout is in Saved layouts as \"{1}\"."),
        new("{0} kaldırıldı.", "{0} was removed."),
        new("Kaydedilecek widget yok. Önce masaüstüne bir widget ekle.", "There are no widgets to save. Add a widget to your desktop first."),
        // Varsayılan düzen adı: kaydedilirken arayüz dilinde bir kez yazılır.
        new("Düzen {0}", "Layout {0}"),
        new("\"{0}\" düzeni kaydedildi.", "The \"{0}\" layout was saved."),
        new("\"{0}\" düzeni uygulandı. Önceki yerleşim \"{1}\" adıyla kaydedildi.",
            "The \"{0}\" layout was applied. The previous layout was saved as \"{1}\"."),
        new("\"{0}\" düzeni uygulandı.", "The \"{0}\" layout was applied."),
        new("\"{0}\" düzeni silindi.", "The \"{0}\" layout was deleted."),

        // WidgetManager: kaldırma (tepsi balonu ve Widget'lar sayfası), açılamayan widget
        new("Widget kaldırıldı", "Widget removed"),
        new("Kutudaki {0} öğe masaüstüne geri konuyor.",
            "{0} item from the box is going back to the desktop.", "{0} items from the box are going back to the desktop."),
        new("Masaüstü simgeleri yeniden gösteriliyor.", "Desktop icons are visible again."),
        new("Geri getirmek için buraya ya da tepsi menüsüne tıkla.", "To bring it back, click here or use the menu in the notification area."),
        new("Widget açılamadı. {0}'ten çıkıp yeniden açtıktan sonra tekrar dene.",
            "The widget couldn't be opened. Exit {0}, open it again, and then try again."),

        // Windows masaüstü simgeleri (DesktopModes: Ayarlar, Widget'lar sayfası, tepsi, karşılama)
        new("Hepsi masaüstünde görünsün", "Show everything on the desktop"),
        new("Kutulara eklediklerim masaüstünden kalksın", "Items I add to boxes leave the desktop"),
        new("Yalnızca bölmelerde göster", "Show only in panels"),
        new("Windows simgeleri görünür. Kısayol kutusuna eklediğin masaüstü öğesi masaüstünden kalkar: masaüstünün yanındaki {0} klasörüne taşınır ve kutuda durur. Masaüstüne sonradan gelenler görünür kalır; kutudan çıkarınca masaüstüne geri döner.",
            "Windows icons stay visible. A desktop item you add to a shortcut box leaves the desktop: it moves to the {0} folder next to the desktop and stays in the box. Things that land on the desktop later stay visible; take an item out of the box and it goes back to the desktop."),
        new("Masaüstündeki her şey bölmelerde toplanır; Windows'un kendi simgeleri gizlenir. Dosyalarına dokunulmaz; kapatınca simgeler geri gelir.",
            "Everything on the desktop is gathered into panels, and Windows' own icons are hidden. Your files aren't touched; turn this off and the icons come back."),
        new("Windows'un masaüstü simgeleri her zamanki gibi görünür; kutular ve bölmeler dosyalara dokunmadan kısayol gösterir.",
            "Windows desktop icons show as usual; boxes and panels show shortcuts without touching your files."),
        new("Kutulardaki öğeler masaüstüne geri konsun mu?", "Put the items in your boxes back on the desktop?"),
        new("{0} öğe kutuya eklendiği için {1} klasöründe duruyor. Geri konunca masaüstünde yine görünür ve kutularda kalır. \"Klasörde kalsın\" dersen kutular onları oradan açmaya devam eder.",
            "{0} item is in the {1} folder because it was added to a box. If you put it back, it shows on the desktop again and stays in your boxes. If you choose \"Keep in folder\", your boxes keep opening it from there.",
            "{0} items are in the {1} folder because they were added to a box. If you put them back, they show on the desktop again and stay in your boxes. If you choose \"Keep in folder\", your boxes keep opening them from there."),
        new("Masaüstüne geri koy", "Put back on desktop"),
        new("Klasörde kalsın", "Keep in folder"),
        new("Kutulardaki masaüstü öğeleri de taşınsın mı?", "Move the desktop items in your boxes too?"),
        new("Kutularında masaüstünde duran {0} öğe var. Taşınırsa masaüstünden kalkar, {1} klasöründe durur ve kutularda görünmeye devam eder.",
            "Your boxes have {0} item that's still on the desktop. If it's moved, it leaves the desktop, is kept in the {1} folder and still shows in your boxes.",
            "Your boxes have {0} items that are still on the desktop. If they're moved, they leave the desktop, are kept in the {1} folder and still show in your boxes."),
        new("Taşı", "Move"),
        new("Şimdi değil", "Not now"),
        new("Bundan sonra kısayol kutusuna eklediğin masaüstü öğeleri masaüstünden kalkar ve kutuda durur.",
            "From now on, desktop items you add to a shortcut box leave the desktop and stay in the box."),
        new("Masaüstü simgeleri yeniden gösteriliyor. Bölmelerin yerinde duruyor.",
            "Desktop icons are visible again. Your panels stay where they are."),

        // Yeni widget'ların yeri (Ayarlar ve Widget'lar sayfası)
        new("İmlecin yanına", "Next to the pointer"),
        new("Etkin ekranın ortasına", "Center of the active screen"),
        new("Köşeye (türüne göre)", "In a corner (by type)"),

        // Göz atma çubuğu (PeekBar)
        new("{0} · Windows masaüstü", "{0} · Windows desktop"),
        new("Windows masaüstü", "Windows desktop"),
        new("+5 dk", "+5 min"),
        new("Beş dakika daha göz at", "Peek for five more minutes"),
        new("Widget'lar geri gelir", "Brings your widgets back"),
        new("Widget'lar geri gelir. Kısayol: {0}", "Brings your widgets back. Shortcut: {0}"),
        new("Kendiliğinden dönülmez", "Doesn't go back automatically"),
        new("{0} sonra kendiliğinden döner", "Goes back automatically in {0}"),
    ]);
}
