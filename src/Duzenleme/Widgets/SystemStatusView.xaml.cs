using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Desktop;

namespace Duzenleme.Widgets;

/// <summary>
/// Sistem durumu (<see cref="WidgetVariants.System"/>; temel türü Clock): işlemci, bellek, disk boş alanı, pil. Yalnızca
/// bilgi verir ("temizle/hızlandır" gibi işi yoktur); değerler saklanmaz, gönderilmez. Görünürken birkaç saniyede bir
/// (ayar: 2/3/5/10 sn) güncellenir, gizliyken hiç uyanmaz. Disk dakikada bir, arka planda okunur. Yazı ve çubuk yalnızca
/// değer değişince yenilenir (katmanlı pencerede her değişiklik yeniden çizim ister).
/// </summary>
public partial class SystemStatusView : UserControl, IWidgetView
{
    private const int DefaultInterval = 3;
    private static readonly TimeSpan DiskEvery = TimeSpan.FromSeconds(60);

    /// <summary>Bir satır: ad, değer, ince çubuk (iki yıldızlı sütun: dolu / boş).</summary>
    private sealed class Row(string key, Grid root, TextBlock label, TextBlock value, Grid bar, ColumnDefinition used, ColumnDefinition free, Border fill, Border track)
    {
        public string Key { get; } = key;
        public Grid Root { get; } = root;
        public TextBlock Label { get; } = label;
        public TextBlock Value { get; } = value;
        public Grid Bar { get; } = bar;
        public ColumnDefinition Used { get; } = used;
        public ColumnDefinition Free { get; } = free;
        public Border Fill { get; } = fill;
        public Border Track { get; } = track;
        public double Fraction { get; set; } = -1;
    }

    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, Row> _rows = [];
    private CpuTimes? _lastCpu;
    private DiskInfo? _disk;
    private DateTime _diskAt = DateTime.MinValue;
    private bool _diskPending, _live;
    private static List<string>? _drives;

    public SystemStatusView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        foreach (var (key, label, hasBar) in RowDefs) AddRow(key, label, hasBar);
        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += (_, _) => Sample();
        ApplyParts();
        Sample();
    }

    private static (string Key, string Label, bool Bar)[] RowDefs =>
    [
        ("cpu", L.T("İşlemci"), true), ("memory", L.T("Bellek"), true), ("disk", L.T("Disk"), true),
        ("battery", L.T("Pil"), true), ("uptime", L.T("Açık kalma süresi"), false),
    ];

    public bool Resizable => false;

    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;

    private int IntervalSeconds => _config.StatusIntervalSeconds is { } s && s >= 1 ? Math.Min(s, 60) : DefaultInterval;

    public void SetLive(bool live)
    {
        if (_live == live) return;
        _live = live;
        if (!live)
        {
            _timer.Stop();
            return;
        }
        _timer.Interval = TimeSpan.FromSeconds(IntervalSeconds);
        _timer.Start();
        Sample();
    }

    private void AddRow(string key, string label, bool hasBar)
    {
        var name = new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Bottom };
        var value = new TextBlock { FontSize = 13, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        System.Windows.Documents.Typography.SetNumeralAlignment(value, FontNumeralAlignment.Tabular);
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(value, 1);
        head.Children.Add(name);
        head.Children.Add(value);

        var used = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
        var free = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        var track = new Border { CornerRadius = new CornerRadius(2) };
        var fill = new Border { CornerRadius = new CornerRadius(2) };
        var bar = new Grid { Height = 4, Margin = new Thickness(0, 5, 0, 0), Visibility = hasBar ? Visibility.Visible : Visibility.Collapsed };
        bar.ColumnDefinitions.Add(used);
        bar.ColumnDefinitions.Add(free);
        Grid.SetColumnSpan(track, 2);
        bar.Children.Add(track);
        bar.Children.Add(fill);

        var root = new Grid { Margin = new Thickness(0, 0, 0, 11) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(bar, 1);
        root.Children.Add(head);
        root.Children.Add(bar);
        _rows[key] = new Row(key, root, name, value, bar, used, free, fill, track);
        Rows.Children.Add(root);
    }

    /// <summary>Ölçer ve yalnızca değişen yazı/çubuğu yeniler.</summary>
    private void Sample()
    {
        if (_config.Shows("cpu") && SystemStats.Cpu() is { } cpu)
        {
            if (_lastCpu is { } previous && CpuUsage.Percent(previous, cpu) is { } percent)
                Set("cpu", L.Percent(Math.Round(percent) / 100), percent / 100);
            else if (_lastCpu is null) Set("cpu", "…", 0);
            _lastCpu = cpu;
        }
        if (_config.Shows("memory") && SystemStats.Memory() is { } memory)
            Set("memory", L.F("{0} / {1} · {2}", MeasureText.Bytes((long)memory.Used), MeasureText.Bytes((long)memory.Total),
                L.Percent(Math.Round(memory.Fraction * 100) / 100)), memory.Fraction);

        var battery = SystemStats.Battery();
        _rows["battery"].Root.Visibility = battery is not null && _config.Shows("battery") ? Visibility.Visible : Visibility.Collapsed;
        if (battery is { } b && _config.Shows("battery"))
        {
            var level = b.Percent is { } p ? L.Percent(p / 100.0) : "?";
            Set("battery", b.Charging ? L.F("{0} · şarj oluyor", level) : b.PluggedIn ? L.F("{0} · takılı", level) : level, (b.Percent ?? 0) / 100.0);
        }

        if (_config.Shows("uptime")) Set("uptime", MeasureText.Uptime(SystemStats.Uptime), 0);

        if (_config.Shows("disk"))
        {
            if (_disk is { } disk)
            {
                _rows["disk"].Label.Text = L.F("Disk ({0})", disk.Root.TrimEnd('\\'));
                Set("disk", L.F("{0} boş", MeasureText.Bytes((long)disk.Free)), disk.UsedFraction);
            }
            if (DateTime.UtcNow - _diskAt >= DiskEvery) RefreshDisk();
        }
        UpdateName();
    }

    private string DriveRoot => _config.StatusDrive is { Length: > 0 } drive ? drive : SystemStats.SystemDrive;

    /// <summary>Disk boş alanı arka planda (uyuyan disk uyanırken arayüz beklemesin).</summary>
    private async void RefreshDisk()
    {
        if (_diskPending) return;
        _diskPending = true;
        _diskAt = DateTime.UtcNow;
        var root = DriveRoot;
        try
        {
            var disk = await Task.Run(() => SystemStats.Disk(root));
            _drives ??= await Task.Run(SystemStats.FixedDrives);
            if (disk is { } info && root == DriveRoot)
            {
                _disk = info;
                _rows["disk"].Label.Text = L.F("Disk ({0})", info.Root.TrimEnd('\\'));
                Set("disk", L.F("{0} boş", MeasureText.Bytes((long)info.Free)), info.UsedFraction);
            }
        }
        catch (Exception ex) { DebugLog.Write($"sistem durumu: disk okunamadı {root}: {ex.Message}"); }
        finally { _diskPending = false; }
    }

    private void Set(string key, string text, double fraction)
    {
        var row = _rows[key];
        if (row.Value.Text != text) row.Value.Text = text;
        fraction = Math.Round(Math.Clamp(fraction, 0, 1), 3);
        if (Math.Abs(row.Fraction - fraction) < 0.001) return;
        row.Fraction = fraction;
        row.Used.Width = new GridLength(fraction, GridUnitType.Star);
        row.Free.Width = new GridLength(1 - fraction, GridUnitType.Star);
    }

    private void UpdateName() =>
        AutomationProperties.SetName(this, string.Join(", ", _rows.Values.Where(r => r.Root.Visibility == Visibility.Visible)
            .Select(r => $"{r.Label.Text} {r.Value.Text}")));

    private void ApplyParts()
    {
        foreach (var row in _rows.Values)
            row.Root.Visibility = _config.Shows(row.Key) && (row.Key != "battery" || SystemStats.Battery() is not null)
                ? Visibility.Visible : Visibility.Collapsed;
        // Son görünen satırın altında boşluk kalmasın.
        var visible = _rows.Values.Where(r => r.Root.Visibility == Visibility.Visible).ToList();
        foreach (var row in _rows.Values) row.Root.Margin = new Thickness(0, 0, 0, row == visible.LastOrDefault() ? 0 : 11);
        RemoveButton.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        foreach (var row in _rows.Values)
        {
            row.Label.Foreground = palette.Foreground;
            row.Value.Foreground = palette.Secondary;
            row.Fill.Background = palette.Accent;
            row.Track.Background = palette.BorderBrush;
            ClearTypeText.Follow(this, row.Label, row.Value);
        }
        RemoveButton.Foreground = palette.Foreground;
        ApplyParts();
    }

    public void AddMenuItems(WidgetMenu menu)
    {
        menu.Primary.Add(Menus.Item(L.T("Görev Yöneticisi'ni aç"), OpenTaskManager));
        menu.Primary.Add(Menus.Choice(L.T("Güncelleme sıklığı"), IntervalSeconds,
            new[] { 2, 3, 5, 10 }.Select(s => (s, L.F("{0} saniyede bir", s))), value =>
            {
                _config.StatusIntervalSeconds = value == DefaultInterval ? null : value;
                AppHost.SaveSettings();
                if (_live) _timer.Interval = TimeSpan.FromSeconds(IntervalSeconds);
            }));
        var drives = _drives ?? [SystemStats.SystemDrive];
        if (drives.Count > 1)
            menu.Primary.Add(Menus.Choice(L.T("Disk"), DriveRoot, drives.Select(d => (d, d.TrimEnd('\\'))), value =>
            {
                _config.StatusDrive = string.Equals(value, SystemStats.SystemDrive, StringComparison.OrdinalIgnoreCase) ? null : value;
                AppHost.SaveSettings();
                _disk = null;
                _diskAt = DateTime.MinValue;
                RefreshDisk();
            }));
        menu.Appearance.Add(Menus.Parts(_config,
            [("cpu", L.T("İşlemci")), ("memory", L.T("Bellek")), ("disk", L.T("Disk")), ("battery", L.T("Pil")),
             ("uptime", L.T("Açık kalma süresi")), Menus.ClosePart],
            () =>
            {
                ApplyParts();
                _diskAt = DateTime.MinValue;
                Sample();
            }));
    }

    /// <summary>Görev Yöneticisi (kullanıcı istedi; arka planda başlatılır).</summary>
    private static void OpenTaskManager() => Task.Run(() =>
    {
        try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            DebugLog.Write($"Görev Yöneticisi açılamadı: {ex.Message}");
        }
    });

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    public void Detach() => _timer.Stop();
}
