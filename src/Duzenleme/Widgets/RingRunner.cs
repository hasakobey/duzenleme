using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Halka öğesini çalıştırır. Yol <see cref="TileItem.Launch"/> ile (arka planda); web adresi günlüğe yazılmadan varsayılan
/// tarayıcıda; komut yalnızca kullanıcının yazdığıyla, arka planda (konsolu gizlemek ve "yönetici olarak" öğe başına açık
/// seçimdir; yönetici onayını Windows her seferinde sorar); NestDesk eylemi kendi kısayolunun işiyle. Tuş ve metin öğeleri
/// yalnızca kısayolla açılan halkadan çalışır (hedef, halka açılmadan önce öndeki penceredir): masaüstündeki widget'tan
/// seçilince bunu söyler. Çalıştırılamayan öğe (<see cref="RingActions.IsRunnable"/>) asla çalışmaz. Öğe içerikleri (adres,
/// komut, metin, tuşlar) hiçbir günlüğe yazılmaz; hata yalnızca türüyle.
/// </summary>
internal static class RingRunner
{
    /// <summary>Masaüstündeki widget'ta seçilen öğe.</summary>
    public static void RunFromWidget(RingSlot slot)
    {
        switch (slot.Kind)
        {
            case RingSlotKind.Path when slot.Path is { } path:
                // Bulunamayan / ulaşılamayan yol soluk görünür ve açılmaz (kısayol kutusundaki gibi).
                if (slot.Tile is not { Missing: true }) TileItem.Launch(path);
                break;
            case RingSlotKind.Action when slot.Action is { } action:
                if (RingActionTypes.NeedsTargetWindow(action.Type))
                {
                    Views.Notice.Show(L.T("Bu öğe kısayolla açılan halkada çalışır."), Views.NoticeKind.Info);
                    return;
                }
                Run(action);
                break;
        }
    }

    /// <summary>
    /// Pencere hedeflemeyen öğe (web adresi, komut, NestDesk eylemi); tuş ve metin öğesi burada çalışmaz (bkz. sınıf). Çalıştırdıysa
    /// true; çalıştırılamayan öğede nedenini söyler.
    /// </summary>
    public static bool Run(LauncherAction action)
    {
        if (!RingActions.IsRunnable(action))
        {
            Views.Notice.Show(action.Disabled ? L.T("Bu öğe kapalı: başka bir yerden aktarıldı ve henüz onaylanmadı.")
                : RingActionTypes.IsKnown(action.Type) ? L.T("Bu öğe eksik ya da geçersiz. Düzenle…'yi kullanarak düzeltebilirsin.")
                : L.F("Bu öğe {0}'in daha yeni bir sürümünde eklenmiş; bu sürümde çalışmaz.", AppInfo.Name), Views.NoticeKind.Warning);
            return false;
        }
        switch (action.Type)
        {
            case RingActionTypes.Url:
                if (!RingActions.TryParseUrl(action.Url, out var uri, out _)) return false;
                NativeMethods.AllowSetForegroundWindow(-1);
                Views.Browser.Open(uri.AbsoluteUri, log: false);
                return true;
            case RingActionTypes.Command:
                StartCommand(action);
                return true;
            case RingActionTypes.App when RingAppIds.ToHotkeyAction(action.App) is { } hotkey:
                (Application.Current as App)?.RunAction(hotkey);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Komutu arka planda başlatır (kabuk çağrısı arayüzü bekletmez). Yönetici onayından vazgeçmek sessizdir.</summary>
    private static void StartCommand(LauncherAction action)
    {
        var info = new ProcessStartInfo(Environment.ExpandEnvironmentVariables(action.Command!.Trim().Trim('"')))
        {
            UseShellExecute = true,
            Arguments = action.Args?.Trim() ?? "",
            // Konsolu gizlemek öğe başına açık bir seçimdir (varsayılan görünür).
            WindowStyle = action.HideWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
        };
        if (!string.IsNullOrWhiteSpace(action.Dir)) info.WorkingDirectory = Environment.ExpandEnvironmentVariables(action.Dir.Trim().Trim('"'));
        if (action.Admin) info.Verb = "runas";
        // Başlatılan program öne gelebilsin.
        NativeMethods.AllowSetForegroundWindow(-1);
        Task.Run(() =>
        {
            try { Process.Start(info)?.Dispose(); }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // Kullanıcı yönetici onayını iptal etti.
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or ArgumentException or PlatformNotSupportedException)
            {
                DebugLog.Write($"RingRunner: {ex.GetType().Name}");
                Views.Notice.Show(L.T("Komut çalıştırılamadı. Programın adını ve çalışma klasörünü denetle."), Views.NoticeKind.Warning);
            }
        });
    }
}
