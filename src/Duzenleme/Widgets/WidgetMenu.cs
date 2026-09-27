namespace Duzenleme.Widgets;

/// <summary>
/// Widget sağ tık menüsünün widget'a özel bölümleri. İskeleti (Yeni widget ekle…, Görünüm ▸, Diğer ▸, Kaldır)
/// <see cref="WidgetWindow"/> kurar; görünüm sınıfı yalnızca bu listeleri doldurur. Öğeler MenuItem ya da Separator'dır.
/// </summary>
public sealed class WidgetMenu
{
    /// <summary>Üst düzeyde, "Yeni widget ekle…"nin altında: widget'ın asıl işleri (ör. Klasörü aç, Renk).</summary>
    public List<object> Primary { get; } = [];

    /// <summary>"Görünüm ▸" alt menüsünün başı (ör. "Göster ▸").</summary>
    public List<object> Appearance { get; } = [];

    /// <summary>"Diğer ▸" alt menüsünün başı (seyrek kullanılanlar).</summary>
    public List<object> More { get; } = [];
}
