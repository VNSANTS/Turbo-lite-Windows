using System.Windows;
using System.Windows.Threading;
using TurboLite.Windows.Services;

namespace TurboLite.Windows;

public partial class MainWindow : Window
{
    private readonly SystemDiagnosticsService _diagnostics = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(8) };

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshDiagnosticsAsync();
        _timer.Tick += async (_, _) => await RefreshDiagnosticsAsync();
        _timer.Start();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshDiagnosticsAsync();

    private async Task RefreshDiagnosticsAsync()
    {
        try
        {
            StatusText.Text = "Atualizando métricas…";
            var snapshot = await Task.Run(() =>
            {
                var memory = _diagnostics.GetMemory();
                var disk = _diagnostics.GetSystemDrive();
                var processes = _diagnostics.GetProcessSummary();
                return (memory, disk, processes, cpu: _diagnostics.GetCpuName(), os: _diagnostics.GetOsName());
            });

            if (snapshot.memory.TotalMb > 0)
            {
                ulong used = snapshot.memory.TotalMb - Math.Min(snapshot.memory.TotalMb, snapshot.memory.AvailableMb);
                double percent = (double)used / snapshot.memory.TotalMb * 100;
                RamValue.Text = $"{percent:0}%";
                RamDetail.Text = $"{SystemDiagnosticsService.FormatBytes((long)used * 1024 * 1024)} usados de {snapshot.memory.TotalMb / 1024.0:0.0} GB";
                RamBar.Value = percent;
            }
            else { RamValue.Text = "N/D"; RamDetail.Text = "Não foi possível consultar a RAM"; }

            DiskValue.Text = $"{snapshot.disk.UsagePercent:0}%";
            DiskDetail.Text = $"Uso do volume {snapshot.disk.Name}";
            DiskBar.Value = snapshot.disk.UsagePercent;
            ProcessValue.Text = snapshot.processes.Count.ToString();
            ProcessDetail.Text = $"Conjunto de trabalho: {SystemDiagnosticsService.FormatBytes(snapshot.processes.WorkingSetBytes)}";
            CpuName.Text = snapshot.cpu;
            OsName.Text = snapshot.os;
            StatusText.Text = $"Atualizado às {DateTime.Now:HH:mm:ss}";
            FooterText.Text = "Turbo Lite Windows • Métricas atualizadas automaticamente a cada 8 segundos";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Não foi possível ler todas as métricas";
            FooterText.Text = $"Diagnóstico parcial: {ex.GetType().Name}";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
