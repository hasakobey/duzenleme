using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Duzenleme.Core;
using Duzenleme.Widgets;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Görünüm galerisi (<see cref="LookGallery"/>; Düzenle paneli, sonra Masaüstüm sayfasının "Tüm widget'lar" şeridi): sekiz küçük
/// kart (72×48), her biri gerçek widget paletiyle çizilir (zemin, kenarlık, yazı ve vurgu rengi; o görünüm seçilirse widget'ın
/// alacağı renkler). Seçili kartın çevresinde vurgu halkası. Fareyle hiçbir kart önizlenmez ya da büyümez: widget yalnızca
/// tıklanınca değişir (widget'ın kendisi önizlemedir). Sekme grupta bir kez durur, oklar gezer ve seçer.
/// </summary>
internal sealed class LookGalleryControl : UniformGrid
{
    public const double CardWidth = 72, CardHeight = 48;

    private readonly Func<string, WidgetPalette> _paletteFor;
    private readonly Func<string?> _current;
    private readonly Action<string> _choose;
    private readonly List<(LookEntry Entry, RadioButton Button, Border Card, Border Ring, TextBlock Sample, Border Accent)> _cards = [];
    private bool _syncing;

    /// <param name="automationPrefix">Kartların AutomationId öneki ("Editor.Look." → "Editor.Look.neon").</param>
    /// <param name="paletteFor">Kart seçilirse widget'ın paleti (çizim için).</param>
    /// <param name="current">Seçili kartın kimliği (yoksa null).</param>
    /// <param name="choose">Kart seçildi.</param>
    public LookGalleryControl(string automationPrefix, Func<string, WidgetPalette> paletteFor, Func<string?> current, Action<string> choose)
    {
        _paletteFor = paletteFor;
        _current = current;
        _choose = choose;
        Columns = 4;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetName(this, L.T("Görünüm"));
        var group = "LookGallery" + Guid.NewGuid().ToString("N");
        foreach (var entry in LookGallery.All) Children.Add(Card(entry, automationPrefix + entry.Id, group));
        Refresh();
    }

    private UIElement Card(LookEntry entry, string automationId, string group)
    {
        var sample = new TextBlock
        {
            Text = "Aa", FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(9, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        var accent = new Border
        {
            Width = 24, Height = 5, CornerRadius = new CornerRadius(2.5), Margin = new Thickness(9, 0, 0, 9),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
        };
        var card = new Border
        {
            Width = CardWidth, Height = CardHeight, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
            Child = new Grid { Children = { sample, accent } },
        };
        // Seçili kartın halkası: kartın dışında, yerleşimi değiştirmeden (kenarlık hep 2 DIP, yalnızca rengi değişir).
        var ring = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(2), Padding = new Thickness(1), Child = card };
        var label = new TextBlock
        {
            Text = entry.Label, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var content = new StackPanel { Children = { ring, label } };

        // Düz şablon: WPF-UI'nin geçişli RadioButton şablonu yok (fareyle hiçbir şey değişmez); klavye odağı GalleryFocus çerçevesiyle.
        var button = new RadioButton
        {
            Content = content, GroupName = group, Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand, MinWidth = 0, MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            Template = new ControlTemplate(typeof(RadioButton)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
        };
        button.FocusVisualStyle = GalleryFocus.Style;
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, entry.Label);
        button.Checked += (_, _) => { if (!_syncing) _choose(entry.Id); };
        button.GotKeyboardFocus += (_, e) =>
        {
            var arrows = Keyboard.IsKeyDown(Key.Left) || Keyboard.IsKeyDown(Key.Right) || Keyboard.IsKeyDown(Key.Up) || Keyboard.IsKeyDown(Key.Down);
            if (arrows && e.OldFocus is RadioButton { GroupName: var g } && g == group && button.IsChecked != true) button.IsChecked = true;
        };
        _cards.Add((entry, button, card, ring, sample, accent));
        return button;
    }

    /// <summary>Kartları gerçek paletle yeniden çizer ve seçili kartı işaretler (görünüş ya da vurgu rengi değişince).</summary>
    public void Refresh()
    {
        var current = _current();
        var selection = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? SystemColors.WindowTextBrush;
        _syncing = true;
        try
        {
            foreach (var (entry, button, card, ring, sample, accent) in _cards)
            {
                var palette = _paletteFor(entry.Id);
                card.Background = palette.Background;
                card.BorderBrush = palette.BorderBrush;
                sample.Foreground = palette.Foreground;
                accent.Background = palette.Accent;
                var on = entry.Id == current;
                ring.BorderBrush = on ? selection : Brushes.Transparent;
                button.IsChecked = on;
            }
        }
        finally { _syncing = false; }
    }
}

/// <summary>
/// Galeri kartlarının klavye odağı çerçevesi (<see cref="LookGalleryControl"/>, <see cref="RingStyleGallery"/>): kartın çevresinde
/// Windows'un odak renginde 2 DIP yuvarlak kenar. WPF yalnızca klavyeyle gelen odakta çizer; fareyle belirmez, hareket yok.
/// </summary>
internal static class GalleryFocus
{
    public static Style Style { get; } = Create();

    private static Style Create()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        border.SetValue(FrameworkElement.MarginProperty, new Thickness(-3));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        border.SetResourceReference(Border.BorderBrushProperty, "FocusStrokeColorOuterBrush");
        var style = new Style(typeof(Control));
        style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Control)) { VisualTree = border }));
        style.Seal();
        return style;
    }
}
