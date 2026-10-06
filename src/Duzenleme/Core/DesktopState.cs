namespace Duzenleme.Core;

/// <summary>Masaüstünün o anki görünümü: Windows simgeleri ve widget'lar gizli mi?</summary>
public readonly record struct DesktopView(bool IconsHidden, bool WidgetsHidden);

/// <summary>Boş masaüstüne çift tıklamanın o anki etkisi (kalıcı değildir; ayar metin olarak saklanır).</summary>
public enum DoubleClickEffect { None, ToggleDesktop, Peek, Ring }

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
    /// <summary>2.2: halka imlecin yanında açılır (açılır halka; kısayol gerekmez). 2.1 tanımaz ve "otomatik" sayar.</summary>
    public const string DoubleClickRing = "ring";

    /// <summary>Ayar kutusundaki sıra (yeni seçenek yalnızca sona).</summary>
    public static readonly string[] DoubleClickChoices = [DoubleClickAuto, DoubleClickToggle, DoubleClickPeek, DoubleClickNone, DoubleClickRing];

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
    /// "Çift tıklayınca halka açılsın" açılıyor: şimdiki seçim (halka değilse) <see cref="AppSettings.DoubleClickBeforeRing"/>'e
    /// yazılır (anahtar kapanınca, yeniden başlatmadan sonra da, ona dönülür). Dönüş: yazılacak seçim ("ring").
    /// </summary>
    public static string RingDoubleClickOn(AppSettings settings)
    {
        var current = DoubleClickChoice(settings.DoubleClickAction, settings.DoubleClickHidesDesktop);
        if (current != DoubleClickRing) settings.DoubleClickBeforeRing = current;
        return DoubleClickRing;
    }

    /// <summary>
    /// "Çift tıklayınca halka açılsın" kapanıyor: halkadan önceki seçim (tanınan ve halka olmayan), yoksa "otomatik". Kayıtlı
    /// önceki seçim silinir. Dönüş: yazılacak seçim.
    /// </summary>
    public static string RingDoubleClickOff(AppSettings settings)
    {
        var before = settings.DoubleClickBeforeRing?.Trim().ToLowerInvariant();
        settings.DoubleClickBeforeRing = null;
        return before is not null && before != DoubleClickRing && DoubleClickChoices.Contains(before) ? before : DoubleClickAuto;
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
            DoubleClickRing => DoubleClickEffect.Ring,
            _ => fencesReplaceIcons ? DoubleClickEffect.Peek : DoubleClickEffect.ToggleDesktop,
        };

    /// <summary>"Kendiliğinden geri dön" seçenekleri (dakika; 0 = ben dönene dek).</summary>
    public static readonly int[] PeekMinuteChoices = [1, 2, 5, 10, 0];

    public const int DefaultPeekMinutes = 2;

    /// <summary>Listede olmayan (elle yazılmış, eksi) değer varsayılana döner.</summary>
    public static int NormalizePeekMinutes(int minutes) =>
        PeekMinuteChoices.Contains(minutes) ? minutes : DefaultPeekMinutes;
}
