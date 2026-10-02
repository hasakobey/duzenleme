using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Masaüstünde yüzen yapışkan not (SlideSlide'daki notlar gibi). Yazdıkça kaydedilir.
/// <see cref="WidgetConfig.NoteChecklist"/> açıkken aynı not onay kutulu liste (Yapılacaklar) olur: maddeler NoteText'te
/// satır satır "☐ " / "☑ " önekiyle durur (<see cref="ChecklistText"/>), bu alanı tanımayan eski sürüm düz not görür.
/// </summary>
public partial class NoteView : UserControl, IWidgetView
{
    // Etiketler gösterilirken çevrilir (L.Dyn).
    private static readonly (NoteColor Color, string Label)[] Colors =
    [
        (NoteColor.Yellow, L.N("Sarı")), (NoteColor.Pink, L.N("Pembe")), (NoteColor.Green, L.N("Yeşil")),
        (NoteColor.Blue, L.N("Mavi")), (NoteColor.Purple, L.N("Mor")), (NoteColor.Graphite, L.N("Grafit")),
    ];

    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _saveTimer;
    private readonly TitleEditor _titleEditor;
    private WidgetPalette _palette;

    // Kip değişirken (satırlar kurulurken, düz metin yüklenirken) kayıt yapılmaz: yarım kurulmuş liste notu silmesin.
    private bool _applying;

    public NoteView(WidgetConfig config)
    {
        var opening = DebugLog.Enabled ? Stopwatch.StartNew() : null;
        _config = config;
        _palette = WidgetPalette.ForNote(config.NoteColor);
        InitializeComponent();
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _saveTimer.Tick += (_, _) => Save();

        Editor.TextChanged += (_, _) => QueueSave();
        Editor.LostKeyboardFocus += (_, _) => Save();
        // Yazı alanının menüsü düzenleme komutlarıdır; notun kendi menüsü başlığa sağ tıklayınca (klavyede Ctrl+Shift+F10)
        // açılır.
        Editor.ContextMenu = Menus.Dynamic(menu =>
        {
            AddEditCommands(menu, Editor);
            AddNoteMenuRow(menu);
        });

        _rowTextStyle = (Style)FindResource("InlineTextBox");
        _rowBoxStyle = (Style)FindResource("ChecklistBox");
        _rowMenu = Menus.Dynamic(FillRowMenu);
        AddBox.ContextMenu = _rowMenu;
        AddBox.ContextMenuOpening += (_, _) => _menuTarget = AddBox;
        AddBox.PreviewKeyDown += OnAddBoxKey;
        DataObject.AddPastingHandler(AddBox, OnPaste);
        ChecklistScroll.LostKeyboardFocus += (_, _) => Save();
        // Listenin boş yerine tıklamak yeni madde yazmaya başlatır (düz notta yazı alanına tıklamak gibi).
        ChecklistScroll.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            FocusText(AddBox);
        };

        // Başlık yerinde düzenlenir (F2; Düzenle panelinde ad kutusu); notun başlık simgesi yok. Yeni not adlandırmaya açılmaz: yazı
        // alanıyla açılır (WidgetManager.FocusNote).
        _titleEditor = new TitleEditor(this, HeaderRow, TitleText, null, () => DefaultTitle, CommitTitle, null, () => { });

        ApplyMode();

        if (opening is not null)
        {
            var tag = $"[Note:{DebugLog.ShortId(config.Id)}]";
            DebugLog.Write($"{tag} kuruldu: {(config.NoteChecklist ? $"{_rows.Count + _pending.Count} madde ({_rows.Count} satır hemen)" : "düz not")}, {opening.ElapsedMilliseconds} ms");
            RoutedEventHandler? loaded = null;
            loaded = (_, _) =>
            {
                Loaded -= loaded;
                DebugLog.Write($"{tag} açıldı (pencere ve yerleşim dahil): {opening.ElapsedMilliseconds} ms");
            };
            Loaded += loaded;
        }
    }

    public bool Resizable => true;
    public Thickness CardPadding => new(16, 12, 16, 14);

    // Not kendi kağıt rengini kullanır (AdjustPalette): widget'ın arka plan ve vurgu rengi seçenekleri etkisiz.
    public bool UsesThemeColors => false;

    private string DefaultTitle => _config.NoteChecklist ? L.T("Yapılacaklar") : L.T("Not");

    // Görünümün o an gösterdiği kip (ApplyMode yazar): Save yalnızca ayardaki kip bununla aynıyken yazar.
    private bool _shownChecklist;

    private void QueueSave()
    {
        if (_applying) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Save()
    {
        _saveTimer.Stop();
        // Gösterilen kip ayardakinden farklıysa ayar bu görünümün altından değişti (Düzenle panelinin "Geri al"ı listeyi düz nota
        // ya da düz notu listeye geri aldı; metni de geri yazmış olabilir): bu görünüm eskidir, yerine yenisi kurulur. Yazarsa
        // ayardaki metni boş yazı alanıyla ya da eski maddelerle ezerdi.
        if (_applying || _shownChecklist != _config.NoteChecklist) return;
        var text = _shownChecklist ? ChecklistText.Format(CurrentItems()) : Editor.Text;
        if (_config.NoteText == text) return;
        _config.NoteText = text;
        AppHost.SaveSettings();
    }

    /// <summary>Kipe göre düz yazı alanını ya da listeyi gösterir; liste NoteText'ten kurulur.</summary>
    private void ApplyMode()
    {
        _applying = true;
        try
        {
            // Önce yeni içerik hazırlanır, sonra görünürlük değişir: gizlenen alanın odak kaybı (kayıt) eksik içerik görmesin.
            _shownChecklist = _config.NoteChecklist;
            if (_config.NoteChecklist)
            {
                // "Bitenler alta" açıksa liste açılırken de öyle dizilir (metin başka yerde, ör. 2.0'da değişmiş olabilir).
                var items = ChecklistText.Parse(_config.NoteText);
                RebuildRows(_config.ChecklistDoneLast ? ChecklistText.DoneLast(items, i => i.Done) : items);
                Editor.Text = "";
                Editor.Visibility = Visibility.Collapsed;
                ChecklistScroll.Visibility = Visibility.Visible;
            }
            else
            {
                Editor.Text = _config.NoteText;
                ClearRows();
                ChecklistScroll.Visibility = Visibility.Collapsed;
                Editor.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            _applying = false;
        }
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        TitleText.Text = string.IsNullOrWhiteSpace(_config.Title) ? DefaultTitle : _config.Title;
        HeaderRow.Visibility = _config.Shows("header") ? Visibility.Visible : Visibility.Collapsed;
        Corner.Update(_config);
        UpdateProgress();
    }

    public WidgetPalette AdjustPalette(WidgetPalette palette) => WidgetPalette.ForNote(_config.NoteColor);

    // --- 2.1 P6: yazı boyutu (LabelSize; 2.0 da bu alanı tanır ve korur) ve Ctrl + tekerlek ---

    /// <summary>Notun ve maddelerin yazı boyutu: Küçük 13, Normal 15, Büyük 18.</summary>
    private double NoteFontSize => _config.LabelSize switch { LabelSize.Small => 13, LabelSize.Large => 18, _ => 15 };

    /// <summary>Ctrl + tekerlek notta yazıyı büyütür/küçültür (bölmedeki simgeler gibi); widget ölçeklenmez.</summary>
    public bool OnCtrlWheel(int delta)
    {
        var next = (LabelSize)Math.Clamp((int)_config.LabelSize + (delta > 0 ? 1 : -1), (int)LabelSize.Small, (int)LabelSize.Large);
        if (next != _config.LabelSize) SetFontSize(next);
        return true;
    }

    private void SetFontSize(LabelSize size)
    {
        _config.LabelSize = size;
        AppHost.SaveSettings();
        ApplyFont();
    }

    private void ApplyFont()
    {
        var size = NoteFontSize;
        Editor.FontSize = size;
        AddBox.FontSize = size;
        foreach (var row in _rows)
        {
            row.Label.FontSize = size;
            if (row.Editor is { } editor) editor.FontSize = size;
        }
    }

    /// <summary>
    /// Kart opakken (not renkleri) yazı ClearType ile çizilir: yazı alanı ve liste kaydırılan (kırpılan) alandadır, ipucu
    /// yazılara ayrıca verilir. Biten madde soluktur (yarı saydam): onda gri tonlama kalır.
    /// </summary>
    private void ApplyClearType()
    {
        var hint = ClearTypeText.Of(this);
        ClearTypeText.Apply(Editor, hint);
        ClearTypeText.Apply(AddBox, hint);
        foreach (var row in _rows) ApplyRowClearType(row, hint);
    }

    private static void ApplyRowClearType(Row row, System.Windows.Media.ClearTypeHint hint)
    {
        var rowHint = row.Done ? System.Windows.Media.ClearTypeHint.Auto : hint;
        System.Windows.Media.RenderOptions.SetClearTypeHint(row.Label, rowHint);
        if (row.Editor is { } editor) ClearTypeText.Apply(editor, rowHint);
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        _titleEditor.ApplyPalette(palette);
        Editor.Foreground = palette.Foreground;
        Editor.CaretBrush = palette.Foreground;
        Editor.SelectionBrush = palette.Accent;
        TitleText.Foreground = palette.Secondary;
        ProgressText.Foreground = palette.Secondary;
        Corner.Paint(palette);
        AddGlyph.Foreground = palette.Foreground;
        PaintText(AddBox);
        foreach (var row in _rows) PaintRow(row);
        ApplyFont();
        ApplyClearType();
        UpdateTitle();
    }

    private void SetColor(NoteColor color)
    {
        _config.NoteColor = color;
        AppHost.SaveSettings();
        AppHost.Widgets.Restyle(_config.Id);
    }

    /// <summary>
    /// Menüde: notta "Listeye çevir"; listede "Bitenleri temizle (n)" (biten varsa) ve "Düz nota çevir". Renk, liste kipi, yazı
    /// boyutu ve "Bitenler alta insin" Düzenle panelinde (<see cref="AddEditorContent"/>); kopyalama ve temizleme "Daha fazla"da.
    /// </summary>
    public void AddMenuItems(WidgetMenu menu)
    {
        if (_config.NoteChecklist)
        {
            var done = CurrentItems().Count(i => i.Done && i.Text.Trim().Length > 0);
            if (done > 0) menu.Primary.Add(Menus.Item(L.F("Bitenleri temizle ({0})", done), ClearDone));
            menu.Primary.Add(Menus.Item(L.T("Düz nota çevir"), ToPlainNote));
        }
        else menu.Primary.Add(Menus.Item(L.T("Listeye çevir"), ToChecklist));
        if (!menu.ForEditor) return;
        menu.Settings.Add(Menus.Parts(_config, [("header", L.N("Başlık")), Menus.EditPart, Menus.ClosePart], UpdateTitle));
        menu.More.Add(Menus.Item(L.T("Panoya kopyala"), CopyToClipboard));
        if (!_config.NoteChecklist) menu.More.Add(Menus.Item(L.T("Notu temizle"), ClearNote));
    }

    /// <summary>
    /// Düzenle paneli: "Renk" (kâğıt örnekleri), "Onay kutulu liste" (panel yeniden kurulur), "Yazı: Küçük · Orta · Büyük" ve
    /// listede "Bitenler alta insin". Hepsi hemen uygulanır.
    /// </summary>
    public bool AddEditorContent(Views.EditorContent content)
    {
        content.Swatches(L.T("Renk"),
            Colors.Select(c => (c.Color.ToString(), L.Dyn(c.Label), WidgetPalette.ForNote(c.Color).Background, "Editor.Note.Color." + c.Color)).ToList(),
            () => _config.NoteColor.ToString(), id =>
            {
                if (Enum.TryParse<NoteColor>(id, out var color) && color != _config.NoteColor) SetColor(color);
            });
        content.Switch(L.T("Onay kutulu liste"), "Editor.Note.Checklist", () => _config.NoteChecklist, on =>
        {
            if (on == _config.NoteChecklist) return;
            if (on) ToChecklist();
            else ToPlainNote();
        }, rebuild: true);
        content.Segments(L.T("Yazı"),
            [
                new(nameof(LabelSize.Small), L.T("Küçük"), "Editor.Note.Font.Small"),
                new(nameof(LabelSize.Normal), L.T("Orta"), "Editor.Note.Font.Normal"),
                new(nameof(LabelSize.Large), L.T("Büyük"), "Editor.Note.Font.Large"),
            ],
            () => _config.LabelSize.ToString(), id =>
            {
                if (Enum.TryParse<LabelSize>(id, out var size) && size != _config.LabelSize) SetFontSize(size);
            });
        if (_config.NoteChecklist)
            content.Switch(L.T("Bitenler alta insin"), "Editor.Note.DoneLast", () => _config.ChecklistDoneLast, on =>
            {
                if (on == _config.ChecklistDoneLast) return;
                _config.ChecklistDoneLast = on;
                if (on) MoveDoneLast();
                AppHost.SaveSettings();
                Save();
            });
        return true;
    }

    /// <summary>F2 (yazarken de: F2'nin metinde bir anlamı yok): başlık yerinde düzenlenir; önce yazılanlar kaydedilir.</summary>
    public bool TryBeginRename()
    {
        if (_titleEditor.IsEditing) return true;
        return BeginTitleEdit();
    }

    public bool RefreshTitle()
    {
        _titleEditor.Cancel();
        UpdateTitle();
        return true;
    }

    /// <summary>Başlığı yerinde düzenler; başlık satırı gizliyse küçük pencereyle sorar (widget'ın monitöründe).</summary>
    private bool BeginTitleEdit()
    {
        Save();
        if (_titleEditor.Begin()) return true;
        var dialogTitle = _config.NoteChecklist ? L.T("Listeyi yeniden adlandır") : L.T("Notu yeniden adlandır");
        if (InputDialog.Ask(dialogTitle, L.T("Başlık"), TitleText.Text, (Window.GetWindow(this) as WidgetWindow)?.CenterPoint) is { } title)
            CommitTitle(string.IsNullOrWhiteSpace(title) || title == DefaultTitle ? null : title);
        return true;
    }

    private void CommitTitle(string? title)
    {
        if (string.Equals(_config.Title, title, StringComparison.Ordinal)) return;
        _config.Title = title;
        AppHost.SaveSettings();
        UpdateTitle();
    }

    private void CopyToClipboard()
    {
        var text = _config.NoteChecklist ? ChecklistText.Format(CurrentItems()) : Editor.Text;
        if (text.Length == 0) return;
        try { Clipboard.SetText(text); }
        catch (ExternalException) { } // pano başka bir uygulamada kilitli; kullanıcı yeniden dener
    }

    /// <summary>Düz notu listeye çevirir: her dolu satır bir madde olur (madde işaretleri ve "[x]" gibi önekler tanınır).</summary>
    private void ToChecklist()
    {
        Save();
        _config.NoteText = ChecklistText.Format(ChecklistText.Parse(_config.NoteText));
        _config.NoteChecklist = true;
        AppHost.SaveSettings();
        ApplyMode();
    }

    /// <summary>Listeyi düz nota çevirir; metin "☐ "/"☑ " önekleriyle kalır, geri çevirmek aynı listeyi verir.</summary>
    private void ToPlainNote()
    {
        Save();
        _config.NoteChecklist = false;
        AppHost.SaveSettings();
        ApplyMode();
    }

    public void FocusEditor()
    {
        if (_config.NoteChecklist)
        {
            FocusText(AddBox);
            return;
        }
        Editor.Focus();
        Keyboard.Focus(Editor);
        Editor.CaretIndex = Editor.Text.Length;
    }

    public void Flush() => Save();

    public void Detach()
    {
        _titleEditor.Cancel();
        Save();
    }

    /// <summary>
    /// "Notu temizle" (soru yok): yazı silinir, bildirimdeki "Geri al" onu geri getirir (o arada yazılan da kalır). Geri alma
    /// notu kimliğiyle bulur (<see cref="WidgetManager.ViewOf"/>): widget o arada kaldırılıp geri getirildiyse yeni görünüme
    /// yazar; kaldırılmış kaldıysa bir şey yapmaz.
    /// </summary>
    private void ClearNote()
    {
        Save();
        var cleared = _config.NoteText;
        if (string.IsNullOrEmpty(cleared)) return;
        Editor.Clear();
        Save();
        var id = _config.Id;
        Views.Notice.Show(L.T("Not temizlendi."), Views.NoticeKind.Info, L.T("Geri al"),
            () => (AppHost.Widgets.ViewOf(id) as NoteView)?.RestoreCleared(cleared), L.T("Geri almak için buraya tıkla."));
    }

    private void RestoreCleared(string cleared)
    {
        if (_config.NoteChecklist) return;
        Editor.Text = NoteClear.Restore(cleared, Editor.Text);
        Save();
    }

    // --- Yapılacaklar ---
    // Satırlar hafiftir (onay kutusu + TextBlock): 200 maddelik liste de hızlı açılır. Yalnızca düzenlenen satırda bir
    // TextBox bulunur (BeginEdit); odak başka yere geçince yeniden TextBlock olur (EndEdit).

    /// <summary>Bir madde satırı: onay kutusu, metin ve düzenlenirken metnin yerine geçen TextBox.</summary>
    private sealed class Row(Grid root, CheckBox box, TextBlock label, string text)
    {
        public Grid Root { get; } = root;
        public CheckBox Box { get; } = box;
        public TextBlock Label { get; } = label;
        public string Text { get; set; } = text;
        public TextBox? Editor { get; set; }
        public bool Done => Box.IsChecked == true;
    }

    private static readonly System.Windows.Media.FontFamily RowFont = new("Segoe UI Variable Text, Segoe UI");

    private readonly List<Row> _rows = [];
    private readonly Style _rowTextStyle, _rowBoxStyle;
    private Row? _editing;

    // Satırların ve "Yeni madde ekle…" kutusunun ortak sağ tık menüsü; hangi metin kutusunda açıldığı ContextMenuOpening'de yazılır.
    private readonly ContextMenu _rowMenu;
    private TextBox? _menuTarget;

    // Uzun liste açılırken önce ekranı dolduracak kadar satır kurulur, gerisi hemen ardından (boşta) eklenir: widget
    // maddelerin sayısından bağımsız olarak beklemeden görünür. Henüz kurulmamış maddeler (_pending) listenin sonudur;
    // _rows'a sıra numarasıyla erişen her işlem önce AttachAll çağırır. ChecklistRows.Children her zaman _rows'tur.
    private const int FirstRows = 40;
    private List<ChecklistItem> _pending = [];
    private int _attachGeneration;

    private List<ChecklistItem> CurrentItems() => _rows.Select(r => new ChecklistItem(r.Text, r.Done)).Concat(_pending).ToList();

    /// <summary>Satırları baştan kurar (açılış, kip değişimi, temizleme). Yazarken satırlar yeniden kurulmaz.</summary>
    private void RebuildRows(IEnumerable<ChecklistItem> items)
    {
        ClearRows();
        var all = items.ToList();
        foreach (var item in all.Take(FirstRows)) AddRowAtEnd(item);
        if (all.Count > FirstRows)
        {
            _pending = all.Skip(FirstRows).ToList();
            var generation = _attachGeneration;
            var clock = DebugLog.Enabled ? Stopwatch.StartNew() : null;
            Dispatcher.BeginInvoke(() =>
            {
                if (generation != _attachGeneration) return;
                var count = _pending.Count;
                AttachAll();
                if (clock is not null) DebugLog.Write($"[Note:{DebugLog.ShortId(_config.Id)}] kalan {count} madde eklendi: {clock.ElapsedMilliseconds} ms sonra");
            }, DispatcherPriority.Background);
        }
        UpdateProgress();
    }

    private void AddRowAtEnd(ChecklistItem item)
    {
        var row = CreateRow(item);
        _rows.Add(row);
        ChecklistRows.Children.Add(row.Root);
    }

    /// <summary>Henüz kurulmamış maddeleri (uzun listenin sonu) hemen kurar.</summary>
    private void AttachAll()
    {
        if (_pending.Count == 0) return;
        var pending = _pending;
        _pending = [];
        foreach (var item in pending) AddRowAtEnd(item);
    }

    private void ClearRows()
    {
        _attachGeneration++;
        _pending = [];
        _editing = null;
        _rows.Clear();
        ChecklistRows.Children.Clear();
    }

    private Row CreateRow(ChecklistItem item)
    {
        var box = new CheckBox { Style = _rowBoxStyle, IsChecked = item.Done, Margin = new Thickness(0, 2, 8, 0) };
        AutomationProperties.SetName(box, item.Text);
        // Kenar payı TextBox'ın metin payıyla aynı: düzenlemeye geçerken yazı yerinden oynamaz.
        var label = new TextBlock
        {
            Text = item.Text, TextWrapping = TextWrapping.Wrap, FontSize = NoteFontSize, FontFamily = RowFont,
            Margin = new Thickness(2, 0, 2, 0), Cursor = Cursors.IBeam,
        };
        Grid.SetColumn(label, 1);
        var root = new Grid { Background = System.Windows.Media.Brushes.Transparent, Margin = new Thickness(0, 1, 0, 1), ContextMenu = _rowMenu };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.Children.Add(box);
        root.Children.Add(label);

        var row = new Row(root, box, label, item.Text);
        PaintRow(row);
        // Click değil Checked/Unchecked: UI Automation'ın Toggle'ı ve Ctrl+Enter da aynı yoldan geçsin.
        box.Checked += (_, _) => DoneChanged(row);
        box.Unchecked += (_, _) => DoneChanged(row);
        // Metne (ya da satırın boş yerine) tıklamak, tıklanan yerde düzenlemeyi başlatır.
        root.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            BeginEdit(row, click: e.GetPosition(root));
        };
        // Satıra sağ tıklayınca menü o satırın metin kutusu için açılır (Kes/Kopyala/Yapıştır, Maddeyi sil…).
        root.ContextMenuOpening += (_, _) => _menuTarget = row.Editor ?? BeginEdit(row);
        return row;
    }

    private void PaintRow(Row row)
    {
        row.Label.Foreground = _palette.Foreground;
        row.Box.Foreground = _palette.Foreground;           // kenar
        row.Box.Tag = _palette.Accent;                      // işaretliyken zemin
        row.Box.Background = _palette.AccentForeground;     // ✓
        if (row.Editor is { } editor) PaintText(editor);
        ApplyDoneLook(row);
    }

    private void PaintText(TextBox box)
    {
        box.Foreground = _palette.Foreground;
        box.CaretBrush = _palette.Foreground;
        box.SelectionBrush = _palette.Accent;
    }

    /// <summary>
    /// Biten madde üstü çizili ve soluk görünür; "Bitenler alta insin" kapalıysa yerinde kalır. Soluk (yarı saydam) satırda
    /// ClearType kapanır (saydam katmanda renkli saçak bırakırdı).
    /// </summary>
    private void ApplyDoneLook(Row row)
    {
        var decorations = row.Done ? TextDecorations.Strikethrough : null;
        var opacity = row.Done ? 0.55 : 1;
        row.Label.TextDecorations = decorations;
        row.Label.Opacity = opacity;
        if (row.Editor is { } editor)
        {
            editor.TextDecorations = decorations;
            editor.Opacity = opacity;
        }
        ApplyRowClearType(row, ClearTypeText.Of(this));
    }

    private void DoneChanged(Row row)
    {
        ApplyDoneLook(row);
        if (_config.ChecklistDoneLast && !_applying) MoveDoneLast();
        UpdateProgress();
        QueueSave();
    }

    /// <summary>
    /// "Bitenler alta": açık maddeler üstte, bitenler altta (iki grup kendi sırasını korur). Satırlar yeniden kurulmaz,
    /// yalnızca yerleri değişir; düzenlenen madde odağını korur.
    /// </summary>
    private void MoveDoneLast()
    {
        AttachAll();
        var ordered = ChecklistText.DoneLast(_rows, r => r.Done);
        if (ordered.SequenceEqual(_rows)) return;
        var focused = _rows.FirstOrDefault(r => r.Editor?.IsKeyboardFocusWithin == true);
        var caret = focused?.Editor?.CaretIndex;
        _rows.Clear();
        _rows.AddRange(ordered);
        ChecklistRows.Children.Clear();
        foreach (var row in _rows) ChecklistRows.Children.Add(row.Root);
        if (focused?.Editor is { } editor) FocusText(editor, caret ?? int.MaxValue);
    }

    /// <summary>Satırın metnini ve işaretini değiştirir (taşıma, yapıştırma).</summary>
    private void SetContent(Row row, string text, bool done)
    {
        if (row.Editor is { } editor) editor.Text = text; // TextChanged row.Text'i günceller
        row.Text = text;
        row.Label.Text = text;
        row.Box.IsChecked = done;
        AutomationProperties.SetName(row.Box, text);
        ApplyDoneLook(row);
    }

    private void UpdateProgress()
    {
        if (!_config.NoteChecklist)
        {
            ProgressText.Visibility = Visibility.Collapsed;
            return;
        }
        var (done, total) = ChecklistText.Progress(CurrentItems().Where(i => i.Text.Trim().Length > 0));
        ProgressText.Text = $"{done}/{total}";
        ProgressText.Visibility = total > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Satırı düzenlemeye açar: metnin yerine aynı yazı biçiminde bir TextBox koyar ve odaklar. click (satıra göre)
    /// verilirse imleç tıklanan yere, yoksa caret konumuna (varsayılan: sona) konur.
    /// </summary>
    private TextBox BeginEdit(Row row, int caret = int.MaxValue, Point? click = null)
    {
        AttachAll();
        var editor = row.Editor;
        if (editor is null)
        {
            if (_editing is { } other && other != row) EndEdit(other);
            editor = new TextBox
            {
                Style = _rowTextStyle, Text = row.Text, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false,
                ContextMenu = _rowMenu, FontSize = NoteFontSize,
            };
            AutomationProperties.SetName(editor, L.T("Madde"));
            Grid.SetColumn(editor, 1);
            PaintText(editor);
            var current = editor;
            editor.TextChanged += (_, _) =>
            {
                row.Text = current.Text;
                AutomationProperties.SetName(row.Box, current.Text);
                UpdateProgress();
                QueueSave();
            };
            editor.PreviewKeyDown += (_, e) => OnRowKey(row, current, e);
            editor.ContextMenuOpening += (_, _) => _menuTarget = current;
            // Odak başka yere geçince (başka satır, dışarı tıklama, Esc) satır yeniden TextBlock olur. Sağ tık menüsü
            // açılırken odak menüye geçer: o sırada düzenleme sürer, menü kapanınca odak geri gelir.
            editor.LostKeyboardFocus += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                if (row.Editor == current && !current.IsKeyboardFocusWithin && !_rowMenu.IsOpen) EndEdit(row);
            }, DispatcherPriority.Input);
            DataObject.AddPastingHandler(editor, OnPaste);
            row.Editor = editor;
            _editing = row;
            ApplyDoneLook(row);
            row.Label.Visibility = Visibility.Collapsed;
            row.Root.Children.Add(editor);
        }
        if (click is { } point)
        {
            editor.UpdateLayout();
            caret = CaretAt(editor, row.Root.TranslatePoint(point, editor));
        }
        FocusText(editor, caret);
        return editor;
    }

    private void EndEdit(Row row)
    {
        if (row.Editor is not { } editor) return;
        row.Editor = null;
        if (_editing == row) _editing = null;
        row.Label.Text = row.Text;
        row.Label.Visibility = Visibility.Visible;
        row.Root.Children.Remove(editor);
    }

    /// <summary>Tıklanan noktaya en yakın imleç konumu (karakterin sağ yarısına tıklanınca ondan sonrası).</summary>
    private static int CaretAt(TextBox editor, Point point)
    {
        var index = editor.GetCharacterIndexFromPoint(point, snapToText: true);
        if (index < 0 || index >= editor.Text.Length) return editor.Text.Length;
        var leading = editor.GetRectFromCharacterIndex(index);
        var trailing = editor.GetRectFromCharacterIndex(index, trailingEdge: true);
        return point.X > (leading.X + trailing.X) / 2 ? index + 1 : index;
    }

    private Row InsertRow(int index, ChecklistItem item)
    {
        AttachAll();
        var row = CreateRow(item);
        index = Math.Clamp(index, 0, _rows.Count);
        _rows.Insert(index, row);
        ChecklistRows.Children.Insert(index, row.Root);
        return row;
    }

    private void DeleteRow(Row row)
    {
        AttachAll();
        EndEdit(row);
        if (!_rows.Remove(row)) return;
        ChecklistRows.Children.Remove(row.Root);
        StructureChanged();
    }

    /// <summary>Maddeyi bir yukarı/aşağı taşır (komşusuyla yer değiştirir); düzenleniyorsa düzenleme onunla gider.</summary>
    private void MoveRow(Row row, int delta)
    {
        AttachAll();
        var from = _rows.IndexOf(row);
        var to = from + delta;
        if (from < 0 || to < 0 || to >= _rows.Count) return;
        var other = _rows[to];
        var caret = row.Editor?.CaretIndex;
        EndEdit(row);
        EndEdit(other);
        var (text, done) = (row.Text, row.Done);
        SetContent(row, other.Text, other.Done);
        SetContent(other, text, done);
        if (caret is { } c) BeginEdit(other, c);
        StructureChanged();
    }

    private void StructureChanged()
    {
        UpdateProgress();
        QueueSave();
    }

    private void ClearDone()
    {
        var cleared = new List<(int Index, ChecklistItem Item)>();
        var kept = new List<ChecklistItem>();
        var items = CurrentItems();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Done && items[i].Text.Trim().Length > 0) cleared.Add((i, items[i]));
            else kept.Add(items[i]);
        }
        if (cleared.Count == 0) return;
        RebuildRows(kept);
        Save();
        // Soru yok: bildirimdeki "Geri al" maddeleri geri koyar. Not kimliğiyle bulunur (WidgetManager.ViewOf): widget o arada
        // yeniden kurulduysa yeni görünüme yazılır, kaldırıldıysa bir şey olmaz.
        var id = _config.Id;
        Views.Notice.Show(L.P(cleared.Count, "{0} biten madde temizlendi."), Views.NoticeKind.Info, L.T("Geri al"),
            () => (AppHost.Widgets.ViewOf(id) as NoteView)?.UndoClear(cleared), L.T("Geri almak için buraya tıkla."));
    }

    /// <summary>Temizlenen maddeleri eski yerlerine koyar (özgün sıra geri gelir); not o arada düz nota döndüyse bir şey yapmaz.</summary>
    private void UndoClear(List<(int Index, ChecklistItem Item)> cleared)
    {
        if (!_config.NoteChecklist) return;
        foreach (var (index, item) in cleared.OrderBy(c => c.Index)) InsertRow(index, item);
        UpdateProgress();
        Save();
    }

    /// <summary>Metin kutusuna odaklanır, imleci koyar ve görünür yere kaydırır (kutu henüz yerleşmediyse hemen ardından).</summary>
    private void FocusText(TextBox box, int caret = int.MaxValue)
    {
        void Apply()
        {
            if (!box.IsKeyboardFocused) Keyboard.Focus(box);
            box.CaretIndex = Math.Clamp(caret, 0, box.Text.Length);
            box.BringIntoView();
        }
        Apply();
        if (!box.IsKeyboardFocused) Dispatcher.BeginInvoke(Apply, DispatcherPriority.Loaded);
    }

    /// <summary>Esc: yazmayı bırakır ve hemen kaydeder.</summary>
    private void LeaveEditing()
    {
        Keyboard.ClearFocus();
        Save();
    }

    private static bool OnFirstLine(TextBox box) => box.GetLineIndexFromCharacterIndex(box.CaretIndex) <= 0;

    private static bool OnLastLine(TextBox box)
    {
        var line = box.GetLineIndexFromCharacterIndex(box.CaretIndex);
        return line < 0 || line >= box.LineCount - 1;
    }

    private void OnRowKey(Row row, TextBox editor, KeyEventArgs e)
    {
        // Alt basılıyken ok tuşları Key.System olarak gelir.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        AttachAll();
        var index = _rows.IndexOf(row);
        if (index < 0) return;
        switch (key)
        {
            case Key.Enter when modifiers == ModifierKeys.Control:
                row.Box.IsChecked = !row.Done; // Checked/Unchecked → DoneChanged
                break;
            case Key.Enter when modifiers == ModifierKeys.None:
                if (row.Text.Trim().Length == 0)
                {
                    // Boş satırda Enter listeden çıkar: boş satır kaldırılır, yeni madde kutusuna geçilir.
                    DeleteRow(row);
                    FocusText(AddBox);
                }
                else
                {
                    BeginEdit(InsertRow(index + 1, new ChecklistItem("", false)));
                    StructureChanged();
                }
                break;
            case Key.Back when modifiers == ModifierKeys.None && editor.Text.Length == 0:
                DeleteRow(row);
                if (index > 0) BeginEdit(_rows[index - 1]);
                else FocusText(AddBox);
                break;
            case Key.Up or Key.Down when modifiers == ModifierKeys.Alt:
                MoveRow(row, key == Key.Up ? -1 : 1);
                break;
            case Key.Up when modifiers == ModifierKeys.None && index > 0 && OnFirstLine(editor):
                BeginEdit(_rows[index - 1]);
                break;
            case Key.Down when modifiers == ModifierKeys.None && OnLastLine(editor):
                if (index < _rows.Count - 1) BeginEdit(_rows[index + 1]);
                else FocusText(AddBox);
                break;
            case Key.Escape:
                LeaveEditing();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnAddBoxKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        AttachAll();
        switch (key)
        {
            case Key.Enter when Keyboard.Modifiers == ModifierKeys.None:
                // Boşken Enter bir şey yapmaz; doluysa sona madde eklenir, kutu boşalır ve odak kutuda kalır.
                var text = AddBox.Text.Trim();
                if (text.Length > 0)
                {
                    InsertRow(_rows.Count, new ChecklistItem(text, false));
                    AddBox.Clear();
                    AddRow.BringIntoView();
                    StructureChanged();
                }
                break;
            case Key.Up when Keyboard.Modifiers == ModifierKeys.None && _rows.Count > 0:
                BeginEdit(_rows[^1]);
                break;
            case Key.Escape:
                LeaveEditing();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    /// <summary>Çok satırlı yapıştırma: her dolu satır ayrı madde olur ("☑"/"[x]" gibi önekler tanınır).</summary>
    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox box || !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true)) return;
        if (e.SourceDataObject.GetData(DataFormats.UnicodeText, true) is not string pasted || pasted.IndexOfAny(['\r', '\n']) < 0) return;
        e.CancelCommand();
        var items = ChecklistText.Parse(pasted);
        if (items.Count == 0) return;
        AttachAll();

        if (_rows.FirstOrDefault(r => r.Editor == box) is { } row)
        {
            // Boş satıra yapıştırılınca ilk madde o satıra yazılır, gerisi altına eklenir.
            var rest = items.AsEnumerable();
            if (row.Text.Trim().Length == 0)
            {
                SetContent(row, items[0].Text, items[0].Done);
                rest = items.Skip(1);
            }
            var last = row;
            foreach (var item in rest) last = InsertRow(_rows.IndexOf(last) + 1, item);
            BeginEdit(last);
        }
        else
        {
            foreach (var item in items) InsertRow(_rows.Count, item);
            AddRow.BringIntoView();
        }
        StructureChanged();
    }

    private void FillRowMenu(ContextMenu menu)
    {
        if (_menuTarget is not { } box) return;
        AttachAll();
        AddEditCommands(menu, box);
        if (_rows.FirstOrDefault(r => r.Editor == box) is { } row)
        {
            var index = _rows.IndexOf(row);
            menu.Items.Add(new Separator());
            menu.Items.Add(Menus.Item(L.T("Maddeyi sil"), () => DeleteRow(row)));
            var up = Menus.Item(L.T("Yukarı taşı"), () => MoveRow(row, -1));
            up.IsEnabled = index > 0;
            up.InputGestureText = "Alt+↑";
            menu.Items.Add(up);
            var down = Menus.Item(L.T("Aşağı taşı"), () => MoveRow(row, 1));
            down.IsEnabled = index < _rows.Count - 1;
            down.InputGestureText = "Alt+↓";
            menu.Items.Add(down);
        }
        AddNoteMenuRow(menu);
    }

    /// <summary>
    /// Başlığı gizli notta (Düzenle ▸ Göster ▸ Başlık kapalı; ⋯ ve × de başlıktadır) kartı yazı alanı ya da maddeler doldurur;
    /// notun menüsüne fareyle başka yol kalmaz: yazı alanı ve madde menülerinin sonunda "Not/Liste ayarları…" onu açar ("Düzenle…"
    /// orada ilk satırdır). Başlık görünürken yok.
    /// </summary>
    private void AddNoteMenuRow(ContextMenu menu)
    {
        if (_config.Shows("header")) return;
        menu.Items.Add(new Separator());
        menu.Items.Add(Menus.Item(_config.NoteChecklist ? L.T("Liste ayarları…") : L.T("Not ayarları…"),
            () => (Window.GetWindow(this) as WidgetWindow)?.OpenCardMenu(), KeyNames.WidgetMenu));
    }

    private static void AddEditCommands(ContextMenu menu, TextBox target)
    {
        menu.Items.Add(new MenuItem { Header = L.T("Kes"), Command = ApplicationCommands.Cut, CommandTarget = target });
        menu.Items.Add(new MenuItem { Header = L.T("Kopyala"), Command = ApplicationCommands.Copy, CommandTarget = target });
        menu.Items.Add(new MenuItem { Header = L.T("Yapıştır"), Command = ApplicationCommands.Paste, CommandTarget = target });
        menu.Items.Add(new MenuItem { Header = L.T("Tümünü seç"), Command = ApplicationCommands.SelectAll, CommandTarget = target });
    }
}
