using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>Geri alınamayan işlemler için küçük onay penceresi (InputDialog kalıbında; eşzamanlı, animasyonsuz).</summary>
internal static class Confirm
{
    /// <summary>Başlık, ileti ve iki düğme: [confirmText] (Appearance=Danger; danger=false ise Primary) ve [cancelText]
    /// (IsDefault + IsCancel). Owner verilir, ortalanır. Enter ve Esc ikinci düğmedir. Onaylanırsa true.
    /// Sahip yoksa (ör. widget'tan sorulan) near verilirse o noktanın monitörünün ortasında açılır, yoksa birincil monitörde.</summary>
    public static bool Ask(Window? owner, string title, string message, string confirmText, bool danger = true, string cancelText = "Vazgeç",
        Widgets.NativeMethods.POINT? near = null)
    {
        var ok = new Button { Content = confirmText, Appearance = danger ? ControlAppearance.Danger : ControlAppearance.Primary, MinWidth = 100 };
        var cancel = new Button { Content = cancelText, IsDefault = true, IsCancel = true, MinWidth = 100, Margin = new Thickness(8, 0, 0, 0) };
        System.Windows.Automation.AutomationProperties.SetAutomationId(ok, "Confirm.Ok");
        System.Windows.Automation.AutomationProperties.SetAutomationId(cancel, "Confirm.Cancel");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(24, 12, 24, 20), MaxWidth = 460 };
        panel.Children.Add(new TextBlock
        {
            Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = message, FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 20),
        });
        panel.Children.Add(buttons);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        // Soru gövdede başlık olarak durur; başlık çubuğunda yalnızca uygulama adı.
        root.Children.Add(new TitleBar { Title = Core.AppInfo.Name, ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(panel, 1);
        root.Children.Add(panel);

        var usableOwner = owner is { IsVisible: true } ? owner : null;
        var window = new FluentWindow
        {
            Title = title,
            Content = root,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = usableOwner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = usableOwner,
            ExtendsContentIntoTitleBar = true,
            WindowBackdropType = WindowBackdropType.Mica,
            Topmost = usableOwner is null,
            ShowInTaskbar = false,
            // FluentWindow'un varsayılan en küçük boyutu içerikten büyük; iletişim kutusu içeriğe sığsın.
            MinWidth = 0,
            MinHeight = 0,
        };
        WindowFit.FitChromeToContent(window);
        if (usableOwner is null && near is { } point) WindowFit.CenterOn(window, point);
        ok.Click += (_, _) => window.DialogResult = true;
        // Odak "Vazgeç"te başlar: Enter yanlışlıkla onaylamasın.
        window.Loaded += (_, _) => { cancel.Focus(); Keyboard.Focus(cancel); };
        return window.ShowDialog() == true;
    }
}
