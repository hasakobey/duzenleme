using Duzenleme.Core;
using Duzenleme.Localization;

namespace Duzenleme.Tests;

/// <summary>Dile göre oluşturulan kalıcı varsayılanlar (hazır kurallar, düzen yedekleri): bir kez oluşur, sonra kullanıcı verisidir.</summary>
public class LocalizedDefaultsTests
{
    [Fact]
    public void Turkish_defaults_are_unchanged()
    {
        // 2.0 ile birebir aynı: mevcut kullanıcıların "Varsayılana döndür"ü ve testlerin beklentileri değişmesin.
        var rules = Rule.Defaults(Lang.Tr);
        Assert.Equal(new[] { "PDF", "Resimler", "Belgeler", "Arşivler", "Videolar", "Müzik" }, rules.Select(r => r.TargetFolder));
        Assert.Equal(new[] { "pdf" }, rules[0].Extensions);
        Assert.Equal(new[] { "jpg", "jpeg", "png", "gif", "webp", "bmp", "heic", "svg" }, rules[1].Extensions);
        Assert.Equal(new[] { "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "rtf", "odt", "csv" }, rules[2].Extensions);
        Assert.Equal(new[] { "zip", "rar", "7z", "tar", "gz" }, rules[3].Extensions);
        Assert.Equal(new[] { "mp4", "mov", "avi", "mkv", "webm" }, rules[4].Extensions);
        Assert.Equal(new[] { "mp3", "wav", "flac", "m4a", "ogg" }, rules[5].Extensions);
        Assert.All(rules, r => Assert.True(r.Enabled));

        // Varsayılan dil Türkçe (testler belirlenimci).
        Assert.Equal(rules.Select(r => r.TargetFolder), Rule.Defaults().Select(r => r.TargetFolder));
        Assert.Equal(rules.Select(r => r.TargetFolder), new AppSettings().Rules.Select(r => r.TargetFolder));
    }

    [Fact]
    public void English_defaults_use_windows_library_names()
    {
        Assert.Equal(new[] { "PDF", "Pictures", "Documents", "Archives", "Videos", "Music" },
            Rule.Defaults(Lang.En).Select(r => r.TargetFolder));
        Assert.Equal(Rule.Defaults(Lang.Tr).Select(r => string.Join(",", r.Extensions)),
            Rule.Defaults(Lang.En).Select(r => string.Join(",", r.Extensions)));

        using var _ = L.Use(Lang.En);
        Assert.Equal("Pictures", new AppSettings().Rules[1].TargetFolder);
    }

    [Fact]
    public void Defaults_reuse_existing_folder_of_other_language()
    {
        // İngilizce arayüz, masaüstünde Türkçe adlı klasörler: kural onlara yönelir (gerçek adıyla).
        var en = Rule.Defaults(Lang.En, ["resimler", "Oyunlar", "Arşivler"]).Select(r => r.TargetFolder).ToArray();
        Assert.Equal(new[] { "PDF", "resimler", "Documents", "Arşivler", "Videos", "Music" }, en);

        // Kendi dilindeki klasör varsa o dilin adı kalır (öteki dildeki de olsa).
        Assert.Equal("Resimler", Rule.Defaults(Lang.Tr, ["Pictures", "resimler"])[1].TargetFolder);
        Assert.Equal("Pictures", Rule.Defaults(Lang.Tr, ["Pictures"])[1].TargetFolder);
        Assert.Equal("Resimler", Rule.Defaults(Lang.Tr, [])[1].TargetFolder);
    }

    [Fact]
    public void Defaults_are_recognised_in_any_language()
    {
        Assert.True(Rule.AreDefaults(Rule.Defaults(Lang.Tr)));
        Assert.True(Rule.AreDefaults(Rule.Defaults(Lang.En)));
        Assert.True(Rule.AreDefaults(Rule.Defaults(Lang.En, ["Resimler"])));

        var changed = Rule.Defaults(Lang.Tr);
        changed[0].Extensions.Add("xps");
        Assert.False(Rule.AreDefaults(changed));
        var disabled = Rule.Defaults(Lang.Tr);
        disabled[1].Enabled = false;
        Assert.False(Rule.AreDefaults(disabled));
        var renamed = Rule.Defaults(Lang.Tr);
        renamed[1].TargetFolder = "Fotoğraflar";
        Assert.False(Rule.AreDefaults(renamed));
        Assert.False(Rule.AreDefaults(Rule.Defaults(Lang.Tr).Take(5).ToList()));
    }

    [Fact]
    public void New_user_rules_follow_existing_desktop_folders()
    {
        using var _ = L.Use(Lang.En);
        var settings = new AppSettings();
        var before = settings.Rules;
        Assert.True(Onboarding.PrepareNewUser(settings, ["Resimler", "Games"]));
        Assert.Equal("Resimler", settings.Rules[1].TargetFolder);
        Assert.Equal("Documents", settings.Rules[2].TargetFolder);
        // Liste yerinde değişmez (izleyici arka planda okur): yenisi atanır.
        Assert.NotSame(before, settings.Rules);
        Assert.Equal("Pictures", before[1].TargetFolder);
    }

    [Fact]
    public void New_user_preparation_keeps_custom_rules_and_existing_users()
    {
        using var _ = L.Use(Lang.En);
        var custom = new AppSettings { Rules = [new Rule { TargetFolder = "Faturalar", Extensions = ["pdf"] }] };
        Onboarding.PrepareNewUser(custom, ["Resimler"]);
        Assert.Equal("Faturalar", Assert.Single(custom.Rules).TargetFolder);

        var existing = new AppSettings { FirstRunDone = true, Rules = Rule.Defaults(Lang.Tr) };
        Assert.False(Onboarding.PrepareNewUser(existing, ["Pictures"]));
        Assert.Equal("Resimler", existing.Rules[1].TargetFolder);

        // Klasör listesi verilmezse kurallara dokunulmaz.
        var noFolders = new AppSettings();
        var rules = noFolders.Rules;
        Onboarding.PrepareNewUser(noFolders);
        Assert.Same(rules, noFolders.Rules);
    }

    [Fact]
    public void Layout_backup_is_replaced_after_language_switch()
    {
        var widgets = new List<WidgetConfig> { new() { Kind = WidgetKind.Clock } };
        var layouts = new List<LayoutSnapshot>
        {
            LayoutSnapshot.Capture("Düzen uygulanmadan önce", widgets),
            LayoutSnapshot.Capture("Otomatik yerleştirmeden önce", widgets),
            LayoutSnapshot.Capture("İş", widgets),
        };

        using (L.Use(Lang.En))
        {
            Assert.True(LayoutBackup.IsApply("Düzen uygulanmadan önce"));
            Assert.True(LayoutBackup.IsApply("Before applying a layout"));
            Assert.False(LayoutBackup.IsApply("İş"));
            LayoutBackup.SaveBeforeApply(layouts, widgets);
            Assert.Equal(new[] { "Otomatik yerleştirmeden önce", "İş", "Before applying a layout" }, layouts.Select(l => l.Name));
            LayoutBackup.SaveBeforeArrange(layouts, widgets);
            Assert.Equal(new[] { "İş", "Before applying a layout", "Before auto-arrange" }, layouts.Select(l => l.Name));
        }

        // Türkçeye dönülünce İngilizce yedek de tanınır ve yerine Türkçesi yazılır (iki yedek birikmez).
        Assert.True(LayoutBackup.IsApply("Before applying a layout"));
        LayoutBackup.SaveBeforeApply(layouts, widgets);
        Assert.Equal(new[] { "İş", "Before auto-arrange", "Düzen uygulanmadan önce" }, layouts.Select(l => l.Name));
        Assert.Equal("Düzen uygulanmadan önce", LayoutBackup.ApplyName);
    }
}
