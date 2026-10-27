using System.IO;
using System.Management;

namespace TurboLite.Windows.Services;

public sealed record VolumeEntry(
    string Name,
    string Label,
    string Format,
    string TotalText,
    string FreeText,
    double UsedPercent,
    string Status);

public sealed record PhysicalDiskEntry(
    string Model,
    string InterfaceType,
    string MediaType,
    string SizeText,
    string WindowsStatus);

public sealed record StorageSnapshot(
    IReadOnlyList<VolumeEntry> Volumes,
    IReadOnlyList<PhysicalDiskEntry> PhysicalDisks,
    string Note);

public sealed class StorageManagerService
{
    public StorageSnapshot GetSnapshot()
    {
        var volumes = new List<VolumeEntry>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                long total = drive.TotalSize;
                long free = drive.AvailableFreeSpace;
                double used = total > 0 ? (double)(total - free) / total * 100 : 0;
                volumes.Add(new VolumeEntry(
                    drive.Name,
                    string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "(sem rótulo)" : drive.VolumeLabel,
                    drive.DriveFormat,
                    SystemDiagnosticsService.FormatBytes(total),
                    SystemDiagnosticsService.FormatBytes(free),
                    used,
                    free < 10L * 1024 * 1024 * 1024 ? "Pouco espaço livre" : "Espaço livre adequado"));
            }
            catch { /* Unidades removíveis ou sem permissão podem falhar ao consultar. */ }
        }

        var disks = new List<PhysicalDiskEntry>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Model, InterfaceType, MediaType, Size, Status FROM Win32_DiskDrive");
            foreach (ManagementObject disk in searcher.Get())
            {
                try
                {
                    long size = 0;
                    if (disk["Size"] is not null) long.TryParse(disk["Size"].ToString(), out size);
                    disks.Add(new PhysicalDiskEntry(
                        disk["Model"]?.ToString()?.Trim() ?? "Modelo não informado",
                        disk["InterfaceType"]?.ToString() ?? "Não informado",
                        disk["MediaType"]?.ToString() ?? "Não informado",
                        size > 0 ? SystemDiagnosticsService.FormatBytes(size) : "Tamanho indisponível",
                        disk["Status"]?.ToString() ?? "Sem status informado"));
                }
                finally { disk.Dispose(); }
            }
        }
        catch
        {
            // A lista de volumes ainda é útil mesmo se o WMI não responder.
        }

        return new StorageSnapshot(
            volumes.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            disks,
            "O status do WMI não é um teste SMART completo e não garante a saúde física do disco. Faça backup se houver ruídos, travamentos ou erros de leitura.");
    }
}
