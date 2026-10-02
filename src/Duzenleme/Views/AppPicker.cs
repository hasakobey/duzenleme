using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using Image = System.Windows.Controls.Image;
using MenuItem = System.Windows.Controls.MenuItem;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Duzenleme.Views;

/// <summary>"Uygulama seç"in sonucu: eklenecek yollar ve (Başlat menüsü uygulamasında) görünen adları.</summary>
internal sealed record AppPick(IReadOnlyList<string> Paths, IReadOnlyDictionary<string, string> Names);

/// <summary>
/// "Uygulama seç": Başlat menüsündeki uygulamalar (simge + ad, arayüz dilinde sıralı), üstte arama kutusu. Bir uygulamaya
/// tek tıklamak ekler ve pencereyi kapatır; zaten ekli olanlar "Zaten ekli" yazar ve seçilemez. Alttaki "Dosya ya da klasör
/// seç…" eski dosya/klasör seçicisini açar, "Vazgeç" (Esc) kapatır. Liste <see cref="StartMenuApps"/>'ten gelir: önbellekteki
/// hemen görünür, arka planda yenilenen gelince güncellenir; ilk okuma sürerken yalnızca "Uygulamalar yükleniyor…" yazar
/// (dönen halka yok). Simgeler arka planda (<see cref="ShellIconImage"/>). Pencere widget'ın monitöründe, onun ortasında açılır.
/// Kutu (menü ve boş kutudaki düğme) ve halka ("Uygulama ekle…", "+" yeri) kullanır. Yalnızca yerel; ağa çıkmaz.
/// </summary>
internal static class AppPicker
{
    private sealed class Row(StartApp app, bool added)
    {
        public string Name => app.Name;
        public string Path { get; } = app.Path!;
        public bool Selectable => !added;
        public Visibility AddedVisibility => added ? Visibility.Visible : Visibility.Collapsed;
        public string AccessibleName => added ? L.F("{0} (zaten ekli)", app.Name) : app.Name;
        public override string ToString() => app.Name;
    }

    /// <param name="near">Pencerenin ortalanacağı nokta (widget'ın ortası); yoksa imleç.</param>
    /// <param name="existing">Hedefte zaten duran yollar ("Zaten ekli").</param>
    /// <param name="fileTitle">Dosya seçicinin başlığı ("Kısayol kutusuna ekle", "Halkaya ekle").</param>
    /// <param name="folderTitle">Klasör seçicinin başlığı.</param>
    public static AppPick? Ask(NativeMethods.POINT? near, IReadOnlyCollection<string> existing, string fileTitle, string folderTitle)
    {
        var search = new TextBox { PlaceholderText = L.T("Ara"), Margin = new Thickness(0, 0, 0, 10), Width = 400 };
        AutomationProperties.SetName(search, L.T("Ara"));
        AutomationProperties.SetAutomationId(search, "AppPicker.Search");

        var list = new ListBox
        {
            Height = 360, Width = 400, SelectionMode = SelectionMode.Single, ItemTemplate = RowTemplate(),
            ItemContainerStyle = RowStyle(),
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);
        AutomationProperties.SetName(list, L.T("Uygulamalar"));
        AutomationProperties.SetAutomationId(list, "AppPicker.List");

        // Liste boşken ortasında tek satır: yükleniyor, uygulama yok ya da aramaya uyan yok.
        var status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 340,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false,
        };
        status.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        AutomationProperties.SetAutomationId(status, "AppPicker.Status");
        var listArea = new Grid();
        listArea.Children.Add(list);
        listArea.Children.Add(status);

        var file = new Button { Content = L.T("Dosya ya da klasör seç…"), Appearance = ControlAppearance.Secondary };
        AutomationProperties.SetAutomationId(file, "AppPicker.File");
        var cancel = new Button { Content = L.T("Vazgeç"), IsCancel = true, MinWidth = 100 };
        AutomationProperties.SetAutomationId(cancel, "AppPicker.Cancel");
        var buttons = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
        DockPanel.SetDock(cancel, Dock.Right);
        buttons.Children.Add(file);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(24, 12, 24, 20) };
        panel.Children.Add(search);
        panel.Children.Add(listArea);
        panel.Children.Add(buttons);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(new TitleBar { Title = L.T("Uygulama seç"), ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(panel, 1);
        root.Children.Add(panel);

        var window = new FluentWindow
        {
            Title = L.T("Uygulama seç"),
            Content = root,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            ExtendsContentIntoTitleBar = true,
            WindowBackdropType = WindowBackdropType.Mica,
            Topmost = true,
            ShowInTaskbar = false,
            MinWidth = 0,
            MinHeight = 0,
        };
        AutomationProperties.SetAutomationId(window, "AppPicker");
        WindowFit.FitChromeToContent(window);
        DialogPlacement.CenterOn(window, near);

        var added = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<StartApp> apps = StartMenuApps.Cached ?? [];
        var loading = StartMenuApps.Cached is null;
        var closed = false;

        void Fill()
        {
            var shown = AppList.Filter(apps, search.Text);
            var rows = shown.Select(a => new Row(a, a.Path is { } p && added.Contains(p))).ToList();
            list.ItemsSource = rows;
            list.SelectedItem = rows.FirstOrDefault(r => r.Selectable);
            status.Text = rows.Count > 0 ? "" : loading ? L.T("Uygulamalar yükleniyor…")
                : apps.Count == 0 ? L.T("Başlat menüsünde uygulama bulunamadı.") : L.T("Aramaya uyan uygulama yok.");
            status.Visibility = rows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        search.TextChanged += (_, _) => Fill();

        AppPick? result = null;
        void Accept(Row? row)
        {
            if (row is not { Selectable: true } || closed) return;
            result = new AppPick([row.Path], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [row.Path] = row.Name });
            window.DialogResult = true;
        }

        // Seçilebilir bir sonraki/önceki satır (zaten ekli olanlar atlanır).
        void Step(int direction)
        {
            if (list.ItemsSource is not List<Row> rows || rows.Count == 0) return;
            var index = list.SelectedIndex;
            for (var i = index + direction; i >= 0 && i < rows.Count; i += direction)
            {
                if (!rows[i].Selectable) continue;
                list.SelectedIndex = i;
                list.ScrollIntoView(rows[i]);
                return;
            }
        }
        search.PreviewKeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Down or Key.Up:
                    Step(e.Key == Key.Down ? 1 : -1);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    Accept(list.SelectedItem as Row);
                    e.Handled = true;
                    break;
            }
        };
        list.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            Accept(list.SelectedItem as Row);
            e.Handled = true;
        };
        // Tek tık ekler (sürükleme yok; kaydırma çubuğuna tıklamak satır değildir).
        list.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(list, e.OriginalSource as DependencyObject) is ListBoxItem { IsEnabled: true, DataContext: Row row })
                Accept(row);
        };

        // "Dosya ya da klasör seç…": eski seçiciler (birden çok seçilebilir); seçilince pencere kapanır.
        var choose = new ContextMenu { PlacementTarget = file, Placement = PlacementMode.Top };
        var pickFiles = new MenuItem { Header = L.T("Dosya seç…") };
        AutomationProperties.SetAutomationId(pickFiles, "AppPicker.PickFiles");
        pickFiles.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = fileTitle,
                Multiselect = true,
                DereferenceLinks = false,
                Filter = L.T("Uygulamalar ve kısayollar|*.exe;*.lnk;*.url;*.appref-ms;*.bat;*.cmd|Tüm dosyalar|*.*"),
                InitialDirectory = AppHost.DesktopDirectory,
            };
            if (dialog.ShowDialog(window) != true || dialog.FileNames.Length == 0) return;
            result = new AppPick(dialog.FileNames, new Dictionary<string, string>());
            window.DialogResult = true;
        };
        var pickFolders = new MenuItem { Header = L.T("Klasör seç…") };
        AutomationProperties.SetAutomationId(pickFolders, "AppPicker.PickFolders");
        pickFolders.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = folderTitle, Multiselect = true, InitialDirectory = AppHost.DesktopDirectory };
            if (dialog.ShowDialog(window) != true || dialog.FolderNames.Length == 0) return;
            result = new AppPick(dialog.FolderNames, new Dictionary<string, string>());
            window.DialogResult = true;
        };
        choose.Items.Add(pickFiles);
        choose.Items.Add(pickFolders);
        file.Click += (_, _) => choose.IsOpen = true;

        Fill();
        // Önbellek gösterilirken liste arka planda yeniden okunur (yeni kurulan uygulama da gelsin); pencere kapandıysa yok sayılır.
        _ = Refresh();
        async Task Refresh()
        {
            var fresh = await StartMenuApps.RefreshAsync();
            if (closed) return;
            loading = false;
            if (fresh.Count == apps.Count && fresh.SequenceEqual(apps))
            {
                if (apps.Count == 0) Fill();
                return;
            }
            var selected = (list.SelectedItem as Row)?.Path;
            apps = fresh;
            Fill();
            if (selected is not null && list.ItemsSource is List<Row> rows && rows.FirstOrDefault(r => r.Path == selected) is { } keep)
                list.SelectedItem = keep;
        }

        window.Loaded += (_, _) => { search.Focus(); Keyboard.Focus(search); };
        window.Closed += (_, _) => closed = true;
        return window.ShowDialog() == true ? result : null;
    }

    /// <summary>Satır: 24 DIP simge (arka planda gelir), ad (kısaltılır) ve sağda gerekiyorsa "Zaten ekli".</summary>
    private static DataTemplate RowTemplate()
    {
        var dock = new FrameworkElementFactory(typeof(DockPanel));
        dock.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 3, 0, 3));
        var image = new FrameworkElementFactory(typeof(Image));
        image.SetValue(FrameworkElement.WidthProperty, 24.0);
        image.SetValue(FrameworkElement.HeightProperty, 24.0);
        image.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
        image.SetValue(DockPanel.DockProperty, Dock.Left);
        image.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
        image.SetBinding(ShellIconImage.PathProperty, new Binding(nameof(Row.Path)));
        var note = new FrameworkElementFactory(typeof(TextBlock));
        note.SetValue(TextBlock.TextProperty, L.T("Zaten ekli"));
        note.SetValue(TextBlock.FontSizeProperty, 12.0);
        note.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 0, 0, 0));
        note.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        note.SetValue(DockPanel.DockProperty, Dock.Right);
        note.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        note.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(Row.AddedVisibility)));
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new Binding(nameof(Row.Name)));
        name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        name.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        dock.AppendChild(image);
        dock.AppendChild(note);
        dock.AppendChild(name);
        return new DataTemplate { VisualTree = dock };
    }

    /// <summary>Satırın kabı: zaten ekli olan seçilemez (soluk); ekran okuyucu "(zaten ekli)" der.</summary>
    private static Style RowStyle()
    {
        var style = new Style(typeof(ListBoxItem), Application.Current?.TryFindResource(typeof(ListBoxItem)) as Style);
        style.Setters.Add(new Setter(UIElement.IsEnabledProperty, new Binding(nameof(Row.Selectable))));
        style.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(nameof(Row.AccessibleName))));
        return style;
    }
}
