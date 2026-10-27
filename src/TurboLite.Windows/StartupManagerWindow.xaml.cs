using System.Windows;
using TurboLite.Windows.Services;

namespace TurboLite.Windows;

public partial class StartupManagerWindow : Window
{
    private readonly StartupManagerService _service = new();

    public StartupManagerWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshEntries();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshEntries();

    private void RefreshEntries()
    {
        try
        {
            var entries = _service.GetEntries();
            StartupGrid.ItemsSource = entries;
            CountText.Text = $"{entries.Count} itens encontrados";
        }
        catch (Exception ex)
        {
            CountText.Text = $"Não foi possível ler a inicialização: {ex.Message}";
        }
    }
}
