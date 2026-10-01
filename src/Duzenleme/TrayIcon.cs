using System.IO;
using System.Runtime.InteropServices;
using Path = System.IO.Path;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme;

/// <summary>
/// Sistem tepsisi simgesi, menüsü ve taşıma bildirimleri; doğrudan Shell_NotifyIcon ile (2.2'ye dek WinForms'un tepsi simgesi
/// vardı, davranış aynı kaldı). Simge, kullanıcının görev çubuğundaki "göster/gizle" seçimi korunsun diye eskisi gibi pencere
/// + uID 1 ile tanınır (GUID yok) ve eski tür bildirimler alır (NIM_SETVERSION yok).
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Action _openMainWindow;
    private readonly Action _quickAdd;
    private readonly Action _exit;
    private readonly HwndSource _window;
    private readonly uint _taskbarCreatedMessage;
    private IntPtr _hIcon;
    private string _tip = "";
    private bool _added;
    private bool _doubleClick;
    private bool _menuOpen;
    private bool _disposed;
    private DispatcherTimer? _retryTimer;
    private int _retries;
    private Action? _balloonAction;
    private long _balloonShownAt;
    private readonly List<MoveEntry> _pendingNotices = [];
    private readonly DispatcherTimer _noticeTimer;

    /// <summary>
    /// Explorer yeniden başladı (görev çubuğu yeniden kuruldu) ve tepsi simgesi yeniden eklendi. Masaüstüne bağlı pencereler
    /// yeni masaüstüne hemen bağlanmak için dinleyebilir.
    /// </summary>
    public event Action? TaskbarCreated;

    public TrayIcon(Action openMainWindow, Action quickAdd, Action exit)
    {
        _openMainWindow = openMainWindow;
        _quickAdd = quickAdd;
        _exit = exit;
        // Görünmez üst düzey pencere: yalnızca ileti alan (HWND_MESSAGE) pencereye "TaskbarCreated" yayını ulaşmaz.
        _window = new HwndSource(new HwndSourceParameters(AppInfo.Name + " Tray")
        {
            Width = 0, Height = 0, ParentWindow = IntPtr.Zero,
            WindowStyle = Native.WS_POPUP, ExtendedWindowStyle = Native.WS_EX_TOOLWINDOW,
        });
        _window.AddHook(WndProc);
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
        // Yönetici olarak çalışırken de Explorer'ın yayını gelsin (gelmezse Explorer yeniden başlayınca simge kaybolur).
        try { Native.ChangeWindowMessageFilterEx(_window.Handle, _taskbarCreatedMessage, Native.MSGFLT_ALLOW, IntPtr.Zero); }
        catch (EntryPointNotFoundException) { }

        _tip = TooltipText();
        RefreshIcon();
        AddIcon();
        // Görev çubuğunun ekranının ölçeği değişince (ya da görev çubuğu başka ekrana geçince) simge o boyutta yeniden
        // seçilir: Windows yanlış boyuttaki simgeyi ölçekleyip bulanıklaştırır.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnPreferenceChanged;

        // Toplu düzenlemede tek tek balon yerine tek özet göster.
        _noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _noticeTimer.Tick += (_, _) => FlushNotices();
        AppHost.Organizer.FileMoved += OnFileMoved;
        AppHost.SettingsChanged += UpdateTooltip;
        AppHost.DesktopVisibilityChanged += UpdateTooltip;
    }

    private int _iconPixels;

    /// <summary>
    /// Tepsi simgesini görev çubuğunun ekranındaki küçük simge boyutunda (%100'de 16, %125'te 20, %150'de 24 piksel) app.ico'nun
    /// o boyuttaki karesinden kurar; boyut değişmediyse bir şey yapmaz. Görev çubuğuna ileti gönderilmez (yalnızca pencere
    /// sınıfıyla bulunur ve ölçeği sorulur).
    /// </summary>
    public void RefreshIcon()
    {
        if (_disposed) return;
        var pixels = TrayIconPixels();
        if (pixels == _iconPixels && _hIcon != IntPtr.Zero) return;
        var handle = CreateIcon(pixels);
        if (handle == IntPtr.Zero)
        {
            DebugLog.Write($"tepsi simgesi kurulamadı: {pixels} px");
            return;
        }
        _iconPixels = pixels;
        var old = _hIcon;
        _hIcon = handle;
        if (_added) Modify();
        if (old != IntPtr.Zero) NativeMethods.DestroyIcon(old);
        DebugLog.Write($"tepsi simgesi {pixels} px");
    }

    /// <summary>app.ico'nun <paramref name="pixels"/> boyutuna en uygun karesinden simge tutamacı (kare kendi boyutunda).</summary>
    private static IntPtr CreateIcon(int pixels)
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))!.Stream;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var ico = memory.GetBuffer().AsSpan(0, (int)memory.Length);
        if (IcoFrames.Pick(ico, pixels) is not { } frame) return IntPtr.Zero;
        var bits = IcoFrames.Bytes(ico, frame);
        return Native.CreateIconFromResourceEx(bits, (uint)bits.Length, true, Native.IconResourceVersion, 0, 0, 0);
    }

    private static int TrayIconPixels()
    {
        const int SM_CXSMICON = 49;
        try
        {
            var taskbar = NativeMethods.FindWindow("Shell_TrayWnd", null);
            var dpi = taskbar != IntPtr.Zero ? NativeMethods.GetDpiForWindow(taskbar) : 0;
            if (dpi == 0) dpi = (uint)Math.Round(96 * NativeMethods.SystemPixelsPerDip);
            var pixels = NativeMethods.GetSystemMetricsForDpi(SM_CXSMICON, dpi);
            return pixels > 0 ? pixels : SmallIconPixels();
        }
        catch (EntryPointNotFoundException)
        {
            return SmallIconPixels();
        }

        static int SmallIconPixels() => Math.Max(16, NativeMethods.GetSystemMetrics(SM_CXSMICON));
    }

    private void OnDisplayChanged(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.BeginInvoke(RefreshIcon, DispatcherPriority.Background);

    private void OnPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category is Microsoft.Win32.UserPreferenceCategory.Desktop or Microsoft.Win32.UserPreferenceCategory.Icon
            or Microsoft.Win32.UserPreferenceCategory.General or Microsoft.Win32.UserPreferenceCategory.Window)
            Application.Current?.Dispatcher.BeginInvoke(RefreshIcon, DispatcherPriority.Background);
    }

    // ---- Shell_NotifyIcon ----

    private Native.NOTIFYICONDATA Data(uint flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<Native.NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = Native.IconId,
        uFlags = flags,
        szTip = "", szInfo = "", szInfoTitle = "",
    };

    /// <summary>Simge, ipucu ve geri çağırma iletisi (eklerken ve her değişiklikte birlikte gönderilir).</summary>
    private Native.NOTIFYICONDATA IconData()
    {
        var data = Data(Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP);
        data.uCallbackMessage = Native.CallbackMessage;
        data.hIcon = _hIcon;
        data.szTip = _tip;
        return data;
    }

    private void AddIcon()
    {
        if (_disposed) return;
        var data = IconData();
        // Explorer meşgulken ekleme zaman aşımına uğrayıp yine de olmuş olabilir: o zaman güncelleme başarılı olur.
        _added = Native.Shell_NotifyIcon(Native.NIM_ADD, ref data) || Native.Shell_NotifyIcon(Native.NIM_MODIFY, ref data);
        if (_added)
        {
            _retryTimer?.Stop();
            _retries = 0;
            return;
        }
        DebugLog.Write("tepsi simgesi eklenemedi; yeniden denenecek");
        // Oturum açılırken görev çubuğu henüz hazır değilse: birkaç kez yeniden dene (görev çubuğu kurulunca TaskbarCreated da gelir).
        if (_retries >= 5) return;
        _retries++;
        _retryTimer ??= NewRetryTimer();
        _retryTimer.Stop();
        _retryTimer.Start();
    }

    private DispatcherTimer NewRetryTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!_added) AddIcon();
        };
        return timer;
    }

    private void Modify()
    {
        var data = IconData();
        Native.Shell_NotifyIcon(Native.NIM_MODIFY, ref data);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.CallbackMessage)
        {
            handled = true;
            OnIconMessage((int)(lParam.ToInt64() & 0xFFFF));
        }
        else if (_taskbarCreatedMessage != 0 && (uint)msg == _taskbarCreatedMessage)
        {
            OnTaskbarCreated();
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Eski tür (sürüm 0) bildirimler: lParam fare iletisidir. Sol tık bırakılınca ana pencere açılır; çift tıkta ikinci
    /// bırakma yok sayılır (pencere bir kez açılır). Sağ tık bırakılınca menü.
    /// </summary>
    private void OnIconMessage(int message)
    {
        switch (message)
        {
            case Native.WM_LBUTTONDBLCLK or Native.WM_RBUTTONDBLCLK or Native.WM_MBUTTONDBLCLK:
                _doubleClick = true;
                break;
            case Native.WM_LBUTTONUP:
                var secondOfDouble = _doubleClick;
                _doubleClick = false;
                if (!secondOfDouble) _openMainWindow();
                break;
            case Native.WM_RBUTTONUP:
                ShowMenu();
                _doubleClick = false;
                break;
            case Native.WM_MBUTTONUP:
                _doubleClick = false;
                break;
            case Native.NIN_BALLOONUSERCLICK:
                var action = _balloonAction ?? _openMainWindow;
                _balloonAction = null;
                action();
                break;
            case Native.NIN_BALLOONHIDE or Native.NIN_BALLOONTIMEOUT:
                // Yeni bir balon hemen öncekinin yerini alınca eskisinin "kapandı" bildirimi yenisinin eylemini silmesin.
                if (Environment.TickCount64 - _balloonShownAt > 1500) _balloonAction = null;
                break;
        }
    }

    private void OnTaskbarCreated()
    {
        if (_disposed) return;
        DebugLog.Write("görev çubuğu yeniden kuruldu: tepsi simgesi yeniden ekleniyor");
        _added = false;
        _retries = 0;
        RefreshIcon();
        AddIcon();
        TaskbarCreated?.Invoke();
    }

    // ---- Menü ----

    private static TrayMenuState MenuState() => new(
        AutoMove: !AppHost.Settings.Paused,
        DesktopHidden: AppHost.DesktopHidden,
        Peeking: AppHost.Peeking,
        PeekHotkey: AppHost.Settings.Hotkeys.PeekDesktop,
        LastRemovedName: AppHost.Widgets.LastRemovedName,
        IconModes: Views.DesktopModes.Choices.Select(Views.DesktopModes.Label).ToList(),
        CurrentIconMode: Array.IndexOf(Views.DesktopModes.Choices, Views.DesktopModes.Current));

    /// <summary>
    /// Windows'un kendi açılır menüsü, her açılışta güncel durumla yeniden kurulur. Seçilen komut menü kapandıktan sonra
    /// (menünün kendi döngüsünün dışında) çalışır.
    /// </summary>
    private void ShowMenu()
    {
        if (_menuOpen || _disposed) return;
        _menuOpen = true;
        var menu = IntPtr.Zero;
        try
        {
            RefreshIcon();
            var commands = new Dictionary<int, TrayMenuItem>();
            menu = BuildMenu(TrayMenuModel.Build(MenuState()), commands);
            NativeMethods.GetCursorPos(out var point);
            // Menü, dışına tıklanınca kapansın diye pencere önce öne alınır; sonra boş ileti gönderilir (Windows'un belgelenmiş gereği).
            Native.SetForegroundWindow(_window.Handle);
            var flags = Native.TPM_RIGHTBUTTON | Native.TPM_RETURNCMD | Native.TPM_NONOTIFY
                        | (NativeMethods.GetSystemMetrics(Native.SM_MENUDROPALIGNMENT) != 0 ? Native.TPM_RIGHTALIGN : Native.TPM_LEFTALIGN);
            var chosen = Native.TrackPopupMenuEx(menu, flags, point.X, point.Y, _window.Handle, IntPtr.Zero);
            NativeMethods.PostMessage(_window.Handle, Native.WM_NULL, IntPtr.Zero, IntPtr.Zero);
            if (chosen != 0 && commands.TryGetValue(chosen, out var item))
                _window.Dispatcher.BeginInvoke(() => Run(item));
        }
        finally
        {
            if (menu != IntPtr.Zero) Native.DestroyMenu(menu);
            _menuOpen = false;
        }
    }

    private static IntPtr BuildMenu(IReadOnlyList<TrayMenuItem> items, Dictionary<int, TrayMenuItem> commands)
    {
        var menu = Native.CreatePopupMenu();
        uint position = 0;
        foreach (var item in items)
        {
            var info = new Native.MENUITEMINFO { cbSize = (uint)Marshal.SizeOf<Native.MENUITEMINFO>() };
            if (item.IsSeparator)
            {
                info.fMask = Native.MIIM_FTYPE;
                info.fType = Native.MFT_SEPARATOR;
            }
            else
            {
                var id = commands.Count + 1;
                commands[id] = item;
                info.fMask = Native.MIIM_FTYPE | Native.MIIM_STRING | Native.MIIM_STATE | Native.MIIM_ID;
                info.fType = item.IsRadio ? Native.MFT_RADIOCHECK : Native.MFT_STRING;
                info.fState = item.IsChecked ? Native.MFS_CHECKED : 0;
                info.wID = (uint)id;
                info.dwTypeData = item.NativeText;
                if (item.Children is { } children)
                {
                    info.fMask |= Native.MIIM_SUBMENU;
                    info.hSubMenu = BuildMenu(children, commands);
                }
            }
            Native.InsertMenuItem(menu, position++, true, ref info);
            if (item.IsDefault) Native.SetMenuDefaultItem(menu, info.wID, 0);
        }
        return menu;
    }

    private void Run(TrayMenuItem item)
    {
        switch (item.Command)
        {
            case TrayCommand.QuickAdd: _quickAdd(); break;
            case TrayCommand.OpenApp: _openMainWindow(); break;
            case TrayCommand.AutoMove: AppHost.SetPaused(!AppHost.Settings.Paused); break;
            case TrayCommand.OrganizeNow: AppHost.OrganizeNowInBackground(); break;
            case TrayCommand.UndoLastMove: UndoLast(); break;
            case TrayCommand.ToggleDesktop: AppHost.ToggleDesktop(); break;
            case TrayCommand.PeekDesktop: AppHost.TogglePeek(AppHost.PeekOrigin.Tray); break;
            case TrayCommand.UndoRemove: AppHost.Widgets.UndoRemove(); break;
            case TrayCommand.IconMode when item.Choice >= 0 && item.Choice < Views.DesktopModes.Choices.Length:
                Views.DesktopModes.Set(Views.DesktopModes.Choices[item.Choice], null, null);
                break;
            case TrayCommand.RevealWidgets: AppHost.Widgets.RevealAll(); break;
            case TrayCommand.Exit: _exit(); break;
        }
    }

    // ---- Bildirimler ----

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
            : string.Join(", ", _pendingNotices.GroupBy(e => e.FolderName).Select(g => L.P(g.Count(), "{0} dosya → {1}", g.Key)));
        var title = _pendingNotices.Count == 1 ? L.T("Dosya taşındı") : L.P(_pendingNotices.Count, "{0} dosya düzenlendi");
        _pendingNotices.Clear();
        ShowBalloon(3000, title, text, Native.NIIF_NONE, null);
    }

    /// <summary>Tüm balonlar buradan geçer: tıklanınca çalışacak eylem yalnızca bu balona aittir.</summary>
    private void ShowBalloon(int milliseconds, string title, string text, uint icon, Action? onClick)
    {
        _balloonAction = onClick;
        _balloonShownAt = Environment.TickCount64;
        if (!_added || _disposed) return;
        var data = Data(Native.NIF_INFO);
        data.uTimeoutOrVersion = (uint)milliseconds;
        data.szInfoTitle = TrayText.Fit(title, TrayText.TitleMax);
        // Boş metin balonu kaldırır: en az bir boşluk.
        data.szInfo = TrayText.Fit(string.IsNullOrEmpty(text) ? " " : text, TrayText.BodyMax);
        data.dwInfoFlags = icon;
        Native.Shell_NotifyIcon(Native.NIM_MODIFY, ref data);
    }

    /// <summary>Kısa bilgi balonu; <paramref name="onClick"/> verilirse balona tıklanınca çalışır.</summary>
    public void Notify(string title, string text, Action? onClick = null)
    {
        ShowBalloon(5000, title, text, Native.NIIF_INFO, onClick);
    }

    /// <summary>Yeni klasör için tıklanabilir öneri: balona tıklayınca simge seçici açılır.</summary>
    public void SuggestFolderIcon(string folder)
    {
        var (glyph, _) = Core.FolderIconCatalog.Suggest(Path.GetFileName(folder));
        // glyph.Label kendisi çevirir (katalogda L.N anahtarı + bağlam): ikinci kez L.Dyn'e verme.
        ShowBalloon(6000, L.F("“{0}” klasörüne simge ver", Path.GetFileName(folder)),
            L.F("Önerilen: {0}. Seçmek için tıkla.", glyph.Label), Native.NIIF_NONE, () => Icons.FolderIconWindow.ShowFor(folder));
    }

    private void UndoLast()
    {
        var last = AppHost.Journal.LastActive();
        if (last is null)
        {
            ShowBalloon(2000, L.T("Geri alınacak bir şey yok"), L.T("Henüz taşınmış bir dosya yok."), Native.NIIF_NONE, null);
            return;
        }
        try
        {
            AppHost.Organizer.Undo(last);
            ShowBalloon(2000, L.T("Geri alındı"), L.F("{0} masaüstüne döndü.", last.FileName), Native.NIIF_NONE, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowBalloon(3000, L.T("Geri alınamadı"), ex.Message, Native.NIIF_WARNING, null);
        }
    }

    private static string TooltipText() => TrayText.Fit(
        AppHost.Peeking ? L.F("{0} — Windows masaüstüne göz atılıyor", AppInfo.Name)
        : AppHost.Settings.Paused ? L.F("{0} — otomatik taşıma kapalı", AppInfo.Name)
        : L.F("{0} — otomatik taşıma açık", AppInfo.Name), TrayText.TipMax);

    private void UpdateTooltip()
    {
        var tip = TooltipText();
        if (tip == _tip) return;
        _tip = tip;
        if (_added && !_disposed) Modify();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        AppHost.Organizer.FileMoved -= OnFileMoved;
        AppHost.SettingsChanged -= UpdateTooltip;
        AppHost.DesktopVisibilityChanged -= UpdateTooltip;
        _noticeTimer.Stop();
        _retryTimer?.Stop();
        if (_added)
        {
            var data = Data(0);
            Native.Shell_NotifyIcon(Native.NIM_DELETE, ref data);
            _added = false;
        }
        _window.RemoveHook(WndProc);
        _window.Dispose();
        if (_hIcon != IntPtr.Zero) NativeMethods.DestroyIcon(_hIcon);
        _hIcon = IntPtr.Zero;
    }

    /// <summary>Tepsi simgesi ve menü için Win32 tanımları (yapı boyutlarını testler denetler).</summary>
    internal static class Native
    {
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const uint IconId = 1;
        /// <summary>Simgenin geri çağırma iletisi (WM_APP aralığında).</summary>
        public const int CallbackMessage = 0x8000 + 0x101;
        public const uint IconResourceVersion = 0x00030000;
        public const uint MSGFLT_ALLOW = 1;

        public const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
        public const uint NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04, NIF_INFO = 0x10;
        public const uint NIIF_NONE = 0, NIIF_INFO = 1, NIIF_WARNING = 2;

        public const int WM_NULL = 0x0000;
        public const int WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203;
        public const int WM_RBUTTONUP = 0x0205, WM_RBUTTONDBLCLK = 0x0206;
        public const int WM_MBUTTONUP = 0x0208, WM_MBUTTONDBLCLK = 0x0209;
        public const int NIN_BALLOONHIDE = 0x0403, NIN_BALLOONTIMEOUT = 0x0404, NIN_BALLOONUSERCLICK = 0x0405;

        public const uint MIIM_STATE = 0x0001, MIIM_ID = 0x0002, MIIM_SUBMENU = 0x0004, MIIM_STRING = 0x0040, MIIM_FTYPE = 0x0100;
        public const uint MFT_STRING = 0x0000, MFT_RADIOCHECK = 0x0200, MFT_SEPARATOR = 0x0800;
        public const uint MFS_CHECKED = 0x0008;
        public const uint TPM_LEFTALIGN = 0x0000, TPM_RIGHTALIGN = 0x0008, TPM_RIGHTBUTTON = 0x0002, TPM_NONOTIFY = 0x0080, TPM_RETURNCMD = 0x0100;
        public const int SM_MENUDROPALIGNMENT = 40;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MENUITEMINFO
        {
            public uint cbSize;
            public uint fMask;
            public uint fType;
            public uint fState;
            public uint wID;
            public IntPtr hSubMenu;
            public IntPtr hbmpChecked;
            public IntPtr hbmpUnchecked;
            public IntPtr dwItemData;
            public string? dwTypeData;
            public uint cch;
            public IntPtr hbmpItem;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);

        [DllImport("user32.dll")]
        public static extern IntPtr CreateIconFromResourceEx(byte[] bits, uint size, [MarshalAs(UnmanagedType.Bool)] bool icon,
            uint version, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "InsertMenuItemW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool InsertMenuItem(IntPtr menu, uint item, [MarshalAs(UnmanagedType.Bool)] bool byPosition, ref MENUITEMINFO info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetMenuDefaultItem(IntPtr menu, uint item, uint byPosition);

        /// <summary>TPM_RETURNCMD ile seçilen öğenin kimliğini döner (vazgeçilirse 0).</summary>
        [DllImport("user32.dll")]
        public static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr parameters);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ChangeWindowMessageFilterEx(IntPtr hwnd, uint message, uint action, IntPtr changeFilterStruct);
    }
}
