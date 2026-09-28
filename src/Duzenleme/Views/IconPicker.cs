using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using Image = System.Windows.Controls.Image;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Simge seçici: widget başlığına (bölme, kutu, "Yeni bölme…") ya da kısayol kutusu öğesine simge seçtirir. Widget'ın
/// yanında, onun monitöründe açılır (birincil monitörün ortasında değil). Hücre seçilince widget hemen o simgeyle görünür
/// (canlı önizleme; kaydedilmez), Kaydet yazar, Vazgeç/Esc/kapatma eski simgeye döner. Kutu öğesinde Windows'un "Simge
/// Değiştir" penceresi (.ico/.exe/.dll içindeki simgeler) ve "Resim dosyasından…" da var. Dosya penceresi açtığı için
/// odak gidince kapanmaz. Aynı anda tek seçici açıktır.
/// </summary>
internal sealed class IconPicker : FluentWindow
{
    private static IconPicker? _current;

    private readonly WidgetWindow? _anchor;
    private readonly string? _original;
    private readonly Action<string?> _preview;
    private readonly Action<string?> _commit;
    private readonly ListBox _grid;
    private readonly TextBlock _error;
    private readonly string? _itemPath;
    private ListBoxItem? _customCell;
    private bool _saved;

    /// <summary>Widget başlığının simgesi (yalnızca Fluent simgeleri). name: widget'ın görünen başlığı.</summary>
    public static void ForWidget(WidgetWindow anchor, WidgetConfig config, string name, Action<string?> preview, Action<string?> commit) =>
        Open(new IconPicker(anchor, L.F("\"{0}\" için simge", name), config.Icon, WidgetIcons.DefaultFor(config), null, preview, commit));

    /// <summary>Kısayol kutusu öğesinin simgesi: Fluent simgeleri, Windows simgeleri ve resim dosyası.</summary>
    public static void ForItem(WidgetWindow anchor, TileItem item, string? current, Action<string?> preview, Action<string?> commit) =>
        Open(new IconPicker(anchor, L.F("\"{0}\" için simge", item.Name), current, null, item.Path, preview, commit));

    private static void Open(IconPicker picker)
    {
        _current?.Close();
        _current = picker;
        picker.Closed += (_, _) => { if (_current == picker) _current = null; };
        picker.Show();
    }

    /// <param name="defaultSymbol">"Varsayılan" hücresinde gösterilecek simge (widget); öğede dosyanın kendi simgesi.</param>
    /// <param name="itemPath">Kısayol kutusu öğesinin yolu; widget başlığında null.</param>
    private IconPicker(WidgetWindow? anchor, string heading, string? current, SymbolRegular? defaultSymbol, string? itemPath,
        Action<string?> preview, Action<string?> commit)
    {
        _anchor = anchor;
        _original = current;
        _preview = preview;
        _commit = commit;
        _itemPath = itemPath;
        // WPF ilk açılan pencereyi Application.MainWindow yapar; tema değişikliği (WPF-UI) bu küçük pencereye bağlanmasın.
        if (Application.Current?.MainWindow == this) Application.Current.MainWindow = null;

        Title = L.T("Simge seç");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.Mica;
        ShowInTaskbar = false;
        Topmost = true;
        MinWidth = 0;
        MinHeight = 0;
        AutomationProperties.SetAutomationId(this, "IconPicker");
        // En baştan widget'ın monitöründe oluşur (ölçeği farklı monitöre sonradan geçip büyümesin); yeri yüklenince kartın yanı.
        var anchorPoint = anchor?.CenterPoint ?? CursorPoint();
        WindowFit.FitChromeToContent(this);
        WindowFit.StartOn(this, anchorPoint);
        MaxHeight = Math.Max(300, NativeMethods.WorkAreaAt(anchorPoint).Height / NativeMethods.ScaleAt(anchorPoint) - 16);

        const double width = 8 * 48 + 8;
        var body = new StackPanel { Margin = new Thickness(20, 4, 20, 0), Width = width };
        body.Children.Add(new TextBlock
        {
            Text = heading, FontSize = 16, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 10),
        });

        // İlk hücre: varsayılan (türün simgesi ya da dosyanın kendi simgesi).
        UIElement defaultContent;
        if (defaultSymbol is { } symbol) defaultContent = new SymbolIcon { Symbol = symbol, Filled = true, FontSize = 22 };
        else
        {
            var own = NewImage();
            if (itemPath is not null) ShellIcons.Request(TileItem.NativePath(itemPath), CellPixels, false, icon => own.Source = icon);
            defaultContent = own;
        }
        _grid = SymbolGrid(defaultContent, rows: 6, "IconPicker.Grid");
        // Öğenin şimdiki özel simgesi (Windows simgesi ya da resim) listede olmadığı için kendi hücresinde.
        if (IconRef.Parse(current) is { Kind: not IconRefKind.Symbol } custom) SetCustomCell(custom);
        Select(_grid, current);
        _grid.SelectionChanged += (_, _) => { if (_grid.SelectedItem is ListBoxItem cell) _preview(cell.Tag as string); };
        _grid.MouseDoubleClick += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && ItemsControl.ContainerFromElement(_grid, d) is ListBoxItem) Save();
        };
        body.Children.Add(_grid);

        _error = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        body.Children.Add(_error);

        var buttons = new DockPanel { Margin = new Thickness(20, 14, 20, 18), Width = width, LastChildFill = false };
        if (itemPath is not null)
        {
            var fromWindows = new Button { Content = L.T("Windows simgelerinden…"), Margin = new Thickness(0, 0, 8, 0) };
            AutomationProperties.SetAutomationId(fromWindows, "IconPicker.FromWindows");
            fromWindows.ToolTip = L.T("Programların ve Windows'un simgelerinden seç (Simge Değiştir penceresi)");
            fromWindows.Click += (_, _) => PickFromWindows();
            var fromFile = new Button { Content = L.T("Resim dosyasından…") };
            AutomationProperties.SetAutomationId(fromFile, "IconPicker.FromFile");
            fromFile.ToolTip = L.T("PNG, ICO, JPG, BMP ya da GIF; bu bilgisayarda uygulama verisine kopyalanır");
            fromFile.Click += (_, _) => PickFromFile();
            buttons.Children.Add(fromWindows);
            buttons.Children.Add(fromFile);
        }
        var cancel = new Button { Content = L.T("Vazgeç"), IsCancel = true, MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetAutomationId(cancel, "IconPicker.Cancel");
        cancel.Click += (_, _) => Close();
        var save = new Button { Content = L.T("Kaydet"), Appearance = ControlAppearance.Primary, IsDefault = true, MinWidth = 90 };
        AutomationProperties.SetAutomationId(save, "IconPicker.Save");
        save.Click += (_, _) => Save();
        DockPanel.SetDock(cancel, Dock.Right);
        DockPanel.SetDock(save, Dock.Right);
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TitleBar { Title = L.T("Simge seç"), ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        Content = root;

        Loaded += (_, _) =>
        {
            PlaceNextToWidget(anchorPoint);
            if (_grid.SelectedItem is ListBoxItem selected)
            {
                _grid.ScrollIntoView(selected);
                selected.Focus();
            }
        };
        Closed += (_, _) => { if (!_saved) _preview(_original); };
    }

    private static NativeMethods.POINT CursorPoint()
    {
        NativeMethods.GetCursorPos(out var cursor);
        return cursor;
    }

    private static bool SameRef(string? a, string? b) =>
        string.Equals(IconRef.Parse(a)?.ToString(), IconRef.Parse(b)?.ToString(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Simge ızgarası (seçici ve "Yeni bölme…" paylaşır): ilk hücre "Varsayılan" (Tag null), sonra katalogdaki Fluent
    /// simgeleri (Tag "sym:Ad"). Yön tuşlarıyla gezilir; hücrelerin adı ekran okuyucuya ve ipucuna gider.
    /// </summary>
    internal static ListBox SymbolGrid(UIElement defaultContent, int rows, string automationId)
    {
        var grid = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            MaxHeight = rows * 48 + 8,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Disabled);
        grid.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)));
        // WPF-UI'nin liste öğesi renkleri koyu temada simgeleri koyu çizer ve seçileni vurgu rengiyle doldurur: simgeler metin
        // renginde, seçili hücre hafif zeminle (Windows'un emoji paneli gibi) okunur kalsın.
        foreach (var (key, source) in new[]
                 {
                     ("ListBoxItemForeground", "TextFillColorPrimaryBrush"),
                     ("ListBoxItemSelectedForegroundThemeBrush", "TextFillColorPrimaryBrush"),
                     ("ListBoxItemSelectedBackgroundThemeBrush", "SubtleFillColorTertiaryBrush"),
                 })
            if (Application.Current?.TryFindResource(source) is Brush brush) grid.Resources[key] = brush;
        AutomationProperties.SetAutomationId(grid, automationId);
        AutomationProperties.SetName(grid, L.T("Simgeler"));
        grid.Items.Add(Cell(defaultContent, null, L.T("Varsayılan"), "Icon.Default"));
        foreach (var (glyph, label) in IconCatalog.All)
            grid.Items.Add(Cell(new SymbolIcon { Symbol = glyph, Filled = true, FontSize = 22 }, IconRef.Symbol(glyph.ToString()),
                L.Dyn(label), "Icon." + glyph));
        return grid;
    }

    /// <summary>Izgarada simgeyi seçer (yoksa "Varsayılan").</summary>
    internal static void Select(ListBox grid, string? iconRef) =>
        grid.SelectedItem = grid.Items.OfType<ListBoxItem>().FirstOrDefault(i => SameRef(i.Tag as string, iconRef)) ?? grid.Items[0];

    private static ListBoxItem Cell(UIElement content, string? iconRef, string label, string automationId)
    {
        var cell = new ListBoxItem
        {
            Content = content, Tag = iconRef, Width = 44, Height = 44, Margin = new Thickness(2),
            Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center, ToolTip = label,
        };
        AutomationProperties.SetName(cell, label);
        AutomationProperties.SetAutomationId(cell, automationId);
        return cell;
    }

    /// <summary>Windows simgesi ya da resim: listede olmadığı için ikinci hücrede gösterilir (yüklenince).</summary>
    private const double CellImage = 28;

    private static int CellPixels => (int)Math.Round(CellImage * NativeMethods.SystemPixelsPerDip);

    private static Image NewImage()
    {
        var image = new Image { Width = CellImage, Height = CellImage, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    private void SetCustomCell(IconRef custom)
    {
        var image = NewImage();
        void Show(ImageSource? icon) => image.Source = icon;
        if (custom.Kind == IconRefKind.Resource) ShellIcons.RequestResource(custom.Value, custom.Index, CellPixels, Show);
        else if (IconFiles.PathOf(custom.Value, AppHost.IconsDirectory) is { } file) ShellIcons.RequestImage(file, CellPixels, Show);
        var cell = Cell(image, custom.ToString(), L.T("Seçilen simge"), "Icon.Custom");
        if (_customCell is not null) _grid.Items.Remove(_customCell);
        _customCell = cell;
        _grid.Items.Insert(Math.Min(1, _grid.Items.Count), cell);
    }

    private void Choose(IconRef custom)
    {
        _error.Visibility = Visibility.Collapsed;
        SetCustomCell(custom);
        _grid.SelectedItem = _customCell;
        _customCell?.Focus();
    }

    private void Save()
    {
        _saved = true;
        _commit((_grid.SelectedItem as ListBoxItem)?.Tag as string);
        Close();
    }

    // ------------------------------------------------------------------ Windows simgeleri ve resim dosyası

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "PickIconDlg")]
    private static extern int PickIconDlg(IntPtr owner, StringBuilder path, int capacity, ref int index);

    /// <summary>Windows'un "Simge Değiştir" penceresi: öğe bir program/kitaplık/simge dosyasıysa ondan, değilse imageres.dll'den başlar.</summary>
    private void PickFromWindows()
    {
        var start = IconRef.Parse(_original) is { Kind: IconRefKind.Resource } r ? r.Value
            : _itemPath is { } path && Path.GetExtension(path).ToLowerInvariant() is ".exe" or ".dll" or ".ico" or ".icl" ? path
            : @"%SystemRoot%\System32\imageres.dll";
        var index = IconRef.Parse(_original) is { Kind: IconRefKind.Resource } current ? current.Index : 0;
        var buffer = new StringBuilder(start, 1024);
        if (PickIconDlg(new WindowInteropHelper(this).Handle, buffer, buffer.Capacity, ref index) == 0) return;
        Choose(new IconRef(IconRefKind.Resource, buffer.ToString(), index));
    }

    /// <summary>Resim seçtirir; veri klasörüne arka planda kopyalanır (asıl dosya silinse de simge kalır).</summary>
    private async void PickFromFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L.T("Simge için resim seç"),
            Filter = L.T("Resimler") + "|" + string.Join(";", IconFiles.Extensions.Select(e => "*" + e)),
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true) return;
        var source = dialog.FileName;
        var folder = AppHost.IconsDirectory;
        try
        {
            var stored = await Task.Run(() => IconFiles.Import(source, folder));
            if (IsLoaded) Choose(new IconRef(IconRefKind.Image, stored));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _error.Text = L.T("Bu resim kullanılamadı. En çok 8 MB'lık PNG, ICO, JPG, BMP ya da GIF seç.");
            _error.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Kartın yanına (sağına, sığmazsa soluna, altına, üstüne) aynı monitörde yerleştirir.</summary>
    private void PlaceNextToWidget(NativeMethods.POINT anchorPoint)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetWindowRect(hwnd, out var r)) return;
        var work = NativeMethods.WorkAreaAt(anchorPoint);
        var area = new Box(work.Left, work.Top, work.Right, work.Bottom);
        var card = _anchor?.VisibleCardBox ?? new Box(anchorPoint.X, anchorPoint.Y, anchorPoint.X, anchorPoint.Y);
        var gap = (int)Math.Round(12 * NativeMethods.ScaleAt(anchorPoint));
        var (x, y) = AnchorPlacement.NextTo(card, r.Width, r.Height, area, gap);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER);
        Activate();
    }
}
