using System.Windows;

namespace Duzenleme.Widgets;

public interface IWidgetView
{
    /// <summary>Kullanıcı bu widget'ı yeniden boyutlandırabilir mi?</summary>
    bool Resizable { get; }

    /// <summary>Başlığa katlanabilir mi (bölme, kutu)?</summary>
    bool Collapsible => false;

    /// <summary>Arka plan (Cam/Koyu/Açık) ve vurgu rengi bu widget'ta etkili mi? Not kendi kağıt rengini kullanır.</summary>
    bool UsesThemeColors => true;

    /// <summary>Katlama/açma isteği (ör. başlığa çift tıklama).</summary>
    event Action? CollapseToggleRequested { add { } remove { } }

    /// <summary>Parça gizlenip gösterilince pencerenin yerleşimi (katlanabilirlik, boyut) yeniden uygulanmalı.</summary>
    event Action? LayoutChanged { add { } remove { } }

    /// <summary>Widget'ın ayar menüsünü açma isteği (ör. dosya menüsündeki "Bölme ayarları…").</summary>
    event Action? MenuRequested { add { } remove { } }

    /// <summary>Katlanınca gizlenecek gövde.</summary>
    void SetBodyVisible(bool visible) { }

    /// <summary>Kartın iç boşluğu. Kenarındaki ~9 piksellik şerit boyutlandırma tutamacıdır; bundan dar olmamalı.</summary>
    Thickness CardPadding => new(20, 16, 20, 18);

    /// <summary>Ctrl + fare tekerleği; widget işlediyse true (bölme: simge boyutu), yoksa pencere ölçeklenir.</summary>
    bool OnCtrlWheel(int delta) => false;

    /// <summary>Widget kendi zeminini seçebilir (ör. not rengi).</summary>
    WidgetPalette AdjustPalette(WidgetPalette palette) => palette;

    void ApplyPalette(WidgetPalette palette);

    /// <summary>Sağ tık menüsünün widget'a özel bölümlerini doldurur (iskeleti WidgetWindow kurar).</summary>
    void AddMenuItems(WidgetMenu menu);

    /// <summary>Bekleyen değişiklikleri (ör. notun son yazılanları) ayarlara hemen yazar.</summary>
    void Flush() { }

    /// <summary>Widget kapanırken zamanlayıcı/izleyici gibi kaynakları bırakır.</summary>
    void Detach();
}
