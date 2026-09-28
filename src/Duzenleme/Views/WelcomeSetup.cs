using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using Duzenleme.Core;
using Duzenleme.Widgets;

namespace Duzenleme.Views;

/// <summary>
/// Karşılamadaki seçimleri uygular ("Bitti"). Karşılama penceresi önce kapanır, sonra bu çalışır. Sıra: bölmeler, klasörler
/// ve kurallar, araçlar, taşıma, Windows ile başlatma. Bölmeler klasörlerden önce eklenir: 1. adımda gösterilen bölme planı
/// sonradan açılan klasörlerle büyümesin.
/// </summary>
internal static class WelcomeSetup
{
    /// <param name="near">Karşılama penceresinin ortası (fiziksel piksel): yeni bölmeler ve araçlar o ekrana yerleşir.</param>
    public static void Apply(WelcomeChoices c, NativeMethods.POINT near)
    {
        var watch = Stopwatch.StartNew();
        var settings = AppHost.Settings;

        // 0. Önce "tamamlandı" yazılır: yarıda çökerse yarım kurulumun üstüne karşılama yeniden açılmaz.
        settings.FirstRunDone = true;
        settings.RenameNoticeShown = true;
        AppHost.SaveSettings();

        // 1. Bölmeler.
        var widgetsBefore = settings.Widgets.Count;
        if (c.Fences == true)
        {
            AppHost.Widgets.AddStarterFences(near);
            if (c.IconsOnlyInFences != settings.FencesReplaceIcons)
            {
                // Eksik tür bölmesi kaldıysa (ör. yalnızca "Tüm masaüstü" silinmişse) o da bu ekrana eklenir.
                if (c.IconsOnlyInFences) AppHost.Widgets.EnsureDesktopCoverage(near);
                AppHost.SetFencesManageDesktop(c.IconsOnlyInFences);
            }
        }
        var fencesAdded = settings.Widgets.Count - widgetsBefore;

        // 2. Klasörler ve kurallar (kural listesi yerinde değiştirilmez: izleyici arka planda okur).
        var failed = new List<string>();
        string? firstError = null;
        if (c.AutoMove == true)
        {
            var existing = ExistingFolders();
            // "Klasör yoksa oluştur" açıkken seçilmeyen kurallar kapanır ve klasörler şimdi açılmaz (önizlemeyle aynı hesap).
            var createMissing = settings.CreateMissingFolders;
            var rules = Onboarding.ApplyFolderSelection(settings.Rules, c.MoveFolders, existing, createMissing);
            settings.Rules = rules;
            foreach (var name in Onboarding.FoldersToCreate(rules, c.MoveFolders, existing, createMissing))
            {
                var path = Path.Combine(AppHost.DesktopDirectory, name);
                // Klasörü biz açıyoruz: "simge ver" balonu çıkmasın.
                AppHost.MarkQuietFolder(path);
                try { Directory.CreateDirectory(path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    AppHost.ConsumeQuietFolder(path);
                    DebugLog.Write($"karşılama: klasör oluşturulamadı {name}: {ex.Message}");
                    failed.Add(name);
                    firstError ??= ex.Message;
                }
            }
            AppHost.SaveSettings();
        }

        // 3. Araçlar: masaüstünde aynı türden olan eklenmez (karşılama widget kaldırmaz, ikincisini de eklemez).
        var toolsBefore = settings.Widgets.Count;
        foreach (var choice in WidgetCatalog.Tools)
        {
            if (!c.Tools.Contains(choice.Key)) continue;
            if (choice.Matches is { } matches && settings.Widgets.Any(matches)) continue;
            WidgetCatalog.Invoke(choice with { FocusAfterAdd = false }, near);
        }
        var toolsAdded = settings.Widgets.Count - toolsBefore;

        // 4. Taşıma: klasörler hazır olduktan sonra açılır.
        switch (c.AutoMove)
        {
            case true when settings.Paused: AppHost.SetPaused(false); break;   // kaydeder ve masaüstünü düzenler
            case true: AppHost.OrganizeNowInBackground(); break;
            case false when !settings.Paused: AppHost.SetPaused(true); break;
        }

        // 5. Windows ile başlatma (pencere yalnızca kullanıcı değiştirdiyse değer verir). Test örneği gerçek kaydı değiştirmez.
        if (c.StartWithWindows is { } start && !AppHost.IsTestDesktop)
        {
            if (PackageInfo.IsPackaged) SetStartupTask(start);
            else SetRunValue(start);
        }

        // 6. Açılamayan klasörler.
        if (failed.Count > 0)
            AppHost.Tray?.Notify("Bazı klasörler oluşturulamadı", $"{L.Join(failed)}: {firstError}");

        DebugLog.Write($"karşılama uygulandı: bölme {fencesAdded}, araç {toolsAdded}, taşıma {Describe(c.AutoMove)}, {watch.ElapsedMilliseconds} ms");

        // 7. Masaüstüne bir şey eklendiyse ana pencere açılmaz: yeni widget'lar birkaç saniye öne gelir, kullanıcı onları görsün.
        if (fencesAdded + toolsAdded == 0) (Application.Current as App)?.ShowMainWindow();
    }

    private static string Describe(bool? autoMove) => autoMove switch
    {
        true => "açık",
        false => "kapalı",
        null => "değişmedi",
    };

    private static List<string> ExistingFolders()
    {
        try { return AppHost.Organizer.ExistingFolders().ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            DebugLog.Write($"karşılama: masaüstü klasörleri okunamadı: {ex.Message}");
            return [];
        }
    }

    private static void SetRunValue(bool on)
    {
        try { StartupRegistration.Set(on); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            DebugLog.Write($"karşılama: başlangıç kaydı yazılamadı: {ex.Message}");
            NotifyStartupFailed(null);
        }
    }

    /// <summary>
    /// Store sürümü: başlangıç görevini arka planda açar/kapatır; Windows izin vermezse (kullanıcı Windows'tan kapatmış,
    /// ilke) nedenini arayüz iş parçacığında balonla söyler.
    /// </summary>
    private static void SetStartupTask(bool on)
    {
        var dispatcher = Application.Current.Dispatcher;
        StartupRegistration.SetTaskEnabledAsync(on).ContinueWith(t =>
        {
            var view = PackagedApp.DescribeStartupTask(t.IsCompletedSuccessfully ? t.Result : null);
            if (view.IsOn != on) dispatcher.BeginInvoke(() => NotifyStartupFailed(view.Note));
        }, TaskScheduler.Default);
    }

    private static void NotifyStartupFailed(string? note) =>
        AppHost.Tray?.Notify("Windows ile başlatma değiştirilemedi",
            note ?? "Ayarı Windows'un Başlangıç uygulamaları sayfasından değiştirebilirsin.",
            OpenStartupSettings);

    /// <summary>Windows'un Başlangıç uygulamaları ayar sayfasını açar (balon ve karşılamadaki düğme).</summary>
    internal static void OpenStartupSettings()
    {
        try { Process.Start(new ProcessStartInfo(StartupRegistration.StartupAppsSettingsUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            DebugLog.Write($"Başlangıç ayarları açılamadı: {ex.Message}");
        }
    }
}
