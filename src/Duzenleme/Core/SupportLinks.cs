namespace Duzenleme.Core;

/// <summary>Tarayıcıda açılan destek bağlantıları. Hiçbiri kendiliğinden bir şey göndermez: kullanıcı sayfayı görür, isterse gönderir.</summary>
public static class SupportLinks
{
    /// <summary>
    /// Yapay zekânın ürettiği uygunsuz içeriği bildirme (Store 11.16): önceden doldurulmuş, boş bir GitHub sorunu. Adrese
    /// kullanıcının açıklaması, klasör adı ya da simge KONMAZ (sayfa açılırken adres GitHub'a gider); yalnızca sürüm ve şablon.
    /// </summary>
    public static string ReportAiContent(string version)
    {
        var title = "Uygunsuz yapay zekâ içeriği / Inappropriate AI content";
        var body = string.Join("\n",
            $"{AppInfo.Name} {version}",
            "",
            "Ne istendi (simge açıklaması) / What was requested (icon description):",
            "",
            "Ne üretildi, neden uygunsuz (istersen ekran görüntüsü ekle) / What was generated and why it is inappropriate (you may attach a screenshot):",
            "");
        return $"{AppInfo.IssuesUrl}/new?title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";
    }
}
