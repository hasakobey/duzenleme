using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Kolay NestDesk, Dalga 3b: Widget'lar sayfası (yönetim yeri: "Tüm widget'lar" şeridi, liste ve gömülü Düzenle paneli,
// "Daha fazla") ve iki görünümlü Ayarlar (yedi satır + "Gelişmiş ayarlar"). Terimler: bölme = panel, Kısayol kutusu =
// shortcut box, Halka menü = ring menu.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddKolay3b() => Add(
    [
        // Widget'lar sayfası
        new("Masaüstündeki widget'larını buradan ayarla.", "Set up the widgets on your desktop here."),
        new("Tüm widget'lar", "All widgets"),
        new("Seçtiğin görünüm bütün widget'lara uygulanır. Notlar kendi rengini korur.",
            "The look you pick applies to every widget. Notes keep their own color."),
        new("Düzenle: {0}", "Edit: {0}"),
        new("Masaüstünde henüz widget yok.", "No widgets on your desktop yet."),
        new("Saat, not ya da bölme ekle; hemen masaüstüne gelir.", "Add a clock, a note or a panel; it shows up on your desktop right away."),
        new("Hizala, öne getir, kayıtlı yerleşimler", "Arrange, bring to front, saved layouts"),
        new("Otomatik yedekler ({0})", "Automatic backups ({0})"),

        // Ayarlar: yedi satır
        new("Simgeleri bölmelerde topla", "Gather icons into panels"),
        new("Halkadaki uygulamaları ve öğeleri halkanın kendisinden değiştirirsin.", "You change the ring's apps and items on the ring itself."),
        new("Kısa tur, karşılama ya da sorun kaydı. Var olanlar silinmez.",
            "A short tour, the setup guide or a problem report. Nothing you have is deleted."),
        new("Karşılamayı yeniden başlat", "Restart the setup guide"),
        new("Gelişmiş ayarlar", "Advanced settings"),
        new("Masaüstü, görünüm, kısayollar, klasör simgeleri, kutular ve sorun giderme",
            "Desktop, appearance, shortcuts, folder icons, boxes and troubleshooting"),

        // Ayarlar > Gelişmiş ayarlar
        new("Widget yerleşimi", "Widget placement"),
        new("Sürüklerken widget'lar ekran kenarına ve birbirine yapışır.", "While you drag, widgets snap to the screen edges and to each other."),
        new("Bırakırken ya da büyütürken widget'lar birbirinin üstüne gelmez.", "Widgets don't land on each other when you drop or resize them."),
        new("Sorun giderme", "Troubleshooting"),
        new("Tanılama", "Diagnostics"),
    ]);
}
