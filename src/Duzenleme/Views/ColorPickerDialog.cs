using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace Duzenleme.Views;

/// <summary>
/// Özel vurgu rengi seçici (Görünüm ▸ Vurgu rengi ▸ Özel renk…): büyük renk kutuları (4 × 6), renk kodu ("#RRGGBB") ve
/// "Windows vurgu rengi". Widget'ın yanında, onun monitöründe açılır; seçilen renk widget'ta hemen görünür (önizleme,
/// kaydedilmez), Kaydet yazar, Vazgeç/Esc/kapatma kayıtlı renge döner. Renk widget'ın zemininde okunur kalsın diye çizerken
/// gerekirse açılır ya da koyulaştırılır (<see cref="ThemeMath.EnsureContrast"/>). Aynı anda tek seçici açıktır. Renk
/// geçişi yok: önizleme anında.
/// </summary>
internal sealed class ColorPickerDialog : FluentWindow
{
    private static ColorPickerDialog? _current;

    /// <summary>Renk kutuları, satır satır (ColorPicker.Swatch.1 … 24).</summary>
    private static readonly uint[] Swatches =
    [
        0xFFE5484D, 0xFFF76B15, 0xFFE2A400, 0xFF30A46C, 0xFF0894B3, 0xFF3E63DD,
        0xFF6E56CF, 0xFF8E4EC6, 0xFFC2298A, 0xFFE93D82, 0xFFD13415, 0xFF12A594,
        0xFFFF9592, 0xFFFFA057, 0xFFF5D90A, 0xFF65BA74, 0xFF60B3D7, 0xFF8DA4EF,
        0xFF8E1F25, 0xFF9A3B0A, 0xFF7A5A00, 0xFF1F6B45, 0xFF0B5770, 0xFF2A3F94,
    ];

    private const int Columns = 6;
    private const double SwatchSize = 40;

    private readonly Func<Box?> _cardBox;
    private readonly Action<string?> _preview;
    private readonly Action<string> _commit;
    private readonly TextBox _hex;
    private readonly Border _sample;
    private readonly Button _save;
    private readonly List<(Border Face, TextBlock Check, uint Color)> _cells = [];
    private string? _chosen;
    private bool _syncing;

    /// <param name="current">Açılışta seçili renk ("#RRGGBB").</param>
    /// <param name="preview">Seçilen rengi widget'ta gösterir (null: kayıtlı renge döner).</param>
    /// <param name="commit">Kaydet: rengi yazar.</param>
    public static void ForWidget(WidgetWindow anchor, string current, Action<string?> preview, Action<string> commit)
    {
        _current?.Close();
        var picker = new ColorPickerDialog(anchor.CenterPoint, () => anchor.VisibleCardBox, current, preview, commit);
        _current = picker;
        // Widget kapanırsa (kaldırıldı, Explorer yeniden başladı) seçici de kapanır: kapanmış pencereye ve kaldırılmış ayara yazılmaz.
        void OnAnchorClosed(object? sender, EventArgs e) => picker.Close();
        anchor.Closed += OnAnchorClosed;
        picker.Closed += (_, _) =>
        {
            anchor.Closed -= OnAnchorClosed;
            if (_current == picker) _current = null;
        };
        picker.Show();
    }

    /// <param name="anchorPoint">Widget kartının ortası (fiziksel piksel): seçici bu monitörde, kartın yanında açılır.</param>
    /// <param name="cardBox">Kartın ekrandaki dikdörtgeni (yerleşim için; bilinmiyorsa null).</param>
    internal ColorPickerDialog(NativeMethods.POINT anchorPoint, Func<Box?> cardBox, string current, Action<string?> preview, Action<string> commit)
    {
        _cardBox = cardBox;
        _preview = preview;
        _commit = commit;
        // WPF ilk açılan pencereyi Application.MainWindow yapar; tema değişikliği (WPF-UI) bu küçük pencereye bağlanmasın.
        if (Application.Current?.MainWindow == this) Application.Current.MainWindow = null;

        Title = L.T("Özel renk");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.Mica;
        ShowInTaskbar = false;
        Topmost = true;
        MinWidth = 0;
        MinHeight = 0;
        AutomationProperties.SetAutomationId(this, "ColorPicker");
        // En baştan widget'ın monitöründe oluşur; yüklenince kartın yanına (önizleme görünür kalsın).
        WindowFit.FitChromeToContent(this);
        WindowFit.StartOn(this, anchorPoint);

        const double width = Columns * (SwatchSize + 8);
        var body = new StackPanel { Margin = new Thickness(20, 4, 20, 0), Width = width };

        // Renk kutuları: yön tuşlarıyla gezilir (Tab tek durak), Boşluk/Enter seçer.
        var grid = new UniformGrid { Columns = Columns };
        KeyboardNavigation.SetDirectionalNavigation(grid, KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetTabNavigation(grid, KeyboardNavigationMode.Once);
        AutomationProperties.SetName(grid, L.T("Renkler"));
        for (var i = 0; i < Swatches.Length; i++) grid.Children.Add(Swatch(Swatches[i], i + 1));
        body.Children.Add(grid);

        // Renk kodu ve küçük örnek.
        var codeRow = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
        var codeLabel = new TextBlock { Text = L.T("Renk kodu"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        _hex = new TextBox { Width = 110, MaxLength = 7, VerticalContentAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(_hex, "ColorPicker.Hex");
        AutomationProperties.SetName(_hex, L.T("Renk kodu"));
        _hex.TextChanged += (_, _) => { if (!_syncing) Choose(_hex.Text, fromText: true); };
        _sample = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(6), Margin = new Thickness(10, 0, 0, 0), BorderThickness = new Thickness(1) };
        _sample.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        var system = new Button { Content = L.T("Windows vurgu rengi"), Margin = new Thickness(12, 0, 0, 0) };
        AutomationProperties.SetAutomationId(system, "ColorPicker.Windows");
        system.ToolTip = L.T("Windows'un Kişiselleştirme > Renkler'deki vurgu rengi");
        system.Click += (_, _) => { if (SystemAccent() is { } color) Choose(ThemeMath.ToHex(color), fromText: false); };
        codeRow.Children.Add(codeLabel);
        codeRow.Children.Add(_hex);
        codeRow.Children.Add(_sample);
        codeRow.Children.Add(system);
        body.Children.Add(codeRow);

        var note = new TextBlock
        {
            Text = L.T("Renk, widget'ın zemininde okunur kalsın diye gerekirse biraz açılır ya da koyulaşır."),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), FontSize = 12,
        };
        note.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        body.Children.Add(note);

        var buttons = new DockPanel { Margin = new Thickness(20, 14, 20, 18), Width = width, LastChildFill = false };
        var cancel = new Button { Content = L.T("Vazgeç"), IsCancel = true, MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetAutomationId(cancel, "ColorPicker.Cancel");
        cancel.Click += (_, _) => Close();
        _save = new Button { Content = L.T("Kaydet"), Appearance = ControlAppearance.Primary, IsDefault = true, MinWidth = 90 };
        AutomationProperties.SetAutomationId(_save, "ColorPicker.Save");
        _save.Click += (_, _) => Save();
        DockPanel.SetDock(cancel, Dock.Right);
        DockPanel.SetDock(_save, Dock.Right);
        buttons.Children.Add(cancel);
        buttons.Children.Add(_save);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TitleBar { Title = L.T("Özel renk"), ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        Content = root;

        // Açılışta kayıtlı renk seçili görünür; önizleme yalnızca kullanıcı başka bir renk seçince başlar.
        Show(ThemeMath.NormalizeHex(current), updateText: true);
        Loaded += (_, _) =>
        {
            PlaceNextToWidget(anchorPoint);
            var selected = _cells.FindIndex(c => ThemeMath.ToHex(c.Color) == _chosen);
            if (selected >= 0 && grid.Children[selected] is UIElement cell) cell.Focus();
            else _hex.Focus();
        };
        // Kaydedildiyse renk zaten yazıldı; önizleme yine de bırakılır (widget kayıtlı görünüşüne, yani aynı renge döner).
        Closed += (_, _) => _preview(null);
    }

    private UIElement Swatch(uint color, int number)
    {
        var hex = ThemeMath.ToHex(color);
        var check = new TextBlock
        {
            Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed,
            Foreground = new SolidColorBrush(ToColor(ThemeMath.TextOn(color))),
        };
        var face = new Border
        {
            Width = SwatchSize, Height = SwatchSize, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(ToColor(color)), BorderBrush = Brushes.Transparent, Child = check,
        };
        // Düz düğme: WPF-UI'nin zemin ve fare katmanı yok (fareyle hiçbir şey değişmez); odak çerçevesi klavye için kalır.
        var button = new System.Windows.Controls.Button
        {
            Content = face, Margin = new Thickness(4), Cursor = Cursors.Hand, ToolTip = hex,
            Template = new ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) },
        };
        AutomationProperties.SetAutomationId(button, "ColorPicker.Swatch." + number);
        AutomationProperties.SetName(button, hex);
        button.Click += (_, _) => Choose(hex, fromText: false);
        _cells.Add((face, check, color));
        return button;
    }

    /// <summary>Seçilen rengi gösterir ve widget'ta önizler; okunamayan kod (yazılırken) yalnızca Kaydet'i kapatır.</summary>
    private void Choose(string? text, bool fromText)
    {
        var hex = ThemeMath.NormalizeHex(text);
        if (hex is null)
        {
            if (fromText) _save.IsEnabled = false;
            return;
        }
        Show(hex, updateText: !fromText);
        _preview(hex);
    }

    private void Show(string? hex, bool updateText)
    {
        _chosen = hex;
        _save.IsEnabled = hex is not null;
        var selection = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? SystemColors.WindowTextBrush;
        foreach (var (face, check, color) in _cells)
        {
            var on = ThemeMath.ToHex(color) == hex;
            face.BorderBrush = on ? selection : Brushes.Transparent;
            check.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }
        _sample.Background = hex is not null && ThemeMath.TryParse(hex, out var argb) ? new SolidColorBrush(ToColor(argb)) : Brushes.Transparent;
        if (!updateText || hex is null) return;
        _syncing = true;
        try { _hex.Text = hex; }
        finally { _syncing = false; }
    }

    private void Save()
    {
        if (_chosen is not { } hex) return;
        _commit(hex);
        Close();
    }

    /// <summary>
    /// Windows'un vurgu rengi (Kişiselleştirme > Renkler): kayıt defterindeki AccentColor (ABGR), yoksa pencere çerçevesinin
    /// rengi. Küçük, yerel bir okuma (diske ya da ağa gitmez).
    /// </summary>
    private static uint? SystemAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                var v = unchecked((uint)abgr);
                return 0xFF000000 | (v & 0xFF) << 16 | (v >> 8 & 0xFF) << 8 | (v >> 16 & 0xFF);
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or System.IO.IOException or UnauthorizedAccessException)
        {
        }
        var c = Wpf.Ui.Appearance.ApplicationAccentColorManager.GetColorizationColor();
        return c.A == 0 ? null : 0xFF000000 | (uint)c.R << 16 | (uint)c.G << 8 | c.B;
    }

    private static Color ToColor(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    /// <summary>Kartın yanına (sağına, sığmazsa soluna, altına, üstüne) aynı monitörde yerleştirir.</summary>
    private void PlaceNextToWidget(NativeMethods.POINT anchorPoint)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(hwnd, out var r)) return;
        var work = NativeMethods.WorkAreaAt(anchorPoint);
        var area = new Box(work.Left, work.Top, work.Right, work.Bottom);
        var card = _cardBox() ?? new Box(anchorPoint.X, anchorPoint.Y, anchorPoint.X, anchorPoint.Y);
        var gap = (int)Math.Round(12 * NativeMethods.ScaleAt(anchorPoint));
        var (x, y) = AnchorPlacement.NextTo(card, r.Width, r.Height, area, gap);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
        Activate();
    }
}
