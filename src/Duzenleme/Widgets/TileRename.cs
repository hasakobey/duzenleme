using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Duzenleme.Widgets;

/// <summary>
/// Bölme ya da kutu kutucuğunun adını yerinde düzenler (F2, "Yeniden adlandır"): kutucuğun adının yerine, Gezgin'deki gibi
/// kenarlıklı bir kutu gelir. Kutu şablonda hazır durmaz, yalnızca düzenlenen kutucuğa eklenir (yüzlerce kutucukta her
/// birine bir TextBox kurmak açılışı yavaşlatırdı). Enter/Tab ya da dışarı tıklamak kaydeder, Esc vazgeçer. Yazılamayan
/// karakterler kutuya hiç girmez ve kutunun altında kısa bir uyarı çıkar. Kutu açıkken görünüm listeyi yenilemez
/// (<see cref="IsActive"/>): yenileme kutucuğu, dolayısıyla yazılanı yok ederdi.
/// </summary>
internal sealed class TileRename
{
    private readonly ListBox _list;
    private readonly TileItem _item;
    private readonly Func<string, bool> _commit;
    private readonly Action _ended;
    private readonly bool _fileName;
    private TextBox? _box;
    private TextBlock? _label;
    private Panel? _panel;
    private Popup? _hint;
    private Window? _window;
    private bool _ending;

    private TileRename(ListBox list, TileItem item, Func<string, bool> commit, Action ended, bool fileName)
    {
        _list = list;
        _item = item;
        _commit = commit;
        _ended = ended;
        _fileName = fileName;
    }

    /// <summary>Düzenlenen kutucuk.</summary>
    public TileItem Item => _item;

    /// <summary>Kutu açık mı (ya da açılmak üzere mi)?</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Kutucuğun adını düzenlemeye açar. commit yazılanla çağrılır; false dönerse (ad geçersiz) kutu açık kalır.
    /// ended kutu kapanınca (kayıt ya da vazgeçme) çağrılır.
    /// </summary>
    /// <param name="fileName">Dosya adı mı (yazılamayan karakterler engellenir)? Kutu etiketinde serbest metin.</param>
    public static TileRename Begin(ListBox list, TileItem item, string text, int selectStart, int selectLength, int maxLength,
        bool fileName, WidgetPalette palette, Func<string, bool> commit, Action ended)
    {
        var rename = new TileRename(list, item, commit, ended, fileName);
        list.SelectedItem = item;
        list.ScrollIntoView(item);
        (Window.GetWindow(list) as WidgetWindow)?.ActivateForInput();
        // Kutucuk yeni kurulduysa (liste henüz yerleşmedi) şablonu bir sonraki yerleşimde hazırdır.
        list.Dispatcher.BeginInvoke(() => rename.Attach(text, selectStart, selectLength, maxLength, palette), DispatcherPriority.Loaded);
        return rename;
    }

    private void Attach(string text, int selectStart, int selectLength, int maxLength, WidgetPalette palette)
    {
        if (!IsActive) return;
        var container = _list.ItemContainerGenerator.ContainerFromItem(_item) as ListBoxItem;
        var presenter = container is null ? null : FindChild<ContentPresenter>(container);
        presenter?.ApplyTemplate();
        _label = presenter?.ContentTemplate?.FindName("Label", presenter) as TextBlock;
        _panel = _label?.Parent as Panel;
        if (_label is null || _panel is null)
        {
            // Kutucuk görünmüyor (ör. arama süzdü): düzenleme olmaz.
            Finish();
            return;
        }

        _window = Window.GetWindow(_list);
        _box = new TextBox
        {
            Style = (Style)_list.FindResource("TileRenameBox"),
            Text = text,
            MaxLength = maxLength,
            FontSize = _label.FontSize,
            TextAlignment = _label.TextAlignment,
            TextWrapping = _label.TextWrapping,
            Margin = _label.Margin,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 40,
            Foreground = palette.Foreground,
            CaretBrush = palette.Foreground,
            SelectionBrush = palette.Accent,
            BorderBrush = palette.Accent,
            // Kutu, cam zeminde de okunabilsin diye opaktır (Gezgin'deki beyaz kutu gibi).
            Background = palette.IsLight ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x2E)),
        };
        _panel.Children.Insert(_panel.Children.IndexOf(_label) + 1, _box);
        _label.Visibility = Visibility.Collapsed;

        _box.PreviewKeyDown += OnKey;
        _box.KeyDown += (_, e) =>
        {
            // Liste yön tuşlarıyla seçimi değiştirmesin (kutu kapanıp yazılan kaybolurdu). Karakter üreten tuşlara dokunulmaz:
            // KeyDown işaretlenirse Windows o tuşun karakterini hiç göndermez.
            if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End or Key.Left or Key.Right) e.Handled = true;
        };
        if (_fileName)
        {
            _box.PreviewTextInput += (_, e) =>
            {
                if (!e.Text.Any(FileNamesInvalid)) return;
                e.Handled = true;
                ShowHint(Core.FileNames.InvalidCharactersHint);
            };
            DataObject.AddPastingHandler(_box, OnPaste);
        }
        _box.TextChanged += (_, _) => HideHint();
        // Odak başka yere geçince (başka kutucuk, pencere dışı) kaydedilir. Metin kutusunun kendi menüsü (Kes/Kopyala/Yapıştır)
        // açılınca odak menüdedir: kapanınca kutuya döner. Kaydederken sorulan soru (uzantı değişimi) açıkken _ending kaydı engeller.
        var box = _box;
        _box.LostKeyboardFocus += (_, e) =>
        {
            if (TitleEditor.IsMenu(e.NewFocus)) return;
            TitleEditor.AfterFocusLoss(_list, () => IsActive && !_ending && _box == box && !box.IsKeyboardFocusWithin, Commit);
        };
        if (_window is not null) _window.PreviewMouseDown += OnWindowMouseDown;

        Keyboard.Focus(_box);
        _box.Select(Math.Clamp(selectStart, 0, text.Length), Math.Clamp(selectLength, 0, text.Length - Math.Clamp(selectStart, 0, text.Length)));
        if (!_box.IsKeyboardFocused)
            _list.Dispatcher.BeginInvoke(() =>
            {
                if (_box is not { } box) return;
                Keyboard.Focus(box);
                box.Select(Math.Clamp(selectStart, 0, box.Text.Length), Math.Clamp(selectLength, 0, box.Text.Length - Math.Clamp(selectStart, 0, box.Text.Length)));
            }, DispatcherPriority.Input);
    }

    private static bool FileNamesInvalid(char c) => Core.FileNames.IsInvalidChar(c);

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true) ||
            e.SourceDataObject.GetData(DataFormats.UnicodeText, true) is not string pasted) return;
        var clean = Core.FileNames.StripInvalid(pasted.Replace("\r", " ").Replace("\n", " "));
        if (clean == pasted) return;
        e.CancelCommand();
        ShowHint(Core.FileNames.InvalidCharactersHint);
        if (_box is { } box) box.SelectedText = clean;
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
                e.Handled = true;
                break;
        }
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsActive || _box is null || e.OriginalSource is not DependencyObject source) return;
        for (var d = source; d is not null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (ReferenceEquals(d, _box)) return;
        Commit();
    }

    /// <summary>Yazılanı kaydetmeyi dener; ad geçersizse kutu açık kalır (uyarı çağıranın <see cref="ShowHint"/>'iyle).</summary>
    public void Commit()
    {
        if (!IsActive || _ending || _box is not { } box) return;
        _ending = true;
        bool done;
        try { done = _commit(box.Text); }
        finally { _ending = false; }
        if (done) Finish();
        else if (IsActive)
        {
            Keyboard.Focus(box);
            box.SelectAll();
        }
    }

    /// <summary>Kutuyu kapatır; ad değişmez.</summary>
    public void Cancel()
    {
        if (!IsActive) return;
        Finish();
    }

    private void Finish()
    {
        if (!IsActive) return;
        IsActive = false;
        var hadFocus = _box?.IsKeyboardFocusWithin == true;
        HideHint();
        if (_window is not null) _window.PreviewMouseDown -= OnWindowMouseDown;
        if (_box is not null) _panel?.Children.Remove(_box);
        if (_label is not null) _label.Visibility = Visibility.Visible;
        _box = null;
        // Gezgin gibi: kutucuk seçili ve odakta kalır, F2 ya da yön tuşları hemen çalışır.
        if (hadFocus && _list.ItemContainerGenerator.ContainerFromItem(_item) is ListBoxItem container) container.Focus();
        _ended();
    }

    /// <summary>Kutunun altında kısa uyarı (yazılamayan karakter, geçersiz ad); bir sonraki tuşta kaybolur.</summary>
    public void ShowHint(string text)
    {
        if (_box is null) return;
        HideHint();
        _hint = new Popup
        {
            PlacementTarget = _box,
            Placement = PlacementMode.Bottom,
            PopupAnimation = PopupAnimation.None,
            AllowsTransparency = true,
            StaysOpen = true,
            Focusable = false,
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x33)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 5, 8, 6),
                Margin = new Thickness(0, 4, 0, 0),
                MaxWidth = 260,
                Child = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 12, TextWrapping = TextWrapping.Wrap },
            },
        };
        _hint.IsOpen = true;
    }

    private void HideHint()
    {
        if (_hint is null) return;
        _hint.IsOpen = false;
        _hint = null;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } deeper) return deeper;
        }
        return null;
    }
}
