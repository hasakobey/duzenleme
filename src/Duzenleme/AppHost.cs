using System.IO;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme;

/// <summary>Uygulama genelinde paylaşılan servisler.</summary>
public static class AppHost
{
    public static string DataDirectory { get; private set; } = "";
    public static string DesktopDirectory { get; private set; } = "";
    public static AppSettings Settings { get; private set; } = new();
    public static MoveJournal Journal { get; private set; } = null!;
    public static DesktopOrganizer Organizer { get; private set; } = null!;
    public static DesktopWatcher Watcher { get; private set; } = null!;
    public static WidgetManager Widgets { get; private set; } = null!;
    public static TrayIcon? Tray { get; set; }

    /// <summary>Ayarlar kaydedildiğinde (UI iş parçacığında) tetiklenir.</summary>
    public static event Action? SettingsChanged;

    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public static void Initialize(string? desktopOverride, string? dataOverride)
    {
        DataDirectory = dataOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Duzenleme");
        DesktopDirectory = desktopOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Directory.CreateDirectory(DataDirectory);

        Settings = JsonFile.Load(SettingsPath, () => new AppSettings());
        Journal = new MoveJournal(Path.Combine(DataDirectory, "journal.json"));
        Organizer = new DesktopOrganizer(DesktopDirectory, () => Settings, Journal);
        Watcher = new DesktopWatcher(Organizer, () => Settings.Paused);
        Widgets = new WidgetManager();
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
}
