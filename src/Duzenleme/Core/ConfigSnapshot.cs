using System.Reflection;
using System.Text.Json;

namespace Duzenleme.Core;

/// <summary>
/// Düzenle panelinin "Geri al"ı: panel açılınca widget'ın ayarının JSON kopyası alınır (daha yeni sürümün alanları,
/// <see cref="WidgetConfig.Extra"/> dahil) ve "Geri al" onu aynı ayar nesnesine geri yazar (pencereler ve listeler aynı nesneyi
/// tutar). Geri yazma her seferinde kopyadan yeni nesneler üretir: kopya hiçbir zaman ayarla paylaşılmaz, iki kez geri yazmak
/// bir kez yazmakla aynıdır.
/// <para>Ayar değil içerik ve yer olan alanlara dokunulmaz (<see cref="Kept"/>): panel açıkken yazılan not, taşınan ya da
/// büyütülen kart, elle verilen sıra, kutu klasörü. Zamanlayıcının çalışma durumu (bitiş anı, kalan süre…) de korunur;
/// yalnızca türü panelde değiştirildiyse bütünüyle geri gelir (tür değişince çalışma durumu zaten sıfırlanmıştır).</para>
/// </summary>
public sealed class ConfigSnapshot
{
    /// <summary>Geri alınmayan alanlar: kimlik, tür, yer ve boyut, sıra, notun metni, elle sıra, kutu klasörü ve seçili sekme.</summary>
    public static readonly IReadOnlySet<string> Kept = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(WidgetConfig.Id), nameof(WidgetConfig.Kind),
        nameof(WidgetConfig.Left), nameof(WidgetConfig.Top), nameof(WidgetConfig.PixelLeft), nameof(WidgetConfig.PixelTop),
        nameof(WidgetConfig.Width), nameof(WidgetConfig.Height), nameof(WidgetConfig.Z),
        nameof(WidgetConfig.NoteText), nameof(WidgetConfig.ItemOrder), nameof(WidgetConfig.BoxFolder), nameof(WidgetConfig.ActiveTab),
    };

    private static readonly PropertyInfo[] Restorable = typeof(WidgetConfig)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && !Kept.Contains(p.Name))
        .ToArray();

    private readonly string _json;

    private ConfigSnapshot(string id, string json)
    {
        Id = id;
        _json = json;
    }

    /// <summary>Kopyalanan widget'ın kimliği.</summary>
    public string Id { get; }

    /// <summary>Ayarın şimdiki hâlini kopyalar (JSON; bilinmeyen alanlar da).</summary>
    public static ConfigSnapshot Take(WidgetConfig config) => new(config.Id, JsonSerializer.Serialize(config, JsonFile.Options));

    /// <summary>Kopyanın yeni bir nesnesi (ayarla paylaşılmaz).</summary>
    public WidgetConfig Copy() => JsonSerializer.Deserialize<WidgetConfig>(_json, JsonFile.Options)!;

    /// <summary>
    /// Kopyayı <paramref name="target"/>'a geri yazar (<see cref="Kept"/> dışındaki bütün alanlar). Değişen alanların adlarını
    /// döner; hiçbiri değişmediyse boş (ikinci geri yazma).
    /// </summary>
    public IReadOnlyList<string> Restore(WidgetConfig target)
    {
        var copy = Copy();
        var changed = new List<string>();
        foreach (var property in Restorable)
        {
            if (property.Name == nameof(WidgetConfig.Timer))
            {
                if (RestoreTimer(copy.Timer, target)) changed.Add(property.Name);
                continue;
            }
            var then = property.GetValue(copy);
            if (SameJson(then, property.GetValue(target), property.PropertyType)) continue;
            property.SetValue(target, then);
            changed.Add(property.Name);
        }
        return changed;
    }

    /// <summary>Geri alınacak bir şey var mı (<see cref="Kept"/> dışında kopyadan farklı bir alan)?</summary>
    public bool Differs(WidgetConfig current)
    {
        var copy = Copy();
        foreach (var property in Restorable)
        {
            if (property.Name == nameof(WidgetConfig.Timer))
            {
                if (TimerDiffers(copy.Timer, current.Timer)) return true;
                continue;
            }
            if (!SameJson(property.GetValue(copy), property.GetValue(current), property.PropertyType)) return true;
        }
        return false;
    }

    // Zamanlayıcının ayarları (tür, süreler, bildirim, kendiliğinden başlama); öbürleri çalışma durumudur.
    private static readonly string[] TimerSettings =
    [
        nameof(TimerState.Mode), nameof(TimerState.Minutes), nameof(TimerState.FocusMinutes), nameof(TimerState.BreakMinutes),
        nameof(TimerState.LongBreakMinutes), nameof(TimerState.RoundsBeforeLong), nameof(TimerState.AutoStartNext),
        nameof(TimerState.Notify), nameof(TimerState.Extra),
    ];

    private static bool TimerDiffers(TimerState? then, TimerState? now)
    {
        if (then is null || now is null || then.Mode != now.Mode) return !SameJson(then, now, typeof(TimerState));
        return TimerSettings.Any(name =>
        {
            var p = typeof(TimerState).GetProperty(name)!;
            return !SameJson(p.GetValue(then), p.GetValue(now), p.PropertyType);
        });
    }

    /// <summary>Tür aynıysa yalnızca ayarları, değilse (ya da biri yoksa) bütün durumu geri yazar. Değiştiyse true.</summary>
    private static bool RestoreTimer(TimerState? then, WidgetConfig target)
    {
        if (!TimerDiffers(then, target.Timer)) return false;
        if (then is null || target.Timer is null || then.Mode != target.Timer.Mode)
        {
            target.Timer = then;
            return true;
        }
        foreach (var name in TimerSettings)
        {
            var p = typeof(TimerState).GetProperty(name)!;
            p.SetValue(target.Timer, p.GetValue(then));
        }
        return true;
    }

    private static bool SameJson(object? a, object? b, Type type) =>
        ReferenceEquals(a, b) ||
        JsonSerializer.Serialize(a, type, JsonFile.Options) == JsonSerializer.Serialize(b, type, JsonFile.Options);
}
