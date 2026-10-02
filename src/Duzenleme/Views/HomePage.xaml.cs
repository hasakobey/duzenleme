using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

/// <summary>
/// Ana sayfa (sakin): selam, iki büyük düğme ("Widget ekle", "Halkayı düzenle"), ilk ziyarette "Nasıl kullanılır"
/// (<see cref="HowToCards"/>), tek satırlık otomatik taşıma kartı (durum, "Geri al", "Dosyaları şimdi taşı", anahtar) ve son
/// üç taşıma. Açılışı hafif tutulur: klasörlerin içi sayılmaz, yalnızca masaüstündeki klasör adlarına bakılır.
/// </summary>
public partial class HomePage : Page
{
    private const int RecentCount = 3;

    /// <summary>Otomatik taşıma satırının düğmeleri bu genişliğin altında (DIP) metnin altına iner.</summary>
    private const double NarrowRow = 600;

    // Oluşturulmadan ilk Loaded'a dek geçen süre (NESTDESK_DEBUGLOG açıksa günlüğe yazılır).
    private readonly Stopwatch _firstLoad = Stopwatch.StartNew();
    private int _journalQueued;
    /// <summary>"Anladım"dan sonra şerit solarken: kayıt bildirimi onu yarıda kesip hemen kaldırmasın.</summary>
    private bool _howToLeaving;
    /// <summary>Şeridin kurulduğu çift tıklama etkisi: Halka kartı ona göre yazılır; ayar değişince şerit yeniden kurulur.</summary>
    private DoubleClickEffect? _howToFor;

    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Refresh();
            if (_firstLoad.IsRunning)
            {
                _firstLoad.Stop();
                DebugLog.Write($"ana sayfa hazır: {_firstLoad.ElapsedMilliseconds} ms");
            }
        };
        // Ana pencere gizliyken (kapatınca tepsiye iner) sayfa kayıtlara, taşımalara ve widget'lara tepki vermez; açılınca bir
        // kez yenilenir.
        PageLife.WhileShown(this,
            attach: () =>
            {
                AppHost.SettingsChanged += OnSettingsChanged;
                AppHost.Journal.Changed += OnJournalChanged;
                // Masaüstünde kural klasörü açılınca/silinince "hedef klasör yok" hemen değişsin (anlık görüntüden, ucuz).
                AppHost.DesktopSnapshot.Changed += RefreshCard;
                // Halka eklenince/kaldırılınca ikinci büyük düğme "Halkayı düzenle" / "Halka menü ekle" olur.
                AppHost.Widgets.Changed += RefreshRingTile;
            },
            detach: () =>
            {
                AppHost.SettingsChanged -= OnSettingsChanged;
                AppHost.Journal.Changed -= OnJournalChanged;
                AppHost.DesktopSnapshot.Changed -= RefreshCard;
                AppHost.Widgets.Changed -= RefreshRingTile;
            },
            refresh: Refresh);
    }

    /// <summary>
    /// Geçmiş değişti (taşıyıcının iş parçacığından da gelir). Toplu taşımada art arda gelen bildirimler tek yenilemede
    /// birleşir.
    /// </summary>
    private void OnJournalChanged()
    {
        if (Interlocked.Exchange(ref _journalQueued, 1) == 1) return;
        Dispatcher.BeginInvoke(() =>
        {
            Volatile.Write(ref _journalQueued, 0);
            if (IsLoaded) RefreshMoves();
        }, DispatcherPriority.Background);
    }

    // Ayarlar her kaydedildiğinde (widget taşımak da kaydeder) çağrılır: yalnızca ucuz parçalar.
    private void OnSettingsChanged()
    {
        RefreshCard();
        RefreshRingTile();
        RefreshHowTo();
    }

    private void Refresh()
    {
        // Pencere saatlerce açık kalabilir: selam günün saatine göre güncel kalsın ("Günaydın" gece de görünmesin).
        Hello.Text = ClockView.Greeting(DateTime.Now.Hour);
        RefreshRingTile();
        // Şerit solarken sayfadan çıkıldıysa (ya da pencere gizlendiyse) solma yarıda kalmış olabilir: bitmiş sayılır.
        if (_howToLeaving)
        {
            Motion.Stop(HowToHost);
            _howToLeaving = false;
        }
        RefreshHowTo();
        RefreshMoves();
    }

    private void RefreshMoves()
    {
        RefreshCard();
        var recent = AppHost.Journal.Snapshot().Take(RecentCount).Select(e => new MoveRow(e)).ToList();
        Recent.ItemsSource = recent;
        RecentEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SeeAllButton.Visibility = recent.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- Büyük düğmeler ----

    // Her ekleme yolu aynı "Widget ekle" penceresini açar (tepsi, Ctrl+Alt+B, halka, Widget'lar sayfası, Başlat menüsü).
    private void AddWidget_Click(object sender, RoutedEventArgs e) => (Application.Current as App)?.ShowQuickAdd();

    private void EditRing_Click(object sender, RoutedEventArgs e)
    {
        HomeRing.Edit(WindowCenter());
        RefreshRingTile();
    }

    private void RefreshRingTile()
    {
        var hasRing = HomeRing.Target() is not null;
        RingTileTitle.Text = hasRing ? L.T("Halkayı düzenle") : L.T("Halka menü ekle");
        RingTileText.Text = hasRing ? L.T("Halkadaki uygulamalar") : L.T("Uygulamalar bir halkada");
        AutomationProperties.SetName(RingTile, RingTileTitle.Text);
    }

    /// <summary>Ana pencerenin ortası (fiziksel piksel): yeni halka bu ekrana eklenir.</summary>
    private NativeMethods.POINT? WindowCenter()
    {
        if (Window.GetWindow(this) is not { } window) return null;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var r)) return null;
        return new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 };
    }

    // ---- Nasıl kullanılır ----

    private void RefreshHowTo()
    {
        if (HowToCards.ShouldShow(AppHost.Settings))
        {
            if (_howToLeaving) return;
            var doubleClick = HowToCards.CurrentDoubleClick();
            if (HowToHost.Child is null || _howToFor != doubleClick)
            {
                HowToHost.Child = HowToCards.Create(doubleClick, OnHowToDismissed);
                _howToFor = doubleClick;
            }
            HowToHost.Visibility = Visibility.Visible;
        }
        else if (!_howToLeaving)
        {
            HowToHost.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>"Anladım": şerit kısa bir solmayla kalkar (Views.Motion; kapalıysa hemen), odak "Widget ekle"ye geçer.</summary>
    private void OnHowToDismissed()
    {
        _howToLeaving = true;
        // Klavyeyle basıldıysa odak kaybolmasın: kalkan şeridin yerine ilk büyük düğmeye geçer.
        if (HowToHost.IsKeyboardFocusWithin) AddWidgetTile.Focus();
        Motion.Disappear(HowToHost, () =>
        {
            _howToLeaving = false;
            HowToHost.Visibility = Visibility.Collapsed;
            RefreshHowTo();
        });
    }

    // ---- Otomatik taşıma satırı ----

    private void RefreshCard()
    {
        var on = !AppHost.Settings.Paused;
        var last = AppHost.Journal.LastActive();
        var (text, setUp) = AutoMoveStatus.Line(on, AutoMoveStatus.ReadyRules(), last);
        AutoMoveToggle.IsChecked = on;
        StatusLine.Text = text;
        StatusIcon.Symbol = on ? SymbolRegular.FolderArrowRight24 : SymbolRegular.Pause24;
        StatusIcon.SetResourceReference(IconElement.ForegroundProperty, on ? "AccentTextFillColorPrimaryBrush" : "TextFillColorTertiaryBrush");
        SetUpFolders.Visibility = setUp ? Visibility.Visible : Visibility.Collapsed;
        // Hedef klasör yokken "Dosyaları şimdi taşı" bir şey taşımaz ("Klasör yoksa oluştur" kapalıysa): yerini "Klasörleri
        // ayarla" alır, satırda en çok iki düğme ve anahtar kalır.
        OrganizeButton.Visibility = setUp && !AppHost.Settings.CreateMissingFolders ? Visibility.Collapsed : Visibility.Visible;
        // "Geri al" yalnızca geri alınacak taşıma varken; ekran okuyucu hangi dosyanın döneceğini duyar.
        UndoButton.Visibility = last is null ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(UndoButton, last is null ? L.T("Geri al") : L.F("Geri al: {0}", last.FileName));
    }

    /// <summary>Dar pencerede düğmeler durum metninin altına iner (metin kısalıp okunmaz olmasın); yerleşim değişir, hiçbir şey oynamaz.</summary>
    private void AutoMoveRow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < NarrowRow;
        if (narrow == (Grid.GetRow(AutoMoveActions) == 1)) return;
        Grid.SetRow(AutoMoveActions, narrow ? 1 : 0);
        Grid.SetColumn(AutoMoveActions, narrow ? 1 : 2);
        Grid.SetColumnSpan(AutoMoveActions, narrow ? 3 : 1);
        AutoMoveActions.Margin = narrow ? new Thickness(0, 10, 0, 0) : new Thickness(12, 0, 0, 0);
        AutoMoveActions.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
    }

    // Checked/Unchecked (Click değil): ekran okuyucunun "Aç/Kapat"ı (UI Automation Toggle) da uygulasın. Koddan gösterilen
    // durum ayarla zaten aynıdır: yalnızca kullanıcının değişikliği taşımayı açar/kapatır.
    private void AutoMoveToggle_Changed(object sender, RoutedEventArgs e)
    {
        var on = AutoMoveToggle.IsChecked == true;
        if (on != !AppHost.Settings.Paused) AppHost.SetPaused(!on);
    }

    private async void OrganizeNow_Click(object sender, RoutedEventArgs e)
    {
        OrganizeButton.IsEnabled = false;
        try { await MoveActions.OrganizeNowAsync(); }
        finally { OrganizeButton.IsEnabled = true; }
    }

    private void UndoLast_Click(object sender, RoutedEventArgs e) => MoveActions.Undo(AppHost.Journal.LastActive());

    private void SetUpFolders_Click(object sender, RoutedEventArgs e) => (Window.GetWindow(this) as MainWindow)?.NavigateTo(typeof(AutoMovePage));

    private void SeeAll_Click(object sender, RoutedEventArgs e) => AutoMovePage.ShowHistory();
}

/// <summary>
/// "Halkayı düzenle"nin tek giriş noktası (Ana sayfanın ikinci büyük düğmesi; Ayarlar'daki ve halkanın altındaki "Halkayı
/// düzenle" de buraya bağlanır). Hedef kısayolun açtığı halka, yoksa ilk halka (<see cref="RingTargets.Pick"/>). Halka
/// düzenleyicisi gelene dek düzenleme halkanın kendi menüsündedir: halka öne gelip bir kez nabız atar ve şerit sağ tıklamayı
/// söyler. Halka yoksa "Halka menü ekle": "Widget ekle"deki kutucukla aynı yoldan ("{0} eklendi." + "Geri al"). Düzenleyici
/// (RingEditor) geldiğinde yalnızca <see cref="Edit"/>'in ilk dalı onu açacak biçimde değişir.
/// </summary>
internal static class HomeRing
{
    /// <summary>Düzenlenecek halka widget'ı; hiç halka yoksa null.</summary>
    public static WidgetConfig? Target() => RingTargets.Pick(AppHost.Settings.Widgets, AppHost.Settings.RingWidgetId);

    /// <param name="near">Yeni halkanın ekleneceği ekrandaki nokta (fiziksel piksel; ana pencerenin ortası).</param>
    public static void Edit(NativeMethods.POINT? near)
    {
        if (Target() is { } ring)
        {
            AppHost.Widgets.Reveal(ring.Id);
            Notice.Show(L.T("Halkan öne geldi. Uygulama eklemek ya da çıkarmak için ona sağ tıkla."), NoticeKind.Info);
            return;
        }
        if (WidgetCatalog.Tools.FirstOrDefault(c => c.Key == "Ring") is { } choice) WidgetCatalog.AddAndAnnounce(choice, near);
    }
}
