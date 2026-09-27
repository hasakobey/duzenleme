using System.Runtime.InteropServices;
using Duzenleme.Core;

namespace Duzenleme.Tests;

public class PackagedAppTests
{
    private const string Roaming = @"C:\Users\ali\AppData\Roaming";
    private const string Local = @"C:\Users\ali\AppData\Local";
    private const string Family = "hasakobey.Duzenleme_abc123";
    private const string Cache = @"C:\Users\ali\AppData\Local\Packages\hasakobey.Duzenleme_abc123\LocalCache\Roaming";

    [Theory]
    [InlineData(@"C:\Users\ali\AppData\Roaming\Duzenleme", Cache + @"\Duzenleme")]
    [InlineData(@"c:\users\ALI\appdata\roaming\Duzenleme\yedekler\", Cache + @"\Duzenleme\yedekler")]
    [InlineData(@"C:\Users\ali\AppData\Roaming", Cache)]
    public void Roaming_paths_map_to_package_local_cache(string path, string expected) =>
        Assert.Equal(expected, PackagedApp.RedirectedRoamingPath(path, Roaming + @"\", Local, Family));

    [Theory]
    [InlineData(@"C:\Users\ali\AppData\RoamingX\Duzenleme")]
    [InlineData(@"C:\Users\ali\AppData\Local\Duzenleme")]
    [InlineData(@"D:\test\data")]
    public void Paths_outside_roaming_are_not_redirected(string path) =>
        Assert.Null(PackagedApp.RedirectedRoamingPath(path, Roaming, Local, Family));

    private static string Show(string? redirected, params string[] existing)
    {
        var set = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        return PackagedApp.DataFolderToShow(Roaming + @"\Duzenleme", redirected, set.Contains, set.Contains);
    }

    [Fact]
    public void Data_folder_prefers_redirected_copy_that_holds_settings()
    {
        var cache = Cache + @"\Duzenleme";
        // Eski sürümün ayarları gerçek konumda dursa da uygulama yönlendirilen kopyayı okur.
        Assert.Equal(cache, Show(cache, cache + @"\settings.json", Roaming + @"\Duzenleme\settings.json", cache));
    }

    [Fact]
    public void Data_folder_falls_back_to_real_appdata_when_old_settings_are_read_from_there()
    {
        var cache = Cache + @"\Duzenleme";
        // Yönlendirilen klasör (ör. yalnızca yedekler için) oluşmuş ama ayarlar hâlâ gerçek %AppData%'da.
        Assert.Equal(Roaming + @"\Duzenleme", Show(cache, Roaming + @"\Duzenleme\settings.json", cache));
    }

    [Fact]
    public void Data_folder_uses_whichever_folder_exists_before_first_save()
    {
        var cache = Cache + @"\Duzenleme";
        Assert.Equal(cache, Show(cache, cache));
        Assert.Equal(Roaming + @"\Duzenleme", Show(cache));
        Assert.Equal(Roaming + @"\Duzenleme", Show(null, cache + @"\settings.json"));
    }

    [Theory]
    [InlineData(0, StartupTaskState.Disabled)]
    [InlineData(1, StartupTaskState.DisabledByUser)]
    [InlineData(2, StartupTaskState.Enabled)]
    [InlineData(3, StartupTaskState.DisabledByPolicy)]
    [InlineData(4, StartupTaskState.EnabledByPolicy)]
    public void Startup_task_state_is_parsed(int value, StartupTaskState expected) =>
        Assert.Equal(expected, PackagedApp.ParseStartupTaskState(value));

    [Theory]
    [InlineData(5)]
    [InlineData(-1)]
    public void Unknown_startup_task_state_is_null(int value) =>
        Assert.Null(PackagedApp.ParseStartupTaskState(value));

    [Theory]
    [InlineData(StartupTaskState.Disabled, false)]
    [InlineData(StartupTaskState.Enabled, true)]
    public void App_can_toggle_startup_task_it_controls(StartupTaskState state, bool on)
    {
        var view = PackagedApp.DescribeStartupTask(state);
        Assert.True(view.ShowToggle);
        Assert.Equal(on, view.IsOn);
        Assert.Null(view.Note);
    }

    [Theory]
    [InlineData(StartupTaskState.DisabledByUser)]
    [InlineData(StartupTaskState.DisabledByPolicy)]
    [InlineData(StartupTaskState.EnabledByPolicy)]
    [InlineData(null)]
    public void Locked_or_unknown_startup_task_points_to_windows_settings(StartupTaskState? state)
    {
        // Kullanıcının Windows'taki seçimini RequestEnableAsync ezmez; ilke de uygulamadan değişmez: anahtar yerine
        // Başlangıç uygulamaları düğmesi ve nedeni.
        var view = PackagedApp.DescribeStartupTask(state);
        Assert.False(view.ShowToggle);
        Assert.False(string.IsNullOrWhiteSpace(view.Note));
    }

    [Fact]
    public async Task Test_process_is_not_packaged()
    {
        // Testler paketsiz çalışır: paketsiz yol (Run kaydı, %AppData%) değişmeden kullanılır.
        Assert.False(PackageInfo.IsPackaged);
        Assert.Null(PackageInfo.FamilyName);
        Assert.False(PackageInfo.LaunchedByStartupTask());
        Assert.Null(await StartupRegistration.GetTaskStateAsync());
        Assert.Null(await StartupRegistration.SetTaskEnabledAsync(true));
        Assert.True(Desktop.DesktopSystemIcons.CanChange);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void Startup_task_call_without_package_identity_fails_quietly(bool? enable)
    {
        // Paket kimliği olmadan StartupTask.GetAsync hata verir: WinRT yolu çökmeden ve beklemeden null dönmeli.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Null(StartupRegistration.QueryTask(enable));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
    }

    [Fact]
    public void WinRt_async_operation_result_is_read()
    {
        // Paketsiz süreçte de çalışan bir IAsyncOperation<nesne> (StorageFolder.GetFolderFromPathAsync) ile bekleme ve
        // GetResults sanal tablo yuvası doğrulanır; StartupTask aynı yolu kullanır.
        var dir = Directory.CreateTempSubdirectory("duzenleme-winrt-");
        try
        {
            var statics = WinRt.Factory<IStorageFolderStatics>("Windows.Storage.StorageFolder");
            Assert.NotNull(statics);
            using var path = new WinRt.HString(dir.FullName);
            var folder = WinRt.AwaitObject((out IntPtr operation) => statics.GetFolderFromPathAsync(path.Handle, out operation), TimeSpan.FromSeconds(10));
            try
            {
                var item = (IStorageItem)Marshal.GetObjectForIUnknown(folder);
                Marshal.ThrowExceptionForHR(item.GetName(out var name));
                try { Assert.Equal(dir.Name, Marshal.PtrToStringUni(WindowsGetStringRawBuffer(name, out _))); }
                finally { WindowsDeleteString(name); }
            }
            finally { Marshal.Release(folder); }
        }
        finally { dir.Delete(); }
    }

    [Fact]
    public void WinRt_async_error_becomes_exception()
    {
        var statics = WinRt.Factory<IStorageFolderStatics>("Windows.Storage.StorageFolder");
        Assert.NotNull(statics);
        using var path = new WinRt.HString(Path.Combine(Path.GetTempPath(), "duzenleme-yok-" + Guid.NewGuid()));
        Assert.Throws<FileNotFoundException>(() =>
            WinRt.AwaitObject((out IntPtr operation) => statics.GetFolderFromPathAsync(path.Handle, out operation), TimeSpan.FromSeconds(10)));
    }

    [DllImport("combase.dll")]
    private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    // GUID'ler ve yöntem sırası Windows.Storage.winmd'den; ilk üç yuva IInspectable'ın.
    [ComImport, Guid("08f327ff-85d5-48b9-aee9-28511e339f9f"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IStorageFolderStatics
    {
        void GetIids();
        void GetRuntimeClassName();
        void GetTrustLevel();
        [PreserveSig] int GetFolderFromPathAsync(IntPtr path, out IntPtr operation);
    }

    [ComImport, Guid("4207a996-ca2f-42f7-bde8-8b10457a7f30"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IStorageItem
    {
        void GetIids();
        void GetRuntimeClassName();
        void GetTrustLevel();
        void RenameAsyncOverloadDefaultOptions();
        void RenameAsync();
        void DeleteAsyncOverloadDefaultOptions();
        void DeleteAsync();
        void GetBasicPropertiesAsync();
        [PreserveSig] int GetName(out IntPtr name);
    }
}
