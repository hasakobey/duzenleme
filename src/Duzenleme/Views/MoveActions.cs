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
            Notice.Show(L.F("Masaüstü düzenlenemedi: {0}", ex.Message), NoticeKind.Error);
            return;
        }
        if (moved.Count > 0)
            Notice.Show(L.P(moved.Count, "{0} dosya yerine taşındı."), NoticeKind.Success, L.T("Geçmişi gör"), AutoMovePage.ShowHistory);
        else
            Notice.Show(L.T("Taşınacak dosya bulunamadı."), NoticeKind.Info);
    }

    /// <summary>Taşımayı geri alır: dosya masaüstüne döner ve bir daha otomatik taşınmaz.</summary>
    public static void Undo(MoveEntry? entry)
    {
        if (entry is null || entry.Undone)
        {
            Notice.Show(L.T("Geri alınacak taşıma yok."), NoticeKind.Info);
            return;
        }
        try
        {
            AppHost.Organizer.Undo(entry);
            Notice.Show(L.F("{0} masaüstüne döndü. Bir daha otomatik taşınmayacak.", Path.GetFileName(entry.Source)), NoticeKind.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notice.Show(L.F("Geri alınamadı: {0}", ex.Message), NoticeKind.Error);
        }
    }
}

/// <summary>Otomatik taşıma durum kartının metinleri (Ana sayfa ve Otomatik taşıma sayfası aynı dili konuşur).</summary>
internal static class AutoMoveStatus
{
    /// <summary>
    /// Masaüstünde klasörü bulunan etkin kural sayısı: bu kuralların dosyaları gerçekten taşınır. Yalnızca masaüstündeki
    /// klasör adlarına bakılır; onlar da diskten değil, arka planda güncel tutulan anlık görüntüden okunur (her ayar
    /// kaydında çağrılır).
    /// </summary>
    public static int ReadyRules()
    {
        var folders = AppHost.DesktopFolders();
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
            return (L.T("Otomatik taşıma kapalı"),
                L.T("Yeni dosyalar masaüstünde kalır. Açarsan masaüstündeki uygun dosyalar da klasörlerine taşınır; her taşıma geri alınabilir."));
        var title = L.T("Otomatik taşıma açık");
        if (today > 0) return (title, L.P(today, "Bugün {0} dosya yerine taşındı · toplam {1}", total));
        if (ready > 0) return (title, L.T("Masaüstüne düşen dosyalar klasörlerine gider."));
        return (title, L.T("Masaüstünde henüz hedef klasör yok. Örneğin masaüstünde \"PDF\" adında bir klasör açınca PDF'ler oraya gider."));
    }
}
