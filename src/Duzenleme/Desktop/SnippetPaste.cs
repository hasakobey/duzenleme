using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Duzenleme.Core;

namespace Duzenleme.Desktop;

/// <summary>
/// Halkanın "metin yapıştır" öğesi (Store 10.5: pano geçmişine ve bulut panosuna girmez, önceki pano geri konur):
/// <list type="number">
/// <item>önceki pano içeriği bellekte saklanır (yalnızca beyaz listedeki biçimler; bkz. <see cref="Classify"/>),</item>
/// <item>metin, izleme/geçmiş/bulut dışlama biçimleriyle panoya konur ve kalıcı yapılır (OleFlushClipboard),</item>
/// <item>pano sıra numarası alınır, Ctrl+V gönderilir,</item>
/// <item>~1 sn sonra sıra numarası değişmediyse (başka bir şey panoya yazmadıysa) önceki içerik, yine dışlama biçimleriyle
/// geri konur; önce pano boşsa boşaltılır. Önceki içerikte beyaz liste dışı biçim varsa geri konmaz, sonuç bildirilir.</item>
/// </list>
/// Hepsi kendi STA iş parçacığında (<see cref="ShellFileOperations.RunSta"/>). Metin ve pano içeriği asla günlüğe yazılmaz.
/// </summary>
internal static class SnippetPaste
{
    /// <summary>Yapıştırmadan sonra önceki içeriği geri koymadan önceki bekleme (hedef uygulama panoyu okusun).</summary>
    public static readonly TimeSpan RestoreDelay = TimeSpan.FromSeconds(1);

    public enum Outcome
    {
        /// <summary>Yapıştırıldı, önceki içerik geri kondu (ya da pano boştu ve boşaltıldı).</summary>
        Restored,

        /// <summary>Yapıştırıldı; bu arada pano başka biri tarafından değişti: geri konmadı (yenisinin üstüne yazılmaz).</summary>
        ClipboardChanged,

        /// <summary>Yapıştırıldı; önceki içerik geri konamayacak biçimler içeriyordu (ör. Office nesnesi): metin panoda kaldı.</summary>
        NotRestorable,

        /// <summary>Pano açılamadı ya da tuş gönderilemedi.</summary>
        Failed,
    }

    // Dışlama biçimleri (Windows pano geçmişi, bulut panosu ve pano izleyicileri).
    public const string ExcludeFromMonitoring = "ExcludeClipboardContentFromMonitorProcessing";
    public const string CanIncludeInHistory = "CanIncludeInClipboardHistory";
    public const string CanUploadToCloud = "CanUploadToCloudClipboard";
    public const string PreferredDropEffect = "Preferred DropEffect";

    /// <summary>Geri konabilen asıl biçimler.</summary>
    public static IReadOnlyList<string> Primary =>
        [DataFormats.UnicodeText, DataFormats.Text, DataFormats.Rtf, DataFormats.Html, DataFormats.FileDrop, DataFormats.Dib, DataFormats.Bitmap];

    /// <summary>
    /// Asıl biçimlerin Windows'un ya da kaynak uygulamanın eklediği yan bilgileri: içerik taşımaz, geri konmaz (asıl biçimden
    /// yeniden üretilir ya da önemsizdir). Bunlar dışında bir biçim varsa önceki içerik geri konmaz.
    /// </summary>
    public static IReadOnlyList<string> Companions =>
    [
        DataFormats.OemText, DataFormats.Locale, "Format17", // CF_DIBV5: DIB'den üretilir
        "FileName", "FileNameW", "Shell IDList Array", "Shell Object Offsets", "DataObjectAttributes",
        "DataObjectAttributesRequiringElevation", "AsyncFlag", PreferredDropEffect,
        "Chromium internal source URL", "msSourceUrl", "text/x-moz-url-priv", "text/_moz_htmlcontext", "text/_moz_htmlinfo",
        ExcludeFromMonitoring, CanIncludeInHistory, CanUploadToCloud, "Clipboard Viewer Ignore",
    ];

    /// <summary>
    /// Önceki pano içeriği geri konabilir mi ve hangi biçimlerle (saf; testlenir). Yalnızca asıl ve yan biçimler varsa
    /// geri konabilir; geri konanlar: UnicodeText (yoksa Text), RTF, HTML, FileDrop (+ Preferred DropEffect: kes/kopyala),
    /// DIB (yoksa Bitmap). Boş pano geri konabilir (boşaltılır).
    /// </summary>
    public static (bool Restorable, IReadOnlyList<string> Formats) Classify(IEnumerable<string> formats)
    {
        var set = new HashSet<string>(formats, StringComparer.OrdinalIgnoreCase);
        var known = new HashSet<string>(Primary.Concat(Companions), StringComparer.OrdinalIgnoreCase);
        if (set.Any(f => !known.Contains(f))) return (false, []);

        var restore = new List<string>();
        if (set.Contains(DataFormats.UnicodeText)) restore.Add(DataFormats.UnicodeText);
        else if (set.Contains(DataFormats.Text)) restore.Add(DataFormats.Text);
        if (set.Contains(DataFormats.Rtf)) restore.Add(DataFormats.Rtf);
        if (set.Contains(DataFormats.Html)) restore.Add(DataFormats.Html);
        if (set.Contains(DataFormats.FileDrop))
        {
            restore.Add(DataFormats.FileDrop);
            if (set.Contains(PreferredDropEffect)) restore.Add(PreferredDropEffect);
        }
        if (set.Contains(DataFormats.Dib)) restore.Add(DataFormats.Dib);
        else if (set.Contains(DataFormats.Bitmap)) restore.Add(DataFormats.Bitmap);
        // Yalnızca yan bilgi (asıl içerik yok): geri konacak bir şey yok, pano boş sayılır.
        return (true, restore);
    }

    /// <summary>
    /// Metni ön plandaki pencereye yapıştırır (çağıran önce hedefi öne getirir, kısayol tuşlarının bırakılmasını bekler ve
    /// yönetici penceresini dışlar). Metin <see cref="RingActions.ValidText"/> değilse hiçbir şey yapmaz.
    /// </summary>
    public static async Task<Outcome> PasteAsync(string text)
    {
        if (!RingActions.ValidText(text)) return Outcome.Failed;
        var outcome = Outcome.Failed;
        try { await ShellFileOperations.RunSta(() => outcome = Run(text)); }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException) { return Outcome.Failed; }
        return outcome;
    }

    private static Outcome Run(string text)
    {
        // 1. Önceki içerik (yalnızca bellekte).
        var (restorable, saved) = Save();

        // 2. Metin + dışlama biçimleri; copy: true → OleFlushClipboard: iş parçacığı bitse de içerik panoda kalır. Otomatik
        // dönüştürme yok (WPF'in .NET nesne biçimi eklenmesin); Text/OEMText'i Windows UnicodeText'ten üretir.
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text, autoConvert: false);
        AddExclusions(data);
        Clipboard.SetDataObject(data, copy: true);
        var sequence = GetClipboardSequenceNumber();

        // 3. Ctrl+V (hâlâ basılı değiştiriciler önce bırakılır).
        var sent = KeySender.Send(["Ctrl+V"], KeySender.HeldModifiers());

        // 4. Hedef okusun; sonra yalnızca pano bu arada değişmediyse geri koy.
        Thread.Sleep(RestoreDelay);
        if (GetClipboardSequenceNumber() != sequence) return sent ? Outcome.ClipboardChanged : Outcome.Failed;
        if (!restorable) return sent ? Outcome.NotRestorable : Outcome.Failed;
        if (saved.Count == 0) Clipboard.Clear();
        else
        {
            var back = new DataObject();
            foreach (var (format, value) in saved) back.SetData(format, value, autoConvert: false);
            AddExclusions(back);
            Clipboard.SetDataObject(back, copy: true);
        }
        return sent ? Outcome.Restored : Outcome.Failed;
    }

    /// <summary>Panodaki içeriğin geri konacak kopyası; okunamayan asıl biçim varsa geri konamaz sayılır.</summary>
    private static (bool Restorable, List<(string Format, object Value)> Saved) Save()
    {
        var saved = new List<(string, object)>();
        IDataObject? current;
        try { current = Clipboard.GetDataObject(); }
        catch (ExternalException) { return (false, saved); }
        if (current is null) return (true, saved);

        string[] formats;
        try { formats = current.GetFormats(autoConvert: false); }
        catch (ExternalException) { return (false, saved); }
        var (restorable, restore) = Classify(formats);
        if (!restorable) return (false, saved);
        foreach (var format in restore)
        {
            object? value;
            try { value = current.GetData(format, autoConvert: false); }
            catch (Exception ex) when (ex is ExternalException or OutOfMemoryException) { return (false, saved); }
            // Akışlar kopyalanır: panonun eski nesnesi değişince geçersiz kalmasın.
            if (value is Stream stream) value = Copy(stream);
            if (value is null) return (false, saved);
            saved.Add((format, value));
        }
        return (true, saved);
    }

    private static MemoryStream Copy(Stream stream)
    {
        var copy = new MemoryStream();
        if (stream.CanSeek) stream.Position = 0;
        stream.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    /// <summary>Pano geçmişi (Win+V), bulut panosu ve pano izleyicileri bu içeriği almaz (DWORD 0).</summary>
    private static void AddExclusions(DataObject data)
    {
        data.SetData(ExcludeFromMonitoring, new MemoryStream(BitConverter.GetBytes(0)), autoConvert: false);
        data.SetData(CanIncludeInHistory, new MemoryStream(BitConverter.GetBytes(0)), autoConvert: false);
        data.SetData(CanUploadToCloud, new MemoryStream(BitConverter.GetBytes(0)), autoConvert: false);
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
