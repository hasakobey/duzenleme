using System.Windows;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>Widget pencerelerini açar, kapatır ve ayarlarla eşitler.</summary>
public sealed class WidgetManager
{
    private readonly Dictionary<string, WidgetWindow> _open = [];
    private readonly HashSet<string> _closingOnPurpose = [];
    private bool _shuttingDown;

    public event Action? Changed;

    public IReadOnlyList<WidgetConfig> Configs => AppHost.Settings.Widgets;

    public void RestoreAll()
    {
        foreach (var config in AppHost.Settings.Widgets.ToList()) Open(config);
    }

    public void Add(WidgetKind kind, string? folderName = null)
    {
        var config = new WidgetConfig
        {
            Kind = kind,
            FolderName = folderName,
            ShowSeconds = kind == WidgetKind.Clock ? false : default,
        };
        AppHost.Settings.Widgets.Add(config);
        AppHost.SaveSettings();
        Open(config);
        Changed?.Invoke();
    }

    public void Remove(string id)
    {
        if (_open.Remove(id, out var window))
        {
            _closingOnPurpose.Add(id);
            window.Close();
        }
        AppHost.Settings.Widgets.RemoveAll(w => w.Id == id);
        AppHost.SaveSettings();
        Changed?.Invoke();
    }

    public void Restyle(string id)
    {
        if (_open.TryGetValue(id, out var window)) window.ApplyStyle();
    }

    public void CloseAll()
    {
        _shuttingDown = true;
        foreach (var window in _open.Values.ToList()) window.Close();
        _open.Clear();
    }

    /// <summary>Yeni widget'ları sağ üstten başlayarak üst üste binmeyecek şekilde yerleştirir.</summary>
    public Point DefaultPosition(WidgetKind kind, double width)
    {
        var area = SystemParameters.WorkArea;
        var sameKind = AppHost.Settings.Widgets.Count(w => w.Kind == kind) - 1;
        var offset = Math.Max(0, sameKind) * 28;
        return kind switch
        {
            WidgetKind.Clock => new Point(area.Right - Math.Max(width, 320) - 24 - offset, area.Top + 24 + offset),
            WidgetKind.Date => new Point(area.Right - Math.Max(width, 320) - 24 - offset, area.Top + 210 + offset),
            _ => new Point(area.Left + area.Width / 2 - 200 + offset, area.Top + 90 + offset),
        };
    }

    private void Open(WidgetConfig config)
    {
        IWidgetView view = config.Kind switch
        {
            WidgetKind.Clock => new ClockView(config),
            WidgetKind.Date => new DateView(),
            _ => new FenceView(config),
        };
        var window = new WidgetWindow(config, view);
        window.Closed += (_, _) => OnWindowClosed(config);
        _open[config.Id] = window;
        window.Show();
    }

    private void OnWindowClosed(WidgetConfig config)
    {
        if (_closingOnPurpose.Remove(config.Id) || _shuttingDown) return;
        _open.Remove(config.Id);

        // Explorer yeniden başlarsa masaüstüne bağlı pencereler de kapanır: kısa süre sonra geri aç.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!_shuttingDown && AppHost.Settings.Widgets.Contains(config) && !_open.ContainsKey(config.Id)) Open(config);
        };
        timer.Start();
    }
}
