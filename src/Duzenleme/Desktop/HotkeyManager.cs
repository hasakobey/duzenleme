using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Duzenleme.Core;

namespace Duzenleme.Desktop;

/// <summary>Genel klavye kısayollarını (iTop'taki özel kısayollar gibi) kaydeder.</summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    private readonly HwndSource _window;
    private readonly Action<HotkeyAction> _handler;
    private readonly List<int> _registered = [];

    /// <summary>Kaydedilemeyen kısayollar (başka bir uygulama kullanıyor olabilir).</summary>
    public Dictionary<HotkeyAction, string> Failures { get; } = [];

    public HotkeyManager(Action<HotkeyAction> handler)
    {
        _handler = handler;
        // Görünmez, yalnızca mesaj alan pencere.
        _window = new HwndSource(new HwndSourceParameters("DuzenlemeHotkeys") { Width = 0, Height = 0, WindowStyle = 0, ParentWindow = new IntPtr(-3) });
        _window.AddHook(WndProc);
    }

    public static bool TryGetVirtualKey(string key, out uint vk)
    {
        vk = 0;
        if (key.Length == 1 && char.IsDigit(key[0])) key = "D" + key;
        if (!Enum.TryParse<Key>(key, ignoreCase: true, out var k) || k == Key.None) return false;
        vk = (uint)KeyInterop.VirtualKeyFromKey(k);
        return vk != 0;
    }

    public void Apply(HotkeySettings settings)
    {
        foreach (var id in _registered) UnregisterHotKey(_window.Handle, id);
        _registered.Clear();
        Failures.Clear();

        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var text = settings.Get(action);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (!Hotkey.TryParse(text, out var hk) || !TryGetVirtualKey(hk.Key, out var vk))
            {
                Failures[action] = "Geçersiz kısayol";
                continue;
            }
            var id = (int)action + 1;
            if (RegisterHotKey(_window.Handle, id, (uint)hk.Modifiers | MOD_NOREPEAT, vk)) _registered.Add(id);
            else Failures[action] = "Başka bir uygulama kullanıyor";
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var action = (HotkeyAction)((int)wParam - 1);
            if (Enum.IsDefined(action))
            {
                handled = true;
                _handler(action);
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _registered) UnregisterHotKey(_window.Handle, id);
        _registered.Clear();
        _window.Dispose();
    }
}
