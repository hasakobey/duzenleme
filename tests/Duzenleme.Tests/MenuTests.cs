using System.Runtime.ExceptionServices;
using System.Windows;
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
/// Widget menüsünün seçenek modeli (Menus + OptionMenuItem): işaretler modelden okunur, tıklama/UIA ayarı uygular, Sync
/// menüyü yerinde tazeler. Menü hiç açılmaz (pencere yok); tıklama WPF'in kendi yolundan (UIA Invoke → ClickItem →
/// OnClick → Click) geçer.
/// </summary>
public class MenuTests
{
    private enum Look { Glass, Dark, Light }

    [Fact]
    public void Choice_checks_exactly_the_current_value_and_clicking_it_again_keeps_it()
    {
        OnSta(() =>
        {
            var current = Look.Glass;
            var applied = new List<Look>();
            var menu = new ContextMenu();
            var choice = Menus.Choice("Arka plan", () => current,
                [(Look.Glass, "Cam"), (Look.Dark, "Koyu"), (Look.Light, "Açık")], v => { applied.Add(v); current = v; });
            menu.Items.Add(choice);
            var options = choice.Items.OfType<OptionMenuItem>().ToList();

            Assert.Equal([true, false, false], options.Select(o => o.IsChecked));

            Click(options[1]);
            Assert.Equal([Look.Dark], applied);
            Assert.Equal([false, true, false], options.Select(o => o.IsChecked));

            // Seçili seçeneğe yeniden tıklamak onu boşa düşürmez (eskiden WPF işareti kendisi çeviriyordu).
            Click(options[1]);
            Assert.Equal([Look.Dark, Look.Dark], applied);
            Assert.Equal([false, true, false], options.Select(o => o.IsChecked));
        });
    }

    [Fact]
    public void Check_mark_cannot_be_flipped_without_the_model()
    {
        OnSta(() =>
        {
            var on = false;
            var item = Menus.Toggle("Gölge", () => on, () => on = !on);
            item.IsChecked = true; // WPF'in kendi çevirmesi de bu yoldan geçer (SetCurrentValue)
            Assert.False(item.IsChecked);
            on = true;
            item.Refresh();
            Assert.True(item.IsChecked);
        });
    }

    [Fact]
    public void Automation_toggle_applies_the_option_like_a_click()
    {
        OnSta(() =>
        {
            var on = false;
            var calls = 0;
            var menu = new ContextMenu();
            var item = Menus.Toggle("Gölge", () => on, () => { calls++; on = !on; });
            menu.Items.Add(item);
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(Peer(item).GetPattern(PatternInterface.Toggle));

            Assert.Equal(ToggleState.Off, toggle.ToggleState);
            toggle.Toggle();
            Pump();
            Assert.Equal(1, calls);
            Assert.True(on);
            Assert.True(item.IsChecked);
            Assert.Equal(ToggleState.On, toggle.ToggleState);

            toggle.Toggle();
            Pump();
            Assert.Equal(2, calls);
            Assert.False(item.IsChecked);
        });
    }

    [Fact]
    public void Automation_toggle_on_a_radio_option_selects_it()
    {
        OnSta(() =>
        {
            var current = Look.Glass;
            var menu = new ContextMenu();
            var choice = Menus.Choice("Arka plan", () => current, [(Look.Glass, "Cam"), (Look.Dark, "Koyu")], v => current = v);
            menu.Items.Add(choice);
            var dark = choice.Items.OfType<OptionMenuItem>().Last();

            ((IToggleProvider)Peer(dark).GetPattern(PatternInterface.Toggle)).Toggle();
            Pump();
            Assert.Equal(Look.Dark, current);
            Assert.Equal([false, true], choice.Items.OfType<OptionMenuItem>().Select(o => o.IsChecked));
        });
    }

    [Fact]
    public void Sync_refreshes_every_item_of_the_menu_including_closed_submenus()
    {
        OnSta(() =>
        {
            var header = true;
            var collapsed = false;
            var menu = new ContextMenu();
            var show = new MenuItem { Header = "Göster" };
            var headerPart = Menus.Toggle("Başlık satırı", () => header, () => header = !header);
            show.Items.Add(headerPart);
            menu.Items.Add(new MenuItem { Header = "Görünüm", Items = { show } });
            var more = new MenuItem { Header = "Diğer" };
            var collapse = Menus.Live(Menus.Toggle("Başlığa katla", () => collapsed, () => collapsed = !collapsed),
                visible: () => header);
            var counter = Menus.Live(new MenuItem(), header: () => $"Gizlenen ({(header ? 0 : 1)})", enabled: () => !header);
            more.Items.Add(collapse);
            more.Items.Add(counter);
            menu.Items.Add(more);

            Assert.Equal(Visibility.Visible, collapse.Visibility);
            Assert.Equal("Gizlenen (0)", counter.Header);
            Assert.False(counter.IsEnabled);

            // Başlık satırı başka bir alt menüden gizlenince katlama seçeneği kaybolur, sayaç ve etkinlik tazelenir.
            Click(headerPart);
            Assert.False(header);
            Assert.False(headerPart.IsChecked);
            Assert.Equal(Visibility.Collapsed, collapse.Visibility);
            Assert.Equal("Gizlenen (1)", counter.Header);
            Assert.True(counter.IsEnabled);

            // Model başka yerden değişince Sync herhangi bir öğeden çağrılabilir.
            collapsed = true;
            Menus.Sync(counter);
            Assert.True(collapse.IsChecked);
        });
    }

    [Fact]
    public void Options_stay_open_and_commands_close()
    {
        OnSta(() =>
        {
            var c = new WidgetConfig { Kind = WidgetKind.Fence };
            Assert.True(Menus.Toggle("Gölge", () => false, () => { }).StaysOpenOnClick);
            Assert.False(Menus.Toggle("Masaüstü simgelerini yalnızca bölmelerde göster", () => false, () => { }, staysOpen: false).StaysOpenOnClick);
            Assert.False(Menus.Item("Kaldır", () => { }).StaysOpenOnClick);

            var icons = Menus.TileOptions(c, change => change(), singleClickOption: true);
            var options = icons.SelectMany(i => Descendants(i).Prepend(i)).OfType<OptionMenuItem>().ToList();
            Assert.NotEmpty(options);
            Assert.All(options, o => Assert.True(o.StaysOpenOnClick));

            var parts = Menus.Parts(c, [("header", "Başlık satırı"), Menus.ClosePart], () => { });
            Assert.All(parts.Items.OfType<OptionMenuItem>(), o => Assert.True(o.StaysOpenOnClick));

            // Menünün yapısını değiştiren seçim (zamanlayıcının türü) menüyü kapatır; seçili olan yine işaretli kalır.
            var mode = "countdown";
            var kind = Menus.Choice("Tür", () => mode, [("countdown", "Zamanlayıcı"), ("pomodoro", "Pomodoro")], v => mode = v, staysOpen: false);
            var kinds = kind.Items.OfType<OptionMenuItem>().ToList();
            Assert.All(kinds, o => Assert.False(o.StaysOpenOnClick));
            Assert.True(kinds[0].IsChecked);
        });
    }

    [Fact]
    public void Tile_options_apply_immediately_and_move_the_check()
    {
        OnSta(() =>
        {
            var c = new WidgetConfig { Kind = WidgetKind.Fence, Align = TileAlign.Left };
            var changes = 0;
            var menu = new ContextMenu();
            var icons = Menus.TileOptions(c, change => { change(); changes++; }, singleClickOption: false);
            foreach (var item in icons) menu.Items.Add(item);
            // Görünüş (Izgara/Liste) ve simge boyutu panelin üst bölümünde (EditorContent.TileLook); burada seyrek olanlar.
            Assert.DoesNotContain(icons, m => (string)m.Header is "Düzen" or "Boyut");
            var align = icons.Single(m => (string)m.Header == "Hizalama");
            var center = align.Items.OfType<OptionMenuItem>().Single(o => (string)o.Header == "Ortaya");

            Click(center);
            Assert.Equal(TileAlign.Center, c.Align);
            Assert.Equal(1, changes);
            Assert.Equal(["Ortaya"], align.Items.OfType<OptionMenuItem>().Where(o => o.IsChecked).Select(o => (string)o.Header));

            var hideNames = icons.OfType<OptionMenuItem>().Single(o => (string)o.Header == "Adları gizle");
            Click(hideNames);
            Assert.True(c.HideLabels);
            Assert.True(hideNames.IsChecked);
        });
    }

    [Fact]
    public void Parts_toggle_hidden_parts_and_are_translated()
    {
        OnSta(() =>
        {
            using var _ = L.Use(Lang.En);
            var c = new WidgetConfig { Kind = WidgetKind.Clock };
            var changed = 0;
            var menu = new ContextMenu();
            var parts = Menus.Parts(c, [("greeting", "Selam ve gün"), Menus.ClosePart], () => changed++);
            menu.Items.Add(parts);
            var close = parts.Items.OfType<OptionMenuItem>().Last();

            Assert.Equal("Show", parts.Header);
            Assert.Equal(["Greeting and day", "Remove button (×)"], parts.Items.OfType<OptionMenuItem>().Select(o => (string)o.Header));
            Assert.True(close.IsChecked);

            Click(close);
            Assert.Contains("close", c.HiddenParts);
            Assert.False(close.IsChecked);
            Assert.Equal(1, changed);
        });
    }

    /// <summary>
    /// Kolay NestDesk: fareyle (ya da klavyeyle) bir seçeneğin üstüne gelmek hiçbir şeyi değiştirmez; önizleme yok. Bütün
    /// seçenek türleri (Choice, Toggle, Parts, simge seçenekleri) üstüne gelme, ayrılma, odak ve alt menünün kapanması
    /// olaylarını alır; ne ayar ne işaret değişir, hiçbir değişiklik bildirilmez.
    /// </summary>
    [Fact]
    public void Options_never_react_to_hover()
    {
        OnSta(() =>
        {
            var c = new WidgetConfig { Kind = WidgetKind.Fence, Style = WidgetStyle.Glass, Accent = WidgetAccent.Violet };
            var before = JsonFile.Serialize(c);
            var calls = 0;
            var menu = new ContextMenu();
            menu.Items.Add(Menus.Choice("Ölçek", () => 1.0, [(1.0, "Normal"), (2.0, "%200")], _ => calls++));
            menu.Items.Add(Menus.Toggle("Gölge", () => false, () => calls++));
            menu.Items.Add(Menus.Parts(c, [("header", "Başlık satırı"), Menus.ClosePart], () => calls++));
            foreach (var item in Menus.TileOptions(c, change => { change(); calls++; }, singleClickOption: true)) menu.Items.Add(item);

            var all = menu.Items.OfType<MenuItem>().SelectMany(i => Descendants(i).Prepend(i)).ToList();
            var options = all.OfType<OptionMenuItem>().ToList();
            Assert.True(options.Count > 15);
            var checks = options.Select(o => o.IsChecked).ToList();
            foreach (var item in all)
            {
                item.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
                item.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
                item.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, null, item) { RoutedEvent = Keyboard.GotKeyboardFocusEvent });
                item.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuClosedEvent, item));
            }
            Pump();

            Assert.Equal(0, calls);
            Assert.Equal(before, JsonFile.Serialize(c));
            Assert.Equal(checks, options.Select(o => o.IsChecked));
        });
    }

    /// <summary>
    /// Kolay NestDesk: widget menülerinde gri ipucu satırı, alt menü ve uygulama geneli öğe yok. İskelet (FillMenu → BuildMenu)
    /// "Görünüm ▸", "Diğer ▸", widget ekleme, göz atma, mıknatıs, çakışma, hizalama ve masaüstü simge kiplerini içermez;
    /// görünümler simge kipini değiştirmez. Fareye bağlı iki seçenek ("Fare üstünde değilken …") menüde hiç yok, Düzenle
    /// panelinde yalnızca açıkken.
    /// </summary>
    [Fact]
    public void Widget_menus_have_no_hint_rows_and_no_app_wide_items()
    {
        var widgets = Path.Combine(RepoRoot(), "src", "Duzenleme", "Widgets");
        var hints = Directory.GetFiles(widgets, "*.cs").Where(f => File.ReadAllText(f).Contains("Menus.Hint(", StringComparison.Ordinal))
            .Select(Path.GetFileName).ToList();
        Assert.Empty(hints);
        Assert.DoesNotContain("Hint(", File.ReadAllText(Path.Combine(widgets, "Menus.cs")));

        var window = File.ReadAllText(Path.Combine(widgets, "WidgetWindow.cs"));
        var fill = System.Text.RegularExpressions.Regex.Match(window, @"private void FillMenu\(ContextMenu menu\)\s*\{(?<body>[\s\S]*?)\n    \}").Groups["body"].Value;
        var build = System.Text.RegularExpressions.Regex.Match(window, @"internal static void BuildMenu\((?<body>[\s\S]*?)\n    \}").Groups["body"].Value;
        Assert.NotEmpty(fill);
        Assert.NotEmpty(build);
        Assert.Contains("BuildMenu(", fill);
        foreach (var banned in new[] { "ShowQuickAdd", "TogglePeek", "SnapWidgets", "PreventOverlap", "ArrangeAll", "DesktopModes", "Hint(", "Preview(",
                     "LookMenus", "\"Görünüm\"", "\"Diğer\"", "Appearance", "Settings", "More", "FadeUntilHover", "AutoRollup", "Fare üstünde" })
        {
            Assert.DoesNotContain(banned, fill);
            Assert.DoesNotContain(banned, build);
        }
        var editor = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Duzenleme", "Views", "WidgetEditor.cs"));
        Assert.Contains("if (c.FadeUntilHover)", editor);
        Assert.Contains("if (c.AutoRollup && target.View.Collapsible)", editor);

        foreach (var file in Directory.GetFiles(widgets, "*View*.cs"))
            Assert.DoesNotContain("DesktopModes.Set(", File.ReadAllText(file));
    }

    [Fact]
    public void Data_in_headers_keeps_its_underscores()
    {
        Assert.Equal("İş__Dosyaları", Menus.Literal("İş_Dosyaları"));
        Assert.Equal("PDF", Menus.Literal("PDF"));
        OnSta(() =>
        {
            // Menü başlığı AccessText ile çizilir: "_b" erişim tuşu olurdu (alt çizgi görünmez); "__" düz "_" kalır.
            Assert.Equal('b', new AccessText { Text = "a_b" }.AccessKey);
            Assert.Equal('\0', new AccessText { Text = Menus.Literal("a_b") }.AccessKey);
        });
    }

    // ---------------------------------------------------------------- 2.2 Tema (widget'ın teması Düzenle panelinin galerisinde: LookGalleryTests)

    [Fact]
    public void Theme_names_are_translated_in_english()
    {
        using var _ = L.Use(Lang.En);
        Assert.Equal(["Classic", "Automatic (follows Windows)", "Neon", "Night", "Paper", "Ocean"],
            LookMenus.ThemeChoices.Select(t => t.Label));
    }

    // ---------------------------------------------------------------- yardımcılar

    private static AutomationPeer Peer(UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element);

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }

    /// <summary>Fare tıklamasıyla aynı yol: UIA Invoke → MenuItem.ClickItem → OnClick; Click olayı çizimden sonra gelir.</summary>
    private static void Click(MenuItem item)
    {
        ((IInvokeProvider)Peer(item).GetPattern(PatternInterface.Invoke)).Invoke();
        Pump();
    }

    /// <summary>Bekleyen iş dağıtıcı işlerini (ör. MenuItem'ın çizimden sonra kuyruğa koyduğu Click) çalıştırır.</summary>
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    private static IEnumerable<MenuItem> Descendants(MenuItem item)
    {
        foreach (var child in item.Items.OfType<MenuItem>())
        {
            yield return child;
            foreach (var below in Descendants(child)) yield return below;
        }
    }

    /// <summary>WPF denetimleri STA iş parçacığında kurulur (uygulama nesnesi yok).</summary>
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
}
