using System.Windows;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Widget sağ tık menüleri ve Düzenle paneli için küçük yardımcılar.
/// <para>İki tür öğe var. <b>Komut</b> (<see cref="Item"/>: aç, ekle, kaldır, pencere açan işler) tıklanınca menüyü kapatır.
/// <b>Seçenek</b> (<see cref="Toggle"/>, <see cref="Choice{T}"/>, <see cref="Parts"/>, <see cref="TileOptions"/>) durumunu
/// bir okuyucudan alır, tıklanınca menü açık kalır ve widget hemen değişir; ardından <see cref="Sync"/> menüdeki bütün
/// işaretleri, etkinlikleri ve başlıkları yerinde tazeler. Kolay NestDesk: seçenekler Düzenle panelinde satır olarak çizilir
/// (<c>Views.MenuRows</c>; <see cref="WidgetMenu.Settings"/> ve <see cref="WidgetMenu.More"/>); sağ tık menüsünde yalnızca widget'ın
/// en çok iki işi durur. Kaynağı, başlığı ya da panelin yapısını değiştiren seçenekler (ör. zamanlayıcının türü)
/// <c>staysOpen: false</c> ile işaretlenir (panel yeniden kurulur). Fareyle üstüne gelmek hiçbir öğede bir şey değiştirmez
/// (önizleme yok); gri ipucu satırı ve uygulama geneli ayar widget menüsüne girmez.</para>
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
    /// olana yeniden tıklamak onu boşa düşürmez. Fareyle üstüne gelmek hiçbir şeyi değiştirmez (önizleme yok): widget yalnızca
    /// tıklanınca değişir. Menünün yapısını değiştiren seçim (ör. zamanlayıcının türü) <paramref name="staysOpen"/> = false ile
    /// menüyü kapatır.
    /// </summary>
    public static MenuItem Choice<T>(string header, Func<T> current, IEnumerable<(T Value, string Label)> options, Action<T> select,
        bool staysOpen = true)
    {
        var parent = new MenuItem { Header = header };
        parent.SetValue(PanelShapeProperty, PanelShape.Choice);
        foreach (var (value, label) in options)
            parent.Items.Add(new OptionMenuItem(label, () => EqualityComparer<T>.Default.Equals(current(), value), () => select(value), staysOpen));
        return parent;
    }

    // 2.2 Kolay NestDesk 2. dalga: Düzenle paneli görünümlerin menü öğelerini satır olarak çizer (Views/MenuRows). Alt menünün
    // ne olduğu yapısından anlaşılmaz (tek seçimli seçenekler de parça listesi de OptionMenuItem'dır): kuran yardımcı işaretler.

    /// <summary>Alt menünün panelde nasıl çizileceği: Group = başlık ve altında satırlar, Choice = tek seçim, Parts = "Göster" listesi.</summary>
    internal enum PanelShape { Group, Choice, Parts }

    private static readonly DependencyProperty PanelShapeProperty =
        DependencyProperty.RegisterAttached("PanelShape", typeof(PanelShape), typeof(Menus), new PropertyMetadata(PanelShape.Group));

    private static readonly DependencyProperty PartKeyProperty =
        DependencyProperty.RegisterAttached("PartKey", typeof(string), typeof(Menus));

    /// <summary>Alt menünün panel biçimi (işaretlenmemişse Group).</summary>
    internal static PanelShape ShapeOf(MenuItem item) => (PanelShape)item.GetValue(PanelShapeProperty);

    /// <summary>"Göster" listesindeki seçeneğin parça anahtarı (HiddenParts); parça değilse null.</summary>
    internal static string? PartKeyOf(MenuItem item) => item.GetValue(PartKeyProperty) as string;

    /// <summary>Öğenin <see cref="Live{T}"/> okuyucularını çalıştırır (başlık, etkinlik, görünürlük); yoksa bir şey yapmaz.</summary>
    internal static void RefreshLive(MenuItem item) => (item.GetValue(RefreshProperty) as Action)?.Invoke();

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
    /// boş alana sağ tıklanınca liste menüsü devre dışı kalır, olay widget kartına ulaşır ve widget'ın kendi
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
    /// Kaldırma düğmesi (×) parçası: her widget'ın "Göster" listesinin sonunda durur (Düzenle panelinde "Daha fazla"da). Kapalıyken
    /// widget menüdeki ya da paneldeki "Kaldır" ile kaldırılır; kilitli widget'ta ×'in yerinde kilit durur
    /// (<see cref="CardButtons"/>). Etiket <see cref="L.Dyn"/> ile çevrilir.
    /// </summary>
    public static readonly (string Key, string Label) ClosePart = ("close", L.N("Kaldır düğmesi (×)"));

    /// <summary>
    /// Düzenle düğmesi (⋯) parçası (Kolay NestDesk; <see cref="ClosePart"/>'tan hemen önce): kapalıyken panel sağ tık menüsündeki
    /// "Düzenle…" ile açılır.
    /// </summary>
    public static readonly (string Key, string Label) EditPart = ("edit", L.N("Düzenle düğmesi (⋯)"));

    /// <summary>
    /// "Göster" alt menüsü: widget'ın parçalarını tek tek açıp kapatır (menü açık kalır). Etiketler <see cref="L.N"/> ile
    /// işaretlenmiş Türkçe metinlerdir; burada çevrilir.
    /// </summary>
    public static MenuItem Parts(WidgetConfig c, IEnumerable<(string Key, string Label)> parts, Action changed)
    {
        var parent = new MenuItem { Header = L.T("Göster") };
        parent.SetValue(PanelShapeProperty, PanelShape.Parts);
        foreach (var (key, label) in parts)
        {
            var toggle = Toggle(L.Dyn(label), () => c.Shows(key), () =>
            {
                if (!c.HiddenParts.Remove(key)) c.HiddenParts.Add(key);
                AppHost.SaveSettings();
                changed();
            });
            toggle.SetValue(PartKeyProperty, key);
            parent.Items.Add(toggle);
        }
        return parent;
    }

    /// <summary>
    /// Menü başlığına giren veri (dosya, klasör, sekme adı): menü öğesi "_"yi erişim tuşu sayar ve gizlerdi
    /// ("İş_Dosyaları" → "İşDosyaları").
    /// </summary>
    public static string Literal(string text) => text.Replace("_", "__", StringComparison.Ordinal);

    /// <summary>
    /// Bölme ve kısayol kutusunun seyrek simge seçenekleri (Düzenle paneli ▸ "Daha fazla"): hizalama, aralık, yazı boyutu, adlar,
    /// önizlemeler, tek tıkla açma ve 1–9; hepsi hemen uygulanır. Görünüş (Izgara/Liste) ve simge boyutu panelin üst bölümündedir
    /// (<see cref="Views.EditorContent.TileLook"/>).
    /// </summary>
    public static IReadOnlyList<MenuItem> TileOptions(WidgetConfig c, Action<Action> change, bool singleClickOption)
    {
        var items = new List<MenuItem>
        {
            Choice(L.T("Hizalama"), () => c.Align,
                [(TileAlign.Left, L.T("Sola")), (TileAlign.Center, L.T("Ortaya")), (TileAlign.Right, L.T("Sağa"))], v => change(() => c.Align = v)),
            Choice(L.T("Aralık"), () => c.Spacing,
                [(TileSpacing.Compact, L.T("Sık")), (TileSpacing.Normal, L.T("Normal")), (TileSpacing.Wide, L.T("Geniş"))], v => change(() => c.Spacing = v)),
            Choice(L.T("Yazı boyutu"), () => c.LabelSize,
                [(LabelSize.Small, L.T("Küçük")), (LabelSize.Normal, L.T("Normal")), (LabelSize.Large, L.T("Büyük"))], v => change(() => c.LabelSize = v)),
            Toggle(L.T("Adları gizle"), () => c.HideLabels, () => change(() => c.HideLabels = !c.HideLabels)),
            Toggle(L.T("Önizlemeleri göster (resim, video, PDF)"), () => c.ShowPreviews, () => change(() => c.ShowPreviews = !c.ShowPreviews)),
        };
        if (singleClickOption)
            items.Add(Toggle(L.T("Tek tıkla aç"), () => c.SingleClick, () => change(() => c.SingleClick = !c.SingleClick)));
        // 2.2 Kutu: görünüşü değiştirmez, kutucuklar yeniden kurulmaz (yalnızca kaydedilir). Rozet ve ipucu satırı yok.
        items.Add(Toggle(L.T("Numara tuşlarıyla aç (1–9)"), () => c.QuickKeys, () =>
        {
            c.QuickKeys = !c.QuickKeys;
            AppHost.SaveSettings();
        }));
        return items;
    }

    /// <summary>
    /// "Numara tuşlarıyla aç": 1–9 (sayısal tuş takımı da) listede görünen n'inci öğeyi açar. Seçenek kapalıyken, widget
    /// katlıyken, ad düzenlenirken (<paramref name="editing"/>), yazı kutusunda (bölmenin araması), tuş basılı tutulurken ya da o
    /// sırada öğe yoksa false: tuş harfle aramaya ve yazıya kalır. Bulunamayan öğe açılmaz.
    /// </summary>
    public static bool TryQuickOpen(KeyEventArgs e, WidgetConfig c, ListBox list, bool editing)
    {
        if (QuickOpenTarget(e, c, list, editing) is not { } item) return false;
        TileItem.Launch(item.Path);
        return true;
    }

    /// <summary>Tuşun açacağı öğe (<see cref="TryQuickOpen"/>'ın kararı; açmaz); açılacak bir şey yoksa null.</summary>
    public static TileItem? QuickOpenTarget(KeyEventArgs e, WidgetConfig c, ListBox list, bool editing)
    {
        if (!c.QuickKeys || c.Collapsed || editing || e.IsRepeat || e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase) return null;
        var index = QuickKeys.IndexOf(KeyInterop.VirtualKeyFromKey(e.Key));
        return index >= 0 && index < list.Items.Count && list.Items[index] is TileItem { Missing: false } item ? item : null;
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
    private static readonly ConditionalWeakTable<ListBox, StrongBox<Point>> _pressedAt = new();

    /// <summary>
    /// Bırakılan sol tuş bir tıklama mı (basıldığı yerden sürükleme mesafesi kadar uzaklaşmadı mı)? Sürüklenemeyen öğeyi
    /// (Bu Bilgisayar…) sürükleyip bırakmak onu açmasın.
    /// </summary>
    public static bool IsClick(ListBox list, MouseButtonEventArgs e)
    {
        if (!_pressedAt.TryGetValue(list, out var down)) return true;
        var delta = e.GetPosition(list) - down.Value;
        return Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
               Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance;
    }

    /// <param name="describe">Öğenin sürükleme kaynağı (başka widget'a bırakılınca hedef onu tanır).</param>
    /// <param name="completed">Sürükleme bitti: yol, sonuç ve bir widget'ın içine bırakıldı mı.</param>
    internal static void EnableDragOut(ListBox list, DragDropEffects allowed, Func<TileItem, WidgetDragSource>? describe = null,
        Action<string, DragDropEffects, bool>? completed = null)
    {
        Point? start = null;
        TileItem? item = null;
        list.PreviewMouseLeftButtonDown += (_, e) =>
        {
            item = ItemAt(list, e.OriginalSource);
            start = item is null ? null : e.GetPosition(list);
            _pressedAt.AddOrUpdate(list, new StrongBox<Point>(e.GetPosition(list)));
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
            var source = draggable ? describe?.Invoke(item) : null;
            start = null;
            item = null;
            if (!draggable) return;
            var result = DragDropEffects.None;
            var inside = WidgetDrag.Run(source,
                () => result = DragDrop.DoDragDrop(list, new DataObject(DataFormats.FileDrop, new[] { path }), allowed));
            completed?.Invoke(path, result, inside);
        };
    }
}
