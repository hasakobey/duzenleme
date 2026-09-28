namespace Duzenleme.Core;

/// <summary>Masaüstünün o anki görünümü: Windows simgeleri ve widget'lar gizli mi?</summary>
public readonly record struct DesktopView(bool IconsHidden, bool WidgetsHidden);

/// <summary>Boş masaüstüne çift tıklamanın o anki etkisi (kalıcı değildir; ayar metin olarak saklanır).</summary>
public enum DoubleClickEffect { None, ToggleDesktop, Peek }

/// <summary>
/// Masaüstünün görünürlüğü tek yerde hesaplanır (AppHost.ApplyDesktopState bunu uygular): bölmeler masaüstünü yönetiyor
/// mu, masaüstü gizlendi mi (Ctrl+Alt+H, çift tık), Windows masaüstüne göz atılıyor mu. Saf hesap; arayüze dokunmaz.
/// </summary>
public static class DesktopState
{
    /// <summary>
    /// Göz atarken Windows simgeleri her zaman görünür, widget'lar isteğe göre çekilir. Göz atılmıyorsa simgeler masaüstü
    /// gizliyken ya da bölmeler yönetirken gizlidir; widget'lar yalnızca masaüstü gizliyken ve (ayar açıksa ya da bölmeler
    /// yönetiyorsa) gizlenir: bölmeler yönetirken "gizle" tüm masaüstünü (bölmeleri) gizler.
    /// </summary>
    public static DesktopView Compute(bool fencesReplaceIcons, bool desktopHidden, bool peeking, bool peekHidesWidgets,
        bool hideWidgetsWithIcons) =>
        peeking
            ? new(false, peekHidesWidgets)
            : new(desktopHidden || fencesReplaceIcons, desktopHidden && (hideWidgetsWithIcons || fencesReplaceIcons));

    // "Boş masaüstüne çift tıklayınca" (AppSettings.DoubleClickAction). Metin olarak saklanır: yeni seçenek eski sürümü bozmaz.
    public const string DoubleClickAuto = "auto";
    public const string DoubleClickToggle = "toggle";
    public const string DoubleClickPeek = "peek";
    public const string DoubleClickNone = "none";

    /// <summary>Ayar kutusundaki sıra.</summary>
    public static readonly string[] DoubleClickChoices = [DoubleClickAuto, DoubleClickToggle, DoubleClickPeek, DoubleClickNone];

    /// <summary>
    /// Kayıtlı seçim. Yeni alan boşsa (2.0'dan gelen) eski anahtara bakılır: çift tıklama kapatılmışsa "hiçbir şey",
    /// açıksa "otomatik" (bugünkü davranış). Bilinmeyen değer (gelecek sürüm) de "otomatik" sayılır.
    /// </summary>
    public static string DoubleClickChoice(string? action, bool legacyHidesDesktop)
    {
        if (string.IsNullOrWhiteSpace(action)) return legacyHidesDesktop ? DoubleClickAuto : DoubleClickNone;
        var trimmed = action.Trim().ToLowerInvariant();
        return DoubleClickChoices.Contains(trimmed) ? trimmed : DoubleClickAuto;
    }

    /// <summary>
    /// Çift tıklamanın şimdiki etkisi. "Otomatik": bölmeler masaüstünü yönetirken Windows masaüstüne göz atar (simgeler
    /// başka türlü görünmez), yönetmiyorsa masaüstünü gizler/gösterir (2.0'daki davranış).
    /// </summary>
    public static DoubleClickEffect ResolveDoubleClick(string? action, bool legacyHidesDesktop, bool fencesReplaceIcons) =>
        DoubleClickChoice(action, legacyHidesDesktop) switch
        {
            DoubleClickToggle => DoubleClickEffect.ToggleDesktop,
            DoubleClickPeek => DoubleClickEffect.Peek,
            DoubleClickNone => DoubleClickEffect.None,
            _ => fencesReplaceIcons ? DoubleClickEffect.Peek : DoubleClickEffect.ToggleDesktop,
        };

    /// <summary>"Kendiliğinden geri dön" seçenekleri (dakika; 0 = ben dönene dek).</summary>
    public static readonly int[] PeekMinuteChoices = [1, 2, 5, 10, 0];

    public const int DefaultPeekMinutes = 2;

    /// <summary>Listede olmayan (elle yazılmış, eksi) değer varsayılana döner.</summary>
    public static int NormalizePeekMinutes(int minutes) =>
        PeekMinuteChoices.Contains(minutes) ? minutes : DefaultPeekMinutes;
}
