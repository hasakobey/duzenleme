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
        ApplyZOrder();
    }

    /// <summary>
    /// Widget'ı diğer widget'ların önüne alır; yine masaüstü katmanında (açık pencerelerin arkasında) kalır.
    /// Tıklanan, taşınan ya da yeni eklenen widget başka bir widget'ın altında kaybolmasın.
    /// </summary>
    public void BringToFront(WidgetWindow window)
    {
        var top = _open.Values.Where(w => w != window).Select(w => w.Config.Z).DefaultIfEmpty(0).Max();
        if (window.Config.Z <= top)
        {
            window.Config.Z = Math.Max(top + 1, DateTime.UtcNow.Ticks);
            AppHost.SaveSettings();
        }
        ApplyZOrder();
    }

    /// <summary>
    /// Widget'ları kayıtlı sıraya dizer. Hepsi en alta itildiği için sırayla (en öndeki önce) en alta gönderilir:
    /// en son gönderilen en altta kalır.
    /// </summary>
    private void ApplyZOrder()
    {
        foreach (var window in _open.Values.OrderByDescending(w => w.Config.Z).ToList()) window.SendToBottom();
    }

    public WidgetConfig Add(WidgetKind kind, string? folderName = null)
    {
        var config = new WidgetConfig { Kind = kind, FolderName = folderName, Z = DateTime.UtcNow.Ticks };
        if (kind == WidgetKind.Launcher)
            config.Tabs = [new LauncherTab { Name = "Uygulamalar" }, new LauncherTab { Name = "Dosyalar" }];
        if (kind == WidgetKind.Note)
            config.NoteColor = NextNoteColor();
        return AddConfig(config);
    }

    /// <summary>Onay kutulu liste (Yapılacaklar): Kind yine Note'tur, yeni enum üyesi yoktur (eski sürümler düz not görür).</summary>
    public WidgetConfig AddChecklist() =>
        AddConfig(new WidgetConfig { Kind = WidgetKind.Note, NoteChecklist = true, NoteColor = NextNoteColor(), Z = DateTime.UtcNow.Ticks });

    /// <summary>Yeni notun kağıt rengi: notlar sırayla sarı, pembe, yeşil, mavi, mor olur.</summary>
    private static NoteColor NextNoteColor() => (NoteColor)(AppHost.Settings.Widgets.Count(w => w.Kind == WidgetKind.Note) % 5);

    /// <summary>
    /// Klasör bölmesi seçenekleri: kural klasörleri (PDF, Resimler…; masaüstünde yoksa eklenirken açılır) ve
    /// kullanıcının masaüstündeki diğer klasörleri.
    /// </summary>
    public List<(string Name, bool Exists)> FolderFenceChoices()
    {
        var existing = AppHost.Organizer.ExistingFolders().ToList();
        var names = new List<string>();
        foreach (var rule in AppHost.Settings.Rules.Where(r => r.Enabled))
            if (!names.Any(n => FolderName.Equal(n, rule.TargetFolder)))
                names.Add(existing.FirstOrDefault(f => FolderName.Equal(f, rule.TargetFolder)) ?? rule.TargetFolder);
        foreach (var folder in existing.OrderBy(f => f, L.Sorter))
            if (!names.Any(n => FolderName.Equal(n, folder))) names.Add(folder);
        return names.Select(n => (n, existing.Any(f => FolderName.Equal(f, n)))).ToList();
    }

    /// <summary>
    /// Bir klasörün içini gösteren bölme ekler; klasör masaüstünde yoksa açar (uygun dosyalar oraya taşınır).
    /// Klasör açılamazsa hata gösterir ve null döner.
    /// </summary>
    public WidgetConfig? AddFolderFence(string name)
    {
        if (!AppHost.Organizer.ExistingFolders().Any(f => FolderName.Equal(f, name)))
        {
            var path = System.IO.Path.Combine(AppHost.DesktopDirectory, name);
            // Klasörü biz açıyoruz: "simge ver" balonu çıkmasın; otomatik taşıma kapalıysa dosya da taşınmasın.
            AppHost.MarkQuietFolder(path);
            try { System.IO.Directory.CreateDirectory(path); }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                AppHost.ConsumeQuietFolder(path);
                MessageBox.Show(ex.Message, AppInfo.Name);
                return null;
            }
            AppHost.OrganizeIfActive();
        }
        return Add(WidgetKind.Fence, name);
    }

    /// <summary>Masaüstündeki öğeleri türüne göre gösteren bölme (Klasörler, Kısayollar, Dosyalar, Tümü).</summary>
    public WidgetConfig AddFence(DesktopFilter filter) =>
        AddConfig(new WidgetConfig { Kind = WidgetKind.Fence, Filter = filter, Sort = FenceSort.Name, Z = DateTime.UtcNow.Ticks });

    /// <summary>
    /// "Masaüstümü bölmelere ayır": Klasörler, Kısayollar, Dosyalar ve masaüstünde var olan kural klasörleri
    /// (PDF, Resimler…) için birer bölme açar; zaten olanları tekrar eklemez (bkz. <see cref="StarterFences.Plan"/>).
    /// near verilirse bölmeler o noktanın monitörüne yerleşir. Eklenen bölme sayısını döner.
    /// </summary>
    internal int AddStarterFences(NativeMethods.POINT? near = null)
    {
        var plan = StarterFences.Plan(AppHost.Settings.Widgets, AppHost.Settings.Rules, AppHost.Organizer.ExistingFolders());
        foreach (var fence in plan)
        {
            if (near is { } point) PlacementHint = point;
            if (fence.Folder is { } folder) Add(WidgetKind.Fence, folder);
            else AddFence(fence.Filter);
        }
        return plan.Count;
    }

    public void Duplicate(string id)
    {
        if (AppHost.Settings.Widgets.FirstOrDefault(w => w.Id == id) is not { } source) return;
        var copy = source.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        // Yeni kopya boş bir yere yerleşsin (kaydedilmiş konumu yok).
        copy.Left = copy.Top = double.NaN;
        copy.PixelLeft = copy.PixelTop = null;
        copy.Z = DateTime.UtcNow.Ticks;
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
            MessageBox.Show($"Widget açılamadı. {AppInfo.Name}'ten çıkıp yeniden açtıktan sonra tekrar dene.", AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Changed?.Invoke();
        return config;
    }

    /// <summary>Widget'ı kaldırır. "Simgeler yalnızca bölmelerde" modu bu yüzden kapandıysa true.</summary>
    public bool Remove(string id, bool notify = true)
    {
        if (_open.Remove(id, out var window))
        {
            _closingOnPurpose.Add(id);
            window.Close();
        }
        AppHost.Settings.Widgets.RemoveAll(w => w.Id == id);
        AppHost.SaveSettings();
        var modeOff = AppHost.EnsureNothingInvisible(notify);
        Changed?.Invoke();
        return modeOff;
    }

    private readonly RemovedWidgets _removed = new();

    /// <summary>Son kaldırılan widget'ın adı (tepsi menüsündeki "geri getir" için); yoksa null.</summary>
    public string? LastRemovedName => _removed.Latest is { } last ? WidgetText.DisplayName(last.Copy) : null;

    /// <summary>
    /// Widget'ı kaldırır; tek bir bildirimle (<see cref="UndoRemove(string)"/>) ya da tepsi menüsündeki "geri getir" ile
    /// ayarları ve yeriyle geri gelir (kaldırma yüzünden kapanan "simgeler yalnızca bölmelerde" modu da geri açılır).
    /// notify=false: tepsi balonu çıkmaz (çağıran kendi bildirimini gösterir), geri getirme yine çalışır. Mod bu yüzden
    /// kapandıysa true.
    /// </summary>
    public bool RemoveWithUndo(string id, bool notify = true)
    {
        var index = AppHost.Settings.Widgets.FindIndex(w => w.Id == id);
        if (index < 0) return false;
        if (_open.TryGetValue(id, out var window)) window.FlushState(); // son taşıma/yazılanlar da geri gelsin
        var copy = AppHost.Settings.Widgets[index].Clone();
        var modeOff = Remove(id, notify: false);
        _removed.Add(new RemovedWidget(copy, index, modeOff));
        if (notify)
            AppHost.Tray?.Notify("Widget kaldırıldı",
                (modeOff ? "Masaüstü simgeleri yeniden gösteriliyor. " : "") + "Geri getirmek için buraya ya da tepsi menüsüne tıkla.",
                () => UndoRemove(id));
        return modeOff;
    }

    /// <summary>En son kaldırılan widget'ı geri getirir (tepsi menüsü).</summary>
    public void UndoRemove()
    {
        if (_removed.Latest is { } last) UndoRemove(last.Copy.Id);
    }

    /// <summary>
    /// Bu widget'ı geri getirir (bildirimdeki "Geri al"). Arada başka widget kaldırılmış olsa da yalnızca bu gelir; zaten
    /// geri geldiyse bir şey yapmaz.
    /// </summary>
    public void UndoRemove(string id)
    {
        if (_removed.Take(id) is not { } last) return;
        if (AppHost.Settings.Widgets.Any(w => w.Id == last.Copy.Id)) return;
        AppHost.Settings.Widgets.Insert(Math.Min(last.Index, AppHost.Settings.Widgets.Count), last.Copy);
        AppHost.SaveSettings();
        if (TryOpen(last.Copy) is not null) ApplyZOrder();
        if (last.ModeTurnedOff && !AppHost.Settings.FencesReplaceIcons) AppHost.SetFencesManageDesktop(true);
        Changed?.Invoke();
    }

    /// <summary>Masaüstündeki her öğe türü (klasör, kısayol, dosya) en az bir bölmede görünüyor mu?</summary>
    public bool CoversDesktop()
    {
        var filters = AppHost.Settings.Widgets.Where(w => w.Kind == WidgetKind.Fence).Select(w => w.Filter).ToHashSet();
        return filters.Contains(DesktopFilter.All) ||
               (filters.Contains(DesktopFilter.Folders) && filters.Contains(DesktopFilter.Shortcuts) && filters.Contains(DesktopFilter.Files));
    }

    /// <summary>
    /// Eksik Klasörler/Kısayollar/Dosyalar bölmelerini ekler (masaüstünü bölmeler yönetecekse). near verilirse bölmeler o
    /// noktanın monitörüne yerleşir.
    /// </summary>
    internal void EnsureDesktopCoverage(NativeMethods.POINT? near = null)
    {
        if (CoversDesktop()) return;
        var filters = AppHost.Settings.Widgets.Where(w => w.Kind == WidgetKind.Fence).Select(w => w.Filter).ToHashSet();
        foreach (var filter in new[] { DesktopFilter.Folders, DesktopFilter.Shortcuts, DesktopFilter.Files })
        {
            if (filters.Contains(filter)) continue;
            if (near is { } point) PlacementHint = point;
            AddFence(filter);
        }
    }

    /// <summary>Düzen uygulanmadan önce alınan otomatik yedeğin adı (Kayıtlı düzenler'de durur; arayüz dilinde).</summary>
    public static string ApplyBackupName => LayoutBackup.ApplyName;

    /// <summary>
    /// Kayıtlı bir düzeni uygular: mevcut widget'lar kapanır, düzendekiler açılır. Önceki yerleşim önce
    /// "Düzen uygulanmadan önce" adıyla kaydedilir (yedeğin kendisi uygulanıyorsa yedek alınmaz).
    /// </summary>
    public void ApplyLayout(LayoutSnapshot layout)
    {
        if (!LayoutBackup.IsApply(layout.Name)) LayoutBackup.SaveBeforeApply(AppHost.Settings.Layouts, AppHost.Settings.Widgets);
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
        AppHost.EnsureNothingInvisible();
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

    public static string ArrangeBackupName => LayoutBackup.ArrangeName;

    /// <summary>
    /// Tüm widget'ları bulundukları monitörde çakışmadan dizer: sağ kenardan başlayan sütunlar (masaüstü simgeleri
    /// genelde soldadır), geniş olanlar önce. Önceki düzen "Otomatik yerleştirmeden önce" adıyla kaydedilir.
    /// </summary>
    public int ArrangeAll()
    {
        if (Hidden) SetHidden(false);
        var placed = _open.Values.Select(w => (Window: w, Bounds: w.PixelBounds)).Where(t => t.Bounds is not null).ToList();
        if (placed.Count == 0) return 0;

        LayoutBackup.SaveBeforeArrange(AppHost.Settings.Layouts, AppHost.Settings.Widgets);

        var byMonitor = placed.GroupBy(t =>
        {
            var r = t.Bounds!.Value;
            return NativeMethods.WorkAreaAt(new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 });
        });
        foreach (var group in byMonitor)
        {
            var area = group.Key;
            var right = area.Right;
            var columnWidth = 0;
            var y = area.Top;
            var first = true;
            foreach (var (window, bounds) in group.OrderByDescending(t => t.Bounds!.Value.Width).ThenBy(t => t.Bounds!.Value.Top))
            {
                var r = bounds!.Value;
                if (first || (y + r.Height > area.Bottom && y > area.Top))
                {
                    // Yeni sütun (ilk ya da önceki doldu): bir öncekinin soluna.
                    if (!first) right -= columnWidth;
                    columnWidth = r.Width;
                    y = area.Top;
                    first = false;
                }
                var x = Math.Max(area.Left, right - r.Width); // sütunda sağa yaslı
                window.MoveTo(x, y);
                y += r.Height;
            }
        }
        AppHost.SaveSettings();
        Changed?.Invoke();
        return placed.Count;
    }

    public void Restyle(string id)
    {
        if (_open.TryGetValue(id, out var window)) window.ApplyStyle();
    }

    /// <summary>
    /// Arka planı (Cam/Koyu/Açık) ve/veya vurgu rengini bütün widget'lara uygular; null olan değişmez. Notlar atlanır:
    /// kendi kağıt rengini kullanırlar.
    /// </summary>
    public void SetLookForAll(WidgetStyle? style, WidgetAccent? accent)
    {
        var targets = AppHost.Settings.Widgets.Where(w => w.Kind != WidgetKind.Note).ToList();
        foreach (var config in targets)
        {
            if (style is { } s) config.Style = s;
            if (accent is { } a) config.Accent = a;
        }
        AppHost.SaveSettings();
        foreach (var config in targets) Restyle(config.Id);
        Changed?.Invoke();
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
    /// <summary>Bir sonraki yeni widget'ın yerleşeceği ekrandaki nokta (ör. "Widget ekle" penceresinin yeri); bir kez kullanılır.</summary>
    internal NativeMethods.POINT? PlacementHint { get; set; }

    /// <summary>Diğer görünür widget'ların kart dikdörtgenleri (gölge payı hariç, fiziksel piksel).</summary>
    internal List<Box> OtherCards(WidgetWindow self) =>
        _open.Values.Where(w => w != self).Select(w => w.CardBox).OfType<Box>().ToList();

    internal NativeMethods.POINT FreeSpot(WidgetKind kind, Size dipSize, WidgetWindow? self = null)
    {
        NativeMethods.GetCursorPos(out var cursor);
        if (PlacementHint is { } hint)
        {
            cursor = hint;
            PlacementHint = null;
        }
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
        // Boş yer yoksa en az çakışan yer seçilir (bir widget'ın tam üstüne binmesin).
        var best = preferred;
        var bestOverlap = long.MaxValue;
        foreach (var x in columns)
            for (var y = area.Top + gap; y + h <= area.Bottom; y += stepY)
            {
                if (Free(x, y)) return new NativeMethods.POINT { X = x, Y = y };
                var overlap = taken.Sum(t =>
                    (long)Math.Max(0, Math.Min(x + w, t.Right) - Math.Max(x, t.Left)) *
                    Math.Max(0, Math.Min(y + h, t.Bottom) - Math.Max(y, t.Top)));
                if (overlap < bestOverlap)
                {
                    bestOverlap = overlap;
                    best = new NativeMethods.POINT { X = x, Y = y };
                }
            }
        return best;
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
            WidgetKind.Date => new DateView(config),
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
