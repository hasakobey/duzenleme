using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Dünya saati (<see cref="WidgetVariants.World"/>; temel türü Clock). Satırlar <see cref="WidgetConfig.Zones"/>'taki
/// Windows saat dilimleridir; yaz saati ve :30/:45 farklı dilimler <see cref="TimeZoneInfo"/>'dan gelir. Görünürken dakikada
/// bir, bütün saat widget'larıyla aynı anda güncellenir (ortak zamanlayıcı); saat dilimi ayarı değişince hemen.
/// </summary>
public partial class WorldClockView : UserControl, IWidgetView
{
    private static CultureInfo Culture => L.Culture;

    private sealed record Row(WorldZone Zone, Grid Root, TextBlock Name, TextBlock Sub, TextBlock Time);

    private readonly WidgetConfig _config;
    private readonly WidgetNameLine _name;
    private readonly List<Row> _rows = [];
    private WidgetPalette _palette = WidgetPalette.Glass;
    private bool _live;

    public WorldClockView(WidgetConfig config)
    {
        _config = config;
        _config.Zones ??= [];
        InitializeComponent();
        _name = new WidgetNameLine(this, config, NameBox, NameText, shadow: null, Update);
        Rebuild();
    }

    public bool Resizable => false;

    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;

    public void SetLive(bool live)
    {
        if (_live == live) return;
        _live = live;
        WidgetTicker.MinuteTick -= Update;
        if (!live) return;
        WidgetTicker.MinuteTick += Update;
        Update();
    }

    private List<WorldZone> Zones => _config.Zones ??= [];

    /// <summary>Satırları baştan kurar (şehir eklendi, kaldırıldı, taşındı, renk değişti).</summary>
    private void Rebuild()
    {
        Rows.Children.Clear();
        _rows.Clear();
        foreach (var zone in Zones)
        {
            var name = new TextBlock { FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 190 };
            var sub = new TextBlock { FontSize = 12, Margin = new Thickness(0, 1, 0, 0) };
            var time = new TextBlock
            {
                FontSize = 26, FontWeight = FontWeights.Light, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0),
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
            };
            TextOptions.SetTextFormattingMode(time, TextFormattingMode.Ideal);
            System.Windows.Documents.Typography.SetNumeralAlignment(time, FontNumeralAlignment.Tabular);
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name, sub } };
            var root = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 0, 0, 8) };
            root.ColumnDefinitions.Add(new ColumnDefinition());
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(time, 1);
            root.Children.Add(labels);
            root.Children.Add(time);
            var row = new Row(zone, root, name, sub, time);
            root.ContextMenu = Menus.Dynamic(menu => FillRowMenu(menu, row));
            _rows.Add(row);
            Rows.Children.Add(root);
        }
        if (_rows.Count > 0) _rows[^1].Root.Margin = new Thickness(0);
        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Paint();
        Update();
    }

    private void Update()
    {
        var now = DateTime.UtcNow;
        var here = TimeZoneInfo.Local;
        var h24 = WorldClock.Uses24Hour(_config.Clock12Hour);
        foreach (var row in _rows)
        {
            var info = WorldClock.Find(row.Zone.Id);
            var name = LabelOf(row.Zone, info);
            row.Name.Text = name;
            if (info is null)
            {
                row.Time.Text = "—";
                row.Sub.Text = L.T("Saat dilimi bulunamadı");
                AutomationProperties.SetName(row.Root, name);
                continue;
            }
            var at = WorldClock.At(now, here, info);
            row.Time.Text = WorldClock.Time(at.Time, h24, Culture) + (h24 ? "" : " " + WorldClock.Designator(at.Time, Culture));
            var parts = new List<string>();
            if (_config.Shows("day") && WorldClock.DayText(at.DayDelta) is { Length: > 0 } day) parts.Add(day);
            if (_config.Shows("offset")) parts.Add(WorldClock.OffsetText(at.OffsetMinutes));
            row.Sub.Text = string.Join(" · ", parts);
            // Satırın boyu gün değişince (yarın → bugün) oynamasın: parça açıksa satır boşken de yer kaplar.
            row.Sub.Visibility = _config.Shows("day") || _config.Shows("offset") ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetName(row.Root, string.Join(", ", new[] { name, row.Time.Text, WorldClock.DayText(at.DayDelta) }.Where(s => s.Length > 0)));
        }
        RemoveButton.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
        _name.Render();
        AutomationProperties.SetName(this, WidgetText.DisplayName(_config));
    }

    /// <summary>Satırın adı; adsız yerel dilim tabloda yoksa "Yerel saat".</summary>
    private static string LabelOf(WorldZone zone, TimeZoneInfo? info) =>
        string.IsNullOrWhiteSpace(zone.Label) && WorldCities.ForZone(zone.Id) is null &&
        string.Equals(zone.Id, TimeZoneInfo.Local.Id, StringComparison.OrdinalIgnoreCase)
            ? L.T("Yerel saat")
            : WorldCities.Label(zone, info);

    private void Paint()
    {
        foreach (var row in _rows)
        {
            row.Name.Foreground = _palette.Foreground;
            row.Time.Foreground = _palette.Foreground;
            row.Sub.Foreground = _palette.Secondary;
            ClearTypeText.Follow(this, row.Name, row.Sub);
        }
        EmptyText.Foreground = _palette.Secondary;
        AddButton.Foreground = _palette.Foreground;
        RemoveButton.Foreground = _palette.Foreground;
        ClearTypeText.Follow(this, EmptyText, AddButton);
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        _name.ApplyPalette(palette);
        Paint();
        Update();
    }

    private void Changed()
    {
        AppHost.SaveSettings();
        Rebuild();
    }

    private void AddCity()
    {
        if (Views.CityPickerDialog.Ask(CountdownView.AnchorOf(this)) is not { } zone) return;
        Zones.Add(zone);
        Changed();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => AddCity();

    private void FillRowMenu(ContextMenu menu, Row row)
    {
        var index = Zones.IndexOf(row.Zone);
        if (index < 0) return;
        menu.Items.Add(Menus.Item(L.T("Adını değiştir…"), () =>
        {
            if (InputDialog.Ask(L.T("Şehrin adı"), L.T("Ad (boş bırakırsan şehrin kendi adı)"), row.Name.Text,
                    (Window.GetWindow(this) as WidgetWindow)?.CenterPoint) is not { } text) return;
            row.Zone.Label = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            Changed();
        }));
        var up = Menus.Item(L.T("Yukarı taşı"), () => Move(index, -1));
        up.IsEnabled = index > 0;
        menu.Items.Add(up);
        var down = Menus.Item(L.T("Aşağı taşı"), () => Move(index, 1));
        down.IsEnabled = index < Zones.Count - 1;
        menu.Items.Add(down);
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item(L.T("Bu şehri kaldır"), () =>
        {
            Zones.RemoveAt(index);
            Changed();
        }));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item(L.T("Şehir ekle…"), AddCity));
    }

    private void Move(int index, int delta)
    {
        var to = index + delta;
        if (index < 0 || to < 0 || to >= Zones.Count) return;
        (Zones[index], Zones[to]) = (Zones[to], Zones[index]);
        Changed();
    }

    public void AddMenuItems(WidgetMenu menu)
    {
        menu.Primary.Add(Menus.Item(L.T("Şehir ekle…"), AddCity));
        menu.Primary.Add(ClockView.HourFormatChoice(_config, Update));
        menu.Primary.Add(_name.MenuItem());
        menu.Appearance.Add(Menus.Parts(_config, [WidgetNameLine.Part, ("day", L.N("Dün / yarın")), ("offset", L.N("Saat farkı")), Menus.ClosePart], Update));
        menu.More.Add(Menus.Hint(L.T("Satıra sağ tık: adını değiştir, taşı, kaldır")));
    }

    /// <summary>F2 ve "Yeniden adlandır": widget'ın adı (şehirlerin adı satır menüsünden değişir).</summary>
    public bool TryBeginRename() => _name.Begin();

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    public void Detach()
    {
        _name.Cancel();
        WidgetTicker.MinuteTick -= Update;
    }
}
