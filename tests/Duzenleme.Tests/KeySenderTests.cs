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

/// <summary>
/// Gönderimin güvenlik kuralları (saf; gerçek SendInput, pano ya da başka pencere kullanılmaz): hedef önde ve girdiyi
/// alabilmeli (bütünlük düzeyi), yarıda kalan gönderim basılı tuş bırakmaz, gönderimler tek tek, çözülemeyen tuş adı yok.
/// </summary>
public class InputSafetyTests
{
    private static readonly IntPtr Target = new(0x1234), Other = new(0x5678);

    private static string Text(IEnumerable<KeyStroke> strokes) =>
        string.Join(" ", strokes.Select(s => $"{(s.Up ? "-" : "+")}{s.Vk:X2}"));

    private static KeyStroke Down(ushort vk) => new(vk, 0, Up: false, Extended: false);
    private static KeyStroke Up(ushort vk) => new(vk, 0, Up: true, Extended: false);

    [Fact]
    public void Keys_and_text_go_only_to_the_window_in_front_that_accepts_input()
    {
        Assert.Equal(InputTarget.Ok, ForegroundInfo.Decide(Target, Target, own: false, desktop: false, canReceive: true));
        Assert.Equal(InputTarget.NotInFront, ForegroundInfo.Decide(Target, Other, false, false, true));     // öne gelemedi / başkasına geçildi
        Assert.Equal(InputTarget.NotInFront, ForegroundInfo.Decide(Target, IntPtr.Zero, false, false, true));
        Assert.Equal(InputTarget.Elevated, ForegroundInfo.Decide(Target, Target, false, false, false));     // yüksek düzey
        Assert.Equal(InputTarget.Elevated, ForegroundInfo.Decide(Target, Target, false, false, null));      // bilinmiyor: gönderilmez
        Assert.Equal(InputTarget.NoTarget, ForegroundInfo.Decide(IntPtr.Zero, IntPtr.Zero, false, false, true));
        Assert.Equal(InputTarget.NoTarget, ForegroundInfo.Decide(Target, Target, own: true, false, true));  // halka, widget
        Assert.Equal(InputTarget.NoTarget, ForegroundInfo.Decide(Target, Target, false, desktop: true, true));
        Assert.Equal(InputTarget.NoTarget, ForegroundInfo.Check(IntPtr.Zero));
    }

    [Fact]
    public void Integrity_levels_decide_whether_input_arrives()
    {
        const int Low = 0x1000, Medium = 0x2000, MediumPlus = 0x2100, High = 0x3000;
        Assert.True(ForegroundInfo.Receives(Medium, Medium));
        Assert.True(ForegroundInfo.Receives(Low, Medium));
        Assert.False(ForegroundInfo.Receives(High, Medium));         // yönetici olarak çalışan pencere
        Assert.False(ForegroundInfo.Receives(MediumPlus, Medium));   // yükseltilmemiş ama orta düzeyin üstünde (uiAccess)
        Assert.True(ForegroundInfo.Receives(High, High));            // NestDesk de yönetici olarak çalışıyorsa
        Assert.Null(ForegroundInfo.Receives(null, Medium));
        Assert.Null(ForegroundInfo.Receives(Medium, null));

        // Bu sürecin kendi düzeyi okunabilmeli (bütünlük etiketi yapısının okunuşu): düşük ile sistem arası.
        Assert.InRange(ForegroundInfo.OwnIntegrityLevel ?? -1, Low, 0x4000);
    }

    [Fact]
    public void A_partial_send_releases_every_key_it_pressed()
    {
        KeyStroke[] chord = [Down(VK_CONTROL), Down(VK_SHIFT), Down(0x54), Up(0x54), Up(VK_SHIFT), Up(VK_CONTROL)];
        Assert.Empty(ReleaseAfterPartial(chord, 0));
        Assert.Equal("-11", Text(ReleaseAfterPartial(chord, 1)));
        Assert.Equal("-54 -10 -11", Text(ReleaseAfterPartial(chord, 3)));
        Assert.Equal("-10 -11", Text(ReleaseAfterPartial(chord, 4)));
        Assert.Equal("-11", Text(ReleaseAfterPartial(chord, 5)));
        Assert.Empty(ReleaseAfterPartial(chord, 6));
        Assert.Empty(ReleaseAfterPartial(chord, 99));

        KeyStroke[] release = [Down(MaskVk), Up(MaskVk), Up(VK_LMENU)];   // basılı değiştiricileri bırakma grubu
        Assert.Equal("-E8", Text(ReleaseAfterPartial(release, 1)));
        Assert.Empty(ReleaseAfterPartial(release, 2));
    }

    [Fact]
    public void Key_names_resolve_the_same_way_for_checking_and_sending()
    {
        Assert.False(HotkeyManager.TryGetVirtualKey("12", out _));     // Enum.TryParse sayıyı başka tuşa çevirirdi
        Assert.False(HotkeyManager.TryGetVirtualKey("A,B", out _));    // ...virgülü de bayrak birleşimine
        Assert.False(HotkeyManager.TryGetVirtualKey("", out _));
        Assert.True(HotkeyManager.TryGetVirtualKey("1", out var one));
        Assert.Equal(0x31u, one);
        Assert.True(HotkeyManager.TryGetVirtualKey("OemPeriod", out _));

        Assert.True(CanSend(["Ctrl+Shift+T", "F5", "Enter"]));
        Assert.False(CanSend(["Ctrl+12"]));
        Assert.False(CanSend(["Ctrl+Foo"]));
        Assert.False(CanSend(["LeftShift"]));
        Assert.False(CanSend(null));

        // Core tuş tablosunu bilmez; gönderenin çözücüsü verilince "Ctrl+Foo" elenir.
        Assert.True(RingActions.ValidKeys(["Ctrl+Foo"]));
        Assert.False(RingActions.ValidKeys(["Ctrl+Foo"], k => HotkeyManager.TryGetVirtualKey(k, out _)));
        Assert.True(RingActions.ValidKeys(["Ctrl+Shift+T"], k => HotkeyManager.TryGetVirtualKey(k, out _)));
    }

    [Fact]
    public async Task Sends_go_one_at_a_time_and_never_without_a_target()
    {
        Assert.True(TryEnterGate());
        try
        {
            Assert.False(TryEnterGate());
            Assert.Equal(SendResult.Busy, await SendToAsync(Target, ["F5"]));                     // hiçbir şey gönderilmez
            Assert.Equal(SnippetPaste.Outcome.Busy, await SnippetPaste.PasteAsync(Target, "x"));  // panoya dokunulmaz
        }
        finally { ExitGate(); }

        // Hedef yoksa SendInput'a ve panoya hiç varılmaz; kapı da geri bırakılır.
        Assert.Equal(SendResult.NoTarget, SendTo(IntPtr.Zero, ["F5"], null));
        Assert.Equal(SendResult.Invalid, SendTo(IntPtr.Zero, ["Ctrl+Foo"], null));
        Assert.Equal(SnippetPaste.Outcome.NoTarget, await SnippetPaste.PasteAsync(IntPtr.Zero, "x"));
        Assert.Equal(SnippetPaste.Outcome.Failed, await SnippetPaste.PasteAsync(IntPtr.Zero, ""));
        Assert.True(TryEnterGate());
        ExitGate();
    }
}

/// <summary>Metin yapıştırma: önceki pano içeriğinden ne geri konur ve dışlama işaretleri (saf; pano kullanılmaz).</summary>
public class SnippetPasteTests
{
    [Fact]
    public void Plain_text_and_its_synthesized_formats_restore_as_unicode_text()
    {
        var plan = SnippetPaste.Classify(["UnicodeText", "Locale", "Text", "OEMText"]);
        Assert.False(plan.Lossy);
        Assert.Equal(new[] { "UnicodeText" }, plan.Formats);
        Assert.Equal(new[] { "Text" }, SnippetPaste.Classify(["Text", "OEMText"]).Formats);
    }

    [Fact]
    public void Browser_html_files_and_images_restore_in_clipboard_order()
    {
        var web = SnippetPaste.Classify(["HTML Format", "Chromium internal source URL", "UnicodeText", "Text", "Rich Text Format"]);
        Assert.False(web.Lossy);
        Assert.Equal(new[] { "HTML Format", "UnicodeText", "Rich Text Format" }, web.Formats);

        var files = SnippetPaste.Classify(["Shell IDList Array", "DataObjectAttributes", "Preferred DropEffect", "FileDrop", "FileNameW", "FileName"]);
        Assert.False(files.Lossy);
        Assert.Equal(new[] { "FileDrop", "Preferred DropEffect" }, files.Formats);   // kes/kopyala bilgisi korunur

        var image = SnippetPaste.Classify(["Bitmap", "DeviceIndependentBitmap", "Format17", "PNG"]);
        Assert.False(image.Lossy);
        Assert.Equal(new[] { "DeviceIndependentBitmap", "PNG" }, image.Formats);
        Assert.Equal(new[] { "Bitmap" }, SnippetPaste.Classify(["Bitmap"]).Formats);
    }

    [Fact]
    public void Companion_formats_of_common_apps_are_not_a_loss()
    {
        // VS Code / Electron, Chromium
        var code = SnippetPaste.Classify(["Chromium Web Custom MIME Data Format", "UnicodeText", "Text", "HTML Format", "Chromium internal source RFH token"]);
        Assert.False(code.Lossy);
        Assert.Equal(new[] { "UnicodeText", "HTML Format" }, code.Formats);

        // Gezgin, dosyalar bir kez yapıştırıldıktan sonra
        var pasted = SnippetPaste.Classify(["Shell IDList Array", "FileDrop", "Preferred DropEffect", "Performed DropEffect",
            "Logical Performed DropEffect", "Paste Succeeded", "FileNameW"]);
        Assert.False(pasted.Lossy);
        Assert.Equal(new[] { "FileDrop", "Preferred DropEffect" }, pasted.Formats);
    }

    [Fact]
    public void Unknown_formats_drop_but_the_rest_comes_back()
    {
        var excel = SnippetPaste.Classify(["Biff12", "XML Spreadsheet", "UnicodeText", "Text", "HTML Format", "Rich Text Format", "Link"]);
        Assert.True(excel.Lossy);                                                     // hücreler düşer, metin ve biçim döner
        Assert.Equal(new[] { "UnicodeText", "HTML Format", "Rich Text Format" }, excel.Formats);

        var office = SnippetPaste.Classify(["Embed Source", "Object Descriptor", "Rich Text Format"]);
        Assert.True(office.Lossy);
        Assert.Equal(new[] { "Rich Text Format" }, office.Formats);

        var attachment = SnippetPaste.Classify(["FileGroupDescriptorW", "FileContents"]);   // geri konacak asıl biçim yok:
        Assert.True(attachment.Lossy);                                                      // pano boşaltılır, metin kalmaz
        Assert.Empty(attachment.Formats);
    }

    [Fact]
    public void Empty_clipboard_is_restored_by_clearing()
    {
        var empty = SnippetPaste.Classify([]);
        Assert.False(empty.Lossy);
        Assert.Empty(empty.Formats);
        Assert.Empty(SnippetPaste.Classify(["Locale", "Clipboard Viewer Ignore"]).Formats);
        // Önceki içerik de geçmiş dışıydıysa (ör. parola yöneticisi) işaretler kayıp sayılmaz.
        Assert.False(SnippetPaste.Classify(["UnicodeText", SnippetPaste.ExcludeFromMonitoring, SnippetPaste.CanIncludeInHistory]).Lossy);
    }

    [Fact]
    public void Snippet_and_restored_content_carry_all_three_exclusion_markers()
    {
        static void AssertMarkers(List<(string Format, object Value)> payload)
        {
            foreach (var marker in new[] { SnippetPaste.ExcludeFromMonitoring, SnippetPaste.CanIncludeInHistory, SnippetPaste.CanUploadToCloud })
            {
                var (_, value) = Assert.Single(payload, p => p.Format == marker);
                Assert.Equal(new byte[4], Assert.IsType<MemoryStream>(value).ToArray());   // DWORD 0
            }
        }

        var snippet = SnippetPaste.SnippetPayload("Merhaba");
        Assert.Equal(4, snippet.Count);
        Assert.Equal("UnicodeText", snippet[0].Format);
        Assert.Equal("Merhaba", snippet[0].Value);
        AssertMarkers(snippet);

        var restore = SnippetPaste.RestorePayload([("HTML Format", new MemoryStream([1, 2])), ("UnicodeText", "eski")]);
        Assert.Equal(5, restore.Count);
        Assert.Equal(new[] { "HTML Format", "UnicodeText" }, restore.Take(2).Select(p => p.Format));
        AssertMarkers(restore);

        // Akışlar her seferinde yeni (bir DataObject okununca ötekinin işareti boşalmasın).
        Assert.NotSame(SnippetPaste.Exclusions().First().Value, SnippetPaste.Exclusions().First().Value);
    }
}

/// <summary>Genel kısayolların yönlendirilmesi (saf; RegisterHotKey çağrılmaz, pencere kurulmaz).</summary>
public class HotkeyRoutingTests
{
    [Fact]
    public void Ring_message_is_ignored_while_the_ring_shortcut_is_off()
    {
        var off = new HotkeySettings();
        var ringId = (int)HotkeyAction.Ring + 1;
        Assert.Equal((true, (HotkeyAction?)null), HotkeyManager.Route(ringId, off));
        Assert.Equal((true, (HotkeyAction?)null), HotkeyManager.Route(ringId, null));   // ayar henüz uygulanmadı
        Assert.Equal((true, (HotkeyAction?)HotkeyAction.Ring), HotkeyManager.Route(ringId, new HotkeySettings { Ring = "Ctrl+Alt+R" }));

        // Öteki eylemler her zaman işlenir (UI testleri kısayolu WM_HOTKEY ile dener).
        foreach (var action in Enum.GetValues<HotkeyAction>().Where(a => a != HotkeyAction.Ring))
        {
            Assert.Equal((true, (HotkeyAction?)action), HotkeyManager.Route((int)action + 1, off));
            Assert.Equal((true, (HotkeyAction?)action), HotkeyManager.Route((int)action + 1, null));
        }
        Assert.Equal((false, (HotkeyAction?)null), HotkeyManager.Route(0, off));
        Assert.Equal((false, (HotkeyAction?)null), HotkeyManager.Route(ringId + 1, off));
    }

    [Fact]
    public void Same_shortcut_as_an_earlier_action_is_a_clash_not_another_app()
    {
        Assert.Empty(HotkeyManager.Clashes(new HotkeySettings()));
        Assert.Empty(HotkeyManager.Clashes(new HotkeySettings { OrganizeNow = "", NewNote = "x" }));

        // 2.1'de başka eyleme verilen Ctrl+Alt+R, Extra'dan gelen halka kısayolunu gölgeler.
        var clash = Assert.Single(HotkeyManager.Clashes(new HotkeySettings { OrganizeNow = "Ctrl+Alt+R", Ring = "alt+ctrl+r" }));
        Assert.Equal(HotkeyAction.Ring, clash.Key);
        Assert.Equal(HotkeyAction.OrganizeNow, clash.Value);

        // Aynı tuşun iki adı (Enter/Return) da çakışır.
        var enter = HotkeyManager.Clashes(new HotkeySettings { NewNote = "Ctrl+Alt+Enter", QuickAdd = "Ctrl+Alt+Return" });
        Assert.Equal(HotkeyAction.NewNote, enter[HotkeyAction.QuickAdd]);
    }

    /// <summary>
    /// Test masaüstünde NESTDESK_TEST_RING=1 iken Ayarlar'dan halka kısayolunu açmak Windows'a gerçek bir kısayol (Ctrl+Alt+R)
    /// kaydetmez: deneme kullanıcının tuşunu kapmasın; WM_HOTKEY id 8 iletiyle gönderilir. Öteki eylemler ve test dışı her şey
    /// her zamanki gibi kaydedilir.
    /// </summary>
    [Fact]
    public void Test_ring_mode_never_registers_the_ring_shortcut_with_windows()
    {
        Assert.False(HotkeyManager.RegistersWithWindows(HotkeyAction.Ring, testRing: true));
        Assert.True(HotkeyManager.RegistersWithWindows(HotkeyAction.Ring, testRing: false));
        foreach (var action in Enum.GetValues<HotkeyAction>().Where(a => a != HotkeyAction.Ring))
        {
            Assert.True(HotkeyManager.RegistersWithWindows(action, testRing: true));
            Assert.True(HotkeyManager.RegistersWithWindows(action, testRing: false));
        }
        Assert.False(RingHotkey.TestOverride(isTestDesktop: false, "1")); // gerçek masaüstünde test kipi yok
    }
}
