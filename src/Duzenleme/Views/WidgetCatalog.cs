using System.Windows;
using System.Windows.Controls;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>Ekleme yüzeylerindeki bölüm (kalıcı değil): Bölmeler, Araçlar (not, liste, kutu, zamanlayıcılar), Saat ve bilgi.</summary>
internal enum WidgetGroup { Fence, Tool, Info }

/// <summary>
/// Eklenebilecek bir widget. Key: AutomationId'nin ("Add." + Key) ve karşılamadaki seçimin anahtarı. Add: widget'ı ekler
/// (klasör açılamazsa ya da kullanıcı vazgeçerse null). Subtitle: kutucukta adın altında görünen kısa açıklama (Tip ise
/// ekran okuyucunun yardım metni). Matches: masaüstündeki bir widget bu seçimin türünden mi? ShowInWelcome: karşılamanın 3.
/// adımında da gösterilir (yalnızca ilk beş araç).
/// </summary>
internal sealed record WidgetChoice(string Key, string Label, SymbolRegular Icon, string Tip, WidgetGroup Group,
    Func<WidgetConfig?> Add, bool FocusAfterAdd = false, string? Subtitle = null, Func<WidgetConfig, bool>? Matches = null,
    bool ShowInWelcome = false, bool AsksName = false);

/// <summary>
/// Bütün ekleme yüzeylerinin tek kaynağı: "Widget ekle" penceresi (tepsi, Ctrl+Alt+B, halka, ana pencere, Başlat menüsü) ve
/// karşılama. Pencerenin ilk sayfası <see cref="Featured"/> (8), "Hepsini göster" <see cref="More"/> (6); "Bölme ›" kendi
/// sayfasını açar (<see cref="DesktopFences"/>, <see cref="FolderFences"/>).
/// </summary>
internal static class WidgetCatalog
{
    /// <summary>Kutucuğu gösteren yüzeyin ilk açılışta gösterdiği klasör bölmesi sayısı; kalanlar "Diğer klasörler" ile açılır.</summary>
    public const int VisibleFolderCount = 8;

    /// <summary>
    /// Bölme dışındaki bütün widget'lar. İlk beşi karşılamada da vardır (bu sırayla). Metinler arayüz dilinde: her çağrıda kurulur.
    /// </summary>
    public static IReadOnlyList<WidgetChoice> Tools =>
    [
        new("Clock", L.T("Saat"), SymbolRegular.Clock24, L.T("Büyük dijital saat"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.Clock), Subtitle: L.T("Büyük dijital saat"),
            Matches: c => c.Kind == WidgetKind.Clock && WidgetVariants.Of(c) is null, ShowInWelcome: true),
        new("Date", L.T("Tarih"), SymbolRegular.CalendarLtr24, L.T("Gün, ay ve haftalık şerit"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.Date), Subtitle: L.T("Gün ve haftalık şerit"),
            Matches: c => c.Kind == WidgetKind.Date && WidgetVariants.Of(c) is null, ShowInWelcome: true),
        new("Note", L.T("Not"), SymbolRegular.Note24, L.T("Yapışkan not; yazdıkça kaydedilir"), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Note), FocusAfterAdd: true, Subtitle: L.T("Yazdıkça kaydedilir"),
            Matches: c => c.Kind == WidgetKind.Note && !c.NoteChecklist, ShowInWelcome: true),
        new("Checklist", L.T("Yapılacaklar"), SymbolRegular.TaskListLtr24, L.T("Onay kutulu liste; işaretledikçe kaydedilir"), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Checklist), FocusAfterAdd: true, Subtitle: L.T("Onay kutulu liste"),
            Matches: c => c.Kind == WidgetKind.Note && c.NoteChecklist, ShowInWelcome: true),
        new("Launcher", L.T("Kısayol kutusu"), SymbolRegular.AppsAddIn24, L.T("Sekmeli uygulama rafı; tek tıkla açar"), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Launcher), Subtitle: L.T("Uygulamalar tek tıkta"),
            Matches: c => c.Kind == WidgetKind.Launcher && WidgetVariants.Of(c) is null, ShowInWelcome: true),
        new("Ring", L.T("Halka menü"), SymbolRegular.DataPie24,
            L.T("Uygulamaların bir halkada; tıkla ya da 1–9'a bas. İstersen kısayolla imlecin yanında açılır."), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Ring), Subtitle: L.T("Uygulamalar bir halkada"), Matches: c => WidgetVariants.Is(c, WidgetVariants.Ring)),
        new("Calendar", L.T("Takvim"), SymbolRegular.CalendarMonth24, L.T("Aylık takvim; ay ay gezinilir"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.Calendar), Subtitle: L.T("Aylık takvim"), Matches: c => WidgetVariants.Is(c, WidgetVariants.Month)),
        new("Countdown", L.T("Geri sayım"), SymbolRegular.CalendarStar24, L.T("Bir güne kaç gün kaldı (tatil, doğum günü…)"), WidgetGroup.Tool,
            AddCountdown, Subtitle: L.T("Önemli güne kaç gün"), Matches: c => WidgetVariants.Is(c, WidgetVariants.Countdown), AsksName: true),
        new("Timer", L.T("Zamanlayıcı"), SymbolRegular.Timer24, L.T("Geri sayan zamanlayıcı; süre dolunca haber verir. Kronometre de olur."), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Timer), Subtitle: L.T("Pomodoro ve kronometre de"),
            Matches: c => WidgetVariants.Is(c, WidgetVariants.Timer) && TimerModes.Normalize(c.Timer?.Mode) != TimerModes.Pomodoro),
        new("Pomodoro", L.T("Pomodoro"), SymbolRegular.ClockAlarm24, L.T("25 dakika odak, 5 dakika mola; dört turda bir uzun mola"), WidgetGroup.Tool,
            () => Seed(WidgetSeeds.Pomodoro), Subtitle: L.T("Odak ve mola"),
            Matches: c => WidgetVariants.Is(c, WidgetVariants.Timer) && TimerModes.Normalize(c.Timer?.Mode) == TimerModes.Pomodoro),
        new("WorldClock", L.T("Dünya saati"), SymbolRegular.GlobeClock24, L.T("Başka şehirlerde saat kaç, kaç saat fark var"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.WorldClock), Subtitle: L.T("Başka şehirlerde saat"), Matches: c => WidgetVariants.Is(c, WidgetVariants.World)),
        new("SystemStatus", L.T("Sistem durumu"), SymbolRegular.Gauge24, L.T("İşlemci, bellek, disk ve pil"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.SystemStatus), Subtitle: L.T("İşlemci, bellek, disk"), Matches: c => WidgetVariants.Is(c, WidgetVariants.System)),
        new("RecycleBin", L.T("Geri Dönüşüm Kutusu"), SymbolRegular.Delete24, L.T("Kaç öğe var, ne kadar yer tutuyor; boşaltmadan önce sorar"), WidgetGroup.Info,
            () => Seed(WidgetSeeds.RecycleBin), Subtitle: L.T("Tek tıkla açılır"), Matches: c => WidgetVariants.Is(c, WidgetVariants.Recycle)),
    ];

    /// <summary>"Bölme ›" kutucuğunun anahtarı (AutomationId "Add.Fence"): "Widget ekle" penceresinde bölme sayfasını açar.</summary>
    public const string FenceKey = "Fence";

    /// <summary>İlk sayfadaki araçların anahtarları, sırasıyla (ardından "Bölme ›").</summary>
    public static IReadOnlyList<string> FeaturedKeys => ["Clock", "Date", "Note", "Checklist", "Calendar", "Launcher", "Timer"];

    /// <summary>"Hepsini göster" ile açılan araçların anahtarları, sırasıyla.</summary>
    public static IReadOnlyList<string> MoreKeys => ["WorldClock", "Countdown", "Pomodoro", "SystemStatus", "RecycleBin", "Ring"];

    /// <summary>"Widget ekle"nin ilk sayfası, bu sırayla (4 × 2): sık kullanılan yedi araç ve bölme sayfasını açan "Bölme ›".</summary>
    public static IReadOnlyList<WidgetChoice> Featured
    {
        get
        {
            var tools = Tools;
            var list = FeaturedKeys.Select(key => tools.Single(c => c.Key == key)).ToList();
            list.Add(new WidgetChoice(FenceKey, L.T("Bölme ›"), SymbolRegular.Board24,
                L.T("Masaüstündeki öğeleri ya da bir klasörün içini gösterir"), WidgetGroup.Fence, () => null,
                Subtitle: L.T("Dosyalarını topla")));
            return list;
        }
    }

    /// <summary>"Hepsini göster ({0})" ile ilk sayfanın altında açılan araçlar.</summary>
    public static IReadOnlyList<WidgetChoice> More
    {
        get
        {
            var tools = Tools;
            return MoreKeys.Select(key => tools.Single(c => c.Key == key)).ToList();
        }
    }

    /// <summary>"Yeni bölme…" kutucuğunun anahtarı (AutomationId "Add.NewFence"): ad, kaynak ve simge soran pencereyi açar.</summary>
    public const string NewFenceKey = "NewFence";

    /// <summary>Karşılamada gösterilen araçlar (Saat, Tarih, Not, Yapılacaklar, Kısayol kutusu).</summary>
    public static IEnumerable<WidgetChoice> WelcomeTools => Tools.Where(c => c.ShowInWelcome);

    private static WidgetConfig? Seed(string key) => WidgetSeeds.Create(key) is { } seed ? AppHost.Widgets.AddSeed(seed) : null;

    /// <summary>Geri sayım: önce etkinliğin adı ve günü sorulur; vazgeçilirse eklenmez.</summary>
    private static WidgetConfig? AddCountdown()
    {
        var seed = WidgetSeeds.Create(WidgetSeeds.Countdown)!;
        if (CountdownDialog.Ask(null, seed.TargetDate ?? DateTime.Today, false, AppHost.Widgets.PlacementHint?.Anchor) is not { } choice)
            return null;
        seed.Title = choice.Title;
        seed.TargetDate = choice.Date;
        seed.CountdownYearly = choice.Yearly;
        return AppHost.Widgets.AddSeed(seed);
    }

    /// <summary>
    /// "Yeni bölme…" seçimi (AutomationId "Add.NewFence"): adı, kaynağı ve simgesi kendi penceresinde sorulur. Bölme sayfasının
    /// altındaki küçük bağlantı.
    /// </summary>
    public static WidgetChoice NewFence => new(NewFenceKey, L.T("Yeni bölme…"), SymbolRegular.AddSquare24,
        L.T("Adını, ne göstereceğini ve simgesini seçerek bölme ekle"), WidgetGroup.Fence, () => NewFenceDialog.Ask(null), AsksName: true);

    /// <summary>Bölme sayfasının "Masaüstünden" bölümü: Klasörler, Kısayollar, Dosyalar, Tüm masaüstü.</summary>
    public static List<WidgetChoice> DesktopFences() =>
    [
        Filter("Folders", DesktopFilter.Folders, L.T("Klasörler"), SymbolRegular.Folder24, L.T("Masaüstündeki klasörler")),
        Filter("Shortcuts", DesktopFilter.Shortcuts, L.T("Kısayollar"), SymbolRegular.Apps24,
            L.T("Uygulama kısayolları, Bu Bilgisayar, Geri Dönüşüm Kutusu")),
        Filter("Files", DesktopFilter.Files, L.T("Dosyalar"), SymbolRegular.DocumentMultiple24, L.T("Masaüstünde duran dosyalar")),
        Filter("All", DesktopFilter.All, L.T("Tüm masaüstü"), SymbolRegular.Desktop24, L.T("Masaüstündeki her şey tek bölmede")),
    ];

    /// <summary>
    /// Bölme sayfasının "Bir klasörü göster" bölümü: masaüstündeki klasörler (<see cref="WidgetManager.FolderFenceChoices"/>
    /// sırasıyla), Windows'un İndirilenler, Belgeler ve Resimler klasörleri, masaüstünde henüz olmayan kural klasörleri
    /// ("PDF (oluşturulur)": eklenince açılır), en sonda "Başka klasör…". Rozet yok: Windows klasörüyle aynı adlı masaüstü
    /// klasörü "(masaüstü)" ekiyle ayrılır; aynı adlı olup masaüstünde olmayan kural klasörü hiç gösterilmez (yanlışlıkla
    /// masaüstünde yeni klasör açılmasın; o kural klasörü Otomatik taşıma sayfasından açılabilir). Her çağrıda yeniden kurulur.
    /// </summary>
    public static List<WidgetChoice> FolderFences()
    {
        var known = new List<WidgetChoice>();
        var knownNames = new List<string>();
        foreach (var id in new[] { FolderPortal.Downloads, FolderPortal.Documents, FolderPortal.Pictures })
        {
            if (KnownFolders.PathOf(id) is not { } path) continue;
            known.Add(KnownPortal(id, path));
            knownNames.Add(FolderPortal.KnownName(id));
        }
        var existing = new List<WidgetChoice>();
        var missing = new List<WidgetChoice>();
        foreach (var (name, exists) in AppHost.Widgets.FolderFenceChoices())
        {
            var sameAsKnown = knownNames.Any(k => FolderName.Equal(k, name));
            if (sameAsKnown && !exists) continue;
            var icon = name.Equals("PDF", StringComparison.OrdinalIgnoreCase) ? SymbolRegular.DocumentPdf24 : SymbolRegular.FolderOpen24;
            var label = !exists ? L.F("{0} (oluşturulur)", name) : sameAsKnown ? L.F("{0} (masaüstü)", name) : name;
            var tip = exists
                ? L.F("\"{0}\" klasörünün içi", name)
                : L.F("Masaüstünde \"{0}\" klasörü açılır; otomatik taşıma açıksa uygun dosyalar oraya taşınır", name);
            (exists ? existing : missing).Add(new WidgetChoice(FolderKeyPrefix + name, label, icon, tip, WidgetGroup.Fence,
                () => AppHost.Widgets.AddFolderFence(name),
                Matches: c => c.Kind == WidgetKind.Fence && c.Filter == DesktopFilter.None && FolderName.Equal(c.FolderName ?? "", name)));
        }
        var picked = new WidgetChoice("Portal:Pick", L.T("Başka klasör…"), SymbolRegular.FolderLink24,
            L.T("Bilgisayardaki herhangi bir klasörü bölmede göster (masaüstünde olması gerekmez)"), WidgetGroup.Fence, AddPickedFolder);
        return [.. existing, .. known, .. missing, picked];
    }

    /// <summary>Masaüstündeki (ya da eklenince açılacak) klasörün bölmesini ekleyen seçimlerin anahtar öneki.</summary>
    public const string FolderKeyPrefix = "Folder:";

    private static WidgetChoice Filter(string key, DesktopFilter filter, string label, SymbolRegular icon, string tip) =>
        new(key, label, icon, tip, WidgetGroup.Fence, () => AppHost.Widgets.AddFence(filter),
            Matches: c => c.Kind == WidgetKind.Fence && c.Filter == filter);

    /// <summary>Bilinen klasörün (İndirilenler…) portalı. Klasör masaüstündeyse klasik klasör bölmesi olur.</summary>
    private static WidgetChoice KnownPortal(string id, string path) =>
        new("Portal:" + id, FolderPortal.KnownName(id), KnownIcon(id), L.F("{0} klasörünün içi, en yeni üstte", FolderPortal.KnownName(id)),
            WidgetGroup.Fence, () => AddFolder(path, id),
            Matches: c => c.Kind == WidgetKind.Fence && c.Filter == DesktopFilter.None && c.FolderName is { } f && FolderPortal.SamePath(f, path));

    /// <summary>"Başka klasör…": klasör seçtirir; vazgeçilirse eklenmez.</summary>
    private static WidgetConfig? AddPickedFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L.T("Bölmede gösterilecek klasörü seç"),
            InitialDirectory = KnownFolders.PathOf(FolderPortal.Documents) ?? AppHost.DesktopDirectory,
        };
        return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName) ? AddFolder(dialog.FolderName, null) : null;
    }

    /// <summary>
    /// Klasörü gösteren bölme ekler: masaüstünün kendisi "Tüm masaüstü", masaüstündeki klasör klasik klasör bölmesi, başka her
    /// yer portal (başlık klasörün adı; 2.0 da başlığı gösterir). Portal kutucukları ve "Yeni bölme…" aynı yoldan ekler.
    /// </summary>
    internal static WidgetConfig? AddFolder(string path, string? knownId)
    {
        var source = FolderPortal.Normalize(path, AppHost.DesktopDirectory);
        if (source.Filter != DesktopFilter.None) return AppHost.Widgets.AddFence(source.Filter);
        var folder = source.FolderName!;
        if (!System.IO.Path.IsPathFullyQualified(folder)) return AppHost.Widgets.AddFolderFence(folder);
        var title = knownId is not null ? FolderPortal.KnownName(knownId) : FolderPortal.DisplayName(folder);
        return AppHost.Widgets.AddSeed(WidgetSeeds.Portal(folder, knownId, title));
    }

    private static SymbolRegular KnownIcon(string? id) => WidgetIcons.ForKnownFolder(id);

    /// <summary>
    /// Kutucuk: 26 px simge, ad ve (varsa) altında görünen kısa açıklama (<see cref="WidgetChoice.Subtitle"/>; ikincil renkte, en
    /// çok iki satır). Açıklamalı kutucuk 128×100, açıklamasız 128×82 (adı iki satıra geçebilir). AutomationId "Add." + Key;
    /// AutomationProperties.Name = Label (sondaki "›" hariç); HelpText = Tip (ipucu yok: açıklama görünür). Basınca içerik
    /// hafifçe küçülür (<see cref="Motion.PressProperty"/>).
    /// </summary>
    public static Button Tile(WidgetChoice choice, Action<WidgetChoice> onClick)
    {
        var content = new StackPanel();
        content.Children.Add(new SymbolIcon { Symbol = choice.Icon, FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center });
        var label = new TextBlock
        {
            Text = choice.Label, Margin = new Thickness(0, 6, 0, 0), TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 112,
        };
        content.Children.Add(label);
        if (choice.Subtitle is { } subtitle)
        {
            var line = new TextBlock
            {
                Text = subtitle, FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0), TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 112, MaxHeight = 32,
            };
            // Soluk yazı saydamlıkla değil renkle (WPF-UI'ın ikincil yazı rengi; tema değişince güncellenir).
            line.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            content.Children.Add(line);
        }
        else
        {
            // Açıklamasız kutucukta uzun ad ikinci satıra geçer ("Geri Dönüşüm Kutusu"). 40 DIP: 14 pt'lik iki satır.
            label.TextWrapping = TextWrapping.Wrap;
            label.MaxHeight = 40;
        }
        var button = new Button
        {
            Content = content, Width = 128, Height = choice.Subtitle is null ? 82 : 100, Margin = new Thickness(0, 0, 8, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 4, 6, 4),
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(button, "Add." + choice.Key);
        // "›" (sayfa açar) yalnızca görünür; ekran okuyucu onu okumasın ("Bölme ›" → "Bölme").
        System.Windows.Automation.AutomationProperties.SetName(button, choice.Label.TrimEnd('›').TrimEnd());
        System.Windows.Automation.AutomationProperties.SetHelpText(button, choice.Tip);
        // Basınca içerik hafifçe küçülür (yalnızca Canlı; kutucuğun yeri ve boyutu değişmez).
        Motion.SetPress(button, true);
        button.Click += (_, _) => onClick(choice);
        return button;
    }

    /// <summary>
    /// Kutucukları panele ekler. Klasör bölmelerinin ilk <see cref="VisibleFolderCount"/> tanesi görünür; kalanlar
    /// "Diğer klasörler ({n})" düğmesine basınca aynı yerde, animasyonsuz açılır.
    /// </summary>
    public static void AddTiles(Panel panel, IEnumerable<WidgetChoice> choices, Action<WidgetChoice> onClick)
    {
        var hidden = new List<UIElement>();
        var folders = 0;
        foreach (var choice in choices)
        {
            var tile = Tile(choice, onClick);
            if (choice.Key.StartsWith(FolderKeyPrefix, StringComparison.Ordinal) && ++folders > VisibleFolderCount)
            {
                tile.Visibility = Visibility.Collapsed;
                hidden.Add(tile);
            }
            panel.Children.Add(tile);
        }
        if (hidden.Count == 0) return;

        var more = new Button
        {
            Content = L.F("Diğer klasörler ({0})", hidden.Count), Height = 82, Margin = new Thickness(0, 0, 8, 8),
            Icon = new SymbolIcon { Symbol = SymbolRegular.ChevronDown24 },
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(more, "Add.MoreFolders");
        more.Click += (_, _) =>
        {
            foreach (var tile in hidden) tile.Visibility = Visibility.Visible;
            more.Visibility = Visibility.Collapsed;
        };
        // "Diğer klasörler" ilk gizli kutucuğun yerinde durur; açılınca kutucuklar onun yerine gelir.
        panel.Children.Insert(panel.Children.IndexOf(hidden[0]), more);
    }

    /// <summary>
    /// Seçimi ekler: near verilirse widget o noktanın ekranına yerleşir; followSetting ise ayardaki kiple ("Yeni widget'ların
    /// yeri": imleç, orta, köşe; "Widget ekle" penceresi kapanır, widget onun yerine gelir), değilse türüne göre köşeye
    /// (yalnızca karşılamanın toplu eklemesi). Yeni widget adlandırmaya açılmaz: not ve yapılacaklar (FocusAfterAdd) yazı
    /// alanıyla ya da ilk satırla açılır, ad sonradan F2 ile verilir. "Yeni bölme…" adı ve simgeyi kendi penceresinde sorar
    /// (near'ın monitöründe). Klasör açılamazsa ya da vazgeçilirse null.
    /// </summary>
    public static WidgetConfig? Invoke(WidgetChoice choice, NativeMethods.POINT? near, bool followSetting = true)
    {
        if (near is { } point)
            AppHost.Widgets.PlacementHint = followSetting ? WidgetPlacement.FromSettingsAt(point) : WidgetPlacement.CornerNear(point);
        var config = choice.Key == NewFenceKey ? NewFenceDialog.Ask(near) : choice.Add();
        if (config is null)
        {
            // Eklenemedi: yer ipucu sonraki (başka yerden eklenen) widget'a kalmasın.
            AppHost.Widgets.PlacementHint = null;
            return null;
        }
        if (choice.FocusAfterAdd) AppHost.Widgets.FocusNote(config.Id);
        return config;
    }

    /// <summary>
    /// "Widget ekle"deki bir kutucuk: widget'ı ekler ve söyler. Ana pencere açıksa şeritte "{0} eklendi." + "Geri al" (widget
    /// geri getirilebilir biçimde kalkar, <see cref="UndoAdd"/>; eklerken açılan klasör yerinde kalır); değilse ilk üç
    /// eklemede tepside nereye geldiği (<see cref="AppHost.ShowNewWidgetHint"/>). Yeni widget birkaç saniye vurgulu öne gelir (WidgetManager, RevealGlow).
    /// </summary>
    public static WidgetConfig? AddAndAnnounce(WidgetChoice choice, NativeMethods.POINT? near)
    {
        var folder = choice.Key.StartsWith(FolderKeyPrefix, StringComparison.Ordinal) ? choice.Key[FolderKeyPrefix.Length..] : null;
        // Klasör eklerken açılacak mı? Kutucuktaki ada değil anlık görüntüye bakılır (kutucuk kurulduktan sonra değişmiş olabilir).
        var creates = folder is not null && !AppHost.DesktopFolders().Any(f => FolderName.Equal(f, folder));
        if (Invoke(choice, near) is not { } config) return null;
        // Pencere açılamadıysa WidgetManager widget'ı geri çıkarıp uyardı: "eklendi" denmesin.
        if (!AppHost.Settings.Widgets.Any(w => w.Id == config.Id)) return null;
        if (!Notice.MainWindowShown)
        {
            AppHost.ShowNewWidgetHint(config);
            return config;
        }
        var name = choice.Group == WidgetGroup.Fence ? WidgetText.DisplayName(config) : choice.Label;
        var text = creates && folder is not null
            ? L.F("\"{0}\" bölmesi eklendi; masaüstünde \"{0}\" klasörü de oluşturuldu.", folder)
            : L.F("{0} eklendi.", name);
        var id = config.Id;
        Notice.Show(text, NoticeKind.Success, L.T("Geri al"), () => UndoAdd(id));
        return config;
    }

    /// <summary>
    /// "{0} eklendi."deki "Geri al": şerit uzun süre açık kalabilir ve widget o arada doldurulmuş olabilir (not, kutu), bu yüzden
    /// widget sıradan kaldırma gibi gider: ayarları ve içeriğiyle tutulur, yeni bildirimdeki "Geri al" (ya da tepsideki
    /// "Geri getir") onu geri getirir.
    /// </summary>
    private static void UndoAdd(string id)
    {
        if (AppHost.Settings.Widgets.FirstOrDefault(w => w.Id == id) is not { } config) return;
        var name = WidgetText.DisplayName(config); // kaldırmadan önce
        var returning = BoxMover.MovedOnlyIn(config);
        var modeOff = AppHost.Widgets.RemoveWithUndo(id, notify: false);
        Notice.Show(WidgetManager.RemovedText(L.F("{0} kaldırıldı.", name), returning, modeOff),
            NoticeKind.Info, L.T("Geri al"), () => AppHost.Widgets.UndoRemove(id));
    }
}
