using System.Runtime.InteropServices;
using Duzenleme.Core;
using Duzenleme.Desktop;
using static Duzenleme.Desktop.KeySender;

namespace Duzenleme.Tests;

/// <summary>
/// Tuş gönderme: yalnızca saf kurucular denenir (gerçek SendInput, pano ya da GetAsyncKeyState kullanılmaz). Değiştirici
/// sırası, genişletilmiş tuşlar, bırakma sırası, basılı değiştiricilerin önce bırakılması ve birleşim sınırı.
/// </summary>
public class KeySenderTests
{
    // Klavye düzeninden bağımsız sahte tarama kodları (0xE0 öneki: genişletilmiş).
    private static readonly Dictionary<ushort, ushort> Scans = new()
    {
        [VK_CONTROL] = 0x1D, [VK_MENU] = 0x38, [VK_SHIFT] = 0x2A, [VK_LWIN] = 0xE05B,
        [VK_LCONTROL] = 0x1D, [VK_RCONTROL] = 0xE01D, [VK_LMENU] = 0x38, [VK_RMENU] = 0xE038, [VK_LSHIFT] = 0x2A,
        [0x54] = 0x14, [0x56] = 0x2F, [0x74] = 0x3F, [0x25] = 0x4B /* sol ok: önek yok, tablodan */, [0x2E] = 0xE053,
    };

    private static readonly Dictionary<string, ushort> Vks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["T"] = 0x54, ["V"] = 0x56, ["F5"] = 0x74, ["Left"] = 0x25, ["Delete"] = 0x2E, ["LeftShift"] = VK_LSHIFT,
    };

    private static ushort Scan(ushort vk) => Scans.GetValueOrDefault(vk);

    private static ushort? Vk(string key) => Vks.TryGetValue(key, out var vk) ? vk : null;

    private static List<KeyStroke[]> Build(IReadOnlyList<string> chords, IReadOnlyCollection<ushort>? held = null)
    {
        Assert.True(TryBuild(chords, out var groups, held, Scan, Vk));
        return groups;
    }

    private static string Text(IEnumerable<KeyStroke> strokes) =>
        string.Join(" ", strokes.Select(s => $"{(s.Up ? "-" : "+")}{s.Vk:X2}"));

    [Fact]
    public void Chord_presses_modifiers_then_key_and_releases_in_reverse()
    {
        var group = Assert.Single(Build(["Ctrl+Shift+T"]));
        Assert.Equal("+11 +10 +54 -54 -10 -11", Text(group));
        Assert.Equal(0x14, group[2].Scan);
        Assert.All(group, s => Assert.False(s.Extended));
    }

    [Fact]
    public void Modifier_order_is_ctrl_alt_shift_win()
    {
        var group = Assert.Single(Build(["Win+Shift+Alt+Ctrl+T"]));
        Assert.Equal("+11 +12 +10 +5B +54 -54 -5B -10 -12 -11", Text(group));
        Assert.True(group[3].Extended);       // Win
        Assert.Equal(0x5B, group[3].Scan);    // önek atılır, bayrak kalır
    }

    [Fact]
    public void Extended_keys_get_the_flag_from_the_table_or_the_prefix()
    {
        var groups = Build(["Left", "Ctrl+Delete", "F5"]);
        Assert.Equal(3, groups.Count);         // her birleşim ayrı SendInput (arada 20 ms)
        Assert.All(groups[0], s => Assert.True(s.Extended));                  // ok tuşu: tablodan
        Assert.Equal(0x4B, groups[0][0].Scan);
        Assert.True(groups[1].Single(s => s.Vk == 0x2E && !s.Up).Extended);  // Delete: 0xE0 önekinden
        Assert.False(groups[1].Single(s => s.Vk == VK_CONTROL && !s.Up).Extended);
        Assert.Equal("+74 -74", Text(groups[2]));                             // tek tuş da gönderilir
        Assert.True(IsExtended(VK_RCONTROL, 0x1D));
        Assert.True(IsExtended(0x2D, 0x52));   // Insert
        Assert.False(IsExtended(0x41, 0x1E));  // A
    }

    [Fact]
    public void Held_modifiers_are_released_first_with_a_mask_for_alt_and_win()
    {
        var groups = Build(["Ctrl+V"], held: [VK_LMENU, VK_LCONTROL]);
        Assert.Equal(2, groups.Count);
        Assert.Equal("+E8 -E8 -A2 -A4", Text(groups[0]));   // maske, sonra bırakmalar (Ctrl, Alt sırasıyla)
        Assert.Equal("+11 +56 -56 -11", Text(groups[1]));

        var shiftOnly = Build(["T"], held: [VK_LSHIFT]);
        Assert.Equal("-A0", Text(shiftOnly[0]));            // Alt/Win yok: maske yok
        Assert.Single(Build(["T"], held: [0x41]));          // değiştirici olmayan "basılı" tuş yok sayılır
        Assert.True(Build(["T"], held: [VK_RMENU])[0].Single(s => s.Vk == VK_RMENU).Extended);
    }

    [Fact]
    public void Chord_count_and_content_are_checked()
    {
        Assert.True(TryBuild(Enumerable.Repeat("T", RingActions.MaxChords).ToList(), out var eight, null, Scan, Vk));
        Assert.Equal(RingActions.MaxChords, eight.Count);
        Assert.False(TryBuild(Enumerable.Repeat("T", RingActions.MaxChords + 1).ToList(), out var none, null, Scan, Vk));
        Assert.Empty(none);
        Assert.False(TryBuild([], out _, null, Scan, Vk));
        Assert.False(TryBuild(null, out _, null, Scan, Vk));
        Assert.False(TryBuild(["Ctrl+Bilinmeyen"], out _, null, Scan, Vk));   // çözülemeyen tuş
        Assert.False(TryBuild(["LeftShift"], out _, null, Scan, Vk));         // yalnızca değiştirici
    }

    [Fact]
    public void Default_key_names_resolve_through_the_hotkey_table()
    {
        Assert.True(TryBuild(["Ctrl+Shift+T", "F5", "Enter", "1"], out var groups, null, Scan));
        Assert.Equal(0x54, groups[0][2].Vk);
        Assert.Equal(0x74, groups[1][0].Vk);
        Assert.Equal(0x0D, groups[2][0].Vk);
        Assert.Equal(0x31, groups[3][0].Vk);
        Assert.Equal(new[] { 0x11, 0x12, 0x52 }, VirtualKeysOf(new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "R")));
        Assert.Equal(new[] { 0x5B, 0x5C, 0x52 }, VirtualKeysOf(new Hotkey(HotkeyModifiers.Win, "R")));
    }

    [Fact]
    public void Input_structure_matches_win32()
    {
        var input = ToInput(new KeyStroke(0x2E, 0x53, Up: true, Extended: true));
        Assert.Equal(1u, input.type);                      // INPUT_KEYBOARD
        Assert.Equal(0x2E, input.u.ki.wVk);
        Assert.Equal(0x53, input.u.ki.wScan);
        Assert.Equal(0x0003u, input.u.ki.dwFlags);         // KEYUP | EXTENDEDKEY
        Assert.Equal(0u, ToInput(new KeyStroke(0x54, 0x14, false, false)).u.ki.dwFlags);
        Assert.Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf<INPUT>());
    }
}

/// <summary>Metin yapıştırma: önceki pano içeriğinin geri konup konamayacağı (saf sınıflandırma; pano kullanılmaz).</summary>
public class SnippetPasteTests
{
    [Fact]
    public void Plain_text_and_its_synthesized_formats_restore_as_unicode_text()
    {
        var (ok, formats) = SnippetPaste.Classify(["UnicodeText", "Locale", "Text", "OEMText"]);
        Assert.True(ok);
        Assert.Equal(new[] { "UnicodeText" }, formats);
        Assert.Equal(new[] { "Text" }, SnippetPaste.Classify(["Text", "OEMText"]).Formats);
    }

    [Fact]
    public void Browser_html_files_and_images_are_restorable()
    {
        var web = SnippetPaste.Classify(["HTML Format", "Chromium internal source URL", "UnicodeText", "Text", "Rich Text Format"]);
        Assert.True(web.Restorable);
        Assert.Equal(new[] { "UnicodeText", "Rich Text Format", "HTML Format" }, web.Formats);

        var files = SnippetPaste.Classify(["Shell IDList Array", "DataObjectAttributes", "Preferred DropEffect", "FileDrop", "FileNameW", "FileName"]);
        Assert.True(files.Restorable);
        Assert.Equal(new[] { "FileDrop", "Preferred DropEffect" }, files.Formats);   // kes/kopyala bilgisi korunur

        var image = SnippetPaste.Classify(["Bitmap", "DeviceIndependentBitmap", "Format17"]);
        Assert.Equal(new[] { "DeviceIndependentBitmap" }, image.Formats);
    }

    [Fact]
    public void Empty_clipboard_is_restored_by_clearing()
    {
        var (ok, formats) = SnippetPaste.Classify([]);
        Assert.True(ok);
        Assert.Empty(formats);
        // Önceki içerik de geçmiş dışıydıysa (ör. parola yöneticisi) işaretler sorun değil.
        Assert.True(SnippetPaste.Classify(["UnicodeText", SnippetPaste.ExcludeFromMonitoring, SnippetPaste.CanIncludeInHistory]).Restorable);
    }

    [Fact]
    public void Anything_outside_the_whitelist_is_not_restored()
    {
        Assert.False(SnippetPaste.Classify(["UnicodeText", "Biff12", "XML Spreadsheet"]).Restorable);      // Excel hücreleri
        Assert.False(SnippetPaste.Classify(["Embed Source", "Object Descriptor", "Rich Text Format"]).Restorable);
        Assert.Empty(SnippetPaste.Classify(["PNG"]).Formats);
    }
}
