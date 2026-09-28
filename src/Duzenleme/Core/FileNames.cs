using System.IO;

namespace Duzenleme.Core;

/// <summary>
/// Bölmede yerinde yeniden adlandırma (F2) için Gezgin'e benzer ad kuralları. Diske dokunmaz; testlenir.
/// <para>Kısayol, internet kısayolu, program ve ClickOnce uzantısı (<c>.lnk .url .exe .appref-ms</c>) bölmede gösterilmez
/// (<see cref="HiddenExtensions"/>): düzenleme kutusunda da görünmez ve yeni ada geri eklenir; yoksa "Chrome" yazan
/// kullanıcı kısayolu uzantısız, bozuk bir dosyaya çevirirdi.</para>
/// </summary>
public static class FileNames
{
    /// <summary>Bölmede ve düzenleme kutusunda gösterilmeyen uzantılar (TileItem.DisplayName ile aynı).</summary>
    public static readonly IReadOnlyList<string> HiddenExtensions = [".lnk", ".url", ".exe", ".appref-ms"];

    /// <summary>Dosya adında olamayan karakterler (denetim karakterleri ayrıca).</summary>
    public const string InvalidCharacters = "\\/:*?\"<>|";

    /// <summary>Tam yolun sınırı: bu uzunluğa ulaşan yolu Gezgin ve çoğu program açamaz.</summary>
    public const int MaxPath = 260;

    /// <summary>Tek bir ad parçasının sınırı (NTFS).</summary>
    public const int MaxName = 255;

    private static readonly HashSet<string> ReservedNames = BuildReserved();

    private static HashSet<string> BuildReserved()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" };
        // COM0-9, LPT0-9 ve üst simge ¹²³ (Windows bunları da aygıt sayar).
        foreach (var digit in "0123456789¹²³")
        {
            names.Add("COM" + digit);
            names.Add("LPT" + digit);
        }
        return names;
    }

    /// <summary>Düzenleme kutusuna konacak ad, gizli uzantı ve başta seçilecek karakter sayısı.</summary>
    /// <param name="Editable">Kutuda görünen ad (gizli uzantısız).</param>
    /// <param name="HiddenExtension">Kutuda görünmeyen, kaydederken geri eklenen uzantı (yoksa boş).</param>
    /// <param name="SelectLength">Başta seçilen kısım: uzantısı görünen dosyada noktadan öncesi, diğerlerinde hepsi.</param>
    public readonly record struct EditName(string Editable, string HiddenExtension, int SelectLength)
    {
        /// <summary>Kutudaki metinden diskteki yeni ad (gizli uzantı geri eklenir).</summary>
        public string Compose(string edited) => Normalize(edited) + HiddenExtension;
    }

    public static EditName SplitForEditing(string path, bool isDirectory)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        if (isDirectory) return new EditName(name, "", name.Length);
        var ext = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        if (ext.Length > 0 && stem.Length > 0 && HiddenExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return new EditName(stem, ext, stem.Length);
        // ".gitignore" gibi yalnızca uzantıdan oluşan adlarda hepsi seçilir.
        return new EditName(name, "", ext.Length > 0 && stem.Length > 0 ? stem.Length : name.Length);
    }

    /// <summary>
    /// Baştaki ve sondaki boşlukları, sondaki noktaları atar: Windows bunları sessizce siler ("a. " → "a"); kullanıcının
    /// gördüğü ad diskteki adla aynı olsun.
    /// </summary>
    public static string Normalize(string name) => name.Trim().TrimEnd('.', ' ');

    /// <summary>Yazılamayan karakter mi? (Kutuya yazılırken engellenir.)</summary>
    public static bool IsInvalidChar(char c) => c < 32 || InvalidCharacters.Contains(c);

    /// <summary>Metindeki yazılamayan karakterleri atar (yapıştırılan metin).</summary>
    public static string StripInvalid(string text) => new(text.Where(c => !IsInvalidChar(c)).ToArray());

    /// <summary>
    /// Yeni ad kullanılabilir mi? Kullanılamıyorsa arayüz dilinde kısa bir açıklama, kullanılabiliyorsa null.
    /// </summary>
    /// <param name="name">Diskteki yeni ad (gizli uzantısı eklenmiş, <see cref="Normalize"/>'dan geçmiş).</param>
    /// <param name="directory">Öğenin bulunduğu klasör (yol uzunluğu için); bilinmiyorsa null.</param>
    public static string? Validate(string name, string? directory = null)
    {
        if (name.Length == 0 || name is "." or "..") return L.T("Bir ad yazmalısın.");
        if (name.Any(IsInvalidChar)) return InvalidCharactersHint;
        if (IsReserved(name)) return L.F("\"{0}\" Windows'a ayrılmış bir ad; başka bir ad seç.", name);
        if (name.Length > MaxName || (directory is not null && Path.Combine(directory, name).Length >= MaxPath))
            return L.T("Ad çok uzun: Windows bu kadar uzun bir yolu açamaz.");
        return null;
    }

    /// <summary>Yazılamayan karakter uyarısı (Gezgin'deki gibi, karakterleri de gösterir).</summary>
    public static string InvalidCharactersHint => L.F("Ad şu karakterleri içeremez: {0}", "\\ / : * ? \" < > |");

    /// <summary>Aygıt adı mı (CON, NUL, COM1…; uzantılı ya da sonunda boşluk olsa da)? Windows 10 bunları dosya adı olarak kabul etmez.</summary>
    public static bool IsReserved(string name)
    {
        var stem = name.Split('.')[0].TrimEnd(' ');
        return ReservedNames.Contains(stem);
    }

    /// <summary>Dosyanın (görünen) uzantısı değişiyor mu? Klasörde uzantı yoktur.</summary>
    public static bool ExtensionChanged(string oldName, string newName, bool isDirectory) =>
        !isDirectory && !string.Equals(Path.GetExtension(oldName), Path.GetExtension(newName), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Aynı klasörde çakışmayan ad: Windows gibi "(2)"den başlar ("Rapor (2).pdf", "Yeni klasör (2)"). Klasör adındaki nokta
    /// uzantı sayılmaz ("v1.2" → "v1.2 (2)").
    /// </summary>
    /// <param name="exists">Ad o klasörde var mı? (Disk ya da anlık görüntü; çağıran verir.)</param>
    public static string UniqueName(string name, bool isDirectory, Func<string, bool> exists, int startAt = 2)
    {
        if (!exists(name)) return name;
        var ext = isDirectory ? "" : Path.GetExtension(name);
        if (ext.Length == name.Length) ext = ""; // ".env" gibi yalnızca uzantıdan oluşan ad
        var stem = name[..^ext.Length];
        for (var i = Math.Max(1, startAt); ; i++)
        {
            var candidate = $"{stem} ({i}){ext}";
            if (!exists(candidate)) return candidate;
        }
    }

    /// <summary>Yalnızca büyük/küçük harf mi değişiyor? (Windows'ta aynı ad sayılır; çakışma değildir.)</summary>
    public static bool IsCaseOnlyChange(string oldName, string newName) =>
        !string.Equals(oldName, newName, StringComparison.Ordinal) && string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase);
}
