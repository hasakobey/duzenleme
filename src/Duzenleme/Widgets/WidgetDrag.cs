using System.Windows;

namespace Duzenleme.Widgets;

/// <summary>Widget'tan başlayan sürüklemenin kaynağı: hangi widget, hangi öğe, kutudan mı.</summary>
/// <param name="RemoveFromSource">Kutudan başka bir widget'a taşınınca öğeyi kaynak kutudan çıkarır (bölmede null).</param>
/// <param name="Look">Kutuda verilmiş ad ve simge (başka kutuya taşınınca onunla gider).</param>
internal sealed record WidgetDragSource(string WidgetId, string Path, bool FromBox, Action? RemoveFromSource = null,
    Core.ItemLook? Look = null);

/// <summary>
/// Widget'lar arası sürükleme. <see cref="DragDrop.DoDragDrop"/> eşzamanlı olduğundan kaynak sürükleme boyunca burada
/// durur; hedef widget onu buradan tanır (veri nesnesinde yalnızca FileDrop vardır: Gezgin ve başka uygulamalar da
/// anlasın). Başka süreçten gelen sürüklemede <see cref="For"/> null döner.
/// </summary>
internal static class WidgetDrag
{
    private static WidgetDragSource? _current;
    private static bool _dropped;

    /// <summary>Sürüklenen veri bizim widget'ımızdan mı? Öyleyse kaynağı.</summary>
    public static WidgetDragSource? For(IDataObject data)
    {
        if (_current is not { } current) return null;
        return data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths &&
               string.Equals(paths[0], TileItem.NativePath(current.Path), StringComparison.OrdinalIgnoreCase)
            ? current : null;
    }

    /// <summary>Hedef widget bırakılanı işledi (sürükleme bir widget'ın içinde bitti).</summary>
    public static void MarkDropped() => _dropped = true;

    /// <summary>Sürüklemeyi kaynakla başlatır; bitince bir widget'ın içine bırakıldı mı döner.</summary>
    public static bool Run(WidgetDragSource? source, Action drag)
    {
        _current = source;
        _dropped = false;
        try { drag(); }
        finally { _current = null; }
        return _dropped;
    }
}
