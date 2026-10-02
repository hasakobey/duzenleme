namespace Duzenleme.Widgets;

/// <summary>
/// Widget'ın sağ tık menüsünün ve Düzenle panelinin öğeleri (Kolay NestDesk). Görünüm sınıfı <see cref="IWidgetView.AddMenuItems"/>
/// ile doldurur; menünün iskeletini <see cref="WidgetWindow"/> kurar ("Düzenle…" · en çok iki iş · [Kilidi aç] · Kaldır), panelin
/// bölümlerini <c>Views.WidgetEditor</c>. Öğeler MenuItem ya da Separator'dır.
/// <para>Menüye yalnızca <see cref="Primary"/> girer: widget'ın en çok iki işi (<see cref="MaxActions"/>), alt menüsüz; komut
/// <see cref="Menus.Item"/>, halka menüde bir de <see cref="Menus.Toggle"/>. Ayarlar paneldedir: <see cref="Settings"/> "widget'ın
/// ayarları" bölümü (görünümün kendi panel içeriği yoksa, <see cref="IWidgetView.AddEditorContent"/>), <see cref="More"/> "Daha
/// fazla". Seçenekler <see cref="Menus.Toggle"/>, <see cref="Menus.Choice{T}"/>, <see cref="Menus.Parts"/> ile eklenir: durumu
/// okuyucudan alır, panelde anahtar ya da bölümlü seçim olur. "Göster" listesi (<see cref="Menus.Parts"/>) hangi bölümde olursa
/// olsun panelin "Göster"ine gider.</para>
/// </summary>
public sealed class WidgetMenu
{
    /// <summary>Menüye giren en çok iş sayısı: "Düzenle…" + 2 + "Kilidi aç" + "Kaldır" = en çok beş satır.</summary>
    public const int MaxActions = 2;

    /// <summary>
    /// true: Düzenle paneli için (bütün bölümler). false: yalnızca sağ tık menüsü; görünüm <see cref="Primary"/>'yi doldurup döner
    /// (panelin öğeleri her menü açılışında boşuna kurulmasın).
    /// </summary>
    public bool ForEditor { get; init; }

    /// <summary>Menüde "Düzenle…"nin altında: widget'ın en çok iki işi (ör. Klasörü aç, Başlat), alt menüsüz.</summary>
    public List<object> Primary { get; } = [];

    /// <summary>Düzenle panelinde "widget'ın ayarları" (ör. Saat biçimi, Tür, Göster ▸).</summary>
    public List<object> Settings { get; } = [];

    /// <summary>Düzenle panelinin kapalı "Daha fazla" bölümü (seyrek kullanılanlar: Panoya kopyala, Yenile…).</summary>
    public List<object> More { get; } = [];
}
