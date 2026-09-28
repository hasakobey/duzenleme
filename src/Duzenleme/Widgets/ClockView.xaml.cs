using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

public partial class ClockView : UserControl, IWidgetView
{
    /// <summary>Ay/gün adları arayüz dilinde (Türkçede tr-TR).</summary>
    private static CultureInfo Culture => L.Culture;
    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _timer;

    public ClockView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        _timer = new DispatcherTimer(DispatcherPriority.Render);
        _timer.Tick += (_, _) => { Update(); Schedule(); };
        Update();
        Schedule();
        _timer.Start();
        // Uyku/uyanma ya da saat ayarı değişince beklemeden güncelle.
        Microsoft.Win32.SystemEvents.TimeChanged += OnSystemTime;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnSystemTime;
    }

    /// <summary>
    /// Bir sonraki saniye (saniye gösteriliyorsa) ya da dakika başına kurulur: saat boşta gereksiz yere
    /// uyanıp yeniden çizilmez (katmanlı pencerede her çizim tüm widget'ı yeniden oluşturur).
    /// </summary>
    private void Schedule()
    {
        var now = DateTime.Now;
        var unit = _config.ShowSeconds ? TimeSpan.TicksPerSecond : TimeSpan.TicksPerMinute;
        _timer.Interval = TimeSpan.FromTicks(unit - now.Ticks % unit) + TimeSpan.FromMilliseconds(15);
    }

    private void OnSystemTime(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => { Update(); Schedule(); });

    public bool Resizable => false;

    /// <summary>
    /// Kaldırma düğmesi (×) negatif kenar boşluğuyla kartın dolgusuna taşar. Yerleşim yuvarlaması (UseLayoutRounding)
    /// görünüme istediğinden bir pikselden az dar yer verince WPF görünümü kendi sınırına kırpar ve × yarım görünürdü.
    /// </summary>
    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;

    private void Update()
    {
        var now = DateTime.Now;
        TimeText.Text = now.ToString("HH:mm", Culture);
        SecondsText.Text = now.ToString("ss", Culture);
        SecondsBox.Visibility = _config.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;
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
        // Opak kartta selam satırı ClearType ile (ipucunu WidgetWindow görünüme verir); büyük rakamlar gri tonlamalı kalır.
        RenderOptions.SetClearTypeHint(GreetingText, RenderOptions.GetClearTypeHint(this));
        // Cam: yazıların altında efektsiz gölge kopyası (bkz. ShadowText).
        foreach (var shadow in new[] { TimeShadow, SecondsShadow, GreetingShadow }) shadow.Show(palette.TextShadow);
        RemoveButton.Foreground = palette.Foreground;
        ApplyParts();
    }

    public void AddMenuItems(WidgetMenu menu)
    {
        menu.Primary.Add(Menus.Toggle("Saniyeyi göster", _config.ShowSeconds, () =>
        {
            _config.ShowSeconds = !_config.ShowSeconds;
            AppHost.SaveSettings();
            Update();
            Schedule();
        }));
        menu.Appearance.Add(Menus.Parts(_config, [("greeting", "Selam ve gün"), Menus.ClosePart], ApplyParts));
    }

    public void Detach()
    {
        _timer.Stop();
        Microsoft.Win32.SystemEvents.TimeChanged -= OnSystemTime;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnSystemTime;
    }
}
