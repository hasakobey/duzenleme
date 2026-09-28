using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Bir güne geri sayım (<see cref="WidgetVariants.Countdown"/>; temel türü Date). Başlık etkinliğin adıdır, hedef
/// <see cref="WidgetConfig.TargetDate"/>. Yalnızca gece yarısı güncellenir (ortak zamanlayıcı); o gün "Bugün!", geçince
/// "3 gün önce", her yıl yinelenende gelecek yıla sayar.
/// </summary>
public partial class CountdownView : UserControl, IWidgetView
{
    private static CultureInfo Culture => L.Culture;

    private readonly WidgetConfig _config;
    private readonly TitleEditor _titleEditor;
    private WidgetPalette _palette = WidgetPalette.Glass;
    private DateTime _shownDate;
    private bool _live;

    public CountdownView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        // Etkinliğin adı yerinde düzenlenir (F2, "Yeniden adlandır"); düzenlerken yazının gölge kopyası gizlenir.
        _titleEditor = new TitleEditor(this, TitleBox, TitleText, iconButton: null, () => DefaultTitle,
            title =>
            {
                _config.Title = title;
                AppHost.SaveSettings();
                Render();
            },
            pickIcon: null,
            refit: () => TitleShadow.Show(_palette.TextShadow && !_titleEditor!.IsEditing));
        Render();
    }

    public bool Resizable => false;

    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;

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

    private DateTime Target => _config.TargetDate?.Date ?? DateTime.Today;

    private static string DefaultTitle => L.T("Geri sayım");

    private string Title => string.IsNullOrWhiteSpace(_config.Title) ? DefaultTitle : _config.Title.Trim();

    private void Render()
    {
        var today = DateTime.Today;
        _shownDate = today;
        var (days, date) = CountdownDays.For(Target, today, _config.CountdownYearly);
        if (days == 0)
        {
            NumberText.Text = L.T("Bugün!");
            UnitText.Text = "";
        }
        else
        {
            NumberText.Text = Math.Abs(days).ToString("N0", Culture);
            UnitText.Text = days > 0 ? L.P(days, "gün") : L.P(-days, "gün önce");
        }
        TitleText.Text = Title;
        DateText.Text = date.ToString("D", Culture);
        AutomationProperties.SetName(this, days switch
        {
            0 => L.F("{0}: bugün", Title),
            > 0 => L.P(days, "{1}: {0} gün kaldı", Title),
            _ => L.P(-days, "{1}: {0} gün önceydi", Title),
        });

        NumberText.Foreground = _palette.Accent;
        UnitText.Foreground = _palette.Secondary;
        DateText.Foreground = _palette.Secondary;
        TitleBox.Visibility = _config.Shows("title") ? Visibility.Visible : Visibility.Collapsed;
        DateBox.Visibility = _config.Shows("date") ? Visibility.Visible : Visibility.Collapsed;
        RemoveButton.Foreground = _palette.Foreground;
        RemoveButton.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        _titleEditor.ApplyPalette(palette);
        foreach (var shadow in new[] { NumberShadow, UnitShadow, TitleShadow, DateShadow }) shadow.Show(palette.TextShadow);
        if (_titleEditor.IsEditing) TitleShadow.Show(false);
        ClearTypeText.Follow(this, TitleText, DateText, UnitText);
        Render();
    }

    public void AddMenuItems(WidgetMenu menu)
    {
        menu.Primary.Add(Menus.Item(L.T("Yeniden adlandır"), () => TryBeginRename(), KeyNames.F2));
        menu.Primary.Add(Menus.Item(L.T("Tarihi ve adı değiştir…"), Edit));
        menu.Primary.Add(Menus.Toggle(L.T("Her yıl yinele"), () => _config.CountdownYearly, () =>
        {
            _config.CountdownYearly = !_config.CountdownYearly;
            AppHost.SaveSettings();
            Render();
        }));
        menu.Appearance.Add(Menus.Parts(_config, [("title", L.N("Etkinliğin adı")), ("date", L.N("Tarih satırı")), Menus.ClosePart], Render));
    }

    /// <summary>
    /// F2 ve "Yeniden adlandır": etkinliğin adı yerinde düzenlenir; ad satırı gizliyse adı ve günü soran pencere açılır.
    /// </summary>
    public bool TryBeginRename()
    {
        if (_titleEditor.Begin()) return true;
        Edit();
        return true;
    }

    private void Edit()
    {
        if (Views.CountdownDialog.Ask(_config.Title, Target, _config.CountdownYearly, AnchorOf(this)) is not { } result) return;
        _config.Title = result.Title;
        _config.TargetDate = result.Date;
        _config.CountdownYearly = result.Yearly;
        AppHost.SaveSettings();
        Render();
    }

    /// <summary>İletişim kutusu widget'ın ekranında açılsın (fiziksel piksel; pencere yoksa imleç).</summary>
    internal static NativeMethods.POINT? AnchorOf(Visual view) =>
        PresentationSource.FromVisual(view) is System.Windows.Interop.HwndSource source &&
        NativeMethods.GetWindowRect(source.Handle, out var r)
            ? new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 }
            : null;

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    public void Detach()
    {
        _titleEditor.Cancel();
        WidgetTicker.DayChanged -= OnDayChanged;
    }
}
