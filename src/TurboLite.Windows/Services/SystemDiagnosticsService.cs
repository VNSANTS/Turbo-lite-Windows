using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;

namespace TurboLite.Windows.Services;

public sealed class SystemDiagnosticsService
{
    public string GetCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (ManagementObject item in searcher.Get())
                return item["Name"]?.ToString()?.Trim() ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "CPU não identificada";
        }
        catch { }
        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "CPU não identificada";
    }

    public (ulong TotalMb, ulong AvailableMb) GetMemory()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject item in searcher.Get())
            {
                ulong total = Convert.ToUInt64(item["TotalVisibleMemorySize"] ?? 0UL) / 1024;
                ulong free = Convert.ToUInt64(item["FreePhysicalMemory"] ?? 0UL) / 1024;
                return (total, free);
            }
        }
        catch { }
        return (0, 0);
    }

    public (string Name, double UsagePercent, long FreeBytes, long TotalBytes) GetSystemDrive()
    {
        try
        {
            string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(root);
            if (drive.IsReady)
            {
                double used = drive.TotalSize == 0 ? 0 : (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100;
                return ($"{drive.Name} ({FormatBytes(drive.TotalSize)})", used, drive.AvailableFreeSpace, drive.TotalSize);
            }
        }
        catch { }
        return ("Disco indisponível", 0, 0, 0);
    }

    public string GetOsName() => RuntimeInformation.OSDescription;

    public (int Count, long WorkingSetBytes) GetProcessSummary()
    {
        var processes = Process.GetProcesses();
        try { return (processes.Length, processes.Sum(p => { try { return p.WorkingSet64; } catch { return 0L; } })); }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = Math.Max(0, bytes); int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.0} {units[unit]}";
    }
}
