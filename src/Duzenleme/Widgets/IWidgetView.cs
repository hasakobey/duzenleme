using System.Windows;
using System.Windows.Controls;

namespace Duzenleme.Widgets;

public interface IWidgetView
{
    /// <summary>Kullanıcı bu widget'ı yeniden boyutlandırabilir mi?</summary>
    bool Resizable { get; }

    /// <summary>Başlığa katlanabilir mi (bölme, kutu)?</summary>
    bool Collapsible => false;

    /// <summary>Katlama/açma isteği (ör. başlığa çift tıklama).</summary>
    event Action? CollapseToggleRequested { add { } remove { } }

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

    /// <summary>Sağ tık menüsüne widget'a özel öğeler ekler.</summary>
    void AddMenuItems(ContextMenu menu);

    /// <summary>Widget kapanırken zamanlayıcı/izleyici gibi kaynakları bırakır.</summary>
    void Detach();
}
