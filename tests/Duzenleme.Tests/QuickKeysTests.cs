using System.Runtime.ExceptionServices;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Localization;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// "Numara tuşlarıyla aç" (2.2): 1–9 ve sayısal tuş takımı görünen n'inci öğeyi açar; seçenek varsayılan kapalı, rozet yok,
/// ipucu menüde. Harfle arama zaten çalışır (ListBox'ta açık, öğenin adı ToString): XAML'e ekleme yapılmadı.
/// </summary>
public class QuickKeysTests
{
    [Fact]
    public void Number_row_and_numpad_digits_map_to_items()
    {
        Key[] row = [Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9];
        Key[] pad = [Key.NumPad1, Key.NumPad2, Key.NumPad3, Key.NumPad4, Key.NumPad5, Key.NumPad6, Key.NumPad7, Key.NumPad8, Key.NumPad9];
        for (var i = 0; i < QuickKeys.Count; i++)
        {
            Assert.Equal(i, QuickKeys.IndexOf(KeyInterop.VirtualKeyFromKey(row[i])));
            Assert.Equal(i, QuickKeys.IndexOf(KeyInterop.VirtualKeyFromKey(pad[i])));
        }
        foreach (var other in new[] { Key.D0, Key.NumPad0, Key.A, Key.F2, Key.Enter, Key.End, Key.Space, Key.System, Key.ImeProcessed })
            Assert.Equal(-1, QuickKeys.IndexOf(KeyInterop.VirtualKeyFromKey(other)));
        Assert.Equal(-1, QuickKeys.IndexOf(0));
    }

    [Fact]
    public void Quick_keys_are_off_by_default()
    {
        var config = new WidgetConfig { Kind = WidgetKind.Launcher };
        Assert.False(config.QuickKeys);
    }

    [Fact]
    public void Digit_opens_the_nth_visible_item_only_when_allowed()
    {
        OnSta(() =>
        {
            var c = new WidgetConfig { Kind = WidgetKind.Launcher, QuickKeys = true };
            var tiles = Enumerable.Range(1, 3).Select(i => new TileItem { Name = $"Öğe {i}", Path = $@"C:\yok\{i}.txt" }).ToList();
            var list = new ListBox { ItemsSource = tiles };

            Assert.Same(tiles[0], Menus.QuickOpenTarget(Press(Key.D1), c, list, editing: false));
            Assert.Same(tiles[2], Menus.QuickOpenTarget(Press(Key.NumPad3), c, list, editing: false));
            Assert.Null(Menus.QuickOpenTarget(Press(Key.D4), c, list, editing: false));   // o sırada öğe yok
            Assert.Null(Menus.QuickOpenTarget(Press(Key.A), c, list, editing: false));      // harf aramaya kalır
            Assert.Null(Menus.QuickOpenTarget(Press(Key.D1), c, list, editing: true));      // ad düzenleniyor

            c.Collapsed = true;
            Assert.Null(Menus.QuickOpenTarget(Press(Key.D1), c, list, editing: false));     // katlı widget
            c.Collapsed = false;
            c.QuickKeys = false;
            Assert.Null(Menus.QuickOpenTarget(Press(Key.D1), c, list, editing: false));     // seçenek kapalı (varsayılan)
            c.QuickKeys = true;

            // Yazı kutusundaki rakam yazıya kalır (bölmenin araması, ad kutusu).
            var box = new TextBox();
            TileItem? target = tiles[0];
            box.PreviewKeyDown += (_, e) => target = Menus.QuickOpenTarget(e, c, list, editing: false);
            box.RaiseEvent(Press(Key.D1));
            Assert.Null(target);

            // Görünen sıra: süzülmüş liste (arama) sonucu n'inci öğe.
            list.Items.Filter = o => o != tiles[0];
            Assert.Same(tiles[1], Menus.QuickOpenTarget(Press(Key.D1), c, list, editing: false));
        });
    }

    [Fact]
    public void Letter_type_ahead_already_works_without_xaml_changes()
    {
        OnSta(() =>
        {
            // WPF ListBox harfle aramayı varsayılan açar; öğenin aranan metni ToString (TileItem: görünen ad).
            Assert.True(new ListBox().IsTextSearchEnabled);
            Assert.Equal("Rapor", new TileItem { Name = "Rapor", Path = @"C:\x\2024 rapor.pdf" }.ToString());
        });
        foreach (var view in new[] { "FenceView.xaml", "LauncherView.xaml" })
        {
            var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "Widgets", view));
            Assert.DoesNotContain("IsTextSearchEnabled", xaml);
            Assert.DoesNotContain("TextSearch.", xaml);
            Assert.DoesNotContain("DisplayMemberPath", xaml);
        }
    }

    [Fact]
    public void Menu_option_turns_quick_keys_on_without_rebuilding_tiles()
    {
        OnSta(() =>
        {
            var c = new WidgetConfig { Kind = WidgetKind.Fence };
            var changes = 0;
            var menu = new ContextMenu();
            var icons = Menus.TileOptions(c, change => { change(); changes++; }, singleClickOption: true);
            foreach (var item in icons) menu.Items.Add(item);
            var option = icons.OfType<OptionMenuItem>().Single(o => (string)o.Header == "Numara tuşlarıyla aç (1–9)");
            // Kolay NestDesk: rozet ve gri ipucu satırı yok (tıklanamayan satır yok).
            Assert.DoesNotContain(icons, o => !o.IsEnabled);

            Assert.False(option.IsChecked);
            Assert.True(option.StaysOpenOnClick);

            Click(option);
            Assert.True(c.QuickKeys);
            Assert.True(option.IsChecked);
            Assert.Equal(0, changes); // görünüş değişmez: kutucuklar yeniden kurulmaz

            Click(option);
            Assert.False(c.QuickKeys);
        });
    }

    [Fact]
    public void Menu_option_is_translated()
    {
        OnSta(() =>
        {
            using var _ = L.Use(Lang.En);
            var icons = Menus.TileOptions(new WidgetConfig { Kind = WidgetKind.Launcher, QuickKeys = true }, change => change(), singleClickOption: false);
            Assert.Contains(icons.OfType<OptionMenuItem>(), o => (string)o.Header == "Open with number keys (1–9)" && o.IsChecked);
        });
    }

    private static KeyEventArgs Press(Key key) =>
        new(Keyboard.PrimaryDevice, new TestSource(), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };

    /// <summary>Tuş olayının kaynağı (pencere yok).</summary>
    private sealed class TestSource : System.Windows.PresentationSource
    {
        public override System.Windows.Media.Visual? RootVisual { get; set; }
        public override bool IsDisposed => false;
        protected override System.Windows.Media.CompositionTarget? GetCompositionTargetCore() => null;
    }

    private static void Click(MenuItem item)
    {
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(item).GetPattern(PatternInterface.Invoke)).Invoke();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    }

    private static void OnSta(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { error = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA testi zaman aşımına uğradı");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException(AppContext.BaseDirectory);
    }
}
