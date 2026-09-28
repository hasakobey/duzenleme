using System.Collections.Concurrent;
using System.Globalization;

namespace Duzenleme.Localization;

/// <summary>Arayüz dili. Kalıcı değildir (ayar metindir: <see cref="Duzenleme.Core.AppSettings.Language"/>).</summary>
public enum Lang { Tr, En }

/// <summary>
/// Arayüz metinleri (gettext tarzı): anahtar, arayüzde görünen Türkçe metnin kendisidir. İngilizce karşılıklar
/// <c>Localization/En.*.cs</c> tablolarındadır. Türkçede metin olduğu gibi döner (bellek ayırmaz).
/// <para>Kurallar: T/F/P/N/Variants yalnızca metin sabiti (literal) alır (<c>$"…"</c>, değişken ya da birleştirme değil);
/// değişen parçalar <c>{0}</c>, <c>{1}</c> olur. <c>static readonly</c> alanlarda, <c>static … { get; } =</c>
/// başlatıcılarında ve <c>const</c>'larda L çağrılmaz (dil o an neyse donar); yalnızca <see cref="N"/> serbesttir.
/// <c>LocalizationTests</c> bunları denetler.</para>
/// </summary>
public static class L
{
    private sealed record State(Lang Lang, bool Pseudo);

    private static State s_state = new(Lang.Tr, false);          // testlerde belirlenimci: Türkçe
    private static readonly AsyncLocal<State?> s_override = new();  // testler: using var _ = L.Use(Lang.En);
    private static CultureInfo? s_systemUiCulture;

    private static State Active => s_override.Value ?? s_state;

    /// <summary>Şu anki arayüz dili.</summary>
    public static Lang Current => Active.Lang;

    /// <summary>Sahte dil (DUZENLEME_LANG=pseudo): çevrilen her metin ⟦Ŝáħţé …⟧ biçiminde görünür; çevrilmemiş Türkçe
    /// ve sığmayan metin ekran görüntüsünde hemen fark edilir.</summary>
    public static bool IsPseudo => Active.Pseudo;

    /// <summary>Windows'un görüntüleme dili (ilk okumadaki UI kültürü): Türkçe → Tr, diğer her dil → En.</summary>
    public static Lang SystemLanguage => SystemUiCulture.TwoLetterISOLanguageName == "tr" ? Lang.Tr : Lang.En;

    private static CultureInfo SystemUiCulture => s_systemUiCulture ??= CultureInfo.CurrentUICulture;

    /// <summary>
    /// Dili belirler; her pencere ve widget'tan önce çağrılır (App.OnStartup'ta önce null ile, ayarlar okununca
    /// ayardaki değerle). setting: null/"" = Windows ile aynı, "tr", "en". NESTDESK_LANG / DUZENLEME_LANG = tr|en|pseudo
    /// ayarı ezer (UI testleri). Yalnızca UI kültürü değişir, CurrentCulture hiç değişmez: tarih/sayı biçimi Windows'un
    /// bölge ayarında kalır ve desktop.ini kod sayfası (FolderIconService) bozulmaz.
    /// </summary>
    public static void Init(string? setting)
    {
        var system = SystemLanguage;   // ilk çağrıda Windows'un UI kültürünü yakalar (aşağıda değiştirilmeden önce)
        var state = Resolve(setting, EnvironmentLanguage(), system);
        s_state = state;
        // Windows'unkinden farklı dil seçildiyse WPF'in kendi metinleri (TextBox menüsü, MessageBox düğmeleri) de o dilde olsun.
        var ui = state.Lang == system ? SystemUiCulture : state.Lang == Lang.Tr ? Turkish : EnglishUs;
        CultureInfo.DefaultThreadCurrentUICulture = state.Lang == system ? null : ui;
        CultureInfo.CurrentUICulture = ui;
    }

    /// <summary>Bu ayarla yeniden başlatınca dil değişir mi? (Ortam değişkeni ayarı eziyorsa değişmez.)</summary>
    public static bool NeedsRestart(string? setting) => Resolve(setting, EnvironmentLanguage(), SystemLanguage) != s_state;

    /// <summary>Ayar + ortam değişkeni + Windows dili → etkin dil. Saf; testlenir.</summary>
    internal static (Lang Lang, bool Pseudo) ResolveFor(string? setting, string? environment, Lang system)
    {
        var state = Resolve(setting, environment, system);
        return (state.Lang, state.Pseudo);
    }

    private static State Resolve(string? setting, string? environment, Lang system)
    {
        switch (environment?.Trim().ToLowerInvariant())
        {
            case "tr": return new(Lang.Tr, false);
            case "en": return new(Lang.En, false);
            case "pseudo": return new(Lang.En, true);
        }
        return setting?.Trim().ToLowerInvariant() switch
        {
            "tr" => new(Lang.Tr, false),
            "en" => new(Lang.En, false),
            _ => new(system, false),
        };
    }

    /// <summary>NESTDESK_LANG, yoksa DUZENLEME_LANG (boş değer yok sayılır; bkz. <see cref="Duzenleme.Core.AppEnvironment"/>).</summary>
    private static string? EnvironmentLanguage() => Duzenleme.Core.AppEnvironment.Get("LANG");

    /// <summary>Yalnızca testler: bu akışta (AsyncLocal) dili geçici olarak değiştirir. <c>using var _ = L.Use(Lang.En);</c></summary>
    public static IDisposable Use(Lang lang, bool pseudo = false)
    {
        var previous = s_override.Value;
        s_override.Value = new State(lang, pseudo);
        return new Restore(previous);
    }

    private sealed class Restore(State? previous) : IDisposable
    {
        public void Dispose() => s_override.Value = previous;
    }

    // ---- Kültür ----

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CultureInfo EnglishUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly StringComparer TurkishSorter = StringComparer.Create(Turkish, ignoreCase: true);
    private static readonly ConcurrentDictionary<string, StringComparer> Sorters = new();

    /// <summary>
    /// Ay/gün adları ve sayı biçimi için kültür: Türkçede tr-TR; İngilizcede Windows'un bölge biçimi İngilizceyse o
    /// (en-GB: gün önce), değilse en-US. <see cref="CultureInfo.CurrentCulture"/> hiç değiştirilmez.
    /// </summary>
    public static CultureInfo Culture
    {
        get
        {
            if (Current == Lang.Tr) return Turkish;
            var regional = CultureInfo.CurrentCulture;
            return regional.TwoLetterISOLanguageName == "en" ? regional : EnglishUs;
        }
    }

    /// <summary>Arayüz diline göre büyük/küçük harf duyarsız sıralama (Türkçede tr-TR: "Çiçek" C ile D arasında).</summary>
    public static StringComparer Sorter
    {
        get
        {
            var culture = Culture;
            return ReferenceEquals(culture, Turkish)
                ? TurkishSorter
                : Sorters.GetOrAdd(culture.Name, _ => StringComparer.Create(culture, ignoreCase: true));
        }
    }

    /// <summary>Saat 24 saatlik mi? Dilden değil Windows'un bölge ayarından (kısa saat biçiminde "H" var mı).</summary>
    public static bool Uses24Hour => CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('H');

    // ---- Metinler ----

    /// <summary>Metni arayüz diline çevirir. Yalnızca metin sabiti alır.</summary>
    public static string T(string tr) => Translate(tr, null);

    /// <summary>Aynı Türkçe metnin bağlama göre farklı çevirisi (ör. <c>T("Gizli", "simge")</c> → "Private"). Yalnızca metin sabitleri.</summary>
    public static string T(string tr, string context) => Translate(tr, context);

    /// <summary>Çevirip biçimler: <c>F("{0} dosya taşındı.", n)</c>. Biçim kültürü <see cref="Culture"/>.</summary>
    public static string F(string tr, params object?[] args) => string.Format(Culture, Translate(tr, null), args);

    /// <summary>
    /// Sayıya göre tekil/çoğul: {0} = n, {1}… = rest. Türkçede tek biçim; İngilizcede n == 1 ise tekil (En), değilse
    /// çoğul (EnPlural). Ör. <c>P(n, "{0} dosya → {1}", klasör)</c>.
    /// </summary>
    public static string P(long n, string tr, params object?[] rest)
    {
        var state = Active;
        var text = tr;
        if (state.Lang != Lang.Tr)
        {
            var entry = En.Find(tr, null);
            if (entry is null) ReportMissing(tr, null);
            text = PluralForm(n, tr, entry);
            if (state.Pseudo) text = PseudoText.Apply(text, entry is null);
        }
        return string.Format(Culture, text, [n, .. rest]);
    }

    /// <summary>İngilizce tekil/çoğul: yalnızca 1 tekil ("1 file", "0 files", "2 files"); çeviri yoksa Türkçe metin.</summary>
    internal static string PluralForm(long n, string tr, En.Entry? entry) =>
        entry is not { } e ? tr : n == 1 ? e.En : e.EnPlural ?? e.En;

    /// <summary>
    /// Metni tarayıcı için "çevrilecek" diye işaretler ve olduğu gibi döndürür: tablolarda (static alanlarda) Türkçe
    /// saklanır, gösterirken <see cref="Dyn"/> ile çevrilir.
    /// </summary>
    public static string N(string tr, string? context = null) => tr;

    /// <summary><see cref="N"/> ile işaretlenmiş bir değeri çalışma anında çevirir (değişken alabilir).</summary>
    public static string Dyn(string markedKey, string? context = null) => Translate(markedKey, context);

    /// <summary>Tüm dillerdeki karşılıklar [tr, en]: yalnızca eski/diğer dilde kaydedilmiş adları tanımak için
    /// (ör. "Düzen uygulanmadan önce" yedeği dil değişince de yerine yazılsın).</summary>
    public static IReadOnlyList<string> Variants(string tr)
    {
        var en = En.Find(tr, null)?.En;
        return en is null || en == tr ? [tr] : [tr, en];
    }

    /// <summary>Sıralı birleştirme: "", "A", "A ve B", "A, B ve C" / "A and B", "A, B and C".</summary>
    public static string Join(IReadOnlyList<string> items)
    {
        var and = Current == Lang.Tr ? " ve " : " and ";
        return items.Count switch
        {
            0 => "",
            1 => items[0],
            _ => string.Join(", ", items.Take(items.Count - 1)) + and + items[^1],
        };
    }

    /// <summary>Yüzde: Türkçede "%70", İngilizcede "70%".</summary>
    public static string Percent(double fraction) => fraction.ToString("P0", Culture);

    /// <summary>Dil seçeneğinin adı, her zaman kendi dilinde: arayüzü anlamayan kullanıcı da dilini bulabilsin.</summary>
    public static string NativeName(Lang lang) => lang == Lang.Tr ? "Türkçe" : "English";

    /// <summary>"Windows ile aynı" seçeneği, Windows'un dilinde ve o dilin adıyla.</summary>
    public static string SystemChoiceName =>
        SystemLanguage == Lang.Tr ? "Windows ile aynı (Türkçe)" : "Same as Windows (English)";

    private static string Translate(string tr, string? context)
    {
        var state = Active;
        if (state.Lang == Lang.Tr) return tr;
        var entry = En.Find(tr, context);
        if (entry is null) ReportMissing(tr, context);
        var text = entry?.En ?? tr;
        return state.Pseudo ? PseudoText.Apply(text, entry is null) : text;
    }

    private static readonly ConcurrentDictionary<string, bool> Reported = new();

    /// <summary>Eksik çeviri: Türkçe metin gösterilir (hiç hata atılmaz), günlüğe bir kez yazılır. Testler eksikleri yakalar.</summary>
    private static void ReportMissing(string tr, string? context)
    {
        if (DebugLog.Enabled && Reported.TryAdd(context + "\u0001" + tr, true))
            DebugLog.Write($"l10n: çevirisi yok: \"{tr}\"" + (context is null ? "" : $" ({context})"));
    }
}
