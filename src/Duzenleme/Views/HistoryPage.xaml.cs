using System.IO;
using System.Windows;
using System.Windows.Controls;
using Duzenleme.Core;
using Wpf.Ui.Controls;

namespace Duzenleme.Views;

public partial class HistoryPage : Page
{
    private static HistoryPage? _current;

    public HistoryPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _current = this;
            AppHost.Journal.Changed += RefreshAsync;
            Feedback.IsOpen = false;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            if (_current == this) _current = null;
            AppHost.Journal.Changed -= RefreshAsync;
        };
    }

    private void RefreshAsync() => Dispatcher.BeginInvoke(Refresh);

    private void Refresh()
    {
        var rows = AppHost.Journal.Snapshot().Select(e => new MoveRow(e)).ToList();
        Entries.ItemsSource = rows;
        Empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Geri alır; sonucu (açıksa) geçmiş sayfasında, değilse iletişim kutusunda gösterir.</summary>
    public static void UndoWithFeedback(MoveEntry? entry)
    {
        if (entry is null)
        {
            Show("Geri alınacak bir şey yok", "Henüz taşınmış bir dosya yok.", InfoBarSeverity.Informational);
            return;
        }
        try
        {
            AppHost.Organizer.Undo(entry);
            Show("Geri alındı", $"{Path.GetFileName(entry.Source)} masaüstüne döndü.", InfoBarSeverity.Success);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Show("Geri alınamadı", ex.Message, InfoBarSeverity.Error);
        }
    }

    private static void Show(string title, string message, InfoBarSeverity severity)
    {
        if (_current is { } page)
        {
            page.Feedback.Title = title;
            page.Feedback.Message = message;
            page.Feedback.Severity = severity;
            page.Feedback.IsOpen = true;
        }
        else
        {
            System.Windows.MessageBox.Show(message, title);
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is MoveRow row) UndoWithFeedback(row.Entry);
    }

    private void UndoLast_Click(object sender, RoutedEventArgs e) => UndoWithFeedback(AppHost.Journal.LastActive());

    private void Clear_Click(object sender, RoutedEventArgs e) => AppHost.Journal.Clear();
}
