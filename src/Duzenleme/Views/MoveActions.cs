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
    /// kapalıyken de çalışır (tepsideki "Dosyaları şimdi taşı" ve Ctrl+Alt+O gibi).
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

/// <summary>Otomatik taşıma durumunun metinleri (Ana sayfanın satırı ve Otomatik taşıma sayfasının kartı aynı dili konuşur).</summary>
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

    /// <summary>Otomatik taşıma sayfasının durum kartı: başlık ve açıklama.</summary>
    public static (string Title, string Text) Describe(bool on, int ready)
    {
        if (!on)
            return (L.T("Otomatik taşıma kapalı"),
                L.T("Yeni dosyalar masaüstünde kalır. Açarsan masaüstündeki uygun dosyalar da klasörlerine taşınır; her taşıma geri alınabilir."));
        var title = L.T("Otomatik taşıma açık");
        if (ready > 0) return (title, L.T("Masaüstüne düşen dosyalar klasörlerine gider."));
        return (title, L.T("Masaüstünde henüz hedef klasör yok. Örneğin masaüstünde \"PDF\" adında bir klasör açınca PDF'ler oraya gider."));
    }

    /// <summary>
    /// Ana sayfanın tek satırlık kartı: "Otomatik taşıma kapalı", "Otomatik taşıma açık · son: rapor.pdf → PDF" ya da (açık ama
    /// masaüstünde hedef klasör yokken; dosyalar gitmez) "… · masaüstünde hedef klasör yok" ve SetUp (kartta "Klasörleri
    /// ayarla" görünür). <paramref name="last"/>: geri alınmamış son taşıma.
    /// </summary>
    public static (string Text, bool SetUp) Line(bool on, int ready, MoveEntry? last)
    {
        if (!on) return (L.T("Otomatik taşıma kapalı"), false);
        if (ready == 0) return (L.T("Otomatik taşıma açık · masaüstünde hedef klasör yok"), true);
        return last is null
            ? (L.T("Otomatik taşıma açık"), false)
            : (L.F("Otomatik taşıma açık · son: {0} → {1}", last.FileName, last.FolderName), false);
    }
}
