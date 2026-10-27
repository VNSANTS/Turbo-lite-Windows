using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TurboLite.Windows.Services;

public sealed record PowerPlan(string Guid, string Name, bool IsActive)
{
    public override string ToString() => IsActive ? $"{Name} (ativo)" : Name;
}

public sealed class PowerPlanService
{
    private readonly ActionLogService _log = new();
    private readonly string _statePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TurboLiteWindows", "power-plan-state.json");

    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string PowerSaverGuid = "a1841308-3541-4fab-bc81-f71556f20b4a";

    public IReadOnlyList<PowerPlan> GetPlans()
    {
        var result = RunPowerCfg("/list");
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Output)
                ? "O Windows não conseguiu listar os planos de energia."
                : result.Output.Trim());

        var plans = new List<PowerPlan>();
        foreach (var line in result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line, @"Power Scheme GUID:\s*([a-fA-F0-9-]{36})\s*\((.*?)\)(\s*\*)?");
            if (!match.Success) continue;
            plans.Add(new PowerPlan(match.Groups[1].Value, match.Groups[2].Value.Trim(), match.Groups[3].Success));
        }
        return plans;
    }

    public string? GetSuggestedPlanGuid(string mode) => mode switch
    {
        "Desempenho" => HighPerformanceGuid,
        "Equilibrado" => BalancedGuid,
        "Economia" => PowerSaverGuid,
        _ => null
    };

    public string GetActivePlanName()
    {
        var active = GetPlans().FirstOrDefault(p => p.IsActive);
        return active?.Name ?? "Plano ativo não identificado";
    }

    public string ApplyPlan(string guid, string name)
    {
        if (!Guid.TryParse(guid, out _))
            throw new ArgumentException("Identificador de plano inválido.", nameof(guid));

        var plans = GetPlans();
        var selected = plans.FirstOrDefault(p => string.Equals(p.Guid, guid, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
            throw new InvalidOperationException("Esse plano não está disponível neste computador. Atualize a lista ou escolha outro plano.");

        var current = plans.FirstOrDefault(p => p.IsActive);
        if (current is not null && !current.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase))
            SavePreviousPlan(current.Guid, current.Name);

        var result = RunPowerCfg($"/setactive {guid}");
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Output) ? "O Windows recusou a alteração do plano de energia." : result.Output.Trim());

        _log.Write("Energia", "Alterar plano de energia", "Sucesso", $"Plano aplicado: {selected.Name} ({guid}).");
        return selected.Name;
    }

    public string RestorePreviousPlan()
    {
        if (!File.Exists(_statePath))
            throw new InvalidOperationException("Ainda não há um plano anterior salvo. Aplique outro plano primeiro.");

        SavedPlan? saved;
        try { saved = JsonSerializer.Deserialize<SavedPlan>(File.ReadAllText(_statePath)); }
        catch { saved = null; }

        if (saved is null || !Guid.TryParse(saved.Guid, out _))
            throw new InvalidOperationException("O registro do plano anterior está inválido.");

        var plans = GetPlans();
        if (!plans.Any(p => p.Guid.Equals(saved.Guid, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("O plano anterior não está mais disponível no Windows.");

        var result = RunPowerCfg($"/setactive {saved.Guid}");
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Output) ? "Não foi possível restaurar o plano anterior." : result.Output.Trim());

        _log.Write("Energia", "Restaurar plano anterior", "Sucesso", $"Plano restaurado: {saved.Name} ({saved.Guid}).");
        File.Delete(_statePath);
        return saved.Name;
    }

    private void SavePreviousPlan(string guid, string name)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(new SavedPlan(guid, name)));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Não foi possível salvar a opção de restauração. Nenhuma alteração foi aplicada.", ex);
        }
    }

    private static (int ExitCode, string Output) RunPowerCfg(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe"),
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        if (!process.WaitForExit(10000))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("O comando de energia demorou mais de 10 segundos e foi interrompido.");
        }
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        return (process.ExitCode, string.Join(Environment.NewLine, new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s))));
    }

    private sealed record SavedPlan(string Guid, string Name);
}
