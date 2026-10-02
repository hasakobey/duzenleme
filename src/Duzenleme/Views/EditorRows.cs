using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Image = System.Windows.Controls.Image;
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
    /// Bölümlü seçimin (ya da yongaların) bir seçeneği. <paramref name="SelectsOnArrow"/>: oklarla üstüne gelince seçilsin; false:
    /// pencere açar ("Özel…") ya da yalnızca durumu gösterir, seçiliyken yeniden basılınca da işini yapar. <paramref name="Menu"/>:
    /// basılınca seçmek yerine altında bu menüyü açar ("Bir klasör seç…"; seçim menüden gelir, işaret modelden).
    /// </summary>
    public sealed record Segment(string Id, string Label, string AutomationId, bool SelectsOnArrow = true, Func<ContextMenu>? Menu = null,
        string? HelpText = null);

    /// <summary>
    /// Bölümlü seçim (Küçük · Orta · Büyük): seçili bölümün zemini vurgu rengindedir. Sekme grupta bir kez durur, oklar gezer ve
    /// seçer; seçili bölüme yeniden basmak (ör. "Özel…") işini yeniden yapar. Seçim modelden okunur (<paramref name="selected"/>).
    /// <paramref name="wrap"/>: yan yana sığmayan çok seçenek (zamanlayıcı süreleri, bölmenin kaynağı) satıra sığdıkça dizilen
    /// yongalar olur.
    /// </summary>
    public static FrameworkElement Segments(Func<string>? label, IReadOnlyList<Segment> options, Func<string?> selected, Action<string> choose,
        ICollection<Action> refreshers, bool wrap = false)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
        TextBlock? title = null;
        if (label is not null)
        {
            title = Label(label());
            title.Margin = new Thickness(0, 0, 0, 6);
            panel.Children.Add(title);
        }
        Panel strip = wrap ? new WrapPanel() : new UniformGrid { Rows = 1, Columns = options.Count };
        KeyboardNavigation.SetTabNavigation(strip, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetDirectionalNavigation(strip, KeyboardNavigationMode.Cycle);
        var group = "EditorSegments" + Guid.NewGuid().ToString("N");
        var buttons = new List<RadioButton>();
        var syncing = false;
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
        foreach (var option in options)
        {
            var text = new TextBlock { Text = option.Label, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
            var button = new RadioButton { Content = text, GroupName = group, Style = wrap ? ChipStyle : SegmentStyle };
            AutomationProperties.SetAutomationId(button, option.AutomationId);
            AutomationProperties.SetName(button, option.Label);
            if (option.HelpText is not null) AutomationProperties.SetHelpText(button, option.HelpText);
            else if (label is not null) AutomationProperties.SetHelpText(button, label());
            var justChecked = false;
            button.Checked += (_, _) =>
            {
                if (syncing) return;
                justChecked = true;
                if (option.Menu is { } menu)
                {
                    // Seçim menüden gelir: işaret modele döner (menü kapanınca seçilen kaynak işaretlenir).
                    OpenMenu(button, menu);
                    button.Dispatcher.BeginInvoke(Sync);
                    return;
                }
                choose(option.Id);
            };
            // Seçili bölüme yeniden basılınca (Checked gelmez) pencere ya da menü açan seçenek ("Özel…") yine sorar; öbürleri zaten seçili.
            button.Click += (_, _) =>
            {
                if (syncing) return;
                if (justChecked) justChecked = false;
                else if (option.Menu is { } menu) OpenMenu(button, menu);
                else if (!option.SelectsOnArrow) choose(option.Id);
            };
            button.GotKeyboardFocus += (_, e) =>
            {
                var arrows = Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down);
                if (option.SelectsOnArrow && option.Menu is null && arrows && e.OldFocus is RadioButton { GroupName: var g } && g == group &&
                    button.IsChecked != true)
                    button.IsChecked = true;
            };
            buttons.Add(button);
            strip.Children.Add(button);
        }
        if (wrap) panel.Children.Add(strip);
        else
        {
            var frame = new Border
            {
                Padding = new Thickness(2), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), Child = strip,
            };
            frame.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush");
            frame.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
            panel.Children.Add(frame);
        }
        Sync();
        refreshers.Add(Sync);
        return panel;
    }

    /// <summary>Menüyü düğmenin altında açar (klavyeyle de; menü odağı alır, Esc kapatır).</summary>
    private static void OpenMenu(FrameworkElement owner, Func<ContextMenu> build)
    {
        var menu = build();
        menu.PlacementTarget = owner;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
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

    /// <summary>
    /// Renk örnekleri (notun kâğıt rengi): 32 DIP'lik yuvarlak örnekler, seçilinin çevresinde halka. Sekme grupta bir kez durur,
    /// oklar gezer ve seçer. Seçim modelden (<paramref name="selected"/>); seçilince <paramref name="choose"/>.
    /// </summary>
    public static FrameworkElement Swatches(Func<string> label, IReadOnlyList<(string Id, string Label, Brush Fill, string AutomationId)> swatches,
        Func<string?> selected, Action<string> choose, ICollection<Action> refreshers)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
        var title = Label(label());
        title.Margin = new Thickness(0, 0, 0, 6);
        panel.Children.Add(title);
        var row = new WrapPanel();
        KeyboardNavigation.SetTabNavigation(row, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetDirectionalNavigation(row, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetName(row, label());
        var group = "EditorSwatches" + Guid.NewGuid().ToString("N");
        var faces = new List<(string Id, RadioButton Button, Ellipse Ring)>();
        var syncing = false;
        foreach (var (id, text, fill, automationId) in swatches)
        {
            var dot = new Ellipse { Width = 26, Height = 26, Fill = fill, StrokeThickness = 1 };
            dot.SetResourceReference(Shape.StrokeProperty, "ControlStrokeColorDefaultBrush");
            var ring = new Ellipse { Width = 32, Height = 32, StrokeThickness = 2 };
            var button = new RadioButton
            {
                Content = new Grid { Width = 32, Height = 32, Children = { ring, dot } }, GroupName = group, Margin = new Thickness(0, 0, 8, 4),
                Cursor = Cursors.Hand, ToolTip = text,
                Template = new ControlTemplate(typeof(RadioButton)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
            };
            AutomationProperties.SetAutomationId(button, automationId);
            AutomationProperties.SetName(button, text);
            AutomationProperties.SetHelpText(button, label());
            var chosen = id;
            button.Checked += (_, _) => { if (!syncing) choose(chosen); };
            button.GotKeyboardFocus += (_, e) =>
            {
                var arrows = Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down);
                if (arrows && e.OldFocus is RadioButton { GroupName: var g } && g == group && button.IsChecked != true) button.IsChecked = true;
            };
            faces.Add((id, button, ring));
            row.Children.Add(button);
        }
        panel.Children.Add(row);
        void Sync()
        {
            var mark = row.TryFindResource("TextFillColorPrimaryBrush") as Brush ?? SystemColors.WindowTextBrush;
            var current = selected();
            syncing = true;
            try
            {
                title.Text = label();
                foreach (var (id, button, ring) in faces)
                {
                    var on = id == current;
                    button.IsChecked = on;
                    ring.Stroke = on ? mark : Brushes.Transparent;
                }
            }
            finally { syncing = false; }
        }
        Sync();
        refreshers.Add(Sync);
        return panel;
    }

    /// <summary>Gün seçici (geri sayım): takvim ve yazılan tarih arayüz dilinde; seçilince <paramref name="choose"/> (yalnızca gün).</summary>
    public static FrameworkElement Date(Func<string> label, string automationId, Func<DateTime> selected, Action<DateTime> choose,
        ICollection<Action> refreshers)
    {
        var title = Label(label());
        title.Margin = new Thickness(0, 0, 0, 6);
        // Tarih ve takvim arayüz dilinde (widget'taki tarih gibi, L.Culture); WPF'in DatePicker'ı yoksa Windows'un bölge biçimine uyar.
        var picker = new DatePicker
        {
            SelectedDateFormat = DatePickerFormat.Long, HorizontalAlignment = HorizontalAlignment.Stretch,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag),
        };
        AutomationProperties.SetAutomationId(picker, automationId);
        AutomationProperties.SetName(picker, label());
        var syncing = false;
        picker.SelectedDateChanged += (_, _) =>
        {
            // Okunamayan yazı seçimi boşaltır: kayıtlı gün kalır (kutu bir sonraki tazelemede onu gösterir).
            if (!syncing && picker.SelectedDate is { } day && day.Date != selected().Date) choose(day.Date);
        };
        void Sync()
        {
            syncing = true;
            try
            {
                title.Text = label();
                if (picker.SelectedDate?.Date != selected().Date) picker.SelectedDate = selected().Date;
            }
            finally { syncing = false; }
        }
        Sync();
        refreshers.Add(Sync);
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
        panel.Children.Add(title);
        panel.Children.Add(picker);
        return panel;
    }

    /// <summary>Yan yana düğmeler (kutunun "+ Uygulama · + Klasör · + Dosya · + Sekme"); sığmayan alt satıra geçer.</summary>
    public static FrameworkElement ButtonBar(IEnumerable<(string Label, string AutomationId, Action Click)> buttons)
    {
        var bar = new WrapPanel { Margin = new Thickness(0, 4, 0, 6) };
        foreach (var (label, id, click) in buttons)
        {
            var button = ActionButton(label, id, click);
            button.Margin = new Thickness(0, 0, 6, 6);
            bar.Children.Add(button);
        }
        return bar;
    }

    /// <summary>
    /// Öğe listesi (kısayol kutusunun öğeleri): her satırda simge ve ad. Yalnızca seçili satırın yanında "Kaldır" · "↑" · "↓"
    /// durur (fareyle hiçbir şey belirmez). Satıra basmak seçer (yeniden basmak bırakır); satırda Delete kaldırır, Alt+↑/↓ taşır.
    /// Liste her tazelemede modelden okunur (<paramref name="items"/>); öğeler değiştiyse satırlar yeniden kurulur, seçim yoluyla
    /// korunur.
    /// </summary>
    /// <param name="items">Gösterilen öğeler (kutucuklar: simgesi arka planda gelir).</param>
    /// <param name="remove">Öğeyi kaldırır (yolu).</param>
    /// <param name="move">Öğeyi bir yukarı (-1) ya da aşağı (+1) taşır.</param>
    public static FrameworkElement ItemList(Func<string> label, string emptyText, Func<IReadOnlyList<TileItem>> items, Action<string> remove,
        Action<string, int> move, ICollection<Action> refreshers)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
        var title = Label(label());
        title.Margin = new Thickness(0, 0, 0, 6);
        panel.Children.Add(title);
        var rows = new StackPanel();
        AutomationProperties.SetName(rows, label());
        AutomationProperties.SetAutomationId(rows, "Editor.Items");
        panel.Children.Add(rows);
        var empty = Muted(emptyText);
        panel.Children.Add(empty);

        string? selectedPath = null;
        var shown = new List<(string Path, string Name)>();

        void Build()
        {
            var list = items();
            shown = list.Select(t => (t.Path, t.Name)).ToList();
            if (selectedPath is not null && !shown.Any(s => string.Equals(s.Path, selectedPath, StringComparison.OrdinalIgnoreCase))) selectedPath = null;
            rows.Children.Clear();
            empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            for (var i = 0; i < list.Count; i++) rows.Children.Add(Row(list[i], i, list.Count));
        }

        void Remove(string path, int index)
        {
            remove(path);
            Build();
            Focus(Math.Min(index, rows.Children.Count - 1), null);
        }

        void Move(string path, int index, int delta, string? focusId)
        {
            if (index + delta < 0 || index + delta >= rows.Children.Count) return;
            selectedPath = path;
            move(path, delta);
            Build();
            Focus(index + delta, focusId);
        }

        FrameworkElement Row(TileItem tile, int index, int count)
        {
            var path = tile.Path;
            var selected = string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase);
            var icon = new Image { Width = 20, Height = 20, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            icon.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding(nameof(TileItem.Icon)) { Source = tile });
            var name = new TextBlock { Text = tile.Name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            name.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            var face = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(icon, Dock.Left);
            face.Children.Add(icon);
            face.Children.Add(name);
            var pick = new Button
            {
                Content = face, Appearance = selected ? ControlAppearance.Secondary : ControlAppearance.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 5, 8, 5), MinHeight = 34,
            };
            AutomationProperties.SetAutomationId(pick, "Editor.Item." + (index + 1));
            AutomationProperties.SetName(pick, tile.Name);
            if (selected) AutomationProperties.SetItemStatus(pick, L.T("Seçili"));
            pick.Click += (_, _) =>
            {
                selectedPath = selected ? null : path;
                Build();
                Focus(index, null);
            };
            pick.PreviewKeyDown += (_, e) =>
            {
                var key = e.Key == Key.System ? e.SystemKey : e.Key;
                if (key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
                {
                    e.Handled = true;
                    Remove(path, index);
                }
                else if (key is Key.Up or Key.Down && Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    e.Handled = true;
                    Move(path, index, key == Key.Up ? -1 : 1, null);
                }
            };

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(pick);
            if (!selected) return grid;

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            actions.Children.Add(SmallButton(L.T("Kaldır"), "Editor.Item.Remove", L.F("{0}: kaldır", tile.Name), () => Remove(path, index)));
            var up = SmallButton("↑", "Editor.Item.Up", L.T("Yukarı taşı"), () => Move(path, index, -1, "Editor.Item.Up"));
            up.IsEnabled = index > 0;
            var down = SmallButton("↓", "Editor.Item.Down", L.T("Aşağı taşı"), () => Move(path, index, 1, "Editor.Item.Down"));
            down.IsEnabled = index < count - 1;
            actions.Children.Add(up);
            actions.Children.Add(down);
            Grid.SetColumn(actions, 1);
            grid.Children.Add(actions);
            return grid;
        }

        // Yeniden kurulan satırlarda odak: taşınan öğenin satırı ya da (hâlâ basılabiliyorsa) aynı düğmesi.
        void Focus(int index, string? actionId) => rows.Dispatcher.BeginInvoke(() =>
        {
            if (index < 0 || index >= rows.Children.Count || rows.Children[index] is not Grid row) return;
            var action = actionId is null ? null
                : row.Children.OfType<StackPanel>().SelectMany(p => p.Children.OfType<Button>())
                    .FirstOrDefault(b => AutomationProperties.GetAutomationId(b) == actionId && b.IsEnabled);
            (action ?? row.Children[0] as UIElement)?.Focus();
        }, System.Windows.Threading.DispatcherPriority.Loaded);

        void Sync()
        {
            title.Text = label();
            var now = items();
            if (now.Count == shown.Count && now.Select(t => (t.Path, t.Name)).SequenceEqual(shown)) return;
            Build();
        }

        Build();
        refreshers.Add(Sync);
        return panel;
    }

    private static Button SmallButton(string text, string automationId, string name, Action click)
    {
        var button = new Button { Content = text, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 4, 0), MinWidth = 32 };
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    private static Style? _chipStyle;

    /// <summary>
    /// Yonga şablonu (bölmenin kaynağı, zamanlayıcı süreleri): yuvarlak kenarlı küçük düğme; seçilinin zemini anında vurgu rengi
    /// olur, fareyle yalnızca zemin hafifçe koyulaşır. Hiçbir şey kaymaz, solmaz ya da belirmez.
    /// </summary>
    internal static Style ChipStyle => _chipStyle ??= BuildChipStyle();

    private static Style BuildChipStyle()
    {
        var border = new FrameworkElementFactory(typeof(Border), "Part");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        border.SetValue(Border.PaddingProperty, new Thickness(12, 5, 12, 5));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(RadioButton)) { VisualTree = border };

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("ControlFillColorSecondaryBrush"), "Part"));
        var check = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        check.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("AccentFillColorDefaultBrush"), "Part"));
        check.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("AccentFillColorDefaultBrush"), "Part"));
        check.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextOnAccentFillColorPrimaryBrush")));
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush"), "Part"));
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55, "Part"));
        template.Triggers.Add(hover);
        template.Triggers.Add(check);
        template.Triggers.Add(focus);
        template.Triggers.Add(disabled);

        var style = new Style(typeof(RadioButton));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush")));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 6)));
        style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 40.0));
        style.Seal();
        return style;
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
