using System.Runtime.InteropServices;

namespace Duzenleme.Core;

/// <summary>
/// Acelesi olmayan disk işleri (açılıştaki ilk masaüstü taraması, klasör simgesi onarımı, günlük yedek) için düşük
/// öncelikli iş parçacığı. Windows'un "arka plan kipi" (THREAD_MODE_BACKGROUND_BEGIN) disk ve bellek önceliğini de
/// düşürür: oturum açılırken Gezgin ve kullanıcının açtığı programlarla yarışmaz.
/// </summary>
public static class BackgroundIo
{
    private const int THREAD_MODE_BACKGROUND_BEGIN = 0x00010000;
    private const int THREAD_MODE_BACKGROUND_END = 0x00020000;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetThreadPriority(IntPtr thread, int priority);

    /// <summary>İşi <paramref name="delay"/> sonra, kendi düşük öncelikli iş parçacığında çalıştırır. Hatalar yutulup günlüğe yazılır.</summary>
    public static Task Run(string name, Action work, TimeSpan delay = default, Action<Exception>? onError = null)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                if (delay > TimeSpan.Zero) Thread.Sleep(delay);
                var background = SetThreadPriority(GetCurrentThread(), THREAD_MODE_BACKGROUND_BEGIN);
                try { work(); }
                finally { if (background) SetThreadPriority(GetCurrentThread(), THREAD_MODE_BACKGROUND_END); }
            }
            catch (Exception ex)
            {
                try { onError?.Invoke(ex); } catch { }
            }
            finally { done.TrySetResult(); }
        })
        { IsBackground = true, Name = name, Priority = ThreadPriority.BelowNormal };
        thread.Start();
        return done.Task;
    }
}
