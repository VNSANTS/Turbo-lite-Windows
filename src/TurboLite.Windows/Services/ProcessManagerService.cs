using System.Diagnostics;

namespace TurboLite.Windows.Services;

public sealed record ProcessEntry(
    int Id,
    string Name,
    string MemoryText,
    long WorkingSetBytes,
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
        var entries = new List<ProcessEntry>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                long memory = process.WorkingSet64;
                string name = process.ProcessName;
                bool protectedProcess = process.Id <= 4 || ProtectedNames.Contains(name);
                string reason = process.Id <= 4
                    ? "Processo do sistema"
                    : ProtectedNames.Contains(name)
                        ? "Processo crítico ou componente do Windows"
                        : string.Empty;

                entries.Add(new ProcessEntry(process.Id, name,
                    SystemDiagnosticsService.FormatBytes(memory), memory,
                    protectedProcess, reason));
            }
            catch
            {
                // Processos podem terminar ou negar acesso durante a leitura.
            }
            finally
            {
                process.Dispose();
            }
        }

        return entries.OrderByDescending(p => p.WorkingSetBytes).ToArray();
    }

    public bool TryCloseGracefully(int processId)
    {
        ValidateTarget(processId);
        using var process = Process.GetProcessById(processId);

        // Aplicativos com janela recebem primeiro um pedido normal de fechamento,
        // permitindo salvar dados ou mostrar a própria confirmação.
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
