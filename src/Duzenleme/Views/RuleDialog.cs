using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Duzenleme.Core;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Duzenleme.Views;

/// <summary>
/// "Kural ekle" (iki adım) ve kural kartının "Değiştir…"i. Ekleme: "Hangi dosyalar?" (eksik hazır kurallar, Tasarım,
/// E-kitaplar: <see cref="RuleTemplates.Offers"/>; "Başka uzantı…" çip girişi) → "Nereye?" (önceden doldurulmuş
/// "Yeni klasör: [ad]" ya da masaüstündeki bir klasör) → "Ekle". Değiştirme tek sayfadır (uzantı çipleri + klasör).
/// Pencere yalnızca seçimi toplar; kapandıktan sonra <see cref="Apply"/> klasörü açar (MarkQuietFolder → TryCreate →
/// NoteFolderCreated), yeni kural listesini atar, kaydeder ve otomatik taşıma açıksa masaüstünü tarar. Sonuç bildirimde
/// "Geri al" ile geri alınır (açılan klasör yerinde kalır). Kip pencere; sahibinin (ana pencere) ortasında açılır.
/// </summary>
internal sealed class RuleDialog : FluentWindow
{
    private const double BodyWidth = 512;

    private readonly Rule? _editing;
    private readonly List<string> _desktopFolders;
    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;
    private readonly ContentControl _page;
    private readonly DockPanel _footer;
    private readonly TextBlock _error;

    private ExtensionChips? _chips;
    private FolderChoice? _folder;
    private List<string> _extensions = [];
    private string _suggested = "";
    private (Rule Rule, string Folder, bool Create)? _result;

    /// <summary>"+ Kural ekle": pencereyi gösterir ve seçilen kuralı ekler. Eklenen kural (vazgeçilirse null).</summary>
    public static Rule? Add(Window? owner)
    {
        var dialog = new RuleDialog(owner, null);
        return dialog.ShowDialog() == true && dialog._result is { } result ? Apply(null, result.Rule, result.Folder, result.Create) : null;
    }

    /// <summary>Kartın "Değiştir…"i: uzantılar ve klasör tek sayfada. Kuralın yeni hâli (vazgeçilir ya da değişmezse null).</summary>
    public static Rule? Edit(Window? owner, Rule rule)
    {
        var dialog = new RuleDialog(owner, rule);
        if (dialog.ShowDialog() != true || dialog._result is not { } result) return null;
        var unchanged = !result.Create && result.Rule.TargetFolder == rule.TargetFolder && result.Rule.Extensions.SequenceEqual(rule.Extensions);
        return unchanged ? null : Apply(rule, result.Rule, result.Folder, result.Create);
    }

    private RuleDialog(Window? owner, Rule? editing)
    {
        if (Application.Current?.MainWindow == this) Application.Current.MainWindow = null;
        _editing = editing;
        _desktopFolders = AppHost.DesktopFolders().OrderBy(n => n, L.Sorter).ToList();
        var title = editing is null ? L.T("Kural ekle") : L.T("Kuralı değiştir");
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.Mica;
        ShowInTaskbar = false;
        MinWidth = 0;
        MinHeight = 0;
        var usableOwner = owner is { IsVisible: true } ? owner : null;
        Owner = usableOwner;
        Topmost = usableOwner is null;
        WindowStartupLocation = usableOwner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        AutomationProperties.SetAutomationId(this, "Rule");
        WindowFit.FitChromeToContent(this);

        _title = new TextBlock { FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        _title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _subtitle = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        _subtitle.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var header = new StackPanel { Margin = new Thickness(22, 2, 22, 14), Width = BodyWidth };
        header.Children.Add(_title);
        header.Children.Add(_subtitle);

        _page = new ContentControl { Margin = new Thickness(22, 0, 22, 0), Width = BodyWidth, Focusable = false, IsTabStop = false };
        _error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(22, 10, 22, 0), Width = BodyWidth, Visibility = Visibility.Collapsed };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        AutomationProperties.SetAutomationId(_error, "Rule.Error");
        _footer = new DockPanel { Margin = new Thickness(22, 18, 22, 20), Width = BodyWidth, LastChildFill = false };

        var root = new Grid();
        for (var i = 0; i < 5; i++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TitleBar { Title = title, ShowMaximize = false, ShowMinimize = false });
        Grid.SetRow(header, 1);
        root.Children.Add(header);
        Grid.SetRow(_page, 2);
        root.Children.Add(_page);
        Grid.SetRow(_error, 3);
        root.Children.Add(_error);
        Grid.SetRow(_footer, 4);
        root.Children.Add(_footer);
        Content = root;

        PreviewKeyDown += OnKey;
        Loaded += (_, _) =>
        {
            if (_initialFocus is { } first) FocusLater(first);
            _initialFocus = null;
        };
        if (editing is null) ShowTemplates();
        else ShowEdit(editing);
    }

    private Action? _back;

    private void OnKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_back is not null && key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            e.Handled = true;
            _back();
        }
    }

    // ---- Sayfalar ----

    /// <summary>1. adım: "Hangi dosyalar?" kutucukları. Tek tık "Nereye?"ye geçer.</summary>
    private void ShowTemplates()
    {
        _title.Text = L.T("Hangi dosyalar?");
        _subtitle.Text = L.T("Bir tür seç ya da uzantıyı kendin yaz.");
        var tiles = new WrapPanel();
        foreach (var offer in RuleTemplates.Offers(AppHost.Settings.Rules))
        {
            var template = offer.Template;
            tiles.Children.Add(Tile("Rule.Template." + template.Key, IconFor(template.Key), template.Name,
                RuleTemplates.Dotted(offer.Extensions), () =>
                {
                    _extensions = [.. offer.Extensions];
                    _suggested = RuleTemplates.FolderFor(template, L.Current, _desktopFolders);
                    ShowWhere(template.Name);
                }));
        }
        tiles.Children.Add(Tile("Rule.Template.custom", SymbolRegular.TagMultiple24, L.T("Başka uzantı…"), L.T("Kendin yaz"), ShowCustom));
        SetPage(tiles, back: null);
        SetFooter(back: null, primary: null);
        FocusLater(tiles.Children[0]);
    }

    /// <summary>"Başka uzantı…": çip girişi; "İleri" en az bir uzantı ister.</summary>
    private void ShowCustom()
    {
        _title.Text = L.T("Hangi dosyalar?");
        _subtitle.Text = L.T("Uzantıyı yaz ve Enter'a bas. Birden çok yazabilirsin.");
        _chips = new ExtensionChips(AppHost.Settings.Rules, editing: null, _extensions);
        _chips.Changed += ClearError;
        SetPage(_chips, back: ShowTemplates);
        SetFooter(back: ShowTemplates, primary: (L.T("İleri"), "Rule.Next", () =>
        {
            if (!_chips.Commit()) return;
            if (_chips.Extensions.Count == 0)
            {
                ShowError(L.T("En az bir uzantı ekle."));
                return;
            }
            _extensions = [.. _chips.Extensions];
            _suggested = RuleTemplates.SuggestFolder(_extensions, L.Current, _desktopFolders);
            ShowWhere(null);
        }));
        FocusLater(_chips.Input);
    }

    /// <summary>2. adım: "Nereye?" (yeni klasör ya da masaüstündeki bir klasör) ve "Ekle".</summary>
    private void ShowWhere(string? templateName)
    {
        _title.Text = L.T("Nereye?");
        var dotted = RuleTemplates.Dotted(_extensions);
        _subtitle.Text = templateName is null ? dotted : L.F("{0} · {1}", templateName, dotted);
        _folder = new FolderChoice(_desktopFolders, _suggested);
        _folder.Changed += ClearError;
        Action back = templateName is null ? ShowCustom : ShowTemplates;
        SetPage(_folder, back);
        SetFooter(back, primary: (L.T("Ekle"), "Rule.Add", Accept));
        FocusLater(_folder.FocusTarget);
    }

    /// <summary>"Değiştir…": uzantı çipleri ve klasör tek sayfada; "Kaydet".</summary>
    private void ShowEdit(Rule rule)
    {
        // Başlık çubuğunda "Kuralı değiştir"; burada hangi kural olduğu.
        _title.Text = rule.TargetFolder.Trim() is { Length: > 0 } name ? L.F("\"{0}\" kuralı", name) : L.T("Kuralı değiştir");
        _subtitle.Text = L.T("Uzantıları ya da klasörü değiştir.");
        var body = new StackPanel();
        body.Children.Add(SectionLabel(L.T("Hangi dosyalar?"), top: 0));
        _chips = new ExtensionChips(AppHost.Settings.Rules, rule, rule.Extensions.Select(RuleTemplates.Normalize).Where(e => e.Length > 0).Distinct());
        _chips.Changed += ClearError;
        body.Children.Add(_chips);
        body.Children.Add(SectionLabel(L.T("Nereye?"), top: 18));
        _folder = new FolderChoice(_desktopFolders, rule.TargetFolder.Trim());
        _folder.Changed += ClearError;
        body.Children.Add(_folder);
        SetPage(body, back: null);
        SetFooter(back: null, primary: (L.T("Kaydet"), "Rule.Save", Accept));
        FocusLater(_chips.Input);
    }

    /// <summary>"Ekle" / "Kaydet": yazılmış uzantı çipe döner, klasör denetlenir; sorun yoksa pencere kapanır.</summary>
    private void Accept()
    {
        if (_chips is not null && _editing is not null)
        {
            if (!_chips.Commit()) return;
            _extensions = [.. _chips.Extensions];
        }
        if (_extensions.Count == 0)
        {
            ShowError(L.T("En az bir uzantı ekle."));
            return;
        }
        if (_folder?.Resolve() is not { } target)
        {
            if (_folder?.Error is { } error) ShowError(error);
            return;
        }
        var rule = _editing is null
            ? RuleTemplates.Create(_extensions, target.Folder)
            : RuleTemplates.Edited(_editing, _extensions, target.Folder);
        _result = (rule, target.Folder, target.Create);
        DialogResult = true;
    }

    private void SetPage(UIElement content, Action? back)
    {
        _back = back;
        _page.Content = content;
        ClearError();
    }

    private void SetFooter(Action? back, (string Text, string Id, Action Run)? primary)
    {
        _footer.Children.Clear();
        if (back is not null)
        {
            var backButton = new Button { Content = L.T("‹ Geri"), Appearance = ControlAppearance.Transparent };
            AutomationProperties.SetAutomationId(backButton, "Rule.Back");
            AutomationProperties.SetName(backButton, L.T("Geri"));
            backButton.Click += (_, _) => back();
            DockPanel.SetDock(backButton, Dock.Left);
            _footer.Children.Add(backButton);
        }
        var cancel = new Button { Content = L.T("Vazgeç"), IsCancel = true, MinWidth = 100, Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetAutomationId(cancel, "Rule.Cancel");
        DockPanel.SetDock(cancel, Dock.Right);
        _footer.Children.Add(cancel);
        if (primary is { } p)
        {
            var ok = new Button { Content = p.Text, Appearance = ControlAppearance.Primary, IsDefault = true, MinWidth = 100 };
            AutomationProperties.SetAutomationId(ok, p.Id);
            ok.Click += (_, _) => p.Run();
            DockPanel.SetDock(ok, Dock.Right);
            _footer.Children.Add(ok);
        }
    }

    private void ShowError(string text)
    {
        _error.Text = text;
        _error.Visibility = Visibility.Visible;
    }

    private void ClearError() => _error.Visibility = Visibility.Collapsed;

    /// <summary>Sayfa açılınca odak: ilk kutucuk ya da yazı kutusu (önceden doldurulmuş ad seçili: hemen üstüne yazılabilsin).</summary>
    private void FocusLater(UIElement element)
    {
        // İlk sayfa kurucuda kurulur: pencere yüklenince odaklanır.
        if (!IsLoaded)
        {
            _initialFocus = element;
            return;
        }
        Dispatcher.BeginInvoke(() =>
        {
            element.Focus();
            Keyboard.Focus(element as IInputElement);
            if (element is System.Windows.Controls.TextBox box) box.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private UIElement? _initialFocus;

    private static TextBlock SectionLabel(string text, double top) => new()
    {
        Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, top, 0, 6),
    };

    /// <summary>Kalıbın simgesi (Core UI'dan bağımsızdır; simge burada seçilir). Hepsi BMP içinde.</summary>
    private static SymbolRegular IconFor(string key) => key switch
    {
        "pdf" => SymbolRegular.DocumentPdf24,
        "pictures" => SymbolRegular.Image24,
        "documents" => SymbolRegular.Document24,
        "videos" => SymbolRegular.Video24,
        "music" => SymbolRegular.MusicNote224,
        "archives" => SymbolRegular.Archive24,
        "design" => SymbolRegular.PaintBrush24,
        "ebooks" => SymbolRegular.Book24,
        _ => SymbolRegular.Folder24,
    };

    /// <summary>Kutucuk (Widget ekle'dekiyle aynı ölçü): simge, ad, altında uzantılar. Basınca içerik hafifçe küçülür.</summary>
    private static Button Tile(string automationId, SymbolRegular icon, string label, string subtitle, Action onClick)
    {
        var content = new StackPanel();
        content.Children.Add(new SymbolIcon { Symbol = icon, FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(new TextBlock
        {
            Text = label, Margin = new Thickness(0, 6, 0, 0), TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 104,
        });
        var line = new TextBlock
        {
            Text = subtitle, FontSize = 11.5, Margin = new Thickness(0, 2, 0, 0), TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 104, MaxHeight = 32,
        };
        line.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        content.Children.Add(line);
        var button = new Button
        {
            Content = content, Width = 120, Height = 100, Margin = new Thickness(0, 0, 8, 8),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 4, 6, 4),
        };
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, label);
        AutomationProperties.SetHelpText(button, subtitle);
        Motion.SetPress(button, true);
        button.Click += (_, _) => onClick();
        return button;
    }

    // ---- Uygulama (pencere kapandıktan sonra) ----

    /// <summary>
    /// Kuralı ekler (old null) ya da değiştirir: klasör gerekiyorsa önce açılır (açılamazsa kural yine eklenir, kartında
    /// "Masaüstünde yok · Oluştur" kalır ve neden bildirilir), sonra yeni liste atanır ve kaydedilir, otomatik taşıma açıksa
    /// masaüstü taranır. Bildirimdeki "Geri al" önceki listeye döner (kural o arada değişmediyse).
    /// </summary>
    internal static Rule Apply(Rule? old, Rule rule, string folder, bool create)
    {
        var created = create && CreateDesktopFolder(folder);
        var rules = AppHost.Settings.Rules;
        AppHost.Settings.Rules = old is null ? RuleTemplates.Added(rules, rule) : RuleTemplates.Replaced(rules, old, rule);
        AppHost.SaveSettings();
        AppHost.OrganizeIfActive();
        // Klasör açılamadıysa nedeni (CreateDesktopFolder söyledi) şeritte kalsın.
        if (create && !created) return rule;
        var name = folder.Trim();
        string text;
        if (old is not null)
            text = created ? L.F("\"{0}\" kuralı değiştirildi; masaüstünde \"{0}\" klasörü de oluşturuldu.", name) : L.F("\"{0}\" kuralı değiştirildi.", name);
        else if (created) text = L.F("\"{0}\" kuralı eklendi; masaüstünde \"{0}\" klasörü de oluşturuldu.", name);
        else text = L.F("\"{0}\" kuralı eklendi.", name);
        Notice.Show(text, NoticeKind.Success, L.T("Geri al"), () => Undo(old, rule));
        return rule;
    }

    /// <summary>Eklemeyi ya da değişikliği geri alır (yeni liste atanır); kural o arada silindiyse ya da değiştiyse bir şey yapmaz.</summary>
    private static void Undo(Rule? old, Rule rule)
    {
        var rules = AppHost.Settings.Rules;
        if (!rules.Contains(rule)) return;
        AppHost.Settings.Rules = old is null ? rules.Where(r => !ReferenceEquals(r, rule)).ToList() : RuleTemplates.Replaced(rules, rule, old);
        AppHost.SaveSettings();
        if (old is not null) AppHost.OrganizeIfActive();
    }

    /// <summary>
    /// Masaüstünde klasör açar (kuralın klasörü): "simge ver" balonu çıkmaz; açılamazsa nedeni bildirilir ve false döner.
    /// Otomatik taşımayı çağıran başlatır (kural listesi önce atanmalı).
    /// </summary>
    internal static bool CreateDesktopFolder(string name)
    {
        var path = Path.Combine(AppHost.DesktopDirectory, name.Trim());
        try { AppHost.MarkQuietFolder(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { }
        if (FolderCreation.TryCreate(path) is { Ok: false } failed)
        {
            try { AppHost.ConsumeQuietFolder(path); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { }
            Notice.FolderFailed(failed);
            return false;
        }
        AppHost.NoteFolderCreated(path);
        return true;
    }

    // ---- Parçalar ----

    /// <summary>
    /// Uzantı çipleri: yazılan uzantı Enter, virgül, noktalı virgül ya da boşlukla çipe döner; çipin × düğmesi kaldırır,
    /// boş kutuda Geri silme son çipi kaldırır. Başka kuralda olan, hiç taşınmayan (kısayol, yarım indirme) ya da geçersiz
    /// uzantı eklenmez; nedeni altında yazar (<see cref="RuleTemplates.Check"/>).
    /// </summary>
    private sealed class ExtensionChips : StackPanel
    {
        private readonly IReadOnlyList<Rule> _rules;
        private readonly Rule? _editing;
        private readonly List<string> _extensions = [];
        private readonly WrapPanel _panel = new();
        private readonly TextBlock _message;
        private bool _committing;

        public TextBox Input { get; }
        public IReadOnlyList<string> Extensions => _extensions;
        public event Action? Changed;

        public ExtensionChips(IReadOnlyList<Rule> rules, Rule? editing, IEnumerable<string> initial)
        {
            _rules = rules;
            _editing = editing;
            Input = new TextBox { PlaceholderText = L.T("ör. psd"), MinWidth = 140, Margin = new Thickness(0, 2, 0, 2) };
            AutomationProperties.SetAutomationId(Input, "Rule.Extensions");
            AutomationProperties.SetName(Input, L.T("Uzantılar"));
            Input.PreviewKeyDown += OnKey;
            Input.TextChanged += (_, _) =>
            {
                if (_committing) return;
                // Ayraç yazıldı ya da birden çok uzantı yapıştırıldı: hepsi çipe döner.
                if (Input.Text.IndexOfAny([',', ';', ' ', '\t', '\r', '\n']) >= 0) Commit();
                else Changed?.Invoke();
            };
            _panel.Children.Add(Input);
            Children.Add(_panel);
            _message = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
            _message.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
            AutomationProperties.SetAutomationId(_message, "Rule.ExtensionError");
            Children.Add(_message);
            foreach (var ext in initial) AddChip(ext);
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Input.Text.Trim().Length > 0)
            {
                // Yazılan uzantı çipe döner; "Ekle"/"İleri"ye ikinci Enter basar.
                e.Handled = true;
                Commit();
            }
            else if (e.Key == Key.Back && Input.Text.Length == 0 && _extensions.Count > 0)
            {
                e.Handled = true;
                Remove(_extensions[^1]);
            }
        }

        /// <summary>Kutudaki metni çiplere çevirir. Sorunlu uzantı varsa nedeni yazar ve false döner (metin kutuda kalır).</summary>
        public bool Commit()
        {
            var text = Input.Text;
            var failed = new List<string>();
            string? problem = null;
            foreach (var raw in text.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var check = RuleTemplates.Check(raw, _rules, _editing);
                if (check.Ok)
                {
                    if (!_extensions.Contains(check.Extension)) AddChip(check.Extension);
                    continue;
                }
                failed.Add(raw);
                problem = check.Problem switch
                {
                    ExtensionProblem.Ignored => L.F(".{0} dosyaları hiç taşınmaz.", check.Extension),
                    ExtensionProblem.Taken => L.F(".{0} zaten \"{1}\" kuralında.", check.Extension, check.Owner),
                    _ => L.F("\"{0}\" bir uzantı olamaz.", raw.Trim()),
                };
            }
            _committing = true;
            Input.Text = string.Join(" ", failed);
            Input.CaretIndex = Input.Text.Length;
            _committing = false;
            _message.Text = problem ?? "";
            _message.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;
            Changed?.Invoke();
            return problem is null;
        }

        private void AddChip(string ext)
        {
            if (ext.Length == 0 || _extensions.Contains(ext)) return;
            _extensions.Add(ext);
            var label = "." + ext;
            var text = new TextBlock { Text = label, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };
            text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            var remove = new Button
            {
                Appearance = ControlAppearance.Transparent, Padding = new Thickness(3), Margin = new Thickness(4, 0, -4, 0),
                Icon = new SymbolIcon { Symbol = SymbolRegular.Dismiss12, FontSize = 10 }, VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetAutomationId(remove, "Rule.Chip." + ext);
            AutomationProperties.SetName(remove, L.F("Kaldır: {0}", label));
            remove.Click += (_, _) =>
            {
                Remove(ext);
                Input.Focus();
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(text);
            row.Children.Add(remove);
            var chip = new Border
            {
                Child = row, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), Padding = new Thickness(8, 1, 6, 1),
                Margin = new Thickness(0, 2, 6, 2), Tag = ext, VerticalAlignment = VerticalAlignment.Center,
            };
            chip.SetResourceReference(Border.BackgroundProperty, "ControlFillColorSecondaryBrush");
            chip.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
            _panel.Children.Insert(_panel.Children.Count - 1, chip);
        }

        private void Remove(string ext)
        {
            if (!_extensions.Remove(ext)) return;
            if (_panel.Children.OfType<Border>().FirstOrDefault(b => Equals(b.Tag, ext)) is { } chip) _panel.Children.Remove(chip);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// "Nereye?": "Yeni klasör: [ad]" ya da "Masaüstündeki klasör: [liste]". Ad masaüstündeki bir klasörle aynıysa (büyük/küçük
    /// harf ve Türkçe karakter fark etmez) o klasör seçili gelir; yeni klasör adı yazılırken altında ne olacağı yazar.
    /// </summary>
    private sealed class FolderChoice : StackPanel
    {
        private readonly List<string> _folders;
        private readonly RadioButton _new;
        private readonly RadioButton _existing;
        private readonly TextBox _name;
        private readonly ComboBox _list;
        private readonly TextBlock _hint;

        public event Action? Changed;
        public string? Error { get; private set; }
        public UIElement FocusTarget => _existing.IsChecked == true ? _list : _name;

        public FolderChoice(List<string> folders, string suggested)
        {
            _folders = folders;
            _new = new RadioButton { Content = L.T("Yeni klasör:"), GroupName = "RuleFolder", VerticalAlignment = VerticalAlignment.Center, MinWidth = 150 };
            AutomationProperties.SetAutomationId(_new, "Rule.NewFolder");
            _name = new TextBox { Text = suggested, MinWidth = 240, MaxLength = 120 };
            AutomationProperties.SetAutomationId(_name, "Rule.Folder");
            AutomationProperties.SetName(_name, L.T("Klasör adı"));
            _name.TextChanged += (_, _) =>
            {
                if (_name.IsKeyboardFocusWithin) _new.IsChecked = true;
                Update();
            };
            _name.GotKeyboardFocus += (_, _) => _new.IsChecked = true;
            Children.Add(Row(_new, _name));

            _existing = new RadioButton
            {
                Content = L.T("Masaüstündeki klasör:"), GroupName = "RuleFolder", VerticalAlignment = VerticalAlignment.Center, MinWidth = 150,
                Margin = new Thickness(0, 10, 0, 0),
            };
            AutomationProperties.SetAutomationId(_existing, "Rule.Existing");
            _list = new ComboBox { MinWidth = 240, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var folder in folders) _list.Items.Add(folder);
            AutomationProperties.SetAutomationId(_list, "Rule.ExistingFolder");
            AutomationProperties.SetName(_list, L.T("Masaüstündeki klasör:").TrimEnd(':'));
            // Listeyi açmak ya da klavyeyle seçmek "Masaüstündeki klasör"ü seçer (yalnızca üstünden Sekme ile geçmek değil).
            _list.DropDownOpened += (_, _) => _existing.IsChecked = true;
            _list.SelectionChanged += (_, _) =>
            {
                if (_list.IsKeyboardFocusWithin || _list.IsDropDownOpen) _existing.IsChecked = true;
                Update();
            };
            Children.Add(Row(_existing, _list));

            _hint = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 10, 0, 0) };
            _hint.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            AutomationProperties.SetAutomationId(_hint, "Rule.FolderHint");
            Children.Add(_hint);

            // Önerilen ad masaüstünde varsa o klasör seçili gelir; yoksa yeni klasör.
            var match = folders.FirstOrDefault(f => FolderName.Equal(f, suggested));
            if (folders.Count == 0) _existing.IsEnabled = _list.IsEnabled = false;
            else _list.SelectedItem = match ?? folders[0];
            if (match is not null) _existing.IsChecked = true;
            else _new.IsChecked = true;
            _new.Checked += (_, _) => Update();
            _existing.Checked += (_, _) => Update();
            Update();
        }

        private static Grid Row(UIElement label, UIElement field)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(field, 1);
            row.Children.Add(label);
            row.Children.Add(field);
            return row;
        }

        private string TypedName => FileNames.Normalize(_name.Text);

        private void Update()
        {
            Error = null;
            if (_existing.IsChecked == true)
                _hint.Text = _list.SelectedItem is string folder ? L.F("Dosyalar masaüstündeki \"{0}\" klasörüne gider.", folder) : "";
            else
            {
                var name = TypedName;
                _hint.Text = name.Length == 0 ? ""
                    : _folders.FirstOrDefault(f => FolderName.Equal(f, name)) is { } same ? L.F("Masaüstünde \"{0}\" klasörü var; dosyalar oraya gider.", same)
                    : L.F("Masaüstünde \"{0}\" klasörü oluşturulur.", name);
            }
            Changed?.Invoke();
        }

        /// <summary>Seçilen klasör ve açılması gerekip gerekmediği; ad kullanılamıyorsa null (neden <see cref="Error"/>).</summary>
        public (string Folder, bool Create)? Resolve()
        {
            Error = null;
            if (_existing.IsChecked == true)
            {
                if (_list.SelectedItem is string folder) return (folder, false);
                Error = L.T("Bir klasör seç.");
                return null;
            }
            var name = TypedName;
            if (name.Length == 0)
            {
                Error = L.T("Klasöre bir ad ver.");
                return null;
            }
            if (FileNames.Validate(name, AppHost.DesktopDirectory) is { } invalid)
            {
                Error = invalid;
                return null;
            }
            return _folders.FirstOrDefault(f => FolderName.Equal(f, name)) is { } same ? (same, false) : (name, true);
        }
    }
}
