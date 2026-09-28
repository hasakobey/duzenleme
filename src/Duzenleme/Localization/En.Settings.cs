using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// Settings alanı: Views/SettingsPage.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddSettings() => Add(
    [
        // Dil satırı: başlık iki dilde, arayüzü anlamayan kullanıcı da bulsun. Seçenek adları çevrilmez (L.NativeName).
        new("Dil / Language", "Language / Dil"),
        new("Dil", "Language"),
        new("Yeniden başlatınca uygulanır.", "Applies after a restart."),
        new("Şimdi yeniden başlat", "Restart now"),
        new("Yeniden başlatılamadı: {0}", "Couldn't restart: {0}"),
    ]);
}
