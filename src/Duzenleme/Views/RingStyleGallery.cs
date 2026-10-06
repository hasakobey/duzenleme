using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Image = System.Windows.Controls.Image;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Halka biçimi galerisi (Düzenle paneli, halka menü widget'ı; <see cref="RingStyles"/>): dört kart, her biri widget'ın kendi
/// paletiyle ve gerçek <see cref="RingView"/> çizimiyle küçültülmüş bir halka (beş örnek öğe; resmi bir kez çizilir, yalnızca
/// görünüş değişince yeniden). Seçili kartın çevresinde halka. Fareyle hiçbir kart önizlenmez ya da büyümez: halka yalnızca
/// tıklanınca değişir. Sekme grupta bir kez durur, oklar gezer ve seçer (<see cref="LookGalleryControl"/> gibi). Kartların
/// AutomationId'si <c>Editor.RingStyle.&lt;kimlik&gt;</c> (classic, orbit, labels, glow).
/// </summary>
internal sealed class RingStyleGallery : UniformGrid
{
    public const double CardWidth = 72, CardHeight = 64;

    /// <summary>Kartın içindeki halka resminin kenarı (DIP).</summary>
    private const double SampleDip = 56;

    private readonly WidgetConfig _config;
    private readonly Func<string?> _current;
    private readonly Action<string> _choose;
    private readonly List<(string Id, RadioButton Button, Border Card, Border Ring, Image Sample)> _cards = [];
    private string? _drawnFor;
    private bool _syncing;

    /// <param name="config">Halkanın ayarı (görünüşü okunur, yazılmaz).</param>
    /// <param name="current">Seçili kartın kimliği.</param>
    /// <param name="choose">Kart seçildi.</param>
    public RingStyleGallery(WidgetConfig config, Func<string?> current, Action<string> choose)
    {
        _config = config;
        _current = current;
        _choose = choose;
        Columns = 4;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetName(this, L.T("Halka biçimi"));
        var group = "RingStyleGallery" + Guid.NewGuid().ToString("N");
        foreach (var style in RingStyles.All) Children.Add(Card(style, group));
        Refresh();
    }

    private UIElement Card(RingStyle style, string group)
    {
        var id = style.Id ?? RingStyles.ClassicId;
        var sample = new Image { Width = SampleDip, Height = SampleDip, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(sample, BitmapScalingMode.HighQuality);
        var card = new Border
        {
            Width = CardWidth, Height = CardHeight, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Child = sample,
        };
        var ring = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(2), Padding = new Thickness(1), Child = card };
        var label = new TextBlock
        {
            Text = style.Label, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var button = new RadioButton
        {
            Content = new StackPanel { Children = { ring, label } }, GroupName = group, Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Center,
            Template = new ControlTemplate(typeof(RadioButton)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
        };
        AutomationProperties.SetAutomationId(button, "Editor.RingStyle." + id);
        AutomationProperties.SetName(button, style.Label);
        button.Checked += (_, _) => { if (!_syncing) _choose(id); };
        button.GotKeyboardFocus += (_, e) =>
        {
            var arrows = Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down);
            if (arrows && e.OldFocus is RadioButton { GroupName: var g } && g == group && button.IsChecked != true) button.IsChecked = true;
        };
        _cards.Add((id, button, card, ring, sample));
        return button;
    }

    /// <summary>Seçili kartı işaretler; widget'ın görünüşü değiştiyse örnek halkaları yeniden çizer.</summary>
    public void Refresh()
    {
        var current = _current();
        var selection = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? SystemColors.WindowTextBrush;
        var palette = RingView.PaletteFor(_config);
        var look = string.Join("|", _config.Style, _config.Accent, _config.LookTheme, _config.LookAccentColor, WidgetPalette.SystemDark,
            SystemParameters.HighContrast);
        var redraw = look != _drawnFor;
        _drawnFor = look;
        _syncing = true;
        try
        {
            foreach (var (id, button, card, ring, sample) in _cards)
            {
                if (redraw)
                {
                    card.Background = palette.Background;
                    card.BorderBrush = palette.BorderBrush;
                    sample.Source = Draw(id, palette);
                }
                var on = id == current;
                ring.BorderBrush = on ? selection : Brushes.Transparent;
                button.IsChecked = on;
            }
        }
        finally { _syncing = false; }
    }

    /// <summary>Örnek halka: widget'ın görünüşüyle beş Fluent simgeli öğe, gerçek <see cref="RingView"/> ile; dondurulmuş resim.</summary>
    private BitmapSource? Draw(string id, WidgetPalette palette)
    {
        var sample = new WidgetConfig
        {
            Kind = WidgetKind.Launcher, Variant = WidgetVariants.Ring, Style = _config.Style, Accent = _config.Accent,
            LookTheme = _config.LookTheme, LookAccentColor = _config.LookAccentColor, IconSize = IconSize.Small,
            HiddenParts = [RingView.PartKeys],
        };
        RingStyles.Apply(sample, id);
        var tab = new LauncherTab { Actions = [] };
        foreach (var symbol in SampleSymbols)
            tab.Actions.Add(new LauncherAction { Type = RingActionTypes.Url, Url = "https://example.com/", Name = "·", Icon = "sym:" + symbol }); // l10n: çevrilmez
        sample.Tabs.Add(tab);
        var scale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var view = new RingView { PixelsPerDipHint = scale, StableSize = false, CenterShowsIcon = true };
        try
        {
            view.ApplyPalette(palette, rebuild: false);
            view.SetRing(sample);
            view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            view.Arrange(new Rect(view.DesiredSize));
            var size = view.Metrics.Size;
            if (size <= 0) return null;
            var pixels = (int)Math.Ceiling(SampleDip * scale);
            var dpi = 96 * pixels / size;
            var bitmap = new RenderTargetBitmap(pixels, pixels, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(view);
            bitmap.Freeze();
            return bitmap;
        }
        finally { view.Release(); }
    }

    private static readonly string[] SampleSymbols = ["Globe24", "Mail24", "MusicNote224", "Image24", "Settings24"]; // l10n: çevrilmez
}
