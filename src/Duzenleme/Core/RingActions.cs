namespace Duzenleme.Core;

/// <summary>
/// Halka öğesi türleri (<see cref="LauncherAction.Type"/>). Metindir, enum değil: tanınmayan (daha yeni sürümün) tür
/// korunur, gösterilir ama çalıştırılmaz.
/// </summary>
public static class RingActionTypes
{
    /// <summary>Web adresi (http, https, mailto serbest; diğer şemalar bir kez onaylanır).</summary>
    public const string Url = "url";

    /// <summary>Kullanıcının yazdığı komut (yalnızca kullanıcıdan; örnek komut yok).</summary>
    public const string Command = "command";

    /// <summary>Tuş kısayolu gönder (en çok 8 birleşim; yalnızca kısayolla açılan halkadan).</summary>
    public const string Keys = "keys";

    /// <summary>Metin yapıştır (pano geçmişine girmez; yalnızca kısayolla açılan halkadan).</summary>
    public const string Text = "text";

    /// <summary>NestDesk eylemi (<see cref="RingAppIds"/>).</summary>
    public const string App = "app";

    public static IReadOnlyList<string> All => [Url, Command, Keys, Text, App];

    public static bool IsKnown(string? type) => type is Url or Command or Keys or Text or App;

    /// <summary>Hedef pencereye tuş/metin gönderen tür: yalnızca kısayolla açılan halkadan çalışır (önceki pencereye gider).</summary>
    public static bool NeedsTargetWindow(string? type) => type is Keys or Text;
}

/// <summary>
/// "app" öğelerinin kendi metin kimlikleri. <see cref="HotkeyAction"/> kalıcı değildir; adları ayara yazılmaz, eşleme buradadır.
/// "Şimdi düzenle" bilerek yok: Paused'u atlar ve tuşu bırakınca yanlışlıkla seçilebilir.
/// </summary>
public static class RingAppIds
{
    public const string NewNote = "new-note";
    public const string PeekDesktop = "peek-desktop";
    public const string QuickAdd = "quick-add";
    public const string OpenApp = "open-app";

    /// <summary>Tanınan bütün kimlikler (tohum sırasıyla).</summary>
    public static IReadOnlyList<string> All => [NewNote, PeekDesktop, QuickAdd, OpenApp];

    /// <summary>Yeni halka menünün kök halkasındaki eylemler.</summary>
    public static IReadOnlyList<string> Seed => All;

    public static bool IsKnown(string? id) => id is NewNote or PeekDesktop or QuickAdd or OpenApp;

    /// <summary>Kimliğin kısayol eylemi (App.RunAction); tanınmayan kimlikte null.</summary>
    public static HotkeyAction? ToHotkeyAction(string? id) => id switch
    {
        NewNote => HotkeyAction.NewNote,
        PeekDesktop => HotkeyAction.PeekDesktop,
        QuickAdd => HotkeyAction.QuickAdd,
        OpenApp => HotkeyAction.OpenApp,
        _ => null,
    };

    /// <summary>Kısayol eyleminin halka kimliği; halkada sunulmayan eylemde (ör. OrganizeNow, Ring) null.</summary>
    public static string? FromHotkeyAction(HotkeyAction action) => action switch
    {
        HotkeyAction.NewNote => NewNote,
        HotkeyAction.PeekDesktop => PeekDesktop,
        HotkeyAction.QuickAdd => QuickAdd,
        HotkeyAction.OpenApp => OpenApp,
        _ => null,
    };

    /// <summary>Öğenin varsayılan adı (kullanıcı ad vermediyse), arayüz dilinde; tanınmayan kimlikte null.</summary>
    public static string? Label(string? id) => id switch
    {
        NewNote => L.T("Yeni not"),
        PeekDesktop => L.T("Windows masaüstüne göz at"),
        QuickAdd => L.T("Widget ekle"),
        OpenApp => L.F("{0}'i aç", AppInfo.Name),
        _ => null,
    };
}

/// <summary>Web adresi denetiminin sonucu (kalıcı değil).</summary>
public enum UrlCheck
{
    /// <summary>Adres değil (göreli, dosya yolu, file:, UNC, tehlikeli şema): kaydedilmez, çalıştırılmaz.</summary>
    Invalid,

    /// <summary>http, https, mailto: doğrudan açılır.</summary>
    Allowed,

    /// <summary>Başka bir şema (ms-settings:, steam:…): öğe eklenirken bir kez onaylanır; içe aktarılırsa kapalı gelir.</summary>
    NeedsConfirmation,
}

/// <summary>
/// Halka öğelerinin kuralları (saf; testlenir). Store koşulları: web adresi hiçbir zaman dosya yolu ya da program olamaz;
/// tuş öğesi en çok <see cref="MaxChords"/> birleşim, metin en çok <see cref="MaxTextLength"/> karakter; içe aktarılan
/// komut, tuş ve metin öğeleri kullanıcı onaylayana dek kapalıdır. Öğe içerikleri günlüğe yazılmaz.
/// </summary>
public static class RingActions
{
    public const int MaxChords = 8;
    public const int MaxTextLength = 10_000;
    public const int MaxUrlLength = 4096;

    // Kabukta kod çalıştıran ya da uzak aramaya açılan şemalar: onayla da kabul edilmez.
    private static readonly HashSet<string> BlockedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "file", "javascript", "vbscript", "data", "jar", "ms-msdt", "search-ms", "search",
    };

    /// <summary>Uygulama eylemi öğesi (tohum ve "NestDesk eylemi" menüsü).</summary>
    public static LauncherAction ForApp(string id) => new() { Type = RingActionTypes.App, App = id };

    /// <summary>
    /// Metni web adresi olarak denetler. Mutlak bir URI olmalı; file:, UNC ve "C:\x.exe" gibi yollar geçersizdir (kabuk onları
    /// program olarak çalıştırırdı). Şeması olmayan "example.com" için <see cref="TryNormalizeUrl"/>.
    /// </summary>
    public static UrlCheck CheckUrl(string? text) => TryParseUrl(text, out _, out var check) ? check : UrlCheck.Invalid;

    /// <summary>Açılacak adres (kaçışlanmış, <see cref="Uri.AbsoluteUri"/>); geçersizse false.</summary>
    public static bool TryParseUrl(string? text, out Uri uri, out UrlCheck check)
    {
        uri = null!;
        check = UrlCheck.Invalid;
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > MaxUrlLength) return false;
        // Yol gibi görünen her şey (C:\…, \\sunucu\…, /…) adres değildir; Uri onları file: olarak kabul ederdi.
        if (value.StartsWith('\\') || value.StartsWith('/') || (value.Length >= 2 && value[1] == ':' && char.IsAsciiLetter(value[0])))
            return false;
        if (value.Any(char.IsControl)) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)) return false;
        if (parsed.IsFile || parsed.IsUnc || string.IsNullOrEmpty(parsed.Scheme) || BlockedSchemes.Contains(parsed.Scheme)) return false;
        // http/https sunucusuz olamaz ("http:x").
        var web = parsed.Scheme is "http" or "https";
        if (web && string.IsNullOrEmpty(parsed.Host)) return false;
        uri = parsed;
        check = web || parsed.Scheme == "mailto" ? UrlCheck.Allowed : UrlCheck.NeedsConfirmation;
        return true;
    }

    /// <summary>
    /// Kullanıcının yazdığı adres: şeması yoksa ve bir alan adına benziyorsa ("example.com/a") başına https:// eklenir.
    /// Geçerli adres döner; değilse null.
    /// </summary>
    public static string? TryNormalizeUrl(string? text)
    {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        if (CheckUrl(value) != UrlCheck.Invalid) return value;
        if (value.Contains(':') || value.Contains('\\') || value.Any(char.IsWhiteSpace) || !value.Contains('.') || value.StartsWith('.'))
            return null;
        var withScheme = "https://" + value;
        return CheckUrl(withScheme) == UrlCheck.Allowed ? withScheme : null;
    }

    /// <summary>Tuş öğesi: 1–<see cref="MaxChords"/> birleşim, her biri ayrıştırılabilir (tek tuş da olur: "F5").</summary>
    public static bool ValidKeys(IReadOnlyList<string>? chords) =>
        chords is { Count: > 0 and <= MaxChords } && chords.All(c => Hotkey.TryParse(c, out _, requireModifier: false));

    /// <summary>Metin öğesi: boş değil, en çok <see cref="MaxTextLength"/> karakter.</summary>
    public static bool ValidText(string? text) => !string.IsNullOrEmpty(text) && text.Length <= MaxTextLength;

    /// <summary>
    /// Öğe çalıştırılabilir mi: tür tanınıyor, kapalı (onaylanmamış) değil ve içeriği geçerli. Tanınmayan tür, geçersiz adres
    /// (elle düzenlenmiş dosyadaki "C:\x.exe" dahil) ve sınırı aşan öğe asla çalışmaz.
    /// </summary>
    public static bool IsRunnable(LauncherAction action) => !action.Disabled && action.Type switch
    {
        RingActionTypes.Url => CheckUrl(action.Url) != UrlCheck.Invalid,
        RingActionTypes.Command => !string.IsNullOrWhiteSpace(action.Command),
        RingActionTypes.Keys => ValidKeys(action.Keys),
        RingActionTypes.Text => ValidText(action.Text),
        RingActionTypes.App => RingAppIds.IsKnown(action.App),
        _ => false,
    };

    /// <summary>
    /// İçe aktarılan öğe (paylaşılan halka): komut, tuş ve metin öğeleri ile http/https/mailto dışındaki adresler kullanıcı
    /// tek tek görüp onaylayana dek kapalı gelir. Kapattıysa true.
    /// </summary>
    public static bool MarkImported(LauncherAction action)
    {
        var risky = action.Type switch
        {
            RingActionTypes.Command or RingActionTypes.Keys or RingActionTypes.Text => true,
            RingActionTypes.Url => CheckUrl(action.Url) != UrlCheck.Allowed,
            _ => false,
        };
        if (!risky || action.Disabled) return false;
        action.Disabled = true;
        return true;
    }
}

/// <summary>
/// Halka kısayolu önerisi (Store: varsayılanı kapalı ve atanmamış; kullanıcı açınca önerilir). Win+, F12 ve Ctrl+Space
/// önerilmez. Akış: Suggest → Set(Ring) → Apply → Failures.ContainsKey(Ring) ise denenen listeye ekleyip yeniden Suggest.
/// </summary>
public static class RingHotkey
{
    /// <summary>Sırayla denenen öneriler (R: "Ring"; K, J, Y yedek).</summary>
    public static IReadOnlyList<string> Candidates => ["Ctrl+Alt+R", "Ctrl+Alt+K", "Ctrl+Alt+J", "Ctrl+Alt+Y"];

    /// <summary>Öneri olabilir mi: değiştiricili, Win'siz, F12 ve Ctrl+Space değil.</summary>
    public static bool Suggestable(string? text) =>
        Hotkey.TryParse(text, out var hk) && !hk.Modifiers.HasFlag(HotkeyModifiers.Win) &&
        !string.Equals(hk.Key, "F12", StringComparison.OrdinalIgnoreCase) &&
        !(hk.Modifiers == HotkeyModifiers.Ctrl && string.Equals(hk.Key, "Space", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// İlk boş öneri; hepsi kullanılıyorsa null. Kullanılan: diğer eylemlerin kısayolları (kaydedilemeseler de atanmışlardır)
    /// ve daha yeni sürümün yazdığı kısayollar (<see cref="HotkeySettings.Extra"/>). <paramref name="failures"/> içinde
    /// yalnızca Ring'in kaydı başarısızsa şu anki halka kısayolu atlanır; başka eylemlerin hataları öneriyi etkilemez.
    /// <paramref name="tried"/>: bu akışta denenip kaydedilemeyenler.
    /// </summary>
    public static string? Suggest(HotkeySettings settings, IReadOnlyDictionary<HotkeyAction, string>? failures = null,
        IEnumerable<string>? tried = null)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in Enum.GetValues<HotkeyAction>())
            if (action != HotkeyAction.Ring && Normalize(settings.Get(action)) is { } text) used.Add(text);
        if (settings.Extra is { } extra)
            foreach (var value in extra.Values)
                if (value.ValueKind == System.Text.Json.JsonValueKind.String && Normalize(value.GetString()) is { } text) used.Add(text);
        foreach (var text in tried ?? [])
            if (Normalize(text) is { } t) used.Add(t);
        if (failures?.ContainsKey(HotkeyAction.Ring) == true && Normalize(settings.Ring) is { } failed) used.Add(failed);

        return Candidates.FirstOrDefault(c => Suggestable(c) && !used.Contains(Normalize(c)!));
    }

    private static string? Normalize(string? text) => Hotkey.TryParse(text, out var hk) ? hk.ToString() : null;
}
