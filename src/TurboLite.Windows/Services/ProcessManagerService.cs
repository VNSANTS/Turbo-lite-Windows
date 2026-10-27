using System.Diagnostics;
using System.Threading;

namespace TurboLite.Windows.Services;

public sealed record ProcessEntry(
    int Id,
    string Name,
    string MemoryText,
    long WorkingSetBytes,
    double CpuPercent,
    string CpuText,
    bool IsProtected,
    string ProtectionReason);

public sealed class ProcessManagerService
{
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "smss", "csrss", "wininit", "winlogon",
        "services", "lsass", "svchost", "dwm", "explorer", "spoolsv",
        "MsMpEng", "SecurityHealthService", "TrustedInstaller"
    };

    public IReadOnlyList<ProcessEntry> GetProcesses()
    {
        var samples = new Dictionary<int, (TimeSpan Cpu, string Name, long Memory)>();
        var firstPass = Process.GetProcesses();
        foreach (var process in firstPass)
        {
            try
            {
                samples[process.Id] = (process.TotalProcessorTime, process.ProcessName, process.WorkingSet64);
            }
            catch
            {
                // Alguns processos terminam ou negam acesso durante a amostragem.
            }
            finally { process.Dispose(); }
        }

        var timer = Stopwatch.StartNew();
        Thread.Sleep(500);
        timer.Stop();
        double elapsedMs = Math.Max(1, timer.Elapsed.TotalMilliseconds);
        var entries = new List<ProcessEntry>();
        var secondPass = Process.GetProcesses();
        try
        {
            foreach (var process in secondPass)
            {
                try
                {
                    int id = process.Id;
                    string name = process.ProcessName;
                    long memory = process.WorkingSet64;
                    bool protectedProcess = id <= 4 || ProtectedNames.Contains(name);
                    string reason = id <= 4
                        ? "Processo do sistema"
                        : ProtectedNames.Contains(name)
                            ? "Processo crítico ou componente do Windows"
                            : string.Empty;

                    double cpuPercent = -1;
                    if (samples.TryGetValue(id, out var before))
                    {
                        var deltaMs = (process.TotalProcessorTime - before.Cpu).TotalMilliseconds;
                        cpuPercent = Math.Clamp(deltaMs / (elapsedMs * Math.Max(1, Environment.ProcessorCount)) * 100, 0, 100);
                    }

                    entries.Add(new ProcessEntry(
                        id, name, SystemDiagnosticsService.FormatBytes(memory), memory,
                        cpuPercent, cpuPercent < 0 ? "N/D" : $"{cpuPercent:0.0}%",
                        protectedProcess, reason));
                }
                catch
                {
                    // Processos podem terminar ou negar acesso durante a leitura.
                }
            }
        }
        finally
        {
            foreach (var process in secondPass) { try { process.Dispose(); } catch { } }
        }

        return entries.OrderByDescending(p => p.WorkingSetBytes).ToArray();
    }

    public bool TryCloseGracefully(int processId)
    {
        ValidateTarget(processId);
        using var process = Process.GetProcessById(processId);

        if (process.MainWindowHandle == IntPtr.Zero)
            return false;

        if (!process.CloseMainWindow())
            return false;

        return process.WaitForExit(2500);
    }

    public void ForceTerminate(int processId)
    {
        ValidateTarget(processId);
        using var process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: false);
        if (!process.WaitForExit(3000))
            throw new TimeoutException("O processo ainda não confirmou o encerramento.");
    }

    private static void ValidateTarget(int processId)
    {
        if (processId <= 4)
            throw new InvalidOperationException("O Turbo Lite bloqueia o encerramento de processos com PID 0 a 4.");
        if (processId == Environment.ProcessId)
            throw new InvalidOperationException("O aplicativo não pode encerrar a si próprio.");

        using var process = Process.GetProcessById(processId);
        if (ProtectedNames.Contains(process.ProcessName))
            throw new InvalidOperationException($"O encerramento de '{process.ProcessName}' está bloqueado por proteção.");
    }
}
