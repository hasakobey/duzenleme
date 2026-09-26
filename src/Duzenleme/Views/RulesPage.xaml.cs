using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Duzenleme.Core;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

/// <summary>Kural düzenleme satırı; değişiklikler anında ayarlara yazılır.</summary>
public sealed class RuleRow(Rule rule, Action changed) : INotifyPropertyChanged
{
    public Rule Rule { get; } = rule;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Enabled
    {
        get => Rule.Enabled;
        set { Rule.Enabled = value; Changed(); }
    }

    public string TargetFolder
    {
        get => Rule.TargetFolder;
        set
        {
            var clean = string.Concat(value.Trim().Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            if (clean == Rule.TargetFolder) return;
            Rule.TargetFolder = clean;
            Changed();
        }
    }

    public string ExtensionsText
    {
        get => string.Join(", ", Rule.Extensions);
        set
        {
            Rule.Extensions = value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(Rule.NormalizeExtension).Where(e => e.Length > 0).Distinct().ToList();
            Changed();
        }
    }

    public bool FolderExists => AppHost.Organizer.ExistingFolders().Any(f => FolderName.Equal(f, Rule.TargetFolder));
    public SymbolRegular StatusIcon => FolderExists ? SymbolRegular.CheckmarkCircle24 : SymbolRegular.Circle24;
    public Brush StatusBrush => (Brush)Application.Current.FindResource(FolderExists ? "SystemFillColorSuccessBrush" : "TextFillColorTertiaryBrush");
    public string StatusText => FolderExists ? "Var" : "Yok";
    public Visibility CreateVisibility => FolderExists || string.IsNullOrWhiteSpace(Rule.TargetFolder) ? Visibility.Collapsed : Visibility.Visible;

    private void Changed([CallerMemberName] string? name = null)
    {
        changed();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

public partial class RulesPage : Page
{
    public RulesPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        CreateMissing.IsChecked = AppHost.Settings.CreateMissingFolders;
        RuleList.ItemsSource = AppHost.Settings.Rules.Select(r => new RuleRow(r, Save)).ToList();
    }

    private static void Save() => AppHost.SaveSettings();

    private void CreateMissing_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.CreateMissingFolders = CreateMissing.IsChecked == true;
        Save();
        if (AppHost.Settings.CreateMissingFolders) AppHost.OrganizeNowInBackground();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        // Liste yerine yenisi atanır: izleyici iş parçacığı eski listeyi güvenle okumaya devam eder.
        AppHost.Settings.Rules = [.. AppHost.Settings.Rules, new Rule { TargetFolder = "Yeni klasör", Extensions = [] }];
        Save();
        Load();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RuleRow row) return;
        AppHost.Settings.Rules = AppHost.Settings.Rules.Where(r => r != row.Rule).ToList();
        Save();
        Load();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        AppHost.Settings.Rules = Rule.Defaults();
        Save();
        Load();
    }

    private void CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RuleRow row) return;
        Directory.CreateDirectory(Path.Combine(AppHost.DesktopDirectory, row.Rule.TargetFolder));
        Load();
        AppHost.OrganizeNowInBackground();
    }
}
