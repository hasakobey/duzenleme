using System.Windows;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>Widget pencerelerini açar, kapatır ve ayarlarla eşitler.</summary>
public sealed class WidgetManager
{
    private static readonly TimeSpan RevealTime = TimeSpan.FromSeconds(5);

    /// <summary>Yeni widget vurgulu olarak bu kadar öne gelir (yazılan not odak kalkana dek önde kalır).</summary>
    private static readonly TimeSpan NewWidgetRevealTime = TimeSpan.FromSeconds(6);

    private readonly Dictionary<string, WidgetWindow> _open = [];
    private readonly HashSet<string> _closingOnPurpose = [];
    private readonly DispatcherTimer _shellWatch;
    private bool _shuttingDown;

    public event Action? Changed;

    public IReadOnlyList<WidgetConfig> Configs => AppHost.Settings.Widgets;

    /// <summary>Widget'lar gizli mi (masaüstü gizlendi ya da Windows masaüstüne göz atılıyor)? Durumu AppHost belirler.</summary>
    public bool Hidden { get; private set; }

    public WidgetManager()
    {
        // Masaüstü nöbetçisi: Explorer yeniden başlarsa ya da (Windows 10 / 11 23H2'de) duvar kağıdı slayt gösterisi
        // masaüstünü WorkerW'ye bölerse widget'lar yeni sahibine bağlanır; aksi halde duvar kağıdının arkasında kalırlar.
        // Her turda pencereler taranmaz: WorkerW düzeninde son sahip önbellektedir (bkz. DesktopIcons.FindDefView). Gezgin
        // yeniden başlayınca tepsinin bildirimiyle beklemeden de bağlanılır.
        _shellWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _shellWatch.Tick += (_, _) => CheckDesktopOwner();
        _shellWatch.Start();

        // Monitör takılıp çıkarılınca/uykudan uyanınca düzen birkaç saniye oynar; oturunca widget'lar kayıtlı yerlerine döner.
        _displaySettle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _displaySettle.Tick += (_, _) =>
        {
            _displaySettle.Stop();
            foreach (var window in _open.Values) window.RestoreSavedPosition();
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
    }

    private readonly DispatcherTimer _displaySettle;

    /// <summary>Widget'ları masaüstünün şimdiki sahibine bağlar (değiştiyse) ve Windows simgelerinin durumunu uzlaştırır.</summary>
    private void CheckDesktopOwner()
    {
        AttachAllToDesktop();
        AppHost.ReconcileDesktopState();
    }

    private void AttachAllToDesktop()
    {
        // Masaüstü sahibini bir kez bul, tüm widget'lar için kullan.
        var owner = Desktop.DesktopIcons.DesktopOwner();
        foreach (var window in _open.Values) window.EnsureAttached(owner);
    }

    /// <summary>
    /// Gezgin yeniden başladı (görev çubuğu yeniden kuruldu; tepsinin "TaskbarCreated" bildirimi): widget'lar 3 sn'lik
    /// nöbetçiyi beklemeden yeni masaüstüne bağlanır. Masaüstü penceresi henüz hazır değilse sonraki tur yakalar. Simgelerin
    /// durumu burada uzlaştırılmaz, sonraki tura kalır: Gezgin açılırken simge listesini kendisi yeniden gösterir; o an da
    /// gizlemek "üst üste yeniden gösterildi" sayacını boşuna artırır ve gizli masaüstünden vazgeçirebilir.
    /// </summary>
    public void OnShellRestarted()
    {
        if (_shuttingDown) return;
        _shellWatch.Stop();
        try { AttachAllToDesktop(); }
        finally
        {
            // Bağlanırken bir hata olsa da nöbetçi durmasın (sonraki slayt gösterisi bölünmesini de yakalasın).
            if (!_shuttingDown) _shellWatch.Start();
        }
    }

    private void OnDisplayChanged(object? sender, EventArgs e) =>
        Application.Current?.Dispatcher.BeginInvoke(() => { _displaySettle.Stop(); _displaySettle.Start(); });

    private int _restoreGeneration;

    /// <summary>Açılışta (ya da düzen uygulanınca) widget'lar sırayla açılıyor mu?</summary>
    public bool Restoring { get; private set; }

    /// <summary>
    /// Kayıtlı widget'ları açar. Hepsi tek seferde değil, her biri ayrı bir iş dağıtıcı turunda (Background önceliğinde)
    /// açılır: ilk widget hemen çizilir, tepsi ve fare arada yanıt verir; zayıf bilgisayarda açılış donmuş görünmez.
    /// Öndeki widget önce açılır (her yeni pencere en alta indiği için sıra kendiliğinden doğru kurulur).
    /// </summary>
    public void RestoreAll()
    {
        var generation = ++_restoreGeneration;
        var queue = new Queue<WidgetConfig>(AppHost.Settings.Widgets.OrderByDescending(w => w.Z));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Restoring = queue.Count > 0;

        void OpenNext()
        {
            // Arada yeni bir düzen uygulandıysa ya da kapanıyorsa eski sıra bırakılır.
            if (generation != _restoreGeneration || _shuttingDown) return;
            while (queue.Count > 0)
            {
                var config = queue.Dequeue();
                // Arada kaldırılmış ya da zaten açılmış (ör. "geri getir") widget atlanır.
                if (!AppHost.Settings.Widgets.Contains(config) || _open.ContainsKey(config.Id)) continue;
                TryOpen(config);
                break;
            }
            if (queue.Count > 0)
            {
                Application.Current.Dispatcher.BeginInvoke(OpenNext, DispatcherPriority.Background);
                return;
            }
            Restoring = false;
            ApplyZOrder();
            PerfLog.Write($"widget'lar açıldı: {_open.Count} widget, {clock.ElapsedMilliseconds} ms"); // l10n: çevrilmez
            if (PerfLog.Enabled) PerfLog.Mark("RestoreAll.end", $"widgets={_open.Count} {PerfLog.Gc()}");
            Changed?.Invoke();
        }

        OpenNext();
    }

    /// <summary>
    /// Widget'ı diğer widget'ların önüne alır; yine masaüstü katmanında (açık pencerelerin arkasında) kalır.
    /// Tıklanan, taşınan ya da yeni eklenen widget başka bir widget'ın altında kaybolmasın. Her tıklamada çağrıldığı için
    /// sıra diske hemen yazılmaz: bir sonraki kayıtla birlikte gider (<see cref="AppHost.SaveSettingsLater"/>).
    /// </summary>
    public void BringToFront(WidgetWindow window)
    {
        var top = _open.Values.Where(w => w != window).Select(w => w.Config.Z).DefaultIfEmpty(0).Max();
        if (window.Config.Z <= top)
        {
            window.Config.Z = Math.Max(top + 1, DateTime.UtcNow.Ticks);
            AppHost.SaveSettingsLater();
        }
        ApplyZOrder();
    }

    /// <summary>Masaüstü sistem simgeleri (Bu Bilgisayar, Geri Dönüşüm Kutusu…) açılıp kapandı: bunları gösteren bölmeler güncellensin.</summary>
    public void RefreshSystemIcons()
    {
        foreach (var window in _open.Values)
            if (window.View is FenceView fence) fence.RefreshSystemIcons();
    }

    /// <summary>
    /// Widget'ları kayıtlı sıraya dizer. Hepsi en alta itildiği için sırayla (en öndeki önce) en alta gönderilir:
    /// en son gönderilen en altta kalır.
    /// </summary>
    private void ApplyZOrder()
    {
        foreach (var window in _open.Values.OrderByDescending(w => w.Config.Z).ToList()) window.SendToBottom();
    }

    public WidgetConfig Add(WidgetKind kind, string? folderName = null)
    {
        // Sekme adları (kutu) tohumda arayüz dilinde bir kez yazılır (dil değişince yeniden adlandırılmaz).
        var config = WidgetSeeds.Create(kind.ToString()) ?? new WidgetConfig { Kind = kind };
        config.FolderName = folderName;
        return AddSeed(config);
    }

    /// <summary>Onay kutulu liste (Yapılacaklar): Kind yine Note'tur, yeni enum üyesi yoktur (eski sürümler düz not görür).</summary>
    public WidgetConfig AddChecklist() => AddSeed(WidgetSeeds.Create(WidgetSeeds.Checklist)!);

    /// <summary>
    /// Hazır ayarla (<see cref="WidgetSeeds"/>) widget ekler: sırası en öne, notun rengi sıradaki renk olur; yeri
    /// <see cref="PlacementHint"/> ya da ayardaki kip. Açılamazsa kullanıcı uyarılır ve widget listeden çıkar.
    /// </summary>
    public WidgetConfig AddSeed(WidgetConfig seed)
    {
        seed.Z = DateTime.UtcNow.Ticks;
        if (seed.Kind == WidgetKind.Note) seed.NoteColor = NextNoteColor();
        return AddConfig(seed);
    }

    /// <summary>Yeni notun kağıt rengi: notlar sırayla sarı, pembe, yeşil, mavi, mor olur.</summary>
    private static NoteColor NextNoteColor() => (NoteColor)(AppHost.Settings.Widgets.Count(w => w.Kind == WidgetKind.Note) % 5);

    /// <summary>
    /// Klasör bölmesi seçenekleri: kural klasörleri (PDF, Resimler…; masaüstünde yoksa eklenirken açılır) ve
    /// kullanıcının masaüstündeki diğer klasörleri.
    /// </summary>
    public List<(string Name, bool Exists)> FolderFenceChoices()
    {
        var existing = AppHost.DesktopFolders();
        var names = new List<string>();
        foreach (var rule in AppHost.Settings.Rules.Where(r => r.Enabled))
            if (!names.Any(n => FolderName.Equal(n, rule.TargetFolder)))
                names.Add(existing.FirstOrDefault(f => FolderName.Equal(f, rule.TargetFolder)) ?? rule.TargetFolder);
        foreach (var folder in existing.OrderBy(f => f, L.Sorter))
            if (!names.Any(n => FolderName.Equal(n, folder))) names.Add(folder);
        return names.Select(n => (n, existing.Any(f => FolderName.Equal(f, n)))).ToList();
    }

    /// <summary>
    /// Bir klasörün içini gösteren bölme ekler; klasör masaüstünde yoksa açar (uygun dosyalar oraya taşınır).
    /// Klasör açılamazsa hata gösterir ve null döner.
    /// </summary>
    public WidgetConfig? AddFolderFence(string name)
    {
        if (!AppHost.DesktopFolders().Any(f => FolderName.Equal(f, name)))
        {
            var path = System.IO.Path.Combine(AppHost.DesktopDirectory, name);
            // Klasörü biz açıyoruz: "simge ver" balonu çıkmasın; otomatik taşıma kapalıysa dosya da taşınmasın.
            AppHost.MarkQuietFolder(path);
            if (FolderCreation.TryCreate(path) is { Ok: false } failed)
            {
                AppHost.ConsumeQuietFolder(path);
                Views.Notice.FolderFailed(failed);
                return null;
            }
            AppHost.NoteFolderCreated(path);
            AppHost.OrganizeIfActive();
        }
        return Add(WidgetKind.Fence, name);
    }

    /// <summary>Masaüstündeki öğeleri türüne göre gösteren bölme (Klasörler, Kısayollar, Dosyalar, Tümü).</summary>
    public WidgetConfig AddFence(DesktopFilter filter) =>
        AddConfig(new WidgetConfig { Kind = WidgetKind.Fence, Filter = filter, Sort = FenceSort.Name, Z = DateTime.UtcNow.Ticks });

    /// <summary>
    /// "Masaüstümü bölmelere ayır": Klasörler, Kısayollar, Dosyalar ve masaüstünde var olan kural klasörleri
    /// (PDF, Resimler…) için birer bölme açar; zaten olanları tekrar eklemez (bkz. <see cref="StarterFences.Plan"/>).
    /// near verilirse bölmeler o noktanın monitörüne yerleşir. Eklenen bölme sayısını döner.
    /// </summary>
    internal int AddStarterFences(NativeMethods.POINT? near = null)
    {
        var plan = StarterFences.Plan(AppHost.Settings.Widgets, AppHost.Settings.Rules, AppHost.DesktopFolders());
        foreach (var fence in plan)
        {
            // Birden çok bölme: türüne göre köşeden dizilir (imlecin yanında üst üste yığılmasın).
            PlacementHint = WidgetPlacement.CornerNear(near);
            if (fence.Folder is { } folder) Add(WidgetKind.Fence, folder);
            else AddFence(fence.Filter);
        }
        return plan.Count;
    }

    public void Duplicate(string id)
    {
        if (AppHost.Settings.Widgets.FirstOrDefault(w => w.Id == id) is not { } source) return;
        var copy = source.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        // Yeni kopya boş bir yere yerleşsin (kaydedilmiş konumu yok).
        copy.Left = copy.Top = double.NaN;
        copy.PixelLeft = copy.PixelTop = null;
        copy.Z = DateTime.UtcNow.Ticks;
        WidgetSeeds.PrepareDuplicate(copy);
        AddConfig(copy);
    }

    private WidgetConfig AddConfig(WidgetConfig config)
    {
        // Yer bu eklemeye aittir: ipucu hemen tüketilir (pencere yerleşene dek pencerede durur).
        var place = PlacementHint ?? WidgetPlacement.FromSettings();
        PlacementHint = null;
        // Eklerken seçilen ad ve simge ("Yeni bölme…"): pencere açılmadan yazılır, widget ilk çizimde doğru görünür.
        var setup = NextSetup;
        NextSetup = null;
        setup?.Invoke(config);
        AppHost.Settings.Widgets.Add(config);
        AppHost.SaveSettings();
        // Masaüstü gizliyse ya da göz atılıyorsa widget'lar geri gelir (durumu AppHost değiştirir; tepsi ve Ayarlar da güncellenir).
        AppHost.EnsureWidgetsShown();
        if (TryOpen(config, place, fadeIn: true) is { } window)
        {
            // Yeni widget pencerelerin arkasında kalıp "eklenmedi" sanılmasın: birkaç saniye vurgulu olarak öne gelsin.
            // (Loaded, Show() sırasında tetiklenebildiği için ona bağlanılmaz; yerleşim bitince çalıştırılır.)
            window.Dispatcher.BeginInvoke(() => window.Reveal(NewWidgetRevealTime, highlight: true), DispatcherPriority.ContextIdle);
        }
        else
        {
            // Açılamayan widget listede "eklendi" görünüp masaüstünde hiç çıkmasın.
            AppHost.Settings.Widgets.Remove(config);
            AppHost.SaveSettings();
            MessageBox.Show(L.F("Widget açılamadı. {0}'ten çıkıp yeniden açtıktan sonra tekrar dene.", AppInfo.Name), AppInfo.Name,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        Changed?.Invoke();
        return config;
    }

    /// <summary>Widget'ı kaldırır (hemen kapanır). "Simgeler yalnızca bölmelerde" modu bu yüzden kapandıysa true.</summary>
    public bool Remove(string id, bool notify = true) => RemoveCore(id, notify, leave: false);

    /// <param name="leave">Kullanıcı tek bir widget'ı kaldırdı (<see cref="RemoveWithUndo"/>): kart kısa bir çıkışla gider.
    /// Toplu ve iç kaldırmalar hemen kapanır.</param>
    private bool RemoveCore(string id, bool notify, bool leave)
    {
        if (_open.Remove(id, out var window))
        {
            _closingOnPurpose.Add(id);
            if (leave) Leave(window);
            else window.Close();
        }
        AppHost.Settings.Widgets.RemoveAll(w => w.Id == id);
        AppHost.SaveSettings();
        var modeOff = AppHost.EnsureNothingInvisible(notify);
        Changed?.Invoke();
        return modeOff;
    }

    /// <summary>
    /// Kaldırılan widget'ın çıkışı: kart hafifçe küçülüp solar, sonra pencere kapanır (Views.Motion; Kapalı'da hemen). Pencere
    /// listeden hemen çıkmıştır ("Geri al" aynı kimlikle yenisini açabilir); bu arada kartına tıklanamaz ve klavye çalışmaz
    /// (<see cref="WidgetWindow.BeginLeave"/>; girdiyi Motion değil burası kapatır: dosya açılmasın, düğme çalışmasın).
    /// </summary>
    private static void Leave(WidgetWindow window)
    {
        if (!window.IsVisible)
        {
            window.Close();
            return;
        }
        window.BeginLeave();
        Views.Motion.Exit(window, window.CloseIfOpen, window.Surface, MotionSurface.LayeredWidget, window.PixelArea);
        // Çıkış herhangi bir nedenle bitmezse de pencere kapanır (görünmez bir pencere kalmasın).
        var fallback = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(MotionPolicy.MaxMs * 3) };
        fallback.Tick += (_, _) =>
        {
            fallback.Stop();
            window.CloseIfOpen();
        };
        fallback.Start();
    }

    private readonly RemovedWidgets _removed = new();

    /// <summary>Son kaldırılan widget'ın adı (tepsi menüsündeki "geri getir" için); yoksa null.</summary>
    public string? LastRemovedName => _removed.Latest is { } last ? WidgetText.DisplayName(last.Copy) : null;

    /// <summary>
    /// Widget'ı kaldırır; tek bir bildirimle (<see cref="UndoRemove(string)"/>) ya da tepsi menüsündeki "geri getir" ile
    /// ayarları ve yeriyle geri gelir (kaldırma yüzünden kapanan "simgeler yalnızca bölmelerde" modu da geri açılır).
    /// notify=false: tepsi balonu çıkmaz (çağıran kendi bildirimini gösterir), geri getirme yine çalışır. Mod bu yüzden
    /// kapandıysa true.
    /// </summary>
    public bool RemoveWithUndo(string id, bool notify = true)
    {
        var index = AppHost.Settings.Widgets.FindIndex(w => w.Id == id);
        if (index < 0) return false;
        if (_open.TryGetValue(id, out var window)) window.FlushState(); // son taşıma/yazılanlar da geri gelsin
        var copy = AppHost.Settings.Widgets[index].Clone();
        // Kutunun masaüstünden taşınmış öğeleri (başka kutuda yoksa) masaüstüne döner; "Geri al" onları yeniden taşır.
        var returning = BoxMover.MovedOnlyIn(AppHost.Settings.Widgets[index]);
        var modeOff = RemoveCore(id, notify: false, leave: true);
        // Kullanıcı kaldırdı: ortak masaüstüne dönecek öğe için Windows onay isteyebilir (arka plandaki uzlaştırma sormaz).
        BoxMover.Reconcile(interactive: true);
        _removed.Add(new RemovedWidget(copy, index, modeOff));
        if (notify)
            AppHost.Tray?.Notify(L.T("Widget kaldırıldı"),
                RemovedText(null, returning, modeOff, L.T("Geri getirmek için buraya ya da tepsi menüsüne tıkla.")),
                () => UndoRemove(id));
        return modeOff;
    }

    /// <summary>
    /// Kaldırma bildiriminin metni (tepsi balonu, Widget'lar sayfası, kutu sekmesi ve alt halka): her cümle ayrı çevrilir,
    /// aralarına boşluk konur. returning: masaüstüne dönecek taşınmış öğe sayısı (<see cref="BoxMover.ReturningIf"/>).
    /// lead/tail: baştaki ("Saat kaldırıldı.") ve sondaki cümle; null ise yok.
    /// </summary>
    internal static string RemovedText(string? lead, int returning, bool modeOff, string? tail = null)
    {
        var sentences = new List<string>(4);
        if (lead is not null) sentences.Add(lead);
        // Kutu, halka, sekme ya da alt halka: masaüstünden kalkmış öğelerden başka yerde durmayanlar geri döner.
        if (returning > 0) sentences.Add(L.P(returning, "Masaüstünden kalkan {0} simge geri dönüyor."));
        if (modeOff) sentences.Add(L.T("Masaüstü simgeleri yeniden gösteriliyor."));
        if (tail is not null) sentences.Add(tail);
        return string.Join(" ", sentences);
    }

    /// <summary>En son kaldırılan widget'ı geri getirir (tepsi menüsü).</summary>
    public void UndoRemove()
    {
        if (_removed.Latest is { } last) UndoRemove(last.Copy.Id);
    }

    /// <summary>
    /// Bu widget'ı geri getirir (bildirimdeki "Geri al"). Arada başka widget kaldırılmış olsa da yalnızca bu gelir; zaten
    /// geri geldiyse bir şey yapmaz.
    /// </summary>
    public void UndoRemove(string id)
    {
        if (_removed.Take(id) is not { } last) return;
        if (AppHost.Settings.Widgets.Any(w => w.Id == last.Copy.Id)) return;
        AppHost.Settings.Widgets.Insert(Math.Min(last.Index, AppHost.Settings.Widgets.Count), last.Copy);
        AppHost.SaveSettings();
        AppHost.EnsureWidgetsShown();
        if (TryOpen(last.Copy, fadeIn: true) is not null) ApplyZOrder();
        if (last.ModeTurnedOff && !AppHost.Settings.FencesReplaceIcons) AppHost.SetFencesManageDesktop(true);
        Changed?.Invoke();
        // Kutu geri geldi: masaüstüne dönen öğeleri yeniden taşınırken ortak masaüstü için Windows onay isteyebilir.
        BoxMover.Reconcile(interactive: true);
    }

    /// <summary>Masaüstündeki her öğe türü (klasör, kısayol, dosya) en az bir bölmede görünüyor mu?</summary>
    public bool CoversDesktop()
    {
        var filters = AppHost.Settings.Widgets.Where(w => w.Kind == WidgetKind.Fence).Select(w => w.Filter).ToHashSet();
        return filters.Contains(DesktopFilter.All) ||
               (filters.Contains(DesktopFilter.Folders) && filters.Contains(DesktopFilter.Shortcuts) && filters.Contains(DesktopFilter.Files));
    }

    /// <summary>
    /// Eksik Klasörler/Kısayollar/Dosyalar bölmelerini ekler (masaüstünü bölmeler yönetecekse). near verilirse bölmeler o
    /// noktanın monitörüne yerleşir.
    /// </summary>
    internal void EnsureDesktopCoverage(NativeMethods.POINT? near = null)
    {
        if (CoversDesktop()) return;
        var filters = AppHost.Settings.Widgets.Where(w => w.Kind == WidgetKind.Fence).Select(w => w.Filter).ToHashSet();
        foreach (var filter in new[] { DesktopFilter.Folders, DesktopFilter.Shortcuts, DesktopFilter.Files })
        {
            if (filters.Contains(filter)) continue;
            PlacementHint = WidgetPlacement.CornerNear(near);
            AddFence(filter);
        }
    }

    /// <summary>Düzen uygulanmadan önce alınan otomatik yedeğin adı (Kayıtlı yerleşimler'de durur; arayüz dilinde).</summary>
    public static string ApplyBackupName => LayoutBackup.ApplyName;

    /// <summary>
    /// Kayıtlı bir düzeni uygular: mevcut widget'lar kapanır, düzendekiler açılır. Önceki yerleşim önce
    /// "Düzen uygulanmadan önce" adıyla kaydedilir (yedeğin kendisi uygulanıyorsa yedek alınmaz).
    /// </summary>
    public void ApplyLayout(LayoutSnapshot layout)
    {
        if (!LayoutBackup.IsApply(layout.Name)) LayoutBackup.SaveBeforeApply(AppHost.Settings.Layouts, AppHost.Settings.Widgets);
        foreach (var (id, window) in _open.ToList())
        {
            _closingOnPurpose.Add(id);
            window.Close();
        }
        _open.Clear();
        AppHost.Settings.Widgets = layout.Restore();
        AppHost.SaveSettings();
        AppHost.EnsureWidgetsShown();
        RestoreAll();
        AppHost.EnsureNothingInvisible();
        Changed?.Invoke();
    }

    /// <summary>
    /// Widget'ın şimdi açık görünümü; yoksa null. Bildirimdeki "Geri al" görünümü kimlikle bulur: widget o arada kaldırılıp
    /// geri getirildiyse yeni pencerede yeni görünüm (ve ayarın kopyası) vardır.
    /// </summary>
    internal IWidgetView? ViewOf(string id) => _open.TryGetValue(id, out var window) ? window.View : null;

    /// <summary>Yeni notu öne alıp yazmaya hazır hale getirir (düz notta yazı alanı, yapılacaklarda ilk satır).</summary>
    public void FocusNote(string id)
    {
        if (!_open.TryGetValue(id, out var window) || window.View is not NoteView note) return;
        window.Dispatcher.BeginInvoke(() =>
        {
            window.ActivateForInput();
            note.FocusEditor();
        }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Tüm widget'ları birkaç saniyeliğine pencerelerin önüne getirir.</summary>
    public void RevealAll()
    {
        AppHost.EnsureWidgetsShown();
        foreach (var window in _open.Values) window.Reveal(RevealTime);
    }

    /// <summary>Tek bir widget'ı öne getirir; ekran dışında kaldıysa görünür bir yere taşır.</summary>
    public void Reveal(string id)
    {
        if (!_open.TryGetValue(id, out var window)) return;
        AppHost.EnsureWidgetsShown();
        // Ekran dışı kontrolü fiziksel pikselle yapılır: DIP konumları ölçeği farklı monitörlerde kayar.
        if (!window.IsOnScreen()) window.MoveToFreeSpot(WidgetPlacement.CornerNear(null));
        window.Reveal(RevealTime);
        // Kart bir kez hafifçe nabız atar: hangisi olduğu görülsün (yeni beliriyorsa büyümesi yeter).
        window.Attention();
    }

    /// <summary>
    /// "Widget'ları hizala": tüm widget'ları bulundukları monitörde çakışmadan dizer: sağ kenardan başlayan sütunlar (masaüstü
    /// simgeleri genelde soldadır), geniş olanlar önce. Önceki düzen "Otomatik yerleştirmeden önce" adıyla kaydedilir;
    /// <paramref name="moves"/> bildirimdeki "Geri al" içindir (<see cref="UndoArrange"/>).
    /// </summary>
    public int ArrangeAll(out IReadOnlyList<ArrangeMove> moves)
    {
        moves = [];
        AppHost.EnsureWidgetsShown();
        var placed = _open.Values.Select(w => (Window: w, Bounds: w.PixelBounds)).Where(t => t.Bounds is not null).ToList();
        if (placed.Count == 0) return 0;

        LayoutBackup.SaveBeforeArrange(AppHost.Settings.Layouts, AppHost.Settings.Widgets);
        var record = new List<ArrangeMove>();

        var byMonitor = placed.GroupBy(t =>
        {
            var r = t.Bounds!.Value;
            return NativeMethods.WorkAreaAt(new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 });
        });
        foreach (var group in byMonitor)
        {
            var area = group.Key;
            var right = area.Right;
            var columnWidth = 0;
            var y = area.Top;
            var first = true;
            // Çalışma alanından uzun widget'ın görünen yüksekliği sütuna sığacak kadar kısılır (kayıtlı yüksekliği değişmez).
            var windows = group.Select(t =>
            {
                if (t.Bounds!.Value.Height <= area.Height) return (t.Window, t.Bounds, Clipped: false);
                t.Window.LimitDisplayHeight(area.Height);
                var now = t.Window.PixelBounds ?? t.Bounds;
                return (t.Window, Bounds: now, Clipped: now!.Value.Height < t.Bounds.Value.Height);
            }).ToList();
            foreach (var (window, bounds, clipped) in windows.OrderByDescending(t => t.Bounds!.Value.Width)
                         .ThenBy(t => t.Bounds!.Value.Top))
            {
                var r = bounds!.Value;
                if (first || (y + r.Height > area.Bottom && y > area.Top))
                {
                    // Yeni sütun (ilk ya da önceki doldu): bir öncekinin soluna.
                    if (!first) right -= columnWidth;
                    columnWidth = r.Width;
                    y = area.Top;
                    first = false;
                }
                var x = Math.Max(area.Left, right - r.Width); // sütunda sağa yaslı
                window.MoveTo(x, y);
                if (window.PixelBounds is { } now) record.Add(new ArrangeMove(window.Config.Id, r.Left, r.Top, now.Left, now.Top, clipped));
                y += r.Height;
            }
        }
        AppHost.SaveSettings();
        Changed?.Invoke();
        moves = record;
        return placed.Count;
    }

    /// <summary>
    /// "Widget'ları hizala"yı geri alır: hâlâ hizalamanın koyduğu yerde duran widget'lar önceki yerlerine döner (sonradan
    /// taşınan, eklenen ya da kaldırılan widget'lara dokunulmaz) ve hizalamanın kıstığı görünen yükseklik geri gelir.
    /// </summary>
    public void UndoArrange(IReadOnlyList<ArrangeMove> moves)
    {
        // Gizli widget'ın konumu okunamaz (PixelBounds): önce görünürler.
        AppHost.EnsureWidgetsShown();
        var back = ArrangeUndo.Pending(moves, id =>
            _open.TryGetValue(id, out var w) && w.PixelBounds is { } r ? (r.Left, r.Top) : null);
        if (back.Count == 0) return;
        foreach (var move in back)
        {
            var window = _open[move.Id];
            if (move.Moved) window.MoveTo(move.FromX, move.FromY);
            window.RefitHeight();
        }
        AppHost.SaveSettings();
        Changed?.Invoke();
    }

    /// <summary>Widget'ın konum kilidini açar ("Konum kilitli." bildirimindeki "Kilidi aç").</summary>
    public void Unlock(string id)
    {
        if (_open.TryGetValue(id, out var window)) window.SetLocked(false);
        else if (AppHost.Settings.Widgets.FirstOrDefault(w => w.Id == id) is { Locked: true } config)
        {
            config.Locked = false;
            AppHost.SaveSettings();
        }
    }

    public void Restyle(string id)
    {
        if (_open.TryGetValue(id, out var window)) window.ApplyStyle();
    }

    // 2.2 Tema
    private bool _restyleQueued, _restyleEverything;

    /// <summary>
    /// Windows'un açık/koyu kipi ya da yüksek karşıtlığı değişti (<c>App.ApplyTheme</c>): Windows'a uyan widget'lar ("Otomatik"
    /// ve iki hâlli temalar) yeniden çizilir; yüksek karşıtlık açılıp kapanınca (<paramref name="all"/>) notlar dahil hepsi.
    /// Art arda gelen bildirimler tek çizimde birleşir (boşta, Background önceliğinde). Ayara hiçbir şey yazılmaz.
    /// </summary>
    public void RestyleAll(bool all)
    {
        _restyleEverything |= all;
        if (_restyleQueued) return;
        _restyleQueued = true;
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _restyleQueued = false;
            var everything = _restyleEverything;
            _restyleEverything = false;
            if (_shuttingDown) return;
            foreach (var window in _open.Values.ToList())
                if (everything || WidgetLooks.FollowsWindows(window.Config)) window.ApplyStyle();
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Arka planı (Cam/Koyu/Açık), vurgu rengini ve/veya temayı bütün widget'lara uygular; null olan değişmez
    /// (<paramref name="theme"/>: "" Klasik, "auto", tema adı). Öncelik kuralları menüdekiyle aynı (<see cref="WidgetLooks"/>):
    /// klasik arka plan temayı kaldırır, adlı vurgu rengi özel rengi siler. Notlar atlanır: kendi kağıt rengini kullanırlar.
    /// </summary>
    public void SetLookForAll(WidgetStyle? style, WidgetAccent? accent, string? theme = null)
    {
        var targets = AppHost.Settings.Widgets.Where(w => w.Kind != WidgetKind.Note).ToList();
        foreach (var config in targets)
        {
            if (theme is not null) WidgetLooks.SetTheme(config, theme, WidgetPalette.SystemDark);
            if (style is { } s) WidgetLooks.SetStyle(config, s);
            if (accent is { } a) WidgetLooks.SetAccent(config, a);
        }
        AppHost.SaveSettings();
        foreach (var config in targets) Restyle(config.Id);
        Changed?.Invoke();
    }

    // 2.2 Kolay NestDesk: Düzenle paneli

    /// <summary>
    /// "Bu görünümü tüm widget'lara uygula" (Düzenle paneli): kaynağın teması, arka planı ve vurgu rengi bütün widget'lara
    /// (notlar hariç: kendi kağıt renkleri var) yazılır. Bildirimdeki "Geri al" için her birinin önceki görünüşünü döner
    /// (<see cref="UndoLookCopy"/>).
    /// </summary>
    internal IReadOnlyList<(string Id, LookFields Before)> CopyLookToAll(WidgetConfig source)
    {
        var before = LookGallery.CopyToAll(source, AppHost.Settings.Widgets.Where(w => w.Kind != WidgetKind.Note).ToList());
        if (before.Count == 0) return before;
        AppHost.SaveSettings();
        foreach (var (id, _) in before) Restyle(id);
        Changed?.Invoke();
        return before;
    }

    /// <summary><see cref="CopyLookToAll"/>'u geri alır: hâlâ duran widget'lar önceki görünüşüne döner (kaldırılanlar atlanır).</summary>
    internal void UndoLookCopy(IReadOnlyList<(string Id, LookFields Before)> before)
    {
        var restored = LookGallery.Undo(before, AppHost.Settings.Widgets);
        if (restored.Count == 0) return;
        AppHost.SaveSettings();
        foreach (var id in restored) Restyle(id);
        Changed?.Invoke();
    }

    /// <summary>Bir widget'ın adı ya da simgesi değişti (Düzenle paneli): Widget'lar listesi ve tepsi güncellensin.</summary>
    internal void NotifyChanged() => Changed?.Invoke();

    /// <summary>
    /// Pencereleri gizler/gösterir. Yalnızca <see cref="AppHost.ApplyDesktopState"/> çağırır: başka yerden gizlemek/göstermek
    /// tepsiyi, Ayarlar'ı ve Windows simgelerini durumla çelişik bırakır (bunun yerine <see cref="AppHost.EnsureWidgetsShown"/>).
    /// </summary>
    internal void SetHidden(bool hidden)
    {
        if (Hidden == hidden) return;
        Hidden = hidden;
        // Views.Motion: gizlenirken solar, gösterilirken hafifçe büyüyerek gelir. Widget'lar yazılımla çizilen katmanlı
        // pencerelerdir: MotionPolicy aynı anda en çok 3 büyümeye (fazlası yalnızca solar, büyük kart da) ve 8 solmaya izin
        // verir; daha çoksa hepsi hemen gizlenip gösterilir.
        var windows = _open.Values.ToList();
        var batch = windows.Count(w => hidden ? w.IsVisible : !w.IsVisible);
        foreach (var window in windows)
        {
            if (hidden)
                Views.Motion.Disappear(window, window.HideIfOpen, MotionKind.Fade, surface: MotionSurface.LayeredWidget,
                    pixels: window.PixelArea, batch: batch);
            else
                Views.Motion.Appear(window, () => QuietShow.Show(window), MotionKind.Pop, window.Surface, MotionSurface.LayeredWidget,
                    window.PixelArea, batch);
        }
    }

    public void CloseAll()
    {
        _shuttingDown = true;
        _shellWatch.Stop();
        _displaySettle.Stop();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        foreach (var window in _open.Values.ToList()) window.Close();
        _open.Clear();
    }

    /// <summary>
    /// Bir sonraki yeni widget'ın yeri (ör. "Widget ekle" penceresinin ya da ana pencerenin ekranı); eklenirken tüketilir.
    /// Yoksa ayardaki kip imlecin yerinde kullanılır (<see cref="WidgetPlacement.FromSettings"/>).
    /// </summary>
    internal WidgetPlacement? PlacementHint { get; set; }

    /// <summary>
    /// Bir sonraki yeni widget'ın ayarına eklenirken uygulanacak son dokunuş (ör. "Yeni bölme…"de seçilen ad ve simge);
    /// <see cref="PlacementHint"/> gibi eklerken tüketilir. Eklenemezse çağıran temizler.
    /// </summary>
    internal Action<WidgetConfig>? NextSetup { get; set; }

    /// <summary>Diğer görünür widget'ların kart dikdörtgenleri (gölge payı hariç, fiziksel piksel).</summary>
    internal List<Box> OtherCards(WidgetWindow? self) =>
        _open.Values.Where(w => w != self).Select(w => w.CardBox).OfType<Box>().ToList();

    /// <summary>
    /// Yeni widget için boş bir yer (pencerenin sol üstü, fiziksel piksel): kipe göre istenen noktaya
    /// (<see cref="WidgetLayout.DesiredSpot"/>) en yakın, diğer widget'lara ve açık ana pencereye (<see cref="MainWindowBox"/>)
    /// değmeyen yer (<see cref="WidgetLayout.FindSpot"/>). Hesap kartla yapılır (gölge payı hariç), aralık bırakılan
    /// widget'lar arasındakiyle aynıdır.
    /// </summary>
    /// <param name="dipSize">Pencerenin DIP boyutu (gölge payı dahil).</param>
    /// <param name="marginDip">Kartın çevresindeki gölge payı (DIP).</param>
    internal NativeMethods.POINT FreeSpot(WidgetConfig config, Size dipSize, double marginDip, WidgetWindow? self, WidgetPlacement place)
    {
        var work = place.WorkArea(out var scale);
        var area = new Box(work.Left, work.Top, work.Right, work.Bottom);
        var m = (int)Math.Round(marginDip * scale);
        var w = Math.Max(1, (int)Math.Ceiling(Math.Max(dipSize.Width, 120) * scale) - 2 * m);
        var h = Math.Max(1, (int)Math.Ceiling(Math.Max(dipSize.Height, 80) * scale) - 2 * m);
        var gap = (int)Math.Round(18 * scale);
        // Köşe kipinde saat/tarih/not (ve alt türleri) sağ üstten, bölme ve kutu üst ortadan (2.0'daki gibi), Geri Dönüşüm
        // Kutusu sağ alttan başlar.
        var corner = WidgetVariants.Corner(config);
        var (dx, dy) = WidgetLayout.DesiredSpot(place.Mode, area, w, h, place.Anchor.X, place.Anchor.Y,
            cornerRight: corner != WidgetCorner.TopCenter, offset: (int)Math.Round(16 * scale), gap,
            cornerBottom: corner == WidgetCorner.BottomRight);
        var taken = OtherCards(self);
        if (MainWindowBox() is { } main) taken.Add(main);
        var (x, y) = WidgetLayout.FindSpot(area, w, h, dx, dy, taken, gap,
            step: Math.Max(8, (int)Math.Round(16 * scale)), horizontalWeight: place.Mode == PlaceMode.Corner ? 4 : 1);
        return new NativeMethods.POINT { X = x - m, Y = y - m };
    }

    /// <summary>
    /// Açık ana pencerenin dikdörtgeni (fiziksel piksel; simge durumundaysa ya da gizliyse null). Yeni widget onun da yanına
    /// yerleşir: widget'lar hep en altta durur, ana pencerenin altına düşen widget vurgusu bitince arkasında kaybolurdu
    /// (ör. Ana sayfadan imleç kipinde eklerken).
    /// </summary>
    private static Box? MainWindowBox()
    {
        if (Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault(w => w.IsVisible && w.WindowState != WindowState.Minimized)
            is not { } main) return null;
        var handle = new System.Windows.Interop.WindowInteropHelper(main).Handle;
        return handle != IntPtr.Zero && NativeMethods.GetWindowRect(handle, out var r) && r.Right > r.Left && r.Bottom > r.Top
            ? new Box(r.Left, r.Top, r.Right, r.Bottom) : null;
    }

    private WidgetWindow? TryOpen(WidgetConfig config, WidgetPlacement? place = null, bool fadeIn = false)
    {
        // Bir widget'ın açılamaması başlangıcı (izleyici, tepsi, ana pencere) durdurmasın.
        try { return Open(config, place, fadeIn); }
        catch (Exception ex)
        {
            DebugLog.Write($"widget açılamadı {config.Kind}: {ex}");
            AppLog.Error("widget", "open-failed", ex);
            if (_open.Remove(config.Id, out var half))
            {
                _closingOnPurpose.Add(config.Id);
                try { half.Close(); } catch (InvalidOperationException) { }
            }
            return null;
        }
    }

    /// <param name="fadeIn">Tek tek eklenen/geri gelen widget hafifçe büyüyerek ve solarak belirir (Views.Motion.Pop); açılıştaki
    /// toplu açılışta hiçbir şey oynamaz.</param>
    private WidgetWindow Open(WidgetConfig config, WidgetPlacement? place = null, bool fadeIn = false)
    {
        var view = WidgetViews.Create(config);
        var window = new WidgetWindow(config, view) { PendingPlacement = place };
        window.Closed += (_, _) => OnWindowClosed(config);
        if (PerfLog.Enabled && !_firstRendered) window.ContentRendered += OnFirstRendered;
        _open[config.Id] = window;
        // Oluşurken etkinleşip kullanıcının yazdığı pencereden odağı almasın (bkz. QuietShow). Pencere henüz gösterilmediği
        // için alanı kayıtlı boyuttan tahmin edilir (büyük kart yalnızca solar).
        if (Hidden) return window;
        if (fadeIn) Views.Motion.Pop(window, () => QuietShow.Show(window), window.Surface, MotionSurface.LayeredWidget, window.PixelArea);
        else Views.Motion.Appear(window, () => QuietShow.Show(window), fade: false);
        return window;
    }

    // Başarım işareti (yalnızca NESTDESK_PERF_LOG açıkken): ilk çizilen widget.
    private bool _firstRendered;

    private void OnFirstRendered(object? sender, EventArgs e)
    {
        if (sender is Window window) window.ContentRendered -= OnFirstRendered;
        if (_firstRendered) return;
        _firstRendered = true;
        PerfLog.Mark("Widget.firstRendered");
    }

    private void OnWindowClosed(WidgetConfig config)
    {
        if (_closingOnPurpose.Remove(config.Id) || _shuttingDown) return;
        _open.Remove(config.Id);

        // Beklenmedik kapanma (ör. Alt+F4): kısa süre sonra geri aç; widget ayarlardan silinmedikçe kaybolmasın.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (!_shuttingDown && AppHost.Settings.Widgets.Contains(config) && !_open.ContainsKey(config.Id)) TryOpen(config);
        };
        timer.Start();
    }
}
