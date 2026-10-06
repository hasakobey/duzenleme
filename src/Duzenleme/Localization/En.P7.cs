using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.1 P7 "Hareket, eşitleme, denetimler": Animasyonlar ayarı, karşılamadaki Windows simgeleri seçimi, "Yeni bölme…"deki
// klasör kaynakları, menü ipuçları ve denetim gözden geçirmesinde eklenen/dokunulan metinler. Terim: "bölme" = "panel".
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddP7() => Add(
    [
        // Ayarlar > Genel > Animasyonlar (seçenekler ve notlar En.Motion.cs'te)
        new("Animasyonlar", "Animations"),

        // Karşılama: Windows simgeleri için Ayarlar'daki üç seçenek

        // "Yeni bölme…": masaüstü dışındaki klasör (klasör portalı)
        new("Masaüstü dışındaki bir klasör", "A folder outside the desktop"),
        new("Gözat…", "Browse…"),
        new("Klasör seç…", "Choose folder…"),

        // Widget menüsü

        // Denetim gözden geçirmesi: yalnızca simgeli düğmelerin ve yazı kutularının adları, ipuçları
        new("Bu bölmede ve alt klasörlerinde ara (Ctrl+F)", "Search this panel and its subfolders (Ctrl+F)"),
        new("Sekme ekle", "Add tab"),
        new("Simge açıklaması", "Icon description"),

        // Masaüstü türleri (bölme başlığı ve "Ne gösterilsin?" / "Yeni bölme…" açıklaması); Masaüstü, Klasörler, Kısayollar,
        // Dosyalar ve "Masaüstündeki klasörler" ortak tabloda
        new("Masaüstündeki her şey", "Everything on the desktop"),
        new("Uygulama ve web kısayolları", "App and web shortcuts"),
        new("Masaüstünde kalan dosyalar", "Files left on the desktop"),
    ]);
}
