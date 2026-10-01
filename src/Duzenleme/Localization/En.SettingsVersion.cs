using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 ayar güvenliği: ayar dosyası daha yeni bir sürümle kaydedilmiş (App.StartServices, Core/SettingsVersion).
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddSettingsVersion() => Add(
    [
        new("Ayarların {0} {1} ile kaydedilmiş. Bu sürümün bilmediği ayarların çoğu korunur; bazı yeni özellikler burada görünmez. Önceki hâli yedekler klasöründe.",
            "Your settings were saved by {0} {1}. Most settings this version doesn't know are kept, but some newer features won't show up here. The previous copy is in the backups folder (yedekler)."),
        new("Ayarların {0} {1} ile kaydedilmiş. Bu sürümün bilmediği ayarların çoğu korunur; bazı yeni özellikler burada görünmez.",
            "Your settings were saved by {0} {1}. Most settings this version doesn't know are kept, but some newer features won't show up here."),
        new("Yedekler klasörünü aç", "Open backups folder"),
        new("Yedekler klasörünü açmak için buraya tıkla.", "Click here to open the backups folder."),
    ]);
}
