using System.ComponentModel;
using Duzenleme.Views;
using Wpf.Ui.Controls;

namespace Duzenleme;

public partial class MainWindow : FluentWindow
{
    private bool _reallyClose;

    public MainWindow()
    {
        InitializeComponent();
        Views.WindowFit.Attach(this);
        Loaded += (_, _) => RootNavigation.Navigate(typeof(HomePage));
    }

    public void NavigateTo(Type page) => RootNavigation.Navigate(page);

    /// <summary>Pencereyi kapatmak uygulamayı kapatmaz; tepside çalışmaya devam eder.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    public void CloseForReal()
    {
        _reallyClose = true;
        Close();
    }
}
