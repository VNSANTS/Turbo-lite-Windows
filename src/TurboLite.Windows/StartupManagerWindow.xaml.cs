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

    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        if (StartupGrid.SelectedItem is not StartupEntry entry || !entry.CanDisable)
        {
            MessageBox.Show("Selecione uma entrada compatível da chave Run do usuário atual. Entradas do sistema, RunOnce e pastas não são alteradas por esta função.",
                "Seleção não compatível", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            $"Desativar a inicialização automática de '{entry.Name}'?\n\nComando: {entry.Command}\n\nO Turbo Lite salvará uma cópia reversível antes de remover a entrada. O programa instalado não será desinstalado.",
            "Confirmar desativação",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            _service.DisableCurrentUserRunEntry(entry);
            MessageBox.Show("Entrada desativada e backup salvo. Use 'Restaurar backups' para reativá-la.",
                "Concluído", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshEntries();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível desativar a entrada.\n\n{ex.Message}",
                "Alteração não concluída", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var pending = _service.GetDisabledEntries();
        if (pending.Count == 0)
        {
            MessageBox.Show("Não há backups de inicialização para restaurar.", "Turbo Lite",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            $"Foram encontrados {pending.Count} backup(s). Restaurar as entradas cujos nomes ainda estejam livres? Entradas existentes não serão sobrescritas.",
            "Confirmar restauração",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            int restored = _service.RestoreAllDisabledEntries();
            MessageBox.Show($"{restored} entrada(s) restaurada(s). Backups conflitantes foram preservados.",
                "Restauração concluída", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshEntries();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"A restauração não foi concluída.\n\n{ex.Message}",
                "Falha ao restaurar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshEntries()
    {
        try
        {
            var entries = _service.GetEntries();
            StartupGrid.ItemsSource = entries;
            var pending = _service.GetDisabledEntries().Count;\n            CountText.Text = $"{entries.Count} itens encontrados • {pending} backup(s) reversível(is)";
        }
        catch (Exception ex)
        {
            CountText.Text = $"Não foi possível ler a inicialização: {ex.Message}";
        }
    }
}
