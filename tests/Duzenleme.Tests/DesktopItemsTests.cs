using Duzenleme.Core;

namespace Duzenleme.Tests;

public class DesktopItemsTests
{
    [Fact]
    public void Splits_desktop_into_folders_shortcuts_and_files()
    {
        using var t = new TempDesktop();
        t.Folder("Oyunlarım");
        t.File("Chrome.lnk");
        t.File("site.url");
        t.File("kurulum.exe");
        t.File("rapor.pdf");
        t.File("not.txt");

        var items = new DirectoryInfo(t.Desktop).EnumerateFileSystemInfos().ToList();
        string[] Names(DesktopFilter f) => items.Where(i => DesktopItems.Matches(f, i)).Select(i => i.Name).Order().ToArray();

        Assert.Equal(["Oyunlarım"], Names(DesktopFilter.Folders));
        Assert.Equal(["Chrome.lnk", "kurulum.exe", "site.url"], Names(DesktopFilter.Shortcuts));
        Assert.Equal(["not.txt", "rapor.pdf"], Names(DesktopFilter.Files));
        Assert.Equal(6, Names(DesktopFilter.All).Length);
        Assert.Empty(Names(DesktopFilter.None));
    }

    [Fact]
    public void Hidden_items_and_desktop_ini_are_never_shown()
    {
        using var t = new TempDesktop();
        var ini = t.File("desktop.ini");
        var hidden = t.File("gizli.txt");
        File.SetAttributes(hidden, FileAttributes.Hidden);

        var items = new DirectoryInfo(t.Desktop).EnumerateFileSystemInfos().ToList();
        Assert.DoesNotContain(items, i => DesktopItems.Matches(DesktopFilter.All, i));
        Assert.True(File.Exists(ini));
    }

    [Fact]
    public void Old_settings_without_new_fields_get_sensible_defaults()
    {
        var config = System.Text.Json.JsonSerializer.Deserialize<WidgetConfig>(
            """{ "Kind": "Fence", "FolderName": "PDF", "IconSize": "Large" }""", JsonFile.Options)!;

        Assert.Equal(DesktopFilter.None, config.Filter);
        Assert.Equal(IconSize.Large, config.IconSize);
        Assert.Equal(TileAlign.Left, config.Align);
        Assert.Equal(TileSpacing.Normal, config.Spacing);
        Assert.False(config.HideLabels);
        Assert.True(config.Shadow);
        Assert.Equal(CornerStyle.Round, config.Corners);
        Assert.False(config.AutoRollup);
        Assert.False(config.ShowPreviews);
    }

    [Fact]
    public void Old_hotkey_settings_get_the_new_peek_hotkey()
    {
        var hotkeys = System.Text.Json.JsonSerializer.Deserialize<HotkeySettings>(
            """{ "ToggleDesktop": "Ctrl+Alt+H" }""", JsonFile.Options)!;
        Assert.Equal("Ctrl+Alt+W", hotkeys.Get(HotkeyAction.PeekWidgets));
        hotkeys.Set(HotkeyAction.PeekWidgets, "Ctrl+Alt+Q");
        Assert.Equal("Ctrl+Alt+Q", hotkeys.PeekWidgets);
        Assert.Equal("Ctrl+Alt+N", hotkeys.NewNote);
    }
}
