using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

public partial class ClockView : UserControl, IWidgetView
{
    /// <summary>Ay/gün adları arayüz dilinde (Türkçede tr-TR).</summary>
    private static CultureInfo Culture => L.Culture;
    private readonly WidgetConfig _config;
    private bool _live;

    public ClockView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        Update();
    }

    /// <summary>
    /// Görünürken ortak zamanlayıcıya (<see cref="WidgetTicker"/>) abone olur: saniye gösteriliyorsa saniyede, değilse
    /// dakikada bir güncellenir. Gizliyken hiç uyanmaz; görünür olunca hemen güncellenir. Saat ayarı değişince ya da uykudan
    /// uyanınca da ortak zamanlayıcı haber verir.
    /// </summary>
    public void SetLive(bool live)
    {
        if (_live == live) return;
        _live = live;
        Subscribe(live);
        if (live) Update();
    }

    private void Subscribe(bool on)
    {
        WidgetTicker.MinuteTick -= Update;
        WidgetTicker.SecondTick -= Update;
        if (!on) return;
        if (_config.ShowSeconds) WidgetTicker.SecondTick += Update;
        else WidgetTicker.MinuteTick += Update;
    }

    public bool Resizable => false;

    /// <summary>
    /// Kaldırma düğmesi (×) negatif kenar boşluğuyla kartın dolgusuna taşar. Yerleşim yuvarlaması (UseLayoutRounding)
    /// görünüme istediğinden bir pikselden az dar yer verince WPF görünümü kendi sınırına kırpar ve × yarım görünürdü.
    /// </summary>
    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;

    private void Update()
    {
        var now = DateTime.Now;
        var h24 = WorldClock.Uses24Hour(_config.Clock12Hour);
        TimeText.Text = WorldClock.Time(now, h24, Culture);
        SecondsText.Text = now.ToString("ss", Culture);
        SecondsBox.Visibility = _config.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;
        DesignatorText.Text = h24 ? "" : WorldClock.Designator(now, Culture);
        DesignatorBox.Visibility = h24 ? Visibility.Collapsed : Visibility.Visible;
        GreetingText.Text = Greeting(now.Hour) + " · " + Culture.TextInfo.ToTitleCase(now.ToString("dddd", Culture));
        GreetingRow.Visibility = _config.Shows("greeting") ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Parçalar değişince (Göster ▸): selam satırı ve kaldırma düğmesi.</summary>
    private void ApplyParts()
    {
        Update();
        RemoveButton.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    public static string Greeting(int hour) => hour switch
    {
        >= 5 and < 12 => L.T("Günaydın"),
        >= 12 and < 18 => L.T("İyi günler"),
        >= 18 and < 23 => L.T("İyi akşamlar"),
        _ => L.T("İyi geceler"),
    };

    public void ApplyPalette(WidgetPalette palette)
    {
        SecondsText.Foreground = palette.Accent;
        Dot.Fill = palette.Accent;
        GreetingText.Foreground = palette.Secondary;
        DesignatorText.Foreground = palette.Secondary;
        // Opak kartta selam satırı ClearType ile (ipucunu WidgetWindow görünüme verir); büyük rakamlar gri tonlamalı kalır.
        ClearTypeText.Follow(this, GreetingText, DesignatorText);
        // Cam: yazıların altında efektsiz gölge kopyası (bkz. ShadowText).
        foreach (var shadow in new[] { TimeShadow, SecondsShadow, GreetingShadow, DesignatorShadow }) shadow.Show(palette.TextShadow);
        RemoveButton.Foreground = palette.Foreground;
        ApplyParts();
    }

    public void AddMenuItems(WidgetMenu menu)
    {
        menu.Primary.Add(Menus.Toggle(L.T("Saniyeyi göster"), () => _config.ShowSeconds, () =>
        {
            _config.ShowSeconds = !_config.ShowSeconds;
            AppHost.SaveSettings();
            if (_live) Subscribe(true);
            Update();
        }));
        menu.Primary.Add(HourFormatChoice(_config, Update));
        menu.Appearance.Add(Menus.Parts(_config, [("greeting", L.N("Selam ve gün")), Menus.ClosePart], ApplyParts));
    }

    /// <summary>"Saat biçimi ▸ Windows'taki gibi / 24 saat / 12 saat" (saat ve dünya saati).</summary>
    internal static MenuItem HourFormatChoice(WidgetConfig config, Action changed) =>
        Menus.Choice<bool?>(L.T("Saat biçimi"), () => config.Clock12Hour,
            [(null, L.T("Windows'taki gibi")), (false, L.T("24 saat (15:30)")), (true, L.T("12 saat (3:30 ÖS)"))],
            value =>
            {
                config.Clock12Hour = value;
                AppHost.SaveSettings();
                changed();
            });

    public void Detach() => Subscribe(false);
}
