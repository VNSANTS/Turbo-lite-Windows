using System.Windows;
using TurboLite.Windows.Services;

namespace TurboLite.Windows;

public partial class HistoryWindow : Window
{
    private readonly ActionLogService _service = new();

    public HistoryWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshEntries();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshEntries();

    private void RefreshEntries()
    {
        var entries = _service.GetRecent();
        HistoryGrid.ItemsSource = entries;
        CountText.Text = $"{entries.Count} registros recentes";
    }
}
