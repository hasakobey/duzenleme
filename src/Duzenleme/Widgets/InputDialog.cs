using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;
using Button = Wpf.Ui.Controls.Button;

namespace Duzenleme.Widgets;

/// <summary>Başlık/sekme adı gibi tek satırlık girdiler için küçük pencere.</summary>
internal static class InputDialog
{
    /// <param name="near">Widget'tan soruluyorsa widget'ın bir noktası (fiziksel piksel): pencere o monitörde açılır, birincil
    /// monitörde widget'tan uzakta değil.</param>
    public static string? Ask(string title, string label, string initial, NativeMethods.POINT? near = null)
    {
        var box = new TextBox { Text = initial, Margin = new Thickness(0, 8, 0, 18), MinWidth = 300 };
        var ok = new Button { Content = L.T("Kaydet"), Appearance = ControlAppearance.Primary, IsDefault = true, MinWidth = 100 };
        var cancel = new Button { Content = L.T("Vazgeç"), IsCancel = true, MinWidth = 100, Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(24, 12, 24, 20) };
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = label, FontSize = 14 });
        panel.Children.Add(box);
        panel.Children.Add(buttons);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        var titleBar = new TitleBar { Title = title, ShowMaximize = false, ShowMinimize = false };
        root.Children.Add(titleBar);
        Grid.SetRow(panel, 1);
        root.Children.Add(panel);

        var window = new FluentWindow
        {
            Title = title,
            Content = root,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ExtendsContentIntoTitleBar = true,
            WindowBackdropType = WindowBackdropType.Mica,
            Topmost = true,
            ShowInTaskbar = false,
            // FluentWindow'un varsayılan en küçük boyutu içerikten büyük; iletişim kutusu içeriğe sığsın.
            MinWidth = 0,
            MinHeight = 0,
        };
        if (near is { } point) Views.WindowFit.CenterOn(window, point);
        string? result = null;
        ok.Click += (_, _) => { result = box.Text.Trim(); window.DialogResult = true; };
        window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); Keyboard.Focus(box); };
        // Vazgeç → null; Kaydet → girilen metin (boş olabilir: "varsayılana dön").
        return window.ShowDialog() == true ? result : null;
    }
}
