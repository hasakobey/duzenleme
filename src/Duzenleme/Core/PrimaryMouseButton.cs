namespace Duzenleme.Core;

/// <summary>
/// Birincil fare düğmesi. Solak ayarında ("Birincil ve ikincil düğmeleri değiştir") birincil düğme fiziksel SAĞ düğmedir.
/// Pencere iletileri (WM_LBUTTONDOWN) ve alçak düzey kancalar bu değişimi uygular; ham giriş (Raw Input,
/// RAWMOUSE.usButtonFlags) ve GetAsyncKeyState ise fiziksel düğmeyi bildirir: onlarla çalışan kod değişimi kendisi hesaba
/// katmalıdır (Windows ayarı: GetSystemMetrics(SM_SWAPBUTTON)).
/// </summary>
public static class PrimaryMouseButton
{
    /// <summary>GetSystemMetrics dizini: düğmeler değiştirildiyse sıfırdan farklı.</summary>
    public const int SM_SWAPBUTTON = 23;

    private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
    private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
    private const int VK_LBUTTON = 0x01;
    private const int VK_RBUTTON = 0x02;

    /// <summary>Ham girişte birincil düğmeye basış bayrağı.</summary>
    public static ushort RawInputDownFlag(bool swapped) => swapped ? RI_MOUSE_RIGHT_BUTTON_DOWN : RI_MOUSE_LEFT_BUTTON_DOWN;

    /// <summary>GetAsyncKeyState için birincil düğmenin sanal tuş kodu.</summary>
    public static int VirtualKey(bool swapped) => swapped ? VK_RBUTTON : VK_LBUTTON;
}
