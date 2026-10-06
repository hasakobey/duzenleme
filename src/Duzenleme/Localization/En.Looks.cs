using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Görünüş: widget temaları (Görünüm ▸ Tema, Widget'lar sayfası), özel vurgu rengi (renk seçici) ve yüksek karşıtlık.
// "Tema" En.SweepSettings.cs'te; "Arka plan", "Vurgu rengi", renk adları, "Kaydet" ve "Vazgeç" En.Common.cs'te.
// 2.2 Kutu: 1–9 ile açma (Simgeler ▸ Numara tuşlarıyla aç) ve kısayol kutusuna akıllı bırakma (bağlantı, Başlat menüsü uygulaması).
// "Bırak, kısayol olarak eklensin" En.SweepWidgetViews.cs'te.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddLooks() => Add(
    [
        new("Klasik", "Classic"),
        new("Otomatik (Windows'a uyar)", "Automatic (follows Windows)"),
        new("Neon", "Neon"),
        new("Gece", "Night"),
        new("Kâğıt", "Paper"),
        new("Okyanus", "Ocean"),
        new("Yüksek karşıtlık açık: widget'lar Windows'un renklerini kullanır.", "High contrast is on: widgets use your Windows colors."),
        new("Özel renk…", "Custom color…"),
        new("Özel renk", "Custom color"),
        new("Renkler", "Colors"),
        new("Renk kodu", "Color code"),
        new("Windows vurgu rengi", "Windows accent color"),
        new("Windows'un Kişiselleştirme > Renkler'deki vurgu rengi", "The accent color from Personalization > Colors in Windows"),
        new("Renk, widget'ın zemininde okunur kalsın diye gerekirse biraz açılır ya da koyulaşır.",
            "If needed, the color is made a little lighter or darker so it stays readable on the widget."),

        // 2.2 Kutu
        new("Numara tuşlarıyla aç (1–9)", "Open with number keys (1–9)"),
        new("Bırak, uygulama olarak eklensin", "Drop to add as an app"),
        new("Bırak, bağlantı olarak eklensin", "Drop to add as a link"),
        new("Bu bağlantı türü eklenemez (yalnızca http, https, mailto)", "This type of link can't be added (only http, https, mailto)"),
        new("Bu öğe kısayol kutusuna eklenemez.", "This item can't be added to the shortcut box."),
        new("Bağlantı kaydedilemedi.", "Couldn't save the link."),
        new("Bu bağlantı açılamıyor (yalnızca http, https ve mailto açılır).", "This link can't be opened (only http, https and mailto links open)."),
        new("Varsayılan simge", "Default icon"),
    ]);
}
