using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Duzenleme.Core;

namespace Duzenleme.Desktop;

/// <summary>
/// Halkanın "metin yapıştır" öğesi (Store 10.5: pano geçmişine ve bulut panosuna girmez, önceki pano geri konur):
/// <list type="number">
/// <item>hedef hâlâ önde ve girdiyi alabiliyor mu bakılır (değilse panoya dokunulmaz),</item>
/// <item>önceki pano içeriği bellekte saklanır (asıl biçimler; bkz. <see cref="Classify"/>); okunamazsa yapıştırılmaz,</item>
/// <item>metin, izleme/geçmiş/bulut dışlama biçimleriyle panoya konur ve kalıcı yapılır (OleFlushClipboard),</item>
/// <item>pano sıra numarası alınır, hedef yeniden denetlenip Ctrl+V gönderilir (değilse önceki içerik hemen geri konur),</item>
/// <item>~1 sn ileti işlenerek beklenir (panonun sahibi biziz; başka uygulama panoyu boşaltırken takılmasın), sonra sıra
/// numarası değişmediyse (başka bir şey panoya yazmadıysa) önceki içeriğin asıl biçimleri, yine dışlama biçimleriyle geri
/// konur; geri konacak asıl biçim yoksa pano boşaltılır (metin panoda bırakılmaz).</item>
/// </list>
/// Hepsi kendi STA iş parçacığında (<see cref="ShellFileOperations.RunSta"/>), <see cref="KeySender"/>'ın kapısıyla tek tek
/// ve en çok <see cref="Timeout"/> beklenerek. Metin ve pano içeriği asla günlüğe yazılmaz (yalnızca hata türü).
/// </summary>
internal static class SnippetPaste
{
    /// <summary>Yapıştırmadan sonra önceki içeriği geri koymadan önceki bekleme (hedef uygulama panoyu okusun).</summary>
    public static readonly TimeSpan RestoreDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// <see cref="PasteAsync"/>'in en uzun beklemesi (askıdaki bir uygulamanın geciktirilmiş biçimi panoyu kilitleyebilir).
    /// Süre dolarsa iş parçacığı Ctrl+V göndermez; kapı iş gerçekten bitince bırakılır.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    public enum Outcome
    {
        /// <summary>Yapıştırıldı, önceki içerik geri kondu (ya da pano boştu ve boşaltıldı).</summary>
        Restored,

        /// <summary>Yapıştırıldı; önceki içeriğin asıl biçimleri geri kondu, geri konamayanlar (ör. Office nesnesi) düştü.</summary>
        PartiallyRestored,

        /// <summary>Yapıştırıldı; önceki içerikte geri konabilen biçim yoktu: pano boşaltıldı (metin panoda kalmadı).</summary>
        NotRestorable,

        /// <summary>Yapıştırıldı; bu arada pano başka biri tarafından değişti: geri konmadı (yenisinin üstüne yazılmaz).</summary>
        ClipboardChanged,

        /// <summary>Yapıştırıldı; önceki içerik geri konamadı (pano meşguldü): metin panoda kalmış olabilir.</summary>
        PastedNotRestored,

        /// <summary>Başka bir tuş/metin gönderimi sürüyor: hiçbir şey yapılmadı.</summary>
        Busy,

        /// <summary>Hedef yok, masaüstü ya da bu uygulama: yapıştırılmadı, pano eski hâlinde.</summary>
        NoTarget,

        /// <summary>Öndeki pencere hedef değil: yapıştırılmadı, pano eski hâlinde.</summary>
        NotInFront,

        /// <summary>Hedef yönetici olarak (daha yüksek düzeyde) çalışıyor ya da bilinmiyor: yapıştırılmadı.</summary>
        Elevated,

        /// <summary>Pano açılamadı, süre doldu ya da tuş gönderilemedi: yapıştırılmadı.</summary>
        Failed,
    }

    // Dışlama biçimleri (Windows pano geçmişi, bulut panosu ve pano izleyicileri).
    public const string ExcludeFromMonitoring = "ExcludeClipboardContentFromMonitorProcessing";
    public const string CanIncludeInHistory = "CanIncludeInClipboardHistory";
    public const string CanUploadToCloud = "CanUploadToCloudClipboard";
    public const string PreferredDropEffect = "Preferred DropEffect";
    public const string Png = "PNG";

    /// <summary>Geri konabilen asıl biçimler (içeriğin kendisi).</summary>
    public static IReadOnlyList<string> Primary =>
        [DataFormats.UnicodeText, DataFormats.Text, DataFormats.Rtf, DataFormats.Html, DataFormats.FileDrop, DataFormats.Dib,
            DataFormats.Bitmap, Png];

    /// <summary>
    /// Asıl biçimlerin Windows'un ya da kaynak uygulamanın eklediği yan bilgileri: geri konmaz (asıl biçimden yeniden
    /// üretilir ya da önemsizdir) ve düşmeleri kayıp sayılmaz. Bunlar dışındaki biçimler (ör. Excel hücreleri, Office
    /// nesneleri) geri konamaz: <see cref="Outcome.PartiallyRestored"/>.
    /// </summary>
    public static IReadOnlyList<string> Companions =>
    [
        DataFormats.OemText, DataFormats.Locale, "Format17", // CF_DIBV5: DIB'den üretilir
        "FileName", "FileNameW", "Shell IDList Array", "Shell Object Offsets", "DataObjectAttributes",
        "DataObjectAttributesRequiringElevation", "AsyncFlag", PreferredDropEffect, "Performed DropEffect",
        "Logical Performed DropEffect", "Paste Succeeded", "DragImageBits", "DragContext", "DropDescription",
        "UniformResourceLocator", "UniformResourceLocatorW", "Ole Private Data",
        "Chromium internal source URL", "Chromium internal source RFH token", "Chromium Web Custom MIME Data Format",
        "msSourceUrl", "text/x-moz-url", "text/x-moz-url-priv", "text/_moz_htmlcontext", "text/_moz_htmlinfo",
        "VisualStudioEditorOperationsLineCutCopyClipboardTag", "MSDEVColumnSelect", "MSDEVLineSelect",
        ExcludeFromMonitoring, CanIncludeInHistory, CanUploadToCloud, "Clipboard Viewer Ignore",
    ];

    /// <summary>Önceki içeriğin geri konma planı: geri konacak biçimler (panodaki sırasıyla) ve bir şeyin düşüp düşmediği.</summary>
    public readonly record struct RestorePlan(IReadOnlyList<string> Formats, bool Lossy);

    /// <summary>
    /// Önceki pano içeriğinden ne geri konur (saf; testlenir): panoda bulunan her asıl biçim, panodaki sırasıyla; UnicodeText
    /// varsa Text, DIB varsa Bitmap (Windows üretir) alınmaz; FileDrop'la birlikte Preferred DropEffect (kes/kopyala) de.
    /// Asıl ve yan biçim dışında bir biçim varsa Lossy. Boş pano ya da yalnızca yan bilgi: hiçbir şey (pano boşaltılır).
    /// </summary>
    public static RestorePlan Classify(IEnumerable<string> formats)
    {
        var list = formats.Where(f => !string.IsNullOrEmpty(f)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var set = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        var primary = new HashSet<string>(Primary, StringComparer.OrdinalIgnoreCase);
        var companions = new HashSet<string>(Companions, StringComparer.OrdinalIgnoreCase);

        var restore = new List<string>();
        foreach (var format in list)
        {
            if (!primary.Contains(format)) continue;
            if (Is(format, DataFormats.Text) && set.Contains(DataFormats.UnicodeText)) continue;
            if (Is(format, DataFormats.Bitmap) && set.Contains(DataFormats.Dib)) continue;
            restore.Add(format);
            if (Is(format, DataFormats.FileDrop) && set.Contains(PreferredDropEffect)) restore.Add(PreferredDropEffect);
        }
        var lossy = list.Any(f => !primary.Contains(f) && !companions.Contains(f));
        return new RestorePlan(restore, lossy);
    }

    private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Yapıştırılacak metnin pano içeriği: metin ve üç dışlama biçimi (saf; testlenir).</summary>
    public static List<(string Format, object Value)> SnippetPayload(string text) => [(DataFormats.UnicodeText, text), .. Exclusions()];

    /// <summary>Geri konan önceki içerik: saklanan biçimler ve üç dışlama biçimi (saf; testlenir).</summary>
    public static List<(string Format, object Value)> RestorePayload(IEnumerable<(string Format, object Value)> saved) =>
        [.. saved, .. Exclusions()];

    /// <summary>Pano geçmişi (Win+V), bulut panosu ve pano izleyicileri bu içeriği almaz (her biri DWORD 0, her seferinde yeni akış).</summary>
    public static IEnumerable<(string Format, object Value)> Exclusions() =>
    [
        (ExcludeFromMonitoring, new MemoryStream(BitConverter.GetBytes(0))),
        (CanIncludeInHistory, new MemoryStream(BitConverter.GetBytes(0))),
        (CanUploadToCloud, new MemoryStream(BitConverter.GetBytes(0))),
    ];

    // Panoya en son koyduğumuz metnin sıra numarası (geri konunca 0): panoda hâlâ kendi metnimiz duruyorsa (önceki geri koyma
    // başarısız olduysa) o "önceki içerik" sayılmaz, boşaltılır.
    private static uint _snippetSequence;

    /// <summary>
    /// Metni <paramref name="target"/> penceresine yapıştırır. Çağıran önce halkanın kapanmasını, hedefin öne gelmesini ve
    /// kısayol tuşlarının bırakılmasını bekler; hedefin önde olduğuna ve girdiyi alabildiğine burada da (panoya yazmadan ve
    /// Ctrl+V'den hemen önce) bakılır. Metin <see cref="RingActions.ValidText"/> değilse hiçbir şey yapmaz. Hata fırlatmaz.
    /// </summary>
    public static async Task<Outcome> PasteAsync(IntPtr target, string text)
    {
        if (!RingActions.ValidText(text)) return Outcome.Failed;
        if (!KeySender.TryEnterGate()) return Outcome.Busy;

        var run = new PasteRun(target, text);
        Task work;
        try { work = ShellFileOperations.RunSta(run.Execute); }
        catch (Exception ex)
        {
            KeySender.ExitGate();
            DebugLog.Write($"SnippetPaste: {ex.GetType().Name}");
            return Outcome.Failed;
        }
        // Kapı iş gerçekten bitince bırakılır: süre dolsa da geç kalan iş başka bir gönderimle iç içe geçmesin. İş zamanında
        // biterse burada hemen (dönmeden önce) bırakılır; devam işlevi o zaman bir şey yapmaz.
        var released = 0;
        void Release() { if (Interlocked.Exchange(ref released, 1) == 0) KeySender.ExitGate(); }
        _ = work.ContinueWith(_ => Release(), TaskScheduler.Default);

        if (await Task.WhenAny(work, Task.Delay(Timeout)).ConfigureAwait(false) != work)
        {
            run.Abandon();
            DebugLog.Write("SnippetPaste: timeout");
            return run.Sent ? Outcome.PastedNotRestored : Outcome.Failed;
        }
        Release();
        return run.Result;
    }

    /// <summary>Bir yapıştırmanın STA iş parçacığındaki gövdesi ve durumu.</summary>
    private sealed class PasteRun(IntPtr target, string text)
    {
        private volatile bool _abandoned;
        private volatile bool _sent;

        public Outcome Result { get; private set; } = Outcome.Failed;
        public bool Sent => _sent;

        /// <summary>Süre doldu: iş bundan sonra Ctrl+V göndermez, panoya yazdıysa önceki içeriği geri koyar.</summary>
        public void Abandon() => _abandoned = true;

        /// <summary>Hiçbir hatayı dışarı sızdırmaz; günlüğe yalnızca hatanın türü yazılır.</summary>
        public void Execute()
        {
            try { Result = Run(); }
            catch (Exception ex)
            {
                DebugLog.Write($"SnippetPaste: {ex.GetType().Name}");
                Result = _sent ? Outcome.PastedNotRestored : Outcome.Failed;
            }
        }

        private Outcome Run()
        {
            // 1. Hedef: panoya dokunmadan önce.
            if (Map(ForegroundInfo.Check(target)) is { } refused) return refused;

            // 2. Önceki içerik (yalnızca bellekte); okunamazsa yapıştırılmaz (yapıştırsak kaybolurdu).
            if (_abandoned) return Outcome.Failed;
            var previous = Save();
            if (previous is null || _abandoned) return Outcome.Failed;

            // 3. Metin + dışlama biçimleri; copy: true → OleFlushClipboard: iş parçacığı bitse de içerik panoda kalır.
            // Otomatik dönüştürme yok (WPF'in .NET nesne biçimi eklenmesin); Text/OEMText'i Windows UnicodeText'ten üretir.
            Clipboard.SetDataObject(Build(SnippetPayload(text)), copy: true);
            var sequence = GetClipboardSequenceNumber();
            _snippetSequence = sequence;

            // 4. Ctrl+V: hedef hâlâ önde ve girdiyi alabiliyorsa (hâlâ basılı değiştiriciler önce bırakılır).
            var result = _abandoned ? SendResult.Failed : KeySender.SendTo(target, ["Ctrl+V"], KeySender.HeldModifiers());
            if (result != SendResult.Sent)
            {
                // Yapıştırılmadı: önceki içerik hemen geri (pano bu arada değişmediyse). Geri konamazsa panoda kalan kendi
                // metnimiz bir sonraki yapıştırmada "önceki içerik" sayılmaz (_snippetSequence).
                try { if (GetClipboardSequenceNumber() == sequence) Restore(previous); }
                catch (Exception ex) when (ex is ExternalException or InvalidOperationException or IOException)
                {
                    DebugLog.Write($"SnippetPaste: {ex.GetType().Name}");
                }
                return Map(result);
            }
            _sent = true;

            // 5. Hedef okusun (iletiler işlenerek); sonra yalnızca pano bu arada değişmediyse geri koy.
            PumpFor(RestoreDelay);
            if (GetClipboardSequenceNumber() != sequence) return Outcome.ClipboardChanged;
            try { Restore(previous); }
            catch (Exception ex) when (ex is ExternalException or InvalidOperationException or IOException)
            {
                DebugLog.Write($"SnippetPaste: {ex.GetType().Name}");
                return Outcome.PastedNotRestored;
            }
            return previous.Saved.Count == 0 ? (previous.Lossy ? Outcome.NotRestorable : Outcome.Restored)
                : previous.Lossy ? Outcome.PartiallyRestored : Outcome.Restored;
        }

        private static Outcome? Map(InputTarget check) => check switch
        {
            InputTarget.Ok => null,
            InputTarget.NotInFront => Outcome.NotInFront,
            InputTarget.Elevated => Outcome.Elevated,
            _ => Outcome.NoTarget,
        };

        private static Outcome Map(SendResult result) => result switch
        {
            SendResult.NoTarget => Outcome.NoTarget,
            SendResult.NotInFront => Outcome.NotInFront,
            SendResult.Elevated => Outcome.Elevated,
            _ => Outcome.Failed,
        };
    }

    /// <summary>Saklanan önceki içerik: geri konacak biçimler ve bir şeyin düşüp düşmediği.</summary>
    private sealed record Previous(List<(string Format, object Value)> Saved, bool Lossy);

    /// <summary>
    /// Panodaki içeriğin geri konacak kopyası; pano açılamazsa null. Okunamayan asıl biçim düşer (Lossy). Panoda hâlâ kendi
    /// metnimiz duruyorsa önceki içerik yok sayılır (geri konunca pano boşaltılır).
    /// </summary>
    private static Previous? Save()
    {
        var saved = new List<(string, object)>();
        if (_snippetSequence != 0 && GetClipboardSequenceNumber() == _snippetSequence) return new Previous(saved, Lossy: false);

        IDataObject? current;
        string[] formats;
        try
        {
            current = Clipboard.GetDataObject();
            if (current is null) return new Previous(saved, Lossy: false);
            formats = current.GetFormats(autoConvert: false);
        }
        catch (ExternalException) { return null; }

        var plan = Classify(formats);
        var lossy = plan.Lossy;
        foreach (var format in plan.Formats)
        {
            object? value;
            try { value = current.GetData(format, autoConvert: false); }
            catch (Exception ex) when (ex is ExternalException or OutOfMemoryException or InvalidOperationException) { value = null; }
            // Akışlar kopyalanır: panonun eski nesnesi değişince geçersiz kalmasın.
            try { if (value is Stream stream) value = Copy(stream); }
            catch (Exception ex) when (ex is IOException or NotSupportedException or ObjectDisposedException) { value = null; }
            if (value is null) { lossy = true; continue; }
            saved.Add((format, value));
        }
        return new Previous(saved, lossy);
    }

    /// <summary>Önceki içeriği (dışlama biçimleriyle) geri koyar; geri konacak bir şey yoksa panoyu boşaltır.</summary>
    private static void Restore(Previous previous)
    {
        if (previous.Saved.Count == 0) Clipboard.Clear();
        else
        {
            // Akışlar bir kez okunur: her geri koymada baştan.
            foreach (var (_, value) in previous.Saved) if (value is Stream { CanSeek: true } stream) stream.Position = 0;
            Clipboard.SetDataObject(Build(RestorePayload(previous.Saved)), copy: true);
        }
        _snippetSequence = 0;
    }

    private static DataObject Build(IEnumerable<(string Format, object Value)> payload)
    {
        var data = new DataObject();
        foreach (var (format, value) in payload) data.SetData(format, value, autoConvert: false);
        return data;
    }

    private static MemoryStream Copy(Stream stream)
    {
        var copy = new MemoryStream();
        if (stream.CanSeek) stream.Position = 0;
        stream.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    /// <summary>
    /// Süre dolana dek bu iş parçacığının iletilerini işleyerek bekler: panonun sahibi (OLE pano penceresi) bu iş
    /// parçacığındadır; başka bir uygulama panoyu boşaltırken gönderdiği WM_DESTROYCLIPBOARD'a hemen yanıt verilir.
    /// </summary>
    private static void PumpFor(TimeSpan duration)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < duration)
        {
            var left = (uint)Math.Max(1, Math.Ceiling((duration - clock.Elapsed).TotalMilliseconds));
            MsgWaitForMultipleObjectsEx(0, IntPtr.Zero, left, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
            while (PeekMessageW(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
        }
    }

    private const uint QS_ALLINPUT = 0x04FF;
    private const uint MWMO_INPUTAVAILABLE = 0x0004;
    private const uint PM_REMOVE = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
        public uint lPrivate;
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    private static extern uint MsgWaitForMultipleObjectsEx(uint count, IntPtr handles, uint milliseconds, uint wakeMask, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out MSG msg, IntPtr hwnd, uint filterMin, uint filterMax, uint remove);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref MSG msg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG msg);
}
