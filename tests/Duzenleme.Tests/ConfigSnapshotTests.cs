using System.Text.Json;
using Duzenleme.Core;

namespace Duzenleme.Tests;

/// <summary>
/// Düzenle panelinin "Geri al"ı (<see cref="ConfigSnapshot"/>): panel açılınca alınan kopya aynı ayar nesnesine geri yazılır;
/// daha yeni sürümün alanları (Extra) da döner, iki kez geri yazmak bir kez yazmakla aynıdır, yer/boyut ve notun metni olduğu
/// gibi kalır.
/// </summary>
public class ConfigSnapshotTests
{
    private static WidgetConfig Fence() => new()
    {
        Kind = WidgetKind.Fence, Filter = DesktopFilter.Files, Title = "İş", Scale = 1.25, HiddenParts = ["count"],
        PixelLeft = 100, PixelTop = 200, Width = 300, Height = 250, Z = 7, NoteText = "",
        Extra = new Dictionary<string, JsonElement> { ["Zz_Future"] = JsonDocument.Parse("""{"a":1}""").RootElement.Clone() },
    };

    private static string Json(WidgetConfig c) => JsonSerializer.Serialize(c, JsonFile.Options);

    [Fact]
    public void Restore_brings_back_settings_including_unknown_fields()
    {
        var config = Fence();
        var before = Json(config);
        var snapshot = ConfigSnapshot.Take(config);
        Assert.Equal(config.Id, snapshot.Id);
        Assert.False(snapshot.Differs(config));

        config.Title = null;
        config.Scale = 0.85;
        config.HiddenParts.Add("search");
        config.LookTheme = "neon";
        config.Extra = null; // 2.1 gibi bilinmeyen alanı düşüren bir kod
        Assert.True(snapshot.Differs(config));

        var changed = snapshot.Restore(config);
        Assert.Contains(nameof(WidgetConfig.Title), changed);
        Assert.Contains(nameof(WidgetConfig.Extra), changed);
        Assert.Equal(before, Json(config));
        Assert.Equal(1, config.Extra!["Zz_Future"].GetProperty("a").GetInt32());
        Assert.False(snapshot.Differs(config));
    }

    [Fact]
    public void Restore_is_idempotent_and_never_shares_objects_with_the_snapshot()
    {
        var config = Fence();
        var snapshot = ConfigSnapshot.Take(config);
        config.HiddenParts.Clear();
        config.Shadow = false;

        Assert.NotEmpty(snapshot.Restore(config));
        var once = Json(config);
        Assert.Empty(snapshot.Restore(config));
        Assert.Equal(once, Json(config));

        // Geri yazılan liste kopyanın değildir: değiştirmek sonraki geri yazmayı etkilemez.
        config.HiddenParts.Add("open");
        snapshot.Restore(config);
        Assert.Equal(["count"], config.HiddenParts);
    }

    [Fact]
    public void Place_size_order_and_note_text_are_kept()
    {
        var config = Fence();
        config.Kind = WidgetKind.Note;
        config.NoteText = "ilk";
        config.ItemOrder = ["a"];
        var snapshot = ConfigSnapshot.Take(config);

        config.PixelLeft = 500;
        config.PixelTop = 600;
        config.Left = 1;
        config.Width = 400;
        config.Height = 500;
        config.Z = 99;
        config.NoteText = "panel açıkken yazılan";
        config.ItemOrder = ["b", "a"];
        config.ActiveTab = 1;
        config.NoteColor = NoteColor.Blue;
        Assert.True(snapshot.Differs(config)); // not rengi

        var changed = snapshot.Restore(config);
        Assert.Equal([nameof(WidgetConfig.NoteColor)], changed);
        Assert.Equal(500, config.PixelLeft);
        Assert.Equal(600, config.PixelTop);
        Assert.Equal(400, config.Width);
        Assert.Equal(99, config.Z);
        Assert.Equal("panel açıkken yazılan", config.NoteText);
        Assert.Equal(["b", "a"], config.ItemOrder);
        Assert.Equal(1, config.ActiveTab);
        Assert.Equal(NoteColor.Yellow, config.NoteColor);

        // Yalnızca korunan alanlar farklıysa geri alınacak bir şey yoktur.
        Assert.False(snapshot.Differs(config));
    }

    [Fact]
    public void Running_timer_keeps_running_and_only_its_settings_come_back()
    {
        var config = new WidgetConfig { Kind = WidgetKind.Clock, Variant = WidgetVariants.Timer, Timer = new TimerState { Minutes = 10 } };
        var snapshot = ConfigSnapshot.Take(config);

        // Panel açıkken süre değişti ve zamanlayıcı başlatıldı.
        var ends = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        config.Timer!.Minutes = 25;
        config.Timer.EndsUtc = ends;
        config.Timer.Notify = false;
        Assert.True(snapshot.Differs(config));

        Assert.Equal([nameof(WidgetConfig.Timer)], snapshot.Restore(config));
        Assert.Equal(10, config.Timer.Minutes);
        Assert.True(config.Timer.Notify);
        Assert.Equal(ends, config.Timer.EndsUtc); // çalışma durumu korunur
        Assert.False(snapshot.Differs(config));

        // Yalnızca çalışma durumu farklıysa geri alınacak bir şey yoktur.
        config.Timer.EndsUtc = null;
        Assert.False(snapshot.Differs(config));
    }

    [Fact]
    public void Changed_timer_mode_comes_back_whole()
    {
        var config = new WidgetConfig { Kind = WidgetKind.Clock, Variant = WidgetVariants.Timer, Timer = new TimerState { Minutes = 15 } };
        var snapshot = ConfigSnapshot.Take(config);
        config.Timer = new TimerState { Mode = TimerModes.Pomodoro, Round = 3 };

        snapshot.Restore(config);
        Assert.Equal(TimerModes.Countdown, config.Timer!.Mode);
        Assert.Equal(15, config.Timer.Minutes);
        Assert.Equal(1, config.Timer.Round);
    }

    [Fact]
    public void Undoing_the_source_brings_back_its_manual_order()
    {
        // Panelde kaynak değişti (FenceView.ShowSource: başlık ve elle sıra kaynağa aitti, silinir).
        var config = new WidgetConfig { Kind = WidgetKind.Fence, Filter = DesktopFilter.Folders, SortBy = FenceOrder.Manual, ItemOrder = ["b", "a"] };
        var snapshot = ConfigSnapshot.Take(config);
        config.Filter = DesktopFilter.All;
        config.ItemOrder = null;

        var changed = snapshot.Restore(config);
        Assert.Contains(nameof(WidgetConfig.Filter), changed);
        Assert.Contains(nameof(WidgetConfig.ItemOrder), changed);
        Assert.Equal(["b", "a"], config.ItemOrder);
        Assert.Empty(snapshot.Restore(config));

        // Kaynak gidip geri geldi (Filter yine aynı) ama sıra silindi: silinen sıra da döner.
        config.ItemOrder = null;
        Assert.Equal([nameof(WidgetConfig.ItemOrder)], snapshot.Restore(config));
        Assert.Equal(["b", "a"], config.ItemOrder);

        // Yeni kaynakta elle verilen sıra o kaynağındır: eski kaynak geri gelince eski sırası (yoksa sırasızlık) döner.
        var portal = new WidgetConfig { Kind = WidgetKind.Fence, FolderName = "PDF" };
        var before = ConfigSnapshot.Take(portal);
        portal.FolderName = @"C:\İndirilenler";
        portal.ItemOrder = ["x"];
        before.Restore(portal);
        Assert.Equal("PDF", portal.FolderName);
        Assert.Null(portal.ItemOrder);
    }

    [Fact]
    public void Undoing_convert_to_list_brings_back_the_plain_text_unless_it_was_edited()
    {
        const string plain = "süt\n- ekmek\n\nyumurta";
        var config = new WidgetConfig { Kind = WidgetKind.Note, NoteText = plain };
        var snapshot = ConfigSnapshot.Take(config);
        // NoteView.ToChecklist: her dolu satır "☐ " önekli bir madde olur.
        config.NoteText = ChecklistText.Format(ChecklistText.Parse(plain));
        config.NoteChecklist = true;
        Assert.NotEqual(plain, config.NoteText);

        var changed = snapshot.Restore(config);
        Assert.Contains(nameof(WidgetConfig.NoteText), changed);
        Assert.False(config.NoteChecklist);
        Assert.Equal(plain, config.NoteText);

        // Listeye çevrildikten sonra yazılan (ya da işaretlenen) kalır; yalnızca tür geri gelir.
        config.NoteText = ChecklistText.Format(ChecklistText.Parse(plain)) + "\n☐ peynir";
        config.NoteChecklist = true;
        changed = snapshot.Restore(config);
        Assert.DoesNotContain(nameof(WidgetConfig.NoteText), changed);
        Assert.False(config.NoteChecklist);
        Assert.EndsWith("☐ peynir", config.NoteText);

        // Listeyi düz nota çevirmek metne dokunmaz: geri alma da dokunmaz.
        var list = new WidgetConfig { Kind = WidgetKind.Note, NoteChecklist = true, NoteText = "☐ a\n☑ b" };
        var opened = ConfigSnapshot.Take(list);
        list.NoteChecklist = false;
        list.NoteText = "☐ a\n☑ b\nyazılan";
        opened.Restore(list);
        Assert.True(list.NoteChecklist);
        Assert.Equal("☐ a\n☑ b\nyazılan", list.NoteText);
    }

    [Fact]
    public void Kept_fields_are_real_properties()
    {
        var names = typeof(WidgetConfig).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.All(ConfigSnapshot.Kept, name => Assert.Contains(name, names));
        // Görünüş ve ayar alanları geri alınır.
        foreach (var name in new[] { "Style", "Accent", "LookTheme", "LookAccentColor", "Scale", "Opacity", "Corners", "Shadow", "Locked",
                     "HiddenParts", "Title", "Icon", "Filter", "FolderName", "Variant", "Timer", "Tabs", "Extra" })
            Assert.DoesNotContain(name, ConfigSnapshot.Kept);
    }
}
