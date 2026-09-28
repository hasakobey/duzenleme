using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Widget sağ tık menüleri için küçük yardımcılar.
/// <para>İki tür öğe var. <b>Komut</b> (<see cref="Item"/>: aç, ekle, kaldır, pencere açan işler) tıklanınca menüyü kapatır.
/// <b>Seçenek</b> (<see cref="Toggle"/>, <see cref="Choice{T}"/>, <see cref="Parts"/>, <see cref="TileOptions"/>) durumunu
/// bir okuyucudan alır, tıklanınca menü açık kalır ve widget hemen değişir; ardından <see cref="Sync"/> menüdeki bütün
/// işaretleri, etkinlikleri ve başlıkları yerinde tazeler. Kaynağı, başlığı ya da menünün kendisini değiştiren seçenekler
/// (ör. "Ne gösterilsin?", masaüstü simgesi kipi) <c>staysOpen: false</c> ile kapatır.</para>
/// </summary>
public static class Menus
{
    /// <summary>Komut: tıklanınca menü kapanır. <paramref name="gesture"/> sağda sönük yazılan kısayoldur (ör. "F2").</summary>
    public static MenuItem Item(string header, Action action, string? gesture = null)
    {
        var item = new MenuItem { Header = header };
        if (gesture is not null) item.InputGestureText = gesture;
        item.Click += (_, _) => action();
        return item;
    }

    /// <summary>Açılıp kapanan seçenek. Durum her seferinde <paramref name="isOn"/>'dan okunur.</summary>
    public static OptionMenuItem Toggle(string header, Func<bool> isOn, Action toggle, bool staysOpen = true) =>
        new(header, isOn, toggle, staysOpen);

    /// <summary>
    /// Tek seçimli alt menü (Arka plan, Boyut, Renk…): her seçenek <paramref name="current"/> ona eşitken işaretlidir; seçili
    /// olana yeniden tıklamak onu boşa düşürmez. <paramref name="preview"/> verilirse fare (ya da klavye) bir seçeneğin
    /// üstüne gelince o seçenek geçici olarak gösterilir; üstünden çıkınca, alt menü kapanınca <paramref name="endPreview"/>
    /// geri alır (yalnızca ucuz, görünüşe ait seçimler için: arka plan, renk, köşe, saydamlık).
    /// </summary>
    public static MenuItem Choice<T>(string header, Func<T> current, IEnumerable<(T Value, string Label)> options, Action<T> select,
        Action<T>? preview = null, Action? endPreview = null)
    {
        var parent = new MenuItem { Header = header };
        foreach (var (value, label) in options)
        {
            var item = new OptionMenuItem(label, () => EqualityComparer<T>.Default.Equals(current(), value), () => select(value));
            if (preview is not null)
            {
                item.MouseEnter += (_, _) => preview(value);
                item.GotKeyboardFocus += (_, _) => preview(value);
                if (endPreview is not null) item.MouseLeave += (_, _) => endPreview();
            }
            parent.Items.Add(item);
        }
        if (endPreview is not null) parent.SubmenuClosed += (_, _) => endPreview();
        return parent;
    }

    /// <summary>
    /// Durumu değişebilen herhangi bir öğe (komut ya da alt menü başlığı): <see cref="Sync"/> başlığını, etkinliğini ve
    /// görünürlüğünü bu okuyuculardan yeniden alır. Öğeyi döndürür (zincirleme için).
    /// </summary>
    public static T Live<T>(T item, Func<string>? header = null, Func<bool>? enabled = null, Func<bool>? visible = null) where T : MenuItem
    {
        void Refresh()
        {
            if (header is not null) item.Header = header();
            if (enabled is not null) item.IsEnabled = enabled();
            if (visible is not null) item.Visibility = visible() ? Visibility.Visible : Visibility.Collapsed;
        }
        item.SetValue(RefreshProperty, (Action)Refresh);
        Refresh();
        return item;
    }

    private static readonly DependencyProperty RefreshProperty =
        DependencyProperty.RegisterAttached("Refresh", typeof(Action), typeof(Menus));

    /// <summary>
    /// <paramref name="from"/>'un bulunduğu menünün bütün öğelerini (kapalı alt menülerdekiler de) modelden tazeler:
    /// seçenek işaretleri ve <see cref="Live{T}"/> okuyucuları. Bir seçenek değişince kardeş radyo seçenekleri, ona bağlı
    /// seçenekler ve sayaçlar tutarlı kalır; menü yeniden kurulmadığından açık alt menüler kapanmaz.
    /// </summary>
    public static void Sync(DependencyObject from)
    {
        var root = from;
        while (root is not ContextMenu && LogicalTreeHelper.GetParent(root) is { } parent) root = parent;
        Refresh(root);
    }

    private static void Refresh(DependencyObject node)
    {
        if (node is not ItemsControl list) return;
        foreach (var child in list.Items)
        {
            if (child is not MenuItem item) continue;
            (item.GetValue(RefreshProperty) as Action)?.Invoke();
            (item as OptionMenuItem)?.Refresh();
            Refresh(item);
        }
    }

    /// <summary>Menü her açılışta yeniden doldurulur; açıkken seçenekler <see cref="Sync"/> ile güncel kalır.</summary>
    public static ContextMenu Dynamic(Action<ContextMenu> fill)
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) =>
        {
            menu.Items.Clear();
            fill(menu);
            if (menu.Items.Count == 0) menu.IsOpen = false;
        };
        menu.Items.Add(new MenuItem());
        return menu;
    }

    /// <summary>
    /// Liste öğelerine (dosya, kısayol) sağ tık menüsü bağlar. Menü yalnızca bir öğeye sağ tıklanınca açılır;
    /// boş alana sağ tıklanınca liste menüsü devre dışı kalır, olay widget kartına ulaşır ve widget'ın kendi ayar
    /// menüsü (Simgeler, Görünüm, Kaldır…) açılır. Böylece eski bir seçim yanlış öğenin menüsünü de açmaz.
    /// <para>Klavyeyle (Shift+F10, menü tuşu) açılan menü odaktaki (yoksa seçili) öğenindir ve onun üstünde açılır; öğe yoksa
    /// widget'ın menüsü açılır. WPF menünün sahibini, olay yolunda ContextMenu'sü olan ilk öğe olarak menü olayından önce
    /// seçer: karar tuşa basılır basılmaz verilir (son sağ tıklanan öğe değil).</para>
    /// </summary>
    public static void AttachItemMenu(ListBox list, Action<ContextMenu, TileItem> fill)
    {
        TileItem? target = null;
        var menu = Dynamic(m => { if (target is { } item) fill(m, item); });
        list.PreviewMouseRightButtonDown += (_, e) =>
        {
            target = ItemAt(list, e.OriginalSource);
            if (target is null) list.SelectedItem = null;
            list.ContextMenu = target is null ? null : menu;
            list.ClearValue(ContextMenuService.PlacementTargetProperty); // fareyle: imlecin yanında
        };
        list.PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key != Key.Apps && !(key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift)) return;
            target = ItemAt(list, e.OriginalSource) ?? list.SelectedItem as TileItem;
            list.ContextMenu = target is null ? null : menu;
            // Klavyeyle açılan menü hedefin ortasına yerleşir (PlacementMode.Center): listenin değil öğenin. ContextMenu hedefi
            // sahibinin ContextMenuService.PlacementTarget'ından alır (menünün kendisine yazılan değer ezilir).
            if (target is not null && list.ItemContainerGenerator.ContainerFromItem(target) is UIElement container)
                ContextMenuService.SetPlacementTarget(list, container);
            else list.ClearValue(ContextMenuService.PlacementTargetProperty);
        };
    }

    /// <summary>Tıklanan öğe; boş alana, kaydırma çubuğuna vb. tıklandıysa null.</summary>
    public static TileItem? ItemAt(ListBox list, object? source)
    {
        var d = source as DependencyObject;
        // Metin içi öğeler (Run) görsel ağaçta değildir; mantıksal üstüne çık.
        while (d is not null and not Visual and not Visual3D) d = LogicalTreeHelper.GetParent(d);
        return d is not null && ItemsControl.ContainerFromElement(list, d) is ListBoxItem { DataContext: TileItem item } ? item : null;
    }

    /// <summary>
    /// Kaldırma düğmesi (×) parçası: beş widget türünün "Göster" listesinin sonunda durur. Kapalıyken widget yalnızca
    /// menüdeki "Kaldır" ile kaldırılır ("Konumu kilitle" de düğmeyi gizler). Etiket <see cref="L.Dyn"/> ile çevrilir.
    /// </summary>
    public static readonly (string Key, string Label) ClosePart = ("close", L.N("Kaldır düğmesi (×)"));

    /// <summary>
    /// "Göster" alt menüsü: widget'ın parçalarını tek tek açıp kapatır (menü açık kalır). Etiketler <see cref="L.N"/> ile
    /// işaretlenmiş Türkçe metinlerdir; burada çevrilir.
    /// </summary>
    public static MenuItem Parts(WidgetConfig c, IEnumerable<(string Key, string Label)> parts, Action changed)
    {
        var parent = new MenuItem { Header = L.T("Göster") };
        foreach (var (key, label) in parts)
            parent.Items.Add(Toggle(L.Dyn(label), () => c.Shows(key), () =>
            {
                if (!c.HiddenParts.Remove(key)) c.HiddenParts.Add(key);
                AppHost.SaveSettings();
                changed();
            }));
        return parent;
    }

    /// <summary>Tıklanamayan, açıklama amaçlı menü satırı.</summary>
    public static MenuItem Hint(string text) => new() { Header = text, IsEnabled = false };

    /// <summary>
    /// Menü başlığına giren veri (dosya, klasör, sekme adı): menü öğesi "_"yi erişim tuşu sayar ve gizlerdi
    /// ("İş_Dosyaları" → "İşDosyaları").
    /// </summary>
    public static string Literal(string text) => text.Replace("_", "__", StringComparison.Ordinal);

    /// <summary>
    /// Bölme ve kısayol kutusunun "Simgeler" alt menüsü: düzen, boyut, hizalama, aralık, yazı, adlar (hepsi menü açıkken
    /// hemen uygulanır). <paramref name="first"/> en başa gelir (bölmenin "Sırala" alt menüsü).
    /// </summary>
    public static MenuItem TileOptions(WidgetConfig c, Action<Action> change, bool singleClickOption, MenuItem? first = null)
    {
        var parent = new MenuItem { Header = L.T("Simgeler") };
        if (first is not null) parent.Items.Add(first);
        parent.Items.Add(Choice(L.T("Düzen"), () => c.View,
            [(ItemView.Icons, L.T("Izgara")), (ItemView.List, L.T("Liste"))], v => change(() => c.View = v)));
        parent.Items.Add(Choice(L.T("Boyut"), () => c.IconSize,
            [(IconSize.Small, L.T("Küçük")), (IconSize.Medium, L.T("Orta")), (IconSize.Large, L.T("Büyük")), (IconSize.ExtraLarge, L.T("Çok büyük"))],
            v => change(() => c.IconSize = v)));
        parent.Items.Add(Choice(L.T("Hizalama"), () => c.Align,
            [(TileAlign.Left, L.T("Sola")), (TileAlign.Center, L.T("Ortaya")), (TileAlign.Right, L.T("Sağa"))], v => change(() => c.Align = v)));
        parent.Items.Add(Choice(L.T("Aralık"), () => c.Spacing,
            [(TileSpacing.Compact, L.T("Sık")), (TileSpacing.Normal, L.T("Normal")), (TileSpacing.Wide, L.T("Geniş"))], v => change(() => c.Spacing = v)));
        parent.Items.Add(Choice(L.T("Yazı boyutu"), () => c.LabelSize,
            [(LabelSize.Small, L.T("Küçük")), (LabelSize.Normal, L.T("Normal")), (LabelSize.Large, L.T("Büyük"))], v => change(() => c.LabelSize = v)));
        parent.Items.Add(new Separator());
        parent.Items.Add(Toggle(L.T("Adları gizle"), () => c.HideLabels, () => change(() => c.HideLabels = !c.HideLabels)));
        parent.Items.Add(Toggle(L.T("Önizlemeleri göster (resim, video, PDF)"), () => c.ShowPreviews, () => change(() => c.ShowPreviews = !c.ShowPreviews)));
        if (singleClickOption)
            parent.Items.Add(Toggle(L.T("Tek tıkla aç"), () => c.SingleClick, () => change(() => c.SingleClick = !c.SingleClick)));
        parent.Items.Add(Hint(L.T("İpucu: Ctrl + fare tekerleği simgeleri büyütür/küçültür")));
        return parent;
    }

    /// <summary>Ctrl + tekerlek: simge boyutunu bir kademe değiştirir. Değiştiyse true.</summary>
    public static bool StepIconSize(WidgetConfig c, int wheelDelta, Action<Action> change)
    {
        var next = (IconSize)Math.Clamp((int)c.IconSize + (wheelDelta > 0 ? 1 : -1), (int)IconSize.Small, (int)IconSize.ExtraLarge);
        if (next == c.IconSize) return false;
        change(() => c.IconSize = next);
        return true;
    }

    /// <summary>
    /// Listedeki öğe dışarı sürüklenebilsin (başka bölmeye, Gezgin'e, e-postaya…). <paramref name="allowed"/> taşımayı
    /// içermiyorsa hedef dosyayı yerinden oynatamaz (kısayol kutusundaki öğeler yalnızca bağlantıdır).
    /// </summary>
    public static void EnableDragOut(ListBox list, DragDropEffects allowed)
    {
        Point? start = null;
        TileItem? item = null;
        list.PreviewMouseLeftButtonDown += (_, e) =>
        {
            item = ItemAt(list, e.OriginalSource);
            start = item is null ? null : e.GetPosition(list);
        };
        list.PreviewMouseLeftButtonUp += (_, _) => { start = null; item = null; };
        list.PreviewMouseMove += (_, e) =>
        {
            if (start is not { } origin || item is null || e.LeftButton != MouseButtonState.Pressed) return;
            var delta = e.GetPosition(list) - origin;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            var path = TileItem.NativePath(item.Path);
            // Yol diske bakılmadan bilinir (bölmede anlık görüntüden, kutuda arka plan denetiminden): ulaşılamayan bir ağ
            // yolu sürüklemeyi başlatırken arayüzü dondurmasın.
            var draggable = !item.Missing && !TileItem.IsShellObject(item.Path);
            start = null;
            item = null;
            if (!draggable) return;
            DragDrop.DoDragDrop(list, new DataObject(DataFormats.FileDrop, new[] { path }), allowed);
        };
    }
}
