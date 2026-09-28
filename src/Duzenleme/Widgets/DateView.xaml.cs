using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

public partial class DateView : UserControl, IWidgetView
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] ShortDays = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];
    private readonly DispatcherTimer _timer;
    private WidgetPalette _palette = WidgetPalette.Glass;
    private DateTime _shownDate;

    private readonly WidgetConfig _config;

    public DateView(WidgetConfig config)
    {
        _config = config;
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
        SubBox.Visibility = _config.Shows("sub") ? Visibility.Visible : Visibility.Collapsed;
        Week.Visibility = _config.Shows("week") ? Visibility.Visible : Visibility.Collapsed;
        RemoveButton.Foreground = _palette.Foreground;
        RemoveButton.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
        DayText.Foreground = _palette.Accent;
        SubText.Foreground = _palette.Secondary;
        // Küçük yazılar opak kartta ClearType ile çizilsin (ipucunu WidgetWindow görünüme verir; kırpılan alanda yazının
        // kendisinde de bulunmalı). Büyük rakamlar zaten gri tonlamalı çizilir.
        var clearType = RenderOptions.GetClearTypeHint(this);
        RenderOptions.SetClearTypeHint(SubText, clearType);

        Week.Children.Clear();
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        for (var i = 0; i < 7; i++)
        {
            var day = monday.AddDays(i);
            var isToday = day == today;
            var dayName = new TextBlock
            {
                Text = ShortDays[i], FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = isToday ? _palette.AccentForeground : _palette.Secondary,
            };
            var dayNumber = new TextBlock
            {
                Text = day.Day.ToString(Tr), FontSize = 15, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0),
                Foreground = isToday ? _palette.AccentForeground : _palette.Foreground,
            };
            RenderOptions.SetClearTypeHint(dayName, clearType);
            RenderOptions.SetClearTypeHint(dayNumber, clearType);
            var cell = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(0, 5, 0, 6),
                Margin = new Thickness(2, 0, 2, 0),
                Background = isToday ? _palette.Accent : Brushes.Transparent,
                Child = new StackPanel { Children = { dayName, dayNumber } },
            };
            Week.Children.Add(cell);
        }
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        // Cam: büyük yazıların altında efektsiz gölge kopyası (bkz. ShadowText).
        foreach (var shadow in new[] { DayShadow, MonthShadow, SubShadow }) shadow.Show(palette.TextShadow);
        Render();
    }

    public void AddMenuItems(WidgetMenu menu) =>
        menu.Appearance.Add(Menus.Parts(_config, [("sub", "Yıl ve gün adı"), ("week", "Haftalık şerit"), Menus.ClosePart], Render));

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    public void Detach() => _timer.Stop();
}
