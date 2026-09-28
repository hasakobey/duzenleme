using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Yeni widget'ın nereye yerleşeceği: bir nokta (fiziksel piksel) ve kip. Widget eklenirken belirlenir ve pencere
/// yerleşene dek pencerede durur (eskiden tek seferlik genel bir ipucuydu; Loaded'ın zamanına bağlıydı).
/// </summary>
/// <param name="Anchor">İmleç ya da eklemenin yapıldığı pencerenin ortası.</param>
/// <param name="FromWindow">Nokta bir pencereden geldi: "ekranın ortası" o pencerenin ekranı demektir, etkin pencereninki değil.</param>
internal readonly record struct WidgetPlacement(NativeMethods.POINT Anchor, PlaceMode Mode, bool FromWindow)
{
    /// <summary>İmlecin yeri; testte DUZENLEME_NEWWIDGET_AT="x,y" (test örneğinin widget'ı kullanıcının ekranına düşmesin).</summary>
    public static NativeMethods.POINT Pointer(out bool fromTest)
    {
        if (NativeMethods.PointFromEnvironment("DUZENLEME_NEWWIDGET_AT") is { } test)
        {
            fromTest = true;
            return test;
        }
        fromTest = false;
        NativeMethods.GetCursorPos(out var cursor);
        return cursor;
    }

    /// <summary>Ayardaki kip, imlecin yerinde (kısayol, tepsi, widget menüsü, Çoğalt).</summary>
    public static WidgetPlacement FromSettings()
    {
        var pointer = Pointer(out var fromTest);
        return new(pointer, PlaceModes.Parse(AppHost.Settings.NewWidgetPlacement), FromWindow: fromTest);
    }

    /// <summary>Ayardaki kip, verilen noktada ("Widget ekle" penceresinin bulunduğu yer).</summary>
    public static WidgetPlacement FromSettingsAt(NativeMethods.POINT near) =>
        new(near, PlaceModes.Parse(AppHost.Settings.NewWidgetPlacement), FromWindow: true);

    /// <summary>
    /// Türüne göre köşe, noktanın ekranında (Widget'lar sayfası, karşılama, başlangıç bölmeleri: ana pencere açık kalır,
    /// imlecin yanına konan widget onun arkasında kaybolurdu). Nokta yoksa imlecin ekranı.
    /// </summary>
    public static WidgetPlacement CornerNear(NativeMethods.POINT? near) =>
        near is { } point ? new(point, PlaceMode.Corner, true) : new(Pointer(out _), PlaceMode.Corner, true);

    /// <summary>Yerleşilecek çalışma alanı: noktanın ekranı; "ortası" kipinde nokta bir pencereden gelmediyse etkin pencerenin ekranı.</summary>
    public NativeMethods.RECT WorkArea(out double scale)
    {
        if (Mode == PlaceMode.Center && !FromWindow)
        {
            var foreground = NativeMethods.GetForegroundWindow();
            // Masaüstünün kendisi (tüm ekranları kaplar) etkin pencereyse imlecin ekranı kullanılır.
            if (foreground != IntPtr.Zero && !Desktop.DesktopIcons.IsDesktopSurface(foreground) &&
                NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONULL) is var monitor && monitor != IntPtr.Zero)
            {
                var area = NativeMethods.WorkAreaOfMonitor(monitor);
                scale = NativeMethods.ScaleAt(new NativeMethods.POINT { X = (area.Left + area.Right) / 2, Y = (area.Top + area.Bottom) / 2 });
                return area;
            }
        }
        scale = NativeMethods.ScaleAt(Anchor);
        return NativeMethods.WorkAreaAt(Anchor);
    }
}
