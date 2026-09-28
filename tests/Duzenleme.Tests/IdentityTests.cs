using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Duzenleme.Core;
using Duzenleme.Icons;

namespace Duzenleme.Tests;

/// <summary>
/// Ad değişikliği: 2.0.0'da görünen ad (Düzenleme → NestDesk), 2.1.0'da program dosyası, veri klasörü, Run değeri, klasör simgesi
/// öneki ve Store kimlikleri. Buradaki DEĞİŞMEZ kimlikler (tek örnek adı, DPAPI entropisi, Inno AppId) değişirse mevcut kurulumlar
/// bozulur: güncelleme yeni kurulum sanılır, çalışan eski sürüm kapatılamaz, kayıtlı API anahtarı çözülemez. Eski adlar (Legacy*)
/// geçiş için tanınmaya devam etmeli.
/// </summary>
public class IdentityTests
{
    [Fact]
    public void Display_name_is_new_name()
    {
        Assert.Equal("NestDesk", AppInfo.Name);
        Assert.Equal("Düzenleme", AppInfo.FormerName);
    }

    [Fact]
    public void Program_file_and_identities_use_new_name()
    {
        Assert.Equal("NestDesk", typeof(AppInfo).Assembly.GetName().Name);
        Assert.Equal(typeof(AppInfo).Assembly.GetName().Name + ".exe", AppInfo.ExeName);
        Assert.Equal("NestDesk", AppInfo.DataFolderName);
        Assert.Equal("NestDesk", AppInfo.RunValueName);
        Assert.Equal("NestDeskStartup", AppInfo.StartupTaskId);
        Assert.Equal("NestDesk", AppInfo.PackageAppId);
        Assert.Equal("AddWidget", AppInfo.PackageAddWidgetAppId);
        Assert.Equal(".nestdesk-", FolderIconService.IconPrefix);
    }

    [Fact]
    public void Legacy_identities_are_still_known()
    {
        // Geçiş bunlara bakar: 2.0'ın exe'si, veri klasörü, Run değeri ve klasör simgeleri.
        Assert.Equal("Duzenleme.exe", AppInfo.LegacyExeName);
        Assert.Equal("Duzenleme", AppInfo.LegacyDataFolderName);
        Assert.Equal("Duzenleme", AppInfo.LegacyRunValueName);
        Assert.Equal(".duzenleme-", FolderIconService.LegacyIconPrefix);
        Assert.Contains(".duzenleme-", FolderIconService.OwnPrefixes);
        Assert.Contains(".nestdesk-", FolderIconService.OwnPrefixes);
    }

    [Fact]
    public void Unchangeable_identities_keep_old_name()
    {
        // 2.0 (Duzenleme.exe) ile 2.1 (NestDesk.exe) birbirini görsün, kurulum ikisini de kapatsın.
        Assert.Equal("Duzenleme.", AppInfo.InstanceIdPrefix);
        Assert.Equal("Duzenleme.ApiKey.v1", ApiKeyStore.EntropyText);
    }

    [Fact]
    public void Assembly_title_and_product_show_display_name()
    {
        // Görev Yöneticisi, bildirim kaynağı ve Başlangıç uygulamaları exe'nin sürüm bilgisindeki bu adları gösterir.
        var assembly = typeof(AppInfo).Assembly;
        Assert.Equal(AppInfo.Name, assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        Assert.Equal(AppInfo.Name, assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
    }

    [Fact]
    public void Version_comes_from_project_file()
    {
        // Kurulum, MSIX (<sürüm>.0) ve CI etiket denetimi sürümü csproj'dan okur; Hakkında da aynı sayıyı göstermeli.
        var csproj = XDocument.Load(Path.Combine(RepoRoot(), "src", "Duzenleme", "Duzenleme.csproj"));
        var version = csproj.Descendants("Version").Single().Value;
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
        Assert.Equal(version, AppInfo.Version);
    }

    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
    private static readonly XNamespace DesktopNs = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

    private static XDocument Manifest() => XDocument.Load(Path.Combine(RepoRoot(), "tools", "store", "AppxManifest.xml"));

    [Fact]
    public void Store_manifest_uses_new_identities()
    {
        var manifest = Manifest();
        Assert.Equal(AppInfo.Name, manifest.Descendants(Foundation + "Properties").Single().Element(Foundation + "DisplayName")?.Value);

        var apps = manifest.Descendants(Foundation + "Application").ToList();
        Assert.Equal(new[] { AppInfo.PackageAppId, AppInfo.PackageAddWidgetAppId }, apps.Select(a => (string?)a.Attribute("Id")).ToArray());

        // Uygulamalar ve başlangıç görevi: hepsi aynı exe.
        var executables = manifest.Descendants().Attributes("Executable").Select(a => a.Value).ToList();
        Assert.Equal(3, executables.Count);
        Assert.All(executables, e => Assert.Equal(AppInfo.ExeName, e));

        var task = manifest.Descendants(DesktopNs + "StartupTask").Single();
        Assert.Equal(AppInfo.StartupTaskId, (string?)task.Attribute("TaskId"));
        Assert.Equal(AppInfo.Name, (string?)task.Attribute("DisplayName"));
        Assert.Equal(AppInfo.Name, (string?)apps[0].Element(Uap + "VisualElements")?.Attribute("DisplayName"));
        // Paket içeriğinde (yorumlar pakete girmez) eski ad yok.
        manifest.DescendantNodes().OfType<XComment>().Remove();
        Assert.DoesNotContain("uzenleme", manifest.ToString(SaveOptions.DisableFormatting), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Store_manifest_defaults_to_english_and_declares_turkish()
    {
        var languages = Manifest().Descendants(Foundation + "Resource").Select(r => (string?)r.Attribute("Language")).ToArray();
        Assert.Equal(new[] { "en-US", "tr-TR" }, languages);
    }

    [Fact]
    public void Every_manifest_resource_string_exists_in_every_language()
    {
        // makeappx eksik anahtarı fark etmez; Windows ve Store ham "ms-resource:..." ya da boş metin gösterir.
        var manifest = Manifest();
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "tools", "store", "AppxManifest.xml"));
        var keys = Regex.Matches(text, @"ms-resource:([\w.-]+)").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.Contains("AddWidgetName", keys);
        Assert.Contains("AppDescription", keys);
        var languages = manifest.Descendants(Foundation + "Resource").Select(r => (string)r.Attribute("Language")!).ToList();
        var stringsRoot = Path.Combine(RepoRoot(), "tools", "store", "Strings");
        Assert.Equal(languages.Order(), Directory.GetDirectories(stringsRoot).Select(Path.GetFileName).Order()!);
        foreach (var language in languages)
        {
            var data = XDocument.Load(Path.Combine(stringsRoot, language, "Resources.resw")).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value.Trim() ?? "");
            foreach (var key in keys)
            {
                Assert.True(data.TryGetValue(key, out var value) && value.Length > 0, $"{language}/Resources.resw: {key} eksik ya da boş");
            }
            // "Add a widget" girişi de ürün adıyla başlar.
            Assert.StartsWith(AppInfo.Name + " ", data["AddWidgetName"]);
        }
    }

    private static string Installer()
    {
        var path = Path.Combine(RepoRoot(), "tools", "installer", "NestDesk.iss");
        var bytes = File.ReadAllBytes(path);
        // BOM'suz Inno Setup Türkçe karakterleri ANSI sanıp bozar.
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "NestDesk.iss BOM'lu UTF-8 olmalı");
        return Encoding.UTF8.GetString(bytes);
    }

    [Fact]
    public void Installer_uses_new_names_and_keeps_unchangeable_identities()
    {
        var text = Installer();
        Assert.Contains($"#define AppName \"{AppInfo.Name}\"", text);
        Assert.Contains($"#define AppExe \"{AppInfo.ExeName}\"", text);
        Assert.Contains($"#define LegacyExe \"{AppInfo.LegacyExeName}\"", text);
        Assert.Contains($"#define RunValue \"{AppInfo.RunValueName}\"", text);
        Assert.Contains($"#define LegacyRunValue \"{AppInfo.LegacyRunValueName}\"", text);
        Assert.Contains($"#define DataFolder \"{AppInfo.DataFolderName}\"", text);
        Assert.Contains($"#define LegacyDataFolder \"{AppInfo.LegacyDataFolderName}\"", text);
        // DEĞİŞMEZ: güncelleme aynı kurulumun üzerine yazılsın, çalışan eski sürüm sinyalle kapatılabilsin.
        Assert.Contains("#define AppIdGuid \"8F3A1C2E-7B4D-4E6A-9C1F-2D5B8E7A4C31\"", text);
        Assert.Contains($"'{AppInfo.InstanceIdPrefix}' + GetUserNameString", text);
        // Güncellemede eski adlı kısayollar silinir: eski ad AppInfo.FormerName ile aynı olmalı.
        Assert.Contains($"#define LegacyName \"{AppInfo.FormerName}\"", text);
        Assert.Contains("OutputBaseFilename=NestDesk-Setup-", text);
        Assert.Contains("VersionInfoDescription={#AppName} Setup", text);
    }

    [Fact]
    public void Installer_removes_legacy_program_files_and_closes_both_exe_names()
    {
        var text = Installer();
        foreach (var file in LegacyFiles.ProgramFiles)
        {
            var entry = file == AppInfo.LegacyExeName ? "{#LegacyExe}" : file;
            Assert.Contains($"Type: files; Name: \"{{app}}\\{entry}\"", text);
        }
        Assert.Contains("Get-Process NestDesk,Duzenleme", text);
        // İki dildeki "Widget ekle" kısayolu da silinir (öbür dilde kurulmuş eski sürümünki kırık kalmasın).
        Assert.Contains("{autoprograms}\\{#AppName} - Widget ekle.lnk", text);
        Assert.Contains("{autoprograms}\\{#AppName} - Add a widget.lnk", text);
        Assert.Contains("--restore-desktop", text);
        Assert.Contains("MigrateRunValue(ExpandConstant('{app}'))", text);
    }

    [Fact]
    public void Installer_is_bilingual_with_english_first()
    {
        var text = Installer();
        var english = text.IndexOf("Name: \"english\"", StringComparison.Ordinal);
        var turkish = text.IndexOf("Name: \"turkish\"", StringComparison.Ordinal);
        Assert.True(english > 0 && turkish > english, "[Languages]: önce english (başka dillerde varsayılan), sonra turkish");
        Assert.Contains("ShowLanguageDialog=yes", text);
        Assert.Contains("WizardStyle=modern dynamic", text);
        // Her özel ileti iki dilde de var ve eski ad görünmüyor.
        var messages = Regex.Matches(text, @"^(english|turkish)\.(\w+)=(.*)$", RegexOptions.Multiline);
        var byKey = messages.GroupBy(m => m.Groups[2].Value).ToList();
        Assert.NotEmpty(byKey);
        Assert.All(byKey, g => Assert.Equal(new[] { "english", "turkish" }, g.Select(m => m.Groups[1].Value).Order().ToArray()));
        Assert.All(messages, m => Assert.DoesNotContain("zenleme", m.Groups[3].Value, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scripts_that_contain_turkish_text_have_bom()
    {
        foreach (var script in new[] { "publish.ps1", "package-msix.ps1", "make-icon.ps1" })
        {
            var bytes = File.ReadAllBytes(Path.Combine(RepoRoot(), "tools", script));
            Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, $"{script} BOM'lu UTF-8 olmalı");
        }
    }

    /// <summary>
    /// Eski ad kullanıcıya görünmesin: XAML'daki metinler ve C# dizgeleri "Düzenleme"/"Duzenleme" içermez. İzinliler: eski adları
    /// tanımlayan AppInfo, DPAPI entropisi (görünmez) ve eski DUZENLEME_* değişkenleri. ("düzenle" fiili — "Şimdi düzenle" — serbest.)
    /// </summary>
    [Fact]
    public void Old_name_does_not_appear_in_user_facing_text()
    {
        var src = Path.Combine(RepoRoot(), "src", "Duzenleme");
        // Görünmez iç adlar: eski adların tanımı, eski program dosyaları listesi, DPAPI entropisi, pencere ve iş parçacığı adları.
        string[] allowed = [Path.Combine("Core", "AppInfo.cs"), Path.Combine("Core", "LegacyFiles.cs"), Path.Combine("Icons", "ApiKeyStore.cs"),
            Path.Combine("Desktop", "HotkeyManager.cs"), Path.Combine("Widgets", "ShellIcons.cs")];
        var literal = new Regex("\"(?:[^\"\\\\\\r\\n]|\\\\.)*\"");
        // Ad alanları: x:Class="Duzenleme.Views.X", xmlns:x="clr-namespace:Duzenleme.Core".
        var ns = new Regex("^\"(clr-namespace:)?Duzenleme(\\.[A-Z]\\w*)*\"$");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml"))
                     .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                                 && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
        {
            var relative = Path.GetRelativePath(src, file);
            if (allowed.Contains(relative)) continue;
            foreach (var (line, number) in File.ReadLines(file).Select((l, i) => (l, i + 1)))
            {
                var code = line.TrimStart();
                if (code.StartsWith("//") || code.StartsWith("///") || code.StartsWith("<!--")) continue;
                foreach (Match m in literal.Matches(line))
                {
                    var value = m.Value;
                    if (ns.IsMatch(value)) continue;
                    if (value.Contains("Düzenleme") || value.Contains("Duzenleme"))
                        offenders.Add($"{relative}:{number}: {value}");
                }
            }
        }
        Assert.Empty(offenders);
    }

    /// <summary>Depo kökü: test çıktısından yukarı yürüyüp Duzenleme.sln'in bulunduğu klasör.</summary>
    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Duzenleme.sln"))) return dir.FullName;
        }
        throw new DirectoryNotFoundException("Duzenleme.sln bulunamadı: " + AppContext.BaseDirectory);
    }
}
