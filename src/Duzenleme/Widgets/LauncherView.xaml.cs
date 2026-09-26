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
        AddTab.MouseLeftButtonUp += (_, e) => { e.Handled = true; NewTab(); };
        AddTab.MouseLeftButtonDown += (_, e) => e.Handled = true;
        Items.PreviewMouseLeftButtonUp += OnItemClick;
        Items.ContextMenu = Menus.Dynamic(FillItemMenu);
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        Drop += OnDrop;
        Render();
    }

    public bool Resizable => true;
    public bool Collapsible => true;
    public event Action? CollapseToggleRequested;

    public void SetBodyVisible(bool visible) => Body.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

    private LauncherTab Current => _config.Tabs[Math.Clamp(_config.ActiveTab, 0, _config.Tabs.Count - 1)];

    private void Render()
    {
        TitleText.Text = string.IsNullOrWhiteSpace(_config.Title) ? "Kısayollar" : _config.Title;
        CountText.Text = $"{_config.Tabs.Sum(t => t.Items.Count)} öğe";
        RenderTabs();

        Items.ItemsPanel = (ItemsPanelTemplate)FindResource(_config.View == ItemView.List ? "TileStackPanel" : "TileWrapPanel");
        var items = Current.Items.Select(p => TileItem.Create(p, _config.IconSize, _config.View)).ToList();
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
            var pill = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(11, 3, 11, 4),
                Margin = new Thickness(0, 0, 4, 4),
                Cursor = Cursors.Hand,
                Background = active ? _palette.Accent : new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)),
                Child = new TextBlock
                {
                    Text = _config.Tabs[i].Name,
                    FontSize = 12.5,
                    FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = active ? _palette.AccentForeground : _palette.Secondary,
                },
                ContextMenu = Menus.Dynamic(menu => FillTabMenu(menu, index)),
            };
            pill.MouseLeftButtonDown += (_, e) => e.Handled = true;
            pill.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
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
        if ((e.OriginalSource as FrameworkElement)?.DataContext is TileItem item && !item.Missing)
            TileItem.Launch(item.Path);
    }

    private TileItem? Selected => Items.SelectedItem as TileItem;

    private void FillItemMenu(ContextMenu menu)
    {
        if (Selected is not { } item) return;
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
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Choice("Görünüm", _config.View,
            [(ItemView.Icons, "Simgeler"), (ItemView.List, "Liste")],
            v => Change(() => _config.View = v)));
        menu.Items.Add(Menus.Choice("Simge boyutu", _config.IconSize,
            [(IconSize.Small, "Küçük"), (IconSize.Medium, "Orta"), (IconSize.Large, "Büyük")],
            v => Change(() => _config.IconSize = v)));
    }

    public void Detach() { }
}
