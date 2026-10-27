using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TurboLite.Windows.Services;

public sealed class RamOptimizerService
{
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Registry", "smss", "csrss", "wininit", "winlogon", "services",
        "lsass", "svchost", "dwm", "fontdrvhost", "explorer", "MsMpEng",
        "RuntimeBroker", "SearchIndexer", "spoolsv"
    };

    public void TrimWorkingSet(int processId)
    {
        if (processId <= 4 || processId == Environment.ProcessId)
            throw new InvalidOperationException("Este processo é protegido e não pode ser otimizado por esta ferramenta.");

        using var process = Process.GetProcessById(processId);
        if (ProtectedNames.Contains(process.ProcessName))
            throw new InvalidOperationException("Este processo está na lista de proteção do Turbo Lite.");

        if (!EmptyWorkingSet(process.Handle))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                "O Windows recusou a solicitação. Talvez seja necessário executar o Turbo Lite com permissões adequadas.");

        // Esta chamada solicita ao Windows que remova páginas do conjunto de trabalho do processo.
        // Não fecha o aplicativo nem garante uma redução permanente da RAM em uso.
    }
}
