using System.Windows;
using TurboLite.Windows.Services;

namespace TurboLite.Windows;

public partial class PowerModeWindow : Window
{
    private readonly PowerPlanService _service = new();
    private IReadOnlyList<PowerPlan> _plans = Array.Empty<PowerPlan>();

    public PowerModeWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshPlans();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshPlans();

    private void RefreshPlans()
    {
        try
        {
            _plans = _service.GetPlans();
            PlansGrid.ItemsSource = _plans;
            ActivePlanText.Text = _plans.FirstOrDefault(p => p.IsActive)?.Name ?? "Plano ativo não identificado";
            StatusText.Text = $"{_plans.Count} plano(s) encontrado(s). Selecione um plano ou use um dos modos rápidos.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Não foi possível listar planos: {ex.Message}";
            ActivePlanText.Text = "Indisponível";
        }
    }

    private void Performance_Click(object sender, RoutedEventArgs e) => ApplySuggested("Desempenho");
    private void Balanced_Click(object sender, RoutedEventArgs e) => ApplySuggested("Equilibrado");
    private void Saver_Click(object sender, RoutedEventArgs e) => ApplySuggested("Economia");

    private void ApplySuggested(string mode)
    {
        var guid = _service.GetSuggestedPlanGuid(mode);
        var plan = _plans.FirstOrDefault(p => p.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase));
        if (plan is null)
        {
            StatusText.Text = $"O plano padrão de {mode.ToLowerInvariant()} não está instalado/disponível neste Windows. Nenhuma alteração foi feita.";
            return;
        }
        Apply(plan);
    }

    private void ApplySelected_Click(object sender, RoutedEventArgs e)
    {
        if (PlansGrid.SelectedItem is not PowerPlan plan)
        {
            StatusText.Text = "Selecione um plano na lista primeiro.";
            return;
        }
        Apply(plan);
    }

    private void Apply(PowerPlan plan)
    {
        var message = $"Aplicar o plano “{plan.Name}”?\n\nO Turbo Lite salvará o plano atual para permitir restauração. Isso altera apenas o plano de energia do Windows.";
        if (MessageBox.Show(this, message, "Confirmar mudança de energia", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            var name = _service.ApplyPlan(plan.Guid, plan.Name);
            StatusText.Text = $"Plano aplicado: {name}.";
            RefreshPlans();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Falha ao aplicar o plano: {ex.Message}";
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Restaurar o plano de energia salvo antes da última alteração feita pelo Turbo Lite?",
            "Restaurar plano anterior", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            var name = _service.RestorePreviousPlan();
            StatusText.Text = $"Plano anterior restaurado: {name}.";
            RefreshPlans();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }
}
