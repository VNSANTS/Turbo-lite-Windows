using System.Windows;
using System.Windows.Controls;
using TurboLite.Windows.Services;

namespace TurboLite.Windows;

public partial class ProcessManagerWindow : Window
{
    private readonly ProcessManagerService _service = new();
    private bool _loading;

    public ProcessManagerWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshProcessesAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshProcessesAsync();

    private async Task RefreshProcessesAsync()
    {
        if (_loading) return;
        _loading = true;
        TerminateButton.IsEnabled = false;
        CountText.Text = "Atualizando lista…";
        StatusText.Text = "Coletando processos. Alguns podem desaparecer ou negar acesso durante a leitura.";

        try
        {
            var processes = await Task.Run(_service.GetProcesses);
            ProcessGrid.ItemsSource = processes;
            CountText.Text = $"{processes.Count} processos encontrados";
            StatusText.Text = "Ordenado por memória utilizada. A proteção é conservadora, mas não substitui a análise do nome e do aplicativo.";
        }
        catch (Exception ex)
        {
            CountText.Text = "Falha ao consultar processos";
            StatusText.Text = $"Erro: {ex.Message}";
        }
        finally
        {
            _loading = false;
            UpdateTerminateButton();
        }
    }

    private void ProcessGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProcessGrid.SelectedItem is ProcessEntry entry)
        {
            StatusText.Text = entry.IsProtected
                ? $"Protegido: {entry.Name} (PID {entry.Id}) — {entry.ProtectionReason}."
                : $"Selecionado: {entry.Name} (PID {entry.Id}), {entry.MemoryText}. Encerre apenas se reconhecer o processo e tiver salvo seu trabalho.";
        }
        UpdateTerminateButton();
    }

    private void UpdateTerminateButton()
    {
        TerminateButton.IsEnabled = !_loading &&
            ProcessGrid.SelectedItem is ProcessEntry entry &&
            !entry.IsProtected;
    }

    private void Terminate_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessGrid.SelectedItem is not ProcessEntry entry || entry.IsProtected)
            return;

        var result = MessageBox.Show(
            $"Deseja realmente encerrar '{entry.Name}' (PID {entry.Id})?\n\n" +
            $"Memória em uso: {entry.MemoryText}.\n" +
            "O aplicativo pode perder dados não salvos. O Turbo Lite não consegue garantir que o processo seja seguro apenas pelo nome.",
            "Confirmar encerramento",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _service.Terminate(entry.Id);
            StatusText.Text = $"Solicitação de encerramento enviada para {entry.Name}. Atualizando lista…";
            _ = RefreshProcessesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Não foi possível encerrar o processo.\n\n{ex.Message}",
                "Encerramento não concluído",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
