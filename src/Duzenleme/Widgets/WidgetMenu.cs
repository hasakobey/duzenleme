namespace Duzenleme.Widgets;

/// <summary>
/// Widget sağ tık menüsünün widget'a özel bölümleri. İskeleti (Yeni widget ekle…, Görünüm ▸, Diğer ▸, Kaldır)
/// <see cref="WidgetWindow"/> kurar; görünüm sınıfı yalnızca bu listeleri doldurur. Öğeler MenuItem ya da Separator'dır.
/// <para>Görünüş/davranış seçenekleri <see cref="Menus.Toggle"/>, <see cref="Menus.Choice{T}"/>, <see cref="Menus.Parts"/>
/// ile eklenir: durumu okuyucudan alır, tıklanınca menü açık kalır ve widget hemen değişir. İş yapan komutlar (aç, ekle,
/// pencere açan, kaynağı değiştiren) <see cref="Menus.Item"/> ya da <c>staysOpen: false</c> ile menüyü kapatır.</para>
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
