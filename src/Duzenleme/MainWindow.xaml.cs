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
    private Action? _noticeAction;

    public MainWindow()
    {
        InitializeComponent();
        Title = AppInfo.Name;
        TitleBar.Title = AppInfo.Name;
        Views.WindowFit.Attach(this);
        Loaded += (_, _) =>
        {
            RootNavigation.Navigate(_pendingPage ?? typeof(HomePage));
            _pendingPage = null;
        };
        // Bildirim o sayfanın işine aittir: sayfa değişince ya da pencere gizlenince kalkar.
        RootNavigation.Navigated += (_, _) => HideNotice();
        IsVisibleChanged += (_, e) => { if (e.NewValue is false) HideNotice(); };
    }

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
    internal void ShowNotice(string text, NoticeKind kind, string? actionText, Action? action)
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
        _noticeAction = actionText is null ? null : action;
        NoticeAction.Content = actionText;
        NoticeAction.Visibility = _noticeAction is null ? Visibility.Collapsed : Visibility.Visible;
        NoticeBar.Visibility = Visibility.Visible;
    }

    internal void HideNotice()
    {
        _noticeAction = null;
        NoticeBar.Visibility = Visibility.Collapsed;
    }

    private void NoticeClose_Click(object sender, RoutedEventArgs e) => HideNotice();

    private void NoticeAction_Click(object sender, RoutedEventArgs e)
    {
        // Önce şerit kapanır: eylem yeni bir bildirim gösterirse (ör. "Geri alındı") o kalsın.
        var action = _noticeAction;
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
                AppHost.Tray?.Notify($"{AppInfo.Name} arka planda çalışıyor",
                    $"Widget'lar ve otomatik taşıma çalışmaya devam ediyor. Açmak için saatin yanındaki {AppInfo.Name} simgesine tıkla.");
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
