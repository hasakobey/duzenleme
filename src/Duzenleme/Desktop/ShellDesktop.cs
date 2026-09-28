using System.Reflection;
using System.Runtime.InteropServices;

namespace Duzenleme.Desktop;

/// <summary>
/// "Masaüstünü göster" (Win+D ile aynı): Shell.Application.ToggleDesktop. Tuş basımı taklit edilmez. Explorer'a süreçler arası
/// bir çağrıdır; Explorer meşgulken arayüz donmasın diye kendi STA iş parçacığında yapılır.
/// </summary>
public static class ShellDesktop
{
    public static void ToggleInBackground()
    {
        var thread = new Thread(() =>
        {
            object? shell = null;
            try
            {
                if (Type.GetTypeFromProgID("Shell.Application") is not { } type) return;
                shell = Activator.CreateInstance(type);
                type.InvokeMember("ToggleDesktop", BindingFlags.InvokeMethod, null, shell, null);
            }
            catch (Exception ex) when (ex is COMException or TargetInvocationException or InvalidOperationException or MissingMethodException)
            {
                DebugLog.Write($"masaüstünü göster yapılamadı: {ex.Message}");
            }
            finally
            {
                if (shell is not null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
        })
        { IsBackground = true, Name = $"{Core.AppInfo.Name} masaüstünü göster" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
}
