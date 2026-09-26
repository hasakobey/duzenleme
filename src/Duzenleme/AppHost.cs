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

    /// <summary>Masaüstünde görünen klasörler: kullanıcının masaüstü ve (test klasörü verilmediyse) Genel Masaüstü.</summary>
    public static IReadOnlyList<string> DesktopDirectories { get; private set; } = [];
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

    /// <summary>Taşınabilir mod istendi ama klasöre yazılamadığı için %AppData% kullanılıyor.</summary>
    public static bool PortableFallback { get; private set; }

    /// <summary>Exe'nin yanında "portable.txt" varsa ayarlar exe'nin yanındaki "data" klasöründe tutulur (USB bellekte taşınabilir).</summary>
    private static string? PortableDataDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        if (!File.Exists(Path.Combine(baseDir, "portable.txt"))) return null;
        var dir = Path.Combine(baseDir, "data");
        try
        {
            // Salt okunur bir klasörden (ör. zip içinden, CD'den) çalışıyorsa yazılabilir mi dene.
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".yazma-testi");
            File.WriteAllText(probe, "");
            // Yazabildiysek klasör kullanılabilir; deneme dosyası silinemese de (ör. virüs tarayıcısı açık tutuyor) sorun değil.
            try { File.Delete(probe); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return dir;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PortableFallback = true;
            return null;
        }
    }

    /// <summary>
    /// Masaüstü klasörü: yönlendirilmiş (OneDrive, ağ) olsa da yolu doğrulamadan alınır; boş dönerse varsayılan konum.
    /// </summary>
    private static string ResolveDesktop()
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop");
        return path;
    }

    public static void Initialize(string? desktopOverride, string? dataOverride)
    {
        var portable = dataOverride is null ? PortableDataDirectory() : null;
        IsPortable = portable is not null;
        DataDirectory = dataOverride ?? portable ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Duzenleme");
        DesktopDirectory = desktopOverride ?? ResolveDesktop();
        // Kurulan programların kısayolları çoğunlukla Genel Masaüstü'ndedir; "Kısayollar" bölmesi onları da göstersin.
        var common = desktopOverride is null
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory, Environment.SpecialFolderOption.DoNotVerify)
            : "";
        DesktopDirectories = string.IsNullOrWhiteSpace(common) || string.Equals(common, DesktopDirectory, StringComparison.OrdinalIgnoreCase)
            ? [DesktopDirectory]
            : [DesktopDirectory, common];
        Directory.CreateDirectory(DataDirectory);

        Settings = JsonFile.Load(SettingsPath, () => new AppSettings());
        BackupSettingsDaily();
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

    /// <summary>
    /// Explorer yeniden başlarsa simgeler kendiliğinden yeniden görünür; uygulama hâlâ "gizli" sanmasın.
    /// </summary>
    public static void ReconcileDesktopState()
    {
        if (!DesktopHidden || !DesktopIcons.AreVisible) return;
        DesktopHidden = false;
        Widgets.SetHidden(false);
        Settings.IconsHiddenByApp = false;
        SaveSettings();
        DesktopVisibilityChanged?.Invoke();
    }

    /// <summary>
    /// Günde bir kez ayarların (widget düzeni, notlar, kurallar) kopyası "yedekler" klasörüne alınır; son 7 gün tutulur.
    /// Bir şey ters giderse (bozuk dosya, yanlışlıkla silinen not) oradan geri dönülebilir.
    /// </summary>
    private static void BackupSettingsDaily()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var dir = Path.Combine(DataDirectory, "yedekler");
            var today = Path.Combine(dir, $"settings-{DateTime.Now:yyyy-MM-dd}.json");
            if (File.Exists(today)) return;
            Directory.CreateDirectory(dir);
            File.Copy(SettingsPath, today);
            foreach (var old in new DirectoryInfo(dir).GetFiles("settings-*.json").OrderByDescending(f => f.Name).Skip(7))
                old.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Uygulama kapanırken masaüstünü kullanıcıya gizli bırakma.</summary>
    public static void RestoreDesktopOnExit()
    {
        if (!DesktopHidden) return;
        DesktopIcons.SetVisible(true);
        Settings.IconsHiddenByApp = false;
        try { JsonFile.Save(SettingsPath, Settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
