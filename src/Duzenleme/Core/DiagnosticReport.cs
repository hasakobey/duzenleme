using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Duzenleme.Core;

/// <summary>Tanılama sayfasının girdileri (hepsi diskten okunmuş ya da bellekteki temizlenmiş veriler).</summary>
public sealed record ReportInput
{
    public required DiagnosticSnapshot Snapshot { get; init; }
    public DiagnosticEnvironment? Environment { get; init; }
    public PerfFacts? Perf { get; init; }
    /// <summary>Son 7 günün günlük kayıtları (<see cref="LogFiles.Read"/>).</summary>
    public IReadOnlyList<LogEntry> Lines { get; init; } = [];
    /// <summary>Kaydedilmiş çökme raporları.</summary>
    public IReadOnlyList<CrashReport> Crashes { get; init; } = [];
    public int PendingCrashes { get; init; }
    public int LogFileCount { get; init; }
    public long LogBytes { get; init; }
    /// <summary>Veri klasöründeki <c>.bozuk-*</c> yedeklerinin sayısı.</summary>
    public int CorruptFiles { get; init; }
    /// <summary>"Hata günlüğü tut" kapalı mı?</summary>
    public bool LogOff { get; init; }
    public DateTime NowUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>Aynı imzalı kayıtlar: kaç kez, ilk ve son görülme, son örnek.</summary>
public sealed record ErrorGroup(string Signature, AppLogLevel Level, string Area, string Message, int Count, DateTime First,
    DateTime Last, LogEntry Sample);

/// <summary>
/// Yerel tanılama sayfası (<c>logs\tanilama.html</c> ve paketteki rapor.html): tek dosya, betiksiz. CSP
/// <c>default-src 'none'</c>; stil yalnızca satır içi ve özetiyle izinli; resim, yazı tipi, bağlantı ve form yok; yalnızca
/// <c>#</c> çapalı gezinti. Diskten okunan her metin yeniden temizlenir ve kaçışlanır. Başlıklar arayüz dilinde, günlük
/// içeriği çevrilmez. Saf: dosyayı yazan ve açan arayüz tarafıdır (arka planda).
/// </summary>
public static class DiagnosticReport
{
    public const string FileName = "tanilama.html";

    /// <summary>Sayfanın tek stili (CSP özeti bundan hesaplanır). Fareyle üstüne gelince hiçbir şey belirip kaybolmaz.</summary>
    public const string Css =
        ":root{color-scheme:light dark;--bg:#f6f6f8;--card:#ffffff;--text:#1b1b1f;--muted:#5c5c66;--line:#dedee6;--accent:#4f3fd1;--error:#b3261e;--warn:#7a5000;" +
        "font-family:\"Segoe UI Variable Text\",\"Segoe UI\",system-ui,sans-serif}" +
        "@media (prefers-color-scheme:dark){:root{--bg:#1b1b1f;--card:#26262c;--text:#ececf1;--muted:#a9a9b4;--line:#3b3b44;--accent:#b2a6ff;--error:#ff8f86;--warn:#f0c46b}}" +
        "body{margin:0;background:var(--bg);color:var(--text);font-size:14px;line-height:1.5}" +
        "header,main{max-width:1100px;margin:0 auto;padding:16px}" +
        "h1{font-size:24px;margin:8px 0}h2{font-size:18px;margin:0 0 8px}h3{font-size:15px;margin:12px 0 4px}" +
        "nav{display:flex;flex-wrap:wrap;gap:4px 16px;margin-top:8px}a{color:var(--accent)}" +
        "section{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:16px;margin:0 0 16px}" +
        "table{border-collapse:collapse;width:100%}th,td{text-align:left;vertical-align:top;padding:4px 8px;border-bottom:1px solid var(--line)}" +
        "th{color:var(--muted);font-weight:600}td.n{text-align:right;white-space:nowrap}" +
        "pre{white-space:pre-wrap;word-break:break-word;font-family:Consolas,\"Cascadia Mono\",monospace;font-size:12px;margin:4px 0}" +
        "summary{cursor:pointer;color:var(--accent)}.muted{color:var(--muted)}.E{color:var(--error);font-weight:600}.W{color:var(--warn);font-weight:600}" +
        ".crash{border-top:1px solid var(--line);padding-top:8px;margin-top:8px}" +
        "@media (forced-colors:active){:root{--bg:Canvas;--card:Canvas;--text:CanvasText;--muted:CanvasText;--line:CanvasText;--accent:LinkText;--error:CanvasText;--warn:CanvasText}}";

    /// <summary>CSS'in SHA-256 özeti (CSP <c>style-src 'sha256-…'</c>).</summary>
    public static string CssHash => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Css)));

    public static string ContentSecurityPolicy => $"default-src 'none'; style-src 'sha256-{CssHash}'; base-uri 'none'; form-action 'none'";

    public static string Render(ReportInput input)
    {
        var sb = new StringBuilder(32 * 1024);
        var since = input.NowUtc.AddDays(-7);
        var lines = input.Lines.Where(l => l.Utc >= since).ToList();
        var groups = Group(lines);
        var title = L.F("{0} tanılama sayfası", AppInfo.Name);

        sb.Append("<!DOCTYPE html>\n<html lang=\"").Append(L.Current == Lang.En ? "en" : "tr").Append("\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n");
        sb.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"").Append(ContentSecurityPolicy).Append("\">\n");
        sb.Append("<meta name=\"referrer\" content=\"no-referrer\">\n");
        sb.Append("<meta name=\"color-scheme\" content=\"light dark\">\n");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>").Append(Escape(title)).Append("</title>\n");
        sb.Append("<style>").Append(Css).Append("</style>\n</head>\n<body>\n<header>\n");
        sb.Append("<h1>").Append(Escape(title)).Append("</h1>\n<p class=\"muted\">")
            .Append(Escape(L.F("Bu sayfa bu bilgisayarda oluşturuldu ve hiçbir yere gönderilmedi. Oluşturulma: {0}", Time(input.NowUtc))))
            .Append("</p>\n<nav>");
        foreach (var (id, heading) in Sections()) sb.Append("<a href=\"#").Append(id).Append("\">").Append(Escape(heading)).Append("</a>");
        sb.Append("</nav>\n</header>\n<main>\n");

        // Özet
        Open(sb, "ozet", L.T("Özet"));
        var crashes7 = input.Crashes.Count(c => c.Utc >= since);
        sb.Append("<table><tbody>\n");
        Row(sb, L.T("Sürüm"), input.Environment?.Version ?? AppInfo.Version);
        Row(sb, L.T("Son 7 günde hata"), Number(lines.Where(l => l.Level == AppLogLevel.Error).Sum(l => (long)l.Count)));
        Row(sb, L.T("Son 7 günde uyarı"), Number(lines.Where(l => l.Level == AppLogLevel.Warn).Sum(l => (long)l.Count)));
        Row(sb, L.T("Son 7 günde çökme"), Number(crashes7));
        Row(sb, L.T("Kaydedilmeyi bekleyen çökme raporu"), Number(input.PendingCrashes));
        sb.Append("</tbody></table>\n");
        if (input.LogOff) sb.Append("<p class=\"muted\">").Append(Escape(L.T("Hata günlüğü kapalı: yeni kayıt yazılmıyor."))).Append("</p>\n");
        Close(sb);

        // Son hatalar
        Open(sb, "hatalar", L.T("Son hatalar"));
        if (groups.Count == 0)
        {
            sb.Append("<p>").Append(Escape(L.T("Son 7 günde hata ya da uyarı yok."))).Append("</p>\n");
        }
        else
        {
            sb.Append("<table><thead><tr>");
            foreach (var h in new[] { L.T("Kaç kez"), L.T("İlk"), L.T("Son"), L.T("Düzey"), L.T("Alan"), L.T("İleti"), L.T("Hata türü") })
                sb.Append("<th>").Append(Escape(h)).Append("</th>");
            sb.Append("</tr></thead><tbody>\n");
            foreach (var g in groups.Take(200))
            {
                var level = g.Level == AppLogLevel.Error ? "E" : "W";
                sb.Append("<tr><td class=\"n\">").Append(Number(g.Count)).Append("</td><td>").Append(Escape(Time(g.First)))
                    .Append("</td><td>").Append(Escape(Time(g.Last))).Append("</td><td class=\"").Append(level).Append("\">").Append(level)
                    .Append("</td><td>").Append(Escape(Clean(g.Area))).Append("</td><td>").Append(Escape(Clean(g.Message)));
                if (g.Sample.Error is { } error) Stack(sb, error);
                sb.Append("</td><td>").Append(Escape(g.Sample.Error is { } e ? Clean(e.Type) + " " + Clean(e.HResult) : "")).Append("</td></tr>\n");
            }
            sb.Append("</tbody></table>\n");
        }
        Close(sb);

        // Çökmeler
        Open(sb, "cokmeler", L.T("Çökmeler"));
        if (input.Crashes.Count == 0) sb.Append("<p>").Append(Escape(L.T("Kaydedilmiş çökme raporu yok."))).Append("</p>\n");
        foreach (var crash in input.Crashes.OrderByDescending(c => c.Utc).Take(20))
        {
            sb.Append("<div class=\"crash\"><h3>").Append(Escape(Time(crash.Utc))).Append(" · ").Append(Escape(Clean(crash.Kind)))
                .Append(" · ").Append(Escape(Clean(crash.Version))).Append("</h3>\n");
            if (crash.Error is { } error)
            {
                sb.Append("<p>").Append(Escape(L.T("Hata türü"))).Append(": ").Append(Escape(Clean(error.Type) + " " + Clean(error.HResult))).Append("</p>");
                Stack(sb, error);
            }
            if (crash.LogTail.Count > 0)
            {
                sb.Append("<details><summary>").Append(Escape(L.T("Günlüğün son satırları"))).Append("</summary><pre>");
                foreach (var entry in crash.LogTail)
                {
                    sb.Append(Escape($"{Time(entry.Utc)} {entry.LevelChar} {Clean(entry.Area)}: {Clean(entry.Message)}"));
                    if (entry.Error is { } x) sb.Append(Escape($" [{Clean(x.Type)} {Clean(x.HResult)}]"));
                    sb.Append('\n');
                }
                sb.Append("</pre></details>\n");
            }
            sb.Append("</div>\n");
        }
        Close(sb);

        // Ortam
        Open(sb, "ortam", L.T("Ortam"));
        var facts = (input.Environment ?? new DiagnosticEnvironment()).ToFacts();
        KeyValues(sb, facts.Select(f => (f.Key, f.Value)));
        Close(sb);

        // Widget'lar
        Open(sb, "widgetlar", L.T("Widget'lar"));
        if (input.Snapshot.Widgets.Count == 0) sb.Append("<p>0</p>\n");
        else
        {
            sb.Append("<table><thead><tr>");
            foreach (var h in new[] { L.T("Widget türü"), L.T("Alt tür"), L.T("Sayı"), L.T("Kilitli"), L.T("Stil"), L.T("Ölçek"), L.T("Sekmeler"), L.T("Öğeler") })
                sb.Append("<th>").Append(Escape(h)).Append("</th>");
            sb.Append("</tr></thead><tbody>\n");
            foreach (var w in input.Snapshot.Widgets)
            {
                var scale = w.ScaleMin == w.ScaleMax
                    ? w.ScaleMin.ToString("0.##", CultureInfo.InvariantCulture)
                    : w.ScaleMin.ToString("0.##", CultureInfo.InvariantCulture) + "–" + w.ScaleMax.ToString("0.##", CultureInfo.InvariantCulture);
                sb.Append("<tr><td>").Append(Escape(w.Kind)).Append("</td><td>").Append(Escape(w.Type))
                    .Append("</td><td class=\"n\">").Append(Number(w.Count)).Append("</td><td class=\"n\">").Append(Number(w.Locked))
                    .Append("</td><td>").Append(Escape(w.Styles)).Append("</td><td>").Append(Escape(scale))
                    .Append("</td><td class=\"n\">").Append(Number(w.Tabs)).Append("</td><td class=\"n\">").Append(Number(w.Items))
                    .Append("</td></tr>\n");
            }
            sb.Append("</tbody></table>\n");
        }
        Close(sb);

        // Başarım
        Open(sb, "basarim", L.T("Başarım"));
        if (input.Perf is { } perf) KeyValues(sb, perf.Rows());
        else sb.Append("<p class=\"muted\">–</p>\n");
        Close(sb);

        // Dosyalar
        Open(sb, "dosyalar", L.T("Dosyalar"));
        sb.Append("<table><tbody>\n");
        Row(sb, L.T("Günlük dosyaları"), Number(input.LogFileCount));
        Row(sb, L.T("Günlüklerin toplam boyutu"), Size(input.LogBytes));
        Row(sb, L.T("Okunamayan dosyaların yedekleri (.bozuk)"), Number(input.CorruptFiles));
        sb.Append("</tbody></table>\n");
        Close(sb);

        // Ayarlar
        Open(sb, "ayarlar", L.T("Ayarların özeti (yalnızca açık/kapalı seçenekler ve sayılar)"));
        KeyValues(sb, input.Snapshot.Flags.Select(f => (f.Key, f.Value ? L.T("açık") : L.T("kapalı")))
            .Concat(input.Snapshot.Choices.Select(c => (c.Key, c.Value)))
            .Concat(input.Snapshot.Counts.Select(c => (c.Key, Number(c.Value)))));
        Close(sb);

        sb.Append("</main>\n</body>\n</html>\n");
        return sb.ToString();
    }

    private static IEnumerable<(string Id, string Heading)> Sections() =>
    [
        ("ozet", L.T("Özet")),
        ("hatalar", L.T("Son hatalar")),
        ("cokmeler", L.T("Çökmeler")),
        ("ortam", L.T("Ortam")),
        ("widgetlar", L.T("Widget'lar")),
        ("basarim", L.T("Başarım")),
        ("dosyalar", L.T("Dosyalar")),
        ("ayarlar", L.T("Ayarlar")),
    ];

    /// <summary>Kayıtları imzaya göre gruplar (yalnızca hata ve uyarı), en son görülen önce.</summary>
    public static IReadOnlyList<ErrorGroup> Group(IEnumerable<LogEntry> lines) =>
        lines.Where(l => l.Level != AppLogLevel.Info)
            .GroupBy(l => l.Signature.Length > 0 ? l.Signature : LogEntry.SignatureOf(l.Level, l.Area, l.Message, l.Error))
            .Select(g =>
            {
                var sample = g.MaxBy(x => x.Utc)!;
                return new ErrorGroup(g.Key, sample.Level, sample.Area, sample.Message, g.Sum(x => x.Count),
                    g.Min(x => x.Utc), g.Max(x => x.Utc), sample);
            })
            .OrderByDescending(g => g.Last)
            .ToList();

    private static void Open(StringBuilder sb, string id, string heading) =>
        sb.Append("<section id=\"").Append(id).Append("\">\n<h2>").Append(Escape(heading)).Append("</h2>\n");

    private static void Close(StringBuilder sb) => sb.Append("</section>\n");

    private static void Row(StringBuilder sb, string key, string value) =>
        sb.Append("<tr><th>").Append(Escape(key)).Append("</th><td>").Append(Escape(value)).Append("</td></tr>\n");

    private static void KeyValues(StringBuilder sb, IEnumerable<(string Key, string Value)> rows)
    {
        sb.Append("<table><tbody>\n");
        foreach (var (key, value) in rows) Row(sb, Clean(key), Clean(value));
        sb.Append("</tbody></table>\n");
    }

    private static void Stack(StringBuilder sb, ScrubbedError error)
    {
        sb.Append("<details><summary>").Append(Escape(L.T("Yığın"))).Append("</summary><pre>");
        AppendError(sb, error, 0);
        sb.Append("</pre></details>");
    }

    private static void AppendError(StringBuilder sb, ScrubbedError error, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.Append(Escape(indent + Clean(error.Type) + " " + Clean(error.HResult)));
        if (error.ParamName is { } param) sb.Append(Escape(" (" + Clean(param) + ")"));
        if (error.Message is { Length: > 0 } message) sb.Append(Escape(": " + Clean(message)));
        sb.Append('\n');
        foreach (var frame in error.Frames.Take(LogScrubber.MaxFrames))
            sb.Append(Escape(indent + "  at " + (LogScrubber.Plain.Frame(frame) ?? ""))).Append('\n');
        foreach (var inner in error.Inner.Take(LogScrubber.MaxInner))
        {
            if (depth >= LogScrubber.MaxInner) break;
            AppendError(sb, inner, depth + 1);
        }
    }

    /// <summary>Diskten okunan metin yeniden temizlenir (eski/yeni sürümün ya da elle değiştirilmiş dosyanın verisi de).</summary>
    private static string Clean(string? text) => LogScrubber.Plain.Text(text);

    private static string Time(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string Number(long n) => n.ToString("N0", L.Culture);

    private static string Size(long bytes) => bytes < 1024 * 1024
        ? (bytes / 1024.0).ToString("0.#", L.Culture) + " KB"
        : (bytes / 1048576.0).ToString("0.#", L.Culture) + " MB";

    /// <summary>HTML kaçışı (metin ve öznitelik için): &amp; &lt; &gt; " ' ve = (veride "src=" gibi bir dizi hiç oluşmasın).</summary>
    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length + 16);
        foreach (var c in text)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&#39;"); break;
                case '=': sb.Append("&#61;"); break;
                case '`': sb.Append("&#96;"); break;
                case '\0': break;
                default:
                    if (char.IsControl(c) && c is not '\n' and not '\t') sb.Append(' ');
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
