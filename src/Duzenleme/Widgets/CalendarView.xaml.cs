using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Aylık takvim (<see cref="WidgetVariants.Month"/>; temel türü Date: 2.0 onu tarih widget'ı olarak gösterir). Gösterilen ay
/// kaydedilmez: yeniden açılınca ve gece yarısı bugüne döner. Güncelleme yalnızca gün değişince (ortak zamanlayıcı), hiç
/// yoklama yok. Tıklayıp etkinleştirince PageUp/PageDown ay değiştirir, Home bugüne döner.
/// </summary>
public partial class CalendarView : UserControl, IWidgetView
{
    private static CultureInfo Culture => L.Culture;

    private readonly WidgetConfig _config;
    private readonly List<(Border Cell, TextBlock Number)> _cells = [];
    private readonly List<TextBlock> _names = [], _weeks = [];
    private WidgetPalette _palette = WidgetPalette.Glass;
    private DateTime _month = FirstOfMonth(DateTime.Today);
    private DateTime _today = DateTime.Today;
    private bool _live;

    public CalendarView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        for (var i = 0; i < MonthGrid.Columns; i++)
        {
            var name = new TextBlock { FontSize = 11, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _names.Add(name);
            DayNames.Children.Add(name);
        }
        for (var i = 0; i < MonthGrid.Cells; i++)
        {
            var number = new TextBlock
            {
                FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
            };
            var cell = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Margin = new Thickness(2, 1, 2, 1), Child = number };
            _cells.Add((cell, number));
            Days.Children.Add(cell);
        }
        for (var i = 0; i < MonthGrid.Rows; i++)
        {
            var week = new TextBlock { FontSize = 11, Width = 26, Height = 30, TextAlignment = TextAlignment.Center, Padding = new Thickness(0, 8, 0, 0) };
            _weeks.Add(week);
            WeekNumbers.Children.Add(week);
        }
        AutomationProperties.SetName(PrevButton, L.T("Önceki ay"));
        AutomationProperties.SetName(NextButton, L.T("Sonraki ay"));
        PrevButton.ToolTip = L.T("Önceki ay (Page Up)");
        NextButton.ToolTip = L.T("Sonraki ay (Page Down)");
        TodayButton.ToolTip = L.T("Bugüne dön (Home)");
        // Tıklanınca klavye bu widget'ta: PageUp/PageDown/Home.
        PreviewMouseLeftButtonDown += (_, _) => Focus();
        PreviewKeyDown += OnKey;
        Render();
    }

    public bool Resizable => false;

    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;

    /// <summary>Görünürken gün değişimine abone olur; görünür olunca (ya da gece yarısı) bugünün ayına döner.</summary>
    public void SetLive(bool live)
    {
        if (_live == live) return;
        _live = live;
        WidgetTicker.DayChanged -= OnDayChanged;
        if (!live) return;
        WidgetTicker.DayChanged += OnDayChanged;
        if (DateTime.Today != _today) OnDayChanged();
    }

    private void OnDayChanged()
    {
        _today = DateTime.Today;
        _month = FirstOfMonth(_today);
        Render();
    }

    private static DateTime FirstOfMonth(DateTime day) => new(day.Year, day.Month, 1);

    private DayOfWeek FirstDay => MonthGrid.FirstDay(_config.FirstDayOfWeek, Culture);

    private void Render()
    {
        var today = DateTime.Today;
        _today = today;
        var first = FirstDay;
        MonthTitle.Text = Culture.TextInfo.ToTitleCase(_month.ToString("MMMM yyyy", Culture));
        var showingToday = _month == FirstOfMonth(today);
        TodayButton.IsEnabled = !showingToday;
        // Devre dışıyken soluk (yazı saydamlığıyla değil, renkle: opak kartta ClearType kalsın).
        TodayButton.Foreground = showingToday ? _palette.Secondary : _palette.Foreground;

        var order = MonthGrid.WeekdayOrder(first);
        for (var i = 0; i < _names.Count; i++)
        {
            var day = order[i];
            // Kısaltılmış ad ("Pzt", "Mon"): Türkçenin tek harfli adları (P, S, Ç, P, C, C, P) karışır.
            _names[i].Text = Culture.DateTimeFormat.GetAbbreviatedDayName(day);
            AutomationProperties.SetName(_names[i], Culture.DateTimeFormat.GetDayName(day));
        }

        var days = MonthGrid.Days(_month.Year, _month.Month, first);
        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var (cell, number) = _cells[i];
            var inMonth = day.Month == _month.Month;
            var isToday = day == today;
            number.Text = day.Day.ToString(Culture);
            number.FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal;
            cell.Background = isToday ? _palette.Accent : Brushes.Transparent;
            number.Foreground = isToday ? _palette.AccentForeground
                : !inMonth || MonthGrid.IsWeekend(day) ? _palette.Secondary : _palette.Foreground;
            // Başka ayın günleri soluk: yarı saydam katmanda ClearType verilmez (gri tonlamalı kalır).
            cell.Opacity = inMonth || isToday ? 1 : 0.5;
            RenderOptions.SetClearTypeHint(number, inMonth || isToday ? ClearTypeText.Of(this) : ClearTypeHint.Auto);
            var name = day.ToString("D", Culture);
            AutomationProperties.SetName(cell, isToday ? L.F("{0}, bugün", name) : name);
        }
        for (var row = 0; row < _weeks.Count; row++)
            _weeks[row].Text = MonthGrid.WeekNumber(days[row * MonthGrid.Columns], first).ToString(Culture);

        ApplyParts();
    }

    private void ApplyParts()
    {
        HeaderRow.Visibility = _config.Shows("header") ? Visibility.Visible : Visibility.Collapsed;
        DayNames.Visibility = _config.Shows("weekdays") ? Visibility.Visible : Visibility.Collapsed;
        WeekHeader.Visibility = DayNames.Visibility;
        WeekColumn.Visibility = _config.Shows("weeknum") ? Visibility.Visible : Visibility.Collapsed;
        RemoveButton.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
        // Başlık gizliyken × gün adlarının üstüne binmesin.
        Root.Margin = new Thickness(0, _config.Shows("header") || RemoveButton.Visibility != Visibility.Visible ? 0 : 14, 0, 0);
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        MonthTitle.Foreground = palette.Foreground;
        foreach (var name in _names) name.Foreground = palette.Secondary;
        foreach (var week in _weeks) week.Foreground = palette.Secondary;
        WeekHeader.Foreground = palette.Secondary;
        foreach (var button in new[] { PrevButton, NextButton }) button.Foreground = palette.Foreground;
        RemoveButton.Foreground = palette.Foreground;
        ClearTypeText.Follow(this, [MonthTitle, WeekHeader, .. _names, .. _weeks]);
        Render();
    }

    private void Show(DateTime month)
    {
        _month = FirstOfMonth(month);
        Render();
    }

    private void Prev_Click(object sender, RoutedEventArgs e) => Show(MonthGrid.AddMonths(_month, -1));

    private void Next_Click(object sender, RoutedEventArgs e) => Show(MonthGrid.AddMonths(_month, 1));

    private void Today_Click(object sender, RoutedEventArgs e) => Show(DateTime.Today);

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.PageUp: Show(MonthGrid.AddMonths(_month, -1)); break;
            case Key.PageDown: Show(MonthGrid.AddMonths(_month, 1)); break;
            case Key.Home: Show(DateTime.Today); break;
            default: return;
        }
        e.Handled = true;
    }

    public void AddMenuItems(WidgetMenu menu)
    {
        var back = Menus.Item(L.T("Bugüne dön"), () => Show(DateTime.Today), "Home");
        back.IsEnabled = _month != FirstOfMonth(DateTime.Today);
        menu.Primary.Add(back);
        menu.Primary.Add(Menus.Choice<int?>(L.T("Haftanın ilk günü"), () => _config.FirstDayOfWeek,
            [(null, L.T("Dile göre")), (1, Culture.DateTimeFormat.GetDayName(DayOfWeek.Monday)), (0, Culture.DateTimeFormat.GetDayName(DayOfWeek.Sunday))],
            value =>
            {
                _config.FirstDayOfWeek = value;
                AppHost.SaveSettings();
                Render();
            }));
        menu.Appearance.Add(Menus.Parts(_config,
            [("header", L.N("Ay ve düğmeler")), ("weekdays", L.N("Gün adları")), ("weeknum", L.N("Hafta numaraları")), Menus.ClosePart],
            Render));
    }

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    public void Detach() => WidgetTicker.DayChanged -= OnDayChanged;
}
