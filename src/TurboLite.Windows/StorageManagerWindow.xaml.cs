using System.Windows;
using TurboLite.Windows.Services;

namespace TurboLite.Windows;

public partial class StorageManagerWindow : Window
{
    private readonly StorageManagerService _service = new();

    public StorageManagerWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        CountText.Text = "Consultando volumes e discos…";
        try
        {
            var snapshot = await Task.Run(_service.GetSnapshot);
            VolumeList.ItemsSource = snapshot.Volumes.Select(v => new
            {
                v.Name,
                v.Label,
                v.Status,
                v.UsedPercent,
                UsageText = $"{v.UsedPercent:0.0}% usado • {v.FreeText} livre de {v.TotalText} • {v.Format}"
            }).ToArray();
            DiskGrid.ItemsSource = snapshot.PhysicalDisks;
            CountText.Text = $"{snapshot.Volumes.Count} volumes • {snapshot.PhysicalDisks.Count} discos físicos";
            NoteText.Text = snapshot.Note;
        }
        catch (Exception ex)
        {
            CountText.Text = $"Falha na consulta: {ex.Message}";
            NoteText.Text = "Nenhuma alteração foi feita no sistema.";
        }
    }
}
