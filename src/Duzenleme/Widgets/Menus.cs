using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>Widget sağ tık menüleri için küçük yardımcılar.</summary>
public static class Menus
{
    public static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    public static MenuItem Toggle(string header, bool isChecked, Action action)
    {
        var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = isChecked };
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Tek seçimli alt menü (Görünüm, Boyut, Renk…).</summary>
    public static MenuItem Choice<T>(string header, T current, IEnumerable<(T Value, string Label)> options, Action<T> select)
    {
        var parent = new MenuItem { Header = header };
        foreach (var (value, label) in options)
        {
            var item = new MenuItem { Header = label, IsCheckable = true, IsChecked = EqualityComparer<T>.Default.Equals(value, current) };
            item.Click += (_, _) => select(value);
            parent.Items.Add(item);
        }
        return parent;
    }

    /// <summary>Menü her açılışta yeniden doldurulur; böylece işaretler hep günceldir.</summary>
    public static ContextMenu Dynamic(Action<ContextMenu> fill)
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) =>
        {
            menu.Items.Clear();
            fill(menu);
            if (menu.Items.Count == 0) menu.IsOpen = false;
        };
        menu.Items.Add(new MenuItem());
        return menu;
    }

    /// <summary>
    /// Liste öğelerine (dosya, kısayol) sağ tık menüsü bağlar. Menü yalnızca bir öğeye sağ tıklanınca açılır;
    /// boş alana sağ tıklanınca liste menüsü devre dışı kalır, olay widget kartına ulaşır ve widget'ın kendi ayar
    /// menüsü (Özelleştir, Sırala, Kaldır…) açılır. Böylece eski bir seçim yanlış öğenin menüsünü de açmaz.
    /// </summary>
    public static void AttachItemMenu(ListBox list, Action<ContextMenu, TileItem> fill)
    {
        TileItem? target = null;
        var menu = Dynamic(m => { if (target is { } item) fill(m, item); });
        list.PreviewMouseRightButtonDown += (_, e) =>
        {
            target = ItemAt(list, e.OriginalSource);
            if (target is null) list.SelectedItem = null;
            list.ContextMenu = target is null ? null : menu;
        };
    }

    /// <summary>Tıklanan öğe; boş alana, kaydırma çubuğuna vb. tıklandıysa null.</summary>
    public static TileItem? ItemAt(ListBox list, object? source)
    {
        var d = source as DependencyObject;
        // Metin içi öğeler (Run) görsel ağaçta değildir; mantıksal üstüne çık.
        while (d is not null and not Visual and not Visual3D) d = LogicalTreeHelper.GetParent(d);
        return d is not null && ItemsControl.ContainerFromElement(list, d) is ListBoxItem { DataContext: TileItem item } ? item : null;
    }

    /// <summary>"Göster" alt menüsü: widget'ın parçalarını tek tek açıp kapatır.</summary>
    public static MenuItem Parts(WidgetConfig c, IEnumerable<(string Key, string Label)> parts, Action changed)
    {
        var parent = new MenuItem { Header = "Göster" };
        foreach (var (key, label) in parts)
            parent.Items.Add(Toggle(label, c.Shows(key), () =>
            {
                if (!c.HiddenParts.Remove(key)) c.HiddenParts.Add(key);
                AppHost.SaveSettings();
                changed();
            }));
        return parent;
    }

    /// <summary>Tıklanamayan, açıklama amaçlı menü satırı.</summary>
    public static MenuItem Hint(string text) => new() { Header = text, IsEnabled = false };

    /// <summary>Bölme ve kısayol kutusunun "Simgeler" alt menüsü: görünüm, boyut, hizalama, aralık, yazı, adlar.</summary>
    public static MenuItem TileOptions(WidgetConfig c, Action<Action> change, bool singleClickOption)
    {
        var parent = new MenuItem { Header = "Simgeler" };
        parent.Items.Add(Choice("Görünüm", c.View,
            [(ItemView.Icons, "Izgara"), (ItemView.List, "Liste")], v => change(() => c.View = v)));
        parent.Items.Add(Choice("Boyut", c.IconSize,
            [(IconSize.Small, "Küçük"), (IconSize.Medium, "Orta"), (IconSize.Large, "Büyük"), (IconSize.ExtraLarge, "Çok büyük")],
            v => change(() => c.IconSize = v)));
        parent.Items.Add(Choice("Hizalama", c.Align,
            [(TileAlign.Left, "Sola"), (TileAlign.Center, "Ortaya"), (TileAlign.Right, "Sağa")], v => change(() => c.Align = v)));
        parent.Items.Add(Choice("Aralık", c.Spacing,
            [(TileSpacing.Compact, "Sık"), (TileSpacing.Normal, "Normal"), (TileSpacing.Wide, "Geniş")], v => change(() => c.Spacing = v)));
        parent.Items.Add(Choice("Yazı boyutu", c.LabelSize,
            [(LabelSize.Small, "Küçük"), (LabelSize.Normal, "Normal"), (LabelSize.Large, "Büyük")], v => change(() => c.LabelSize = v)));
        parent.Items.Add(new Separator());
        parent.Items.Add(Toggle("Adları gizle", c.HideLabels, () => change(() => c.HideLabels = !c.HideLabels)));
        parent.Items.Add(Toggle("Önizlemeleri göster (resim, video, PDF)", c.ShowPreviews, () => change(() => c.ShowPreviews = !c.ShowPreviews)));
        if (singleClickOption)
            parent.Items.Add(Toggle("Tek tıkla aç", c.SingleClick, () => change(() => c.SingleClick = !c.SingleClick)));
        parent.Items.Add(Hint("İpucu: Ctrl + fare tekerleği simgeleri büyütür/küçültür"));
        return parent;
    }

    /// <summary>Ctrl + tekerlek: simge boyutunu bir kademe değiştirir. Değiştiyse true.</summary>
    public static bool StepIconSize(WidgetConfig c, int wheelDelta, Action<Action> change)
    {
        var next = (IconSize)Math.Clamp((int)c.IconSize + (wheelDelta > 0 ? 1 : -1), (int)IconSize.Small, (int)IconSize.ExtraLarge);
        if (next == c.IconSize) return false;
        change(() => c.IconSize = next);
        return true;
    }

    /// <summary>
    /// Listedeki öğe dışarı sürüklenebilsin (başka bölmeye, Gezgin'e, e-postaya…). <paramref name="allowed"/> taşımayı
    /// içermiyorsa hedef dosyayı yerinden oynatamaz (kısayol kutusundaki öğeler yalnızca bağlantıdır).
    /// </summary>
    public static void EnableDragOut(ListBox list, DragDropEffects allowed)
    {
        Point? start = null;
        TileItem? item = null;
        list.PreviewMouseLeftButtonDown += (_, e) =>
        {
            item = ItemAt(list, e.OriginalSource);
            start = item is null ? null : e.GetPosition(list);
        };
        list.PreviewMouseLeftButtonUp += (_, _) => { start = null; item = null; };
        list.PreviewMouseMove += (_, e) =>
        {
            if (start is not { } origin || item is null || e.LeftButton != MouseButtonState.Pressed) return;
            var delta = e.GetPosition(list) - origin;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            var path = TileItem.NativePath(item.Path);
            start = null;
            item = null;
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            DragDrop.DoDragDrop(list, new DataObject(DataFormats.FileDrop, new[] { path }), allowed);
        };
    }
}
