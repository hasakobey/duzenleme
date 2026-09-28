using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Duzenleme.Localization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Duzenleme.Tests;

/// <summary>
/// Arayüz metinleri tarayıcısı: src/Duzenleme altındaki C# (Roslyn) ve XAML (XDocument) dosyalarındaki L.T/F/P/N/Variants
/// ve {l:T '…'} kullanımlarını İngilizce tablolarla (Localization/En.*.cs) karşılaştırır. Hata iletileri dosya:satır verir.
/// </summary>
public class LocalizationTests
{
    /// <summary>Tabloya bakılan L yöntemleri (ilk metin argümanı anahtardır; P'de ikinci).</summary>
    private static readonly HashSet<string> KeyedMethods = ["T", "F", "P", "N", "Variants"];

    /// <summary>Dil o an neyse donduran yöntemler: static alan/özellik başlatıcısında ve const'ta yasak (N serbest).</summary>
    private static readonly HashSet<string> RuntimeMethods = ["T", "F", "P", "Dyn", "Variants", "Join", "Percent"];

    // ---------------------------------------------------------------- tablolar

    [Fact]
    public void Every_key_has_english()
    {
        var missing = Scan.Value.Uses
            .Where(u => En.Find(u.Key, u.Context) is null)
            .Select(u => $"{u.Where}: \"{u.Key}\"" + (u.Context is null ? "" : $" (bağlam: {u.Context})"))
            .ToList();
        Assert.True(missing.Count == 0, "İngilizcesi olmayan metinler (Localization/En.<Alan>.cs'e ekle):\n" + string.Join("\n", missing));
    }

    [Fact]
    public void No_unused_english()
    {
        var used = Scan.Value.Uses.Select(u => (u.Key, u.Context)).ToHashSet();
        var unused = En.Tables
            .SelectMany(t => t.Entries.Select(e => (Table: t, Entry: e)))
            .Where(x => !used.Contains((x.Entry.Tr, x.Entry.Context)))
            .Select(x => $"{Path.GetFileName(x.Table.File)}: \"{x.Entry.Tr}\"")
            .ToList();
        Assert.True(unused.Count == 0, "Kodda kullanılmayan çeviriler (metin değiştiyse tabloyu da güncelle):\n" + string.Join("\n", unused));
    }

    [Fact]
    public void Same_key_has_one_translation()
    {
        var problems = new List<string>();
        foreach (var group in En.Tables.SelectMany(t => t.Entries.Select(e => (File: Path.GetFileName(t.File), Entry: e)))
                     .GroupBy(x => (x.Entry.Tr, x.Entry.Context)))
        {
            if (group.Select(x => (x.Entry.En, x.Entry.EnPlural)).Distinct().Count() > 1)
                problems.Add($"\"{group.Key.Tr}\" farklı çevrilmiş: " + string.Join(" | ", group.Select(x => $"{x.File}: {x.Entry.En}")));
            foreach (var file in group.GroupBy(x => x.File).Where(f => f.Count() > 1))
                problems.Add($"{file.Key}: \"{group.Key.Tr}\" iki kez yazılmış");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Placeholders_match()
    {
        var problems = new List<string>();
        foreach (var table in En.Tables)
        {
            foreach (var e in table.Entries)
            {
                var where = $"{Path.GetFileName(table.File)}: \"{e.Tr}\"";
                if (e.Tr.Length == 0) { problems.Add($"{Path.GetFileName(table.File)}: boş anahtar"); continue; }
                var tr = Placeholders(e.Tr);
                foreach (var (label, en) in new[] { ("En", e.En), ("EnPlural", e.EnPlural) })
                {
                    if (en is null) continue;
                    if (en.Length == 0) { problems.Add($"{where}: {label} boş"); continue; }
                    if (!tr.SetEquals(Placeholders(en))) problems.Add($"{where}: {label} yer tutucuları farklı ({en})");
                    if (e.Tr.Count(c => c == '|') != en.Count(c => c == '|')) problems.Add($"{where}: {label} '|' sayısı farklı (dosya süzgeci)");
                    if (e.Tr.Count(c => c == '\n') != en.Count(c => c == '\n')) problems.Add($"{where}: {label} satır sayısı farklı");
                    if (char.IsWhiteSpace(e.Tr[0]) != char.IsWhiteSpace(en[0]) || char.IsWhiteSpace(e.Tr[^1]) != char.IsWhiteSpace(en[^1]))
                        problems.Add($"{where}: {label} baştaki/sondaki boşluk farklı");
                    if (en.IndexOfAny(['ı', 'İ', 'ğ', 'Ğ', 'ş', 'Ş']) >= 0) problems.Add($"{where}: {label} Türkçe harf içeriyor ({en})");
                    if (tr.Count > 0 && !Formats(en, tr)) problems.Add($"{where}: {label} string.Format ile biçimlenemiyor");
                }
                if (tr.Count > 0 && !Formats(e.Tr, tr)) problems.Add($"{where}: Türkçe metin string.Format ile biçimlenemiyor");
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Only_literal_keys_and_no_static_initializers()
    {
        Assert.True(Scan.Value.CallProblems.Count == 0, string.Join("\n", Scan.Value.CallProblems));
    }

    [Fact]
    public void No_legacy_name_in_ui_text()
    {
        var legacy = new Regex(@"Duzenleme|DUZENLEME_|\bDüzenleme\b");
        var texts = En.All.SelectMany(e => new[] { e.Tr, e.En, e.EnPlural }).OfType<string>()
            .Concat(Scan.Value.Uses.Select(u => u.Key));
        var found = texts.Where(t => legacy.IsMatch(t)).Distinct().ToList();
        Assert.True(found.Count == 0, $"Eski ad ({Core.AppInfo.FormerName}) arayüz metninde, AppInfo.Name kullan:\n" + string.Join("\n", found));
    }

    [Fact]
    public void Culture_safe_string_calls()
    {
        Assert.True(Scan.Value.CultureProblems.Count == 0,
            "Kültüre bağlı çağrı (Türkçe 'I' sorunu): ToUpperInvariant/ToLowerInvariant ya da StringComparison kullan:\n" +
            string.Join("\n", Scan.Value.CultureProblems));
    }

    [Fact]
    public void No_raw_turkish_ui_text()
    {
        Assert.True(Scan.Value.RawTexts.Count == 0,
            $"Çevrilmemiş arayüz metni ({Scan.Value.RawTexts.Count}): L.T/L.F/L.P ya da {{l:T '…'}} ile sar; veri ise satıra " +
            "\"// l10n: çevrilmez\" yaz:\n" + string.Join("\n", Scan.Value.RawTexts));
    }

    [Fact]
    public void Scanner_sees_converted_sources()
    {
        // Tarayıcı gerçekten okuyor mu: dönüştürülmüş yerlerden bilinenler (XAML ve C#).
        var uses = Scan.Value.Uses;
        Assert.Contains(uses, u => u.Key == "Ana sayfa" && u.Where.StartsWith("MainWindow.xaml:"));
        Assert.Contains(uses, u => u.Key == "Widget'lar" && u.Where.StartsWith("MainWindow.xaml:"));
        Assert.Contains(uses, u => u.Key == "Dil / Language" && u.Where.StartsWith(@"Views\SettingsPage.xaml:"));
        Assert.Contains(uses, u => u.Key == "Düzen uygulanmadan önce" && u.Where.StartsWith(@"Core\LayoutBackup.cs:"));
        Assert.DoesNotContain(Scan.Value.RawTexts, r => r.StartsWith("MainWindow.xaml:") && r.Contains("Ana sayfa"));
    }

    [Fact]
    public void Scanner_flags_bad_calls_and_raw_text()
    {
        const string code = """
            class A
            {
                static readonly string X = L.T("a");
                static string Y { get; } = L.F("b {0}", 1);
                static string Z => L.T("c");
                static readonly Func<string> W = () => L.T("d");
                static readonly string N = L.N("e", "bağlam");
                string I { get; } = L.T("f");
                void M(string name, int n)
                {
                    L.T(name);
                    L.T($"g {name}");
                    L.P(n, "{0} h", name);
                    L.T("i", context: name);
                    _ = "abc".ToUpper() + char.ToLower('A') + string.Compare("a", "b", true) + "x".ToUpperInvariant();
                    var raw = "Türkçe metin";
                    DebugLog.Write($"günlük {name}");
                    var data = "ağaç"; // l10n: çevrilmez
                }
                [Obsolete("Eski yöntem, şunu kullan")] void O() { }
            }
            """;
        var result = new ScanResult();
        ScanCSharp(@"Views\Ornek.cs", code, result);

        Assert.Equal(new[] { "a", "b {0}", "c", "d", "e", "f", "{0} h" }, result.Uses.Select(u => u.Key));
        Assert.Equal("bağlam", result.Uses.Single(u => u.Key == "e").Context);
        Assert.Equal(5, result.CallProblems.Count);
        Assert.Contains(result.CallProblems, p => p.StartsWith(@"Views\Ornek.cs:3:") && p.Contains("static alan"));
        Assert.Contains(result.CallProblems, p => p.StartsWith(@"Views\Ornek.cs:4:") && p.Contains("static özellik"));
        Assert.Equal(2, result.CallProblems.Count(p => p.Contains("metin sabiti değil: ")
                                                         && (p.Contains(":11:") || p.Contains(":12:"))));
        Assert.Contains(result.CallProblems, p => p.Contains(":14:") && p.Contains("bağlamı"));
        Assert.Equal(3, result.CultureProblems.Count);
        Assert.Equal(@"Views\Ornek.cs:16: ""Türkçe metin""", Assert.Single(result.RawTexts));
    }

    [Fact]
    public void Scanner_reads_xaml()
    {
        const string xaml = """
            <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:l="clr-namespace:Duzenleme.Localization">
                <StackPanel ToolTip="{l:T 'Widget\'lar'}">
                    <TextBlock Text="Ham metin" />
                    <TextBlock Text="{Binding Name}" />
                    <TextBlock Text="{l:T 'bozuk}" />
                    <TextBlock>Öğe metni</TextBlock>
                    <Button Content="{l:T 'Gizli', Context=simge}" />
                </StackPanel>
            </UserControl>
            """;
        var result = new ScanResult();
        ScanXaml("Ornek.xaml", XDocument.Parse(xaml, LoadOptions.SetLineInfo), result);

        Assert.Equal(new[] { ("Widget'lar", (string?)null), ("Gizli", "simge") }, result.Uses.Select(u => (u.Key, u.Context)));
        Assert.Contains("Ornek.xaml:6:", Assert.Single(result.CallProblems));
        Assert.Equal(2, result.RawTexts.Count);
    }

    [Theory]
    [InlineData("{l:T Widget ekle}", "Widget ekle", null)]
    [InlineData("{l:T 'Widget\\'lar'}", "Widget'lar", null)]
    [InlineData("{l:T Widget\\'lar}", "Widget'lar", null)]
    [InlineData("{l:T 'Saat, not, bölme…'}", "Saat, not, bölme…", null)]
    [InlineData("{l:T Saat\\, not}", "Saat, not", null)]
    [InlineData("{l:T Text='İpucu: sağ tık → Görünüm'}", "İpucu: sağ tık → Görünüm", null)]
    [InlineData("{l:T 'Gizli', Context=simge}", "Gizli", "simge")]
    [InlineData("{l:T 'Açınca \"Kısayollar\" (x = y)'}", "Açınca \"Kısayollar\" (x = y)", null)]
    public void Xaml_markup_parser(string value, string text, string? context)
    {
        Assert.Equal<(string, string?)?>((text, context), ParseMarkup(value));
    }

    [Theory]
    [InlineData("{l:Tx}")]
    [InlineData("{l:T 'açık kalan}")]
    [InlineData("{l:T Bilinmeyen=x}")]
    [InlineData("{Binding Name}")]
    public void Xaml_markup_parser_rejects(string value) => Assert.Null(ParseMarkup(value));

    // ---------------------------------------------------------------- davranış

    [Fact]
    public void Turkish_is_default_and_returned_as_is()
    {
        Assert.Equal(Lang.Tr, L.Current);
        Assert.Equal("Widget ekle", L.T("Widget ekle"));
        Assert.Equal("Yeniden başlatılamadı: x", L.F("Yeniden başlatılamadı: {0}", "x"));
        Assert.Equal("tr-TR", L.Culture.Name);
    }

    [Fact]
    public void English_translation_and_fallback()
    {
        using var _ = L.Use(Lang.En);
        Assert.Equal(Lang.En, L.Current);
        Assert.Equal("Add widget", L.T("Widget ekle"));
        Assert.Equal("Widgets", L.T("Widget'lar"));
        Assert.Equal("Couldn't restart: x", L.F("Yeniden başlatılamadı: {0}", "x"));
        // Çevirisi olmayan metin Türkçe kalır (hata atılmaz).
        Assert.Equal("Tabloda olmayan metin", L.Dyn("Tabloda olmayan metin"));
        Assert.Equal("en", L.Culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void Language_override_is_per_flow()
    {
        using (L.Use(Lang.En)) Assert.Equal("Settings", L.T("Ayarlar"));
        Assert.Equal("Ayarlar", L.T("Ayarlar"));
    }

    [Fact]
    public void Plural_form_in_english()
    {
        var entry = new En.Entry("{0} dosya", "{0} file", "{0} files");
        Assert.Equal("{0} file", L.PluralForm(1, "{0} dosya", entry));
        Assert.Equal("{0} files", L.PluralForm(0, "{0} dosya", entry));
        Assert.Equal("{0} files", L.PluralForm(2, "{0} dosya", entry));
        Assert.Equal("{0} x", L.PluralForm(5, "{0} dosya", new En.Entry("{0} dosya", "{0} x")));
        Assert.Equal("{0} dosya", L.PluralForm(5, "{0} dosya", null));
        // Türkçede tek biçim; {0} = sayı, {1}… = diğerleri.
        Assert.Equal("3 dosya → PDF", L.P(3, "{0} dosya → {1}", "PDF"));
    }

    [Fact]
    public void Join_per_language()
    {
        Assert.Equal("", L.Join([]));
        Assert.Equal("A", L.Join(["A"]));
        Assert.Equal("A ve B", L.Join(["A", "B"]));
        Assert.Equal("A, B ve C", L.Join(["A", "B", "C"]));
        using var _ = L.Use(Lang.En);
        Assert.Equal("A and B", L.Join(["A", "B"]));
        Assert.Equal("A, B and C", L.Join(["A", "B", "C"]));
    }

    [Fact]
    public void Percent_per_language()
    {
        Assert.Equal("%70", L.Percent(0.7));
        using var _ = L.Use(Lang.En);
        Assert.Equal("70%", L.Percent(0.7));
    }

    [Fact]
    public void Turkish_sorter_orders_turkish_letters()
    {
        var sorted = new[] { "Dağ", "Çiçek", "Cam", "ördek", "Oyun", "ılık", "İz" }.OrderBy(x => x, L.Sorter).ToArray();
        Assert.Equal(new[] { "Cam", "Çiçek", "Dağ", "ılık", "İz", "Oyun", "ördek" }, sorted);
        Assert.True(L.Sorter.Equals("İZ", "iz"));
    }

    [Theory]
    [InlineData(null, null, Lang.Tr, Lang.Tr, false)]
    [InlineData(null, null, Lang.En, Lang.En, false)]
    [InlineData("", null, Lang.En, Lang.En, false)]
    [InlineData("en", null, Lang.Tr, Lang.En, false)]
    [InlineData(" EN ", null, Lang.Tr, Lang.En, false)]
    [InlineData("tr", null, Lang.En, Lang.Tr, false)]
    [InlineData("de", null, Lang.Tr, Lang.Tr, false)]          // tanınmayan dil (yeni sürümden kalma) → Windows ile aynı
    [InlineData("tr", "en", Lang.Tr, Lang.En, false)]           // ortam değişkeni ayarı ezer
    [InlineData("en", "tr", Lang.En, Lang.Tr, false)]
    [InlineData(null, "pseudo", Lang.Tr, Lang.En, true)]
    [InlineData("en", "bilinmeyen", Lang.Tr, Lang.En, false)]   // geçersiz ortam değişkeni yok sayılır
    public void Language_resolution(string? setting, string? environment, Lang system, Lang expected, bool pseudo)
    {
        Assert.Equal((expected, pseudo), L.ResolveFor(setting, environment, system));
    }

    [Fact]
    public void Pseudo_marks_translated_text_and_keeps_placeholders()
    {
        using var _ = L.Use(Lang.En, pseudo: true);
        var text = L.T("Widget ekle");
        Assert.StartsWith("⟦", text);
        Assert.EndsWith("⟧", text);
        Assert.DoesNotMatch("[A-Za-z]", text);
        Assert.True(text.Length > "Add widget".Length + 2);
        Assert.Contains("PDF", L.F("Yeniden başlatılamadı: {0}", "PDF"));
        Assert.StartsWith("⟦‼", L.Dyn("Tabloda olmayan metin"));

        // Dosya süzgeci: desenler bozulmaz.
        var filter = PseudoText.Apply("Simgeler|*.ico;*.png|Tüm dosyalar|*.*", missing: false).Split('|');
        Assert.Equal(4, filter.Length);
        Assert.Equal("*.ico;*.png", filter[1]);
        Assert.Equal("*.*", filter[3]);
        Assert.Contains("{1:N0}", PseudoText.Apply("x {0} y {1:N0}", false));
    }

    [Fact]
    public void Pseudo_alphabet_is_complete()
    {
        const string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var mapped = PseudoText.Apply(letters, false).Trim('⟦', '⟧', '~');
        Assert.Equal(letters.Length, mapped.Length);
        Assert.All(mapped, c => Assert.True(c > 127 && char.IsLetter(c), $"'{c}'"));
        Assert.Equal(letters.Length, mapped.Distinct().Count());
    }

    [Fact]
    public void Variants_list_both_languages()
    {
        Assert.Equal(new[] { "Düzen uygulanmadan önce", "Before applying a layout" }, L.Variants("Düzen uygulanmadan önce"));
        Assert.Equal(new[] { "Tabloda olmayan metin" }, L.Variants("Tabloda olmayan metin"));
    }

    [Fact]
    public void Markup_extension_translates()
    {
        Assert.Equal("Widget'lar", new TExtension("Widget'lar").ProvideValue(null!));
        using var _ = L.Use(Lang.En);
        Assert.Equal("Widgets", new TExtension("Widget'lar").ProvideValue(null!));
    }

    [Fact]
    public void System_choice_and_native_names_are_not_translated()
    {
        using var _ = L.Use(Lang.En);
        Assert.Equal("Türkçe", L.NativeName(Lang.Tr));
        Assert.Equal("English", L.NativeName(Lang.En));
    }

    // ---------------------------------------------------------------- tarayıcı

    private sealed record KeyUse(string Key, string? Context, string Where);

    private sealed class ScanResult
    {
        public List<KeyUse> Uses { get; } = [];
        public List<string> CallProblems { get; } = [];
        public List<string> CultureProblems { get; } = [];
        public List<string> RawTexts { get; } = [];
    }

    private static readonly Lazy<ScanResult> Scan = new(ScanSources);

    private static readonly Regex TurkishLetter = new("[çğıöşüÇĞİÖŞÜ]");

    /// <summary>Tamamı veri olan dosyalar (ham Türkçe denetimi dışında): çok dilli "Yeni klasör" adları, geliştirici aracı.</summary>
    private static readonly string[] DataFiles = [@"Desktop\NewFolderWatcher.cs", @"Icons\IconSheet.cs"];

    /// <summary>Tek tek izin verilen ham metinler (dosya, metin): kimlikler.</summary>
    private static readonly (string File, string Text)[] AllowedLiterals = [(@"Core\AppInfo.cs", "Düzenleme")];

    private static readonly HashSet<string> UiAttributes =
    [
        "Text", "Content", "Header", "ToolTip", "Title", "PlaceholderText", "Description",
        "AutomationProperties.Name", "AutomationProperties.HelpText",
    ];

    private static readonly HashSet<string> TextElements =
    [
        "TextBlock", "Run", "Span", "Bold", "Italic", "Underline", "Hyperlink", "Paragraph", "Label", "Button",
        "CheckBox", "RadioButton", "ComboBoxItem", "ListBoxItem", "MenuItem", "ToolTip", "TextBox", "String",
    ];

    private static string SourceRoot => Path.Combine(RepoRoot(), "src", "Duzenleme");

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(SourceRoot, pattern, SearchOption.AllDirectories)
            .Where(f =>
            {
                var rel = Path.GetRelativePath(SourceRoot, f);
                return !rel.StartsWith(@"obj\", StringComparison.OrdinalIgnoreCase) && !rel.StartsWith(@"bin\", StringComparison.OrdinalIgnoreCase);
            });

    private static ScanResult ScanSources()
    {
        var result = new ScanResult();
        foreach (var file in SourceFiles("*.cs"))
            ScanCSharp(Path.GetRelativePath(SourceRoot, file), File.ReadAllText(file), result);
        foreach (var file in SourceFiles("*.xaml"))
            ScanXaml(Path.GetRelativePath(SourceRoot, file), XDocument.Load(file, LoadOptions.SetLineInfo), result);
        return result;
    }

    /// <summary>Bir C# dosyası: L çağrıları (anahtar, bağlam, sabit/başlatıcı kuralı), kültür denetimi, ham Türkçe metin.</summary>
    private static void ScanCSharp(string rel, string text, ScanResult result)
    {
        var tree = CSharpSyntaxTree.ParseText(text, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview), rel);
        var root = tree.GetRoot();
        string Where(SyntaxNode node) => $"{rel}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
        // Localization klasörü çeviri altyapısının kendisi: içindeki L çağrıları değişkenle yapılır, metinleri veridir.
        var inLocalization = rel.StartsWith(@"Localization\", StringComparison.OrdinalIgnoreCase);

        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            CheckCulture(call, Where, result);
            if (inLocalization || LMethod(call) is not { } method) continue;
            if (RuntimeMethods.Contains(method) && StaticInitializer(call) is { } kind)
                result.CallProblems.Add($"{Where(call)}: L.{method} {kind} içinde (dil o an neyse donar; => özellik ya da L.N + L.Dyn kullan)");
            if (!KeyedMethods.Contains(method)) continue;

            var args = call.ArgumentList.Arguments;
            var keyIndex = method == "P" ? 1 : 0;
            if (args.Count <= keyIndex) continue;
            if (Literal(args[keyIndex].Expression) is not { } key)
            {
                result.CallProblems.Add($"{Where(call)}: L.{method} anahtarı metin sabiti değil: {args[keyIndex]}");
                continue;
            }
            string? context = null;
            if (method is "T" or "N")
            {
                var contextArg = args.Skip(1).FirstOrDefault(a => a.NameColon is null || a.NameColon.Name.Identifier.Text == "context");
                if (contextArg is not null)
                {
                    context = Literal(contextArg.Expression);
                    if (context is null && !contextArg.Expression.IsKind(SyntaxKind.NullLiteralExpression))
                    {
                        result.CallProblems.Add($"{Where(call)}: L.{method} bağlamı metin sabiti değil: {contextArg}");
                        continue;
                    }
                }
            }
            result.Uses.Add(new KeyUse(key, context, Where(call)));
        }

        if (inLocalization || DataFiles.Any(d => rel.Equals(d, StringComparison.OrdinalIgnoreCase))) return;
        var lines = text.Split('\n');
        foreach (var token in root.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.StringLiteralToken) && !token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                && !token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) && !token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
                continue;
            var value = token.ValueText;
            if (!TurkishLetter.IsMatch(value) || token.Parent is null) continue;
            if (AllowedLiterals.Any(a => a.File.Equals(rel, StringComparison.OrdinalIgnoreCase) && a.Text == value)) continue;
            var line = token.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (lines[line].Contains("l10n: çevrilmez")) continue;
            if (IsExemptLiteral(token.Parent)) continue;
            result.RawTexts.Add($"{rel}:{line + 1}: \"{Shorten(value)}\"");
        }
    }

    /// <summary>L.X(...) çağrısıysa yöntem adı (L, Localization.L ya da Duzenleme.Localization.L).</summary>
    private static string? LMethod(InvocationExpressionSyntax call) =>
        call.Expression is MemberAccessExpressionSyntax { Expression: var target, Name: IdentifierNameSyntax name }
        && target.ToString() is "L" or "Localization.L" or "Duzenleme.Localization.L" or "global::Duzenleme.Localization.L"
            ? name.Identifier.Text
            : null;

    private static string? Literal(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression) ? literal.Token.ValueText : null;

    /// <summary>Çağrı static alan/özellik başlatıcısında, static kurucuda ya da const'ta mı? Lambda içindeyse ertelenmiştir: sorun yok.</summary>
    private static string? StaticInitializer(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case LambdaExpressionSyntax or AnonymousMethodExpressionSyntax or LocalFunctionStatementSyntax:
                    return null;
                case FieldDeclarationSyntax field when field.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.ConstKeyword)):
                    return "static alan başlatıcısı";
                case PropertyDeclarationSyntax property when property.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
                                                           && property.Initializer is { } init && init.Span.Contains(node.Span):
                    return "static özellik başlatıcısı";
                case ConstructorDeclarationSyntax ctor when ctor.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)):
                    return "static kurucu";
                case LocalDeclarationStatementSyntax local when local.IsConst:
                    return "const";
                case MemberDeclarationSyntax:
                    return null;
            }
        }
        return null;
    }

    private static void CheckCulture(InvocationExpressionSyntax call, Func<SyntaxNode, string> where, ScanResult result)
    {
        if (call.Expression is not MemberAccessExpressionSyntax access) return;
        var name = access.Name.Identifier.Text;
        var args = call.ArgumentList.Arguments;
        var onChar = access.Expression is PredefinedTypeSyntax p && p.Keyword.IsKind(SyntaxKind.CharKeyword);
        if (name is "ToUpper" or "ToLower" && (args.Count == 0 || (onChar && args.Count == 1)))
            result.CultureProblems.Add($"{where(call)}: {call}");
        var onString = access.Expression is PredefinedTypeSyntax s && s.Keyword.IsKind(SyntaxKind.StringKeyword)
                       || access.Expression.ToString() == "String";
        if (onString && name == "Compare" && args.Count == 3
            && args[2].Expression.Kind() is SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression)
            result.CultureProblems.Add($"{where(call)}: {call}");
    }

    /// <summary>L.* argümanı, DebugLog.Write iletisi ya da öznitelik (ör. [Obsolete]) metni: ham arayüz metni sayılmaz.</summary>
    private static bool IsExemptLiteral(SyntaxNode node) =>
        node.Ancestors().Any(a => a is AttributeSyntax
                                  || a is InvocationExpressionSyntax call
                                  && (LMethod(call) is not null || call.Expression.ToString() is "DebugLog.Write" or "Duzenleme.DebugLog.Write"));

    private static void ScanXaml(string rel, XDocument document, ScanResult result)
    {
        static int Line(XObject o) => o is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
        foreach (var element in document.Descendants())
        {
            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration) continue;
                var value = attribute.Value;
                var where = $"{rel}:{Line(attribute)}";
                if (value.TrimStart().StartsWith("{l:", StringComparison.Ordinal))
                {
                    if (ParseMarkup(value) is { } parsed) result.Uses.Add(new KeyUse(parsed.Text, parsed.Context, where));
                    else result.CallProblems.Add($"{where}: {{l:T …}} okunamadı: {value}");
                    continue;
                }
                var name = attribute.Name.LocalName;
                var isUi = UiAttributes.Contains(name)
                           || (element.Name.LocalName == "Setter" && name == "Value"
                               && UiAttributes.Contains((string?)element.Attribute("Property") ?? ""));
                if (isUi && IsRawText(value)) result.RawTexts.Add($"{where}: {name}=\"{Shorten(value)}\"");
            }
            // Öğe içindeki metin yalnızca metin taşıyan öğelerde arayüz metnidir (<PopupAnimation>None</…> gibi değerler değil).
            if (!TextElements.Contains(element.Name.LocalName)) continue;
            foreach (var node in element.Nodes().OfType<XText>())
                if (node is not XCData && IsRawText(node.Value.Trim()))
                    result.RawTexts.Add($"{rel}:{Line(node)}: \"{Shorten(node.Value.Trim())}\"");
        }
    }

    private static bool IsRawText(string value) =>
        value.Length > 0 && !value.StartsWith('{') && value.Any(char.IsLetter)
        && !value.StartsWith("sk-ant-", StringComparison.Ordinal) && value != "console.anthropic.com";

    /// <summary>
    /// {l:T …} değerini WPF'in biçim eki kurallarıyla ayrıştırır: konumsal ya da Text= ilk değer, isteğe bağlı Context=;
    /// tırnaklı değerde \x → x, tırnaksızda da \x → x ve kenar boşlukları kırpılır. Okunamazsa null.
    /// </summary>
    private static (string Text, string? Context)? ParseMarkup(string value)
    {
        var s = value.Trim();
        if (!s.StartsWith("{l:T", StringComparison.Ordinal) || !s.EndsWith('}')) return null;
        var body = s[4..^1];
        if (body.Length > 0 && !char.IsWhiteSpace(body[0])) return null;
        string? text = null, context = null;
        var i = 0;
        var positional = 0;
        while (true)
        {
            while (i < body.Length && char.IsWhiteSpace(body[i])) i++;
            if (i >= body.Length) break;
            string? name = null;
            var j = i;
            while (j < body.Length && char.IsLetter(body[j])) j++;
            if (j > i && j < body.Length && body[j] == '=')
            {
                name = body[i..j];
                i = j + 1;
                while (i < body.Length && char.IsWhiteSpace(body[i])) i++;
            }
            var sb = new StringBuilder();
            var quoted = i < body.Length && body[i] is '\'' or '"';
            if (quoted)
            {
                var quote = body[i++];
                while (i < body.Length && body[i] != quote)
                {
                    if (body[i] == '\\' && i + 1 < body.Length) i++;
                    sb.Append(body[i++]);
                }
                if (i >= body.Length) return null;
                i++;
                while (i < body.Length && char.IsWhiteSpace(body[i])) i++;
            }
            else
            {
                while (i < body.Length && body[i] != ',')
                {
                    if (body[i] == '\\' && i + 1 < body.Length) i++;
                    sb.Append(body[i++]);
                }
            }
            var item = quoted ? sb.ToString() : sb.ToString().Trim();
            switch (name)
            {
                case null when positional++ == 0: text = item; break;
                case "Text": text = item; break;
                case "Context": context = item; break;
                default: return null;
            }
            if (i < body.Length)
            {
                if (body[i] != ',') return null;
                i++;
            }
        }
        return text is null ? null : (text, context);
    }

    private static HashSet<string> Placeholders(string text) =>
        Regex.Matches(text.Replace("{{", "").Replace("}}", ""), @"\{(\d+)(?:[,:][^}]*)?\}").Select(m => m.Groups[1].Value).ToHashSet();

    private static bool Formats(string text, HashSet<string> placeholders)
    {
        var count = placeholders.Select(int.Parse).DefaultIfEmpty(-1).Max() + 1;
        try
        {
            _ = string.Format(CultureInfo.InvariantCulture, text, Enumerable.Repeat<object?>(1, count).ToArray());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Shorten(string text) => text.Length <= 70 ? text.ReplaceLineEndings(" ") : text[..67].ReplaceLineEndings(" ") + "…";

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
    }
}
