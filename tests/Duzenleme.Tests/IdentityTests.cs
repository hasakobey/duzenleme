using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Duzenleme.Core;
using Duzenleme.Icons;

namespace Duzenleme.Tests;

/// <summary>
/// Ad değişikliğinde (Düzenleme → NestDesk, 2.0.0) yalnızca görünen ad değişir. Buradaki kimlikler bilerek eski adla kalır:
/// biri değişirse mevcut kurulumlar bozulur (güncelleme yeni kurulum sanılır, "Windows ile başlat" kopar, ayarlar ve
/// kayıtlı API anahtarı okunamaz, Store paketi başka uygulama olur). Değiştirmek gerekiyorsa önce geçiş planı yaz.
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
    public void Legacy_identities_keep_old_name()
    {
        Assert.Equal("Duzenleme.", AppInfo.InstanceIdPrefix);
        Assert.Equal("Duzenleme", AppInfo.DataFolderName);
        Assert.Equal("Duzenleme", AppInfo.RunValueName);
        Assert.Equal("DuzenlemeStartup", AppInfo.StartupTaskId);
        Assert.Equal("Duzenleme.ApiKey.v1", ApiKeyStore.EntropyText);
        Assert.Equal(".duzenleme-", FolderIconService.IconPrefix);
        Assert.Equal("Duzenleme", typeof(AppInfo).Assembly.GetName().Name);
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
    public void Store_manifest_changes_only_display_names()
    {
        XNamespace foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";
        var manifest = XDocument.Load(Path.Combine(RepoRoot(), "tools", "store", "AppxManifest.xml"));

        Assert.Equal(AppInfo.Name, manifest.Descendants(foundation + "Properties").Single().Element(foundation + "DisplayName")?.Value);

        var apps = manifest.Descendants(foundation + "Application").ToList();
        Assert.Equal(new[] { "Duzenleme", "WidgetEkle" }, apps.Select(a => (string?)a.Attribute("Id")).ToArray());

        // Uygulamalar ve başlangıç görevi: hepsi aynı (eski adlı) exe.
        var executables = manifest.Descendants().Attributes("Executable").Select(a => a.Value).ToList();
        Assert.NotEmpty(executables);
        Assert.All(executables, e => Assert.Equal("Duzenleme.exe", e));

        var task = manifest.Descendants(desktop + "StartupTask").Single();
        Assert.Equal(AppInfo.StartupTaskId, (string?)task.Attribute("TaskId"));
        Assert.Equal(AppInfo.Name, (string?)task.Attribute("DisplayName"));

        // Başlat menüsündeki adlar yeni adla başlar.
        var visuals = apps.Select(a => (string?)a.Element(uap + "VisualElements")?.Attribute("DisplayName")).ToList();
        Assert.Equal(AppInfo.Name, visuals[0]);
        Assert.StartsWith(AppInfo.Name + " ", visuals[1]);
    }

    [Fact]
    public void Installer_keeps_legacy_identities()
    {
        var path = Path.Combine(RepoRoot(), "tools", "installer", "Duzenleme.iss");
        var bytes = File.ReadAllBytes(path);
        // BOM'suz Inno Setup Türkçe karakterleri ANSI sanıp bozar.
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "Duzenleme.iss BOM'lu UTF-8 olmalı");
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Contains($"#define AppName \"{AppInfo.Name}\"", text);
        Assert.Contains("#define AppExe \"Duzenleme.exe\"", text);
        Assert.Contains("#define AppIdGuid \"8F3A1C2E-7B4D-4E6A-9C1F-2D5B8E7A4C31\"", text);
        Assert.Contains($"'{AppInfo.InstanceIdPrefix}' + GetUserNameString", text);
        Assert.Contains($"#define RunValue \"{AppInfo.RunValueName}\"", text);
        Assert.Contains($"{{userappdata}}\\{AppInfo.DataFolderName}\\settings.json", text);
        // Güncellemede eski adlı kısayollar silinir: eski ad AppInfo.FormerName ile aynı olmalı.
        Assert.Contains($"#define LegacyName \"{AppInfo.FormerName}\"", text);
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
