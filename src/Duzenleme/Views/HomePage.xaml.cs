using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Ana sayfa: selam, üç ana eylem (Widget ekle, Şimdi düzenle, Son taşımayı geri al), otomatik taşıma durumu ve son üç
/// taşıma. Açılışı hafif tutulur: klasörlerin içi sayılmaz, yalnızca masaüstündeki klasör adlarına bakılır.
/// </summary>
public partial class HomePage : Page
{
    private const int RecentCount = 3;

    // Bugünkü renkler: açıkken mor, kapalıyken gri degrade.
    private static readonly Brush OnBrush = Frozen(new LinearGradientBrush(Color.FromRgb(0x63, 0x66, 0xF1), Color.FromRgb(0xA8, 0x55, 0xF7), 0));
    private static readonly Brush OffBrush = Frozen(new LinearGradientBrush(Color.FromRgb(0x4B, 0x4B, 0x57), Color.FromRgb(0x33, 0x33, 0x3D), 0));
    private static readonly Brush SoftWhite = Frozen(new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush IconBack = Frozen(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)));

    // Oluşturulmadan ilk Loaded'a dek geçen süre (DUZENLEME_DEBUGLOG açıksa günlüğe yazılır).
    private readonly Stopwatch _firstLoad = Stopwatch.StartNew();
    private int _journalQueued;

    public HomePage()
    {
        InitializeComponent();
        Tagline.Text = AppInfo.Tagline;
        Loaded += (_, _) =>
        {
            Hello.Text = Widgets.ClockView.Greeting(DateTime.Now.Hour);
            Refresh();
            if (_firstLoad.IsRunning)
            {
                _firstLoad.Stop();
                DebugLog.Write($"ana sayfa hazır: {_firstLoad.ElapsedMilliseconds} ms");
            }
        };
        // Ana pencere gizliyken (kapatınca tepsiye iner) sayfa kayıtlara ve taşımalara tepki vermez; açılınca bir kez yenilenir.
        PageLife.WhileShown(this,
            attach: () =>
            {
                AppHost.SettingsChanged += RefreshCard;
                AppHost.Journal.Changed += OnJournalChanged;
                // Masaüstünde kural klasörü açılınca/silinince "hazır klasör" sayısı hemen değişsin (anlık görüntüden, ucuz).
                AppHost.DesktopSnapshot.Changed += RefreshCard;
            },
            detach: () =>
            {
                AppHost.SettingsChanged -= RefreshCard;
                AppHost.Journal.Changed -= OnJournalChanged;
                AppHost.DesktopSnapshot.Changed -= RefreshCard;
            },
            refresh: () =>
            {
                Hello.Text = Widgets.ClockView.Greeting(DateTime.Now.Hour);
                Refresh();
            });
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
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
            if (IsLoaded) Refresh();
        }, DispatcherPriority.Background);
    }

    private void Refresh()
    {
        var entries = AppHost.Journal.Snapshot();
        RefreshCard(entries);
        RefreshUndoTile();

        var recent = entries.Take(RecentCount).Select(e => new MoveRow(e)).ToList();
        Recent.ItemsSource = recent;
        RecentEmpty.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SeeAllButton.Visibility = recent.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // Ayarlar her kaydedildiğinde (widget taşımak da kaydeder) çağrılır: yalnızca kart, ucuz.
    private void RefreshCard() => RefreshCard(AppHost.Journal.Snapshot());

    private void RefreshCard(IReadOnlyList<MoveEntry> entries)
    {
        // Pencere saatlerce açık kalabilir: selam günün saatine göre güncel kalsın ("Günaydın" gece de görünmesin).
        Hello.Text = Widgets.ClockView.Greeting(DateTime.Now.Hour);
        var on = !AppHost.Settings.Paused;
        var ready = AutoMoveStatus.ReadyRules();
        var moved = entries.Where(e => !e.Undone).ToList();
        var today = moved.Count(e => e.Time.Date == DateTime.Today);
        var (title, text) = AutoMoveStatus.Describe(on, ready, today, moved.Count);

        AutoMoveToggle.IsChecked = on;
        StatusTitle.Text = title;
        StatusText.Text = text;
        StatusIcon.Symbol = on ? SymbolRegular.FolderArrowRight24 : SymbolRegular.Pause24;
        SetUpFolders.Visibility = on && ready == 0 ? Visibility.Visible : Visibility.Collapsed;
        PaintCard(on);
    }

    /// <summary>Degrade kart; yüksek karşıtlıkta sistemin kart rengi ve metin renkleri (beyaz yazı okunmaz olmasın).</summary>
    private void PaintCard(bool on)
    {
        if (SystemParameters.HighContrast)
        {
            StatusCard.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
            StatusIconBack.Background = Brushes.Transparent;
            StatusIcon.SetResourceReference(IconElement.ForegroundProperty, "TextFillColorPrimaryBrush");
            StatusTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            return;
        }
        StatusCard.Background = on ? OnBrush : OffBrush;
        StatusIconBack.Background = IconBack;
        StatusIcon.Foreground = Brushes.White;
        StatusTitle.Foreground = Brushes.White;
        StatusText.Foreground = SoftWhite;
    }

    private void RefreshUndoTile()
    {
        var last = AppHost.Journal.LastActive();
        UndoTile.IsEnabled = last is not null;
        UndoDetail.Text = last is null ? L.T("Geri alınacak taşıma yok") : $"{last.FileName} → {last.FolderName}";
    }

    private void Go(Type page) => (Window.GetWindow(this) as MainWindow)?.NavigateTo(page);

    private void AddWidget_Click(object sender, RoutedEventArgs e) => Go(typeof(WidgetsPage));

    private async void OrganizeNow_Click(object sender, RoutedEventArgs e)
    {
        OrganizeTile.IsEnabled = false;
        try { await MoveActions.OrganizeNowAsync(); }
        finally { OrganizeTile.IsEnabled = true; }
    }

    private void UndoLast_Click(object sender, RoutedEventArgs e) => MoveActions.Undo(AppHost.Journal.LastActive());

    // Checked/Unchecked (Click değil): ekran okuyucunun "Aç/Kapat"ı (UI Automation Toggle) da uygulasın. Koddan gösterilen
    // durum ayarla zaten aynıdır: yalnızca kullanıcının değişikliği taşımayı açar/kapatır.
    private void AutoMoveToggle_Changed(object sender, RoutedEventArgs e)
    {
        var on = AutoMoveToggle.IsChecked == true;
        if (on != !AppHost.Settings.Paused) AppHost.SetPaused(!on);
    }

    private void SetUpFolders_Click(object sender, RoutedEventArgs e) => Go(typeof(AutoMovePage));

    private void SeeAll_Click(object sender, RoutedEventArgs e) => AutoMovePage.ShowHistory();
}
