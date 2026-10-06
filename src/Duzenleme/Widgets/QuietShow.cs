using System.Runtime.InteropServices;
using System.Windows;

namespace Duzenleme.Widgets;

/// <summary>
/// Odak çalmadan pencere açma (widget'lar, göz atma çubuğu). Bu pencereler WS_EX_NOACTIVATE'tir, ama WPF bu stili
/// ancak pencere oluştuktan sonra alır (OnSourceInitialized); oluşturma sırasında pencere ölçeği farklı bir monitöre geçerse
/// WPF'in DPI işleyicisi SWP_NOACTIVATE olmadan SetWindowPos çağırır ve pencere etkinleşir (açılışta widget'lar sırayla
/// etkinleşip kullanıcının yazdığı pencereden odağı alıyordu). Burada pencere yalnızca bu iş parçacığına kurulan bir CBT
/// kancasıyla oluşturulur: yeni üst düzey pencereler WS_EX_NOACTIVATE ile doğar ve oluşurken etkinleşme/odak istekleri
/// geri çevrilir. Kanca yalnızca gösterme süresince durur; sonra tıklamayla etkinleşme (WM_MOUSEACTIVATE) ve
/// <see cref="WidgetWindow.ActivateForInput"/> eskisi gibi çalışır.
/// </summary>
internal static class QuietShow
{
    private const int WH_CBT = 5;
    private const int HCBT_ACTIVATE = 5;
    private const int HCBT_CREATEWND = 3;
    private const int HCBT_SETFOCUS = 9;
    private const int WS_CHILD = 0x40000000;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct CBT_CREATEWND
    {
        public IntPtr lpcs;
        public IntPtr hwndInsertAfter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CREATESTRUCT
    {
        public IntPtr lpCreateParams;
        public IntPtr hInstance;
        public IntPtr hMenu;
        public IntPtr hwndParent;
        public int cy, cx, y, x;
        public int style;
        public IntPtr lpszName;
        public IntPtr lpszClass;
        public int dwExStyle;
    }

    private static readonly int StyleOffset = Marshal.OffsetOf<CREATESTRUCT>(nameof(CREATESTRUCT.style)).ToInt32();
    private static readonly int ExStyleOffset = Marshal.OffsetOf<CREATESTRUCT>(nameof(CREATESTRUCT.dwExStyle)).ToInt32();

    /// <summary>Pencereyi etkinleştirmeden gösterir (henüz oluşmadıysa oluşturma da korunur). Zaten görünürse bir şey yapmaz.</summary>
    public static void Show(Window window)
    {
        if (window.IsVisible) return;
        Run(window.Show);
    }

    /// <summary>
    /// Var olan pencereyi taşır (<paramref name="move"/>) ama taşınırken etkinleşmez: ölçeği farklı bir monitöre geçen pencereye
    /// gelen WM_DPICHANGED'i WPF SWP_NOACTIVATE olmadan SetWindowPos ile işler ve WS_EX_NOACTIVATE'e rağmen pencere etkinleşir
    /// (karşılamadan sonra eklenen saat ve tarih öne gelen pencere olmadığında etkinleşip klavye odağını alıyordu). Taşıma bu
    /// iş parçacığında eşzamanlı olduğundan kanca yalnızca onun süresince durur.
    /// </summary>
    public static void Move(IntPtr hwnd, Action move)
    {
        if (hwnd == IntPtr.Zero) move();
        else Run(move, hwnd);
    }

    /// <summary>
    /// <paramref name="create"/> süresince bu iş parçacığında oluşan üst düzey pencereler (ve <paramref name="guard"/>)
    /// etkinleşmez ve odak almaz. Oluşturulan pencerelerin sayısını döner (tanı için).
    /// </summary>
    public static int Run(Action create, IntPtr guard = default)
    {
        var created = new HashSet<IntPtr>();
        if (guard != IntPtr.Zero) created.Add(guard);
        NativeMethods.HookProc proc = (code, wParam, lParam) =>
        {
            try
            {
                switch (code)
                {
                    case HCBT_CREATEWND:
                        var cbt = Marshal.PtrToStructure<CBT_CREATEWND>(lParam);
                        if (cbt.lpcs != IntPtr.Zero && (Marshal.ReadInt32(cbt.lpcs, StyleOffset) & WS_CHILD) == 0)
                        {
                            Marshal.WriteInt32(cbt.lpcs, ExStyleOffset, Marshal.ReadInt32(cbt.lpcs, ExStyleOffset) | WS_EX_NOACTIVATE);
                            created.Add(wParam);
                        }
                        break;
                    // Oluşan pencere etkinleşmek ya da klavye odağını almak isterse geri çevrilir (sıfırdan farklı = engelle).
                    case HCBT_ACTIVATE or HCBT_SETFOCUS when created.Contains(wParam):
                        DebugLog.Write(wParam == guard
                            ? $"odak isteği engellendi (pencere taşınırken): kod={code}"
                            : $"odak isteği engellendi (pencere oluşurken): kod={code}");
                        return new IntPtr(1);
                }
            }
            catch (ArgumentException)
            {
                // Kanca içinden hata fırlatılmaz (yerel çerçevelerden geçemez): pencere yine oluşur, en fazla korumasız.
            }
            return NativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        };
        var hook = NativeMethods.SetWindowsHookEx(WH_CBT, proc, IntPtr.Zero, NativeMethods.GetCurrentThreadId());
        try
        {
            create();
        }
        finally
        {
            if (hook != IntPtr.Zero) NativeMethods.UnhookWindowsHookEx(hook);
            GC.KeepAlive(proc);
        }
        return created.Count - (guard != IntPtr.Zero ? 1 : 0);
    }
}
