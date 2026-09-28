using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Duzenleme.Views;

/// <summary>
/// Dünya saatine şehir ekleme: arama kutusu ve liste (önce sık seçilen şehirler, iki dilde aranır; sonra Windows'un bütün
/// saat dilimleri). Çift tık ya da Enter ekler; vazgeçilirse null. Diske ve ağa dokunmaz (dilimler Windows'tan).
/// </summary>
internal static class CityPickerDialog
{
    private sealed record Choice(string Label, string? Detail, WorldZone Zone)
    {
        public override string ToString() => Detail is null ? Label : $"{Label}  ·  {Detail}";
    }

    public static WorldZone? Ask(NativeMethods.POINT? near)
    {
        var search = new TextBox { PlaceholderText = L.T("Şehir ya da saat dilimi ara"), Margin = new Thickness(0, 0, 0, 10), MinWidth = 380 };
        AutomationProperties.SetName(search, L.T("Ara"));
        AutomationProperties.SetAutomationId(search, "City.Search");
        var list = new ListBox { Height = 320, SelectionMode = SelectionMode.Single };
        AutomationProperties.SetName(list, L.T("Şehirler"));
        AutomationProperties.SetAutomationId(list, "City.List");

        var ok = new Button { Content = L.T("Ekle"), Appearance = ControlAppearance.Primary, IsDefault = true, MinWidth = 100 };
        var cancel = new Button { Content = L.T("Vazgeç"), IsCancel = true, MinWidth = 100, Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetAutomationId(ok, "City.Ok");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(24, 12, 24, 20) };
        panel.Children.Add(search);
        panel.Children.Add(list);
        panel.Children.Add(new TextBlock
        {
            Text = L.T("Saat farkı ve yaz saati Windows'un saat dilimlerinden gelir."), FontSize = 12, Opacity = 0.7,
            Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 380,
        });
        panel.Children.Add(buttons);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(new TitleBar { Title = L.T("Şehir ekle"), ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(panel, 1);
        root.Children.Add(panel);

        var window = new FluentWindow
        {
            Title = L.T("Şehir ekle"),
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
        WindowFit.FitChromeToContent(window);
        DialogPlacement.CenterOn(window, near);

        // Windows'un saat dilimleri bir kez okunur (bellekten; birkaç yüz öğe).
        var systemZones = TimeZoneInfo.GetSystemTimeZones();
        void Fill()
        {
            var query = search.Text;
            var fold = FolderName.Fold(query.Trim());
            var items = WorldCities.Search(query)
                .Select(c => new Choice(c.Name, WorldClock.Find(c.ZoneId)?.BaseUtcOffset is { } offset ? Utc(offset) : null,
                    new WorldZone { Id = c.ZoneId, Label = WorldCities.ForZone(c.ZoneId) == c ? null : c.Name }))
                .Concat(systemZones
                    .Where(z => fold.Length == 0 || FolderName.Fold(z.DisplayName).Contains(fold, StringComparison.Ordinal)
                                || FolderName.Fold(z.Id).Contains(fold, StringComparison.Ordinal))
                    .Select(z => new Choice(z.DisplayName, null, new WorldZone { Id = z.Id })))
                .ToList();
            list.ItemsSource = items;
            if (items.Count > 0) list.SelectedIndex = 0;
        }
        search.TextChanged += (_, _) => Fill();
        search.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Down or Key.Up) || list.Items.Count == 0) return;
            list.SelectedIndex = Math.Clamp(list.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, list.Items.Count - 1);
            list.ScrollIntoView(list.SelectedItem);
            e.Handled = true;
        };
        Fill();

        WorldZone? result = null;
        void Accept()
        {
            if (list.SelectedItem is not Choice choice) return;
            result = choice.Zone;
            window.DialogResult = true;
        }
        ok.Click += (_, _) => Accept();
        list.MouseDoubleClick += (_, _) => Accept();
        window.Loaded += (_, _) => { search.Focus(); Keyboard.Focus(search); };
        return window.ShowDialog() == true ? result : null;
    }

    /// <summary>"UTC+3", "UTC+5:30", "UTC−8".</summary>
    private static string Utc(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "−" : "+";
        var abs = offset.Duration();
        return abs.Minutes == 0 ? $"UTC{sign}{abs.Hours}" : $"UTC{sign}{abs.Hours}:{abs.Minutes:00}";
    }
}
