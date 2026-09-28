using System.IO;
using System.Windows;
using System.Windows.Interop;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Views;

namespace Duzenleme.Widgets;

/// <summary>
/// Bölmedeki dosya ya da klasörün diskte yeniden adlandırılması (F2), Gezgin gibi: gizli uzantı (.lnk…) korunur, geçersiz ad
/// kutu açıkken söylenir, uzantı değişimi sorulur, aynı adda öğe varsa "(2)" önerilir, izin isteyen yerde (Ortak Masaüstü)
/// Windows'un kendi yönetici onayı çıkar. Taşıma arka planda yapılır; ardından <see cref="AppHost.NotePathRenamed"/> taşıma
/// geçmişini (geri alınan dosya yeniden taşınmasın), kutu kayıtlarını ve widget ayarlarını günceller.
/// </summary>
internal static class ItemRename
{
    private enum Outcome { Done, Exists, Denied, Missing, Failed }

    private sealed record MoveResult(Outcome Outcome, string? Suggested = null, string? Error = null);

    /// <summary>
    /// Kutudaki yeni adı uygular. Ad geçersizse uyarıyı <paramref name="hint"/> ile gösterir ve false döner (kutu açık kalır);
    /// aksi hâlde true (kutu kapanır, yeniden adlandırma arka planda sürer). Başarılı olunca <paramref name="renamed"/> yeni
    /// yolla (UI iş parçacığında) çağrılır.
    /// </summary>
    public static bool Commit(WidgetWindow? window, string path, bool isDirectory, FileNames.EditName split, string edited,
        Action<string> hint, Action<string> renamed)
    {
        var oldName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        var directory = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path));
        if (directory is null) return true;
        var newName = split.Compose(edited);
        if (string.Equals(newName, oldName, StringComparison.Ordinal)) return true;
        if (FileNames.Validate(newName, directory) is { } error)
        {
            hint(error);
            return false;
        }
        if (FileNames.ExtensionChanged(oldName, newName, isDirectory) &&
            !Confirm.Ask(null, L.T("Uzantı değişsin mi?"),
                L.T("Dosya adı uzantısını değiştirirsen dosya kullanılamaz hâle gelebilir. Yine de değiştirilsin mi?"),
                L.T("Değiştir"), danger: false, cancelText: L.T("Vazgeç"), near: window?.CenterPoint))
            return true; // Gezgin gibi: ad eski hâline döner
        Run(window, path, Path.Combine(directory, newName), isDirectory, renamed);
        return true;
    }

    private static void Run(WidgetWindow? window, string path, string target, bool isDirectory, Action<string> renamed)
    {
        var owner = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        var near = window?.CenterPoint;
        var dispatcher = Application.Current.Dispatcher;
        Task.Run(() => Move(path, target, isDirectory)).ContinueWith(t =>
        {
            var result = t.IsCompletedSuccessfully ? t.Result : new MoveResult(Outcome.Failed, Error: t.Exception?.GetBaseException().Message);
            dispatcher.BeginInvoke(() => Handle(result, owner, near, path, target, isDirectory, renamed));
        }, TaskScheduler.Default);
    }

    private static void Handle(MoveResult result, IntPtr owner, NativeMethods.POINT? near, string path, string target, bool isDirectory,
        Action<string> renamed)
    {
        var name = TileItem.DisplayName(path);
        switch (result.Outcome)
        {
            case Outcome.Done:
                renamed(target);
                break;
            case Outcome.Exists when result.Suggested is { } suggested:
                var suggestedName = TileItem.DisplayName(suggested);
                if (Confirm.Ask(null, L.T("Bu adda bir öğe zaten var"),
                        L.F("Bu klasörde \"{0}\" adında bir öğe var. \"{1}\" olarak adlandırılsın mı?", TileItem.DisplayName(target), suggestedName),
                        L.T("Evet"), danger: false, cancelText: L.T("Hayır"), near: near))
                    RunForNewTarget(owner, near, path, suggested, isDirectory, renamed);
                break;
            case Outcome.Denied:
                // İzin gerekiyor (Ortak Masaüstü ya da korunan klasör): Gezgin'in yolu, Windows'un kendi onay penceresiyle.
                var dispatcher = Application.Current.Dispatcher;
                ShellFileOperations.RunSta(() => ShellFileOperations.RenameWithShell(path, Path.GetFileName(target), owner)).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        AppHost.NotePathRenamed(path, target, isDirectory);
                        AppHost.Snapshots.Find(Path.GetDirectoryName(path) ?? "")?.NoteRenamed(path, target);
                        dispatcher.BeginInvoke(() => renamed(target));
                    }
                    else if (t.Exception?.GetBaseException() is { } ex and not OperationCanceledException)
                        Notice.Show(L.F("\"{0}\" yeniden adlandırılamadı: {1}", name, ex.Message), NoticeKind.Warning);
                }, TaskScheduler.Default);
                break;
            case Outcome.Missing:
                Notice.Show(L.F("\"{0}\" yeniden adlandırılamadı: öğe artık orada değil.", name), NoticeKind.Warning);
                break;
            case Outcome.Failed:
                Notice.Show(L.F("\"{0}\" yeniden adlandırılamadı: {1}", name, result.Error ?? ""), NoticeKind.Warning);
                break;
        }
    }

    private static void RunForNewTarget(IntPtr owner, NativeMethods.POINT? near, string path, string target, bool isDirectory, Action<string> renamed)
    {
        var dispatcher = Application.Current.Dispatcher;
        Task.Run(() => Move(path, target, isDirectory)).ContinueWith(t =>
        {
            var result = t.IsCompletedSuccessfully ? t.Result : new MoveResult(Outcome.Failed, Error: t.Exception?.GetBaseException().Message);
            // Önerilen ad da bu arada alındıysa bir kez daha sorulur.
            dispatcher.BeginInvoke(() => Handle(result, owner, near, path, target, isDirectory, renamed));
        }, TaskScheduler.Default);
    }

    /// <summary>Diskteki iş (arka planda). Başarılıysa geçmişi ve anlık görüntüyü hemen günceller.</summary>
    private static MoveResult Move(string path, string target, bool isDirectory)
    {
        var directory = Path.GetDirectoryName(target)!;
        if (isDirectory ? !Directory.Exists(path) : !File.Exists(path)) return new MoveResult(Outcome.Missing);
        var oldName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        var newName = Path.GetFileName(target);
        if (!FileNames.IsCaseOnlyChange(oldName, newName) && (File.Exists(target) || Directory.Exists(target)))
        {
            var suggested = FileNames.UniqueName(newName, isDirectory, n => File.Exists(Path.Combine(directory, n)) || Directory.Exists(Path.Combine(directory, n)));
            return new MoveResult(Outcome.Exists, Path.Combine(directory, suggested));
        }
        try
        {
            if (isDirectory) Directory.Move(path, target);
            else File.Move(path, target);
        }
        catch (UnauthorizedAccessException)
        {
            return new MoveResult(Outcome.Denied);
        }
        catch (IOException ex) when ((ex.HResult & 0xFFFF) is 32 or 33)
        {
            return new MoveResult(Outcome.Failed, Error: L.T("başka bir programda açık. Kapatıp yeniden dene."));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return new MoveResult(Outcome.Failed, Error: ex.Message);
        }
        // İzleyici yeni adı görmeden: geri alınmış dosya yeniden taşınmasın, bölme eski adı bir an bile göstermesin.
        AppHost.NotePathRenamed(path, target, isDirectory);
        AppHost.Snapshots.Find(Path.GetDirectoryName(path) ?? "")?.NoteRenamed(path, target);
        return new MoveResult(Outcome.Done);
    }
}
