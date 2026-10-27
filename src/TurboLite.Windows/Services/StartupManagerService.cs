using Microsoft.Win32;

namespace TurboLite.Windows.Services;

public sealed record StartupEntry(string Name, string Command, string Source, string Type);

public sealed class StartupManagerService
{
    private static readonly (RegistryHive Hive, string Path, string Label)[] RunLocations =
    {
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Usuário atual"),
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Usuário atual • execução única"),
        (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Todos os usuários"),
        (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "Todos os usuários • execução única")
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
                        entries.Add(new StartupEntry(
                            string.IsNullOrWhiteSpace(valueName) ? "(valor sem nome)" : valueName,
                            value.ToString() ?? string.Empty,
                            location.Label,
                            "Registro"));
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
