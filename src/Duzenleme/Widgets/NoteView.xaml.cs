using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>Masaüstünde yüzen yapışkan not (SlideSlide'daki notlar gibi). Yazdıkça kaydedilir.</summary>
public partial class NoteView : UserControl, IWidgetView
{
    private static readonly (NoteColor Color, string Label)[] Colors =
    [
        (NoteColor.Yellow, "Sarı"), (NoteColor.Pink, "Pembe"), (NoteColor.Green, "Yeşil"),
        (NoteColor.Blue, "Mavi"), (NoteColor.Purple, "Mor"), (NoteColor.Graphite, "Grafit"),
    ];

    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _saveTimer;

    public NoteView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        Editor.Text = config.NoteText;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _saveTimer.Tick += (_, _) => Save();
        Editor.TextChanged += (_, _) => { _saveTimer.Stop(); _saveTimer.Start(); };
        Editor.LostKeyboardFocus += (_, _) => Save();
        // Yazı alanına sağ tıklayınca da not ayarlarına ulaşılabilsin (varsayılan menüde yalnızca Kes/Kopyala/Yapıştır var).
        Editor.ContextMenu = Menus.Dynamic(menu =>
        {
            menu.Items.Add(new MenuItem { Header = "Kes", Command = ApplicationCommands.Cut, CommandTarget = Editor });
            menu.Items.Add(new MenuItem { Header = "Kopyala", Command = ApplicationCommands.Copy, CommandTarget = Editor });
            menu.Items.Add(new MenuItem { Header = "Yapıştır", Command = ApplicationCommands.Paste, CommandTarget = Editor });
            menu.Items.Add(new MenuItem { Header = "Tümünü seç", Command = ApplicationCommands.SelectAll, CommandTarget = Editor });
            menu.Items.Add(new Separator());
            menu.Items.Add(Menus.Item("Not ayarları…", () => MenuRequested?.Invoke()));
        });
        UpdateTitle();
        // Başlıktaki × yalnızca fare üstündeyken görünür (kilitli widget'ta hiç).
        MouseEnter += (_, _) => RemoveButton.Visibility = _config.Locked ? Visibility.Collapsed : Visibility.Visible;
        MouseLeave += (_, _) => RemoveButton.Visibility = Visibility.Collapsed;
    }

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    public bool Resizable => true;
    public Thickness CardPadding => new(16, 12, 16, 14);
    public event Action? MenuRequested;

    private void Save()
    {
        _saveTimer.Stop();
        if (_config.NoteText == Editor.Text) return;
        _config.NoteText = Editor.Text;
        AppHost.SaveSettings();
    }

    private void UpdateTitle()
    {
        TitleText.Text = string.IsNullOrWhiteSpace(_config.Title) ? "Not" : _config.Title;
        HeaderRow.Visibility = _config.Shows("header") ? Visibility.Visible : Visibility.Collapsed;
    }

    public WidgetPalette AdjustPalette(WidgetPalette palette) => WidgetPalette.ForNote(_config.NoteColor);

    public void ApplyPalette(WidgetPalette palette)
    {
        Editor.Foreground = palette.Foreground;
        Editor.CaretBrush = palette.Foreground;
        Editor.SelectionBrush = palette.Accent;
        TitleText.Foreground = palette.Secondary;

        // Başlıktaki renk seçici noktalar.
        Swatches.Children.Clear();
        foreach (var (color, label) in Colors)
        {
            var p = WidgetPalette.ForNote(color);
            var dot = new Ellipse
            {
                Width = 12, Height = 12, Margin = new Thickness(3, 0, 0, 0), Cursor = Cursors.Hand, ToolTip = label,
                Fill = p.Background, Stroke = color == _config.NoteColor ? palette.Foreground : palette.BorderBrush,
                StrokeThickness = color == _config.NoteColor ? 1.6 : 1,
            };
            dot.MouseLeftButtonDown += (_, e) => { e.Handled = true; SetColor(color); };
            Swatches.Children.Add(dot);
        }
    }

    private void SetColor(NoteColor color)
    {
        _config.NoteColor = color;
        AppHost.SaveSettings();
        AppHost.Widgets.Restyle(_config.Id);
    }

    public void AddMenuItems(ContextMenu menu)
    {
        menu.Items.Add(Menus.Choice("Renk", _config.NoteColor, Colors, SetColor));
        menu.Items.Add(Menus.Item("Başlığı değiştir…", () =>
        {
            if (InputDialog.Ask("Not başlığı", "Başlık", TitleText.Text) is { } title)
            {
                _config.Title = string.IsNullOrWhiteSpace(title) ? null : title;
                AppHost.SaveSettings();
                UpdateTitle();
            }
        }));
        menu.Items.Add(Menus.Item("Notu temizle", () => Editor.Clear()));
        menu.Items.Add(Menus.Item("Panoya kopyala", () => { if (Editor.Text.Length > 0) Clipboard.SetText(Editor.Text); }));
        menu.Items.Add(Menus.Parts(_config, [("header", "Başlık ve renkler")], UpdateTitle));
    }

    public void FocusEditor()
    {
        Editor.Focus();
        Keyboard.Focus(Editor);
        Editor.CaretIndex = Editor.Text.Length;
    }

    public void Flush() => Save();

    public void Detach() => Save();
}
