using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Duzenleme.Core;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Duzenleme.Icons;

/// <summary>Klasöre hazır, dosyadan ya da yapay zekâyla üretilmiş simge atama penceresi.</summary>
public partial class FolderIconWindow : FluentWindow
{
    private static readonly Dictionary<string, FolderIconWindow> Open = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _folder;
    private IconGlyph _glyph;
    private IconColor _color;
    private Drawing? _custom;
    private string _customLabel = "";

    public static event Action<string>? IconChanged;

    private static string AiDirectory => System.IO.Path.Combine(AppHost.DataDirectory, "ai-icons");

    public static void ShowFor(string folder)
    {
        if (Open.TryGetValue(folder, out var existing))
        {
            existing.Activate();
            return;
        }
        var window = new FolderIconWindow(folder);
        Open[folder] = window;
        window.Closed += (_, _) => Open.Remove(folder);
        window.Show();
        window.Activate();
    }

    private FolderIconWindow(string folder)
    {
        _folder = folder;
        InitializeComponent();
        Views.WindowFit.Attach(this);
        var name = System.IO.Path.GetFileName(folder.TrimEnd('\\'));
        (_glyph, _color) = FolderIconCatalog.Suggest(name);
        TitleBar.Title = $"Klasör simgesi — {name}";
        FolderNameText.Text = name;

        BuildColors();
        BuildGlyphs();
        LoadHistory();
        var hasKey = ApiKeyStore.HasKey;
        NoKeyInfo.IsOpen = !hasKey;
        NoKeyInfo.Visibility = hasKey ? Visibility.Collapsed : Visibility.Visible;
        GenerateButton.IsEnabled = hasKey;
        Prompt.IsEnabled = hasKey;
        Prompt.Text = name;
        UpdatePreview();
        // Esc kapatır (öteki araç pencereleri gibi); üretim sürerken kapanmaz (sonuç yarıda kalmasın).
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || Busy.Visibility == Visibility.Visible) return;
            e.Handled = true;
            Close();
        };
    }

    private Drawing CurrentDrawing => _custom ?? FolderIconRenderer.FolderDrawing(_color, _glyph.Glyph);

    private void UpdatePreview()
    {
        Preview.Source = FolderIconRenderer.Render(CurrentDrawing, 320);
        SourceText.Text = _custom is not null ? _customLabel : $"{_glyph.Label} · {_color.Label}";
    }

    private void BuildColors()
    {
        ColorList.Items.Clear();
        foreach (var color in FolderIconCatalog.Colors)
        {
            var selected = _custom is null && color == _color;
            var dot = new Ellipse
            {
                Width = 30, Height = 30, Margin = new Thickness(0, 0, 10, 8), Cursor = Cursors.Hand, ToolTip = color.Label,
                Fill = new LinearGradientBrush(Color.FromArgb(255, (byte)(color.Front >> 16), (byte)(color.Front >> 8), (byte)color.Front),
                                               Color.FromArgb(255, (byte)(color.Back >> 16), (byte)(color.Back >> 8), (byte)color.Back), 90),
                Stroke = selected ? (Brush)FindResource("TextFillColorPrimaryBrush") : Brushes.Transparent,
                StrokeThickness = 3,
            };
            dot.MouseLeftButtonUp += (_, _) => { _color = color; _custom = null; BuildColors(); BuildGlyphs(); UpdatePreview(); };
            ColorList.Items.Add(dot);
        }
    }

    private void BuildGlyphs()
    {
        GlyphList.Items.Clear();
        foreach (var glyph in FolderIconCatalog.Glyphs)
        {
            var selected = _custom is null && glyph == _glyph;
            var tile = new Button
            {
                Width = 92, Height = 96, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(4), Cursor = Cursors.Hand,
                BorderThickness = new Thickness(selected ? 2 : 1),
                BorderBrush = selected ? (Brush)FindResource("AccentTextFillColorPrimaryBrush") : (Brush)FindResource("CardStrokeColorDefaultBrush"),
                Content = new StackPanel
                {
                    Children =
                    {
                        new Image { Source = FolderIconRenderer.Render(FolderIconRenderer.FolderDrawing(_color, glyph.Glyph), 96), Width = 52, Height = 52 },
                        new TextBlock { Text = glyph.Label, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0),
                                        TextTrimming = TextTrimming.CharacterEllipsis },
                    },
                },
            };
            tile.Click += (_, _) =>
            {
                _glyph = glyph;
                _custom = null;
                BuildGlyphs();
                BuildColors();
                UpdatePreview();
            };
            GlyphList.Items.Add(tile);
        }
    }

    private void LoadHistory()
    {
        HistoryList.Items.Clear();
        var files = Directory.Exists(AiDirectory)
            ? new DirectoryInfo(AiDirectory).GetFiles("*.svg").OrderByDescending(f => f.LastWriteTime).Take(12).ToList()
            : [];
        HistoryHeader.Visibility = files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var file in files)
        {
            Drawing drawing;
            try { drawing = AiIconGenerator.ToDrawing(File.ReadAllText(file.FullName)); }
            catch (Exception) { continue; }
            var label = System.IO.Path.GetFileNameWithoutExtension(file.Name);
            var tile = new Button
            {
                Width = 64, Height = 64, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(4), ToolTip = label,
                Content = new Image { Source = FolderIconRenderer.Render(drawing, 96), Width = 48, Height = 48 },
            };
            tile.Click += (_, _) => SetCustom(drawing, "Yapay zekâ · " + label);
            HistoryList.Items.Add(tile);
        }
    }

    private void SetCustom(Drawing drawing, string label)
    {
        _custom = drawing;
        _customLabel = label;
        BuildColors();
        BuildGlyphs();
        UpdatePreview();
    }

    private void Show(string title, string message, InfoBarSeverity severity)
    {
        Feedback.Title = title;
        Feedback.Message = message;
        Feedback.Severity = severity;
        Feedback.IsOpen = true;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            FolderIconService.Apply(_folder, CurrentDrawing);
            Widgets.ShellIcons.Forget(_folder);
            IconChanged?.Invoke(_folder);
            Show("Uygulandı", "Masaüstü birkaç saniye içinde yeni simgeyi gösterir.", InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            Show("Uygulanamadı", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            FolderIconService.Reset(_folder);
            Widgets.ShellIcons.Forget(_folder);
            IconChanged?.Invoke(_folder);
            Show("Varsayılana döndü", "Klasör standart Windows simgesini kullanıyor.", InfoBarSeverity.Informational);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Show("Geri alınamadı", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void LoadFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Simge ve resimler|*.ico;*.png;*.jpg;*.jpeg;*.svg|Tüm dosyalar|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var path = dialog.FileName;
            Drawing drawing;
            if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                drawing = AiIconGenerator.ToDrawing(AiIconGenerator.Sanitize(File.ReadAllText(path)));
            }
            else
            {
                var decoder = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
                drawing = new ImageDrawing(frame, new Rect(0, 0, 256, 256 * frame.PixelHeight / (double)frame.PixelWidth));
                drawing.Freeze();
            }
            SetCustom(drawing, "Dosya · " + System.IO.Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            Show("Dosya okunamadı", ex.Message, InfoBarSeverity.Error);
        }
    }

    /// <summary>Yapay zekânın ürettiği uygunsuz içeriği bildirme: tarayıcıda önceden doldurulmuş, boş bir GitHub sorunu açar.</summary>
    private void Report_Click(object sender, RoutedEventArgs e) => Views.Browser.Open(SupportLinks.ReportAiContent(AppInfo.Version));

    private void Privacy_Click(object sender, RoutedEventArgs e) => Views.Browser.Open(AppInfo.PrivacyUrl);

    private void Prompt_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && GenerateButton.IsEnabled) Generate_Click(sender, e);
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyStore.Load();
        if (key is null) return;
        GenerateButton.IsEnabled = false;
        Busy.Visibility = Visibility.Visible;
        Feedback.IsOpen = false;
        try
        {
            var name = System.IO.Path.GetFileName(_folder.TrimEnd('\\'));
            var svg = await AiIconGenerator.GenerateSvgAsync(key, name, Prompt.Text);
            var drawing = AiIconGenerator.ToDrawing(svg);

            Directory.CreateDirectory(AiDirectory);
            var safe = string.Concat((Prompt.Text.Length > 0 ? Prompt.Text : name).Take(40).Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            File.WriteAllText(System.IO.Path.Combine(AiDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss} {safe}.svg"), svg);

            SetCustom(drawing, "Yapay zekâ · " + Prompt.Text);
            LoadHistory();
        }
        catch (AiIconException ex)
        {
            Show("Üretilemedi", ex.Message, InfoBarSeverity.Warning);
        }
        catch (Exception ex)
        {
            Show("Beklenmeyen hata", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            GenerateButton.IsEnabled = true;
            Busy.Visibility = Visibility.Collapsed;
        }
    }
}
