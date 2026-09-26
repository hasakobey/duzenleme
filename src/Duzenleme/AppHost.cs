using System.IO;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Widgets;

namespace Duzenleme;

/// <summary>Uygulama genelinde paylaşılan servisler.</summary>
public static class AppHost
{
    public static string DataDirectory { get; private set; } = "";
    public static string DesktopDirectory { get; private set; } = "";
    public static bool IsPortable { get; private set; }
    public static AppSettings Settings { get; private set; } = new();
    public static MoveJournal Journal { get; private set; } = null!;
    public static DesktopOrganizer Organizer { get; private set; } = null!;
    public static DesktopWatcher Watcher { get; private set; } = null!;
    public static WidgetManager Widgets { get; private set; } = null!;
    public static TrayIcon? Tray { get; set; }
    public static HotkeyManager? Hotkeys { get; set; }
    public static DesktopDoubleClick? DoubleClick { get; set; }

    /// <summary>Ayarlar kaydedildiğinde (UI iş parçacığında) tetiklenir.</summary>
    public static event Action? SettingsChanged;

    /// <summary>Masaüstü simgeleri gizlenip gösterildiğinde tetiklenir.</summary>
    public static event Action? DesktopVisibilityChanged;

    public static bool DesktopHidden { get; private set; }

    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    /// <summary>Exe'nin yanında "portable.txt" varsa ayarlar exe'nin yanındaki "data" klasöründe tutulur (USB bellekte taşınabilir).</summary>
    private static string? PortableDataDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        return File.Exists(Path.Combine(baseDir, "portable.txt")) ? Path.Combine(baseDir, "data") : null;
    }

    public static void Initialize(string? desktopOverride, string? dataOverride)
    {
        var portable = dataOverride is null ? PortableDataDirectory() : null;
        IsPortable = portable is not null;
        DataDirectory = dataOverride ?? portable ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Duzenleme");
        DesktopDirectory = desktopOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Directory.CreateDirectory(DataDirectory);

        Settings = JsonFile.Load(SettingsPath, () => new AppSettings());
        Journal = new MoveJournal(Path.Combine(DataDirectory, "journal.json"));
        Organizer = new DesktopOrganizer(DesktopDirectory, () => Settings, Journal);
        Watcher = new DesktopWatcher(Organizer, () => Settings.Paused);
        Widgets = new WidgetManager();

        // Önceki oturum simgeleri gizli bırakarak kapandıysa (ör. çökme) geri aç.
        if (Settings.IconsHiddenByApp)
        {
            DesktopIcons.SetVisible(true);
            Settings.IconsHiddenByApp = false;
            SaveSettings();
        }
    }

    public static void SaveSettings()
    {
        JsonFile.Save(SettingsPath, Settings);
        SettingsChanged?.Invoke();
    }

    public static void SetPaused(bool paused)
    {
        Settings.Paused = paused;
        SaveSettings();
        if (!paused) OrganizeNowInBackground();
    }

    public static void OrganizeNowInBackground() =>
        Task.Run(() => Organizer.OrganizeAll());

    /// <summary>Masaüstü simgelerini (ve ayara göre widget'ları) gizler ya da gösterir.</summary>
    public static void ToggleDesktop() => SetDesktopHidden(!DesktopHidden);

    public static void SetDesktopHidden(bool hidden)
    {
        DesktopHidden = hidden;
        DesktopIcons.SetVisible(!hidden);
        Widgets.SetHidden(hidden && Settings.HideWidgetsWithIcons);
        Settings.IconsHiddenByApp = hidden;
        SaveSettings();
        DesktopVisibilityChanged?.Invoke();
    }

    public static void ApplyDoubleClickSetting()
    {
        if (Settings.DoubleClickHidesDesktop) DoubleClick?.Enable();
        else DoubleClick?.Disable();
    }

    /// <summary>Uygulama kapanırken masaüstünü kullanıcıya gizli bırakma.</summary>
    public static void RestoreDesktopOnExit()
    {
        if (!DesktopHidden) return;
        DesktopIcons.SetVisible(true);
        Settings.IconsHiddenByApp = false;
        JsonFile.Save(SettingsPath, Settings);
    }
}
