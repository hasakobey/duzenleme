using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Localization;
using Duzenleme.Views;
using Duzenleme.Widgets;

namespace Duzenleme.Tests;

/// <summary>
/// "Düzenle" paneli (<see cref="WidgetEditor"/>) sahte bir widget'la: görünümün menü öğeleri panel satırı olur (seçenekler panele,
/// komutlar menüde kalır; <see cref="MenuRows"/>), her denetim widget'a hemen uygulanır, "Geri al" panel açıldığındaki hâle döner.
/// Pencere açılmaz; denetimler STA iş parçacığında kurulur.
/// </summary>
public class EditorTests
{
    [Fact]
    public void Options_become_rows_and_widget_commands_stay_in_the_menu()
    {
        EditorFakes.OnSta(() =>
        {
            var (editor, target, view) = EditorFakes.Open(EditorFakes.Clock());
            var ids = EditorFakes.Ids(editor);

            Assert.Contains("Editor.Option.S.1", ids);        // Saniyeyi göster (anahtar)
            Assert.Contains("Editor.Option.S.2", ids);        // Saat biçimi (uzun seçenekler: açılır liste)
            Assert.Contains("Editor.Option.S.3.1", ids);      // Tür (kısa seçenekler: bölümlü seçim)
            Assert.Contains("Editor.Option.M.1", ids);        // Panoya kopyala (düğme, Daha fazla'da)
            Assert.Contains("Editor.Part.greeting", ids);      // Göster
            Assert.Contains("Editor.Part.close", ids);         // Daha fazla: Kaldır düğmesi (×)
            Assert.Contains("Editor.Part.edit", ids);          // Daha fazla: Düzenle düğmesi (⋯)
            Assert.DoesNotContain(EditorFakes.Named(editor), n => n == "Başlat"); // widget'ın işi menüde kalır
            Assert.IsType<Wpf.Ui.Controls.ToggleSwitch>(EditorFakes.Find(editor, "Editor.Option.S.1"));
            Assert.IsType<ComboBox>(EditorFakes.Find(editor, "Editor.Option.S.2"));
            Assert.IsType<RadioButton>(EditorFakes.Find(editor, "Editor.Option.S.3.2"));
            Assert.Equal(0, view.Runs);
        });
    }

    [Fact]
    public void Opening_and_refreshing_the_panel_changes_nothing()
    {
        EditorFakes.OnSta(() =>
        {
            var config = EditorFakes.Clock();
            config.Scale = 1.25;
            config.Corners = CornerStyle.Soft;
            var before = System.Text.Json.JsonSerializer.Serialize(config, JsonFile.Options);
            var (editor, target, view) = EditorFakes.Open(config);
            editor.Refresh();
            editor.Rebuild();
            EditorFakes.Pump();
            Assert.Equal(0, target.Updates);
            Assert.Equal(0, target.Renames);
            Assert.False(view.Seconds);
            Assert.Null(view.Hour12);
            Assert.Equal(0, view.PartsChanged);
            Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(config, JsonFile.Options));
            // Seçili olanlar işaretli görünür.
            Assert.True(((RadioButton)EditorFakes.Find(editor, "Editor.Accent.1")).IsChecked);
            Assert.True(((RadioButton)EditorFakes.Find(editor, "Editor.Size.Large")).IsChecked);
            Assert.True(((RadioButton)EditorFakes.Find(editor, "Editor.Corners.Soft")).IsChecked);
            Assert.True(((RadioButton)EditorFakes.Find(editor, "Editor.Opacity.None")).IsChecked);
            Assert.True(((ToggleButton)EditorFakes.Find(editor, "Editor.Shadow")).IsChecked);
        });
    }

    [Fact]
    public void Every_row_applies_the_widgets_own_option_at_once()
    {
        EditorFakes.OnSta(() =>
        {
            var (editor, _, view) = EditorFakes.Open(EditorFakes.Clock());

            ((ToggleButton)EditorFakes.Find(editor, "Editor.Option.S.1")).IsChecked = true;
            Assert.True(view.Seconds);

            ((ComboBox)EditorFakes.Find(editor, "Editor.Option.S.2")).SelectedIndex = 2;
            Assert.True(view.Hour12);

            ((RadioButton)EditorFakes.Find(editor, "Editor.Option.S.3.2")).IsChecked = true;
            Assert.Equal("pomodoro", view.Mode);
            EditorFakes.Pump(); // tür menünün yapısını değiştirir: panel yeniden kurulur
            Assert.True(((RadioButton)EditorFakes.Find(editor, "Editor.Option.S.3.2")).IsChecked);

            var greeting = (ToggleButton)EditorFakes.Find(editor, "Editor.Part.greeting");
            Assert.True(greeting.IsChecked);
            greeting.IsChecked = false;
            Assert.Contains("greeting", view.Config.HiddenParts);
            Assert.Equal(1, view.PartsChanged);

            EditorFakes.Click(EditorFakes.Find(editor, "Editor.Option.M.1"));
            Assert.Equal(1, view.Copies);
        });
    }

    [Fact]
    public void Look_color_and_size_apply_live_and_undo_returns_to_the_opening_state()
    {
        EditorFakes.OnSta(() =>
        {
            var config = EditorFakes.Clock();
            var opening = System.Text.Json.JsonSerializer.Serialize(config, JsonFile.Options);
            var (editor, target, _) = EditorFakes.Open(config);
            var undo = (ButtonBase)EditorFakes.Find(editor, "Editor.Undo");
            Assert.False(undo.IsEnabled);

            ((RadioButton)EditorFakes.Find(editor, "Editor.Look.neon")).IsChecked = true;
            Assert.Equal("neon", config.LookTheme);
            Assert.True(undo.IsEnabled);
            ((RadioButton)EditorFakes.Find(editor, "Editor.Accent.4")).IsChecked = true;
            Assert.Equal(WidgetAccent.Orange, config.Accent);
            Assert.Null(config.LookAccentColor);
            ((RadioButton)EditorFakes.Find(editor, "Editor.Size.Large")).IsChecked = true;
            Assert.Equal(1.25, config.Scale);
            Assert.True(target.Updates >= 3); // her biri WidgetWindow.Update'ten (uygula + kısa sürede kaydet)

            EditorFakes.Click(undo);
            Assert.Equal(1, target.Restores);
            Assert.Equal(opening, System.Text.Json.JsonSerializer.Serialize(config, JsonFile.Options));
            Assert.False(((ButtonBase)EditorFakes.Find(editor, "Editor.Undo")).IsEnabled);
            Assert.True(((RadioButton)EditorFakes.Find(editor, "Editor.Look.cam")).IsChecked);
        });
    }

    [Fact]
    public void Other_saved_scale_shows_as_a_selected_custom_size_and_is_not_lost()
    {
        EditorFakes.OnSta(() =>
        {
            var config = EditorFakes.Clock();
            config.Scale = 1.5;
            var (editor, _, _) = EditorFakes.Open(config);
            var custom = (RadioButton)EditorFakes.Find(editor, "Editor.Size.Custom");
            Assert.True(custom.IsChecked);
            Assert.Equal("Özel (%150)", EditorFakes.Text(custom));
            Assert.Equal(1.5, config.Scale);
            Assert.False(((RadioButton)EditorFakes.Find(editor, "Editor.Size.Medium")).IsChecked);
        });
    }

    [Fact]
    public void Notes_have_size_but_no_gallery_and_clocks_have_no_name_box()
    {
        EditorFakes.OnSta(() =>
        {
            var note = new WidgetConfig { Kind = WidgetKind.Note };
            var (editor, _, _) = EditorFakes.Open(note, usesThemeColors: false);
            var ids = EditorFakes.Ids(editor);
            Assert.DoesNotContain("Editor.Look.cam", ids);
            Assert.DoesNotContain("Editor.ApplyAll", ids);
            Assert.Contains("Editor.Size.Medium", ids);
            Assert.Contains("Editor.Name", ids);

            var (clock, _, _) = EditorFakes.Open(EditorFakes.Clock());
            Assert.DoesNotContain("Editor.Name", EditorFakes.Ids(clock));
        });
    }

    [Fact]
    public void Name_saves_on_commit_and_empty_means_the_default_name()
    {
        EditorFakes.OnSta(() =>
        {
            var config = new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.Files };
            var (editor, target, _) = EditorFakes.Open(config);
            var name = (TextBox)EditorFakes.Find(editor, "Editor.Name");
            Assert.Equal("", name.Text);

            name.Text = "  İş  ";
            editor.CommitPending();
            Assert.Equal("İş", config.Title);
            Assert.Equal(1, target.Renames);

            editor.CommitPending(); // değişmedi: yeniden adlandırılmaz
            Assert.Equal(1, target.Renames);

            EditorFakes.Pump();
            name = (TextBox)EditorFakes.Find(editor, "Editor.Name");
            name.Text = " ";
            editor.CommitPending();
            Assert.Null(config.Title);
        });
    }

    [Fact]
    public void Saving_the_name_keeps_the_panel_rows_while_the_card_keeps_its_view()
    {
        EditorFakes.OnSta(() =>
        {
            // Ad kutusu odağı panelde bir denetime basılırken bırakır ve kaydeder: kartın başlığı yerinde tazelenir
            // (IWidgetView.RefreshTitle), panel yeniden kurulmaz; basılan denetim yerinde kalır, tıklama kaybolmaz.
            var config = new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.Files };
            var (editor, target, _) = EditorFakes.Open(config);
            var shadow = EditorFakes.Find(editor, "Editor.Shadow");
            ((TextBox)EditorFakes.Find(editor, "Editor.Name")).Text = "Belgeler";
            editor.CommitPending();
            EditorFakes.Pump();
            Assert.Equal("Belgeler", config.Title);
            Assert.Same(shadow, EditorFakes.Find(editor, "Editor.Shadow"));

            // Görünümü yeniden kurulan kartta (başlığını yerinde tazeleyemeyen görünüm) panel de yeniden kurulur.
            target.ReplaceView();
            editor.Refresh();
            EditorFakes.Pump();
            Assert.NotSame(shadow, EditorFakes.Find(editor, "Editor.Shadow"));
        });
    }

    [Fact]
    public void More_tells_screen_readers_whether_it_is_open()
    {
        EditorFakes.OnSta(() =>
        {
            var (editor, _, _) = EditorFakes.Open(EditorFakes.Clock());
            var more = (UIElement)EditorFakes.Find(editor, "Editor.More");
            var shadow = (UIElement)EditorFakes.Find(editor, "Editor.Shadow");
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(more);
            var fold = Assert.IsAssignableFrom<System.Windows.Automation.Provider.IExpandCollapseProvider>(
                peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.ExpandCollapse));
            Assert.Equal(ExpandCollapseState.Collapsed, fold.ExpandCollapseState);
            Assert.True(Folded(shadow));

            fold.Expand();
            Assert.Equal(ExpandCollapseState.Expanded, fold.ExpandCollapseState);
            Assert.False(Folded(shadow));

            EditorFakes.Click(more);
            Assert.Equal(ExpandCollapseState.Collapsed, fold.ExpandCollapseState);
            Assert.True(Folded(shadow));
        });
    }

    /// <summary>Öğe ya da üstlerinden biri gizli mi (katlı "Daha fazla")?</summary>
    private static bool Folded(DependencyObject d)
    {
        for (var at = d; at is not null; at = LogicalTreeHelper.GetParent(at))
            if (at is UIElement { Visibility: not Visibility.Visible }) return true;
        return false;
    }

    [Fact]
    public void Scale_changed_elsewhere_shows_up_as_custom_size_when_the_panel_refreshes()
    {
        EditorFakes.OnSta(() =>
        {
            var config = EditorFakes.Clock();
            var (editor, _, _) = EditorFakes.Open(config);
            Assert.DoesNotContain("Editor.Size.Custom", EditorFakes.Ids(editor));

            config.Scale = 1.5; // Ctrl + tekerlek
            editor.Refresh();
            EditorFakes.Pump();
            Assert.True(((RadioButton)EditorFakes.Find(editor, "Editor.Size.Custom")).IsChecked);

            config.Scale = 1.0;
            editor.Refresh();
            EditorFakes.Pump();
            Assert.DoesNotContain("Editor.Size.Custom", EditorFakes.Ids(editor));
            Assert.True(((RadioButton)EditorFakes.Find(editor, "Editor.Size.Medium")).IsChecked);
        });
    }

    [Fact]
    public void Note_view_left_behind_by_undo_does_not_overwrite_the_restored_text()
    {
        EditorFakes.OnSta(() =>
        {
            // Görünüm liste gösteriyordu; panelin "Geri al"ı ayarı düz nota (ve ilk metnine) döndürdü. Görünüm yeniden kurulurken
            // eskisi bırakılır (Flush, Detach): boş yazı alanını ya da eski maddeleri ayarın üstüne yazmamalı.
            var config = new WidgetConfig { Kind = WidgetKind.Note, NoteChecklist = false, NoteText = "ilk metin" };
            var note = WidgetMenuTests.View<NoteView>(config,
                ("_saveTimer", new DispatcherTimer()), ("Editor", new TextBox()), ("_shownChecklist", true));
            note.Flush();
            Assert.Equal("ilk metin", config.NoteText);
        });
    }

    [Fact]
    public void Look_for_all_shows_a_notice_whose_undo_puts_the_others_back()
    {
        EditorFakes.OnSta(() =>
        {
            var config = EditorFakes.Clock();
            var other = new WidgetConfig { Kind = WidgetKind.Fence, Style = WidgetStyle.Light };
            var (editor, target, _) = EditorFakes.Open(config, others: [other]);

            ((RadioButton)EditorFakes.Find(editor, "Editor.Look.gece")).IsChecked = true;
            EditorFakes.Click(EditorFakes.Find(editor, "Editor.ApplyAll"));
            Assert.Equal("gece", other.LookTheme);
            var notice = (FrameworkElement)EditorFakes.Find(editor, "Editor.Notice");
            Assert.Equal(Visibility.Visible, notice.Visibility);

            EditorFakes.Click(EditorFakes.Find(editor, "Editor.Notice.Undo"));
            Assert.Null(other.LookTheme);
            Assert.Equal(WidgetStyle.Light, other.Style);
            Assert.Equal(Visibility.Collapsed, notice.Visibility);
            Assert.Equal("gece", config.LookTheme); // bu widget'ın kendi seçimi kalır
        });
    }

    [Fact]
    public void Footer_removes_and_finishes()
    {
        EditorFakes.OnSta(() =>
        {
            var (editor, target, _) = EditorFakes.Open(EditorFakes.Clock());
            var done = 0;
            editor.DoneRequested += () => done++;
            EditorFakes.Click(EditorFakes.Find(editor, "Editor.Done"));
            Assert.Equal(1, done);
            EditorFakes.Click(EditorFakes.Find(editor, "Editor.Remove"));
            Assert.Equal(1, target.Removes);
            EditorFakes.Click(EditorFakes.Find(editor, "Editor.Duplicate"));
            Assert.Equal(1, target.Duplicates);
        });
    }

    [Fact]
    public void Adapter_classifies_menu_items()
    {
        EditorFakes.OnSta(() =>
        {
            var command = Menus.Item("Aç", () => { });
            var option = Menus.Toggle("Gölge", () => true, () => { });
            var choice = Menus.Choice("Köşeler", () => 1, [(1, "Yuvarlak"), (2, "Köşeli")], _ => { });
            var parts = Menus.Parts(new WidgetConfig(), [("count", L.N("Öğe sayısı"))], () => { });
            var group = new MenuItem { Header = "Simgeler", Items = { Menus.Toggle("Adları gizle", () => false, () => { }) } };
            var commands = new MenuItem { Header = "Öğe ekle", Items = { Menus.Item("Web adresi…", () => { }) } };

            Assert.Equal(MenuRows.RowKind.Skip, MenuRows.Classify(command, optionsOnly: true));
            Assert.Equal(MenuRows.RowKind.Command, MenuRows.Classify(command, optionsOnly: false));
            Assert.Equal(MenuRows.RowKind.Skip, MenuRows.Classify(new Separator(), optionsOnly: false));
            Assert.Equal(MenuRows.RowKind.Switch, MenuRows.Classify(option, optionsOnly: true));
            Assert.Equal(MenuRows.RowKind.Choice, MenuRows.Classify(choice, optionsOnly: true));
            Assert.Equal(MenuRows.RowKind.Parts, MenuRows.Classify(parts, optionsOnly: false));
            Assert.Equal(MenuRows.RowKind.Group, MenuRows.Classify(group, optionsOnly: true));
            Assert.Equal(MenuRows.RowKind.Skip, MenuRows.Classify(commands, optionsOnly: true));
            Assert.Equal(MenuRows.RowKind.Group, MenuRows.Classify(commands, optionsOnly: false));
            Assert.Equal([("count", "Öğe sayısı")], MenuRows.PartsIn([parts]).Select(p => (p.Key, MenuRows.Label(p.Option))));
            Assert.Equal("İş_Dosyaları", MenuRows.Label(Menus.Item(Menus.Literal("İş_Dosyaları"), () => { })));
        });
    }

    [Fact]
    public void Widget_menu_starts_with_edit_and_the_panel_applies_through_the_window()
    {
        var widgets = Path.Combine(RepoRoot(), "src", "Duzenleme", "Widgets");
        var window = File.ReadAllText(Path.Combine(widgets, "WidgetWindow.cs"));
        var fill = System.Text.RegularExpressions.Regex.Match(window, @"private void FillMenu\(ContextMenu menu\)\s*\{(?<body>[\s\S]*?)\n    \}").Groups["body"].Value;
        // Menü "Düzenle…" ile başlar (WidgetMenuTests satırları dener); "Düzenle…" ve kartın ⋯ düğmesi aynı paneli açar.
        Assert.Contains("BuildMenu(menu, own.Primary, Config.Locked, OpenEditor,", fill);
        Assert.Contains("OpenEditor()", File.ReadAllText(Path.Combine(widgets, "CardButtons.cs")));
        // Panelin görünüş değişiklikleri menüdekiyle aynı yoldan (uygula + kısa sürede kaydet; önizleme biter).
        Assert.Contains("void Views.IEditorTarget.Update(Action change) => Update(change);", window);
        // "Geri al" kutunun öğelerini değiştirdiyse taşınmış öğeler ve kutudaki dosyalar uzlaştırılır.
        Assert.Matches(@"RestoreFrom[\s\S]*BoxMover\.Reconcile\(\);\s*AppHost\.RefreshPinnedPaths\(\);", window);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Duzenleme.sln");
    }

    // ---------------------------------------------------------------- görünümün kendi panel içeriği (bölme, kutu, not…)

    [Fact]
    public void Native_content_replaces_the_menu_rows_and_keeps_show_and_more()
    {
        EditorFakes.OnSta(() =>
        {
            var config = new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.Files };
            var view = new FakeNativeView(config);
            var (editor, _) = EditorFakes.Open(view);
            var ids = EditorFakes.Ids(editor);

            Assert.Contains("Editor.Test.Files", ids);
            Assert.Contains("Editor.Test.Flag", ids);
            Assert.Contains("Editor.Items", ids);
            Assert.DoesNotContain(ids, id => id.StartsWith("Editor.Option.S", StringComparison.Ordinal)); // menünün ayarları çizilmez
            Assert.Contains("Editor.Part.count", ids);    // Göster yine menünün parçalarından
            Assert.Contains("Editor.Part.edit", ids);     // Daha fazla: ⋯
            Assert.Contains("Editor.Option.M.1", ids);    // Daha fazla: Yenile
            Assert.DoesNotContain(EditorFakes.Named(editor), n => n == "Ara"); // widget'ın işi menüde kalır

            // Yongalar: seçim uygulanır, panel yeniden kurulur, kaynak değişince panelde "Geri al".
            ((RadioButton)EditorFakes.Find(editor, "Editor.Test.All")).IsChecked = true;
            EditorFakes.Pump();
            Assert.Equal("All", view.Source);
            Assert.True(((RadioButton)EditorFakes.Find(editor, "Editor.Test.All")).IsChecked);
            Assert.Equal(Visibility.Visible, ((FrameworkElement)EditorFakes.Find(editor, "Editor.Notice")).Visibility);
            EditorFakes.Click(EditorFakes.Find(editor, "Editor.Notice.Undo"));
            Assert.Equal("Files", view.Source);
            Assert.True(((RadioButton)EditorFakes.Find(editor, "Editor.Test.Files")).IsChecked);

            // Menü açan yonga seçmez: menüyü açar, işaret modelde kalır.
            ((RadioButton)EditorFakes.Find(editor, "Editor.Test.Folder")).IsChecked = true;
            EditorFakes.Pump();
            Assert.Equal(1, view.MenusOpened);
            Assert.Equal("Files", view.Source);
            Assert.False(((RadioButton)EditorFakes.Find(editor, "Editor.Test.Folder")).IsChecked);

            ((ToggleButton)EditorFakes.Find(editor, "Editor.Test.Flag")).IsChecked = true;
            Assert.True(view.Flag);
        });
    }

    [Fact]
    public void Item_list_shows_actions_only_on_the_selected_row()
    {
        EditorFakes.OnSta(() =>
        {
            var view = new FakeNativeView(new WidgetConfig { Kind = WidgetKind.Launcher });
            var (editor, _) = EditorFakes.Open(view);
            Assert.DoesNotContain("Editor.Item.Remove", EditorFakes.Ids(editor)); // seçili satır yok: düğme de yok

            EditorFakes.Click(EditorFakes.Find(editor, "Editor.Item.1"));
            var ids = EditorFakes.Ids(editor);
            Assert.Single(ids, id => id == "Editor.Item.Remove");
            Assert.False(((UIElement)EditorFakes.Find(editor, "Editor.Item.Up")).IsEnabled);   // ilk satır
            Assert.True(((UIElement)EditorFakes.Find(editor, "Editor.Item.Down")).IsEnabled);

            EditorFakes.Click(EditorFakes.Find(editor, "Editor.Item.Down"));
            Assert.Equal(["Not Defteri", "Hesap"], view.Tiles.Select(t => t.Name));
            // Taşınan öğe seçili kalır (artık ikinci satır).
            Assert.Equal("Hesap", AutomationProperties.GetName(EditorFakes.Find(editor, "Editor.Item.2")));
            Assert.Equal("Seçili", AutomationProperties.GetItemStatus(EditorFakes.Find(editor, "Editor.Item.2")));

            EditorFakes.Click(EditorFakes.Find(editor, "Editor.Item.Remove"));
            Assert.Equal(["Not Defteri"], view.Tiles.Select(t => t.Name));
            Assert.DoesNotContain("Editor.Item.Remove", EditorFakes.Ids(editor));

            // Kutu dışarıdan değişince (sürükle-bırak) tazeleme listeyi yeniden kurar.
            view.Tiles.Add(new TileItem { Name = "Paint", Path = @"C:\Uygulamalar\Paint.exe" });
            editor.Refresh();
            Assert.Equal("Paint", AutomationProperties.GetName(EditorFakes.Find(editor, "Editor.Item.2")));
        });
    }

    [Fact]
    public void Panel_rebuilds_when_the_card_view_was_replaced()
    {
        EditorFakes.OnSta(() =>
        {
            var (editor, target, _) = EditorFakes.Open(EditorFakes.Clock());
            var before = EditorFakes.Find(editor, "Editor.Option.S.1");
            target.ReplaceView();
            editor.Refresh();
            EditorFakes.Pump();
            Assert.NotSame(before, EditorFakes.Find(editor, "Editor.Option.S.1"));
        });
    }

    [Fact]
    public void Card_buttons_show_edit_and_close_or_a_lock()
    {
        EditorFakes.OnSta(() =>
        {
            var config = new WidgetConfig { Kind = WidgetKind.Clock };
            var corner = new CardButtons();
            corner.Paint(WidgetPalette.Glass);
            corner.Update(config);
            Assert.Equal("Widget.Edit", AutomationProperties.GetAutomationId(corner.EditButton));
            Assert.Equal("Widget.Remove", AutomationProperties.GetAutomationId(corner.CloseButton));
            Assert.True(corner.EditButton.Width >= 28 || double.IsNaN(corner.EditButton.Width)); // stil 28 DIP verir
            // ⋯ sönük renkle (ikincil yazı rengi), saydamlıkla değil: ClearType'lı kartta keskin kalır.
            Assert.Same(WidgetPalette.Glass.Secondary, corner.EditButton.Foreground);
            Assert.Equal(1.0, corner.EditButton.Opacity);

            config.Locked = true;
            corner.Update(config);
            Assert.True(corner.ShowsLock);
            Assert.Equal("Widget.Unlock", AutomationProperties.GetAutomationId(corner.CloseButton));
            Assert.Equal("Kilidi aç", AutomationProperties.GetName(corner.CloseButton));
            Assert.Equal(Visibility.Visible, corner.EditButton.Visibility); // kilitliyken de düzenlenebilir

            config.HiddenParts.Add("edit");
            config.HiddenParts.Add("close");
            corner.Update(config);
            Assert.Equal(Visibility.Collapsed, corner.Visibility);
        });
    }

    [Fact]
    public void Kind_line_names_the_widget_type()
    {
        Assert.Equal("Saat", WidgetEditor.KindName(new WidgetConfig { Kind = WidgetKind.Clock }));
        Assert.Equal("Pomodoro", WidgetEditor.KindName(new WidgetConfig
        {
            Kind = WidgetKind.Clock, Variant = WidgetVariants.Timer, Timer = new TimerState { Mode = TimerModes.Pomodoro },
        }));
        Assert.Equal("Yapılacaklar", WidgetEditor.KindName(new WidgetConfig { Kind = WidgetKind.Note, NoteChecklist = true }));
        Assert.Equal("Klasör bölmesi", WidgetEditor.KindName(new WidgetConfig { Kind = WidgetKind.Fence, FolderName = "PDF" }));
        Assert.Equal("Bölme", WidgetEditor.KindName(new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.All }));
        Assert.False(WidgetEditor.HasName(new WidgetConfig { Kind = WidgetKind.Date }));
        Assert.True(WidgetEditor.HasName(new WidgetConfig { Kind = WidgetKind.Date, Variant = WidgetVariants.Month }));
        using var _ = L.Use(Lang.En);
        Assert.Equal("Folder panel", WidgetEditor.KindName(new WidgetConfig { Kind = WidgetKind.Fence, FolderName = "PDF" }));
    }
}

/// <summary>Düzenle paneli testlerinin sahte widget'ı ve yardımcıları (AccessibilityTests de kullanır).</summary>
internal static class EditorFakes
{
    public static WidgetConfig Clock() => new() { Kind = WidgetKind.Clock, Style = WidgetStyle.Glass, Accent = WidgetAccent.Violet };

    public static (WidgetEditor Editor, FakeTarget Target, FakeView View) Open(WidgetConfig config, bool usesThemeColors = true,
        List<WidgetConfig>? others = null)
    {
        var view = new FakeView(config) { UsesThemeColors = usesThemeColors };
        var target = new FakeTarget(config, view, others ?? []);
        var editor = new WidgetEditor();
        editor.Edit(target);
        return (editor, target, view);
    }

    /// <summary>Kendi panel içeriği olan görünümle açar.</summary>
    public static (WidgetEditor Editor, FakeTarget Target) Open(FakeNativeView view)
    {
        var target = new FakeTarget(view.Config, view, []);
        var editor = new WidgetEditor();
        editor.Edit(target);
        return (editor, target);
    }

    public static IEnumerable<DependencyObject> All(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var below in All(child)) yield return below;
    }

    public static List<string> Ids(DependencyObject root) =>
        All(root).Select(AutomationProperties.GetAutomationId).Where(id => !string.IsNullOrEmpty(id)).ToList();

    public static List<string> Named(DependencyObject root) =>
        All(root).Select(d => AutomationProperties.GetName(d) is { Length: > 0 } name ? name : Text(d)).Where(n => n.Length > 0).ToList();

    public static DependencyObject Find(DependencyObject root, string id) =>
        All(root).FirstOrDefault(d => AutomationProperties.GetAutomationId(d) == id) ?? throw new Xunit.Sdk.XunitException("bulunamadı: " + id);

    /// <summary>Denetimin görünen metni (içerik metin ya da TextBlock'sa).</summary>
    public static string Text(DependencyObject d) => d switch
    {
        ContentControl { Content: string s } => s,
        ContentControl { Content: TextBlock t } => t.Text,
        TextBlock t => t.Text,
        _ => "",
    };

    /// <summary>Fare tıklamasıyla aynı Click olayı.</summary>
    public static void Click(DependencyObject button)
    {
        ((UIElement)button).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Pump();
    }

    /// <summary>Bekleyen iş dağıtıcı işlerini (ör. panelin yeniden kurulması) çalıştırır.</summary>
    public static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    public static void OnSta(Action body)
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA testi zaman aşımına uğradı");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}

/// <summary>Saat benzeri sahte görünüm: menüde bir işi; panelde iki seçenek, bir tür seçimi, "Göster" ve "Daha fazla"da bir komut.</summary>
internal sealed class FakeView(WidgetConfig config) : IWidgetView
{
    public WidgetConfig Config { get; } = config;
    public bool Resizable => false;
    public bool UsesThemeColors { get; set; } = true;
    public bool Seconds { get; private set; }
    public bool? Hour12 { get; private set; }
    public string Mode { get; private set; } = "countdown";
    public int Runs { get; private set; }
    public int Copies { get; private set; }
    public int PartsChanged { get; private set; }

    public void ApplyPalette(WidgetPalette palette) { }

    public void Detach() { }

    public void AddMenuItems(WidgetMenu menu)
    {
        menu.Primary.Add(Menus.Item("Başlat", () => Runs++));
        if (!menu.ForEditor) return;
        menu.Settings.Add(Menus.Toggle("Saniyeyi göster", () => Seconds, () => Seconds = !Seconds));
        menu.Settings.Add(Menus.Choice<bool?>("Saat biçimi", () => Hour12,
            [(null, "Windows'taki gibi"), (false, "24 saat (15:30)"), (true, "12 saat (3:30 ÖS)")], v => Hour12 = v));
        menu.Settings.Add(Menus.Choice("Tür", () => Mode,
            [("countdown", "Zamanlayıcı"), ("pomodoro", "Pomodoro"), ("stopwatch", "Kronometre")], v => Mode = v, staysOpen: false));
        menu.Settings.Add(Menus.Parts(Config, [("greeting", L.N("Selam ve gün")), Menus.EditPart, Menus.ClosePart], () => PartsChanged++));
        menu.More.Add(Menus.Item("Panoya kopyala", () => Copies++));
    }
}

/// <summary>Sahte düzenleme hedefi: değişiklikleri doğrudan ayara yazar ve sayar (pencere, kayıt yok).</summary>
internal sealed class FakeTarget(WidgetConfig config, IWidgetView view, List<WidgetConfig> others) : IEditorTarget
{
    public WidgetConfig Config { get; } = config;
    public IWidgetView View { get; private set; } = view;

    /// <summary>Kartın görünümü yeniden kuruldu (WidgetWindow.RebuildView gibi).</summary>
    public void ReplaceView() => View = new FakeView(Config);
    public int Updates { get; private set; }
    public int Renames { get; private set; }
    public int Restores { get; private set; }
    public int Removes { get; private set; }
    public int Duplicates { get; private set; }

    public void Update(Action change)
    {
        change();
        Updates++;
    }

    public void SetCollapsed(bool collapsed) => Config.Collapsed = collapsed;
    public void SetLocked(bool locked) => Config.Locked = locked;
    public void SetAutoRollup(bool on) => Config.AutoRollup = on;

    public void Rename(string? title)
    {
        Config.Title = title;
        Renames++;
    }

    public void PickIcon(Action<string?> preview, Action saved) { }
    public void PickAccentColor(Action saved) { }

    public void Restore(ConfigSnapshot snapshot)
    {
        snapshot.Restore(Config);
        Restores++;
    }

    public IReadOnlyList<(string Id, LookFields Before)> ApplyLookToAll() => LookGallery.CopyToAll(Config, [Config, .. others]);
    public void UndoLookToAll(IReadOnlyList<(string Id, LookFields Before)> before) => LookGallery.Undo(before, others);
    public void Duplicate() => Duplicates++;
    public void Remove() => Removes++;
    public Box? CardBox => null;
    public NativeMethods.POINT CenterPoint => default;
}

/// <summary>
/// Kendi panel içeriği olan sahte görünüm (bölme ve kutu gibi): yongalar (biri menü açar; kaynak değişince panelde "Geri al"),
/// bir anahtar ve öğe listesi. Menüde bir işi, "Göster"de parçaları, "Daha fazla"da bir komutu var; Settings'teki seçenek panele
/// çizilmez (kendi içeriği onun yerini alır).
/// </summary>
internal sealed class FakeNativeView(WidgetConfig config) : IWidgetView
{
    public WidgetConfig Config { get; } = config;
    public bool Resizable => true;
    public string Source { get; private set; } = "Files";
    public bool Flag { get; private set; }
    public int MenusOpened { get; private set; }
    public List<TileItem> Tiles { get; } =
    [
        new TileItem { Name = "Hesap", Path = @"C:\Uygulamalar\Hesap.exe" },
        new TileItem { Name = "Not Defteri", Path = @"C:\Uygulamalar\Not.exe" },
    ];

    public void ApplyPalette(WidgetPalette palette) { }

    public void Detach() { }

    public void AddMenuItems(WidgetMenu menu)
    {
        menu.Primary.Add(Menus.Item("Ara", () => { }));
        if (!menu.ForEditor) return;
        menu.Settings.Add(Menus.Toggle("Menüden gelen seçenek", () => false, () => { }));
        menu.Settings.Add(Menus.Parts(Config, [("count", L.N("Öğe sayısı")), Menus.EditPart, Menus.ClosePart], () => { }));
        menu.More.Add(Menus.Item("Yenile", () => { }));
    }

    public bool AddEditorContent(EditorContent content)
    {
        content.Chips("Ne gösterilsin?",
            [
                new("Files", "Dosyalar", "Editor.Test.Files"), new("All", "Tümü", "Editor.Test.All"),
                new("folder", "Bir klasör seç…", "Editor.Test.Folder", Menu: () =>
                {
                    MenusOpened++;
                    return new ContextMenu();
                }),
            ],
            () => Source, id =>
            {
                var before = Source;
                Source = id;
                content.Notice("Kaynak değişti.", () => Source = before);
            }, rebuild: true);
        content.Switch("Bayrak", "Editor.Test.Flag", () => Flag, on => Flag = on);
        content.Items("Öğeler", "Boş", () => Tiles, path => Tiles.RemoveAll(t => t.Path == path), (path, delta) =>
        {
            var from = Tiles.FindIndex(t => t.Path == path);
            var item = Tiles[from];
            Tiles.RemoveAt(from);
            Tiles.Insert(from + delta, item);
        });
        return true;
    }
}
