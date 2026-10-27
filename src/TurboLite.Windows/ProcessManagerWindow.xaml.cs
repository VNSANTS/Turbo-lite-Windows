using System.Windows;
using System.Windows.Controls;
using TurboLite.Windows.Services;

namespace TurboLite.Windows;

public partial class ProcessManagerWindow : Window
{
    private readonly ProcessManagerService _service = new();
    private readonly ActionLogService _log = new();
    private readonly RamOptimizerService _ramOptimizer = new();
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
        TrimRamButton.IsEnabled = false;
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
        bool eligible = !_loading && ProcessGrid.SelectedItem is ProcessEntry entry && !entry.IsProtected && entry.Id > 4;
        TerminateButton.IsEnabled = eligible;
        TrimRamButton.IsEnabled = eligible;
    }


    private async void TrimRam_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessGrid.SelectedItem is not ProcessEntry entry || entry.IsProtected || entry.Id <= 4) return;

        var result = MessageBox.Show(
            $"Solicitar ao Windows a liberação de páginas do conjunto de trabalho de '{entry.Name}' (PID {entry.Id})?\n\n" +
            $"Memória atual do processo: {entry.MemoryText}.\n\n" +
            "O aplicativo continuará aberto. O Windows pode precisar carregar essas páginas novamente, causando lentidão temporária; não há garantia de ganho permanente de RAM. Salve seu trabalho antes de continuar.",
            "Confirmar otimização de RAM", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;
        try
        {
            await Task.Run(() => _ramOptimizer.TrimWorkingSet(entry.Id));
            _log.Write("Memória RAM", "Solicitar redução do conjunto de trabalho", "Sucesso", $"{entry.Name} (PID {entry.Id}); memória antes: {entry.MemoryText}");
            StatusText.Text = $"Solicitação enviada ao Windows para {entry.Name}. A redução pode ser temporária; atualizando lista…";
            await RefreshProcessesAsync();
        }
        catch (Exception ex)
        {
            _log.Write("Memória RAM", "Solicitar redução do conjunto de trabalho", "Falha", $"{entry.Name} (PID {entry.Id}): {ex.Message}");
            MessageBox.Show($"O Windows não permitiu otimizar este processo.\n\n{ex.Message}", "Otimização não concluída", MessageBoxButton.OK, MessageBoxImage.Information);
        }
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
