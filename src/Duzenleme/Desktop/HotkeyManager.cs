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

    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(int count, IntPtr[]? list);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr hkl);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(uint vk, uint scan, byte[] state, System.Text.StringBuilder buffer, int size, uint flags, IntPtr hkl);

    /// <summary>
    /// Ctrl+Alt birleşimi (= AltGr) yüklü klavye düzenlerinden birinde karakter üretiyorsa o karakteri döner.
    /// Örn. Lehçe'de Ctrl+Alt+O "ó" yazar; genel kısayol onu yutarsa kullanıcı o harfi yazamaz.
    /// </summary>
    private static string? AltGrConflict(HotkeyModifiers mods, uint vk)
    {
        if (!mods.HasFlag(HotkeyModifiers.Ctrl) || !mods.HasFlag(HotkeyModifiers.Alt) || mods.HasFlag(HotkeyModifiers.Win)) return null;
        var count = GetKeyboardLayoutList(0, null);
        if (count <= 0) return null;
        var layouts = new IntPtr[count];
        GetKeyboardLayoutList(count, layouts);

        var state = new byte[256];
        foreach (var key in new[] { 0x11, 0xA2, 0x12, 0xA5 }) state[key] = 0x80; // Ctrl, Sol Ctrl, Alt, Sağ Alt (AltGr)
        if (mods.HasFlag(HotkeyModifiers.Shift)) { state[0x10] = 0x80; state[0xA0] = 0x80; }

        foreach (var hkl in layouts)
        {
            var scan = MapVirtualKeyEx(vk, 0, hkl);
            var buffer = new System.Text.StringBuilder(8);
            // 0x4: çekirdeğin ölü tuş durumunu değiştirme.
            var result = ToUnicodeEx(vk, scan, state, buffer, buffer.Capacity, 0x4, hkl);
            if (result != 0) return result > 0 ? buffer.ToString(0, result) : "?";
        }
        return null;
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
            if (AltGrConflict(hk.Modifiers, vk) is { } character)
            {
                Failures[action] = $"Klavyende AltGr ile \"{character}\" yazılıyor; başka bir kısayol seç (ör. Win+Shift+…)";
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
