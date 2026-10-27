using System.Windows;
using TurboLite.Windows.Services;

namespace TurboLite.Windows;

public partial class CleanupWindow : Window
{
    private readonly TempCleanupService _service = new();
    private TempCleanupPreview? _preview;
    private bool _busy;

    public CleanupWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadPreviewAsync();
    }

    private async void Preview_Click(object sender, RoutedEventArgs e) => await LoadPreviewAsync();

    private async Task LoadPreviewAsync()
    {
        if (_busy) return;
        _busy = true;
        CleanButton.IsEnabled = false;
        StatusText.Text = "Analisando pasta temporária…";
        try
        {
            _preview = await Task.Run(_service.Preview);
            FileGrid.ItemsSource = _preview.Files.Take(200).ToArray();
            SummaryText.Text = $"{_preview.Files.Count} arquivos encontrados • {SystemDiagnosticsService.FormatBytes(_preview.TotalBytes)} estimados";
            RootText.Text = $"Pasta: {_preview.RootPath} • {_preview.SkippedCount} item(ns) ignorado(s) por acesso, mudança ou link especial. A lista mostra até 200 maiores arquivos; a confirmação considera todos os arquivos encontrados.";
            StatusText.Text = "Prévia pronta. Nada foi excluído.";
            CleanButton.IsEnabled = _preview.Files.Count > 0;
        }
        catch (Exception ex)
        {
            _preview = null;
            FileGrid.ItemsSource = null;
            SummaryText.Text = "Não foi possível gerar a prévia";
            RootText.Text = ex.Message;
            StatusText.Text = "Nenhuma alteração foi feita.";
        }
        finally { _busy = false; }
    }

    private async void Clean_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _preview is null || _preview.Files.Count == 0) return;
        var answer = MessageBox.Show(
            $"Excluir os {_preview.Files.Count} arquivos temporários listados na prévia?\n\nTamanho estimado: {SystemDiagnosticsService.FormatBytes(_preview.TotalBytes)}.\n\nFeche aplicativos antes: alguns arquivos temporários podem estar em uso. O Turbo Lite não excluirá pastas nem arquivos fora da pasta temporária.",
            "Confirmar limpeza",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        _busy = true;
        CleanButton.IsEnabled = false;
        StatusText.Text = "Excluindo arquivos que ainda podem ser removidos…";
        try
        {
            var preview = _preview;
            var result = await Task.Run(() => _service.Clean(preview));
            MessageBox.Show(
                $"Arquivos excluídos: {result.DeletedCount}\nEspaço liberado: {SystemDiagnosticsService.FormatBytes(result.FreedBytes)}\nFalhas/arquivos em uso: {result.FailedCount}",
                "Resultado da limpeza", MessageBoxButton.OK, MessageBoxImage.Information);
            _preview = null;
            _busy = false;
            await LoadPreviewAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"A limpeza foi interrompida.\n\n{ex.Message}", "Falha na limpeza",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _busy = false; }
    }
}
