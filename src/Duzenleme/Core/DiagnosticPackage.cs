using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Duzenleme.Core;

/// <summary>Paketin bir öğesi: zip içindeki ad, kullanıcıya gösterilen etiket, boyut ve kaynağı (dosya ya da bellekteki içerik).</summary>
public sealed record PackageEntry(string Name, string Label, long Size, string? SourcePath = null, byte[]? Content = null);

/// <summary>
/// Kullanıcının kendi başlattığı tanılama paketi (zip). İçerik koddaki beyaz listeyle belirlenir: rapor.html, ozet.json,
/// iki dilli README.txt, son 7 günün <c>logs\nestdesk-*.jsonl</c> dosyaları ve yalnızca kaydedilmiş <c>crashes\crash-*.json</c>
/// raporları. Ayarlar, .bak, yedekler, .bozuk, geçmiş (journal), box-moves, simgeler, yapay zekâ simgeleri ve bekleyen çökme
/// raporları ASLA girmez: veri klasörü taranmaz, yalnızca günlük klasöründe bu adlar aranır. Önce <see cref="Plan"/> ile
/// içerik gösterilir, sonra <see cref="Write"/> arka planda yazar. Hiçbir şey gönderilmez.
/// </summary>
public sealed class DiagnosticPackage
{
    public const string ReportName = "rapor.html";
    public const string SummaryName = "ozet.json";
    public const string ReadmeName = "README.txt";
    public const int LogDays = 7;

    // İki dilli: paketi açan geliştirici ve kullanıcı içindir, arayüz dilinden bağımsızdır. {0} = uygulamanın adı.
    private const string Readme = /* l10n: çevrilmez */ """
        {0} tanılama paketi / {0} diagnostic package

        TR
        Bu paketi {0} Ayarlar > Yardım bölümünden sen oluşturdun; hiçbir yere kendiliğinden gönderilmedi.
        İçinde dosya ve klasör adların, notların, kuralların, kısayol tuşların ve API anahtarın yok.
        GitHub sorun kayıtları herkese açıktır: eklemeden önce dosyalara bakabilirsin.
          rapor.html    tanılama sayfası (tarayıcıda açılır, betik içermez)
          ozet.json     ayarların özeti: yalnızca açık/kapalı seçenekler ve sayılar
          logs/         son 7 günün hata günlükleri (temizlenmiş)
          crashes/      kaydettiğin çökme raporları

        EN
        You created this package from {0} Settings > Help; nothing was sent anywhere automatically.
        It doesn't contain your file or folder names, notes, rules, shortcut keys or API key.
        GitHub issues are public: you can look through the files before attaching them.
          rapor.html    diagnostics page (opens in a browser, contains no script)
          ozet.json     settings summary: only on/off options and counts
          logs/         error logs from the last 7 days (scrubbed)
          crashes/      crash reports you saved
        """;

    private DiagnosticPackage(IReadOnlyList<PackageEntry> entries) => Entries = entries;

    public IReadOnlyList<PackageEntry> Entries { get; }

    public long TotalSize => Entries.Sum(e => e.Size);

    /// <summary>
    /// Kaydetmeden önce gösterilecek içerik listesi: öğe başına etiket ve boyut, plandaki sırayla. Aynı etiketli öğeler (aynı
    /// günün döndürülmüş günlük dosyaları) tek satırda toplanır.
    /// </summary>
    public IReadOnlyList<(string Label, long Size)> Preview() =>
        Entries.GroupBy(e => e.Label, StringComparer.Ordinal).Select(g => (g.Key, g.Sum(e => e.Size))).ToList();

    /// <summary>Kaydetme penceresinin önerdiği ad (yerel saat; masaüstü kuralı taşımasın diye varsayılan klasör Belgeler).</summary>
    public static string SuggestedFileName(DateTime localNow) =>
        $"{AppInfo.Name}-diagnostics-{localNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.zip";

    /// <summary>
    /// Paketin içeriği. <paramref name="logsDirectory"/> = <c>&lt;veri&gt;\logs</c>; yalnızca orada ve <c>crashes</c> alt klasöründe,
    /// yalnızca beyaz listedeki adlar aranır. Arka planda çağrılır (diski okur).
    /// </summary>
    public static DiagnosticPackage Plan(string logsDirectory, DateTime utcNow, string reportHtml, byte[] summaryJson)
    {
        var entries = new List<PackageEntry>();
        var report = Encoding.UTF8.GetBytes(reportHtml);
        entries.Add(new PackageEntry(ReportName, L.T("Tanılama sayfası"), report.Length, Content: report));
        entries.Add(new PackageEntry(SummaryName, L.T("Ayarların özeti: yalnızca açık/kapalı seçenekler ve sayılar"), summaryJson.Length, Content: summaryJson));
        var readme = Encoding.UTF8.GetBytes(string.Format(CultureInfo.InvariantCulture, Readme, AppInfo.Name).ReplaceLineEndings("\r\n"));
        entries.Add(new PackageEntry(ReadmeName, L.T("Beni oku (iki dilli)"), readme.Length, Content: readme));

        var oldest = LogFiles.Day(utcNow.AddDays(-(LogDays - 1)));
        foreach (var file in LogFiles.List(logsDirectory))
        {
            var name = Path.GetFileName(file);
            if (!LogFiles.TryParse(name, out var day, out _) || string.CompareOrdinal(day, oldest) < 0) continue;
            if (Length(file) is not { } size) continue;
            entries.Add(new PackageEntry("logs/" + name, L.F("Hata günlüğü · {0}", DayLabel(day)), size, SourcePath: file));
        }

        foreach (var crash in CrashReports.List(Path.Combine(logsDirectory, CrashReports.FolderName), pending: false))
        {
            var name = Path.GetFileName(crash.Path);
            if (!name.StartsWith(CrashReports.SavedPrefix, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            if (Length(crash.Path) is not { } size) continue;
            var when = DateTime.SpecifyKind(crash.Utc, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            entries.Add(new PackageEntry("crashes/" + name, L.F("Kaydedilmiş çökme raporu · {0}", when), size, SourcePath: crash.Path));
        }
        return new DiagnosticPackage(entries);
    }

    private static string DayLabel(string day) =>
        DateTime.TryParseExact(day, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : day;

    private static long? Length(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Zip'i yazar (arka planda). Plandan sonra silinen dosya atlanır. Zip içindeki adlar yalnızca plandakilerdir (kök yol, "..",
    /// başka klasör yok).
    /// </summary>
    public void Write(Stream output)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var entry in Entries)
        {
            if (entry.Content is { } content)
            {
                var item = zip.CreateEntry(entry.Name, CompressionLevel.Optimal);
                using var stream = item.Open();
                stream.Write(content);
                continue;
            }
            if (entry.SourcePath is not { } path) continue;
            FileStream source;
            try { source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { continue; }
            using (source)
            {
                var item = zip.CreateEntry(entry.Name, CompressionLevel.Optimal);
                using var stream = item.Open();
                source.CopyTo(stream);
            }
        }
    }
}
