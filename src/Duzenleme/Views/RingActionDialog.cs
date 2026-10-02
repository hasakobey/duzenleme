using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Duzenleme.Core;
using Duzenleme.Desktop;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using Image = System.Windows.Controls.Image;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Duzenleme.Views;

/// <summary>
/// Halka öğesi ekleme ve düzenleme penceresi (InputDialog kalıbında: eşzamanlı, animasyonsuz, widget'ın monitöründe; Enter
/// kaydeder, Esc vazgeçer). Her türün kendi alanları: web adresi (yazılan adres tamamlanır, web dışı şema bir kez onaylanır),
/// komut (program, bağımsız değişkenler, klasör, "konsolu gizle", "yönetici olarak"; yalnızca seçilince çalıştığı söylenir),
/// tuş kısayolu (tuşlar yalnızca bu pencere odaktayken kaydedilir; en çok 8 birleşim), metin (parola uyarısı; pano geçmişine
/// girmez), NestDesk eylemi, alt halka (ad). Ad ve simge hepsinde değiştirilebilir (boş ad: türün/dosyanın adı).
/// Öğe yalnızca "Kaydet"te ve geçerliyse değişir. AutomationId'ler <c>Ring.Dialog.*</c>.
/// </summary>
internal static class RingActionDialog
{
    /// <summary>Yeni yol olmayan öğe; vazgeçilirse null.</summary>
    public static LauncherAction? Create(string type, WidgetConfig config, WidgetWindow? anchor)
    {
        var action = new LauncherAction { Type = type, App = type == RingActionTypes.App ? RingAppIds.All[0] : null };
        return Show(action, isNew: true, config, anchor) ? action : null;
    }

    /// <summary>Var olan öğeyi düzenler; kaydedildiyse true (öğe yerinde değişir).</summary>
    public static bool Edit(LauncherAction action, WidgetConfig config, WidgetWindow? anchor) => Show(action, isNew: false, config, anchor);

    /// <summary>Alt halkanın adı (yeni ya da değiştirme); vazgeçilirse null.</summary>
    public static string? AskSubRing(string? current, WidgetWindow? anchor)
    {
        var form = new Form(current is null ? L.T("Alt halka ekle") : L.T("Alt halka"), anchor);
        var name = form.Field(L.T("Alt halkanın adı"), "Ring.Dialog.Name", current ?? "", ItemLooks.MaxName);
        name.PlaceholderText = L.T("ör. Oyunlar, İş");
        form.Note(L.T("Alt halka kök halkanın sonunda durur; içine öğe eklemek için ona sağ tıkla."));
        string? result = null;
        form.Saving = () =>
        {
            var text = name.Text.Trim();
            if (text.Length == 0) return form.Fail(L.T("Alt halkaya bir ad ver."), name);
            result = text;
            return true;
        };
        form.Focus = name;
        return form.ShowDialog() ? result : null;
    }

    /// <summary>Yol öğesinin halkada görünen adı ve simgesi (dosyaya dokunulmaz); vazgeçilirse null.</summary>
    public static (string? Name, string? Icon)? EditPath(RingSlot slot, WidgetConfig config, WidgetWindow? anchor)
    {
        if (slot.Path is not { } path) return null;
        var look = ItemLooks.Get(config, path);
        var form = new Form(L.T("Öğeyi değiştir"), anchor);
        var defaultName = RingSlot.DefaultPathName(path);
        string? icon = look?.Icon;
        var name = form.NameRow(look?.Name ?? "", defaultName, () => icon, i => icon = i, anchor is null || slot.Tile is null ? null : pick =>
            IconPicker.ForItem(anchor, slot.Tile, icon, pick.Preview, pick.Commit), RingSlot.Preview(path, config));
        form.Note(L.T("Yalnızca halkada görünen ad değişir; dosyanın adı değişmez."));
        (string?, string?)? result = null;
        form.Saving = () =>
        {
            var text = name.Text.Trim();
            result = (text.Length == 0 ? null : text, icon);
            return true;
        };
        form.Focus = name;
        return form.ShowDialog() ? result : null;
    }

    private static bool Show(LauncherAction action, bool isNew, WidgetConfig config, WidgetWindow? anchor)
    {
        var form = new Form(Title(action.Type, isNew), anchor);
        // Alanlar öğenin bir kopyasında değişir; "Kaydet"te ve geçerliyse öğeye yazılır.
        var draft = new LauncherAction
        {
            Type = action.Type, App = action.App, Url = action.Url, Command = action.Command, Text = action.Text,
            Keys = action.Keys is null ? null : [.. action.Keys],
        };
        string? icon = action.Icon;
        var name = form.NameRow(action.Name ?? "", RingSlot.DefaultName(action), () => icon, i => icon = i,
            anchor is null ? null : pick => IconPicker.ForAction(anchor, string.IsNullOrWhiteSpace(form.NameText) ? RingSlot.DefaultName(draft) : form.NameText,
                icon, RingSlot.DefaultGlyph(draft), pick.Preview, pick.Commit),
            RingSlot.Preview(draft, config));
        void Defaults() => name.PlaceholderText = RingSlot.DefaultName(draft);
        Func<bool> save = () => true;

        switch (action.Type)
        {
            case RingActionTypes.Url:
            {
                var url = form.Field(L.T("Web adresi"), "Ring.Dialog.Url", action.Url ?? "", RingActions.MaxUrlLength);
                url.PlaceholderText = L.T("ör. https://example.com");
                url.TextChanged += (_, _) => { draft.Url = RingActions.TryNormalizeUrl(url.Text); Defaults(); };
                save = () =>
                {
                    if (RingActions.TryNormalizeUrl(url.Text) is not { } normalized)
                        return form.Fail(L.T("Bu bir web adresi değil. Örnek: https://example.com"), url);
                    // Web dışı bir şema (ör. bir uygulamanın kendi adresi) yalnızca eklenirken ya da değişince bir kez sorulur.
                    if (RingActions.TryParseUrl(normalized, out var uri, out var check) && check == UrlCheck.NeedsConfirmation &&
                        (isNew || !string.Equals(normalized, action.Url, StringComparison.Ordinal)) &&
                        !Confirm.Ask(form.Window, L.T("Bu adres bir uygulama açar"),
                            L.F("\"{0}:\" ile başlayan adres web tarayıcısında değil, bu bilgisayardaki bir uygulamada açılır. Yalnızca tanıdığın adresleri ekle.", uri.Scheme),
                            L.T("Ekle"), danger: false))
                        return false;
                    action.Url = normalized;
                    return true;
                };
                form.Focus = url;
                break;
            }
            case RingActionTypes.Command:
            {
                var command = form.Field(L.T("Program ya da komut"), "Ring.Dialog.Command", action.Command ?? "", 1024, browse: box =>
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = L.T("Program seç"),
                        Filter = L.T("Programlar|*.exe;*.bat;*.cmd;*.msc;*.lnk|Tüm dosyalar|*.*"),
                        DereferenceLinks = false,
                    };
                    if (dialog.ShowDialog(form.Window) == true) box.Text = dialog.FileName;
                });
                command.PlaceholderText = L.T("ör. notepad.exe");
                command.TextChanged += (_, _) => { draft.Command = command.Text; Defaults(); };
                var args = form.Field(L.T("Bağımsız değişkenler (isteğe bağlı)"), "Ring.Dialog.Args", action.Args ?? "", 2048);
                var dir = form.Field(L.T("Çalışma klasörü (isteğe bağlı)"), "Ring.Dialog.Dir", action.Dir ?? "", 1024);
                var hide = form.Check(L.T("Konsol penceresini gizle"), "Ring.Dialog.HideWindow", action.HideWindow);
                var admin = form.Check(L.T("Yönetici olarak çalıştır (Windows her seferinde sorar)"), "Ring.Dialog.Admin", action.Admin);
                form.Warning(L.T("Komut yalnızca sen bu öğeyi seçince çalışır. Tanımadığın bir komutu ekleme."));
                save = () =>
                {
                    if (string.IsNullOrWhiteSpace(command.Text)) return form.Fail(L.T("Programı ya da komutu yaz."), command);
                    action.Command = command.Text.Trim();
                    action.Args = string.IsNullOrWhiteSpace(args.Text) ? null : args.Text.Trim();
                    action.Dir = string.IsNullOrWhiteSpace(dir.Text) ? null : dir.Text.Trim();
                    action.HideWindow = hide.IsChecked == true;
                    action.Admin = admin.IsChecked == true;
                    return true;
                };
                form.Focus = command;
                break;
            }
            case RingActionTypes.Keys:
            {
                var chords = new List<string>(action.Keys ?? []);
                var keys = form.Field(L.T("Gönderilecek tuşlar"), "Ring.Dialog.Keys", "", 0);
                keys.IsReadOnly = true;
                keys.IsReadOnlyCaretVisible = false;
                keys.PlaceholderText = L.T("Tuşlara bas…");
                void ShowChords()
                {
                    keys.Text = string.Join(", ", chords);
                    draft.Keys = chords.Count > 0 ? [.. chords] : null;
                    Defaults();
                }
                ShowChords();
                // Tuşlar yalnızca bu kutu odaktayken kaydedilir (genel kanca yok). Tab ve Esc pencerenindir.
                keys.PreviewKeyDown += (_, e) =>
                {
                    var key = e.Key switch
                    {
                        Key.System => e.SystemKey,
                        Key.ImeProcessed => e.ImeProcessedKey,
                        Key.DeadCharProcessed => e.DeadCharProcessedKey,
                        _ => e.Key,
                    };
                    if (key is Key.Tab or Key.Escape) return;
                    e.Handled = true;
                    if (e.IsRepeat || key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift
                            or Key.RightShift or Key.LWin or Key.RWin) return;
                    if (chords.Count >= RingActions.MaxChords)
                    {
                        form.Fail(L.T("En çok 8 tuş birleşimi."), null);
                        return;
                    }
                    if (Chord(key) is not { } chord || !KeySender.CanSend([chord]))
                    {
                        form.Fail(L.T("Bu tuş gönderilemez."), null);
                        return;
                    }
                    form.Clear();
                    chords.Add(chord);
                    ShowChords();
                };
                form.Buttons(
                    (L.T("Sonuncuyu sil"), "Ring.Dialog.RemoveLast", () => { if (chords.Count > 0) chords.RemoveAt(chords.Count - 1); ShowChords(); keys.Focus(); }),
                    (L.T("Temizle"), "Ring.Dialog.Clear", () => { chords.Clear(); ShowChords(); keys.Focus(); }));
                form.Note(L.T("En çok 8 tuş birleşimi.") + " " + L.T("Bu öğe kısayolla açılan halkada çalışır."));
                save = () =>
                {
                    if (!KeySender.CanSend(chords)) return form.Fail(L.T("En az bir tuş birleşimine bas."), keys);
                    action.Keys = [.. chords];
                    return true;
                };
                form.Focus = keys;
                break;
            }
            case RingActionTypes.Text:
            {
                var text = form.Field(L.T("Yapıştırılacak metin"), "Ring.Dialog.Text", action.Text ?? "", RingActions.MaxTextLength, multiline: true);
                text.TextChanged += (_, _) => { draft.Text = text.Text; Defaults(); };
                form.Warning(L.T("Parolaları buraya kaydetme."));
                form.Note(L.T("Yapıştırılan metin pano geçmişine ve bulut panosuna eklenmez; önceki pano içeriği geri konur.") + " " +
                          L.T("Bu öğe kısayolla açılan halkada çalışır."));
                save = () =>
                {
                    if (!RingActions.ValidText(text.Text)) return form.Fail(L.T("Yapıştırılacak metni yaz."), text);
                    action.Text = text.Text;
                    return true;
                };
                form.Focus = text;
                break;
            }
            case RingActionTypes.App:
            {
                var choice = form.Choice(L.T("Ne yapılsın?"), "Ring.Dialog.App",
                    RingAppIds.All.Select(id => (id, RingAppIds.Label(id) ?? id)).ToList(), action.App);
                choice.SelectionChanged += (_, _) =>
                {
                    draft.App = (choice.SelectedItem as ComboBoxItem)?.Tag as string;
                    Defaults();
                    form.RefreshIcon?.Invoke(); // varsayılan simge eyleme göre
                };
                save = () =>
                {
                    if ((choice.SelectedItem as ComboBoxItem)?.Tag is not string id) return form.Fail(L.T("Bir eylem seç."), choice);
                    action.App = id;
                    return true;
                };
                form.Focus = choice;
                break;
            }
            default:
                // Daha yeni bir sürümün öğesi: yalnızca adı ve simgesi değiştirilebilir; içeriği korunur.
                form.Note(L.F("Bu öğe {0}'in daha yeni bir sürümünde eklenmiş; bu sürümde çalışmaz.", AppInfo.Name));
                form.Focus = name;
                break;
        }

        form.Saving = () =>
        {
            if (!save()) return false;
            var text = name.Text.Trim();
            action.Name = text.Length == 0 ? null : text;
            action.Icon = string.IsNullOrWhiteSpace(icon) ? null : icon;
            return true;
        };
        return form.ShowDialog();
    }

    /// <summary>Türün pencere başlığı.</summary>
    private static string Title(string type, bool isNew) => type switch
    {
        RingActionTypes.Url => isNew ? L.T("Web adresi ekle") : L.T("Web adresi"),
        RingActionTypes.Command => isNew ? L.T("Komut ekle") : L.T("Komut"),
        RingActionTypes.Keys => isNew ? L.T("Tuş kısayolu ekle") : L.T("Tuş kısayolu"),
        RingActionTypes.Text => isNew ? L.T("Metin ekle") : L.T("Metin"),
        RingActionTypes.App => L.F("{0} eylemi", AppInfo.Name),
        _ => L.T("Öğeyi değiştir"),
    };

    /// <summary>Basılan tuşun birleşimi ("Ctrl+Shift+T", "F5"); adı yazılamayan tuşta null.</summary>
    private static string? Chord(Key key)
    {
        var mods = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Ctrl;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mods |= HotkeyModifiers.Win;
        var name = key is >= Key.D0 and <= Key.D9 ? ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture) : key.ToString();
        return Hotkey.IsWellFormedKeyName(name) ? new Hotkey(mods, name).ToString() : null;
    }

    /// <summary>Simge seçicinin geri çağırmaları (önizleme ve kayıt).</summary>
    internal readonly record struct IconPick(Action<string?> Preview, Action<string?> Commit);

    /// <summary>Pencerenin iskeleti: başlık çubuğu, alanlar, uyarılar, hata satırı ve Kaydet/Vazgeç.</summary>
    private sealed class Form
    {
        private readonly StackPanel _panel = new() { Margin = new Thickness(24, 8, 24, 20), Width = 440 };
        private readonly TextBlock _error;
        private readonly WidgetWindow? _anchor;
        private TextBox? _name;

        public FluentWindow Window { get; }
        public Func<bool> Saving { get; set; } = () => true;
        public UIElement? Focus { get; set; }
        public string NameText => _name?.Text.Trim() ?? "";

        /// <summary>Ad satırındaki simgeyi yeniden çizer (varsayılan simge türün alanına bağlıysa).</summary>
        public Action? RefreshIcon { get; private set; }

        public Form(string title, WidgetWindow? anchor)
        {
            _anchor = anchor;
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.Children.Add(new TitleBar { Title = title, ShowMaximize = false, ShowMinimize = false });
            Grid.SetRow(_panel, 1);
            root.Children.Add(_panel);
            _error = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 10) };
            _error.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
            AutomationProperties.SetAutomationId(_error, "Ring.Dialog.Error");
            AutomationProperties.SetLiveSetting(_error, AutomationLiveSetting.Assertive);
            Window = new FluentWindow
            {
                Title = title,
                Content = root,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                ExtendsContentIntoTitleBar = true,
                WindowBackdropType = WindowBackdropType.Mica,
                Topmost = true,
                ShowInTaskbar = false,
                MinWidth = 0,
                MinHeight = 0,
            };
            AutomationProperties.SetAutomationId(Window, "Ring.Dialog");
            // WPF ilk açılan pencereyi Application.MainWindow yapar; tema değişikliği bu küçük pencereye bağlanmasın.
            if (Application.Current?.MainWindow == Window) Application.Current.MainWindow = null;
        }

        /// <summary>Ad satırı: solda simge düğmesi (seçici açar), sağda ad kutusu (boşsa türün adı görünür).</summary>
        public TextBox NameRow(string current, string placeholder, Func<string?> icon, Action<string?> setIcon, Action<IconPick>? pick,
            Func<string?, double, Color, Action<ImageSource?>, IDisposable?> preview)
        {
            Label(L.T("Ad"));
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            var image = new Image { Width = 24, Height = 24, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            var color = Application.Current?.TryFindResource("TextFillColorPrimaryBrush") is SolidColorBrush brush ? brush.Color : Colors.Gray;
            IDisposable? loading = null;
            void ShowIcon(string? value)
            {
                loading?.Dispose();
                loading = preview(value, 24, color, source => image.Source = source);
            }
            ShowIcon(icon());
            RefreshIcon = () => ShowIcon(icon());
            Window.Closed += (_, _) => loading?.Dispose();
            var button = new Button
            {
                Content = image, Width = 44, Height = 36, Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 0),
                ToolTip = L.T("Simgeyi değiştir…"), IsEnabled = pick is not null,
            };
            AutomationProperties.SetAutomationId(button, "Ring.Dialog.Icon");
            AutomationProperties.SetName(button, L.T("Simgeyi değiştir…"));
            button.Click += (_, _) => pick?.Invoke(new IconPick(ShowIcon, value =>
            {
                setIcon(value);
                ShowIcon(value);
            }));
            DockPanel.SetDock(button, Dock.Left);
            row.Children.Add(button);
            _name = new TextBox { Text = current, PlaceholderText = placeholder, MaxLength = ItemLooks.MaxName, VerticalContentAlignment = VerticalAlignment.Center };
            AutomationProperties.SetAutomationId(_name, "Ring.Dialog.Name");
            AutomationProperties.SetName(_name, L.T("Ad"));
            row.Children.Add(_name);
            _panel.Children.Add(row);
            return _name;
        }

        public TextBox Field(string label, string automationId, string text, int maxLength, bool multiline = false, Action<TextBox>? browse = null)
        {
            Label(label);
            var box = new TextBox { Text = text };
            if (maxLength > 0) box.MaxLength = maxLength;
            if (multiline)
            {
                box.AcceptsReturn = true;
                box.TextWrapping = TextWrapping.Wrap;
                box.Height = 132;
                box.VerticalContentAlignment = VerticalAlignment.Top;
                ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            }
            AutomationProperties.SetAutomationId(box, automationId);
            AutomationProperties.SetName(box, label);
            if (browse is null)
            {
                box.Margin = new Thickness(0, 0, 0, 14);
                _panel.Children.Add(box);
                return box;
            }
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            var button = new Button { Content = L.T("Gözat…"), Margin = new Thickness(8, 0, 0, 0) };
            AutomationProperties.SetAutomationId(button, "Ring.Dialog.Browse");
            button.Click += (_, _) => browse(box);
            DockPanel.SetDock(button, Dock.Right);
            row.Children.Add(button);
            row.Children.Add(box);
            _panel.Children.Add(row);
            return box;
        }

        public CheckBox Check(string label, string automationId, bool value)
        {
            var box = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 0, 0, 8) };
            AutomationProperties.SetAutomationId(box, automationId);
            _panel.Children.Add(box);
            return box;
        }

        public ComboBox Choice(string label, string automationId, IReadOnlyList<(string Value, string Label)> options, string? current)
        {
            Label(label);
            var box = new ComboBox { Margin = new Thickness(0, 0, 0, 14) };
            foreach (var (value, text) in options) box.Items.Add(new ComboBoxItem { Content = text, Tag = value });
            box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, current)) ?? box.Items[0];
            AutomationProperties.SetAutomationId(box, automationId);
            AutomationProperties.SetName(box, label);
            _panel.Children.Add(box);
            return box;
        }

        public void Buttons(params (string Text, string AutomationId, Action Click)[] buttons)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, -6, 0, 12) };
            foreach (var (text, id, click) in buttons)
            {
                var button = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0) };
                AutomationProperties.SetAutomationId(button, id);
                button.Click += (_, _) => click();
                row.Children.Add(button);
            }
            _panel.Children.Add(row);
        }

        /// <summary>Uyarı (sarı simgeli, kalın değil): komutlar ve parolalar.</summary>
        public void Warning(string text)
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 10) };
            var icon = new SymbolIcon { Symbol = SymbolRegular.Warning24, FontSize = 16, Margin = new Thickness(0, 1, 8, 0), VerticalAlignment = VerticalAlignment.Top };
            icon.SetResourceReference(TextElement.ForegroundProperty, "SystemFillColorCautionBrush");
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            row.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
            _panel.Children.Add(row);
        }

        public void Note(string text)
        {
            var note = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Margin = new Thickness(0, 0, 0, 10) };
            note.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            _panel.Children.Add(note);
        }

        private void Label(string text) => _panel.Children.Add(new TextBlock { Text = text, FontSize = 14, Margin = new Thickness(0, 0, 0, 6) });

        /// <summary>Hata satırını gösterir, varsa alanı odaklar; Saving içinde "kaydetme" demek için false döner.</summary>
        public bool Fail(string message, UIElement? field)
        {
            _error.Text = message;
            _error.Visibility = Visibility.Visible;
            field?.Focus();
            return false;
        }

        public void Clear() => _error.Visibility = Visibility.Collapsed;

        public bool ShowDialog()
        {
            _panel.Children.Add(_error);
            var ok = new Button { Content = L.T("Kaydet"), Appearance = ControlAppearance.Primary, IsDefault = true, MinWidth = 100 };
            var cancel = new Button { Content = L.T("Vazgeç"), IsCancel = true, MinWidth = 100, Margin = new Thickness(8, 0, 0, 0) };
            AutomationProperties.SetAutomationId(ok, "Ring.Dialog.Save");
            AutomationProperties.SetAutomationId(cancel, "Ring.Dialog.Cancel");
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            _panel.Children.Add(buttons);

            WindowFit.FitChromeToContent(Window);
            DialogPlacement.CenterOn(Window, _anchor?.CenterPoint);
            ok.Click += (_, _) =>
            {
                if (Saving()) Window.DialogResult = true;
            };
            Window.Loaded += (_, _) =>
            {
                if (Focus is not { } focus) return;
                focus.Focus();
                Keyboard.Focus(focus as IInputElement);
                if (focus is TextBox box && !box.IsReadOnly) box.SelectAll();
            };
            return Window.ShowDialog() == true;
        }
    }
}
