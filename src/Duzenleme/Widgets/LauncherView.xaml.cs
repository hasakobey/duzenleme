using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Sekmeli kısayol kutusu (ViPad'in sekmeleri + LaunchBar Commander'ın dock'u gibi).
/// Sürükle-bırakla uygulama/dosya/klasör eklenir; dosyalar taşınmaz, yalnızca kısayol olarak tutulur.
/// </summary>
public partial class LauncherView : UserControl, IWidgetView
{
    private readonly WidgetConfig _config;
    private WidgetPalette _palette = WidgetPalette.Glass;

    public LauncherView(WidgetConfig config)
    {
        _config = config;
        if (_config.Tabs.Count == 0) _config.Tabs.Add(new LauncherTab());
        InitializeComponent();

        Header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { e.Handled = true; CollapseToggleRequested?.Invoke(); }
        };

        Items.PreviewMouseLeftButtonUp += OnItemClick;
        Menus.AttachItemMenu(Items, FillItemMenu);
        Menus.EnableDragOut(Items, DragDropEffects.Copy | DragDropEffects.Link);
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        Drop += OnDrop;
        Render();
    }

    public bool Resizable => true;
    public bool Collapsible => true;
    public Thickness CardPadding => new(14, 12, 14, 12);
    public event Action? CollapseToggleRequested;
    public event Action? MenuRequested;

    public void SetBodyVisible(bool visible) => Body.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private LauncherTab Current => _config.Tabs[Math.Clamp(_config.ActiveTab, 0, _config.Tabs.Count - 1)];

    private void Render()
    {
        TitleText.Text = string.IsNullOrWhiteSpace(_config.Title) ? "Kısayol kutusu" : _config.Title;
        CountText.Text = $"{_config.Tabs.Sum(t => t.Items.Count)} öğe";
        RenderTabs();

        Items.ItemsPanel = TileItem.Panel(_config);
        var items = Current.Items.Select(p => TileItem.Create(p, _config)).ToList();
        Items.ItemsSource = items;
        EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderTabs()
    {
        TabStrip.Items.Clear();
        for (var i = 0; i < _config.Tabs.Count; i++)
        {
            var index = i;
            var active = i == Math.Clamp(_config.ActiveTab, 0, _config.Tabs.Count - 1);
            var pill = new Button
            {
                Style = (Style)FindResource("PillButton"),
                ToolTip = "Sağ tık: yeniden adlandır, taşı, sil",
                Background = active ? _palette.Accent : new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)),
                Content = new TextBlock
                {
                    Text = _config.Tabs[i].Name,
                    FontSize = 12.5,
                    FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = active ? _palette.AccentForeground : _palette.Secondary,
                },
                ContextMenu = Menus.Dynamic(menu => FillTabMenu(menu, index)),
            };
            pill.Click += (_, _) =>
            {
                _config.ActiveTab = index;
                AppHost.SaveSettings();
                Render();
            };
            TabStrip.Items.Add(pill);
        }
    }

    private void FillTabMenu(ContextMenu menu, int index)
    {
        var tab = _config.Tabs[index];
        menu.Items.Add(Menus.Item("Yeniden adlandır…", () =>
        {
            if (InputDialog.Ask("Sekme adı", "Ad", tab.Name) is { Length: > 0 } name) Change(() => tab.Name = name);
        }));
        if (index > 0)
            menu.Items.Add(Menus.Item("Sola taşı", () => Change(() => Swap(index, index - 1))));
        if (index < _config.Tabs.Count - 1)
            menu.Items.Add(Menus.Item("Sağa taşı", () => Change(() => Swap(index, index + 1))));
        if (_config.Tabs.Count > 1)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Menus.Item("Sekmeyi sil", () => Change(() =>
            {
                _config.Tabs.RemoveAt(index);
                _config.ActiveTab = Math.Clamp(_config.ActiveTab, 0, _config.Tabs.Count - 1);
            })));
        }
    }

    private void Swap(int a, int b)
    {
        (_config.Tabs[a], _config.Tabs[b]) = (_config.Tabs[b], _config.Tabs[a]);
        if (_config.ActiveTab == a) _config.ActiveTab = b;
        else if (_config.ActiveTab == b) _config.ActiveTab = a;
    }

    private void AddTab_Click(object sender, RoutedEventArgs e) => NewTab();

    private void NewTab()
    {
        if (InputDialog.Ask("Yeni sekme", "Sekme adı", $"Sekme {_config.Tabs.Count + 1}") is not { Length: > 0 } name) return;
        Change(() =>
        {
            _config.Tabs.Add(new LauncherTab { Name = name });
            _config.ActiveTab = _config.Tabs.Count - 1;
        });
    }

    private void Change(Action change)
    {
        change();
        AppHost.SaveSettings();
        Render();
    }

    /// <summary>Dock gibi: tek tıkla açılır.</summary>
    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (Menus.ItemAt(Items, e.OriginalSource) is { Missing: false } item)
        {
            TileItem.Launch(item.Path);
            // Açılan öğe seçili kalmasın (vurgusu ve menüsü sonraki tıklamaları karıştırmasın).
            Items.SelectedItem = null;
        }
    }

    private void FillItemMenu(ContextMenu menu, TileItem item)
    {
        menu.Items.Add(Menus.Item("Aç", () => TileItem.Launch(item.Path)));
        var ext = System.IO.Path.GetExtension(item.Path).ToLowerInvariant();
        if (ext is ".exe" or ".lnk" or ".bat" or ".cmd" or ".msc")
            menu.Items.Add(Menus.Item("Yönetici olarak çalıştır", () => TileItem.Launch(item.Path, asAdmin: true)));
        menu.Items.Add(Menus.Item("Dosya konumunu aç", () => TileItem.Reveal(item.Path)));

        if (_config.Tabs.Count > 1)
        {
            var move = new MenuItem { Header = "Sekmeye taşı" };
            foreach (var tab in _config.Tabs.Where(t => t != Current))
                move.Items.Add(Menus.Item(tab.Name, () => Change(() =>
                {
                    Current.Items.Remove(item.Path);
                    tab.Items.Add(item.Path);
                })));
            menu.Items.Add(move);
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item("Listeden kaldır", () => Change(() => Current.Items.Remove(item.Path))));
        menu.Items.Add(Menus.Item("Kutu ayarları…", () => MenuRequested?.Invoke()));
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var ok = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = ok ? DragDropEffects.Link : DragDropEffects.None;
        DropOverlay.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        Change(() =>
        {
            foreach (var path in paths.Where(p => File.Exists(p) || Directory.Exists(p)))
                if (!Current.Items.Contains(path, StringComparer.OrdinalIgnoreCase)) Current.Items.Add(path);
        });
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        Foreground = palette.Foreground;
        HeaderIcon.Foreground = palette.Accent;
        CountText.Foreground = palette.Secondary;
        AddTabIcon.Foreground = palette.Secondary;
        EmptyText.Foreground = palette.Secondary;
        EmptyIcon.Foreground = palette.Secondary;
        DropOverlay.BorderBrush = palette.Accent;
        DropOverlay.Background = new SolidColorBrush(Color.FromArgb(0x55, 0x10, 0x0C, 0x20));
        RenderTabs();
    }

    public void AddMenuItems(ContextMenu menu)
    {
        menu.Items.Add(Menus.Item("Başlığı değiştir…", () =>
        {
            if (InputDialog.Ask("Kutu başlığı", "Başlık", TitleText.Text) is { } title)
                Change(() => _config.Title = string.IsNullOrWhiteSpace(title) ? null : title);
        }));
        menu.Items.Add(Menus.Item("Sekme ekle…", NewTab));
        menu.Items.Add(Menus.Item("Uygulama ya da dosya ekle…", AddFiles));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.TileOptions(_config, Change, singleClickOption: false));
    }

    public bool OnCtrlWheel(int delta)
    {
        Menus.StepIconSize(_config, delta, Change);
        return true;
    }

    /// <summary>Sürükle-bırak dışında da ekleyebilmek için dosya seçici.</summary>
    private void AddFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Kısayol kutusuna ekle",
            Multiselect = true,
            DereferenceLinks = false,
            Filter = "Uygulamalar ve kısayollar|*.exe;*.lnk;*.url;*.appref-ms;*.bat;*.cmd|Tüm dosyalar|*.*",
            InitialDirectory = AppHost.DesktopDirectory,
        };
        if (dialog.ShowDialog() != true) return;
        Change(() =>
        {
            foreach (var path in dialog.FileNames)
                if (!Current.Items.Contains(path, StringComparer.OrdinalIgnoreCase)) Current.Items.Add(path);
        });
    }

    public void Detach() { }
}
