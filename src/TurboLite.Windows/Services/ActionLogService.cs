using System.IO;
using System.Text.Json;

namespace TurboLite.Windows.Services;

public sealed record ActionLogEntry(DateTime Timestamp, string Category, string Action, string Result, string Details);

public sealed class ActionLogService
{
    private static readonly object Sync = new();
    private readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TurboLiteWindows", "logs", "actions.jsonl");

    public void Write(string category, string action, string result, string details)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                var entry = new ActionLogEntry(DateTime.Now, category, action, result, details);
                File.AppendAllText(_logPath, JsonSerializer.Serialize(entry) + Environment.NewLine);
            }
        }
        catch
        {
            // A falha de log nunca deve impedir uma ação principal ou a abertura do app.
        }
    }

    public IReadOnlyList<ActionLogEntry> GetRecent(int maxEntries = 500)
    {
        try
        {
            if (!File.Exists(_logPath)) return Array.Empty<ActionLogEntry>();
            lock (Sync)
            {
                return File.ReadLines(_logPath)
                    .Reverse()
                    .Take(Math.Clamp(maxEntries, 1, 5000))
                    .Select(line =>
                    {
                        try { return JsonSerializer.Deserialize<ActionLogEntry>(line); }
                        catch { return null; }
                    })
                    .Where(entry => entry is not null)
                    .Select(entry => entry!)
                    .ToArray();
            }
        }
        catch
        {
            return Array.Empty<ActionLogEntry>();
        }
    }
}
