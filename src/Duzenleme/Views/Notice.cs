using System.Windows;
using Duzenleme.Core;

namespace Duzenleme.Views;

public enum NoticeKind { Success, Info, Warning, Error }

/// <summary>Şeritteki bir seçenek düğmesi (<see cref="Notice.Ask"/>): metni ve basılınca yapılacak iş.</summary>
public sealed record NoticeChoice(string Text, Action Action);

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
            window.ShowNotice(text, kind, actionText is null || action is null ? [] : [new NoticeChoice(actionText, action)], primary: false);
            return;
        }
        AppHost.Tray?.Notify(AppInfo.Name, trayHint is null ? text : text + " " + trayHint, action);
    }

    /// <summary>
    /// Soru şeridi: 2–3 seçenek düğmesi (ilki birincil), metnin altında. Düğmeler hep görünür (fareyle belirmez); şerit bir
    /// seçenek seçilene, kapatılana, yenisi gelene, sayfa değişene ya da pencere gizlenene dek durur. Ana pencere görünmüyorsa
    /// tepsi balonu çıkar; balona tıklanınca ana pencere açılır ve aynı şerit gösterilir.
    /// </summary>
    public static void Ask(string text, NoticeKind kind, params NoticeChoice[] choices) => Ask(text, kind, choices, allowTray: true);

    private static void Ask(string text, NoticeKind kind, NoticeChoice[] choices, bool allowTray)
    {
        if (Application.Current is not { } app) return;
        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() => Ask(text, kind, choices, allowTray));
            return;
        }
        if (VisibleMainWindow() is { } window)
        {
            window.ShowNotice(text, kind, choices.Take(3).ToList(), primary: true);
            return;
        }
        if (!allowTray) return;
        AppHost.Tray?.Notify(AppInfo.Name, text, () =>
        {
            (app as App)?.ShowMainWindow();
            // Pencere yüklenip ilk sayfaya geçtikten sonra (sayfa değişince şerit kalkar).
            app.Dispatcher.BeginInvoke(() => Ask(text, kind, choices, allowTray: false), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        });
    }

    /// <summary>
    /// Açılamayan klasörü bildirir (<see cref="FolderCreation.TryCreate"/>). Güvenlik engeliyse Windows Güvenliği'nin
    /// ilgili sayfasını açan düğme de çıkar.
    /// </summary>
    public static void FolderFailed(FolderCreateResult result)
    {
        if (result.Ok) return;
        if (result.BlockedBySecurity)
            Show(result.Error!, NoticeKind.Warning, L.T("Windows Güvenliği'ni aç"),
                () => Browser.Open(FolderCreation.RansomwareProtectionUri));
        else Show(result.Error!, NoticeKind.Warning);
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
