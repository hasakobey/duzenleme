using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

public partial class ClockView : UserControl, IWidgetView
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _timer;

    public ClockView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => Update();
        _timer.Start();
        Update();
    }

    public bool Resizable => false;

    private void Update()
    {
        var now = DateTime.Now;
        TimeText.Text = now.ToString("HH:mm", Tr);
        SecondsText.Text = now.ToString("ss", Tr);
        SecondsText.Visibility = _config.ShowSeconds ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        GreetingText.Text = Greeting(now.Hour) + " · " + Tr.TextInfo.ToTitleCase(now.ToString("dddd", Tr));
    }

    public static string Greeting(int hour) => hour switch
    {
        >= 5 and < 12 => "Günaydın",
        >= 12 and < 18 => "İyi günler",
        >= 18 and < 23 => "İyi akşamlar",
        _ => "İyi geceler",
    };

    public void ApplyPalette(WidgetPalette palette)
    {
        SecondsText.Foreground = palette.Accent;
        Dot.Fill = palette.Accent;
        GreetingText.Foreground = palette.Secondary;
        Effect = palette.TextShadow ? new DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Opacity = 0.45, Color = Colors.Black } : null;
    }

    public void AddMenuItems(ContextMenu menu)
    {
        var seconds = new MenuItem { Header = "Saniyeyi göster", IsCheckable = true, IsChecked = _config.ShowSeconds };
        seconds.Click += (_, _) =>
        {
            _config.ShowSeconds = !_config.ShowSeconds;
            AppHost.SaveSettings();
            Update();
        };
        menu.Items.Add(seconds);
    }

    public void Detach() => _timer.Stop();
}
