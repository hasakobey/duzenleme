using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Duzenleme.Core;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Ana sayfadaki ilk ziyaret şeridi "Nasıl kullanılır": dört durağan kart (simge, tek sözcük, tek cümle; video, hareket ve
/// fareyle beliren öğe yok) ve "Anladım". "Anladım" <see cref="AppSettings.HowToDismissed"/>'ı yazar; şerit solarak kalkar
/// ve bir daha gösterilmez. <see cref="ShowAgain"/> yeniden açar (Ayarlar > Yardım > "Nasıl kullanılır" buna bağlanır).
/// </summary>
internal static class HowToCards
{
    /// <summary>Bir kart: <paramref name="Key"/> AutomationId'de ("Home.HowTo.move"), metinler arayüz dilinde.</summary>
    internal sealed record Card(string Key, SymbolRegular Icon, string Title, string Text);

    /// <summary>Şeridin dar düzene (iki sütun) geçtiği genişlik (DIP).</summary>
    private const double NarrowWidth = 560;

    /// <summary>
    /// Kartlar, sırasıyla. Metinler arayüz dilinde: her çağrıda kurulur. Yalnızca bugün gerçekten çalışan yollar yazılır:
    /// widget'larda ⋯ düğmesi yokken "Büyüt" ve "Düzenle" onu anmaz (⋯ gelince HomePageTests kartları güncelletir).
    /// </summary>
    /// <param name="doubleClick">Kullanıcının boş masaüstüne çift tıklamasının bugünkü etkisi (<see cref="CurrentDoubleClick"/>).</param>
    public static IReadOnlyList<Card> CardsFor(DoubleClickEffect doubleClick) =>
    [
        new("move", SymbolRegular.ArrowMove24, L.T("Taşı"), L.T("Başlığından ya da boş yerinden sürükle.")),
        new("resize", SymbolRegular.ResizeLarge24, L.T("Büyüt"), L.T("Kenarından sürükle.")),
        new("edit", SymbolRegular.Edit24, L.T("Düzenle"), L.T("Üzerine sağ tıkla.")),
        new("ring", SymbolRegular.DataPie24, L.T("Halka"), RingText(doubleClick)),
    ];

    /// <summary>
    /// Halka kartı halkaya bu kullanıcının bugün hangi yoldan ulaştığını söyler. Çift tıklamanın hiçbir seçeneği halkayı açmıyor:
    /// halka "Widget ekle" › "Hepsini göster" › "Halka menü" ile eklenir. <see cref="DoubleClickEffect"/>'e halka gelince
    /// yalnızca o seçenekte (kullanıcının gerçek ayarı; eski kullanıcı kendi seçimini korur) kart "Boş masaüstüne çift
    /// tıkla." der; HomePageTests her seçenek için bunu bekler.
    /// </summary>
    internal static string RingText(DoubleClickEffect doubleClick) => L.T("Widget ekle › Hepsini göster › Halka menü.");

    /// <summary>Boş masaüstüne çift tıklamanın bu kullanıcıdaki etkisi (masaüstü çift tıklamasıyla aynı çözüm).</summary>
    public static DoubleClickEffect CurrentDoubleClick()
    {
        var s = AppHost.Settings;
        return DesktopState.ResolveDoubleClick(s.DoubleClickAction, s.DoubleClickHidesDesktop, s.FencesReplaceIcons);
    }

    /// <summary>Şerit gösterilmeli mi (ilk ziyaret: "Anladım"a henüz basılmadı)?</summary>
    public static bool ShouldShow(AppSettings settings) => !settings.HowToDismissed;

    /// <summary>"Anladım": bir daha gösterilmez (kısa süre içinde kaydedilir).</summary>
    public static void Dismiss()
    {
        if (AppHost.Settings.HowToDismissed) return;
        AppHost.Settings.HowToDismissed = true;
        AppHost.SaveSettings();
    }

    /// <summary>Kartları yeniden gösterir ve Ana sayfayı açar (Ayarlar > Yardım > "Nasıl kullanılır").</summary>
    public static void ShowAgain()
    {
        if (AppHost.Settings.HowToDismissed)
        {
            AppHost.Settings.HowToDismissed = false;
            AppHost.SaveSettings();
        }
        (Application.Current as App)?.ShowPage(typeof(HomePage));
    }

    /// <summary>
    /// Şeridi kurar: başlık ve "Anladım" (<c>Home.HowTo.Dismiss</c>), altında kartlar (geniş pencerede dört, darda iki sütun).
    /// "Anladım" önce <see cref="Dismiss"/> der, sonra <paramref name="dismissed"/>'i çağırır (sayfa şeridi soldurup kaldırır).
    /// </summary>
    public static FrameworkElement Create(DoubleClickEffect doubleClick, Action dismissed)
    {
        var root = new StackPanel();
        AutomationProperties.SetAutomationId(root, "Home.HowTo");

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = true };
        var ok = new Button
        {
            Content = L.T("Anladım"),
            Icon = new SymbolIcon { Symbol = SymbolRegular.Checkmark24 },
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetAutomationId(ok, "Home.HowTo.Dismiss");
        AutomationProperties.SetHelpText(ok, L.T("Nasıl kullanılır kartlarını bir daha gösterme"));
        ok.Click += (_, _) =>
        {
            Dismiss();
            dismissed();
        };
        DockPanel.SetDock(ok, Dock.Right);
        header.Children.Add(ok);
        // Bölüm başlığı biçimi; kenar boşluğu yerel (stilin üst boşluğunu sayfa veriyor).
        var title = new TextBlock { Text = L.T("Nasıl kullanılır"), Margin = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        title.SetResourceReference(FrameworkElement.StyleProperty, "SectionHeader");
        header.Children.Add(title);
        root.Children.Add(header);

        var grid = new UniformGrid { Columns = 4, Margin = new Thickness(-4, 0, -4, 0) };
        foreach (var card in CardsFor(doubleClick)) grid.Children.Add(CardView(card));
        // Dar pencerede (en küçük ana pencere ~460 DIP içerik) kartlar ikişerli dizilir; yerleşim değişir, hiçbir şey oynamaz.
        grid.SizeChanged += (_, e) =>
        {
            var columns = e.NewSize.Width < NarrowWidth ? 2 : 4;
            if (grid.Columns != columns) grid.Columns = columns;
        };
        root.Children.Add(grid);
        return root;
    }

    private static Border CardView(Card card)
    {
        var border = new Border { Margin = new Thickness(4), Padding = new Thickness(14, 12, 14, 14) };
        border.SetResourceReference(FrameworkElement.StyleProperty, "Surface");
        AutomationProperties.SetAutomationId(border, "Home.HowTo." + card.Key);

        var panel = new StackPanel();
        var icon = new SymbolIcon { Symbol = card.Icon, FontSize = 22, HorizontalAlignment = HorizontalAlignment.Left };
        icon.SetResourceReference(IconElement.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
        panel.Children.Add(icon);
        var title = new TextBlock
        {
            Text = card.Title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 2),
            TextWrapping = TextWrapping.Wrap,
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        panel.Children.Add(title);
        var text = new TextBlock { Text = card.Text };
        text.SetResourceReference(FrameworkElement.StyleProperty, "Muted");
        panel.Children.Add(text);
        border.Child = panel;
        return border;
    }
}
