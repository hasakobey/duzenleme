using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Widgets;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Views;

/// <summary>
/// Karşılama: 3 adımda bölmeler, dosya taşıma (açık onayla) ve araçlar + Windows ile başlatma. Seçimler bellekte tutulur ve
/// yalnızca "Bitti"de, pencere kapandıktan sonra <see cref="WelcomeSetup.Apply"/> ile uygulanır; yarıda kalırsa masaüstüne
/// hiçbir şey olmaz. "Şimdilik atla", ✕ ve Esc aynı işi görür: ilk açılışta karşılamayı tamamlanmış sayar (otomatik
/// taşıma kapalı kalır) ve ana pencereyi açar; yeniden kurulumda yalnızca kapanır. Animasyon yoktur: adım değişince
/// içerik anında değişir.
/// </summary>
public partial class WelcomeWindow : FluentWindow
{
    private static WelcomeWindow? _current;

    private readonly bool _rerun;
    /// <summary>Açılışta otomatik taşıma açık mıydı (yalnızca yeniden kurulumda olabilir; ilk açılışta hep kapalı)?</summary>
    private readonly bool _moveWasOn;
    private readonly List<string> _existingFolders;
    private int _step = 1;

    // 2. adım: masaüstündeki dosyalar açılışta bir kez, arka planda okunur; önizleme bellekte hesaplanır.
    private IReadOnlyList<DesktopFile> _files = [];
    private bool _filesReady;
    private bool _filesFailed;
    private readonly List<(FolderChoice Choice, ToggleButton Chip, TextBlock State)> _chips = [];
    /// <summary>Seçili klasörlerle şu an taşınacak dosya sayısı; null = bilinmiyor (okunuyor ya da okunamadı).</summary>
    private int? _pending;
    private bool _moveStepSeen;
    private bool _movePreselected;

    // 3. adım
    private readonly List<(WidgetChoice Choice, ToggleButton Tile)> _tools = [];
    private TextBlock? _clockPreview;
    private TextBlock? _datePreview;
    private readonly DispatcherTimer _clock;
    /// <summary>Değiştirilebilen başlangıç ayarının ilk okunan değeri; null = buradan değiştirilemez ya da henüz bilinmiyor.</summary>
    private bool? _startupInitial;

    private WelcomeChoices? _finished;
    private NativeMethods.POINT _near;
    private bool _closed;

    /// <summary>Karşılamayı açar; zaten açıksa öne getirir.</summary>
    /// <param name="rerun">Yeniden kurulum: var olanlar silinmez, yalnızca eksikler eklenir; "Vazgeç" yalnızca kapatır.</param>
    public static void ShowOrActivate(bool rerun)
    {
        if (_current is { } open)
        {
            if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
            open.Activate();
            return;
        }
        _current = new WelcomeWindow(rerun);
        _current.Show();
        _current.Activate();
    }

    private WelcomeWindow(bool rerun)
    {
        _rerun = rerun;
        _moveWasOn = !AppHost.Settings.Paused;
        InitializeComponent();
        Title = rerun ? $"{AppInfo.Name} kurulumu" : $"{AppInfo.Name}'e hoş geldin";
        TitleBar.Title = Title;
        // İmlecin monitöründe (testte DUZENLEME_WINDOW_AT noktasında) açılır ve oraya sığdırılır.
        WindowFit.Attach(this, onCursorMonitor: true);

        _existingFolders = ReadExistingFolders();
        SetupFences();
        SetupMove();
        SetupTools();
        SetupStartup();

        SkipButton.Content = rerun ? "Vazgeç" : "Şimdilik atla";
        SkipButton.Click += (_, _) => Close();
        BackButton.Click += (_, _) => ShowStep(_step - 1);
        NextButton.Click += (_, _) =>
        {
            if (_step < 3) ShowStep(_step + 1);
            else Finish();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Close();
        };

        // Saat kutucuğundaki saat eskimesin.
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _clock.Tick += (_, _) => UpdateToolPreviews();
        _clock.Start();

        Loaded += (_, _) => NextButton.Focus();
        ContentRendered += (_, _) => LogShown();
        Closed += OnClosed;

        ShowStep(1);
        LoadDesktopFiles();
    }

    // ---------------------------------------------------------------- adımlar

    private void ShowStep(int step)
    {
        _step = step;
        Step1.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        StepTitle.Text = step switch
        {
            1 => _rerun ? "Masaüstünü yeniden kuralım" : $"{AppInfo.Name}'e hoş geldin",
            2 => "Dosyalar kendiliğinden yerine gitsin mi?",
            _ => "Masaüstüne küçük araçlar ekleyelim mi?",
        };
        StepCounter.Text = $"Adım {step} / 3";
        // Geri 1. adımda yalnızca görünmez olur (yeri korunur): düğmeler kaymasın.
        BackButton.Visibility = step == 1 ? Visibility.Hidden : Visibility.Visible;
        NextButton.Content = step == 3 ? "Bitti" : "İleri";
        Dot1.SetResourceReference(Shape.FillProperty, DotBrush(1));
        Dot2.SetResourceReference(Shape.FillProperty, DotBrush(2));
        Dot3.SetResourceReference(Shape.FillProperty, DotBrush(3));
        Scroller.ScrollToTop();

        if (step == 2)
        {
            _moveStepSeen = true;
            TryPreselectMove();
        }
        if (step == 3)
        {
            UpdateToolPreviews();
            UpdateStartupTip();
        }
        UpdateNext();

        // Odak gizlenen bir düğmede (Geri) kalmasın. İleri kapalıysa (2. adım) odak içeriğe taşınmaz: odaklanan öğe
        // kendiliğinden görünüme kaydırılır ve önizleme yukarıda kalırdı.
        if (IsLoaded && NextButton.IsEnabled) NextButton.Focus();
    }

    private string DotBrush(int step) => step == _step ? "AccentFillColorDefaultBrush" : "ControlStrongStrokeColorDefaultBrush";

    /// <summary>2. adımda bir seçenek işaretlenmeden ilerlenmez (dosya taşıma açık onay ister).</summary>
    private void UpdateNext()
    {
        var ready = _step != 2 || MoveYes.IsChecked == true || MoveNo.IsChecked == true;
        NextButton.IsEnabled = ready;
        ChooseHint.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        Dots.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Finish()
    {
        var choices = new WelcomeChoices
        {
            Fences = Picked(FencesYes, FencesNo),
            IconsOnlyInFences = IconsOnly.IsChecked == true,
            AutoMove = Picked(MoveYes, MoveNo),
            MoveFolders = SelectedFolders(),
        };
        foreach (var (choice, tile) in _tools)
            if (tile.IsEnabled && tile.IsChecked == true) choices.Tools.Add(choice.Key);
        // Başlangıç ayarı yalnızca buradan değiştirilebiliyorsa ve kullanıcı değiştirdiyse uygulanır.
        var startOn = StartupToggle.IsChecked == true;
        if (_startupInitial is { } initial && StartupToggle.IsEnabled && StartupToggle.Visibility == Visibility.Visible && startOn != initial)
            choices.StartWithWindows = startOn;

        _finished = choices;
        _near = Center();
        Close();
    }

    private static bool? Picked(RadioButton yes, RadioButton no) =>
        yes.IsChecked == true ? true : no.IsChecked == true ? false : null;

    /// <summary>Pencerenin ortası (fiziksel piksel): yeni bölmeler ve araçlar bu ekrana yerleşir.</summary>
    private NativeMethods.POINT Center()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero && WindowState != WindowState.Minimized && NativeMethods.GetWindowRect(hwnd, out var r))
            return new NativeMethods.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 };
        NativeMethods.GetCursorPos(out var cursor);
        return cursor;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _current = null;
        _clock.Stop();
        // WPF ilk açılan pencereyi ana pencere yapar; kapanmış karşılama tema güncellemesinin hedefi olarak kalmasın.
        if (Application.Current is { } app && ReferenceEquals(app.MainWindow, this)) app.MainWindow = null;

        var finished = _finished;
        var near = _near;
        var rerun = _rerun;
        // Seçimler pencere kapandıktan sonra uygulanır. İş dağıtıcıya bırakılır: uygulama kapanırken (Çıkış, oturum
        // kapanması) pencereler kapatıldıktan hemen sonra dağıtıcı durur ve bekleyen işler atılır; böylece çıkışta hiçbir
        // seçim uygulanmaz, ilk açılış da tamamlanmış sayılmaz (sonraki açılışta karşılama yeniden gelir).
        Dispatcher.BeginInvoke(() =>
        {
            if (finished is not null) WelcomeSetup.Apply(finished, near);
            else if (!rerun) SkipFirstRun();
            else DebugLog.Write("karşılama kapatıldı (yeniden kurulum, değişiklik yok)");
        });
    }

    /// <summary>İlk açılışta atlama: hiçbir seçim uygulanmaz, otomatik taşıma kapalı kalır, ana pencere açılır.</summary>
    private static void SkipFirstRun()
    {
        AppHost.Settings.FirstRunDone = true;
        AppHost.SaveSettings();
        DebugLog.Write("karşılama atlandı");
        (Application.Current as App)?.ShowMainWindow();
    }

    private void LogShown()
    {
        if (!DebugLog.Enabled) return;
        using var process = Process.GetCurrentProcess();
        DebugLog.Write($"karşılama göründü ({(_rerun ? "yeniden kurulum" : "ilk açılış")}): süreç başlangıcından " +
                       $"{(DateTime.Now - process.StartTime).TotalMilliseconds:0} ms");
    }

    private static List<string> ReadExistingFolders()
    {
        try { return AppHost.Organizer.ExistingFolders().ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            DebugLog.Write($"karşılama: masaüstü klasörleri okunamadı: {ex.Message}");
            return [];
        }
    }

    // ---------------------------------------------------------------- 1. adım: bölmeler

    private void SetupFences()
    {
        Step1Text.Text = _rerun
            ? "Var olan bölmelerin, widget'ların ve dosyaların silinmez; yalnızca eksikler eklenir."
            : $"{AppInfo.Name} masaüstünü derli toplu tutar: simgeleri bölmelerde toplar, gelen dosyaları klasörlerine taşır, saat ve not gibi küçük araçlar ekler.";
        FencePlan.Text = Onboarding.FencePlanText(StarterFences.Plan(AppHost.Settings.Widgets, AppHost.Settings.Rules, _existingFolders));
        IconsOnly.IsChecked = !_rerun || AppHost.Settings.FencesReplaceIcons;
        IconsOnlyText.Text = "Windows'un kendi masaüstü simgeleri gizlenir, hiçbir şey iki kez görünmez. " +
                             $"Dosyaların yerinden oynamaz; {AppInfo.Name}'ten çıkınca simgeler geri gelir.";
        FencesYes.Checked += (_, _) => UpdateStartupTip();
        FencesNo.Checked += (_, _) => UpdateStartupTip();
        IconsOnly.Checked += (_, _) => UpdateStartupTip();
        IconsOnly.Unchecked += (_, _) => UpdateStartupTip();
    }

    // ---------------------------------------------------------------- 2. adım: dosya taşıma

    private void SetupMove()
    {
        PreviewNote.Text = $"Her taşıma {AppInfo.Name}'teki \"Otomatik taşıma\" sayfasından geri alınabilir; geri aldığın dosya bir daha " +
                           "taşınmaz. Kısayollar, klasörler ve inmekte olan dosyalar hiçbir zaman taşınmaz.";
        MoveNoText.Text = _moveWasOn
            ? "Otomatik taşıma kapatılır; hiçbir dosyaya dokunulmaz."
            : $"Hiçbir dosyaya dokunulmaz. İstediğin zaman {AppInfo.Name}'teki \"Otomatik taşıma\" sayfasından açabilirsin.";
        // Klasör seçenekleri masaüstü okununca kurulur; o zamana dek taşıma seçilemez (seçimsiz "Evet" kuralları kapatırdı).
        MoveYes.IsEnabled = MoveNo.IsEnabled = false;
        MoveYes.Checked += (_, _) => UpdateNext();
        MoveNo.Checked += (_, _) => UpdateNext();
        UpdatePreview();
    }

    /// <summary>
    /// Masaüstündeki dosyaları bir kez, arka planda okur (dosyalara ve kilide dokunmaz). Sonuç açıkça pencerenin
    /// iş parçacığına aktarılır (çağıranın eşitleme bağlamına güvenilmez).
    /// </summary>
    private void LoadDesktopFiles()
    {
        var organizer = AppHost.Organizer;
        Task.Run(() =>
        {
            var watch = Stopwatch.StartNew();
            IReadOnlyList<DesktopFile>? files = null;
            try { files = organizer.SnapshotFiles(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                DebugLog.Write($"karşılama: masaüstü okunamadı: {ex.Message}");
            }
            DebugLog.Write($"karşılama: masaüstü okundu ({files?.Count.ToString() ?? "?"} dosya), {watch.ElapsedMilliseconds} ms");
            Dispatcher.BeginInvoke(() => OnDesktopRead(files));
        });
    }

    /// <param name="files">null: masaüstü okunamadı (taşınacak dosya sayısı bilinmiyor).</param>
    private void OnDesktopRead(IReadOnlyList<DesktopFile>? files)
    {
        if (_closed) return;
        _files = files ?? [];
        _filesFailed = files is null;
        _filesReady = true;
        BuildFolderChips();
        MoveYes.IsEnabled = MoveNo.IsEnabled = true;
        UpdatePreview();
        TryPreselectMove();
        UpdateNext();
    }

    private void BuildFolderChips()
    {
        var style = (Style)FindResource("FolderChip");
        var createMissing = AppHost.Settings.CreateMissingFolders;
        var choices = Onboarding.FolderChoices(AppHost.Settings.Rules, _existingFolders, _files, createMissing);
        foreach (var choice in choices)
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = choice.Folder, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis,
            });
            var state = new TextBlock { FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis };
            state.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            content.Children.Add(state);

            var tip = choice.Extensions.Length > 0 ? $"Uzantılar: {choice.Extensions}" : null;
            var chip = new ToggleButton { Style = style, Content = content, IsChecked = choice.DefaultOn, ToolTip = tip };
            AutomationProperties.SetAutomationId(chip, "Welcome.Folder_" + choice.Folder);
            AutomationProperties.SetName(chip, choice.Folder);
            if (tip is not null) AutomationProperties.SetHelpText(chip, tip);
            // Click değil Checked/Unchecked: UI Automation'ın Toggle'ı Click olayı çıkarmaz.
            chip.Checked += (_, _) => OnFoldersChanged();
            chip.Unchecked += (_, _) => OnFoldersChanged();
            _chips.Add((choice, chip, state));
            FolderChips.Children.Add(chip);
        }
        if (choices.Count == 0)
            FolderHint.Text = $"Henüz taşıma kuralı yok; kuralları {AppInfo.Name}'teki \"Otomatik taşıma\" sayfasından ekleyebilirsin.";
        else if (createMissing)
            FolderHint.Text = "\"Klasör yoksa oluştur\" açık: işaretlediğin klasörler, oraya gidecek ilk dosya gelince oluşturulur.";
        UpdateChipStates();
    }

    private void OnFoldersChanged()
    {
        UpdateChipStates();
        UpdatePreview();
    }

    private void UpdateChipStates()
    {
        foreach (var (choice, chip, state) in _chips)
            state.Text = chip.IsChecked != true ? "kullanılmayacak" : choice.Exists ? "masaüstünde var" : "oluşturulacak";
    }

    private List<string> SelectedFolders() => _chips.Where(c => c.Chip.IsChecked == true).Select(c => c.Choice.Folder).ToList();

    /// <summary>Seçili klasörlerle şu an masaüstündeki hangi dosyalar taşınır? Bellekte hesaplanır; dosyalara dokunulmaz.</summary>
    private void UpdatePreview()
    {
        var selected = SelectedFolders();
        if (!_filesReady)
        {
            _pending = null;
            ShowPreview(warning: false, "Masaüstü inceleniyor…");
        }
        else if (selected.Count == 0)
        {
            _pending = 0;
            ShowPreview(warning: false, "Hiç klasör seçmedin; hiçbir dosya taşınmaz.");
        }
        else if (_filesFailed)
        {
            _pending = null;
            ShowPreview(warning: true, "Masaüstü okunamadı; taşınacak dosyalar şimdi gösterilemiyor.");
        }
        else
        {
            var watch = Stopwatch.StartNew();
            // "Bitti"de uygulanacak kurallarla ve açılacak klasörlerle; "Klasör yoksa oluştur" da hesaba katılır.
            var moves = Onboarding.PreviewMoves(AppHost.Settings.Rules, selected, _existingFolders, _files,
                AppHost.Settings.CreateMissingFolders);
            _pending = moves.Count;
            if (moves.Count == 0)
                ShowPreview(warning: false, "Şu an masaüstünde taşınacak dosya yok. Bundan sonra gelen dosyalar taşınır.");
            else
                ShowPreview(warning: true, $"Şu an masaüstünde duran {moves.Count} dosya da taşınacak:" +
                    string.Concat(Onboarding.MovePreviewLines(moves).Select(line => "\n    " + line)));
            if (DebugLog.Enabled)
                DebugLog.Write($"karşılama önizlemesi: {_files.Count} dosyadan {moves.Count} taşınacak, {watch.Elapsed.TotalMilliseconds:0.0} ms");
        }

        MoveYesText.Text = !_filesReady ? ""
            : selected.Count == 0 ? "Yukarıdan klasör seçersen uygun dosyalar oraya taşınır."
            : _pending switch
            {
                null => "Masaüstündeki ve bundan sonra gelen uygun dosyalar seçtiğin klasörlere taşınır.",
                0 => "Bundan sonra gelen dosyalar taşınır.",
                var n => $"{n} dosya taşınacak.",
            };
    }

    private void ShowPreview(bool warning, string text)
    {
        PreviewText.Text = text;
        PreviewIcon.Symbol = warning ? SymbolRegular.Warning24 : SymbolRegular.Info24;
        PreviewIcon.SetResourceReference(ForegroundProperty, warning ? "SystemFillColorCautionBrush" : "AccentTextFillColorPrimaryBrush");
        PreviewBox.SetResourceReference(Border.BackgroundProperty,
            warning ? "SystemFillColorCautionBackgroundBrush" : "SystemFillColorAttentionBackgroundBrush");
    }

    /// <summary>
    /// Ön seçim, adım ilk gösterildiğinde (masaüstü okunmuşsa) bir kez: taşıma zaten açıksa ya da şu an taşınacak dosya
    /// yoksa "Evet"; masaüstünde taşınacak dosya varsa ya da bilinmiyorsa hiçbiri (kullanıcı açıkça seçer).
    /// </summary>
    private void TryPreselectMove()
    {
        if (_movePreselected || !_moveStepSeen || !_filesReady) return;
        _movePreselected = true;
        if (MoveYes.IsChecked == true || MoveNo.IsChecked == true) return;
        if (_moveWasOn || _pending == 0) MoveYes.IsChecked = true;
        UpdateNext();
    }

    // ---------------------------------------------------------------- 3. adım: araçlar ve başlangıç

    private void SetupTools()
    {
        var style = (Style)FindResource("ToolTile");
        foreach (var choice in WidgetCatalog.Tools)
        {
            // Karşılama widget kaldırmaz ve aynı türden ikincisini eklemez.
            var onDesktop = choice.Matches is { } matches && AppHost.Settings.Widgets.Any(matches);

            FrameworkElement preview = choice.Key switch
            {
                "Clock" => _clockPreview = new TextBlock
                {
                    FontSize = 24, FontWeight = FontWeights.Light, FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                },
                "Date" => _datePreview = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold },
                _ => new SymbolIcon { Symbol = choice.Icon, FontSize = 30 },
            };
            preview.HorizontalAlignment = HorizontalAlignment.Center;
            preview.VerticalAlignment = VerticalAlignment.Center;

            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new Grid { Height = 40, Children = { preview } });
            content.Children.Add(new TextBlock
            {
                Text = choice.Label, Margin = new Thickness(0, 6, 0, 0), TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, MaxWidth = 92, HorizontalAlignment = HorizontalAlignment.Center,
            });
            if (onDesktop)
            {
                var badge = new TextBlock
                {
                    Text = "Masaüstünde var", FontSize = 11, Margin = new Thickness(0, 2, 0, 0),
                    TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                };
                badge.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
                content.Children.Add(badge);
            }

            var tile = new ToggleButton
            {
                Style = style, Content = content, ToolTip = choice.Tip, IsEnabled = !onDesktop,
                // Varsayılan: Saat ve Tarih.
                IsChecked = !onDesktop && (choice.Key is "Clock" or "Date"),
            };
            ToolTipService.SetShowOnDisabled(tile, true);
            AutomationProperties.SetAutomationId(tile, "Welcome.Tool_" + choice.Key);
            AutomationProperties.SetName(tile, choice.Label);
            AutomationProperties.SetHelpText(tile, choice.Tip);
            _tools.Add((choice, tile));
            ToolTiles.Children.Add(tile);
        }
        UpdateToolPreviews();
    }

    private void UpdateToolPreviews()
    {
        var now = DateTime.Now;
        if (_clockPreview is not null) _clockPreview.Text = now.ToString("HH:mm", UiText.Tr);
        if (_datePreview is not null) _datePreview.Text = now.ToString("d MMM", UiText.Tr);
    }

    /// <summary>"Windows ile başlat" kartı gerçek durumu gösterir (paketsizde Run kaydı, Store'da başlangıç görevi).</summary>
    private void SetupStartup()
    {
        StartupTitle.Text = $"Windows açılınca {AppInfo.Name} de başlasın";
        PortableNote.Visibility = AppHost.IsPortable ? Visibility.Visible : Visibility.Collapsed;
        StartupToggle.Checked += (_, _) => UpdateStartupTip();
        StartupToggle.Unchecked += (_, _) => UpdateStartupTip();
        StartupSettingsButton.Click += (_, _) => WelcomeSetup.OpenStartupSettings();
        StartupTip.Text = $"İpucu: {AppInfo.Name} Windows ile başlamazsa masaüstü simgelerin, sen {AppInfo.Name}'i açana dek her zamanki gibi görünür.";
        TrayInfo.Text = TrayInfoText();

        if (AppHost.IsTestDesktop)
        {
            // Test örneği gerçek başlangıç kaydını değiştirmez.
            StartupToggle.IsChecked = StartupRegistration.IsEnabled;
            StartupToggle.IsEnabled = false;
            ShowStartupNote("Test örneğinde değiştirilmez.");
        }
        else if (!PackageInfo.IsPackaged)
        {
            _startupInitial = StartupRegistration.IsEnabled;
            StartupToggle.IsChecked = _startupInitial;
        }
        else
        {
            // Store sürümü: durum gelene dek anahtar dokunulmaz.
            StartupToggle.IsEnabled = false;
            LoadStartupTask();
        }
    }

    private void LoadStartupTask() =>
        StartupRegistration.GetTaskStateAsync().ContinueWith(t =>
            Dispatcher.BeginInvoke(() => ShowStartupTask(t.IsCompletedSuccessfully ? t.Result : null)), TaskScheduler.Default);

    private void ShowStartupTask(StartupTaskState? state)
    {
        var view = PackagedApp.DescribeStartupTask(state);
        if (_closed) return;
        StartupToggle.IsChecked = view.IsOn;
        if (view.ShowToggle)
        {
            _startupInitial = view.IsOn;
            StartupToggle.IsEnabled = true;
        }
        else
        {
            // Kullanıcı Windows'tan kapattıysa ya da ilke yönetiyorsa yalnızca Windows'un ayar sayfasından değişir.
            StartupToggle.Visibility = Visibility.Collapsed;
            StartupSettingsButton.Visibility = Visibility.Visible;
        }
        if (view.Note is { } note) ShowStartupNote(note);
        UpdateStartupTip();
    }

    private void ShowStartupNote(string text)
    {
        StartupNote.Text = text;
        StartupNote.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Simgeler yalnızca bölmelerde olacaksa ve uygulama Windows ile başlamayacaksa: oturum açılınca simgeler görünür.
    /// Anahtar açıkken yeri korunur (Hidden): anahtarla oynarken alttaki metin kaymasın.
    /// </summary>
    private void UpdateStartupTip()
    {
        var iconsOnly = FencesYes.IsChecked == true && IconsOnly.IsChecked == true;
        StartupTip.Visibility = !iconsOnly ? Visibility.Collapsed
            : StartupToggle.IsChecked == true ? Visibility.Hidden : Visibility.Visible;
    }

    private static string TrayInfoText()
    {
        var start = $"{AppInfo.Name} saatin yanındaki simgede (tepside) çalışır. Yeni bir şey eklemek için ";
        var works = Hotkey.TryParse(AppHost.Settings.Hotkeys.QuickAdd, out var hotkey) &&
                    AppHost.Hotkeys?.Failures.ContainsKey(HotkeyAction.QuickAdd) != true;
        return works
            ? start + $"{hotkey} tuşlarına bas ya da o simgeye sağ tıkla → Widget ekle."
            : start + "o simgeye sağ tıkla → Widget ekle.";
    }
}
