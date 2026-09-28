using System.IO;

namespace Duzenleme.Core;

/// <summary>Yeni widget'ın köşe kipinde başlayacağı yer (kalıcı değil).</summary>
public enum WidgetCorner { TopCenter, TopRight, BottomRight }

/// <summary>
/// Widget alt türleri (<see cref="WidgetConfig.Variant"/>). Yeni widget türü <see cref="WidgetKind"/>'a üye eklemez: bilinen
/// bir temel tür + metin alt tür olur. 2.0 alt türü tanımaz ve temel türü gösterir (takvim → tarih, zamanlayıcı → saat,
/// Geri Dönüşüm Kutusu → içinde kutunun durduğu kısayol kutusu); 2.1 tanımadığı (daha yeni) alt türü korur ve temel türü
/// gösterir. Temel türü yanlış olan alt tür (elle düzenlenmiş dosya) yok sayılır.
/// </summary>
public static class WidgetVariants
{
    /// <summary>Aylık takvim (Date).</summary>
    public const string Month = "month";

    /// <summary>Bir güne geri sayım (Date).</summary>
    public const string Countdown = "countdown";

    /// <summary>Zamanlayıcı / Pomodoro / kronometre (Clock).</summary>
    public const string Timer = "timer";

    /// <summary>Dünya saati (Clock).</summary>
    public const string World = "world";

    /// <summary>Sistem durumu: işlemci, bellek, disk, pil (Clock).</summary>
    public const string System = "system";

    /// <summary>Geri Dönüşüm Kutusu (Launcher; kutunun tek öğesi Geri Dönüşüm Kutusu'dur).</summary>
    public const string Recycle = "recycle";

    public static IReadOnlyList<string> All => [Month, Countdown, Timer, World, System, Recycle];

    /// <summary>Alt türün temel türü; tanınmayan alt türde null.</summary>
    public static WidgetKind? BaseKind(string? variant) => variant switch
    {
        Month or Countdown => WidgetKind.Date,
        Timer or World or System => WidgetKind.Clock,
        Recycle => WidgetKind.Launcher,
        _ => null,
    };

    /// <summary>Widget'ın bu sürümde tanınan alt türü; yoksa (klasik widget, bilinmeyen ya da temel türü uymayan alt tür) null.</summary>
    public static string? Of(WidgetConfig config) =>
        config.Variant is { } variant && BaseKind(variant) is { } kind && kind == config.Kind ? variant : null;

    public static bool Is(WidgetConfig config, string variant) => Of(config) == variant;

    /// <summary>
    /// Klasör portalı: masaüstünden bağımsız herhangi bir klasörü (İndirilenler, Belgeler…) gösteren bölme. Alt tür değil:
    /// FolderName tam yoldur (2.0 aynı yolu "klasör yok" diye başlıkla gösterir, veri kaybolmaz). Masaüstündeki klasörün
    /// bölmesinde FolderName yalnızca addır.
    /// </summary>
    public static bool IsPortal(WidgetConfig config) =>
        config.Kind == WidgetKind.Fence && config.Filter == DesktopFilter.None &&
        config.FolderName is { Length: > 0 } folder && Path.IsPathFullyQualified(folder);

    /// <summary>Köşe kipinde başlangıç yeri: saat, tarih, not ve onların alt türleri sağ üstte, Geri Dönüşüm Kutusu sağ altta.</summary>
    public static WidgetCorner Corner(WidgetConfig config) => Of(config) switch
    {
        Recycle => WidgetCorner.BottomRight,
        _ => config.Kind is WidgetKind.Clock or WidgetKind.Date or WidgetKind.Note ? WidgetCorner.TopRight : WidgetCorner.TopCenter,
    };
}

/// <summary>Zamanlayıcı kipleri ve Pomodoro aşamaları (<see cref="TimerState"/>'te metin olarak saklanır).</summary>
public static class TimerModes
{
    public const string Countdown = "countdown";
    public const string Pomodoro = "pomodoro";
    public const string Stopwatch = "stopwatch";

    public const string Focus = "focus";
    public const string Break = "break";
    public const string LongBreak = "long";

    /// <summary>Tanınan kip; bilinmeyen (daha yeni sürümün) kip zamanlayıcı sayılır.</summary>
    public static string Normalize(string? mode) => mode is Pomodoro or Stopwatch ? mode : Countdown;

    public static string NormalizePhase(string? phase) => phase is Break or LongBreak ? phase : Focus;
}
