using System.IO;
using System.Runtime.InteropServices;
using Duzenleme.Core;
using Duzenleme.Localization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Duzenleme.Tests;

/// <summary>
/// WinForms'suz tepsi simgesinin saf parçaları: app.ico'dan kare seçimi (tepside 2.1.1'deki karenin aynısı), Windows'un
/// metin sınırları ve menü modeli (2.1.1'deki menüyle aynı sıra, metin ve işaretler).
/// </summary>
public class TrayModelTests
{
    // ---- IcoFrames ----

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(40)]
    public void Pick_returns_the_exact_32bpp_frame_of_app_ico(int pixels)
    {
        var ico = AppIco();
        var frame = IcoFrames.Pick(ico, pixels);

        Assert.NotNull(frame);
        Assert.Equal(pixels, frame.Value.Width);
        Assert.Equal(pixels, frame.Value.Height);
        Assert.Equal(32, frame.Value.BitDepth);
        // Görüntü verisi de o boyutta 32 bitlik bir BMP (BITMAPINFOHEADER; yükseklik XOR + AND maskesi için iki kat).
        var bits = IcoFrames.Bytes(ico, frame.Value);
        Assert.Equal(frame.Value.Length, bits.Length);
        Assert.Equal(40, BitConverter.ToInt32(bits, 0));
        Assert.Equal(pixels, BitConverter.ToInt32(bits, 4));
        Assert.Equal(pixels * 2, BitConverter.ToInt32(bits, 8));
        Assert.Equal(32, BitConverter.ToUInt16(bits, 14));
    }

    [Fact]
    public void Pick_takes_the_closest_size_and_the_first_of_equally_close_frames()
    {
        var ico = AppIco();
        // app.ico büyükten küçüğe sıralı: 18 px için 20 ile 16 eşit uzaklıkta, önce gelen (20) seçilir.
        Assert.Equal(20, IcoFrames.Pick(ico, 18)!.Value.Width);
        Assert.Equal(48, IcoFrames.Pick(ico, 50)!.Value.Width);
        Assert.Equal(128, IcoFrames.Pick(ico, 140)!.Value.Width);
    }

    [Fact]
    public void Pick_prefers_the_deepest_color_up_to_32_bits_among_same_size_frames()
    {
        var ico = Ico((16, 8), (16, 32), (16, 4), (32, 32));
        var frame = IcoFrames.Pick(ico, 16)!.Value;
        Assert.Equal(16, frame.Width);
        Assert.Equal(32, frame.BitDepth);
        Assert.Equal(Offset(ico, 1), frame.Offset);
    }

    [Fact]
    public void Pick_rejects_files_that_are_not_icons_or_point_outside_the_file()
    {
        Assert.Null(IcoFrames.Pick([], 16));
        Assert.Null(IcoFrames.Pick([0, 0, 2, 0, 1, 0], 16));          // imleç (tür 2)
        Assert.Null(IcoFrames.Pick([0, 0, 1, 0, 0, 0], 16));          // kare yok
        Assert.Null(IcoFrames.Pick([0, 0, 1, 0, 3, 0, 0, 0], 16));    // dizin dosyadan uzun
        var ico = Ico((16, 32));
        BitConverter.GetBytes(ico.Length).CopyTo(ico, 6 + 12);       // kare dosyanın sonunda başlıyor
        Assert.Null(IcoFrames.Pick(ico, 16));
    }

    [Fact]
    public void Native_structures_have_the_windows_sizes()
    {
        // cbSize yanlışsa Shell_NotifyIcon simgeyi hiç eklemez, InsertMenuItem öğe eklemez.
        var x64 = IntPtr.Size == 8;
        Assert.Equal(x64 ? 976 : 956, Marshal.SizeOf<TrayIcon.Native.NOTIFYICONDATA>());
        Assert.Equal(x64 ? 80 : 48, Marshal.SizeOf<TrayIcon.Native.MENUITEMINFO>());
    }

    // ---- Metin sınırları ----

    [Fact]
    public void Fit_keeps_short_text_and_trims_long_text_to_the_windows_limits()
    {
        Assert.Equal("", TrayText.Fit(null, TrayText.TipMax));
        Assert.Equal("NestDesk", TrayText.Fit("NestDesk", TrayText.TipMax));
        var exact = new string('a', TrayText.TitleMax);
        Assert.Same(exact, TrayText.Fit(exact, TrayText.TitleMax));

        foreach (var max in new[] { TrayText.TipMax, TrayText.TitleMax, TrayText.BodyMax })
        {
            var fitted = TrayText.Fit(new string('x', 1000), max);
            Assert.Equal(max, fitted.Length);
            Assert.EndsWith("…", fitted);
        }
        Assert.Equal((127, 63, 255), (TrayText.TipMax, TrayText.TitleMax, TrayText.BodyMax));
    }

    [Fact]
    public void Fit_never_splits_a_surrogate_pair()
    {
        // 62 harf + 😀 (iki UTF-16 birimi) + uzun kuyruk: 63'e sığdırırken vekil çiftinin yarısı kalmamalı.
        var text = new string('a', 61) + "😀" + new string('b', 20);
        var fitted = TrayText.Fit(text, TrayText.TitleMax);
        Assert.True(fitted.Length <= TrayText.TitleMax);
        Assert.False(char.IsHighSurrogate(fitted[^2]));
        Assert.Equal(new string('a', 61) + "…", fitted);
    }

    [Fact]
    public void Menu_text_escapes_ampersands_and_keeps_the_shortcut_column()
    {
        Assert.Equal("A && B", TrayText.MenuText("A & B"));
        Assert.Equal("a b c", TrayText.MenuText("a\tb\nc"));
        var item = new TrayMenuItem(TrayCommand.PeekDesktop, "Göz & at", Shortcut: "Ctrl+Alt+G");
        Assert.Equal("Göz && at\tCtrl+Alt+G", item.NativeText);
        Assert.Equal("Çıkış", new TrayMenuItem(TrayCommand.Exit, "Çıkış").NativeText);
    }

    // ---- Menü modeli ----

    private static TrayMenuState State(bool autoMove = true, bool hidden = false, bool peeking = false, string? hotkey = "Ctrl+Alt+G",
        string? removed = "Notlar", int mode = 0) =>
        new(autoMove, hidden, peeking, hotkey, removed, ["Hepsi", "Kutudakiler", "Bölmeler"], mode);

    [Fact]
    public void Menu_has_the_2_1_1_items_in_the_same_order()
    {
        var items = TrayMenuModel.Build(State());

        Assert.Equal(
        [
            TrayCommand.QuickAdd, TrayCommand.OpenApp, TrayCommand.Separator,
            TrayCommand.AutoMove, TrayCommand.OrganizeNow, TrayCommand.UndoLastMove, TrayCommand.ToggleDesktop, TrayCommand.PeekDesktop,
            TrayCommand.Separator,
            TrayCommand.UndoRemove, TrayCommand.IconModes, TrayCommand.RevealWidgets, TrayCommand.Separator, TrayCommand.Exit,
        ], items.Select(i => i.Command));
        Assert.Equal(
        [
            "Widget ekle…", "NestDesk'i aç", "",
            "Otomatik taşıma", "Masaüstünü şimdi düzenle", "Son taşımayı geri al", "Masaüstünü gizle", "Windows masaüstüne göz at",
            "",
            "Geri getir: Notlar", "Windows masaüstü simgeleri", "Widget'ları öne getir (5 sn)", "", "Çıkış",
        ], items.Select(i => i.Text));
    }

    [Fact]
    public void Add_widget_is_the_only_default_item()
    {
        var items = Flatten(TrayMenuModel.Build(State()));
        Assert.Equal([TrayCommand.QuickAdd], items.Where(i => i.IsDefault).Select(i => i.Command));
    }

    [Fact]
    public void Auto_move_is_checked_only_while_it_is_on()
    {
        Assert.True(TrayMenuModel.Build(State(autoMove: true)).Single(i => i.Command == TrayCommand.AutoMove).IsChecked);
        Assert.False(TrayMenuModel.Build(State(autoMove: false)).Single(i => i.Command == TrayCommand.AutoMove).IsChecked);
        Assert.All(TrayMenuModel.Build(State()).Where(i => i.Command is not (TrayCommand.AutoMove or TrayCommand.IconModes)),
            i => Assert.False(i.IsChecked || i.IsRadio));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Icon_modes_are_a_radio_group_with_the_current_mode_checked(int current)
    {
        var group = TrayMenuModel.Build(State(mode: current)).Single(i => i.Command == TrayCommand.IconModes);

        Assert.NotNull(group.Children);
        Assert.Equal(["Hepsi", "Kutudakiler", "Bölmeler"], group.Children.Select(c => c.Text));
        Assert.All(group.Children, c => Assert.True(c.IsRadio && c.Command == TrayCommand.IconMode));
        Assert.Equal([0, 1, 2], group.Children.Select(c => c.Choice));
        Assert.Equal([current], group.Children.Where(c => c.IsChecked).Select(c => c.Choice));
    }

    [Fact]
    public void Icon_mode_labels_are_the_settings_labels()
    {
        // Tepsi, Ayarlar ve Widget'lar sayfasıyla aynı metinleri Views.DesktopModes'tan alır.
        var labels = Views.DesktopModes.Choices.Select(Views.DesktopModes.Label).ToList();
        var group = TrayMenuModel.Build(State() with { IconModes = labels }).Single(i => i.Command == TrayCommand.IconModes);
        Assert.Equal(["Hepsi masaüstünde görünsün", "Kutulara eklediklerim masaüstünden kalksın", "Yalnızca bölmelerde göster"],
            group.Children!.Select(c => c.Text));
    }

    [Fact]
    public void Undo_remove_appears_only_after_a_widget_was_removed()
    {
        var without = TrayMenuModel.Build(State(removed: null));
        Assert.DoesNotContain(without, i => i.Command == TrayCommand.UndoRemove);
        Assert.Equal(13, without.Count);
        // Ayırıcıdan sonra doğrudan "Windows masaüstü simgeleri" gelir (2.1.1'de gizli öğe yer tutmazdı).
        var separator = without.ToList().FindIndex(8, i => i.IsSeparator);
        Assert.Equal(TrayCommand.IconModes, without[separator + 1].Command);

        var named = TrayMenuModel.Build(State(removed: "Tom & Jerry")).Single(i => i.Command == TrayCommand.UndoRemove);
        Assert.Equal("Geri getir: Tom & Jerry", named.Text);
        Assert.Equal("Geri getir: Tom && Jerry", named.NativeText);
    }

    [Fact]
    public void Desktop_and_peek_items_follow_the_current_state()
    {
        var shown = TrayMenuModel.Build(State(hidden: false, peeking: false));
        Assert.Equal("Masaüstünü gizle", shown.Single(i => i.Command == TrayCommand.ToggleDesktop).Text);
        var peek = shown.Single(i => i.Command == TrayCommand.PeekDesktop);
        Assert.Equal("Windows masaüstüne göz at", peek.Text);
        Assert.Equal("Ctrl+Alt+G", peek.Shortcut);
        Assert.Equal("Windows masaüstüne göz at\tCtrl+Alt+G", peek.NativeText);

        var hidden = TrayMenuModel.Build(State(hidden: true, peeking: true, hotkey: "Ctrl+Shift+F9"));
        Assert.Equal("Masaüstünü göster", hidden.Single(i => i.Command == TrayCommand.ToggleDesktop).Text);
        var back = hidden.Single(i => i.Command == TrayCommand.PeekDesktop);
        Assert.Equal("NestDesk'e dön", back.Text);
        Assert.Equal("NestDesk'e dön\tCtrl+Shift+F9", back.NativeText);

        // Kısayol atanmamışsa sekme ve kısayol sütunu yok.
        foreach (var none in new[] { null, "", " " })
        {
            var item = TrayMenuModel.Build(State(hotkey: none)).Single(i => i.Command == TrayCommand.PeekDesktop);
            Assert.Null(item.Shortcut);
            Assert.DoesNotContain("\t", item.NativeText);
        }
    }

    [Fact]
    public void Menu_is_translated_to_english()
    {
        using var _ = L.Use(Lang.En);
        var items = TrayMenuModel.Build(State(peeking: true));
        Assert.Equal("Add widget…", items[0].Text);
        Assert.Equal("Back to NestDesk", items.Single(i => i.Command == TrayCommand.PeekDesktop).Text);
        Assert.Equal("Bring back: Notlar", items.Single(i => i.Command == TrayCommand.UndoRemove).Text);
        Assert.Equal("Exit", items[^1].Text);
        Assert.Equal("Windows desktop icons", items.Single(i => i.Command == TrayCommand.IconModes).Text);
        Assert.Equal("Bring widgets to front (5 s)", items.Single(i => i.Command == TrayCommand.RevealWidgets).Text);
        Assert.Equal("Hide desktop", TrayMenuModel.Build(State()).Single(i => i.Command == TrayCommand.ToggleDesktop).Text);
    }

    /// <summary>Menü modelindeki her komutun tepside (TrayIcon.Run) bir karşılığı var: yeni öğe tıklanınca boşa düşmesin.</summary>
    [Fact]
    public void Every_menu_command_is_handled_by_the_tray()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "TrayIcon.cs"));
        var run = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "Run");
        var handled = run.DescendantNodes().OfType<SwitchLabelSyntax>()
            .SelectMany(label => label.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            .Where(m => m.Expression.ToString() == nameof(TrayCommand))
            .Select(m => m.Name.Identifier.ValueText)
            .ToHashSet();

        // Ayraç ve alt menü başlığı komut değildir.
        var commands = Enum.GetNames<TrayCommand>().Except([nameof(TrayCommand.Separator), nameof(TrayCommand.IconModes)]);
        Assert.Empty(commands.Where(c => !handled.Contains(c)));
        foreach (var item in Flatten(TrayMenuModel.Build(State())).Where(i => !i.IsSeparator && i.Children is null))
            Assert.Contains(item.Command.ToString(), handled);
    }

    private static IEnumerable<TrayMenuItem> Flatten(IEnumerable<TrayMenuItem> items) =>
        items.SelectMany(i => i.Children is null ? new[] { i } : Flatten(i.Children).Prepend(i));

    private static byte[] AppIco() => File.ReadAllBytes(Path.Combine(RepoRoot(), "src", "Duzenleme", "Assets", "app.ico"));

    /// <summary>Verilen (boyut, renk derinliği) karelerinden en küçük geçerli .ico (her kare 4 baytlık boş veri).</summary>
    private static byte[] Ico(params (int Size, int Bits)[] frames)
    {
        var data = new List<byte> { 0, 0, 1, 0, (byte)frames.Length, 0 };
        var offset = 6 + 16 * frames.Length;
        foreach (var (size, bits) in frames)
        {
            data.AddRange([(byte)size, (byte)size, 0, 0, 1, 0, (byte)bits, 0]);
            data.AddRange(BitConverter.GetBytes(4));
            data.AddRange(BitConverter.GetBytes(offset));
            offset += 4;
        }
        foreach (var _ in frames) data.AddRange([1, 2, 3, 4]);
        return data.ToArray();
    }

    private static int Offset(byte[] ico, int index) => BitConverter.ToInt32(ico, 6 + 16 * index + 12);

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
    }
}
