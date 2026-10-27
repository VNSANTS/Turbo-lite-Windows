using Microsoft.Win32;
using System.IO;
using System.Text.Json;

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
    private static readonly string StartupFileBackupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TurboLiteWindows", "StartupFileBackup");

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
        try
        {
            if (Directory.Exists(StartupFileBackupPath))
            {
                foreach (var folder in Directory.EnumerateDirectories(StartupFileBackupPath))
                {
                    try
                    {
                        var manifestPath = Path.Combine(folder, "backup.json");
                        if (!File.Exists(manifestPath)) continue;
                        var saved = JsonSerializer.Deserialize<StartupFileBackup>(File.ReadAllText(manifestPath));
                        if (saved is not null)
                            entries.Add(new StartupEntry(saved.Name, saved.OriginalPath, "Backup Turbo Lite • Pasta do usuário", "Atalho desativado (reversível)"));
                    }
                    catch { }
                }
            }
        }
        catch { }
        return entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public void DisableEntry(StartupEntry entry)
    {
        if (entry.Source == "Usuário atual • Run")
        {
            DisableCurrentUserRunEntry(entry);
            return;
        }

        if (!entry.CanDisable || entry.Source != "Pasta do usuário" ||
            !entry.Command.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Só é permitido desativar entradas Run compatíveis ou atalhos .lnk da pasta de inicialização do usuário atual.");

        string startupRoot = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Startup))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string sourcePath = Path.GetFullPath(entry.Command);
        if (!sourcePath.StartsWith(startupRoot, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(sourcePath)?.TrimEnd(Path.DirectorySeparatorChar), startupRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O atalho não está diretamente na pasta de inicialização do usuário atual.");

        if (!File.Exists(sourcePath)) throw new FileNotFoundException("O atalho não foi encontrado. Atualize a lista.", sourcePath);
        Directory.CreateDirectory(StartupFileBackupPath);
        string backupFolder = Path.Combine(StartupFileBackupPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupFolder);
        try
        {
            File.Move(sourcePath, Path.Combine(backupFolder, Path.GetFileName(sourcePath)));
            File.WriteAllText(Path.Combine(backupFolder, "backup.json"),
                JsonSerializer.Serialize(new StartupFileBackup(Path.GetFileNameWithoutExtension(sourcePath), sourcePath)));
        }
        catch
        {
            try
            {
                string moved = Path.Combine(backupFolder, Path.GetFileName(sourcePath));
                if (File.Exists(moved) && !File.Exists(sourcePath)) File.Move(moved, sourcePath);
            }
            catch { }
            try { if (Directory.Exists(backupFolder)) Directory.Delete(backupFolder, recursive: true); } catch { }
            throw;
        }
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
        restored += RestoreStartupFolderShortcuts();
        return restored;
    }

    private int RestoreStartupFolderShortcuts()
    {
        int restored = 0;
        if (!Directory.Exists(StartupFileBackupPath)) return restored;
        string startupRoot = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        foreach (var folder in Directory.EnumerateDirectories(StartupFileBackupPath))
        {
            try
            {
                string manifestPath = Path.Combine(folder, "backup.json");
                if (!File.Exists(manifestPath)) continue;
                var saved = JsonSerializer.Deserialize<StartupFileBackup>(File.ReadAllText(manifestPath));
                if (saved is null) continue;
                string originalPath = Path.GetFullPath(saved.OriginalPath);
                string expectedRoot = Path.GetFullPath(startupRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!string.Equals(Path.GetDirectoryName(originalPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), expectedRoot, StringComparison.OrdinalIgnoreCase))
                    continue;
                string backupFile = Path.Combine(folder, Path.GetFileName(originalPath));
                if (!File.Exists(backupFile) || File.Exists(originalPath)) continue;
                File.Move(backupFile, originalPath);
                File.Delete(manifestPath);
                try { Directory.Delete(folder, recursive: false); } catch { }
                restored++;
            }
            catch { }
        }
        return restored;
    }

    private sealed record StartupFileBackup(string Name, string OriginalPath);

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
                    bool canDisable = source == "Pasta do usuário" && info.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase);
                    entries.Add(new StartupEntry(
                        Path.GetFileNameWithoutExtension(info.Name),
                        info.FullName,
                        source,
                        info.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ? "Atalho" : "Arquivo",
                        canDisable));
                }
                catch { }
            }
        }
        catch { }
    }
}
