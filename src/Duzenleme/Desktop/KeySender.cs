using System.Diagnostics;
using System.Runtime.InteropServices;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Desktop;

/// <summary>
/// Halkanın "tuş kısayolu gönder" öğesi: kullanıcının seçtiği en çok <see cref="RingActions.MaxChords"/> tuş birleşimini
/// SendInput ile, birleşimler arasında <see cref="ChordGap"/> bekleyerek bir kez gönderir (döngü, tekrar, zamanlayıcı yok).
/// Kanca yok: yalnızca hâlâ basılı değiştiricilere GetAsyncKeyState ile, sınırlı süre bakılır. Gönderilen tuşlar günlüğe
/// yazılmaz. Gönderme ve bekleme arayüz iş parçacığında yapılmaz (Send uyur).
/// </summary>
internal static class KeySender
{
    /// <summary>Birleşimler arası bekleme.</summary>
    public static readonly TimeSpan ChordGap = TimeSpan.FromMilliseconds(20);

    /// <summary>Basılı tuşların bırakılmasını bekleme sınırı (çağıranın verdiği süre buna kısılır).</summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Alt ya da Win bırakılırken araya giren "maske" tuşu (atanmamış vkE8): tek başına basılıp bırakılan Alt hedef
    /// uygulamanın menü çubuğunu, Win Başlat menüsünü açmasın.
    /// </summary>
    public const ushort MaskVk = 0xE8;

    public const ushort VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    public const ushort VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint MAPVK_VK_TO_VSC_EX = 4;

    /// <summary>Basılı olabilecek değiştiriciler (taraf ayrımıyla), bırakma sırası.</summary>
    public static IReadOnlyList<ushort> SidedModifiers => [VK_LCONTROL, VK_RCONTROL, VK_LMENU, VK_RMENU, VK_LSHIFT, VK_RSHIFT, VK_LWIN, VK_RWIN];

    /// <summary>Tek bir tuş olayı (saf; testlenir).</summary>
    public readonly record struct KeyStroke(ushort Vk, ushort Scan, bool Up, bool Extended);

    // ---------------------------------------------------------------- saf kurucular

    /// <summary>
    /// Gönderilecek olay grupları: varsa önce basılı değiştiricilerin bırakılması (Alt/Win varsa maske tuşuyla), sonra her
    /// birleşim için bir grup (değiştiriciler Ctrl, Alt, Shift, Win sırasıyla basılır, tuş basılıp bırakılır, değiştiriciler
    /// ters sırayla bırakılır). Her grup tek SendInput çağrısıdır. Birleşim sayısı 1–<see cref="RingActions.MaxChords"/>
    /// değilse ya da bir birleşim çözülemiyorsa false.
    /// </summary>
    /// <param name="heldModifiers">Hâlâ basılı (taraflı) değiştiriciler: <see cref="HeldModifiers"/>.</param>
    /// <param name="scanOf">Sanal tuşun tarama kodu (0xE0 öneki genişletilmiş tuş); varsayılan MapVirtualKey.</param>
    /// <param name="vkOf">Tuş adının sanal tuşu; varsayılan <see cref="HotkeyManager.TryGetVirtualKey"/>.</param>
    public static bool TryBuild(IReadOnlyList<string>? chords, out List<KeyStroke[]> groups,
        IReadOnlyCollection<ushort>? heldModifiers = null, Func<ushort, ushort>? scanOf = null, Func<string, ushort?>? vkOf = null)
    {
        groups = [];
        if (!RingActions.ValidKeys(chords)) return false;
        scanOf ??= ScanCode;
        vkOf ??= VirtualKeyOf;

        var parsed = new List<(Hotkey Chord, ushort Vk)>();
        foreach (var text in chords!)
        {
            if (!Hotkey.TryParse(text, out var chord, requireModifier: false) || vkOf(chord.Key) is not { } vk || vk == 0) return false;
            if (IsModifierVk(vk)) return false; // "Ctrl+Shift" gibi yalnızca değiştirici: gönderilmez
            parsed.Add((chord, vk));
        }

        if (heldModifiers is { Count: > 0 })
        {
            var release = new List<KeyStroke>();
            var held = SidedModifiers.Where(v => heldModifiers.Contains(v)).ToList();
            if (held.Any(v => v is VK_LMENU or VK_RMENU or VK_LWIN or VK_RWIN))
            {
                release.Add(Stroke(MaskVk, up: false, scanOf));
                release.Add(Stroke(MaskVk, up: true, scanOf));
            }
            release.AddRange(held.Select(v => Stroke(v, up: true, scanOf)));
            if (held.Count > 0) groups.Add([.. release]);
        }

        foreach (var (chord, vk) in parsed)
        {
            var mods = ModifierVks(chord.Modifiers);
            var group = new List<KeyStroke>();
            group.AddRange(mods.Select(m => Stroke(m, up: false, scanOf)));
            group.Add(Stroke(vk, up: false, scanOf));
            group.Add(Stroke(vk, up: true, scanOf));
            group.AddRange(Enumerable.Reverse(mods).Select(m => Stroke(m, up: true, scanOf)));
            groups.Add([.. group]);
        }
        return true;
    }

    /// <summary>Olayın SendInput yapısı (saf).</summary>
    public static INPUT ToInput(KeyStroke stroke) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = stroke.Vk,
                wScan = stroke.Scan,
                dwFlags = (stroke.Up ? KEYEVENTF_KEYUP : 0) | (stroke.Extended ? KEYEVENTF_EXTENDEDKEY : 0),
            },
        },
    };

    /// <summary>Değiştiricilerin basılma sırası: Ctrl, Alt, Shift, Win (<see cref="Hotkey.ToString"/> ile aynı).</summary>
    public static List<ushort> ModifierVks(HotkeyModifiers modifiers)
    {
        var list = new List<ushort>(4);
        if (modifiers.HasFlag(HotkeyModifiers.Ctrl)) list.Add(VK_CONTROL);
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) list.Add(VK_MENU);
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) list.Add(VK_SHIFT);
        if (modifiers.HasFlag(HotkeyModifiers.Win)) list.Add(VK_LWIN);
        return list;
    }

    /// <summary>
    /// Genişletilmiş tuş mu (KEYEVENTF_EXTENDEDKEY): sağ Ctrl/Alt, Insert/Delete/Home/End/Page, oklar, NumLock, Break,
    /// PrintScreen, sayı tuş takımı bölü, Win, Uygulama ve tarayıcı/ortam tuşları; ya da tarama kodu 0xE0 önekli.
    /// </summary>
    public static bool IsExtended(ushort vk, ushort scan) =>
        (scan & 0xFF00) == 0xE000 || vk is 0x03 or (>= 0x21 and <= 0x28) or 0x2C or 0x2D or 0x2E or 0x6F or 0x90
            or VK_LWIN or VK_RWIN or 0x5D or VK_RCONTROL or VK_RMENU or (>= 0xA6 and <= 0xB7);

    private static bool IsModifierVk(ushort vk) =>
        vk is VK_SHIFT or VK_CONTROL or VK_MENU or VK_LWIN or VK_RWIN or (>= VK_LSHIFT and <= VK_RMENU);

    private static KeyStroke Stroke(ushort vk, bool up, Func<ushort, ushort> scanOf)
    {
        var scan = scanOf(vk);
        return new KeyStroke(vk, (ushort)(scan & 0xFF), up, IsExtended(vk, scan));
    }

    private static ushort ScanCode(ushort vk) => (ushort)MapVirtualKeyW(vk, MAPVK_VK_TO_VSC_EX);

    private static ushort? VirtualKeyOf(string key) =>
        HotkeyManager.TryGetVirtualKey(key, out var vk) && vk is > 0 and <= 0xFE ? (ushort)vk : null;

    // ---------------------------------------------------------------- gönderme ve bekleme (arayüz iş parçacığında değil)

    /// <summary>
    /// Birleşimleri ön plandaki pencereye bir kez gönderir. Önce hâlâ basılı değiştiriciler bırakılır. Hepsi gönderildiyse
    /// true. UIPI (yönetici penceresi) engellerse Windows hata bildirmeyebilir: çağıran hedefi önceden denetler
    /// (<see cref="ForegroundInfo.IsElevated"/>).
    /// </summary>
    public static bool Send(IReadOnlyList<string> chords, IReadOnlyCollection<ushort>? heldModifiers = null)
    {
        if (!TryBuild(chords, out var groups, heldModifiers)) return false;
        var size = Marshal.SizeOf<INPUT>();
        for (var i = 0; i < groups.Count; i++)
        {
            if (i > 0) Thread.Sleep(ChordGap);
            var inputs = groups[i].Select(ToInput).ToArray();
            if (SendInput((uint)inputs.Length, inputs, size) != inputs.Length) return false;
        }
        return true;
    }

    /// <summary>Şu an fiziksel olarak basılı (taraflı) değiştiriciler.</summary>
    public static List<ushort> HeldModifiers() => SidedModifiers.Where(vk => NativeMethods.GetAsyncKeyState(vk) < 0).ToList();

    /// <summary>Kısayolun tuşları (genel değiştiriciler, Win'in iki tarafı ve asıl tuş): bırakılmaları beklenir.</summary>
    public static List<int> VirtualKeysOf(Hotkey hotkey)
    {
        var list = ModifierVks(hotkey.Modifiers).Select(v => (int)v).ToList();
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win)) list.Add(VK_RWIN);
        if (VirtualKeyOf(hotkey.Key) is { } vk) list.Add(vk);
        return list;
    }

    /// <summary>
    /// Tuşlar bırakılana dek (en çok <paramref name="timeout"/>, en çok <see cref="MaxWait"/>) 15 ms aralıkla bakar. Hepsi
    /// bırakıldıysa true; süre dolduysa false (çağıran değiştiricileri <see cref="Send"/> ile bıraktırır ya da vazgeçer).
    /// Arka plan iş parçacığı için; arayüzde <see cref="WaitForKeysReleasedAsync"/>.
    /// </summary>
    public static bool WaitForKeysReleased(IReadOnlyCollection<int> vks, TimeSpan timeout)
    {
        var limit = Clamp(timeout);
        var clock = Stopwatch.StartNew();
        while (true)
        {
            if (AllReleased(vks)) return true;
            if (clock.Elapsed >= limit) return false;
            Thread.Sleep(15);
        }
    }

    /// <summary><see cref="WaitForKeysReleased"/>'in arayüzü bekletmeyen hâli.</summary>
    public static async Task<bool> WaitForKeysReleasedAsync(IReadOnlyCollection<int> vks, TimeSpan timeout)
    {
        var limit = Clamp(timeout);
        var clock = Stopwatch.StartNew();
        while (true)
        {
            if (AllReleased(vks)) return true;
            if (clock.Elapsed >= limit) return false;
            await Task.Delay(15);
        }
    }

    private static TimeSpan Clamp(TimeSpan timeout) => timeout < TimeSpan.Zero ? TimeSpan.Zero : timeout > MaxWait ? MaxWait : timeout;

    private static bool AllReleased(IReadOnlyCollection<int> vks) => vks.All(vk => NativeMethods.GetAsyncKeyState(vk) >= 0);

    // ---------------------------------------------------------------- Win32

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyW(uint code, uint mapType);
}
