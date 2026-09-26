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

    /// <summary>Widget'lar "çift tıkla gizle" ile gizlenmiş mi?</summary>
    public bool Hidden { get; private set; }

    public void RestoreAll()
    {
        foreach (var config in AppHost.Settings.Widgets.ToList()) Open(config);
    }

    public WidgetConfig Add(WidgetKind kind, string? folderName = null)
    {
        var config = new WidgetConfig { Kind = kind, FolderName = folderName };
        if (kind == WidgetKind.Launcher)
            config.Tabs = [new LauncherTab { Name = "Uygulamalar" }, new LauncherTab { Name = "Dosyalar" }];
        if (kind == WidgetKind.Note)
            config.NoteColor = (NoteColor)(AppHost.Settings.Widgets.Count(w => w.Kind == WidgetKind.Note) % 5);
        return AddConfig(config);
    }

    public void Duplicate(string id)
    {
        if (AppHost.Settings.Widgets.FirstOrDefault(w => w.Id == id) is not { } source) return;
        var copy = source.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Left += 36;
        copy.Top += 36;
        AddConfig(copy);
    }

    private WidgetConfig AddConfig(WidgetConfig config)
    {
        AppHost.Settings.Widgets.Add(config);
        AppHost.SaveSettings();
        if (Hidden) SetHidden(false);
        Open(config);
        Changed?.Invoke();
        return config;
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

    /// <summary>Kayıtlı bir düzeni uygular: mevcut widget'lar kapanır, düzendekiler açılır.</summary>
    public void ApplyLayout(LayoutSnapshot layout)
    {
        foreach (var (id, window) in _open.ToList())
        {
            _closingOnPurpose.Add(id);
            window.Close();
        }
        _open.Clear();
        AppHost.Settings.Widgets = layout.Restore();
        AppHost.SaveSettings();
        if (Hidden) SetHidden(false);
        RestoreAll();
        Changed?.Invoke();
    }

    /// <summary>Yeni notu öne alıp yazmaya hazır hale getirir.</summary>
    public void FocusNote(string id)
    {
        if (!_open.TryGetValue(id, out var window) || window.View is not NoteView note) return;
        window.Dispatcher.BeginInvoke(() =>
        {
            window.Activate();
            note.FocusEditor();
        }, DispatcherPriority.ApplicationIdle);
    }

    public void Restyle(string id)
    {
        if (_open.TryGetValue(id, out var window)) window.ApplyStyle();
    }

    public void SetHidden(bool hidden)
    {
        Hidden = hidden;
        foreach (var window in _open.Values)
        {
            if (hidden) window.Hide();
            else window.Show();
        }
    }

    public void CloseAll()
    {
        _shuttingDown = true;
        foreach (var window in _open.Values.ToList()) window.Close();
        _open.Clear();
    }

    /// <summary>Yeni widget'ları türüne göre ayrı bölgelere, üst üste binmeyecek şekilde yerleştirir.</summary>
    public Point DefaultPosition(WidgetKind kind, double width)
    {
        var area = SystemParameters.WorkArea;
        var sameKind = AppHost.Settings.Widgets.Count(w => w.Kind == kind) - 1;
        var offset = Math.Max(0, sameKind) * 32;
        return kind switch
        {
            WidgetKind.Clock => new Point(area.Right - Math.Max(width, 320) - 24 - offset, area.Top + 24 + offset),
            WidgetKind.Date => new Point(area.Right - Math.Max(width, 320) - 24 - offset, area.Top + 210 + offset),
            WidgetKind.Note => new Point(area.Right - 320 - 24 - offset, area.Top + 440 + offset),
            WidgetKind.Launcher => new Point(area.Left + area.Width / 2 - 220 + offset, area.Bottom - 330 - offset),
            _ => new Point(area.Left + area.Width / 2 - 200 + offset, area.Top + 90 + offset),
        };
    }

    private void Open(WidgetConfig config)
    {
        IWidgetView view = config.Kind switch
        {
            WidgetKind.Clock => new ClockView(config),
            WidgetKind.Date => new DateView(),
            WidgetKind.Note => new NoteView(config),
            WidgetKind.Launcher => new LauncherView(config),
            _ => new FenceView(config),
        };
        var window = new WidgetWindow(config, view);
        window.Closed += (_, _) => OnWindowClosed(config);
        _open[config.Id] = window;
        if (!Hidden) window.Show();
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
