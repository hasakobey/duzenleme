using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Widget ayarından görünümü kurar: önce alt tür (<see cref="WidgetVariants.Of"/>), yoksa temel tür. Tanınmayan (daha yeni
/// sürümün) alt tür temel türüyle açılır ve ayarı korunur. Yeni widget türü yalnızca buraya, <see cref="WidgetSeeds"/>'e,
/// <see cref="WidgetText"/>'e ve ekleme kataloğuna (Views/WidgetCatalog) eklenir.
/// </summary>
public static class WidgetViews
{
    public static IWidgetView Create(WidgetConfig config) => (config.Kind, WidgetVariants.Of(config)) switch
    {
        (WidgetKind.Date, WidgetVariants.Month) => new CalendarView(config),
        (WidgetKind.Date, WidgetVariants.Countdown) => new CountdownView(config),
        (WidgetKind.Clock, WidgetVariants.Timer) => new TimerView(config),
        (WidgetKind.Clock, WidgetVariants.World) => new WorldClockView(config),
        (WidgetKind.Clock, WidgetVariants.System) => new SystemStatusView(config),
        (WidgetKind.Launcher, WidgetVariants.Recycle) => new RecycleBinView(config),
        (WidgetKind.Clock, _) => new ClockView(config),
        (WidgetKind.Date, _) => new DateView(config),
        (WidgetKind.Note, _) => new NoteView(config),
        (WidgetKind.Launcher, _) => new LauncherView(config),
        _ => new FenceView(config),
    };
}
