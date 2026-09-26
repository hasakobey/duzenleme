using System.Windows;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>Widget pencerelerini açar, kapatır ve ayarlarla eşitler.</summary>
public sealed class WidgetManager
{
    private static readonly TimeSpan RevealTime = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, WidgetWindow> _open = [];
    private readonly HashSet<string> _closingOnPurpose = [];
    private readonly DispatcherTimer _shellWatch;
    private bool _shuttingDown;

    public event Action? Changed;

    public IReadOnlyList<WidgetConfig> Configs => AppHost.Settings.Widgets;

    /// <summary>Widget'lar "çift tıkla gizle" ile gizlenmiş mi?</summary>
    public bool Hidden { get; private set; }

    public WidgetManager()
    {
        // Masaüstü nöbetçisi: Explorer yeniden başlarsa ya da (Windows 10 / 11 23H2'de) duvar kağıdı slayt gösterisi
        // masaüstünü WorkerW'ye bölerse widget'lar yeni sahibine bağlanır; aksi halde duvar kağıdının arkasında kalırlar.
        _shellWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _shellWatch.Tick += (_, _) =>
        {
            // Masaüstü sahibini bir kez bul (pencereleri tarar), tüm widget'lar için kullan.
            var owner = Desktop.DesktopIcons.DesktopOwner();
            foreach (var window in _open.Values) window.EnsureAttached(owner);
            AppHost.ReconcileDesktopState();
        };
        _shellWatch.Start();

        // Monitör takılıp çıkarılınca/uykudan uyanınca düzen birkaç saniye oynar; oturunca widget'lar kayıtlı yerlerine döner.
        _displaySettle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _displaySettle.Tick += (_, _) =>
        {
            _displaySettle.Stop();
            foreach (var window in _open.Values) window.RestoreSavedPosition();
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
    }

    private readonly DispatcherTimer _displaySettle;

    private void OnDisplayChanged(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.BeginInvoke(() => { _displaySettle.Stop(); _displaySettle.Start(); });

    public void RestoreAll()
    {
        foreach (var config in AppHost.Settings.Widgets.ToList()) TryOpen(config);
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

    /// <summary>Masaüstündeki öğeleri türüne göre gösteren bölme (Klasörler, Kısayollar, Dosyalar, Tümü).</summary>
    public WidgetConfig AddFence(DesktopFilter filter) =>
        AddConfig(new WidgetConfig { Kind = WidgetKind.Fence, Filter = filter, Sort = FenceSort.Name });

    /// <summary>
    /// "Masaüstümü bölümlere ayır": Klasörler, Kısayollar, Dosyalar ve masaüstünde var olan kural klasörleri
    /// (PDF, Resimler…) için birer bölme açar; zaten olanları tekrar eklemez. Eklenen bölme sayısını döner.
    /// </summary>
    public int AddStarterFences()
    {
        var existing = AppHost.Settings.Widgets.Where(w => w.Kind == WidgetKind.Fence).ToList();
        var added = 0;
        foreach (var filter in new[] { DesktopFilter.Folders, DesktopFilter.Shortcuts, DesktopFilter.Files })
        {
            if (existing.Any(w => w.Filter == filter)) continue;
            AddFence(filter);
            added++;
        }
        var folders = AppHost.Organizer.ExistingFolders().ToList();
        foreach (var rule in AppHost.Settings.Rules.Where(r => r.Enabled))
        {
            if (folders.FirstOrDefault(f => FolderName.Equal(f, rule.TargetFolder)) is not { } folder) continue;
            if (existing.Any(w => w.Filter == DesktopFilter.None && FolderName.Equal(w.FolderName ?? "", folder))) continue;
            Add(WidgetKind.Fence, folder);
            added++;
        }
        return added;
    }

    public void Duplicate(string id)
    {
        if (AppHost.Settings.Widgets.FirstOrDefault(w => w.Id == id) is not { } source) return;
        var copy = source.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        // Yeni kopya boş bir yere yerleşsin (kaydedilmiş konumu yok).
        copy.Left = copy.Top = double.NaN;
        copy.PixelLeft = copy.PixelTop = null;
        AddConfig(copy);
    }

    private WidgetConfig AddConfig(WidgetConfig config)
    {
        AppHost.Settings.Widgets.Add(config);
        AppHost.SaveSettings();
        if (Hidden) SetHidden(false);
        if (TryOpen(config) is { } window)
        {
            // Yeni widget pencerelerin arkasında kalıp "eklenmedi" sanılmasın: birkaç saniye öne gelsin.
            // (Loaded, Show() sırasında tetiklenebildiği için ona bağlanılmaz; yerleşim bitince çalıştırılır.)
            window.Dispatcher.BeginInvoke(() => window.Reveal(RevealTime), DispatcherPriority.ContextIdle);
        }
        else
        {
            // Açılamayan widget listede "eklendi" görünüp masaüstünde hiç çıkmasın.
            AppHost.Settings.Widgets.Remove(config);
            AppHost.SaveSettings();
            MessageBox.Show("Widget açılamadı. Ayrıntı için DUZENLEME_DEBUGLOG ile günlük tutulabilir.", "Düzenleme",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
            window.ActivateForInput();
            note.FocusEditor();
        }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Tüm widget'ları birkaç saniyeliğine pencerelerin önüne getirir.</summary>
    public void RevealAll()
    {
        if (Hidden) SetHidden(false);
        foreach (var window in _open.Values) window.Reveal(RevealTime);
    }

    /// <summary>Tek bir widget'ı öne getirir; ekran dışında kaldıysa görünür bir yere taşır.</summary>
    public void Reveal(string id)
    {
        if (!_open.TryGetValue(id, out var window)) return;
        if (Hidden) SetHidden(false);
        // Ekran dışı kontrolü fiziksel pikselle yapılır: DIP konumları ölçeği farklı monitörlerde kayar.
        if (!window.IsOnScreen()) window.MoveToFreeSpot();
        window.Reveal(RevealTime);
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
        _shellWatch.Stop();
        _displaySettle.Stop();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        foreach (var window in _open.Values.ToList()) window.Close();
        _open.Clear();
    }

    /// <summary>
    /// Yeni widget için imlecin bulunduğu monitörün çalışma alanında boş bir yer bulur (fiziksel piksel):
    /// türüne göre tercih edilen köşeden başlar, mevcut widget'lara çarpmadan aşağı, sonra sola kayar.
    /// Hiç yer yoksa tercih edilen noktayı döner. <paramref name="dipSize"/> widget'ın DIP boyutudur.
    /// </summary>
    internal NativeMethods.POINT FreeSpot(WidgetKind kind, Size dipSize, WidgetWindow? self = null)
    {
        NativeMethods.GetCursorPos(out var cursor);
        var area = NativeMethods.WorkAreaAt(cursor);
        var scale = NativeMethods.ScaleAt(cursor);
        var gap = (int)Math.Round(16 * scale);
        var w = (int)Math.Ceiling(Math.Max(dipSize.Width, 120) * scale);
        var h = (int)Math.Ceiling(Math.Max(dipSize.Height, 80) * scale);
        var taken = _open.Values.Where(x => x != self).Select(x => x.PixelBounds).OfType<NativeMethods.RECT>().ToList();

        bool Free(int x, int y) =>
            x >= area.Left && y >= area.Top && x + w <= area.Right && y + h <= area.Bottom &&
            taken.All(t => x >= t.Right || x + w <= t.Left || y >= t.Bottom || y + h <= t.Top);

        // Saat/tarih/not sağ üstten, bölme ve kutu üst ortadan başlar.
        var rightSide = kind is WidgetKind.Clock or WidgetKind.Date or WidgetKind.Note;
        var startX = rightSide ? area.Right - w - gap : area.Left + (area.Right - area.Left - w) / 2;
        var preferred = new NativeMethods.POINT { X = Math.Max(area.Left, startX), Y = area.Top + gap };

        var stepX = Math.Max(w / 2, (int)(60 * scale));
        var stepY = (int)Math.Round(24 * scale);
        // Tercih edilen sütundan başlayıp sırayla bir sola, bir sağa bakılır; ekranın her yeri taranır.
        var columns = new List<int> { startX };
        for (var d = stepX; startX - d >= area.Left || startX + d + w <= area.Right; d += stepX)
        {
            if (startX - d >= area.Left) columns.Add(startX - d);
            if (startX + d + w <= area.Right) columns.Add(startX + d);
        }
        foreach (var x in columns)
            for (var y = area.Top + gap; y + h <= area.Bottom; y += stepY)
                if (Free(x, y)) return new NativeMethods.POINT { X = x, Y = y };
        return preferred;
    }

    private WidgetWindow? TryOpen(WidgetConfig config)
    {
        // Bir widget'ın açılamaması başlangıcı (izleyici, tepsi, ana pencere) durdurmasın.
        try { return Open(config); }
        catch (Exception ex)
        {
            DebugLog.Write($"widget açılamadı {config.Kind}: {ex}");
            if (_open.Remove(config.Id, out var half))
            {
                _closingOnPurpose.Add(config.Id);
                try { half.Close(); } catch (InvalidOperationException) { }
            }
            return null;
        }
    }

    private WidgetWindow Open(WidgetConfig config)
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
        return window;
    }

    private void OnWindowClosed(WidgetConfig config)
    {
        if (_closingOnPurpose.Remove(config.Id) || _shuttingDown) return;
        _open.Remove(config.Id);

        // Beklenmedik kapanma (ör. Alt+F4): kısa süre sonra geri aç; widget ayarlardan silinmedikçe kaybolmasın.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!_shuttingDown && AppHost.Settings.Widgets.Contains(config) && !_open.ContainsKey(config.Id)) TryOpen(config);
        };
        timer.Start();
    }
}
