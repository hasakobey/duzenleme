using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

// 2.2 yerel tanılama: tanılama sayfası (Core/DiagnosticReport) ve tanılama paketi (Core/DiagnosticPackage). Günlük
// içeriği (alan, ileti, hata türü) çevrilmez.
internal static partial class En
{
    [ModuleInitializer]
    internal static void AddDiagnostics() => Add(
    [
        // Tanılama sayfası
        new("{0} tanılama sayfası", "{0} diagnostics page"),
        new("Bu sayfa bu bilgisayarda oluşturuldu ve hiçbir yere gönderilmedi. Oluşturulma: {0}",
            "This page was created on this PC and wasn't sent anywhere. Created: {0}"),
        new("Özet", "Summary"),
        new("Sürüm", "Version"),
        new("Son 7 günde hata", "Errors in the last 7 days"),
        new("Son 7 günde uyarı", "Warnings in the last 7 days"),
        new("Son 7 günde çökme", "Crashes in the last 7 days"),
        new("Kaydedilmeyi bekleyen çökme raporu", "Crash reports waiting to be saved"),
        new("Hata günlüğü kapalı: yeni kayıt yazılmıyor.", "The error log is off: no new entries are being written."),
        new("Son hatalar", "Recent errors"),
        new("Son 7 günde hata ya da uyarı yok.", "No errors or warnings in the last 7 days."),
        new("Kaç kez", "Times"),
        new("İlk", "First"),
        new("Son", "Last"),
        new("Düzey", "Level"),
        new("Alan", "Area"),
        new("İleti", "Message"),
        new("Hata türü", "Error type"),
        new("Yığın", "Stack"),
        new("Çökmeler", "Crashes"),
        new("Kaydedilmiş çökme raporu yok.", "No saved crash reports."),
        new("Günlüğün son satırları", "Last log lines"),
        new("Ortam", "Environment"),
        new("Widget türü", "Widget type"),
        new("Alt tür", "Subtype"),
        new("Sayı", "Count"),
        new("Kilitli", "Locked"),
        new("Stil", "Style"),
        new("Öğeler", "Items"),
        new("Başarım", "Performance"),
        new("Günlük dosyaları", "Log files"),
        new("Günlüklerin toplam boyutu", "Total size of logs"),
        new("Okunamayan dosyaların yedekleri (.bozuk)", "Backups of unreadable files (.bozuk)"),
        new("Ayarların özeti (yalnızca açık/kapalı seçenekler ve sayılar)", "Settings summary (only on/off options and counts)"),
        new("açık", "on"),
        new("kapalı", "off"),

        // Tanılama paketi
        new("Tanılama sayfası", "Diagnostics page"),
        new("Ayarların özeti: yalnızca açık/kapalı seçenekler ve sayılar", "Settings summary: only on/off options and counts"),
        new("Beni oku (iki dilli)", "Read me (Turkish and English)"),
        new("Hata günlüğü · {0}", "Error log · {0}"),
        new("Kaydedilmiş çökme raporu · {0}", "Saved crash report · {0}"),
    ]);
}
