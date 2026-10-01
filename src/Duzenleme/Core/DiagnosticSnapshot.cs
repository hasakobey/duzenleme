using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Duzenleme.Core;

/// <summary>
/// Ortam bilgisi (tanılama sayfası, ozet.json ve oturum başı günlük kaydı): yalnızca uygulamanın kendi değerleri. Windows
/// derlemesi yalnızca yazılır; hiçbir davranış ona bağlanmaz (WACK sürüm denetimi).
/// </summary>
public sealed record DiagnosticEnvironment
{
    public string Version { get; init; } = AppInfo.Version;
    /// <summary>"store", "installer", "portable" ya da "test".</summary>
    public string Channel { get; init; } = "";
    public string Architecture { get; init; } = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
    public string OsBuild { get; init; } = Environment.OSVersion.Version.ToString();
    public string Runtime { get; init; } = Environment.Version.ToString();
    /// <summary>Arayüz dili ("tr", "en").</summary>
    public string Language { get; init; } = L.Current == Lang.En ? "en" : "tr";
    public int Monitors { get; init; }
    /// <summary>Ekran ölçekleri yüzde olarak, virgülle ("100,125").</summary>
    public string Scales { get; init; } = "";
    /// <summary>"light" ya da "dark".</summary>
    public string Theme { get; init; } = "";
    public bool HighContrast { get; init; }
    public bool Animations { get; init; }
    /// <summary><c>AppHost.DataFolderSource</c> (veri klasörünün nereden geldiği).</summary>
    public string DataFolderSource { get; init; } = "";

    public IReadOnlyDictionary<string, string> ToFacts() => new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["version"] = Version,
        ["channel"] = Channel,
        ["arch"] = Architecture,
        ["os"] = OsBuild,
        ["runtime"] = Runtime,
        ["lang"] = Language,
        ["monitors"] = Monitors.ToString(CultureInfo.InvariantCulture),
        ["scales"] = Scales,
        ["theme"] = Theme,
        ["highContrast"] = HighContrast ? "true" : "false",
        ["animations"] = Animations ? "true" : "false",
        ["dataFolder"] = DataFolderSource,
    };
}

/// <summary>Sürecin anlık başarım ölçüleri (tanılama sayfasının "Başarım" bölümü). Herhangi bir iş parçacığından alınabilir.</summary>
public sealed record PerfFacts
{
    public long? StartupMs { get; init; }
    public long PrivateBytes { get; init; }
    public long WorkingSet { get; init; }
    public int Handles { get; init; }
    public int GdiObjects { get; init; }
    public int UserObjects { get; init; }
    public int Threads { get; init; }
    public int Gen0 { get; init; }
    public int Gen1 { get; init; }
    public int Gen2 { get; init; }
    /// <summary>Bellekteki sayaçlar (AppLog.Counters: yazma sayıları, takılmalar, bastırılan tekrarlar…).</summary>
    public IReadOnlyDictionary<string, long> Counters { get; init; } = new Dictionary<string, long>();

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);

    public static PerfFacts Capture(long? startupMs, IReadOnlyDictionary<string, long>? counters)
    {
        var facts = new PerfFacts
        {
            StartupMs = startupMs,
            Gen0 = GC.CollectionCount(0),
            Gen1 = GC.CollectionCount(1),
            Gen2 = GC.CollectionCount(2),
            Counters = counters ?? new Dictionary<string, long>(),
        };
        try
        {
            using var process = Process.GetCurrentProcess();
            facts = facts with
            {
                PrivateBytes = process.PrivateMemorySize64,
                WorkingSet = process.WorkingSet64,
                Handles = process.HandleCount,
                Threads = process.Threads.Count,
                GdiObjects = (int)GetGuiResources(process.Handle, 0),
                UserObjects = (int)GetGuiResources(process.Handle, 1),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or EntryPointNotFoundException or DllNotFoundException)
        {
        }
        return facts;
    }

    public IReadOnlyList<(string Key, string Value)> Rows()
    {
        static string Mb(long bytes) => (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        var rows = new List<(string, string)>();
        if (StartupMs is { } startup) rows.Add(("startupMs", startup.ToString(CultureInfo.InvariantCulture)));
        rows.Add(("privateBytes", Mb(PrivateBytes)));
        rows.Add(("workingSet", Mb(WorkingSet)));
        rows.Add(("handles", Handles.ToString(CultureInfo.InvariantCulture)));
        rows.Add(("gdiObjects", GdiObjects.ToString(CultureInfo.InvariantCulture)));
        rows.Add(("userObjects", UserObjects.ToString(CultureInfo.InvariantCulture)));
        rows.Add(("threads", Threads.ToString(CultureInfo.InvariantCulture)));
        rows.Add(("gc", $"{Gen0}/{Gen1}/{Gen2}"));
        foreach (var (key, value) in Counters) rows.Add((key, value.ToString(CultureInfo.InvariantCulture)));
        return rows;
    }
}

/// <summary>Widget tablosunun bir satırı: tür + alt tür başına sayılar (ad, başlık, not, yol YOK).</summary>
public sealed record WidgetRow(string Kind, string Type, int Count, int Locked, string Styles, double ScaleMin, double ScaleMax,
    int Tabs, int Items);

/// <summary>
/// Ayarların kişisel bilgi içermeyen özeti (ozet.json ve tanılama sayfası): açık/kapalı seçenekler, uygulamanın kendi
/// seçimleri (bilinen değerlere indirgenir), sayılar ve widget türleri tablosu. Hiçbir ad, başlık, not, yol, kural deseni,
/// kısayol tuşu ya da API anahtarı girmez (anahtar yalnızca var/yok). UI iş parçacığında alınır (ayarlar orada değişir),
/// yazma arka planda yapılır.
/// </summary>
public sealed class DiagnosticSnapshot
{
    public required IReadOnlyDictionary<string, bool> Flags { get; init; }
    public required IReadOnlyDictionary<string, string> Choices { get; init; }
    public required IReadOnlyDictionary<string, long> Counts { get; init; }
    public required IReadOnlyList<WidgetRow> Widgets { get; init; }

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static DiagnosticSnapshot From(AppSettings s)
    {
        var flags = new SortedDictionary<string, bool>(StringComparer.Ordinal);
        var choices = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);

        // Açık/kapalı ayarlar ve sayılar: bool ve int kişisel bilgi taşıyamaz (yeni sürümün bool'ları da kendiliğinden girer).
        foreach (var property in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            var type = property.PropertyType;
            if (type == typeof(bool)) flags[property.Name] = (bool)property.GetValue(s)!;
            else if (type == typeof(int)) counts[property.Name] = (int)property.GetValue(s)!;
            else if (type.IsEnum) choices[property.Name] = property.GetValue(s)!.ToString()!;
        }
        flags["AiKeyPresent"] = !string.IsNullOrEmpty(s.AiKeyProtected);

        // Metin ayarları yalnızca beyaz listeden ve bilinen değerlere indirgenerek.
        choices[nameof(AppSettings.Language)] = s.Language?.Trim().ToLowerInvariant() switch
        {
            null or "" => "system",
            "tr" => "tr",
            "en" => "en",
            _ => "other",
        };
        choices[nameof(AppSettings.DoubleClickAction)] = DesktopState.DoubleClickChoice(s.DoubleClickAction, s.DoubleClickHidesDesktop);
        choices[nameof(AppSettings.NewWidgetPlacement)] = s.NewWidgetPlacement?.Trim().ToLowerInvariant() switch
        {
            null or "" => "default",
            PlaceModes.Cursor => PlaceModes.Cursor,
            PlaceModes.Center => PlaceModes.Center,
            PlaceModes.Corner => PlaceModes.Corner,
            _ => "other",
        };
        choices[nameof(AppSettings.CrashReportMode)] = CrashReportModes.Normalize(s.CrashReportMode) ?? "ask";

        var rules = s.Rules ?? [];
        counts["Rules"] = rules.Count;
        counts["RulesEnabled"] = rules.Count(r => r.Enabled);
        counts["RuleExtensions"] = rules.Sum(r => r.Extensions?.Count ?? 0);
        counts["Widgets"] = s.Widgets?.Count ?? 0;
        counts["Layouts"] = s.Layouts?.Count ?? 0;
        counts["IconFolders"] = s.IconFolders?.Count ?? 0;
        counts["HotkeysAssigned"] = s.Hotkeys is null ? 0 : typeof(HotkeySettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0)
            .Count(p => !string.IsNullOrWhiteSpace(p.GetValue(s.Hotkeys) as string));
        counts["UnknownSettings"] = s.Extra?.Count ?? 0;

        return new DiagnosticSnapshot
        {
            Flags = flags,
            Choices = choices,
            Counts = counts,
            Widgets = WidgetTable(s.Widgets ?? []),
        };
    }

    /// <summary>Widget'ın tablo türü: bilinen alt tür, bölmenin çeşidi (portal/klasör/masaüstü süzgeci), not/yapılacaklar, kutu.</summary>
    public static string TypeOf(WidgetConfig w)
    {
        if (WidgetVariants.Of(w) is { } variant) return variant;
        if (!string.IsNullOrEmpty(w.Variant)) return "other";
        return w.Kind switch
        {
            WidgetKind.Fence when WidgetVariants.IsPortal(w) => "portal",
            WidgetKind.Fence when w.Filter != DesktopFilter.None => "desktop-" + w.Filter.ToString().ToLowerInvariant(),
            WidgetKind.Fence when !string.IsNullOrEmpty(w.FolderName) => "folder",
            WidgetKind.Fence => "empty",
            WidgetKind.Note => w.NoteChecklist ? "checklist" : "note",
            WidgetKind.Launcher => "box",
            _ => "classic",
        };
    }

    private static List<WidgetRow> WidgetTable(IEnumerable<WidgetConfig> widgets) =>
        widgets
            .GroupBy(w => (Kind: w.Kind.ToString(), Type: TypeOf(w)))
            .OrderBy(g => g.Key.Kind, StringComparer.Ordinal).ThenBy(g => g.Key.Type, StringComparer.Ordinal)
            .Select(g => new WidgetRow(
                g.Key.Kind,
                g.Key.Type,
                g.Count(),
                g.Count(w => w.Locked),
                string.Join(", ", g.GroupBy(w => w.Style.ToString()).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key} {x.Count()}")),
                g.Min(w => Finite(w.Scale)),
                g.Max(w => Finite(w.Scale)),
                g.Sum(w => w.Tabs?.Count ?? 0),
                g.Sum(w => w.Tabs?.Sum(t => t.Items?.Count ?? 0) ?? 0)))
            .ToList();

    private static double Finite(double value) => double.IsFinite(value) ? Math.Round(value, 2) : 1;

    /// <summary>ozet.json: biçim sürümü, oluşturulma anı, ortam, ayar özeti, widget tablosu ve (verildiyse) başarım.</summary>
    public byte[] ToJson(DiagnosticEnvironment? environment, PerfFacts? perf, DateTime utcNow)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteNumber("format", 1);
            json.WriteString("generated", utcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            if (environment is not null)
            {
                json.WriteStartObject("environment");
                foreach (var (key, value) in environment.ToFacts()) json.WriteString(key, value);
                json.WriteEndObject();
            }
            json.WriteStartObject("flags");
            foreach (var (key, value) in Flags) json.WriteBoolean(key, value);
            json.WriteEndObject();
            json.WriteStartObject("choices");
            foreach (var (key, value) in Choices) json.WriteString(key, value);
            json.WriteEndObject();
            json.WriteStartObject("counts");
            foreach (var (key, value) in Counts) json.WriteNumber(key, value);
            json.WriteEndObject();
            json.WriteStartArray("widgets");
            foreach (var row in Widgets)
            {
                json.WriteStartObject();
                json.WriteString("kind", row.Kind);
                json.WriteString("type", row.Type);
                json.WriteNumber("count", row.Count);
                json.WriteNumber("locked", row.Locked);
                json.WriteString("styles", row.Styles);
                json.WriteNumber("scaleMin", row.ScaleMin);
                json.WriteNumber("scaleMax", row.ScaleMax);
                if (row.Tabs > 0) json.WriteNumber("tabs", row.Tabs);
                if (row.Items > 0) json.WriteNumber("items", row.Items);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            if (perf is not null)
            {
                json.WriteStartObject("perf");
                foreach (var (key, value) in perf.Rows()) json.WriteString(key, value);
                json.WriteEndObject();
            }
            json.WriteEndObject();
        }
        return buffer.ToArray();
    }
}
