using Microsoft.Win32;
using System.IO;

namespace TurboLite.Windows.Services;

public sealed record StartupEntry(
    string Name,
    string Command,
    string Source,
    string Type,
    bool CanDisable = false,
    string RegistryPath = "",
    RegistryValueKind? ValueKind = null);

public sealed class StartupManagerService
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOncePath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string BackupPath = @"Software\TurboLiteWindows\StartupBackup";

    private static readonly (RegistryHive Hive, string Path, string Label)[] RunLocations =
    {
        (RegistryHive.CurrentUser, RunPath, "Usuário atual • Run"),
        (RegistryHive.CurrentUser, RunOncePath, "Usuário atual • RunOnce"),
        (RegistryHive.LocalMachine, RunPath, "Todos os usuários • Run"),
        (RegistryHive.LocalMachine, RunOncePath, "Todos os usuários • RunOnce")
    };

    public IReadOnlyList<StartupEntry> GetEntries()
    {
        var entries = new List<StartupEntry>();
        foreach (var location in RunLocations)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(location.Hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(location.Path, writable: false);
                if (key is null) continue;

                foreach (var valueName in key.GetValueNames())
                {
                    try
                    {
                        var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (value is null) continue;
                        var kind = key.GetValueKind(valueName);
                        bool canDisable = location.Hive == RegistryHive.CurrentUser &&
                            location.Path == RunPath &&
                            (kind == RegistryValueKind.String || kind == RegistryValueKind.ExpandString);
                        entries.Add(new StartupEntry(
                            string.IsNullOrWhiteSpace(valueName) ? "(valor sem nome)" : valueName,
                            value.ToString() ?? string.Empty,
                            location.Label,
                            kind.ToString(),
                            canDisable,
                            location.Path,
                            kind));
                    }
                    catch { /* Um valor inválido não deve impedir a leitura dos demais. */ }
                }
            }
            catch { /* Algumas chaves podem não existir ou negar acesso. */ }
        }

        AddStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Pasta do usuário");
        AddStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Pasta compartilhada");

        return entries
            .OrderBy(e => e.Source, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<StartupEntry> GetDisabledEntries()
    {
        var entries = new List<StartupEntry>();
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(BackupPath, writable: false);
            if (root is null) return entries;
            foreach (var id in root.GetSubKeyNames())
            {
                try
                {
                    using var item = root.OpenSubKey(id, writable: false);
                    if (item is null) continue;
                    entries.Add(new StartupEntry(
                        item.GetValue("OriginalName")?.ToString() ?? id,
                        item.GetValue("OriginalData")?.ToString() ?? string.Empty,
                        "Backup Turbo Lite",
                        "Desativado (reversível)"));
                }
                catch { }
            }
        }
        catch { }
        return entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public void DisableCurrentUserRunEntry(StartupEntry entry)
    {
        if (!entry.CanDisable || entry.Source != "Usuário atual • Run" ||
            string.IsNullOrWhiteSpace(entry.Name) || entry.ValueKind is null)
            throw new InvalidOperationException("Só é permitido desativar entradas comuns da chave Run do usuário atual.");

        if (entry.ValueKind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
            throw new InvalidOperationException("Tipo de valor não suportado para desativação reversível.");

        using var run = Registry.CurrentUser.OpenSubKey(RunPath, writable: true)
            ?? throw new InvalidOperationException("A chave Run do usuário atual não está disponível.");
        var currentKind = run.GetValueKind(entry.Name);
        if (currentKind != entry.ValueKind.Value)
            throw new InvalidOperationException("A entrada mudou desde a leitura. Atualize a lista e tente novamente.");

        var currentData = run.GetValue(entry.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString();
        if (currentData is null || !string.Equals(currentData, entry.Command, StringComparison.Ordinal))
            throw new InvalidOperationException("O comando mudou desde a leitura. Nenhuma alteração foi feita.");

        using var backupRoot = Registry.CurrentUser.CreateSubKey(BackupPath, writable: true)
            ?? throw new InvalidOperationException("Não foi possível criar a área de backup.");
        string backupId = Guid.NewGuid().ToString("N");
        using (var backup = backupRoot.CreateSubKey(backupId, writable: true)
            ?? throw new InvalidOperationException("Não foi possível criar o backup da entrada."))
        {
            backup.SetValue("OriginalName", entry.Name, RegistryValueKind.String);
            backup.SetValue("OriginalPath", RunPath, RegistryValueKind.String);
            backup.SetValue("OriginalKind", currentKind.ToString(), RegistryValueKind.String);
            backup.SetValue("OriginalData", currentData, RegistryValueKind.String);
            backup.SetValue("BackupVersion", 1, RegistryValueKind.DWord);
        }

        // Só remove o valor de inicialização depois que o backup foi gravado.
        try
        {
            run.DeleteValue(entry.Name, throwOnMissingValue: true);
        }
        catch
        {
            backupRoot.DeleteSubKeyTree(backupId, throwOnMissingSubKey: false);
            throw;
        }
    }

    public int RestoreAllDisabledEntries()
    {
        int restored = 0;
        using var root = Registry.CurrentUser.OpenSubKey(BackupPath, writable: true);
        if (root is null) return 0;

        foreach (var id in root.GetSubKeyNames())
        {
            string originalName;
            string originalPath;
            string data;
            string kindText;
            using (var item = root.OpenSubKey(id, writable: false))
            {
                if (item is null) continue;
                originalName = item.GetValue("OriginalName")?.ToString() ?? string.Empty;
                originalPath = item.GetValue("OriginalPath")?.ToString() ?? string.Empty;
                data = item.GetValue("OriginalData")?.ToString() ?? string.Empty;
                kindText = item.GetValue("OriginalKind")?.ToString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(originalName) || originalPath != RunPath ||
                !Enum.TryParse(kindText, out RegistryValueKind kind) ||
                kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
                continue;

            bool nameExists;
            using (var target = Registry.CurrentUser.CreateSubKey(originalPath, writable: true))
            {
                if (target is null) continue;
                nameExists = target.GetValueNames().Contains(originalName, StringComparer.OrdinalIgnoreCase);
                if (!nameExists) target.SetValue(originalName, data, kind);
            }
            if (nameExists) continue; // Não sobrescreve uma entrada criada depois da desativação.

            root.DeleteSubKeyTree(id, throwOnMissingSubKey: false);
            restored++;
        }
        return restored;
    }

    private static void AddStartupFolder(List<StartupEntry> entries, string folder, string source)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                try
                {
                    var info = new FileInfo(file);
                    entries.Add(new StartupEntry(
                        Path.GetFileNameWithoutExtension(info.Name),
                        info.FullName,
                        source,
                        info.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ? "Atalho" : "Arquivo"));
                }
                catch { }
            }
        }
        catch { }
    }
}
