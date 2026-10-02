using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using MenuItem = System.Windows.Controls.MenuItem;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Düzenle panelinin satırları (Kolay NestDesk): bölüm başlığı, anahtar, bölümlü seçim, açılır liste, düğme ve bağlantı. Her
/// satır durumunu bir okuyucudan alır; panel bir şey değişince bütün satırları modelden tazeler (<c>refreshers</c>). Satırlarda
/// fareyle beliren ya da kayan hiçbir şey yoktur; seçim düğmeleri WPF-UI'nin geçişli şablonunu değil kendi şablonunu kullanır
/// (fareyle yalnızca zemin değişir). Her denetimin adı (ekran okuyucu) ve AutomationId'si vardır.
/// </summary>
internal static class EditorRows
{
    /// <summary>Panelin iç genişliği (DIP): pencere 360, iki yanda 20.</summary>
    public const double ContentWidth = 320;

    public static TextBlock Section(string text, double top = 20)
    {
        var block = new TextBlock
        {
            Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, top, 0, 8), TextWrapping = TextWrapping.Wrap,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        return block;
    }

    public static TextBlock Muted(string text, double top = 0)
    {
        var block = new TextBlock { Text = text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 0) };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return block;
    }

    private static TextBlock Label(string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        return block;
    }

    /// <summary>Anahtar satırı: solda ad, sağda anahtar. Kullanıcı çevirince <paramref name="set"/>; işaret hep modelden.</summary>
    public static FrameworkElement Switch(Func<string> label, string automationId, Func<bool> isOn, Action<bool> set, ICollection<Action> refreshers)
    {
        var text = Label(label());
        var toggle = new ToggleSwitch { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        AutomationProperties.SetAutomationId(toggle, automationId);
        AutomationProperties.SetName(toggle, label());
        var syncing = false;
        void Sync()
        {
            syncing = true;
            try
            {
                text.Text = label();
                AutomationProperties.SetName(toggle, text.Text);
                toggle.IsChecked = isOn();
            }
            finally { syncing = false; }
        }
        toggle.Checked += (_, _) => { if (!syncing) set(true); };
        toggle.Unchecked += (_, _) => { if (!syncing) set(false); };
        Sync();
        refreshers.Add(Sync);

        var row = new Grid { Margin = new Thickness(0, 4, 0, 4), MinHeight = 32 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(text);
        Grid.SetColumn(toggle, 1);
        row.Children.Add(toggle);
        return row;
    }

    /// <summary>
    /// Bölümlü seçimin bir seçeneği. <paramref name="SelectsOnArrow"/>: oklarla üstüne gelince seçilsin; false: pencere açar
    /// ("Özel…") ya da yalnızca durumu gösterir, seçiliyken yeniden basılınca da işini yapar.
    /// </summary>
    public sealed record Segment(string Id, string Label, string AutomationId, bool SelectsOnArrow = true);

    /// <summary>
    /// Bölümlü seçim (Küçük · Orta · Büyük): seçili bölümün zemini vurgu rengindedir. Sekme grupta bir kez durur, oklar gezer ve
    /// seçer; seçili bölüme yeniden basmak (ör. "Özel…") işini yeniden yapar. Seçim modelden okunur (<paramref name="selected"/>).
    /// </summary>
    public static FrameworkElement Segments(Func<string>? label, IReadOnlyList<Segment> options, Func<string?> selected, Action<string> choose,
        ICollection<Action> refreshers)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
        TextBlock? title = null;
        if (label is not null)
        {
            title = Label(label());
            title.Margin = new Thickness(0, 0, 0, 6);
            panel.Children.Add(title);
        }
        var strip = new UniformGrid { Rows = 1, Columns = options.Count };
        KeyboardNavigation.SetTabNavigation(strip, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetDirectionalNavigation(strip, KeyboardNavigationMode.Cycle);
        var group = "EditorSegments" + Guid.NewGuid().ToString("N");
        var buttons = new List<RadioButton>();
        var syncing = false;
        foreach (var option in options)
        {
            var text = new TextBlock { Text = option.Label, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
            var button = new RadioButton { Content = text, GroupName = group, Style = SegmentStyle };
            AutomationProperties.SetAutomationId(button, option.AutomationId);
            AutomationProperties.SetName(button, option.Label);
            if (label is not null) AutomationProperties.SetHelpText(button, label());
            var justChecked = false;
            button.Checked += (_, _) =>
            {
                if (syncing) return;
                justChecked = true;
                choose(option.Id);
            };
            // Seçili bölüme yeniden basılınca (Checked gelmez) pencere açan seçenek ("Özel…") yine sorar; öbürleri zaten seçili.
            button.Click += (_, _) =>
            {
                if (syncing) return;
                if (justChecked) justChecked = false;
                else if (!option.SelectsOnArrow) choose(option.Id);
            };
            button.GotKeyboardFocus += (_, e) =>
            {
                var arrows = Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down);
                if (option.SelectsOnArrow && arrows && e.OldFocus is RadioButton { GroupName: var g } && g == group && button.IsChecked != true)
                    button.IsChecked = true;
            };
            buttons.Add(button);
            strip.Children.Add(button);
        }
        var frame = new Border
        {
            Padding = new Thickness(2), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), Child = strip,
        };
        frame.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush");
        frame.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        panel.Children.Add(frame);

        void Sync()
        {
            syncing = true;
            try
            {
                if (title is not null) title.Text = label!();
                var current = selected();
                for (var i = 0; i < options.Count; i++) buttons[i].IsChecked = options[i].Id == current;
            }
            finally { syncing = false; }
        }
        Sync();
        refreshers.Add(Sync);
        return panel;
    }

    /// <summary>Açılır liste (uzun ya da çok seçenekli tek seçim): seçim değişince <paramref name="choose"/>; seçili olan modelden.</summary>
    public static FrameworkElement Dropdown(Func<string> label, string automationId, IReadOnlyList<string> items, Func<int> selected,
        Action<int> choose, ICollection<Action> refreshers)
    {
        var title = Label(label());
        title.Margin = new Thickness(0, 0, 0, 6);
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in items) box.Items.Add(new ComboBoxItem { Content = item });
        AutomationProperties.SetAutomationId(box, automationId);
        AutomationProperties.SetName(box, label());
        var syncing = false;
        box.SelectionChanged += (_, _) =>
        {
            if (!syncing && box.SelectedIndex >= 0) choose(box.SelectedIndex);
        };
        void Sync()
        {
            syncing = true;
            try
            {
                title.Text = label();
                AutomationProperties.SetName(box, title.Text);
                box.SelectedIndex = selected();
            }
            finally { syncing = false; }
        }
        Sync();
        refreshers.Add(Sync);
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
        panel.Children.Add(title);
        panel.Children.Add(box);
        return panel;
    }

    /// <summary>İş yapan düğme (Çoğalt, Panoya kopyala…): soldaki gri düğme.</summary>
    public static Button ActionButton(string label, string automationId, Action click)
    {
        var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 4) };
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Bağlantı gibi duran düğme ("Bu görünümü tüm widget'lara uygula"): vurgu renginde yazı, zemin yok.</summary>
    public static Button Link(string label, string automationId, Action click)
    {
        var text = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap };
        text.SetResourceReference(TextBlock.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
        var button = new Button
        {
            Content = text, Appearance = ControlAppearance.Transparent, HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(4, 4, 4, 4), Margin = new Thickness(-4, 4, 0, 0), Cursor = Cursors.Hand,
        };
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => click();
        return button;
    }

    private static Style? _segmentStyle;

    /// <summary>
    /// Bölüm şablonu (Ayarlar'daki "Segment" gibi): seçili bölümün zemini anında vurgu rengi olur; fareyle yalnızca zemin hafifçe
    /// koyulaşır. Hiçbir şey kaymaz, solmaz ya da belirmez; yerleşim değişmez.
    /// </summary>
    internal static Style SegmentStyle => _segmentStyle ??= BuildSegmentStyle();

    private static Style BuildSegmentStyle()
    {
        var border = new FrameworkElementFactory(typeof(Border), "Part");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.PaddingProperty, new Thickness(8, 5, 8, 5));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(RadioButton)) { VisualTree = border };

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("SubtleFillColorSecondaryBrush"), "Part"));
        var check = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        check.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("AccentFillColorDefaultBrush"), "Part"));
        check.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextOnAccentFillColorPrimaryBrush")));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55, "Part"));
        template.Triggers.Add(hover);
        template.Triggers.Add(check);
        template.Triggers.Add(disabled);

        var style = new Style(typeof(RadioButton));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush")));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        style.Seal();
        return style;
    }
}

/// <summary>
/// Düzenle panelinin uyarlayıcısı (Kolay NestDesk 2. dalga): görünümün kendi menü öğelerini (<see cref="IWidgetView.AddMenuItems"/>)
/// panel satırı olarak çizer; bütün görünümler ilk günden panelde ayarlanır, menüyle panel ayrı ayrı yazılıp birbirinden
/// sapmaz. Seçenek (<see cref="OptionMenuItem"/>) → anahtar; tek seçimli alt menü (<see cref="Menus.Choice{T}"/>,
/// <see cref="Menus.AsChoice"/>) → bölümlü seçim ya da açılır liste; komut (<see cref="Menus.Item"/>) → düğme; başka alt menü →
/// başlıklı grup. "Göster" listesi (<see cref="Menus.Parts"/>) panelin kendi "Göster" bölümüne gider (<see cref="PartsIn"/>).
/// İşler menüdekiyle aynı yoldan çalışır (seçeneğin kendi işi, komutun Click'i); ardından panel tazelenir, menünün yapısını
/// değiştiren seçenek (<c>staysOpen: false</c>) ve komut paneli yeniden kurdurur.
/// </summary>
internal static class MenuRows
{
    internal enum RowKind { Skip, Switch, Choice, Parts, Group, Command }

    /// <summary>
    /// Öğenin panelde ne olacağı. <paramref name="optionsOnly"/>: menünün üst bölümü (Primary) — oradaki komutlar widget'ın
    /// işleridir (aç, ara, başlat…) ve menüde kalır; yalnızca seçenekler panele gelir.
    /// </summary>
    public static RowKind Classify(object item, bool optionsOnly)
    {
        if (item is not MenuItem menuItem) return RowKind.Skip;
        if (menuItem is OptionMenuItem) return RowKind.Switch;
        if (menuItem.Items.Count == 0) return optionsOnly ? RowKind.Skip : RowKind.Command;
        return Menus.ShapeOf(menuItem) switch
        {
            Menus.PanelShape.Parts => RowKind.Parts,
            Menus.PanelShape.Choice => RowKind.Choice,
            _ => optionsOnly && !HasOption(menuItem) ? RowKind.Skip : RowKind.Group,
        };
    }

    private static bool HasOption(MenuItem item) =>
        item.Items.OfType<MenuItem>().Any(child => child is OptionMenuItem || HasOption(child));

    /// <summary>Menü başlığının panelde görünen metni (<see cref="Menus.Literal"/>'in çift alt çizgisi tek olur).</summary>
    public static string Label(MenuItem item) => (item.Header as string ?? item.Header?.ToString() ?? "").Replace("__", "_", StringComparison.Ordinal);

    /// <summary>"Göster" listelerindeki seçenekler ve parça anahtarları, sırasıyla.</summary>
    public static IEnumerable<(OptionMenuItem Option, string Key)> PartsIn(IEnumerable<object> items) =>
        items.OfType<MenuItem>().Where(i => Menus.ShapeOf(i) == Menus.PanelShape.Parts)
            .SelectMany(i => i.Items.OfType<OptionMenuItem>())
            .Select(o => (o, Menus.PartKeyOf(o) ?? ""));

    /// <param name="panel">Satırların ekleneceği yer.</param>
    /// <param name="idPrefix">AutomationId öneki (ör. "Editor.Option"); satırlar sırayla numaralanır.</param>
    /// <param name="refreshers">Satırların tazeleme işleri buraya eklenir.</param>
    /// <param name="changed">Bir satır bir şey değiştirdi; true: menünün yapısı değişebilir, panel yeniden kurulmalı.</param>
    public static void Add(Panel panel, IEnumerable<object> items, bool optionsOnly, string idPrefix, ICollection<Action> refreshers,
        Action<bool> changed)
    {
        var index = 0;
        foreach (var item in items)
        {
            var kind = Classify(item, optionsOnly);
            if (kind is RowKind.Skip or RowKind.Parts) continue;
            var menuItem = (MenuItem)item;
            var id = idPrefix + "." + ++index;
            // Canlı başlık/etkinlik/görünürlük, satırın kendi tazelemesinden önce okunur (satır güncel başlığı göstersin).
            Menus.RefreshLive(menuItem);
            refreshers.Add(() => Menus.RefreshLive(menuItem));
            FrameworkElement row = kind switch
            {
                RowKind.Switch => SwitchRow((OptionMenuItem)menuItem, id, refreshers, changed),
                RowKind.Choice => ChoiceRow(menuItem, id, refreshers, changed),
                RowKind.Group => GroupRow(menuItem, id, refreshers, changed),
                _ => CommandRow(menuItem, id, changed),
            };
            Follow(menuItem, row, refreshers);
            panel.Children.Add(row);
        }
    }

    /// <summary>Seçenek anahtarı: çevirmek seçeneğin kendi işini yapar (menüdeki tıklama gibi).</summary>
    public static FrameworkElement SwitchRow(OptionMenuItem option, string automationId, ICollection<Action> refreshers, Action<bool> changed) =>
        EditorRows.Switch(() => Label(option), automationId, () => option.IsOn, on =>
        {
            if (option.IsOn == on) return;
            option.Choose();
            changed(!option.StaysOpenOnClick);
        }, refreshers);

    private static FrameworkElement ChoiceRow(MenuItem choice, string id, ICollection<Action> refreshers, Action<bool> changed)
    {
        var options = choice.Items.OfType<OptionMenuItem>().ToList();
        void Choose(int i)
        {
            if (i < 0 || i >= options.Count) return;
            options[i].Choose();
            changed(!options[i].StaysOpenOnClick);
        }
        FrameworkElement control;
        // Kısa seçenekler (en çok dört, toplam 30 harf) yan yana sığar; uzunlar açılır listeye.
        if (options.Count <= 4 && options.Sum(o => Label(o).Length) <= 30)
        {
            var segments = options.Select((o, i) => new EditorRows.Segment(i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Label(o), id + "." + (i + 1), SelectsOnArrow: !Label(o).EndsWith('…'))).ToList();
            control = EditorRows.Segments(() => Label(choice), segments, () => SelectedId(options),
                value => Choose(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)), refreshers);
        }
        else
            control = EditorRows.Dropdown(() => Label(choice), id, options.Select(Label).ToList(), () => options.FindIndex(o => o.IsOn), Choose, refreshers);

        // Seçimin yanındaki komutlar ("Başka süre…", "Başka bir klasör…"): altında düğme olarak.
        var commands = choice.Items.OfType<MenuItem>().Where(i => i is not OptionMenuItem && i.Items.Count == 0).ToList();
        if (commands.Count == 0) return control;
        var panel = new StackPanel();
        panel.Children.Add(control);
        var n = 0;
        foreach (var command in commands)
        {
            Menus.RefreshLive(command);
            refreshers.Add(() => Menus.RefreshLive(command));
            var row = CommandRow(command, id + ".Action" + ++n, changed);
            Follow(command, row, refreshers);
            panel.Children.Add(row);
        }
        return panel;
    }

    private static string? SelectedId(List<OptionMenuItem> options)
    {
        var on = options.FindIndex(o => o.IsOn);
        return on < 0 ? null : on.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static FrameworkElement GroupRow(MenuItem group, string id, ICollection<Action> refreshers, Action<bool> changed)
    {
        var title = EditorRows.Section(Label(group), top: 8);
        title.FontSize = 13;
        title.Margin = new Thickness(0, 8, 0, 2);
        refreshers.Add(() => title.Text = Label(group));
        var inner = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        Add(inner, group.Items.Cast<object>().ToList(), optionsOnly: false, id, refreshers, changed);
        var panel = new StackPanel();
        panel.Children.Add(title);
        panel.Children.Add(inner);
        return panel;
    }

    /// <summary>Komut düğmesi: menüdeki tıklamayla aynı iş (öğenin Click'i); ardından panel yeniden kurulur.</summary>
    private static FrameworkElement CommandRow(MenuItem command, string id, Action<bool> changed) =>
        EditorRows.ActionButton(Label(command), id, () =>
        {
            if (!command.IsEnabled) return;
            command.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, command));
            changed(true);
        });

    /// <summary>Satır öğenin canlı etkinliğini ve görünürlüğünü izler (<see cref="Menus.Live{T}"/>; okuyucuları önceden çalışmıştır).</summary>
    private static void Follow(MenuItem item, FrameworkElement row, ICollection<Action> refreshers)
    {
        void Sync()
        {
            row.Visibility = item.Visibility;
            row.IsEnabled = item.IsEnabled;
        }
        Sync();
        refreshers.Add(Sync);
    }
}
