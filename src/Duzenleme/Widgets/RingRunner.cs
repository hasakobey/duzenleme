using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Duzenleme.Core;
using Duzenleme.Desktop;

namespace Duzenleme.Widgets;

/// <summary>
/// Halka öğesini çalıştırır. Yol <see cref="TileItem.Launch"/> ile (arka planda; "yönetici olarak" yalnızca runas ile, Windows
/// her seferinde sorar); web adresi günlüğe yazılmadan varsayılan tarayıcıda; komut yalnızca kullanıcının yazdığıyla, arka
/// planda (konsolu gizlemek ve "yönetici olarak" öğe başına açık seçimdir); NestDesk eylemi kendi kısayolunun işiyle
/// (<see cref="App.RunAction"/>). Tuş ve metin öğeleri yalnızca kısayolla açılan halkadan çalışır (<see cref="SendToTargetAsync"/>;
/// hedef, halka açılmadan önce öndeki penceredir): masaüstündeki widget'tan seçilince bunu söyler. Çalıştırılamayan öğe
/// (<see cref="RingActions.IsRunnable"/>) asla çalışmaz. Öğe içerikleri (adres, komut, metin, tuşlar) hiçbir günlüğe yazılmaz;
/// hata yalnızca türüyle.
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

    /// <summary>Öğe, halka kapandıktan sonra önceki pencereye mi gider (tuş ve metin)?</summary>
    public static bool NeedsTarget(RingSlot slot) =>
        slot is { Kind: RingSlotKind.Action, Action: { } action } && RingActionTypes.NeedsTargetWindow(action.Type);

    /// <summary>
    /// Kısayolla açılan halkada seçilen, pencere hedeflemeyen öğe (yol, web adresi, komut, NestDesk eylemi). Halka henüz
    /// öndeyken çağrılır: açılan program öne gelebilsin diye izin önceden verilir. <paramref name="asAdmin"/>: yol öğesi
    /// yönetici olarak (Ctrl+Shift ile seçilince; yalnızca program ve kısayollarda, Windows her seferinde sorar).
    /// </summary>
    public static void RunFromPopup(RingSlot slot, bool asAdmin)
    {
        NativeMethods.AllowSetForegroundWindow(-1);
        switch (slot.Kind)
        {
            case RingSlotKind.Path when slot.Path is { } path:
                if (slot.Tile is { Missing: true }) return;
                TileItem.Launch(path, asAdmin && CanRunAsAdmin(path));
                break;
            case RingSlotKind.Action when slot.Action is { } action && !RingActionTypes.NeedsTargetWindow(action.Type):
                Run(action);
                break;
        }
    }

    /// <summary>Yol yönetici olarak çalıştırılabilir mi (program, kısayol, betik; kabuk nesnesi değil)?</summary>
    public static bool CanRunAsAdmin(string path) =>
        !TileItem.IsShellObject(path) &&
        System.IO.Path.GetExtension(path).ToLowerInvariant() is ".exe" or ".lnk" or ".bat" or ".cmd" or ".msc";

    /// <summary>
    /// Pencere hedeflemeyen öğe (web adresi, komut, NestDesk eylemi); tuş ve metin öğesi burada çalışmaz (bkz. sınıf). Çalıştırdıysa
    /// true; çalıştırılamayan öğede nedenini söyler.
    /// </summary>
    public static bool Run(LauncherAction action)
    {
        if (!Runnable(action)) return false;
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

    /// <summary>Çalıştırılabilir mi; değilse nedenini söyler (kapalı, eksik ya da daha yeni sürümün öğesi).</summary>
    private static bool Runnable(LauncherAction action)
    {
        if (RingActions.IsRunnable(action)) return true;
        Views.Notice.Show(action.Disabled ? L.T("Bu öğe kapalı: başka bir yerden aktarıldı ve henüz onaylanmadı.")
            : RingActionTypes.IsKnown(action.Type) ? L.T("Bu öğe eksik ya da geçersiz. Halkadaki \"Değiştir…\" ile düzeltebilirsin.")
            : L.F("Bu öğe {0}'in daha yeni bir sürümünde eklenmiş; bu sürümde çalışmaz.", AppInfo.Name), Views.NoticeKind.Warning);
        return false;
    }

    /// <summary>
    /// Komutu arka planda başlatır (kabuk çağrısı ve dosya denetimi arayüzü bekletmez). "Program ya da komut" alanı bütün bir
    /// komut satırı da olabilir ("notepad C:\a.txt", "\"C:\Program Files\x.exe\" -f"): program ve bağımsız değişkenler
    /// <see cref="RingActions.SplitCommand"/> ile ayrılır, Bağımsız değişkenler alanı sona eklenir. Yönetici onayından vazgeçmek
    /// sessizdir.
    /// </summary>
    private static void StartCommand(LauncherAction action)
    {
        var command = Environment.ExpandEnvironmentVariables(action.Command!.Trim());
        var extra = action.Args?.Trim() ?? "";
        var hide = action.HideWindow;
        var admin = action.Admin;
        var dir = string.IsNullOrWhiteSpace(action.Dir) ? null : Environment.ExpandEnvironmentVariables(action.Dir.Trim().Trim('"'));
        // Başlatılan program öne gelebilsin.
        NativeMethods.AllowSetForegroundWindow(-1);
        Task.Run(() =>
        {
            try
            {
                var (program, inline) = RingActions.SplitCommand(command, File.Exists);
                var info = new ProcessStartInfo(program)
                {
                    UseShellExecute = true,
                    Arguments = string.Join(" ", new[] { inline, extra }.Where(a => a.Length > 0)),
                    // Konsolu gizlemek öğe başına açık bir seçimdir (varsayılan görünür).
                    WindowStyle = hide ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                };
                if (dir is not null) info.WorkingDirectory = dir;
                if (admin) info.Verb = "runas";
                Process.Start(info)?.Dispose();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // Kullanıcı yönetici onayını iptal etti.
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or ArgumentException or PlatformNotSupportedException)
            {
                // Günlüğe yalnızca hatanın türü ve Windows hata kodu (komut, program adı ve ileti asla).
                DebugLog.Write($"RingRunner: {ex.GetType().Name}");
                var code = ex is Win32Exception win32 ? win32.NativeErrorCode : ex.HResult;
                AppLog.Warn("ring", "command-failed", code);
                Views.Notice.Show(L.T("Komut çalıştırılamadı. Programın adını ve çalışma klasörünü denetle."), Views.NoticeKind.Warning);
            }
        });
    }

    // ---------------------------------------------------------------- tuş ve metin (yalnızca kısayolla açılan halka)

    /// <summary>
    /// Tuş ya da metin öğesini <paramref name="target"/> penceresine (halka açılmadan önce öndeki) gönderir. Halka gerçekten
    /// kapandıktan sonra çağrılır (Motion'ın kapanış geri çağırması): önce hedef öne getirilir, kısayolun tuşları bırakılana dek
    /// (en çok 2 sn) beklenir, sonra <see cref="KeySender.SendToAsync"/> / <see cref="SnippetPaste.PasteAsync"/>; sonuç gerekiyorsa
    /// bildirime çevrilir. Hedefin önde, masaüstü/kendi penceremiz olmadığına ve girdiyi alabildiğine o API'ler bakar.
    /// </summary>
    /// <param name="hotkeyKeys">Halkayı açan kısayolun sanal tuşları (bırakılmaları beklenir; boşsa beklenmez).</param>
    public static async Task SendToTargetAsync(LauncherAction action, IntPtr target, IReadOnlyCollection<int> hotkeyKeys)
    {
        // Halka geri getirmeden kapandı: öğe çalışamasa da odak önceki pencereye döner (Windows'un seçtiği yerde kalmasın).
        if (target != IntPtr.Zero) NativeMethods.SetForegroundWindow(target);
        if (!Runnable(action)) return;
        try
        {
            await KeySender.WaitForKeysReleasedAsync(hotkeyKeys, KeySender.MaxWait);
            switch (action.Type)
            {
                case RingActionTypes.Keys when action.Keys is { } keys:
                    var sent = await KeySender.SendToAsync(target, keys);
                    if (sent == SendResult.Elevated) Show(KeysNotice(sent, await ElevatedAsync(target)));
                    else Show(KeysNotice(sent, elevated: null));
                    break;
                case RingActionTypes.Text when action.Text is { } text:
                    var pasted = await SnippetPaste.PasteAsync(target, text);
                    if (pasted == SnippetPaste.Outcome.Elevated) Show(TextNotice(pasted, await ElevatedAsync(target)));
                    else Show(TextNotice(pasted, elevated: null));
                    break;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Çağıran beklemez (kapanış geri çağırması): hata burada kalır. Günlüğe yalnızca türü.
            DebugLog.Write($"RingRunner: {ex.GetType().Name}");
            Views.Notice.Show(action.Type == RingActionTypes.Keys ? L.T("Tuşlar gönderilemedi.")
                : L.T("Metin yapıştırılamadı; pano şu an kullanılamıyor olabilir."), Views.NoticeKind.Warning);
        }
    }

    /// <summary>Hedef yönetici olarak mı çalışıyor (yalnızca bildirimin metni için; süreç sorgusu arka planda).</summary>
    private static Task<bool?> ElevatedAsync(IntPtr target) => Task.Run(() => ForegroundInfo.IsElevated(target));

    private static void Show((string? Text, Views.NoticeKind Kind) notice)
    {
        if (notice.Text is { } text) Views.Notice.Show(text, notice.Kind);
    }

    /// <summary>Tuş gönderiminin bildirimi (saf; testlenir). Gönderildiyse sessiz (null).</summary>
    /// <param name="elevated">Hedef yönetici olarak mı çalışıyor (Elevated'da metni seçer); bilinmiyorsa null.</param>
    public static (string? Text, Views.NoticeKind Kind) KeysNotice(SendResult result, bool? elevated) => result switch
    {
        SendResult.Sent => (null, Views.NoticeKind.Info),
        SendResult.Busy => (L.T("Önceki gönderim sürüyor; biraz sonra yeniden dene."), Views.NoticeKind.Info),
        SendResult.Invalid => (L.T("Bu öğe eksik ya da geçersiz. Halkadaki \"Değiştir…\" ile düzeltebilirsin."), Views.NoticeKind.Warning),
        SendResult.NoTarget => (L.T("Gönderilecek pencere yok. Halkayı bir uygulamanın penceresindeyken aç."), Views.NoticeKind.Info),
        SendResult.NotInFront => (L.T("Hedef pencere öne gelmedi; tuş gönderilmedi."), Views.NoticeKind.Info),
        SendResult.Elevated => (elevated == true ? L.T("Yönetici olarak çalışan pencereye tuş gönderilemez.")
            : L.T("Windows bu pencereye tuş gönderilmesine izin vermiyor."), Views.NoticeKind.Warning),
        _ => (L.T("Tuşlar gönderilemedi."), Views.NoticeKind.Warning),
    };

    /// <summary>Metin yapıştırmanın bildirimi (saf; testlenir). Yapıştırılıp pano geri konduysa ya da pano bu arada değiştiyse sessiz.</summary>
    public static (string? Text, Views.NoticeKind Kind) TextNotice(SnippetPaste.Outcome outcome, bool? elevated) => outcome switch
    {
        SnippetPaste.Outcome.Restored or SnippetPaste.Outcome.ClipboardChanged => (null, Views.NoticeKind.Info),
        SnippetPaste.Outcome.PartiallyRestored =>
            (L.T("Metin yapıştırıldı; panodaki önceki içeriğin bir bölümü geri konamadı."), Views.NoticeKind.Info),
        SnippetPaste.Outcome.NotRestorable =>
            (L.T("Metin yapıştırıldı; önceki pano içeriği geri konamadığı için pano boşaltıldı."), Views.NoticeKind.Info),
        SnippetPaste.Outcome.PastedNotRestored =>
            (L.T("Metin yapıştırıldı ama önceki pano içeriği geri konamadı; metin panoda kalmış olabilir."), Views.NoticeKind.Warning),
        SnippetPaste.Outcome.Busy => (L.T("Önceki gönderim sürüyor; biraz sonra yeniden dene."), Views.NoticeKind.Info),
        SnippetPaste.Outcome.NoTarget => (L.T("Gönderilecek pencere yok. Halkayı bir uygulamanın penceresindeyken aç."), Views.NoticeKind.Info),
        SnippetPaste.Outcome.NotInFront => (L.T("Hedef pencere öne gelmedi; metin yapıştırılmadı."), Views.NoticeKind.Info),
        SnippetPaste.Outcome.Elevated => (elevated == true ? L.T("Yönetici olarak çalışan pencereye metin yapıştırılamaz.")
            : L.T("Windows bu pencereye metin yapıştırılmasına izin vermiyor."), Views.NoticeKind.Warning),
        _ => (L.T("Metin yapıştırılamadı; pano şu an kullanılamıyor olabilir."), Views.NoticeKind.Warning),
    };
}
