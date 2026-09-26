using System.Windows.Controls;

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
}
