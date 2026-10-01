namespace Duzenleme.Core;

/// <summary>Tarayıcıda açılan destek bağlantıları. Hiçbiri kendiliğinden bir şey göndermez: kullanıcı sayfayı görür, isterse gönderir.</summary>
public static class SupportLinks
{
    /// <summary>
    /// Yapay zekânın ürettiği uygunsuz içeriği bildirme (Store 11.16): önceden doldurulmuş, boş bir GitHub sorunu. Adrese
    /// kullanıcının açıklaması, klasör adı ya da simge KONMAZ (sayfa açılırken adres GitHub'a gider); yalnızca sürüm ve şablon.
    /// Şablon arayüz dilinden bağımsız olarak iki dillidir (sorunu okuyan geliştirici için): çevrilmez.
    /// </summary>
    public static string ReportAiContent(string version)
    {
        var title = "Uygunsuz yapay zekâ içeriği / Inappropriate AI content"; // l10n: çevrilmez (iki dilli şablon)
        var body = string.Join("\n",
            $"{AppInfo.Name} {version}",
            "",
            "Ne istendi (simge açıklaması) / What was requested (icon description):", // l10n: çevrilmez (iki dilli şablon)
            "",
            "Ne üretildi, neden uygunsuz (istersen ekran görüntüsü ekle) / What was generated and why it is inappropriate (you may attach a screenshot):", // l10n: çevrilmez (iki dilli şablon)
            "");
        return $"{AppInfo.IssuesUrl}/new?title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";
    }

    /// <summary>Sorun kaydı formunun adı (<c>.github/ISSUE_TEMPLATE/sorun.yml</c>) ve sürüm alanının kimliği.</summary>
    public const string ProblemTemplate = "sorun.yml";
    public const string ProblemVersionField = "version";

    /// <summary>
    /// "GitHub'da sorun aç" (Ayarlar > Yardım): sorun formunu açar. Adres YALNIZCA şablonun adını, başlığı ve sürümü taşır;
    /// günlük, tanılama paketi, yol ya da kullanıcının yazdığı hiçbir şey konmaz (Store 10.5.2: uygulama bir şey göndermez,
    /// paketi kullanıcı isterse kendisi ekler). Başlık iki dillidir (sorunu okuyan geliştirici için): çevrilmez.
    /// </summary>
    public static string ReportProblem(string version)
    {
        var title = "Sorun / Problem: "; // l10n: çevrilmez (iki dilli şablon)
        return $"{AppInfo.IssuesUrl}/new?template={Uri.EscapeDataString(ProblemTemplate)}&title={Uri.EscapeDataString(title)}" +
               $"&{ProblemVersionField}={Uri.EscapeDataString(version)}";
    }
}
