using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Görünüş: widget temaları (Görünüm ▸ Tema, Widget'lar sayfası), özel vurgu rengi (renk seçici) ve yüksek karşıtlık.
// "Tema" En.SweepSettings.cs'te; "Arka plan", "Vurgu rengi", renk adları, "Kaydet" ve "Vazgeç" En.Common.cs'te.
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
        new("Tema, arka plan, renk ve yerleşim", "Theme, background, color and placement"),
        new("Özel renk…", "Custom color…"),
        new("Özel renk", "Custom color"),
        new("Renkler", "Colors"),
        new("Renk kodu", "Color code"),
        new("Windows vurgu rengi", "Windows accent color"),
        new("Windows'un Kişiselleştirme > Renkler'deki vurgu rengi", "The accent color from Personalization > Colors in Windows"),
        new("Renk, widget'ın zemininde okunur kalsın diye gerekirse biraz açılır ya da koyulaşır.",
            "If needed, the color is made a little lighter or darker so it stays readable on the widget."),
    ]);
}
