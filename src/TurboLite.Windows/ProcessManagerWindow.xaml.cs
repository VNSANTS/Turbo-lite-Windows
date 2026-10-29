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
        SelectAppsButton.IsEnabled = false;
        CountText.Text = "Atualizando lista…";
        StatusText.Text = "Coletando processos. Alguns podem desaparecer ou negar acesso durante a leitura.";

        try
        {
            var processes = await Task.Run(_service.GetProcesses);
            ProcessGrid.ItemsSource = processes;
            CountText.Text = $"{processes.Count} processos encontrados";
            StatusText.Text = "Processos do Windows e processos críticos são protegidos. Use “Selecionar não-Windows” para marcar os elegíveis.";
        }
        catch (Exception ex)
        {
            CountText.Text = "Falha ao consultar processos";
            StatusText.Text = $"Erro: {ex.Message}";
        }
        finally
        {
            _loading = false;
            SelectAppsButton.IsEnabled = true;
            UpdateTerminateButton();
        }
    }

    private void ProcessGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = ProcessGrid.SelectedItems.Cast<ProcessEntry>().ToArray();
        if (selected.Length == 0) StatusText.Text = "Selecione processos ou use “Selecionar não-Windows”.";
        else
        {
            int eligible = selected.Count(p => !p.IsProtected && p.Id > 4 && p.Id != Environment.ProcessId);
            StatusText.Text = $"{selected.Length} selecionado(s); {eligible} elegível(is) para otimização de RAM. O ganho pode ser temporário.";
        }
        UpdateTerminateButton();
    }

    private void SelectNonWindows_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        ProcessGrid.SelectedItems.Clear();
        foreach (var item in ProcessGrid.Items.OfType<ProcessEntry>())
            if (!item.IsProtected && item.Id > 4 && item.Id != Environment.ProcessId)
                ProcessGrid.SelectedItems.Add(item);
        int count = ProcessGrid.SelectedItems.Count;
        StatusText.Text = count == 0 ? "Nenhum processo elegível encontrado."
            : $"{count} processos não protegidos selecionados. Clique em “Liberar RAM selecionados”.";
        UpdateTerminateButton();
    }

    private void UpdateTerminateButton()
    {
        var selected = ProcessGrid.SelectedItems.Cast<ProcessEntry>().ToArray();
        bool oneEligible = !_loading && selected.Length == 1 &&
            !selected[0].IsProtected && selected[0].Id > 4 && selected[0].Id != Environment.ProcessId;
        bool anyEligible = !_loading && selected.Any(p =>
            !p.IsProtected && p.Id > 4 && p.Id != Environment.ProcessId);
        TerminateButton.IsEnabled = oneEligible;
        TrimRamButton.IsEnabled = anyEligible;
    }

    private async void TrimRam_Click(object sender, RoutedEventArgs e)
    {
        var targets = ProcessGrid.SelectedItems.Cast<ProcessEntry>()
            .Where(p => !p.IsProtected && p.Id > 4 && p.Id != Environment.ProcessId).ToArray();
        if (targets.Length == 0) return;
        long totalBefore = targets.Sum(p => p.WorkingSetBytes);
        var result = MessageBox.Show(
            $"Solicitar liberação de páginas de memória de {targets.Length} processo(s)?\\n\\n" +
            $"Memória somada antes: {SystemDiagnosticsService.FormatBytes(totalBefore)}.\\n\\n" +
            "Os aplicativos continuarão abertos. Processos protegidos são ignorados. O Windows pode carregar as páginas novamente; não há garantia de ganho permanente. Salve seu trabalho.",
            "Confirmar otimização de RAM em lote", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;
        TrimRamButton.IsEnabled = false;
        int succeeded = 0;
        var failures = new List<string>();
        foreach (var entry in targets)
        {
            try
            {
                await Task.Run(() => _ramOptimizer.TrimWorkingSet(entry.Id));
                succeeded++;
                _log.Write("Memória RAM", "Reduzir conjunto de trabalho", "Solicitado", $"{entry.Name} (PID {entry.Id}); antes: {entry.MemoryText}");
            }
            catch (Exception ex)
            {
                failures.Add($"{entry.Name} (PID {entry.Id}): {ex.Message}");
                _log.Write("Memória RAM", "Reduzir conjunto de trabalho", "Falha", $"{entry.Name} (PID {entry.Id}): {ex.Message}");
            }
        }
        StatusText.Text = $"Otimização solicitada para {succeeded} de {targets.Length} processo(s). Atualizando lista…";
        if (failures.Count > 0)
            MessageBox.Show($"Concluído: {succeeded}; falhas: {failures.Count}.\\n\\n" +
                string.Join("\\n", failures.Take(8)), "Resultado da otimização", MessageBoxButton.OK, MessageBoxImage.Information);
        await RefreshProcessesAsync();
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
