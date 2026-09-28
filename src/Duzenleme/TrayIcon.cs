using System.IO;
using Path = System.IO.Path;
using System.Windows;
using System.Windows.Threading;
using Duzenleme.Core;
using Forms = System.Windows.Forms;

namespace Duzenleme;

/// <summary>Sistem tepsisi simgesi, menüsü ve taşıma bildirimleri.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _autoMoveItem;
    private readonly Forms.ToolStripMenuItem _hideItem;
    private Action? _balloonAction;
    private long _balloonShownAt;
    private readonly List<MoveEntry> _pendingNotices = [];
    private readonly DispatcherTimer _noticeTimer;

    public TrayIcon(Action openMainWindow, Action quickAdd, Action exit)
    {
        _icon = new Forms.NotifyIcon { Text = AppInfo.Name };
        RefreshIcon();
        _icon.Visible = true;
        // Görev çubuğunun ekranının ölçeği değişince (ya da görev çubuğu başka ekrana geçince) simge o boyutta yeniden
        // seçilir: Windows yanlış boyuttaki simgeyi ölçekleyip bulanıklaştırır.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnPreferenceChanged;

        // İşaretli = otomatik taşıma açık (Paused'ın tersi).
        _autoMoveItem = new Forms.ToolStripMenuItem("Otomatik taşıma", null, (_, _) => AppHost.SetPaused(!AppHost.Settings.Paused));
        var menu = new Forms.ContextMenuStrip();
        // Her ekleme yolu aynı "Widget ekle" penceresini açar (bölme, araç ve "Masaüstümü bölmelere ayır" orada).
        menu.Items.Add(new Forms.ToolStripMenuItem("Widget ekle…", null, (_, _) => quickAdd()) { Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold) });
        menu.Items.Add(new Forms.ToolStripMenuItem($"{AppInfo.Name}'i aç", null, (_, _) => openMainWindow()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_autoMoveItem);
        menu.Items.Add("Masaüstünü şimdi düzenle", null, (_, _) => AppHost.OrganizeNowInBackground());
        menu.Items.Add("Son taşımayı geri al", null, (_, _) => UndoLast());
        _hideItem = new Forms.ToolStripMenuItem("Masaüstünü gizle", null, (_, _) => AppHost.ToggleDesktop());
        menu.Items.Add(_hideItem);
        _peekItem = new Forms.ToolStripMenuItem("Windows masaüstüne göz at", null, (_, _) => AppHost.TogglePeek(AppHost.PeekOrigin.Tray));
        menu.Items.Add(_peekItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        _undoRemoveItem = new Forms.ToolStripMenuItem("Son kaldırılan widget'ı geri getir", null, (_, _) => AppHost.Widgets.UndoRemove());
        menu.Items.Add(_undoRemoveItem);
        // Windows masaüstü simgeleri: Ayarlar ve Widget'lar sayfasıyla aynı üç seçenek (Views.DesktopModes).
        _iconModeItem = new Forms.ToolStripMenuItem("Windows masaüstü simgeleri");
        foreach (var mode in Views.DesktopModes.Choices)
            _iconModeItem.DropDownItems.Add(new Forms.ToolStripMenuItem(Views.DesktopModes.Label(mode), null,
                (_, _) => Views.DesktopModes.Set(mode, null, null)) { Tag = mode });
        menu.Items.Add(_iconModeItem);
        menu.Items.Add("Widget'ları öne getir (5 sn)", null, (_, _) => AppHost.Widgets.RevealAll());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => exit());
        menu.Opening += (_, _) =>
        {
            RefreshIcon();
            _autoMoveItem.Checked = !AppHost.Settings.Paused;
            var current = Views.DesktopModes.Current;
            foreach (Forms.ToolStripMenuItem item in _iconModeItem.DropDownItems)
                item.Checked = item.Tag is Views.IconMode mode && mode == current;
            var removed = AppHost.Widgets.LastRemovedName;
            _undoRemoveItem.Visible = removed is not null;
            _undoRemoveItem.Text = $"Geri getir: {removed}";
            _hideItem.Text = AppHost.DesktopHidden ? "Masaüstünü göster" : "Masaüstünü gizle";
            _peekItem.Text = AppHost.Peeking ? $"{AppInfo.Name}'e dön" : "Windows masaüstüne göz at";
            _peekItem.ShortcutKeyDisplayString = AppHost.Settings.Hotkeys.PeekDesktop;
        };
        _icon.ContextMenuStrip = menu;
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) openMainWindow(); };
        _icon.BalloonTipClicked += (_, _) => { var action = _balloonAction ?? openMainWindow; _balloonAction = null; action(); };
        // Yeni bir balon hemen öncekinin yerini alınca eskisinin "kapandı" bildirimi yenisinin eylemini silmesin.
        _icon.BalloonTipClosed += (_, _) => { if (Environment.TickCount64 - _balloonShownAt > 1500) _balloonAction = null; };

        // Toplu düzenlemede tek tek balon yerine tek özet göster.
        _noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _noticeTimer.Tick += (_, _) => FlushNotices();
        AppHost.Organizer.FileMoved += OnFileMoved;
        AppHost.SettingsChanged += UpdateTooltip;
        AppHost.DesktopVisibilityChanged += UpdateTooltip;
        UpdateTooltip();
    }

    private int _iconPixels;

    /// <summary>
    /// Tepsi simgesini görev çubuğunun ekranındaki küçük simge boyutunda (%100'de 16, %125'te 20, %150'de 24 piksel) app.ico'nun
    /// o boyuttaki karesinden kurar; boyut değişmediyse bir şey yapmaz. Görev çubuğuna ileti gönderilmez (yalnızca pencere
    /// sınıfıyla bulunur ve ölçeği sorulur).
    /// </summary>
    public void RefreshIcon()
    {
        var pixels = TrayIconPixels();
        if (pixels == _iconPixels) return;
        _iconPixels = pixels;
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        var old = _icon.Icon;
        _icon.Icon = new System.Drawing.Icon(stream, new System.Drawing.Size(pixels, pixels));
        old?.Dispose();
        DebugLog.Write($"tepsi simgesi {pixels} px");
    }

    private static int TrayIconPixels()
    {
        const int SM_CXSMICON = 49;
        try
        {
            var taskbar = Widgets.NativeMethods.FindWindow("Shell_TrayWnd", null);
            var dpi = taskbar != IntPtr.Zero ? Widgets.NativeMethods.GetDpiForWindow(taskbar) : 0;
            if (dpi == 0) dpi = (uint)Math.Round(96 * Widgets.NativeMethods.SystemPixelsPerDip);
            var pixels = Widgets.NativeMethods.GetSystemMetricsForDpi(SM_CXSMICON, dpi);
            return pixels > 0 ? pixels : Forms.SystemInformation.SmallIconSize.Width;
        }
        catch (EntryPointNotFoundException)
        {
            return Forms.SystemInformation.SmallIconSize.Width;
        }
    }

    private void OnDisplayChanged(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.BeginInvoke(RefreshIcon, DispatcherPriority.Background);

    private void OnPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category is Microsoft.Win32.UserPreferenceCategory.Desktop or Microsoft.Win32.UserPreferenceCategory.Icon
            or Microsoft.Win32.UserPreferenceCategory.General or Microsoft.Win32.UserPreferenceCategory.Window)
            Application.Current?.Dispatcher.BeginInvoke(RefreshIcon, DispatcherPriority.Background);
    }

    private void OnFileMoved(MoveEntry entry)
    {
        if (!AppHost.Settings.ShowNotifications) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _pendingNotices.Add(entry);
            _noticeTimer.Stop();
            _noticeTimer.Start();
        });
    }

    private void FlushNotices()
    {
        _noticeTimer.Stop();
        if (_pendingNotices.Count == 0) return;
        var text = _pendingNotices.Count == 1
            ? $"{_pendingNotices[0].FileName}  →  {_pendingNotices[0].FolderName}"
            : string.Join(", ", _pendingNotices.GroupBy(e => e.FolderName).Select(g => $"{g.Count()} dosya → {g.Key}"));
        var title = _pendingNotices.Count == 1 ? "Dosya taşındı" : $"{_pendingNotices.Count} dosya düzenlendi";
        _pendingNotices.Clear();
        ShowBalloon(3000, title, text, Forms.ToolTipIcon.None, null);
    }

    private Forms.ToolStripMenuItem _iconModeItem = null!;
    private Forms.ToolStripMenuItem _peekItem = null!;
    private Forms.ToolStripMenuItem _undoRemoveItem = null!;

    /// <summary>Tüm balonlar buradan geçer: tıklanınca çalışacak eylem yalnızca bu balona aittir.</summary>
    private void ShowBalloon(int milliseconds, string title, string text, Forms.ToolTipIcon icon, Action? onClick)
    {
        _balloonAction = onClick;
        _balloonShownAt = Environment.TickCount64;
        _icon.ShowBalloonTip(milliseconds, title, text, icon);
    }

    /// <summary>Kısa bilgi balonu; <paramref name="onClick"/> verilirse balona tıklanınca çalışır.</summary>
    public void Notify(string title, string text, Action? onClick = null)
    {
        ShowBalloon(5000, title, text, Forms.ToolTipIcon.Info, onClick);
    }

    /// <summary>Yeni klasör için tıklanabilir öneri: balona tıklayınca simge seçici açılır.</summary>
    public void SuggestFolderIcon(string folder)
    {
        var (glyph, _) = Core.FolderIconCatalog.Suggest(Path.GetFileName(folder));
        ShowBalloon(6000, $"“{Path.GetFileName(folder)}” klasörüne simge ver",
            $"Önerilen: {glyph.Label}. Seçmek için tıkla.", Forms.ToolTipIcon.None, () => Icons.FolderIconWindow.ShowFor(folder));
    }

    private void UndoLast()
    {
        var last = AppHost.Journal.LastActive();
        if (last is null)
        {
            ShowBalloon(2000, "Geri alınacak bir şey yok", "Henüz taşınmış bir dosya yok.", Forms.ToolTipIcon.None, null);
            return;
        }
        try
        {
            AppHost.Organizer.Undo(last);
            ShowBalloon(2000, "Geri alındı", $"{last.FileName} masaüstüne döndü.", Forms.ToolTipIcon.None, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowBalloon(3000, "Geri alınamadı", ex.Message, Forms.ToolTipIcon.Warning, null);
        }
    }

    private void UpdateTooltip() =>
        _icon.Text = AppHost.Peeking
            ? $"{AppInfo.Name} — Windows masaüstüne göz atılıyor"
            : $"{AppInfo.Name} — otomatik taşıma {(AppHost.Settings.Paused ? "kapalı" : "açık")}";

    public void Dispose()
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        AppHost.Organizer.FileMoved -= OnFileMoved;
        AppHost.SettingsChanged -= UpdateTooltip;
        AppHost.DesktopVisibilityChanged -= UpdateTooltip;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
