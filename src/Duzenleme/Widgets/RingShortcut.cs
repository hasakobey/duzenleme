using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// "Kısayolla imleçte açılsın": halka kısayolunu açıp kapama akışının tek yeri (widget menüsü; Ayarlar da buradan geçer).
/// Store: kısayol varsayılan olarak kapalı ve atanmamıştır, açılırken ne yaptığı anlatılır (RingHotkey.Suggest → Set → Apply →
/// Failures, açıklamalı Notice). Akış kısayolla açılan halkayla birlikte bağlanır (<see cref="Toggle"/>); bağlanana dek
/// menüde seçenek görünmez: kayıtlı ama işlenmeyen bir kısayol tuşu yutardı.
/// </summary>
internal static class RingShortcut
{
    /// <summary>Halka kısayolu atanmış mı?</summary>
    public static bool IsOn => !string.IsNullOrEmpty(AppHost.Settings.Hotkeys.Ring);

    /// <summary>Aç/kapat akışı (verilen halka hedef olur); bağlanmadıysa null.</summary>
    public static Action<WidgetConfig>? Toggle { get; set; }

    /// <summary>Akış bağlı mı (menüde seçenek görünsün mü)?</summary>
    public static bool Available => Toggle is not null;
}
