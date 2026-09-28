using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Duzenleme.Desktop;

/// <summary>
/// Bir Windows masaüstü simgesi. Ad ve açıklama tabloda Türkçe (L.N ile işaretli) saklanır, okunurken arayüz diline
/// çevrilir: bölme kutucuğu, "Gizlenen öğeler" ve Ayarlar'daki liste aynı adı gösterir.
/// </summary>
public sealed record SystemIcon(string Clsid, string NameKey, string DescriptionKey, bool ShownByDefault)
{
    public string Name => L.Dyn(NameKey);

    public string Description => L.Dyn(DescriptionKey);
}

/// <summary>
/// Windows'un varsayılan masaüstü simgeleri (Bu Bilgisayar, Geri Dönüşüm Kutusu…).
/// "Masaüstü simgesi ayarları" penceresiyle aynı kayıt defteri anahtarını kullanır.
/// </summary>
public static class DesktopSystemIcons
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_F5 = 0x74;
    private const int SHCNE_ASSOCCHANGED = 0x08000000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    public static readonly SystemIcon[] All =
    [
        new("{20D04FE0-3AEA-1069-A2D8-08002B30309D}", L.N("Bu Bilgisayar"), L.N("Sürücüler ve cihazlar"), false),
        new("{645FF040-5081-101B-9F08-00AA002F954E}", L.N("Geri Dönüşüm Kutusu"), L.N("Silinen dosyalar"), true),
        new("{59031a47-3f72-44a7-89c5-5595fe6b30ee}", L.N("Kullanıcı dosyaları"), L.N("Belgeler, Resimler, İndirilenler…"), false),
        new("{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", L.N("Ağ"), L.N("Ağdaki bilgisayarlar ve cihazlar"), false),
        new("{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}", L.N("Denetim Masası"), L.N("Klasik Windows ayarları"), false),
    ];

    /// <summary>
    /// Simgeler buradan açılıp kapatılabilir mi? Store (MSIX) sürümünde HKCU yazmaları pakete özel bir kayıt kopyasına
    /// gider: Gezgin değişikliği hiç görmez, uygulama ise sonra kendi kopyasını okur. Paketliyken bu anahtara hiç
    /// yazılmaz; okuma birleşik görünümden yapıldığı için o zaman gerçek durum okunur (Masaüstü sayfası ve bölmeler
    /// Windows masaüstüyle uyuşur). Değişiklik orada Windows'un "Masaüstü simgesi ayarları"ndan (<see cref="SettingsUri"/>).
    /// </summary>
    public static bool CanChange => !PackageInfo.IsPackaged;

    /// <summary>
    /// Windows'un Temalar sayfası: "Masaüstü simgesi ayarları" bağlantısı burada. Ayarlar uygulaması paket dışında çalışır;
    /// desk.cpl'yi bu süreçten açmak işe yaramazdı (alt süreç paket bağlamında kalır, yazmaları da sanallaşır).
    /// </summary>
    public const string SettingsUri = "ms-settings:themes";

    public static bool IsShown(SystemIcon icon)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key);
        return key?.GetValue(icon.Clsid) is int hidden ? hidden == 0 : icon.ShownByDefault;
    }

    public static void SetShown(SystemIcon icon, bool shown)
    {
        if (!CanChange) return;
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(icon.Clsid, shown ? 0 : 1, RegistryValueKind.DWord);
        Refresh();
    }

    /// <summary>Masaüstünü yeniler (F5 gibi) ki değişiklik hemen görünsün.</summary>
    public static void Refresh()
    {
        SHChangeNotify(SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero);
        var lv = DesktopIcons.FindListView();
        if (lv != IntPtr.Zero) PostMessage(lv, WM_KEYDOWN, VK_F5, IntPtr.Zero);
    }

    /// <summary>Kabuk yolu: Explorer'da açmak ve simgesini almak için.</summary>
    public static string ShellPath(SystemIcon icon) => "shell:::" + icon.Clsid;
}
