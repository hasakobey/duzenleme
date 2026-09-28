using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Duzenleme.Views;

/// <summary>
/// "Yeni bölme…": adı, ne göstereceği ve simgesiyle bölme ekler (tek tıklık kutucukların yanında, eklerken ad ve simge
/// seçmek isteyen için). Ne göstersin: masaüstünde bu adla yeni klasör (varsayılan), var olan bir masaüstü klasörü ya da
/// masaüstündeki öğeler (Klasörler, Kısayollar, Dosyalar, Tümü). Küçük, kip pencere; çağıranın monitöründe açılır.
/// </summary>
internal sealed class NewFenceDialog : FluentWindow
{
    private readonly TextBox _name;
    private readonly RadioButton _newFolder, _existing, _desktop;
    private readonly ComboBox _folders, _filters;
    private readonly ListBox _icons;
    private readonly TextBlock _error, _hint;
    private readonly List<string> _desktopFolders;
    private WidgetConfig? _added;

    /// <summary>Pencereyi gösterir; eklenen bölmenin ayarını (vazgeçilirse ya da klasör açılamazsa null) döner.</summary>
    public static WidgetConfig? Ask(NativeMethods.POINT? near)
    {
        var dialog = new NewFenceDialog(near);
        dialog.ShowDialog();
        return dialog._added;
    }

    private NewFenceDialog(NativeMethods.POINT? near)
    {
        if (Application.Current?.MainWindow == this) Application.Current.MainWindow = null;
        Title = L.T("Yeni bölme");
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.Mica;
        ShowInTaskbar = false;
        Topmost = true;
        MinWidth = 0;
        MinHeight = 0;
        AutomationProperties.SetAutomationId(this, "NewFence");
        if (near is { } point) WindowFit.CenterOn(this, point);
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _desktopFolders = AppHost.DesktopFolders().OrderBy(n => n, L.Sorter).ToList();
        const double width = 8 * 48 + 8;
        var body = new StackPanel { Margin = new Thickness(22, 2, 22, 0), Width = width };

        body.Children.Add(Label(L.T("Ad"), 0));
        _name = new TextBox { PlaceholderText = L.T("ör. Projeler"), MaxLength = TitleEditor.MaxLength };
        AutomationProperties.SetAutomationId(_name, "NewFence.Name");
        AutomationProperties.SetName(_name, L.T("Ad"));
        _name.TextChanged += (_, _) => Validate(showErrors: false);
        body.Children.Add(_name);
        _hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.75, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
        body.Children.Add(_hint);

        body.Children.Add(Label(L.T("Ne göstersin?"), 16));
        _newFolder = Choice(L.T("Masaüstünde bu adla yeni klasör"), "NewFence.NewFolder");
        _newFolder.IsChecked = true;
        body.Children.Add(_newFolder);

        _existing = Choice(L.T("Var olan bir masaüstü klasörü"), "NewFence.Existing");
        _folders = new ComboBox { Margin = new Thickness(28, 4, 0, 6), MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var folder in _desktopFolders) _folders.Items.Add(folder);
        AutomationProperties.SetAutomationId(_folders, "NewFence.Folder");
        AutomationProperties.SetName(_folders, L.T("Klasör"));
        if (_desktopFolders.Count > 0) _folders.SelectedIndex = 0;
        else _existing.IsEnabled = false;
        _folders.SelectionChanged += (_, _) => { _existing.IsChecked = true; Validate(showErrors: false); };
        body.Children.Add(_existing);
        body.Children.Add(_folders);

        _desktop = Choice(L.T("Masaüstündeki öğeler"), "NewFence.Desktop");
        _filters = new ComboBox { Margin = new Thickness(28, 4, 0, 0), MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var filter in DesktopItems.Filters) _filters.Items.Add(new ComboBoxItem { Content = DesktopItems.Description(filter), Tag = filter });
        _filters.SelectedIndex = 0;
        AutomationProperties.SetAutomationId(_filters, "NewFence.Filter");
        AutomationProperties.SetName(_filters, L.T("Masaüstündeki öğeler"));
        _filters.SelectionChanged += (_, _) => { _desktop.IsChecked = true; Validate(showErrors: false); };
        body.Children.Add(_desktop);
        body.Children.Add(_filters);
        foreach (var choice in new[] { _newFolder, _existing, _desktop }) choice.Checked += (_, _) => Validate(showErrors: false);

        body.Children.Add(Label(L.T("Simge"), 16));
        _icons = IconPicker.SymbolGrid(new SymbolIcon { Symbol = SymbolRegular.FolderOpen24, Filled = true, FontSize = 22 }, rows: 3, "NewFence.Icons");
        _icons.SelectedIndex = 0;
        body.Children.Add(_icons);

        _error = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        body.Children.Add(_error);

        var add = new Button { Content = L.T("Ekle"), Appearance = ControlAppearance.Primary, IsDefault = true, MinWidth = 100 };
        AutomationProperties.SetAutomationId(add, "NewFence.Add");
        add.Click += (_, _) => Add();
        var cancel = new Button { Content = L.T("Vazgeç"), IsCancel = true, MinWidth = 100, Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetAutomationId(cancel, "NewFence.Cancel");
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(22, 16, 22, 20),
        };
        buttons.Children.Add(add);
        buttons.Children.Add(cancel);

        var root = new System.Windows.Controls.Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TitleBar { Title = L.T("Yeni bölme"), ShowMaximize = false, ShowMinimize = false });
        System.Windows.Controls.Grid.SetRow(body, 1);
        root.Children.Add(body);
        System.Windows.Controls.Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        Content = root;

        Loaded += (_, _) => { _name.Focus(); Keyboard.Focus(_name); };
    }

    private static TextBlock Label(string text, double top) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, top, 0, 6),
    };

    private static RadioButton Choice(string text, string automationId)
    {
        var radio = new RadioButton { Content = text, GroupName = "NewFenceSource", Margin = new Thickness(0, 2, 0, 0) };
        AutomationProperties.SetAutomationId(radio, automationId);
        return radio;
    }

    private string TypedName => FileNames.Normalize(_name.Text);

    /// <summary>
    /// Girdiyi denetler. Yeni klasörde ad gerekir ve klasör adı olabilmelidir; masaüstünde aynı adda klasör varsa bölmenin onu
    /// göstereceği söylenir. Diğerlerinde ad isteğe bağlıdır (boşsa varsayılan başlık). Eklenebilirse true.
    /// </summary>
    private bool Validate(bool showErrors)
    {
        string? error = null;
        _hint.Visibility = Visibility.Collapsed;
        if (_newFolder.IsChecked == true)
        {
            var name = TypedName;
            if (name.Length == 0) error = L.T("Yeni klasör için bir ad yaz.");
            else if (FileNames.Validate(name, AppHost.DesktopDirectory) is { } invalid) error = invalid;
            else if (_desktopFolders.Any(f => FolderName.Equal(f, name)))
            {
                _hint.Text = L.T("Masaüstünde bu adda bir klasör var; bölme onu gösterir.");
                _hint.Visibility = Visibility.Visible;
            }
        }
        else if (_existing.IsChecked == true && _folders.SelectedItem is not string) error = L.T("Bir klasör seç.");
        // "Varsayılan" hücresi bölmenin kendi simgesini göstersin (klasör, PDF, Kısayollar…).
        if (_icons?.Items.Count > 0 && _icons.Items[0] is ListBoxItem { Content: SymbolIcon defaultIcon })
            defaultIcon.Symbol = WidgetIcons.DefaultFor(new WidgetConfig
            {
                Kind = WidgetKind.Fence,
                Filter = _desktop.IsChecked == true && _filters.SelectedItem is ComboBoxItem { Tag: DesktopFilter f } ? f : DesktopFilter.None,
                FolderName = _existing.IsChecked == true ? _folders.SelectedItem as string : TypedName,
            });
        if (error is not null && (showErrors || _error.Visibility == Visibility.Visible))
        {
            _error.Text = error;
            _error.Visibility = Visibility.Visible;
        }
        else if (error is null) _error.Visibility = Visibility.Collapsed;
        return error is null;
    }

    private void Add()
    {
        if (!Validate(showErrors: true)) return;
        var name = TypedName;
        var icon = (_icons.SelectedItem as ListBoxItem)?.Tag as string;
        var widgets = AppHost.Widgets;
        if (_newFolder.IsChecked == true)
        {
            widgets.NextSetup = c => c.Icon = icon;
            _added = widgets.AddFolderFence(name);
        }
        else if (_existing.IsChecked == true && _folders.SelectedItem is string folder)
        {
            widgets.NextSetup = c =>
            {
                c.Icon = icon;
                if (name.Length > 0 && !string.Equals(name, folder, StringComparison.Ordinal)) c.Title = name;
            };
            _added = widgets.Add(WidgetKind.Fence, folder);
        }
        else if (_filters.SelectedItem is ComboBoxItem { Tag: DesktopFilter filter })
        {
            widgets.NextSetup = c =>
            {
                c.Icon = icon;
                if (name.Length > 0 && !string.Equals(name, DesktopItems.Label(filter), StringComparison.Ordinal)) c.Title = name;
            };
            _added = widgets.AddFence(filter);
        }
        // Klasör açılamadıysa (kullanıcı uyarıldı) son dokunuş sonraki widget'a kalmasın.
        widgets.NextSetup = null;
        if (_added is not null) DialogResult = true;
    }
}
