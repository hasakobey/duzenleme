using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

public partial class DateView : UserControl, IWidgetView
{
    /// <summary>Ay/gün adları arayüz dilinde (Türkçede tr-TR).</summary>
    private static CultureInfo Culture => L.Culture;
    private WidgetPalette _palette = WidgetPalette.Glass;
    private DateTime _shownDate;
    private bool _live;

    private readonly WidgetConfig _config;

    public DateView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        Render();
    }

    /// <summary>
    /// Gün yalnızca gece yarısı değişir: görünürken ortak zamanlayıcının gün bildirimine abone olur (eskiden 20 saniyede bir
    /// uyanıp bakıyordu). Gizliyken hiç uyanmaz; görünür olunca gün değiştiyse hemen yeniden çizilir.
    /// </summary>
    public void SetLive(bool live)
    {
        if (_live == live) return;
        _live = live;
        WidgetTicker.DayChanged -= OnDayChanged;
        if (!live) return;
        WidgetTicker.DayChanged += OnDayChanged;
        OnDayChanged();
    }

    private void OnDayChanged()
    {
        if (DateTime.Today != _shownDate) Render();
    }

    public bool Resizable => false;

    /// <summary>
    /// Kaldırma düğmesi (×) negatif kenar boşluğuyla kartın dolgusuna taşar. Yerleşim yuvarlaması (UseLayoutRounding)
    /// görünüme istediğinden bir pikselden az dar yer verince WPF görünümü kendi sınırına kırpar ve × yarım görünürdü.
    /// </summary>
    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;

    /// <summary>Örn. "26 Eylül 2026, Cumartesi".</summary>
    public static string LongDate(DateTime date) =>
        $"{date.Day} {date.ToString("MMMM", Culture)} {date.Year}, {date.ToString("dddd", Culture)}";

    private void Render()
    {
        var today = DateTime.Today;
        _shownDate = today;
        DayText.Text = today.Day.ToString(Culture);
        MonthText.Text = today.ToString("MMMM", Culture);
        SubText.Text = $"{today.Year} · {today.ToString("dddd", Culture)}";
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
                // Kısa gün adı arayüz dilinde ("Pzt" / "Mon").
                Text = Culture.DateTimeFormat.AbbreviatedDayNames[(int)day.DayOfWeek], FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = isToday ? _palette.AccentForeground : _palette.Secondary,
            };
            var dayNumber = new TextBlock
            {
                Text = day.Day.ToString(Culture), FontSize = 15, FontWeight = FontWeights.SemiBold,
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
        menu.Appearance.Add(Menus.Parts(_config, [("sub", L.T("Yıl ve gün adı")), ("week", L.T("Haftalık şerit")), Menus.ClosePart], Render));

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    public void Detach() => WidgetTicker.DayChanged -= OnDayChanged;
}
