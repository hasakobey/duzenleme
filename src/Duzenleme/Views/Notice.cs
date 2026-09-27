using System.Windows;
using Duzenleme.Core;

namespace Duzenleme.Views;

public enum NoticeKind { Success, Info, Warning, Error }

/// <summary>
/// Kullanıcı eyleminin sonucunu bildirir. Ana pencere açıksa altındaki kalıcı şeritte (isteğe bağlı eylem düğmesiyle,
/// ör. "Geri al"), değilse tepsi balonunda. Kendiliğinden kaybolan ya da fareye bağlı davranan bir öğe değildir.
/// </summary>
public static class Notice
{
    /// <summary>Ana pencere görünürse şeritte gösterir; değilse tepsi balonuna düşer:
    /// Tray.Notify(AppInfo.Name, trayHint is null ? text : text + " " + trayHint, action).</summary>
    public static void Show(string text, NoticeKind kind = NoticeKind.Success, string? actionText = null, Action? action = null, string? trayHint = null)
    {
        if (Application.Current is not { } app) return;
        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() => Show(text, kind, actionText, action, trayHint));
            return;
        }
        if (VisibleMainWindow() is { } window)
        {
            window.ShowNotice(text, kind, actionText, action);
            return;
        }
        AppHost.Tray?.Notify(AppInfo.Name, trayHint is null ? text : text + " " + trayHint, action);
    }

    public static void Hide()
    {
        if (Application.Current is not { } app) return;
        foreach (var window in app.Windows.OfType<MainWindow>()) window.HideNotice();
    }

    /// <summary>Kullanıcının şeridi görebileceği ana pencere (açık ve simge durumunda değil); yoksa null.</summary>
    private static MainWindow? VisibleMainWindow() =>
        Application.Current.Windows.OfType<MainWindow>()
            .FirstOrDefault(w => w.IsVisible && w.WindowState != WindowState.Minimized);
}
