using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Duzenleme.Views;

/// <summary>Geri sayımın adı, günü ve yinelenmesi.</summary>
internal sealed record CountdownChoice(string? Title, DateTime Date, bool Yearly);

/// <summary>
/// Geri sayım eklerken ve değiştirirken sorulan küçük pencere: etkinliğin adı, günü (takvimden ya da yazarak) ve "her yıl
/// yinele". Vazgeçilirse null (katalog widget'ı eklemez). InputDialog kalıbında: eşzamanlı, animasyonsuz, Enter kaydeder,
/// Esc vazgeçer.
/// </summary>
internal static class CountdownDialog
{
    public static CountdownChoice? Ask(string? title, DateTime date, bool yearly, NativeMethods.POINT? near)
    {
        var name = new TextBox
        {
            Text = title ?? "", PlaceholderText = L.T("ör. Tatil, Doğum günü"), Margin = new Thickness(0, 6, 0, 14), MinWidth = 320,
        };
        AutomationProperties.SetName(name, L.T("Etkinliğin adı"));
        AutomationProperties.SetAutomationId(name, "Countdown.Name");
        var picker = new DatePicker { SelectedDate = date.Date, Margin = new Thickness(0, 6, 0, 14), SelectedDateFormat = DatePickerFormat.Long };
        AutomationProperties.SetName(picker, L.T("Gün"));
        AutomationProperties.SetAutomationId(picker, "Countdown.Date");
        var repeat = new CheckBox { Content = L.T("Her yıl yinele (doğum günü, yıl dönümü)"), IsChecked = yearly, Margin = new Thickness(0, 0, 0, 18) };
        AutomationProperties.SetAutomationId(repeat, "Countdown.Yearly");

        var ok = new Button { Content = L.T("Kaydet"), Appearance = ControlAppearance.Primary, IsDefault = true, MinWidth = 100 };
        var cancel = new Button { Content = L.T("Vazgeç"), IsCancel = true, MinWidth = 100, Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetAutomationId(ok, "Countdown.Ok");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(24, 12, 24, 20) };
        panel.Children.Add(new TextBlock { Text = L.T("Etkinliğin adı"), FontSize = 14 });
        panel.Children.Add(name);
        panel.Children.Add(new TextBlock { Text = L.T("Gün"), FontSize = 14 });
        panel.Children.Add(picker);
        panel.Children.Add(repeat);
        panel.Children.Add(buttons);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(new TitleBar { Title = L.T("Geri sayım"), ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(panel, 1);
        root.Children.Add(panel);

        var window = new FluentWindow
        {
            Title = L.T("Geri sayım"),
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
        DialogPlacement.CenterOn(window, near);
        CountdownChoice? result = null;
        ok.Click += (_, _) =>
        {
            // Yazılıp takvimden seçilmeyen tarih de okunur; okunamazsa kutu uyarır, pencere açık kalır.
            if (picker.SelectedDate is not { } day)
            {
                picker.Focus();
                return;
            }
            var text = name.Text.Trim();
            // Yalnızca gün: saat dilimi eki olmadan saklanır (yolculukta ya da dilim değişince gün kaymasın).
            result = new CountdownChoice(text.Length == 0 ? null : text, DateTime.SpecifyKind(day.Date, DateTimeKind.Unspecified), repeat.IsChecked == true);
            window.DialogResult = true;
        };
        window.Loaded += (_, _) => { name.Focus(); name.SelectAll(); Keyboard.Focus(name); };
        return window.ShowDialog() == true ? result : null;
    }
}
