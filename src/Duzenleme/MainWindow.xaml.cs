using System.ComponentModel;
using System.Windows;
using Duzenleme.Core;
using Duzenleme.Views;
using Wpf.Ui.Controls;

namespace Duzenleme;

public partial class MainWindow : FluentWindow
{
    private bool _reallyClose;
    private Type? _pendingPage;
    private IReadOnlyList<NoticeChoice> _noticeChoices = [];

    public MainWindow()
    {
        InitializeComponent();
        Title = AppInfo.Name;
        TitleBar.Title = AppInfo.Name;
        Views.WindowFit.Attach(this);
        // Sayfa geçişi: kısa solma ya da hiçbiri (Ayarlar > Animasyonlar ve Windows'un ayarı değişince hemen).
        Motion.ApplyTo(RootNavigation);
        Motion.Changed += OnMotionChanged;
        Closed += (_, _) => Motion.Changed -= OnMotionChanged;
        Loaded += (_, _) =>
        {
            RootNavigation.Navigate(_pendingPage ?? typeof(HomePage));
            _pendingPage = null;
        };
        // Bildirim o sayfanın işine aittir: sayfa değişince ya da pencere gizlenince kalkar.
        RootNavigation.Navigated += (_, _) => HideNotice();
        IsVisibleChanged += (_, e) => { if (e.NewValue is false) HideNotice(); };
    }

    private void OnMotionChanged() => Motion.ApplyTo(RootNavigation);

    /// <summary>Sayfaya geçer; pencere henüz yüklenmediyse yüklenince geçer.</summary>
    public void NavigateTo(Type page)
    {
        if (IsLoaded) RootNavigation.Navigate(page);
        else _pendingPage = page;
    }

    /// <summary>
    /// Pencerenin altında kalıcı bildirim şeridi: kapatılana, eylemine basılana, yenisi gelene, sayfa değişene ya da pencere
    /// gizlenene dek durur (kendiliğinden kaybolmaz). Doğrudan değil <see cref="Notice"/> üzerinden çağrılır.
    /// </summary>
    /// <param name="choices">0–3 düğme. Tek düğme metnin sağında; birden çoksa metnin altında.</param>
    /// <param name="primary">İlk düğme birincil görünümde (soru şeridi).</param>
    internal void ShowNotice(string text, NoticeKind kind, IReadOnlyList<NoticeChoice> choices, bool primary)
    {
        NoticeText.Text = text;
        var (symbol, brush) = kind switch
        {
            NoticeKind.Info => (SymbolRegular.Info24, "AccentTextFillColorPrimaryBrush"),
            NoticeKind.Warning => (SymbolRegular.Warning24, "SystemFillColorCautionBrush"),
            NoticeKind.Error => (SymbolRegular.ErrorCircle24, "SystemFillColorCriticalBrush"),
            _ => (SymbolRegular.CheckmarkCircle24, "SystemFillColorSuccessBrush"),
        };
        NoticeIcon.Symbol = symbol;
        NoticeIcon.SetResourceReference(ForegroundProperty, brush);
        _noticeChoices = choices;
        var buttons = new[] { NoticeAction, NoticeAction2, NoticeAction3 };
        for (var i = 0; i < buttons.Length; i++)
        {
            var choice = i < choices.Count ? choices[i] : null;
            buttons[i].Content = choice?.Text;
            buttons[i].Tag = i;
            buttons[i].Visibility = choice is null ? Visibility.Collapsed : Visibility.Visible;
            // Soru şeridinin ilk seçeneği birincil; öteki düğmeler (ve tek eylemli bildirim) eskisi gibi stilin varsayılanında.
            if (primary && i == 0) buttons[i].Appearance = ControlAppearance.Primary;
            else buttons[i].ClearValue(Wpf.Ui.Controls.Button.AppearanceProperty);
        }
        NoticeActions.Visibility = choices.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        // Birden çok seçenek metnin altında (uzun soru dar bir sütuna sıkışmasın); tek eylem eskisi gibi sağda.
        var below = choices.Count > 1;
        System.Windows.Controls.DockPanel.SetDock(NoticeActions, below ? System.Windows.Controls.Dock.Bottom : System.Windows.Controls.Dock.Right);
        NoticeActions.Margin = below ? new Thickness(0, 10, 0, 0) : new Thickness(12, 0, 4, 0);
        NoticeActions.HorizontalAlignment = below ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        // Belirirken kısa solma; açık şeritteki bildirim yerinde değişir (oynamaz).
        if (NoticeBar.Visibility != Visibility.Visible)
        {
            NoticeBar.Visibility = Visibility.Visible;
            Motion.FadeIn(NoticeBar);
        }
    }

    internal void HideNotice()
    {
        _noticeChoices = [];
        Motion.Stop(NoticeBar);
        NoticeBar.Visibility = Visibility.Collapsed;
    }

    private void NoticeClose_Click(object sender, RoutedEventArgs e) => HideNotice();

    private void NoticeAction_Click(object sender, RoutedEventArgs e)
    {
        // Önce şerit kapanır: eylem yeni bir bildirim gösterirse (ör. "Geri alındı") o kalsın.
        var index = (sender as FrameworkElement)?.Tag is int i ? i : 0;
        var action = index < _noticeChoices.Count ? _noticeChoices[index].Action : null;
        HideNotice();
        action?.Invoke();
    }

    /// <summary>Pencereyi kapatmak uygulamayı kapatmaz; tepside çalışmaya devam eder.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyClose)
        {
            e.Cancel = true;
            Hide();
            if (!AppHost.Settings.CloseToTrayHintShown)
            {
                // Kapatınca uygulamanın gittiği sanılmasın (bir kez söylenir).
                AppHost.Tray?.Notify(L.F("{0} arka planda çalışıyor", AppInfo.Name),
                    L.F("Widget'lar ve otomatik taşıma çalışmaya devam ediyor. Açmak için saatin yanındaki {0} simgesine tıkla.", AppInfo.Name));
                AppHost.Settings.CloseToTrayHintShown = true;
                AppHost.SaveSettings();
            }
            return;
        }
        base.OnClosing(e);
    }

    public void CloseForReal()
    {
        _reallyClose = true;
        Close();
    }
}
