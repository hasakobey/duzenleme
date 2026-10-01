using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Widgets;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace Duzenleme.Views;

/// <summary>Ayarlar > Gelişmiş > "Hata günlüğü" satırının durumu (arka planda okunur).</summary>
internal sealed record LogStatus(long Errors, long Bytes, int Pending);

/// <summary>
/// Yerel tanılamanın arayüz tarafı: çökme sorusu şeridi, okunamayan dosya bildirimi, "Ayrıntıları kopyala", tanılama paketi
/// (önizleme → Kaydet penceresi → zip) ve tanılama sayfası (logs\tanilama.html). Ayarlar ve anlık görüntü arayüz iş
/// parçacığında alınır; diske dokunan her iş arka planda. Hiçbir şey gönderilmez.
/// </summary>
internal static class Diagnostics
{
    /// <summary>Günlükler, raporlar ya da çökme seçimi değişti (UI iş parçacığında): Ayarlar'daki satır yenilensin.</summary>
    public static event Action? Changed;

    // Çökme raporu dosyalarına yapılan işler sırayla (önce "soruldu" işareti, sonra kullanıcının seçimi): biri ötekinin
    // dosyasını yeniden oluşturmasın.
    private static readonly object QueueGate = new();
    private static Task _queue = Task.CompletedTask;
    private static bool _packageBusy;

    private static void Queue(Action work, Action? done = null)
    {
        lock (QueueGate)
        {
            _queue = _queue.ContinueWith(_ =>
            {
                try { work(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppLog.Warn("diagnostics", "report-file-failed", ex); }
                if (Application.Current is { } app)
                    app.Dispatcher.BeginInvoke(() =>
                    {
                        done?.Invoke();
                        Changed?.Invoke();
                    });
            }, TaskScheduler.Default);
        }
    }

    /// <summary>Oturum başı kaydı ve tanılama sayfası için ortam (UI iş parçacığında: tema ve ekranlar).</summary>
    public static DiagnosticEnvironment CurrentEnvironment()
    {
        var scales = NativeMethods.MonitorScalePercents();
        return new DiagnosticEnvironment
        {
            Channel = AppHost.IsTestDesktop ? "test" : PackageInfo.IsPackaged ? "store" : AppHost.IsPortable ? "portable" : "installer",
            Monitors = scales.Count,
            Scales = string.Join(",", scales),
            Theme = ApplicationThemeManager.GetAppTheme() switch
            {
                ApplicationTheme.Dark => "dark",
                ApplicationTheme.Light => "light",
                ApplicationTheme.HighContrast => "high-contrast",
                _ => "unknown",
            },
            HighContrast = SystemParameters.HighContrast,
            Animations = Motion.Enabled,
            DataFolderSource = AppHost.DataFolderSource.ToString(),
        };
    }

    // ------------------------------------------------------------------------------------------------ çökme sorusu

    /// <summary>
    /// Önceki oturumdan kalan, henüz sorulmamış raporlar: şeritte (pencere gizliyse balonda) "Kaydet / Kaydetme / Hep kaydet".
    /// Bir kez sorulduktan sonra raporlar "soruldu" işaretlenir (balon her açılışta çıkmaz); yanıtlanmayan rapor Ayarlar >
    /// Gelişmiş > Hata günlüğü'nde durur, 14 gün sonra silinir.
    /// </summary>
    public static void AskAboutCrashes(IReadOnlyList<CrashReport> reports)
    {
        if (reports.Count == 0) return;
        var paths = reports.Select(r => r.Path).ToList();
        var text = reports.All(r => r.Kind == CrashReports.KindHang)
            ? L.F("{0} geçen sefer bir süre yanıt vermedi ve düzgün kapanmadı. Rapor bu bilgisayara kaydedilsin mi? Rapor kişisel bilgi içermez ve hiçbir yere gönderilmez.", AppInfo.Name)
            : L.F("{0} geçen sefer beklenmeyen bir hatayla kapandı. Rapor bu bilgisayara kaydedilsin mi? Rapor kişisel bilgi içermez ve hiçbir yere gönderilmez.", AppInfo.Name);
        // Bir kez sorulur (her açılışta balon çıkmasın). Pencere gizliyse balondaki soru bu oturumda bekler: pencere açılınca
        // şeritte gösterilir; yanıtlanmayan rapor Ayarlar'da durur.
        Queue(() => paths.ForEach(CrashReports.MarkAsked));
        Notice.Ask(text, NoticeKind.Warning,
            [
                new NoticeChoice(L.T("Kaydet"), () => SavePending(paths)),
                new NoticeChoice(L.T("Kaydetme"), () => DiscardPending(paths)),
                new NoticeChoice(L.T("Hep kaydet"), SaveAlways),
            ],
            trayHint: L.T("Yanıtlamak için tıkla."));
    }

    /// <summary>
    /// Bekleyen raporları kaydeder (çökme şeridi ve Ayarlar'daki "Kaydet"). Hiçbiri kaydedilemediyse (dosya kilitli ya da bu
    /// arada silinmiş) "kaydedildi" denmez.
    /// </summary>
    public static void SavePending(IReadOnlyList<string>? paths = null)
    {
        var crashes = AppHost.CrashesDirectory;
        var saved = 0;
        Queue(() =>
        {
            var now = DateTime.UtcNow;
            foreach (var path in paths ?? CrashReports.List(crashes, pending: true).Select(r => r.Path).ToList())
                if (CrashReports.Save(path, now) is not null) saved++;
            if (saved > 0) AppLog.Info("diagnostics", "crash-report-saved");
            else AppLog.Warn("diagnostics", "crash-report-save-failed");
        }, () =>
        {
            if (saved > 0)
                Notice.Show(L.T("Rapor bu bilgisayara kaydedildi. Tanılama sayfasında görebilirsin."), NoticeKind.Success,
                    L.T("Tanılama sayfasını aç"), OpenPage);
            else Notice.Show(L.T("Rapor kaydedilemedi."), NoticeKind.Warning);
        });
    }

    /// <summary>Bekleyen raporları siler (çökme şeridindeki "Kaydetme", Ayarlar'daki "Sil").</summary>
    public static void DiscardPending(IReadOnlyList<string>? paths = null)
    {
        var crashes = AppHost.CrashesDirectory;
        Queue(() =>
        {
            foreach (var path in paths ?? CrashReports.List(crashes, pending: true).Select(r => r.Path).ToList()) CrashReports.Discard(path);
        });
    }

    /// <summary>"Hep kaydet": bundan sonra sormadan kaydedilir; bekleyenler de kaydedilir.</summary>
    private static void SaveAlways()
    {
        AppHost.Settings.CrashReportMode = CrashReportModes.Always;
        AppHost.SaveSettings();
        var crashes = AppHost.CrashesDirectory;
        Queue(() => CrashReports.SaveAll(crashes, DateTime.UtcNow),
            () => Notice.Show(L.T("Bundan sonra çökme raporları sormadan kaydedilecek. Ayarlar > Gelişmiş'ten değiştirebilirsin."), NoticeKind.Info));
    }

    // ------------------------------------------------------------------------------------------------ bozuk dosya

    /// <summary>
    /// Okunamayan veri dosyası (açılışta): ne yüklendiğini doğru söyler (.bak ya da varsayılanlar) ve kopyasını Gezgin'de seçili
    /// gösterir. Kopya alınamadıysa "silinmedi" denmez; düğme veri klasörünü açar.
    /// </summary>
    public static void ShowCorruptFile(string kind, string? backup, bool usedBak)
    {
        var text = (kind, usedBak) switch
        {
            (JsonFile.KindSettings, true) => L.T("Ayar dosyası okunamadı; son sağlam kopyası yüklendi."),
            (JsonFile.KindSettings, false) => L.T("Ayar dosyası okunamadı; varsayılan ayarlarla başlandı."),
            (JsonFile.KindJournal, true) => L.T("Taşıma geçmişi okunamadı; son sağlam kopyası yüklendi."),
            (JsonFile.KindJournal, false) => L.T("Taşıma geçmişi okunamadı; geçmiş boş başladı, önceki taşımalar buradan geri alınamaz."),
            (JsonFile.KindBoxMoves, true) => L.T("Kutu kayıtları okunamadı; son sağlam kopyası yüklendi."),
            (JsonFile.KindBoxMoves, false) => L.F("Kutu kayıtları okunamadı; kutulara taşınmış öğeler {0} klasöründe duruyor.", BoxMover.Root),
            (_, true) => L.T("Bir veri dosyası okunamadı; son sağlam kopyası yüklendi."),
            _ => L.T("Bir veri dosyası okunamadı; boş başlandı."),
        };
        if (backup is not null) text += " " + L.T("Okunamayan dosya silinmedi.");
        var target = backup;
        Notice.Show(text, NoticeKind.Warning, L.T("Klasörü aç"), () => Reveal(target, AppHost.DataDirectory));
    }

    /// <summary>Dosyayı Gezgin'de seçili gösterir (yoksa klasörü açar); yol Store sürümünde arka planda çözülür.</summary>
    private static void Reveal(string? file, string folder) => Task.Run(() =>
    {
        if (file is not null && File.Exists(file)) ShellReveal.Select(AppHost.PathOnDisk(file));
        else OpenFolder(folder);
    });

    private static void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(AppHost.PathOnDisk(folder)) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AppLog.Warn("diagnostics", "open-folder-failed", ex);
            var result = FolderCreation.Explain(ex, folder);
            Notice.FolderFailed(result.Ok ? result : result with { Error = L.F("Klasör açılamadı. {0}", result.Error) });
        }
    }

    // ------------------------------------------------------------------------------------------------ ayrıntılar

    /// <summary>"Ayrıntıları kopyala": yalnızca temizlenmiş metin (sürüm, tür, ileti, yığın). Pano meşgulse söylenir.</summary>
    public static void CopyDetails(string text)
    {
        try
        {
            Clipboard.SetText(text);
            Notice.Show(L.T("Ayrıntılar panoya kopyalandı."), NoticeKind.Info);
        }
        catch (ExternalException ex)
        {
            AppLog.Info("diagnostics", "clipboard-busy", ex);
            Notice.Show(L.T("Pano şu an kullanılamıyor; birazdan yeniden dene."), NoticeKind.Warning);
        }
    }

    // ------------------------------------------------------------------------------------------------ ayarlar satırı

    /// <summary>Son 7 günün hata sayısı, günlüklerin boyutu ve bekleyen rapor sayısı (arka planda okunur).</summary>
    public static Task<LogStatus> ReadStatusAsync()
    {
        var logs = AppHost.LogsDirectory;
        return Task.Run(() =>
        {
            var since = DateTime.UtcNow.AddDays(-7);
            var errors = LogFiles.Read(logs, since).Where(e => e.Level == AppLogLevel.Error).Sum(e => (long)e.Count);
            var pending = CrashReports.List(Path.Combine(logs, CrashReports.FolderName), pending: true).Count;
            return new LogStatus(errors, LogFiles.TotalBytes(logs), pending);
        });
    }

    /// <summary>"Hata günlüğü tut": kapalıyken yeni kayıt yazılmaz (var olanlar "Günlükleri sil" ile silinir).</summary>
    public static void SetLogEnabled(bool on)
    {
        if (AppHost.Settings.ErrorLogOff == !on) return;
        AppHost.Settings.ErrorLogOff = !on;
        AppHost.SaveSettings();
        AppLog.SetPersist(on);
        Changed?.Invoke();
    }

    /// <summary>"Çökme olunca": null = her seferinde sor, "always" = sormadan kaydet, "never" = kaydetme.</summary>
    public static void SetCrashMode(string? mode)
    {
        mode = CrashReportModes.Normalize(mode);
        if (CrashReportModes.Normalize(AppHost.Settings.CrashReportMode) == mode) return;
        AppHost.Settings.CrashReportMode = mode;
        AppHost.SaveSettings();
        Changed?.Invoke();
    }

    public static void OpenLogsFolder()
    {
        var logs = AppHost.LogsDirectory;
        Task.Run(() => OpenFolder(logs));
    }

    /// <summary>"Günlükleri sil": hata günlükleri, çökme raporları ve tanılama sayfası (ayarlar, notlar, .bozuk yedekleri kalır).</summary>
    public static void DeleteLogs(Window? owner)
    {
        if (!Confirm.Ask(owner, L.T("Günlükler silinsin mi?"),
                L.T("Hata günlükleri, çökme raporları ve tanılama sayfası silinir. Ayarların ve notların etkilenmez."), L.T("Sil")))
            return;
        var logs = AppHost.LogsDirectory;
        Queue(() =>
        {
            AppLog.Channel.DeleteLogFiles();
            CrashReports.DeleteAll(Path.Combine(logs, CrashReports.FolderName));
            var page = Path.Combine(logs, DiagnosticReport.FileName);
            if (File.Exists(page)) File.Delete(page);
        }, () => Notice.Show(L.T("Günlükler silindi."), NoticeKind.Success));
    }

    // ------------------------------------------------------------------------------------------------ sayfa ve paket

    /// <summary>Tanılama sayfasının girdileri: ayarların özeti ve ortam arayüzde, günlükler ve raporlar diskten (arka planda).</summary>
    private sealed record ReportSource(DiagnosticSnapshot Snapshot, DiagnosticEnvironment Environment, bool LogOff, string Logs, string Data)
    {
        public static ReportSource Take() => new(DiagnosticSnapshot.From(AppHost.Settings), CurrentEnvironment(),
            AppHost.Settings.ErrorLogOff, AppHost.LogsDirectory, AppHost.DataDirectory);

        /// <summary>Arka planda: bekleyen günlük satırları yazılır, sonra sayfa üretilir.</summary>
        public (string Html, PerfFacts Perf) Render(DateTime now)
        {
            AppLog.FlushSync(TimeSpan.FromSeconds(1));
            var perf = Perf();
            var crashes = Path.Combine(Logs, CrashReports.FolderName);
            var input = new ReportInput
            {
                Snapshot = Snapshot,
                Environment = Environment,
                Perf = perf,
                Lines = LogFiles.Read(Logs, now.AddDays(-7)),
                Crashes = CrashReports.List(crashes, pending: false),
                PendingCrashes = CrashReports.List(crashes, pending: true).Count,
                LogFileCount = LogFiles.List(Logs).Count,
                LogBytes = LogFiles.TotalBytes(Logs),
                CorruptFiles = CountCorrupt(Data),
                LogOff = LogOff,
                NowUtc = now,
            };
            return (DiagnosticReport.Render(input), perf);
        }
    }

    private static PerfFacts Perf()
    {
        var counters = new SortedDictionary<string, long>(AppLog.Counters.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal);
        if (AppHost.Store is { } store) counters["settings.writes"] = store.File.WriteCount;
        if (AppHost.Journal is { } journal) counters["journal.writes"] = journal.WriteCount;
        return PerfFacts.Capture(AppHost.StartupMs, counters);
    }

    private static int CountCorrupt(string data)
    {
        try
        {
            return Directory.Exists(data) ? Directory.EnumerateFiles(data, "*.bozuk-*", SearchOption.TopDirectoryOnly).Count() : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }

    /// <summary>"Tanılama sayfasını aç": logs\tanilama.html arka planda yazılır ve tarayıcıda (Store sürümünde diskteki yeriyle) açılır.</summary>
    public static async void OpenPage()
    {
        var source = ReportSource.Take();
        var file = Path.Combine(source.Logs, DiagnosticReport.FileName);
        try
        {
            var shown = await Task.Run(() =>
            {
                var (html, _) = source.Render(DateTime.UtcNow);
                Directory.CreateDirectory(source.Logs);
                File.WriteAllText(file, html, new UTF8Encoding(false));
                return AppHost.PathOnDisk(file);
            });
            Browser.Open(shown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("diagnostics", "page-write-failed", ex);
            var result = FolderCreation.Explain(ex, source.Logs);
            Notice.FolderFailed(result with { Error = L.F("Tanılama sayfası oluşturulamadı. {0}", result.Error) });
        }
    }

    /// <summary>
    /// "Tanılama paketi oluştur": içerik önce gösterilir (girecekler / girmeyecekler), sonra Kaydet penceresi Belgeler'de açılır
    /// (masaüstünde "Arşivler" kuralı zip'i taşırdı), zip arka planda yazılır ve "Klasörde göster" sunulur. Hiçbir şey gönderilmez.
    /// </summary>
    public static async void CreatePackage(Window? owner)
    {
        if (_packageBusy) return;
        _packageBusy = true;
        try
        {
            var source = ReportSource.Take();
            var now = DateTime.UtcNow;
            var package = await Task.Run(() =>
            {
                var (html, perf) = source.Render(now);
                return DiagnosticPackage.Plan(source.Logs, now, html, source.Snapshot.ToJson(source.Environment, perf, now));
            });
            // İçerik kaydetmeden önce dosya dosya gösterilir (etiket ve boyut; aynı günün günlükleri tek satır).
            var contents = string.Join("\n", package.Preview().Select(p => "• " + p.Label + " (" + MeasureText.Bytes(p.Size) + ")"));
            var message = L.F("Pakete girecekler:\n{0}\nToplam: {1}\n\nGirmeyecekler: dosya ve klasör adların, notların, kuralların, API anahtarın. GitHub sorun kayıtları herkese açıktır.",
                contents, MeasureText.Bytes(package.TotalSize));
            if (!Confirm.Ask(owner, L.T("Tanılama paketi oluşturulsun mu?"), message, L.T("Kaydet…"), danger: false)) return;

            var dialog = new SaveFileDialog
            {
                Title = L.T("Tanılama paketini kaydet"),
                FileName = DiagnosticPackage.SuggestedFileName(DateTime.Now),
                DefaultExt = ".zip",
                AddExtension = true,
                OverwritePrompt = true,
                Filter = L.T("Zip arşivi") + " (*.zip)|*.zip",
                // Belgeler: masaüstünde "Arşivler" kuralı zip'i hemen taşırdı; AppData Store sürümünde başka uygulamalara görünmez.
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            };
            if (dialog.ShowDialog(owner is { IsVisible: true } ? owner : null) != true) return;
            var path = dialog.FileName;
            try
            {
                await Task.Run(() => Write(package, path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                AppLog.Warn("diagnostics", "package-save-failed", ex);
                var result = FolderCreation.Explain(ex, Path.GetDirectoryName(path) ?? path);
                Notice.FolderFailed(result with { Error = L.F("Tanılama paketi kaydedilemedi. {0}", result.Error) });
                return;
            }
            AppLog.Info("diagnostics", "package-saved");
            Notice.Show(L.T("Tanılama paketi kaydedildi."), NoticeKind.Success, L.T("Klasörde göster"), () => Reveal(path, Path.GetDirectoryName(path) ?? path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("diagnostics", "package-plan-failed", ex);
            Notice.Show(L.T("Tanılama paketi hazırlanamadı. Birazdan yeniden dene."), NoticeKind.Warning);
        }
        finally
        {
            _packageBusy = false;
        }
    }

    /// <summary>Zip'i yazar; yarıda kalırsa yarım dosya bırakılmaz.</summary>
    private static void Write(DiagnosticPackage package, string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            package.Write(stream);
        }
        catch
        {
            try { File.Delete(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }
}
