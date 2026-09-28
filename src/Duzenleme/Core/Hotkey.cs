namespace Duzenleme.Core;

/// <summary>Değerler Win32 RegisterHotKey MOD_* sabitleriyle aynıdır.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>"Ctrl+Alt+H" biçiminde saklanan genel klavye kısayolu.</summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, string Key)
{
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var mods = HotkeyModifiers.None;
        string? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= HotkeyModifiers.Ctrl; break;
                case "alt": mods |= HotkeyModifiers.Alt; break;
                case "shift": mods |= HotkeyModifiers.Shift; break;
                case "win" or "windows": mods |= HotkeyModifiers.Win; break;
                default:
                    if (key is not null) return false;
                    key = raw.Length == 1 ? raw.ToUpperInvariant() : raw;
                    break;
            }
        }

        // Genel kısayol en az bir değiştirici tuş ister; yoksa normal yazmayı bozar.
        if (key is null || mods == HotkeyModifiers.None) return false;
        hotkey = new Hotkey(mods, key);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }
}

/// <summary>
/// Kısayolun işi. Kalıcı değildir (ayarlarda her iş kendi metin özelliğinde durur); kayıt kimliği (int)eylem + 1 olduğundan
/// yeni üye yalnızca SONA eklenir ve <see cref="HotkeySettings.Get"/>/<see cref="HotkeySettings.Set"/>'e kendi satırı yazılır.
/// </summary>
public enum HotkeyAction { ToggleDesktop, OrganizeNow, OpenApp, NewNote, PeekWidgets, QuickAdd, PeekDesktop }

public sealed class HotkeySettings
{
    public string ToggleDesktop { get; set; } = "Ctrl+Alt+H";
    public string OrganizeNow { get; set; } = "Ctrl+Alt+O";
    public string OpenApp { get; set; } = "Ctrl+Alt+D";
    public string NewNote { get; set; } = "Ctrl+Alt+N";

    /// <summary>Widget'ları pencerelerin önüne getirir (Fences'taki "Peek").</summary>
    public string PeekWidgets { get; set; } = "Ctrl+Alt+W";

    /// <summary>"Widget ekle" penceresini açar.</summary>
    public string QuickAdd { get; set; } = "Ctrl+Alt+B";

    // 2.1 P3
    /// <summary>
    /// Windows masaüstüne göz at: simgeler görünür, widget'lar kısa süre çekilir. G: "Göz at"; Türkçe Q ve ABD düzeninde
    /// AltGr ile karakter yazmaz. Eski ayar dosyasında yoksa bu varsayılan gelir; 2.0 bu özelliği yok sayar.
    /// </summary>
    public string PeekDesktop { get; set; } = "Ctrl+Alt+G";

    public string Get(HotkeyAction action) => action switch
    {
        HotkeyAction.ToggleDesktop => ToggleDesktop,
        HotkeyAction.OrganizeNow => OrganizeNow,
        HotkeyAction.OpenApp => OpenApp,
        HotkeyAction.PeekWidgets => PeekWidgets,
        HotkeyAction.NewNote => NewNote,
        HotkeyAction.QuickAdd => QuickAdd,
        HotkeyAction.PeekDesktop => PeekDesktop,
        // Bilinmeyen eylem başka bir eylemin kısayolunu paylaşmasın (ikinci kayıt "başka uygulama kullanıyor" diye düşerdi).
        _ => "",
    };

    public void Set(HotkeyAction action, string value)
    {
        switch (action)
        {
            case HotkeyAction.ToggleDesktop: ToggleDesktop = value; break;
            case HotkeyAction.OrganizeNow: OrganizeNow = value; break;
            case HotkeyAction.OpenApp: OpenApp = value; break;
            case HotkeyAction.PeekWidgets: PeekWidgets = value; break;
            case HotkeyAction.NewNote: NewNote = value; break;
            case HotkeyAction.QuickAdd: QuickAdd = value; break;
            case HotkeyAction.PeekDesktop: PeekDesktop = value; break;
            // Bilinmeyen eylem hiçbir kısayolun üzerine yazmaz.
        }
    }

    // 2.1 P6
    /// <summary>Daha yeni sürümün yazdığı bilinmeyen kısayollar (aynen geri yazılır).</summary>
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? Extra { get; set; }
}
