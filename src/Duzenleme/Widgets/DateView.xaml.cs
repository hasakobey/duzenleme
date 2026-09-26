using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Duzenleme.Widgets;

public partial class DateView : UserControl, IWidgetView
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] ShortDays = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];
    private readonly DispatcherTimer _timer;
    private WidgetPalette _palette = WidgetPalette.Glass;
    private DateTime _shownDate;

    public DateView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += (_, _) => { if (DateTime.Today != _shownDate) Render(); };
        _timer.Start();
        Render();
    }

    public bool Resizable => false;

    /// <summary>Örn. "26 Eylül 2026, Cumartesi".</summary>
    public static string LongDate(DateTime date) =>
        $"{date.Day} {date.ToString("MMMM", Tr)} {date.Year}, {date.ToString("dddd", Tr)}";

    private void Render()
    {
        var today = DateTime.Today;
        _shownDate = today;
        DayText.Text = today.Day.ToString(Tr);
        MonthText.Text = today.ToString("MMMM", Tr);
        SubText.Text = $"{today.Year} · {today.ToString("dddd", Tr)}";
        DayText.Foreground = _palette.Accent;
        SubText.Foreground = _palette.Secondary;

        Week.Children.Clear();
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        for (var i = 0; i < 7; i++)
        {
            var day = monday.AddDays(i);
            var isToday = day == today;
            var cell = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0, 5, 0, 6),
                Margin = new Thickness(2, 0, 2, 0),
                Background = isToday ? _palette.Accent : Brushes.Transparent,
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = ShortDays[i], FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = isToday ? _palette.AccentForeground : _palette.Secondary,
                        },
                        new TextBlock
                        {
                            Text = day.Day.ToString(Tr), FontSize = 15, FontWeight = FontWeights.SemiBold,
                            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0),
                            Foreground = isToday ? _palette.AccentForeground : _palette.Foreground,
                        },
                    },
                },
            };
            Week.Children.Add(cell);
        }
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        Effect = palette.TextShadow ? new DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Opacity = 0.4, Color = Colors.Black } : null;
        Render();
    }

    public void AddMenuItems(ContextMenu menu) { }

    public void Detach() => _timer.Stop();
}
