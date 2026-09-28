using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace Duzenleme.Localization;

/// <summary>
/// İngilizce karşılıklar. Her alanın tablosu kendi dosyasındadır (<c>En.Common.cs</c>, <c>En.Settings.cs</c>…) ve
/// <c>[ModuleInitializer]</c> ile kaydolur: ortak bir kayıt dosyası yok, paralel çalışanlar aynı dosyaya dokunmaz.
/// Aynı (Tr, Context) iki dosyada aynı çeviriyle olabilir; farklı çeviri <c>LocalizationTests</c>'te hata verir
/// (çalışırken ilk kaydolan kullanılır).
/// </summary>
internal static partial class En
{
    /// <summary>
    /// Bir çeviri. Tr: arayüzdeki Türkçe metin (anahtar). EnPlural: yalnızca L.P ile kullanılan sayılı metinlerde, sayı 1
    /// değilken. Context: aynı Türkçe metnin başka anlamı (ör. "Gizli" + "simge" → "Private").
    /// </summary>
    internal readonly record struct Entry(string Tr, string En, string? EnPlural = null, string? Context = null);

    /// <summary>Kaydolan tablo ve kaynak dosyası (testlerin hata iletisi için).</summary>
    internal readonly record struct Table(string File, Entry[] Entries);

    private static readonly List<Table> s_tables = [];
    private static FrozenDictionary<string, Entry>? s_lookup;
    private static readonly object s_lock = new();

    /// <summary>Yalnızca <c>[ModuleInitializer]</c>'lardan çağrılır (derleme yüklenirken, ilk aramadan önce).</summary>
    internal static void Add(Entry[] entries, [CallerFilePath] string file = "")
    {
        lock (s_lock)
        {
            s_tables.Add(new Table(file, entries));
            s_lookup = null;
        }
    }

    /// <summary>Tüm tablolar (tarayıcı testi için).</summary>
    internal static IReadOnlyList<Table> Tables
    {
        get { lock (s_lock) return [.. s_tables]; }
    }

    internal static IEnumerable<Entry> All => Tables.SelectMany(t => t.Entries);

    internal static Entry? Find(string tr, string? context)
    {
        var lookup = s_lookup ?? Build();
        return lookup.TryGetValue(Key(tr, context), out var entry) ? entry : null;
    }

    private static string Key(string tr, string? context) => context is null ? tr : context + "\u0001" + tr;

    private static FrozenDictionary<string, Entry> Build()
    {
        lock (s_lock)
        {
            if (s_lookup is { } built) return built;
            var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in s_tables.SelectMany(t => t.Entries))
                map.TryAdd(Key(entry.Tr, entry.Context), entry);
            return s_lookup = map.ToFrozenDictionary(StringComparer.Ordinal);
        }
    }
}
