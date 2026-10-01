namespace Duzenleme.Core;

/// <summary>
/// "Numara tuşlarıyla aç" (<see cref="WidgetConfig.QuickKeys"/>): bölmede ya da kısayol kutusunda 1–9 (üst sıra ya da sayısal
/// tuş takımı) görünen n'inci öğeyi açar. Seçenek varsayılan kapalıdır: rakamla başlayan adlarda ("2024 rapor.pdf") harfle
/// arama rakamla da çalışsın. Rozet yoktur; ipucu menüdedir. Saf; testlenir.
/// </summary>
public static class QuickKeys
{
    /// <summary>Kaç öğe tuşla açılabilir (1–9).</summary>
    public const int Count = 9;

    private const int VK_1 = 0x31, VK_9 = 0x39, VK_NUMPAD1 = 0x61, VK_NUMPAD9 = 0x69;

    /// <summary>Win32 sanal tuş kodundan öğe sırası (0 = ilk öğe); 1–9 değilse (0, harf, işaret) -1.</summary>
    public static int IndexOf(int virtualKey) => virtualKey switch
    {
        >= VK_1 and <= VK_9 => virtualKey - VK_1,
        >= VK_NUMPAD1 and <= VK_NUMPAD9 => virtualKey - VK_NUMPAD1,
        _ => -1,
    };
}
