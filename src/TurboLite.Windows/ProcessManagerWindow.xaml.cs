using System.Windows;
using System.Windows.Controls;
using TurboLite.Windows.Services;

namespace TurboLite.Windows;

public partial class ProcessManagerWindow : Window
{
    private readonly ProcessManagerService _service = new();
    private readonly ActionLogService _log = new();
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
            StatusText.Text = "Ordenado por memória utilizada. A proteção por nome é conservadora, mas não identifica todos os processos críticos.";
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
                ? $"Protegido: {entry.Name} (PID {entry.Id}) — {entry.ProtectionReason}. CPU: {entry.CpuText}; RAM: {entry.MemoryText}."
                : $"Selecionado: {entry.Name} (PID {entry.Id}) — CPU: {entry.CpuText}; RAM: {entry.MemoryText}. Primeiro será tentado o fechamento normal, se houver janela.";
        }
        UpdateTerminateButton();
    }

    private void UpdateTerminateButton()
    {
        TerminateButton.IsEnabled = !_loading &&
            ProcessGrid.SelectedItem is ProcessEntry entry &&
            !entry.IsProtected;
    }

    private async void Terminate_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessGrid.SelectedItem is not ProcessEntry entry || entry.IsProtected)
            return;

        var result = MessageBox.Show(
            $"Fechar '{entry.Name}' (PID {entry.Id})?\n\nMemória em uso: {entry.MemoryText}.\n" +
            "O aplicativo receberá primeiro um pedido normal de fechamento, quando possível. Salve seu trabalho antes de continuar.",
            "Confirmar fechamento",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            bool closed = await Task.Run(() => _service.TryCloseGracefully(entry.Id));
            if (!closed)
            {
                var force = MessageBox.Show(
                    $"'{entry.Name}' não fechou normalmente ou não tem uma janela principal.\n\n" +
                    "Forçar o encerramento pode causar perda de dados não salvos. Deseja forçar mesmo assim?",
                    "Confirmar encerramento forçado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (force != MessageBoxResult.Yes)
                {
                    StatusText.Text = "Encerramento forçado cancelado pelo usuário.";
                    return;
                }

                await Task.Run(() => _service.ForceTerminate(entry.Id));
            }

            _log.Write("Processos", "Encerrar processo", "Sucesso", $"{entry.Name} (PID {entry.Id})");
            StatusText.Text = $"Solicitação concluída para {entry.Name}. Atualizando lista…";
            await RefreshProcessesAsync();
        }
        catch (Exception ex)
        {
            _log.Write("Processos", "Encerrar processo", "Falha", $"{entry.Name} (PID {entry.Id}): {ex.Message}");
            MessageBox.Show(
                $"Não foi possível encerrar o processo.\n\n{ex.Message}",
                "Encerramento não concluído",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
