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
    private readonly Forms.ToolStripMenuItem _pauseItem;
    private readonly Forms.ToolStripMenuItem _hideItem;
    private Action? _balloonAction;
    private readonly List<MoveEntry> _pendingNotices = [];
    private readonly DispatcherTimer _noticeTimer;

    public TrayIcon(Action openMainWindow, Action exit)
    {
        var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(iconStream, Forms.SystemInformation.SmallIconSize),
            Text = "Düzenleme",
            Visible = true,
        };

        _pauseItem = new Forms.ToolStripMenuItem("İzlemeyi duraklat", null, (_, _) => AppHost.SetPaused(!AppHost.Settings.Paused));
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(new Forms.ToolStripMenuItem("Düzenleme'yi aç", null, (_, _) => openMainWindow()) { Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold) });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add("Masaüstünü şimdi düzenle", null, (_, _) => AppHost.OrganizeNowInBackground());
        menu.Items.Add("Son taşımayı geri al", null, (_, _) => UndoLast());
        _hideItem = new Forms.ToolStripMenuItem("Masaüstünü gizle", null, (_, _) => AppHost.ToggleDesktop());
        menu.Items.Add(_hideItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        var add = new Forms.ToolStripMenuItem("Widget ekle");
        add.DropDownItems.Add("Saat", null, (_, _) => AppHost.Widgets.Add(WidgetKind.Clock));
        add.DropDownItems.Add("Tarih", null, (_, _) => AppHost.Widgets.Add(WidgetKind.Date));
        add.DropDownItems.Add("Not", null, (_, _) => AppHost.Widgets.FocusNote(AppHost.Widgets.Add(WidgetKind.Note).Id));
        add.DropDownItems.Add("Kısayol kutusu", null, (_, _) => AppHost.Widgets.Add(WidgetKind.Launcher));
        add.DropDownItems.Add("PDF bölmesi", null, (_, _) => AppHost.Widgets.Add(WidgetKind.Fence, "PDF"));
        menu.Items.Add(add);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => exit());
        menu.Opening += (_, _) =>
        {
            _pauseItem.Checked = AppHost.Settings.Paused;
            _hideItem.Text = AppHost.DesktopHidden ? "Masaüstünü göster" : "Masaüstünü gizle";
        };
        _icon.ContextMenuStrip = menu;
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) openMainWindow(); };
        _icon.BalloonTipClicked += (_, _) => { var action = _balloonAction ?? openMainWindow; _balloonAction = null; action(); };
        _icon.BalloonTipClosed += (_, _) => _balloonAction = null;

        // Toplu düzenlemede tek tek balon yerine tek özet göster.
        _noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _noticeTimer.Tick += (_, _) => FlushNotices();
        AppHost.Organizer.FileMoved += OnFileMoved;
        AppHost.SettingsChanged += UpdateTooltip;
        UpdateTooltip();
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
        _balloonAction = null;
        _icon.ShowBalloonTip(3000, title, text, Forms.ToolTipIcon.None);
    }

    /// <summary>Yeni klasör için tıklanabilir öneri: balona tıklayınca simge seçici açılır.</summary>
    public void SuggestFolderIcon(string folder)
    {
        var (glyph, _) = Core.FolderIconCatalog.Suggest(Path.GetFileName(folder));
        _balloonAction = () => Icons.FolderIconWindow.ShowFor(folder);
        _icon.ShowBalloonTip(6000, $"“{Path.GetFileName(folder)}” klasörüne simge ver",
            $"Önerilen: {glyph.Label}. Seçmek için tıkla.", Forms.ToolTipIcon.None);
    }

    private void UndoLast()
    {
        var last = AppHost.Journal.LastActive();
        if (last is null)
        {
            _icon.ShowBalloonTip(2000, "Geri alınacak bir şey yok", "Henüz taşınmış bir dosya yok.", Forms.ToolTipIcon.None);
            return;
        }
        try
        {
            AppHost.Organizer.Undo(last);
            _icon.ShowBalloonTip(2000, "Geri alındı", $"{last.FileName} masaüstüne döndü.", Forms.ToolTipIcon.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _icon.ShowBalloonTip(3000, "Geri alınamadı", ex.Message, Forms.ToolTipIcon.Warning);
        }
    }

    private void UpdateTooltip() =>
        _icon.Text = AppHost.Settings.Paused ? "Düzenleme — duraklatıldı" : "Düzenleme — masaüstü izleniyor";

    public void Dispose()
    {
        AppHost.Organizer.FileMoved -= OnFileMoved;
        AppHost.SettingsChanged -= UpdateTooltip;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
