using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 Hareket 2.0: Ayarlar > Genel > Animasyonlar (Canlı / Sakin / Kapalı) ve nedenini söyleyen notlar. "Animasyonlar" başlığı
// En.P7.cs'te, "Kapalı" ortak çevirisiyle En.SweepMoves.cs'te.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddMotion() => Add(
    [
        new("Canlı", "Lively"),
        new("Sakin", "Calm"),
        new("Canlı: widget'lar ve sayfalar hafifçe büyüyerek ya da kayarak açılır. Sakin: yalnızca kısa solmalar. Kapalı: {0}'in kendi hareketleri oynamaz. Windows'un animasyon ayarına her zaman uyar.",
            "Lively: widgets and pages open with a gentle zoom or slide. Calm: short fades only. Off: {0}'s own animations don't play. Always follows the Windows animation setting."),
        new("Windows'ta animasyon efektleri kapalı olduğu için şu an {0}'in kendi hareketleri oynamıyor.",
            "{0}'s own animations aren't playing right now because animation effects are turned off in Windows."),
        new("Windows ayarını aç", "Open Windows setting"),
        new("Enerji tasarrufu açıkken hareketler sakin oynar.", "Motion stays calm while energy saver is on."),
        new("Uzak masaüstü bağlantısında hareketler sakin oynar.", "Motion stays calm in a Remote Desktop session."),
    ]);
}
