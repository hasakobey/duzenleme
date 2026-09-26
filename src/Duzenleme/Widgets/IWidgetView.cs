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

    /// <summary>Katlanınca gizlenecek gövde.</summary>
    void SetBodyVisible(bool visible) { }

    /// <summary>Widget kendi zeminini seçebilir (ör. not rengi).</summary>
    WidgetPalette AdjustPalette(WidgetPalette palette) => palette;

    void ApplyPalette(WidgetPalette palette);

    /// <summary>Sağ tık menüsüne widget'a özel öğeler ekler.</summary>
    void AddMenuItems(ContextMenu menu);

    /// <summary>Widget kapanırken zamanlayıcı/izleyici gibi kaynakları bırakır.</summary>
    void Detach();
}
