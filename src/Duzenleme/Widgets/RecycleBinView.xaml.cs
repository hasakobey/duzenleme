using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;
using Duzenleme.Desktop;

namespace Duzenleme.Widgets;

/// <summary>
/// Geri Dönüşüm Kutusu widget'ı (<see cref="WidgetVariants.Recycle"/>; temel türü Launcher, tek öğesi kutunun kendisi: 2.0
/// ve alt türü bilmeyen sürüm içinde Geri Dönüşüm Kutusu duran bir kısayol kutusu görür). Sayı/boyut arka planda okunur,
/// değişiklik bildirimiyle (SHChangeNotifyRegister) ve yedek olarak dakikada bir, yalnızca görünürken yenilenir.
/// "Boşalt…" açık onay ister; sürüklenen dosyalar kutuya gider (Windows'un geri alınabilir silmesi).
/// </summary>
public partial class RecycleBinView : UserControl, IWidgetView
{
    private const double IconDip = 56;
    private static readonly TimeSpan Fallback = TimeSpan.FromSeconds(60);

    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _poll;
    private readonly TitleEditor _titleEditor;
    private WidgetPalette _palette = WidgetPalette.Glass;
    private RecycleBinInfo? _info;
    private bool _live, _querying, _again;
    private int _iconPixels;

    public RecycleBinView(WidgetConfig config)
    {
        _config = config;
        InitializeComponent();
        // Başlık hep yazılı kalır (boş bırakılırsa varsayılan ad): alt türü tanımayan sürüm (2.0) de kutuyu bu adla gösterir.
        _titleEditor = new TitleEditor(this, TitleRow, TitleText, iconButton: null, () => DefaultTitle,
            title =>
            {
                _config.Title = title ?? DefaultTitle;
                AppHost.SaveSettings();
                Render();
            },
            pickIcon: null, refit: () => { });
        _poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = Fallback };
        _poll.Tick += (_, _) => Refresh();
        AutomationProperties.SetName(EmptyButton, L.T("Geri Dönüşüm Kutusu'nu boşalt"));
        EmptyButton.ToolTip = L.T("Geri Dönüşüm Kutusu'ndaki her şeyi kalıcı olarak siler (önce sorar)");
        BinIcon.ToolTip = L.T("Açmak için çift tıkla");
        AutomationProperties.SetName(BinIcon, L.T("Geri Dönüşüm Kutusu'nu aç"));
        BinIcon.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 2) return;
            e.Handled = true;
            TileItem.Launch(RecycleBin.ShellName);
        };
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        Drop += OnDrop;
        Loaded += (_, _) => LoadIcon(force: false);
        Render();
    }

    public bool Resizable => false;

    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;

    public void SetLive(bool live)
    {
        if (_live == live) return;
        _live = live;
        if (live)
        {
            RecycleBin.Changed += OnBinChanged;
            _poll.Start();
            Refresh();
        }
        else
        {
            RecycleBin.Changed -= OnBinChanged;
            _poll.Stop();
        }
    }

    private void OnBinChanged()
    {
        // Dolu/boş simgesi değişmiş olabilir.
        ShellIcons.ForgetShell(RecycleBin.ShellName);
        LoadIcon(force: true);
        Refresh();
    }

    /// <summary>Sayı ve boyut arka planda; sürerken yeni istek gelirse bir kez daha okunur.</summary>
    private async void Refresh()
    {
        if (_querying)
        {
            _again = true;
            return;
        }
        _querying = true;
        try
        {
            do
            {
                _again = false;
                var info = await RecycleBin.QueryAsync();
                var changed = info != _info;
                _info = info;
                if (changed) Render();
            } while (_again);
        }
        finally { _querying = false; }
    }

    /// <summary>Simge ekrandaki gerçek piksel boyutunda (ölçek ve DPI) arka planda yüklenir.</summary>
    private void LoadIcon(bool force)
    {
        var dpi = PresentationSource.FromVisual(this) is not null ? VisualTreeHelper.GetDpi(this).PixelsPerDip : WidgetWindow.ExpectedPixelsPerDip(_config);
        var pixels = IconSizing.DevicePixels(IconDip, dpi, _config.Scale);
        if (!force && pixels == _iconPixels) return;
        _iconPixels = pixels;
        if (ShellIcons.TryCached(RecycleBin.ShellName, pixels, false, false, out var cached))
        {
            BinIcon.Source = cached;
            return;
        }
        ShellIcons.Request(RecycleBin.ShellName, pixels, false, icon =>
        {
            if (_iconPixels == pixels && icon is not null) BinIcon.Source = icon;
        });
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        LoadIcon(force: false);
    }

    private string Title => WidgetText.DisplayName(_config);

    private static string DefaultTitle => L.T("Geri Dönüşüm Kutusu");

    private void Render()
    {
        TitleText.Text = Title;
        SizeText.Text = _info switch
        {
            null => "…",
            { IsEmpty: true } => L.T("Boş", "kutu"),
            { } info => L.F("{0} · {1}", L.P(info.Items, "{0} öğe"), MeasureText.Bytes(info.Bytes)),
        };
        EmptyButton.IsEnabled = _info is not { IsEmpty: true };
        EmptyButton.Foreground = EmptyButton.IsEnabled ? _palette.Foreground : _palette.Secondary;
        SizeText.Visibility = _config.Shows("size") ? Visibility.Visible : Visibility.Collapsed;
        EmptyButton.Visibility = _config.Shows("empty") ? Visibility.Visible : Visibility.Collapsed;
        RemoveButton.Visibility = !_config.Locked && _config.Shows(Menus.ClosePart.Key) ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(this, $"{Title}, {SizeText.Text}");
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        _titleEditor.ApplyPalette(palette);
        TitleText.Foreground = palette.Foreground;
        SizeText.Foreground = palette.Secondary;
        RemoveButton.Foreground = palette.Foreground;
        DropOverlay.BorderBrush = palette.Accent;
        DropOverlay.Background = palette.Background;
        DropText.Foreground = palette.Foreground;
        ClearTypeText.Follow(this, TitleText, SizeText, EmptyButton);
        LoadIcon(force: false); // ölçek değiştiyse
        Render();
    }

    private void Empty_Click(object sender, RoutedEventArgs e) => RecycleBinActions.EmptyWithConfirm();

    // --- Sürükle-bırak: dosyalar Geri Dönüşüm Kutusu'na (Windows'un geri alınabilir silmesi, kendi ilerlemesiyle) ---

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var ok = e.Data.GetDataPresent(DataFormats.FileDrop) && e.AllowedEffects.HasFlag(DragDropEffects.Move);
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        DropOverlay.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (!e.AllowedEffects.HasFlag(DragDropEffects.Move) || e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;
        var dispatcher = Dispatcher;
        ShellFileOperations.RunSta(() =>
        {
            foreach (var path in paths)
            {
                if (!File.Exists(path) && !Directory.Exists(path)) continue;
                ShellFileOperations.Recycle(path);
            }
        }).ContinueWith(t =>
        {
            if (t.Exception?.GetBaseException() is { } ex and not OperationCanceledException)
                dispatcher.BeginInvoke(() => MessageBox.Show(ex.Message, AppInfo.Name));
        }, TaskScheduler.Default);
    }

    public void AddMenuItems(WidgetMenu menu)
    {
        menu.Primary.Add(Menus.Item(L.T("Geri Dönüşüm Kutusu'nu aç"), () => TileItem.Launch(RecycleBin.ShellName)));
        var empty = Menus.Item(L.T("Geri Dönüşüm Kutusu'nu boşalt…"), RecycleBinActions.EmptyWithConfirm);
        empty.IsEnabled = _info is not { IsEmpty: true };
        menu.Primary.Add(empty);
        menu.Primary.Add(Menus.Item(L.T("Yeniden adlandır"), () => TryBeginRename(), KeyNames.F2));
        menu.Appearance.Add(Menus.Parts(_config, [("size", L.N("Öğe sayısı ve boyut")), ("empty", L.N("Boşalt düğmesi")), Menus.ClosePart], Render));
        menu.More.Add(Menus.Item(L.T("Yenile"), Refresh));
    }

    /// <summary>F2 ve "Yeniden adlandır": başlık yerinde düzenlenir (boş bırakılırsa varsayılan ada döner).</summary>
    public bool TryBeginRename() => _titleEditor.Begin();

    private void RemoveWidget_Click(object sender, RoutedEventArgs e) => AppHost.Widgets.RemoveWithUndo(_config.Id);

    public void Detach()
    {
        _titleEditor.Cancel();
        _poll.Stop();
        if (_live) RecycleBin.Changed -= OnBinChanged;
        _live = false;
    }
}
