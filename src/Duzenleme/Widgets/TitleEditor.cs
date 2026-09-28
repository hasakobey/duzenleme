using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Duzenleme.Widgets;

/// <summary>
/// Widget başlığının yerinde düzenlenmesi (F2, "Yeniden adlandır", yeni eklenen bölme/kutu): başlık yazısının yerine aynı
/// yazıyla bir kutu gelir; Enter, Tab ya da dışarı tıklamak kaydeder, Esc vazgeçer. Boş ya da varsayılanla aynı ad
/// varsayılana döner. Düzenlenirken başlık simgesi düğme olur ve simge seçiciyi açar (yalnızca bu açık durumda; fareyle
/// belirip kaybolan bir şey yok). Başlık sığdırmaya (<see cref="HeaderFitter"/>) katılır: kutuya yer kalsın diye isteğe bağlı
/// parçalar gizlenir, simge düğmesi gizlenmez. Yeni widget görünümleri (bölme, kutu, not ve sonrakiler) aynı sınıfı kullanır.
/// </summary>
internal sealed class TitleEditor
{
    /// <summary>Başlık en çok bu kadar karakter.</summary>
    public const int MaxLength = 60;

    /// <summary>Düzenlenirken kutuya bırakılan en az genişlik (DIP).</summary>
    public const double EditorMinWidth = 120;

    private readonly FrameworkElement _view;
    private readonly Grid _header;
    private readonly TextBlock _title;
    private readonly Button? _iconButton;
    private readonly Func<string> _defaultTitle;
    private readonly Action<string?> _commit;
    private readonly Action? _pickIcon;
    private readonly Action _refit;

    private Border? _frame;
    private TextBox? _box;
    private IInputElement? _returnFocus;
    private Window? _window;
    private bool _ending;
    private Brush _accent = Brushes.MediumPurple;
    private Brush _foreground = Brushes.White;

    /// <param name="view">Widget görünümü (pencereyi bulmak için).</param>
    /// <param name="header">Başlık satırı; kutu başlık yazısının hücresine konur.</param>
    /// <param name="title">Başlık yazısı (düzenlenirken gizlenir; yazı tipi ondan alınır).</param>
    /// <param name="iconButton">Başlık simgesi düğmesi (<c>HeaderIconButton</c> stili); yoksa null (not).</param>
    /// <param name="defaultTitle">Varsayılan başlık: aynısı yazılırsa ayar boşaltılır.</param>
    /// <param name="commit">Yeni başlık (null = varsayılan): görünüm ayara yazar, kaydeder ve başlığı yeniden çizer.</param>
    /// <param name="pickIcon">Simge seçiciyi açar; yoksa simge düğmesi hiç etkinleşmez.</param>
    /// <param name="refit">Başlık satırını yeniden sığdırır (görünümün parça uygulaması).</param>
    public TitleEditor(FrameworkElement view, Grid header, TextBlock title, Button? iconButton, Func<string> defaultTitle,
        Action<string?> commit, Action? pickIcon, Action refit)
    {
        _view = view;
        _header = header;
        _title = title;
        _iconButton = iconButton;
        _defaultTitle = defaultTitle;
        _commit = commit;
        _pickIcon = pickIcon;
        _refit = refit;
        if (_iconButton is not null)
        {
            _iconButton.Click += (_, _) => OnIconClick();
            ApplyIconLook(false);
        }
    }

    /// <summary>Başlık şu an düzenleniyor mu?</summary>
    public bool IsEditing => _box is not null;

    /// <summary>Düzenleme bitince (kayıt ya da vazgeçme), odak geri verildikten sonra; ör. yeni notta yazı alanına geçmek için.</summary>
    public Action<bool>? Ended { get; set; }

    /// <summary>
    /// Düzenlemeyi başlatır: pencere klavye için etkinleşir, kutu odaklanır, metnin tamamı (selectAll) ya da imleç sona.
    /// Başlık satırı gizliyse başlamaz (false).
    /// </summary>
    public bool Begin(bool selectAll = true)
    {
        if (IsEditing)
        {
            Focus(selectAll);
            return true;
        }
        if (_header.Visibility != Visibility.Visible || !_view.IsVisible) return false;
        _window = Window.GetWindow(_view);
        _returnFocus = Keyboard.FocusedElement is DependencyObject focused && _window is not null && Window.GetWindow(focused) == _window
            ? Keyboard.FocusedElement
            : null;

        _box = new TextBox
        {
            Style = (Style)_view.FindResource("TitleEditorBox"),
            Text = _title.Text,
            MaxLength = MaxLength,
            FontSize = _title.FontSize,
            FontWeight = _title.FontWeight,
            FontFamily = _title.FontFamily,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = _foreground,
            CaretBrush = _foreground,
            SelectionBrush = _accent,
        };
        AutomationProperties.SetAutomationId(_box, "Widget.TitleEditor");
        AutomationProperties.SetName(_box, L.T("Başlık"));
        // Kenarlık ve dolgu negatif kenar boşluğuyla dengelenir: yazı başlığın yerinde kalır, satır yükselmez.
        _frame = new Border
        {
            Child = _box,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(3, 0, 3, 0),
            Margin = new Thickness(-4, -3, 0, -3),
            BorderBrush = _accent,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_frame, Grid.GetColumn(_title));
        Grid.SetRow(_frame, Grid.GetRow(_title));
        _header.Children.Add(_frame);
        _title.Visibility = Visibility.Hidden;

        _box.PreviewKeyDown += OnKey;
        // Odak başka yere geçince kaydedilir; metin kutusunun kendi menüsü (Kes/Kopyala/Yapıştır) açıkken değil (kapanınca odak döner).
        var box = _box;
        _box.LostKeyboardFocus += (_, e) =>
        {
            if (IsMenu(e.NewFocus)) return;
            AfterFocusLoss(_view, () => _box == box && !box.IsKeyboardFocusWithin, Commit);
        };
        if (_window is not null) _window.PreviewMouseDown += OnWindowMouseDown;

        ApplyIconLook(true);
        _refit();
        (_window as WidgetWindow)?.ActivateForInput();
        Focus(selectAll);
        return true;
    }

    private void Focus(bool selectAll)
    {
        var box = _box;
        if (box is null) return;
        void Apply()
        {
            if (_box != box) return;
            Keyboard.Focus(box);
            if (selectAll) box.SelectAll();
            else box.CaretIndex = box.Text.Length;
        }
        Apply();
        // Pencere o an etkinleşmediyse (ör. menü kapanırken) odak biraz sonra yeniden verilir.
        if (!box.IsKeyboardFocused) _view.Dispatcher.BeginInvoke(Apply, DispatcherPriority.Input);
    }

    /// <summary>Yazılanı kaydeder ve düzenlemeyi bitirir (boş ya da varsayılan ad: varsayılana döner).</summary>
    public void Commit()
    {
        if (_box is not { } box || _ending) return;
        var text = box.Text.Trim();
        End(returnFocus: box.IsKeyboardFocusWithin);
        _commit(text.Length == 0 || string.Equals(text, _defaultTitle(), StringComparison.Ordinal) ? null : text);
    }

    /// <summary>Düzenlemeyi bırakır; başlık değişmez.</summary>
    public void Cancel()
    {
        if (_box is not { } box || _ending) return;
        End(returnFocus: box.IsKeyboardFocusWithin);
    }

    private void End(bool returnFocus)
    {
        _ending = true;
        try
        {
            if (_window is not null) _window.PreviewMouseDown -= OnWindowMouseDown;
            if (_frame is not null) _header.Children.Remove(_frame);
            _frame = null;
            _box = null;
            _title.Visibility = Visibility.Visible;
            ApplyIconLook(false);
            _refit();
            if (returnFocus)
            {
                // F2'ye yeniden basılabilsin: odak düzenlemeden önceki yere (ya da widget'ın kartına) döner.
                if (_returnFocus is UIElement { IsVisible: true } previous && Window.GetWindow(previous) == _window) Keyboard.Focus(previous);
                else (_window as WidgetWindow)?.FocusCard();
            }
            _returnFocus = null;
        }
        finally
        {
            _ending = false;
        }
        Ended?.Invoke(returnFocus);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter or Key.Tab:
                Commit();
                e.Handled = true;
                break;
            case Key.Escape:
                Cancel();
                e.Handled = true;
                break;
            case Key.F2:
                e.Handled = true; // düzenlerken F2 bir şey yapmaz (Gezgin gibi)
                break;
        }
    }

    /// <summary>Kutunun ve simge düğmesinin dışına tıklamak kaydeder (tıklama yine olağan işini yapar).</summary>
    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEditing || e.OriginalSource is not DependencyObject source) return;
        if (IsWithin(source, _frame) || IsWithin(source, _iconButton)) return;
        Commit();
    }

    /// <summary>
    /// Yerinde düzenleme kutusu odağı kaybetti: odak pencere içinde başka yere geçtiyse hemen, pencere etkinliğini kaybettiyse
    /// kısa bir süre sonra (hâlâ kaybolmuşsa) kaydedilir. Etkinlik bir an gidip gelirse (ör. yardımcı teknoloji odağı
    /// ayarlarken) WPF odağı kutuya geri verir ve yazılan kaybolmaz; kullanıcı başka pencereye geçtiyse Gezgin gibi kaydedilir.
    /// </summary>
    internal static void AfterFocusLoss(FrameworkElement owner, Func<bool> stillLost, Action commit)
    {
        if (Window.GetWindow(owner)?.IsActive == true)
        {
            owner.Dispatcher.BeginInvoke(() => { if (stillLost()) commit(); }, DispatcherPriority.Input);
            return;
        }
        var timer = new DispatcherTimer(DispatcherPriority.Input, owner.Dispatcher) { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (stillLost()) commit();
        };
        timer.Start();
    }

    /// <summary>Odak bir sağ tık menüsüne mi geçiyor (metin kutusunun Kes/Kopyala/Yapıştır menüsü)?</summary>
    internal static bool IsMenu(IInputElement? focus)
    {
        for (var d = focus as DependencyObject; d is not null; d = LogicalTreeHelper.GetParent(d) ?? (d is Visual ? VisualTreeHelper.GetParent(d) : null))
            if (d is ContextMenu) return true;
        return false;
    }

    private static bool IsWithin(DependencyObject source, DependencyObject? container)
    {
        if (container is null) return false;
        for (var d = source; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (ReferenceEquals(d, container)) return true;
        return false;
    }

    /// <summary>Simge düğmesi: düzenleniyorsa önce başlık kaydedilir, sonra seçici açılır.</summary>
    private void OnIconClick()
    {
        if (_pickIcon is null) return;
        Commit();
        _pickIcon();
    }

    /// <summary>Düzenlerken simge tıklanabilir bir düğme gibi görünür (vurgu kenarlığı, hafif zemin, el imleci, ipucu).</summary>
    private void ApplyIconLook(bool editing)
    {
        if (_iconButton is null) return;
        var active = editing && _pickIcon is not null;
        _iconButton.IsHitTestVisible = active;
        _iconButton.Cursor = active ? Cursors.Hand : null;
        _iconButton.BorderBrush = active ? _accent : Brushes.Transparent;
        _iconButton.Background = active ? new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)) : Brushes.Transparent;
        _iconButton.ToolTip = active ? L.T("Simgeyi değiştir") : null;
    }

    /// <summary>Başlık satırını sığdırır; düzenlenirken kutuya yer açılır ve simge düğmesi hep görünür kalır.</summary>
    public void Fit(IReadOnlyList<UIElement> optional, IReadOnlyList<UIElement> fixedParts)
    {
        if (!IsEditing)
        {
            HeaderFitter.Fit(_header, _title, optional, fixedParts);
            return;
        }
        var parts = optional.Where(p => !ReferenceEquals(p, _iconButton)).ToList();
        if (_iconButton is not null) _iconButton.Visibility = Visibility.Visible;
        var fixedWithIcon = _iconButton is null ? fixedParts : [.. fixedParts, _iconButton];
        HeaderFitter.Fit(_header, EditorMinWidth, parts, fixedWithIcon, EditorMinWidth);
    }

    /// <summary>Renkler görünümün paletinden (vurgu kenarlığı, yazı ve seçim).</summary>
    public void ApplyPalette(WidgetPalette palette)
    {
        _accent = palette.Accent;
        _foreground = palette.Foreground;
        if (_frame is not null) _frame.BorderBrush = palette.Accent;
        if (_box is not null)
        {
            _box.Foreground = palette.Foreground;
            _box.CaretBrush = palette.Foreground;
            _box.SelectionBrush = palette.Accent;
        }
        ApplyIconLook(IsEditing);
    }
}
