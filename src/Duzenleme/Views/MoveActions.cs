using System.IO;
using Duzenleme.Core;

namespace Duzenleme.Views;

/// <summary>
/// Taşıma komutları ve sonuçlarının bildirimi (Ana sayfa ve Otomatik taşıma sayfası ortak kullanır). Sonuç ana pencere
/// açıksa bildirim şeridinde, değilse tepsi balonunda gösterilir (<see cref="Notice"/>).
/// </summary>
internal static class MoveActions
{
    /// <summary>
    /// Masaüstündeki uygun dosyaları şimdi taşır ve sonucu bildirir. Açık bir komut olduğu için otomatik taşıma
    /// kapalıyken de çalışır (tepsideki "Masaüstünü şimdi düzenle" ve Ctrl+Alt+O gibi).
    /// </summary>
    public static async Task OrganizeNowAsync()
    {
        List<MoveEntry> moved;
        try
        {
            moved = await AppHost.OrganizeNowAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Masaüstü klasörü okunamadı (ör. ağ/OneDrive yolu o an erişilemiyor).
            Notice.Show($"Masaüstü düzenlenemedi: {ex.Message}", NoticeKind.Error);
            return;
        }
        if (moved.Count > 0)
            Notice.Show($"{moved.Count} dosya yerine taşındı.", NoticeKind.Success, "Geçmişi gör", AutoMovePage.ShowHistory);
        else
            Notice.Show("Taşınacak dosya bulunamadı.", NoticeKind.Info);
    }

    /// <summary>Taşımayı geri alır: dosya masaüstüne döner ve bir daha otomatik taşınmaz.</summary>
    public static void Undo(MoveEntry? entry)
    {
        if (entry is null || entry.Undone)
        {
            Notice.Show("Geri alınacak taşıma yok.", NoticeKind.Info);
            return;
        }
        try
        {
            AppHost.Organizer.Undo(entry);
            Notice.Show($"{Path.GetFileName(entry.Source)} masaüstüne döndü. Bir daha otomatik taşınmayacak.", NoticeKind.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notice.Show($"Geri alınamadı: {ex.Message}", NoticeKind.Error);
        }
    }
}

/// <summary>Otomatik taşıma durum kartının metinleri (Ana sayfa ve Otomatik taşıma sayfası aynı dili konuşur).</summary>
internal static class AutoMoveStatus
{
    /// <summary>
    /// Masaüstünde klasörü bulunan etkin kural sayısı: bu kuralların dosyaları gerçekten taşınır. Yalnızca masaüstündeki
    /// klasör adlarına bakılır, klasörlerin içi sayılmaz (Ana sayfa açılışı yavaşlamasın).
    /// </summary>
    public static int ReadyRules()
    {
        List<string> folders;
        try { folders = AppHost.Organizer.ExistingFolders().ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
        return AppHost.Settings.Rules.Count(r => r.Enabled && r.Extensions.Count > 0 && !string.IsNullOrWhiteSpace(r.TargetFolder)
                                                 && folders.Any(f => FolderName.Equal(f, r.TargetFolder)));
    }

    /// <summary>
    /// Kartın başlığı ve açıklaması. today/total yalnızca Ana sayfada verilir ("Bugün n dosya… · toplam t");
    /// Otomatik taşıma sayfasının kartında sayı satırı yoktur.
    /// </summary>
    public static (string Title, string Text) Describe(bool on, int ready, int today = 0, int total = 0)
    {
        if (!on)
            return ("Otomatik taşıma kapalı",
                "Yeni dosyalar masaüstünde kalır. Açarsan masaüstündeki uygun dosyalar da klasörlerine taşınır; her taşıma geri alınabilir.");
        const string title = "Otomatik taşıma açık";
        if (today > 0) return (title, $"Bugün {today} dosya yerine taşındı · toplam {total}");
        if (ready > 0) return (title, "Masaüstüne düşen dosyalar klasörlerine gider.");
        return (title, "Masaüstünde henüz hedef klasör yok. Örneğin masaüstünde \"PDF\" adında bir klasör açınca PDF'ler oraya gider.");
    }
}
