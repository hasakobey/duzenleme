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

    /// <summary>Tuşlara bas (en çok 8 birleşim; yalnızca kısayolla açılan halkadan).</summary>
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

    // Kabukta kod çalıştıran, uygulama kuran, yerel/uzak dosyayı açan ya da uzak aramaya açılan şemalar: onayla da kabul
    // edilmez ("bir kez onay" yalnızca öğe eklenirken sorulur, sonra her seçimde sormadan açılır).
    private static readonly HashSet<string> BlockedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "file", "javascript", "vbscript", "data", "jar", "ms-msdt", "search-ms", "search", "shell",
        "ms-officecmd", "ms-appinstaller", "hcp", "its", "ms-its", "mk", "res", "mhtml", "ms-cxh", "ms-cxh-full", "jnlp",
    };

    // Kendisi değiştirici olan tuş adları: tuş öğesinde asıl tuş olamaz (gönderilecek birleşim yalnızca değiştirici kalırdı).
    private static readonly HashSet<string> ModifierKeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "LeftShift", "RightShift", "LeftCtrl", "RightCtrl", "LeftAlt", "RightAlt", "LWin", "RWin", "System",
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
        // Denetim ve biçim karakterleri (ör. U+202E sağdan sola çevirme) adresi gizleyebilir.
        if (value.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format)) return false;
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
    /// Kullanıcının yazdığı adres: şeması yoksa ve bir alan adına benziyorsa ("example.com/a") başına https:// eklenir;
    /// sunucu:bağlantı noktası ("localhost:3000", "example.com:8080/x") şema sanılmaz, yerel sunucuya http://, ötekine
    /// https:// eklenir. Geçerli adres döner; değilse null.
    /// </summary>
    public static string? TryNormalizeUrl(string? text)
    {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        if (HostWithPort(value) is { } host)
        {
            var scheme = IsLoopback(host) ? "http://" : "https://";
            return CheckUrl(scheme + value) == UrlCheck.Allowed ? scheme + value : null;
        }
        if (CheckUrl(value) != UrlCheck.Invalid) return value;
        if (value.Contains(':') || value.Contains('\\') || value.Any(char.IsWhiteSpace) || !value.Contains('.') || value.StartsWith('.'))
            return null;
        var withScheme = "https://" + value;
        return CheckUrl(withScheme) == UrlCheck.Allowed ? withScheme : null;
    }

    /// <summary>
    /// "sunucu:sayı…" biçimindeyse sunucu adı ("localhost", "example.com", "192.168.1.1"): iki noktadan sonrası rakamla
    /// başlıyorsa bu bir bağlantı noktasıdır, önündeki şema değildir. Sunucu adı yalnızca harf, rakam, nokta ve tire olabilir;
    /// gerçek bir şema ("mailto:1@x", "tel:123") nokta içermediğinden ve localhost olmadığından dokunulmaz. Değilse null.
    /// </summary>
    private static string? HostWithPort(string value)
    {
        var colon = value.IndexOf(':');
        if (colon <= 0 || colon + 1 >= value.Length || !char.IsAsciiDigit(value[colon + 1])) return null;
        var host = value[..colon];
        if (!host.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-')) return null;
        return host.Contains('.') || host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? host : null;
    }

    private static bool IsLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.StartsWith("127.", StringComparison.Ordinal);

    /// <summary>
    /// Tuş öğesi: 1–<see cref="MaxChords"/> birleşim, her biri ayrıştırılabilir (tek tuş da olur: "F5"); asıl tuş biçimce
    /// geçerli bir ad (<see cref="Hotkey.IsWellFormedKeyName"/>: "12", "A,B" olmaz) ve kendisi değiştirici değil
    /// ("LeftShift" olmaz). <paramref name="keyResolves"/> verilirse tuş adı onunla da çözülmeli (gönderenin kendi çözücüsü;
    /// Desktop/KeySender.CanSend bunu yapar): Core WPF'in tuş tablosunu bilmez, "Ctrl+Foo" ancak onunla elenir.
    /// </summary>
    public static bool ValidKeys(IReadOnlyList<string>? chords, Func<string, bool>? keyResolves = null) =>
        chords is { Count: > 0 and <= MaxChords } && chords.All(c =>
            Hotkey.TryParse(c, out var chord, requireModifier: false) && Hotkey.IsWellFormedKeyName(chord.Key) &&
            !ModifierKeyNames.Contains(chord.Key) && (keyResolves is null || keyResolves(chord.Key)));

    /// <summary>Metin öğesi: boş değil, en çok <see cref="MaxTextLength"/> karakter.</summary>
    public static bool ValidText(string? text) => !string.IsNullOrEmpty(text) && text.Length <= MaxTextLength;

    // Yolun sonunu belli eden uzantılar: "C:\Program Files\x.exe -f" tırnaksız da ayrılır.
    private static readonly string[] ProgramExtensions = [".exe", ".com", ".bat", ".cmd", ".msc", ".lnk", ".ps1", ".vbs", ".appref-ms"];

    /// <summary>
    /// Komut öğesinin "Program ya da komut" metnini program ve bağımsız değişkenlere ayırır (Windows'un komut satırı gibi):
    /// tırnaklı ilk parça program; tırnaksızsa metnin tamamı (var olan bir dosyaysa, boşluklu yol), sonra program uzantısıyla
    /// biten ilk parça ("C:\Program Files\x.exe -f"), sonra program uzantısıyla biten boşluklu tam yol ("C:\Program Files\x.exe"),
    /// sonra var olan en kısa boşluklu ön ek, yoksa ilk boşluğa kadarı ("notepad C:\a.txt", "ping 8.8.8.8").
    /// <paramref name="exists"/> dosya denetimidir (yalnızca arka planda; ad için null: diske dokunmaz).
    /// </summary>
    public static (string Program, string Arguments) SplitCommand(string? command, Func<string, bool>? exists = null)
    {
        var text = command?.Trim() ?? "";
        if (text.Length == 0) return ("", "");
        if (text[0] == '"')
        {
            var close = text.IndexOf('"', 1);
            return close < 0 ? (text.Trim('"').Trim(), "") : (text[1..close].Trim(), text[(close + 1)..].Trim());
        }
        if (exists?.Invoke(text) == true) return (text, "");
        for (var space = text.IndexOf(' '); space > 0; space = text.IndexOf(' ', space + 1))
        {
            var head = text[..space];
            if (EndsWithProgramExtension(head)) return (head, text[(space + 1)..].Trim());
        }
        // Boşluklu tam yol, bağımsız değişkensiz: ilk parça bir yol başlangıcıysa ("C:\Program", "\\sunucu\…").
        var firstSpace = text.IndexOf(' ');
        if (firstSpace > 0 && EndsWithProgramExtension(text) && text[..firstSpace].IndexOfAny(['\\', '/']) >= 0) return (text, "");
        if (exists is not null)
            for (var space = text.IndexOf(' '); space > 0; space = text.IndexOf(' ', space + 1))
                if (exists(text[..space])) return (text[..space], text[(space + 1)..].Trim());
        return firstSpace < 0 ? (text, "") : (text[..firstSpace], text[(firstSpace + 1)..].Trim());
    }

    private static bool EndsWithProgramExtension(string text) =>
        ProgramExtensions.Any(e => text.EndsWith(e, StringComparison.OrdinalIgnoreCase));

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
    /// İçe aktarılan öğe (paylaşılan halka) kullanıcı tek tek görüp onaylayana dek kapalı gelir; yalnızca http/https/mailto
    /// adresi ve bu sürümün tanıdığı NestDesk eylemi açık kalır. Komut, tuş ve metin öğeleri, başka şemalı adresler,
    /// tanınmayan türler ve kimlikler (daha yeni bir sürüm onları tanıyıp onaysız çalıştırmasın) ve bu sürümün bilmediği
    /// alan taşıyan öğeler (<see cref="LauncherAction.Extra"/>) kapalı gelir. Kapattıysa true.
    /// </summary>
    public static bool MarkImported(LauncherAction action)
    {
        var safe = action.Extra is not { Count: > 0 } && action.Type switch
        {
            RingActionTypes.Url => CheckUrl(action.Url) == UrlCheck.Allowed,
            RingActionTypes.App => RingAppIds.IsKnown(action.App),
            _ => false,
        };
        if (safe || action.Disabled) return false;
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

    /// <summary><see cref="TurnOn"/>'un sonucu: atanan kısayol (yoksa null), son kayıt hatası ve denenip kaydedilemeyenler.</summary>
    public sealed record TurnOnResult(string? Hotkey, string? Failure, IReadOnlyList<string> Tried);

    /// <summary>
    /// "Tuşla imlecin yanında açılsın" akışı (saf; testlenir): <see cref="Suggest"/> → Set(Ring) → <paramref name="apply"/> (kaydeder,
    /// kaydedilemeyenleri döner) → Ring kaydedilemediyse denenenlere eklenip sıradaki öneri. Yalnızca Ring'in hatasına bakılır
    /// (başka eylemin kaydedilememesi öneriyi reddetmez). Hiçbiri kaydedilemezse halka kapalı kalır (Set(Ring, "") ve bir kez
    /// daha <paramref name="apply"/>) ve son hata döner.
    /// </summary>
    public static TurnOnResult TurnOn(HotkeySettings settings, Func<HotkeySettings, IReadOnlyDictionary<HotkeyAction, string>> apply)
    {
        var tried = new List<string>();
        string? failure = null;
        IReadOnlyDictionary<HotkeyAction, string>? failures = null;
        while (tried.Count < Candidates.Count && Suggest(settings, failures, tried) is { } candidate)
        {
            settings.Set(HotkeyAction.Ring, candidate);
            failures = apply(settings);
            if (!failures.TryGetValue(HotkeyAction.Ring, out var why)) return new TurnOnResult(candidate, null, tried);
            failure = why;
            tried.Add(candidate);
        }
        settings.Set(HotkeyAction.Ring, "");
        apply(settings);
        return new TurnOnResult(null, failure, tried);
    }

    /// <summary>
    /// Ölçüm ve arayüz denemesi: NESTDESK_TEST_RING=1 yalnızca test masaüstünde (--desktop) halka yolunu açar; genel kısayol
    /// kaydedilmez (WM_HOTKEY id 8 kısayol atanmamışken de işlenir, açılır halka önceden kurulur). Gerçek örnekte yok sayılır.
    /// </summary>
    public static bool TestOverride(bool isTestDesktop, string? value) => isTestDesktop && value?.Trim() == "1";

    private static string? Normalize(string? text) => Hotkey.TryParse(text, out var hk) ? hk.ToString() : null;
}

/// <summary>
/// Açılır halkanın hedefi (saf; testlenir): seçilen halka (<see cref="AppSettings.RingWidgetId"/>: bir halka widget'ının kimliği,
/// NestDesk halkasının kimliği ya da <see cref="HubId"/>), yoksa masaüstündeki ilk halka widget'ı, o da yoksa NestDesk halkası
/// (<see cref="AppSettings.Hub"/>; henüz kurulmadıysa <see cref="Target"/> <see cref="EnsureHub"/> ile kurar).
/// </summary>
public static class RingTargets
{
    /// <summary><see cref="AppSettings.RingWidgetId"/>'de NestDesk halkasının takma adı.</summary>
    public const string HubId = "hub";

    /// <summary>Halka menüler (verilen yapılandırmalardan), sırayla.</summary>
    public static List<WidgetConfig> All(IEnumerable<WidgetConfig> widgets) =>
        widgets.Where(w => WidgetVariants.Is(w, WidgetVariants.Ring)).ToList();

    /// <summary>Kimliği <paramref name="id"/> olan halka; bulunamazsa (silindi, halka değil, boş) ilk halka; hiç yoksa null.</summary>
    public static WidgetConfig? Pick(IEnumerable<WidgetConfig> widgets, string? id)
    {
        var rings = All(widgets);
        return rings.FirstOrDefault(w => id is not null && string.Equals(w.Id, id, StringComparison.Ordinal)) ?? rings.FirstOrDefault();
    }

    /// <summary>
    /// Açılır halkanın açacağı halka: <c>"hub"</c> ya da NestDesk halkasının kimliği → NestDesk halkası; bir halka widget'ının
    /// kimliği → o; yoksa ilk halka widget'ı; masaüstünde halka widget'ı yoksa NestDesk halkası. NestDesk halkası gerekiyor
    /// ama henüz kurulmadıysa null (<see cref="Target"/> kurar).
    /// </summary>
    public static WidgetConfig? Pick(AppSettings settings)
    {
        var id = settings.RingWidgetId;
        var hub = settings.Hub is { } h && WidgetVariants.Is(h, WidgetVariants.Ring) ? h : null;
        if (string.Equals(id, HubId, StringComparison.Ordinal) || (hub is not null && string.Equals(id, hub.Id, StringComparison.Ordinal)))
            return hub;
        return Pick(settings.Widgets.Where(w => w is not null), id) ?? hub;
    }

    /// <summary>Açılır halkanın açacağı halka; gerekiyorsa NestDesk halkasını kurar (ayara yazılır; kaydetmek çağıranın işi).</summary>
    public static WidgetConfig Target(AppSettings settings) => Pick(settings) ?? EnsureHub(settings);

    /// <summary>
    /// Halka NestDesk halkası mı (<see cref="AppSettings.Hub"/>; ayardaki halka olmayan merkezin yerine bu oturumda kullanılan
    /// geçici halka da: bkz. <see cref="EnsureHub"/>)?
    /// </summary>
    public static bool IsHub(AppSettings settings, WidgetConfig ring) =>
        ReferenceEquals(settings.Hub, ring) || (Stand.TryGetValue(settings, out var stand) && ReferenceEquals(stand, ring));

    /// <summary>Ayardaki merkez bu sürümün tanımadığı bir biçimdeyken (daha yeni sürüm) bu oturumda yerine kullanılan halka.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<AppSettings, WidgetConfig> Stand = new();

    /// <summary>
    /// NestDesk halkası yoksa halka tohumundan kurar: "{0} halkası" adıyla, tohumun NestDesk eylemleriyle (Yeni not, Windows
    /// masaüstüne göz at, Widget ekle, NestDesk'i aç). Ad kullanıcı verisidir: kurulurken bir kez arayüz dilinde yazılır. Kurulanı
    /// ya da var olanı döner. Ayarda halka olmayan bir merkez varsa (daha yeni sürümün biçimi) ona asla yazılmaz, olduğu gibi
    /// korunur: bu oturumda yerine ayara yazılmayan bir halka kullanılır (her çağrıda aynısı).
    /// </summary>
    public static WidgetConfig EnsureHub(AppSettings settings)
    {
        if (settings.Hub is { } hub)
            return WidgetVariants.Is(hub, WidgetVariants.Ring) ? hub : Stand.GetValue(settings, _ => NewHub());
        var seed = NewHub();
        settings.Hub = seed;
        return seed;
    }

    /// <summary>
    /// "Halkayı düzenle"nin götüreceği yer (iş anında, kimlikle): NestDesk halkası kendi penceresinde; masaüstündeki halka widget'ı
    /// (ayardaki şimdiki nesnesi: "Geri al" kaldırılan widget'ı yeni bir nesneyle geri getirir) Düzenle panelinde; ikisi de
    /// değilse (widget kaldırıldı) hiçbir yer: kopuk bir kopyayı düzenlemek değişiklikleri kaybettirirdi.
    /// </summary>
    public static (RingEditPlace Place, WidgetConfig? Ring) EditPlace(AppSettings settings, WidgetConfig ring)
    {
        if (IsHub(settings, ring)) return (RingEditPlace.Hub, ring);
        var live = settings.Widgets.FirstOrDefault(w => w is not null && string.Equals(w.Id, ring.Id, StringComparison.Ordinal));
        if (live is not null && WidgetVariants.Is(live, WidgetVariants.Ring)) return (RingEditPlace.Widget, live);
        if (settings.Hub is { } hub && string.Equals(hub.Id, ring.Id, StringComparison.Ordinal) && WidgetVariants.Is(hub, WidgetVariants.Ring))
            return (RingEditPlace.Hub, hub);
        return (RingEditPlace.Gone, null);
    }

    /// <summary>Yeni bir NestDesk halkası (ayara yazılmaz): karşılamanın önizlemesi ve <see cref="EnsureHub"/>.</summary>
    public static WidgetConfig NewHub()
    {
        var seed = WidgetSeeds.Create(WidgetSeeds.Ring)!;
        seed.Title = L.F("{0} halkası", AppInfo.Name);
        return seed;
    }

    /// <summary>Kısayol <paramref name="ring"/> halkasını mı açar (widget menüsündeki seçenek yalnızca o halkada işaretli)?</summary>
    public static bool Opens(IEnumerable<WidgetConfig> widgets, string? id, WidgetConfig ring) =>
        Pick(widgets, id) is { } target && string.Equals(target.Id, ring.Id, StringComparison.Ordinal);

    /// <summary>Açılır halka <paramref name="ring"/> halkasını mı açar (NestDesk halkası dahil)?</summary>
    public static bool Opens(AppSettings settings, WidgetConfig ring) =>
        Pick(settings) is { } target && string.Equals(target.Id, ring.Id, StringComparison.Ordinal);
}

/// <summary>"Halkayı düzenle"nin yeri (yalnızca çalışma zamanı; bkz. <see cref="RingTargets.EditPlace"/>).</summary>
public enum RingEditPlace { Gone, Hub, Widget }

/// <summary>Açılır halkayı neyin açtığı (yalnızca çalışma zamanı; hiçbir yere yazılmaz).</summary>
public enum RingOrigin { Hotkey, DoubleClick, Tray, Command }

/// <summary>Hangi öğenin hangi açılışta çalıştığı (saf; testlenir).</summary>
public static class RingAvailability
{
    /// <summary>
    /// Tuş ve metin öğeleri önceki pencereye gönderilir: yalnızca kısayolla açılan halkada (çift tıklama ve tepsi halkayı
    /// masaüstünden ya da görev çubuğundan açar; önceki pencere yazılacak yer değildir). Öbürleri her açılışta çalışır.
    /// </summary>
    public static bool Available(string? actionType, RingOrigin origin) =>
        origin == RingOrigin.Hotkey || !RingActionTypes.NeedsTargetWindow(actionType);

    /// <summary>
    /// Vazgeçince (Esc, sağ tık, ortaya basmak) önceki pencere öne gelsin mi: yalnızca kısayolla açılan halkada (kullanıcı bir
    /// pencerede çalışıyordu). Çift tıklama ve tepsi masaüstünden ya da görev çubuğundan açar: oraya dönmek görev çubuğunu ya da
    /// masaüstünü öne getirirdi.
    /// </summary>
    public static bool RestoresPrevious(RingOrigin origin) => origin == RingOrigin.Hotkey;
}
