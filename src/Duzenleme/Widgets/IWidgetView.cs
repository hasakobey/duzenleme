using System.Windows.Controls;

namespace Duzenleme.Widgets;

public interface IWidgetView
{
    /// <summary>Kullanıcı bu widget'ı yeniden boyutlandırabilir mi?</summary>
    bool Resizable { get; }

    void ApplyPalette(WidgetPalette palette);

    /// <summary>Sağ tık menüsüne widget'a özel öğeler ekler.</summary>
    void AddMenuItems(ContextMenu menu);

    /// <summary>Widget kapanırken zamanlayıcı/izleyici gibi kaynakları bırakır.</summary>
    void Detach();
}
