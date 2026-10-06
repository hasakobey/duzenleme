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
        new("Masaüstündekiler", "On your desktop"),
        new("Masaüstündekiler ({0})", "On your desktop ({0})"),
        new("Hepsini öne getir", "Bring all to front"),
        new("Widget'lar pencerelerin arkasında kalır; hepsini birkaç saniyeliğine öne getirir",
            "Widgets stay behind your windows; this brings them all to the front for a few seconds"),
        new("Widget'ları hizala", "Align widgets"),
        new("Widget'ları bulundukları ekranda çakışmadan sağdan başlayarak dizer; önceki yerleşim kaydedilir",
            "Lines up the widgets on their screen without overlaps, starting from the right; the previous layout is saved"),
        new("Bul", "Find"),
        new("Bul: {0}", "Find: {0}"),
        new("Kaldır: {0}", "Remove: {0}"),
        new("Masaüstündeki yerini göster", "Show where it is on the desktop"),
        new("Masaüstünde henüz widget yok. Yukarıdan birine tıkla, hemen masaüstüne gelsin.",
            "There are no widgets on your desktop yet. Click one above and it appears on your desktop right away."),
        new("Tüm widget'ların görünümü", "Appearance of all widgets"),
        new("Tek bir widget'ı ayarlamak için kartındaki ⋯ düğmesine bas (ya da ona sağ tıkla → Düzenle). Notlar kendi kağıt rengini kullanır.",
            "To change a single widget, select the ⋯ button on it (or right-click it → Edit). Notes use their own paper color."),
        new("Kayıtlı yerleşimler", "Saved layouts"),
        new("Kayıtlı yerleşimler ({0})", "Saved layouts ({0})"),
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
        new("Hizalanacak widget yok.", "There are no widgets to align."),
        new("{0} widget hizalandı.", "{0} widget was aligned.", "{0} widgets were aligned."),
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
        new("Masaüstü simgeleri yeniden gösteriliyor.", "Desktop icons are visible again."),
        new("Geri getirmek için buraya ya da tepsi menüsüne tıkla.", "To bring it back, click here or use the menu in the notification area."),
        new("Widget açılamadı. {0}'ten çıkıp yeniden açtıktan sonra tekrar dene.",
            "The widget couldn't be opened. Exit {0}, open it again, and then try again."),

        // Windows masaüstü simgeleri (DesktopModes: Ayarlar, Widget'lar sayfası, tepsi, karşılama)
        new("Hepsi masaüstünde görünsün", "Show everything on the desktop"),
        new("Yalnızca bölmelerde göster", "Show only in panels"),
        new("Masaüstündeki her şey bölmelerde toplanır; Windows'un kendi simgeleri gizlenir. Dosyalarına dokunulmaz; kapatınca simgeler geri gelir.",
            "Everything on the desktop is gathered into panels, and Windows' own icons are hidden. Your files aren't touched; turn this off and the icons come back."),
        new("Şimdi değil", "Not now"),
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
