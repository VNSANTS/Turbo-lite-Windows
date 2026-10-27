using System.IO;

namespace TurboLite.Windows.Services;

public sealed record TempFileEntry(string Path, long SizeBytes, string SizeText);
public sealed record TempCleanupPreview(string RootPath, IReadOnlyList<TempFileEntry> Files, long TotalBytes, int SkippedCount);
public sealed record TempCleanupResult(int DeletedCount, long FreedBytes, int FailedCount);

public sealed class TempCleanupService
{
    public TempCleanupPreview Preview()
    {
        string root = Path.GetFullPath(Path.GetTempPath());
        var files = new List<TempFileEntry>();
        int skipped = 0;
        long total = 0;
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string current = pending.Pop();
            IEnumerable<string> childDirectories;
            IEnumerable<string> childFiles;
            try
            {
                childDirectories = Directory.EnumerateDirectories(current).ToArray();
                childFiles = Directory.EnumerateFiles(current).ToArray();
            }
            catch
            {
                skipped++;
                continue;
            }

            foreach (string directory in childDirectories)
            {
                try
                {
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    {
                        skipped++;
                        continue;
                    }
                    pending.Push(directory);
                }
                catch { skipped++; }
            }

            foreach (string file in childFiles)
            {
                try
                {
                    if (!IsInsideRoot(root, file) || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    {
                        skipped++;
                        continue;
                    }
                    long size = new FileInfo(file).Length;
                    files.Add(new TempFileEntry(file, size, SystemDiagnosticsService.FormatBytes(size)));
                    total = total > long.MaxValue - size ? long.MaxValue : total + size;
                }
                catch { skipped++; }
            }
        }

        return new TempCleanupPreview(root, files.OrderByDescending(f => f.SizeBytes).ToArray(), total, skipped);
    }

    public TempCleanupResult Clean(TempCleanupPreview preview)
    {
        string root = Path.GetFullPath(Path.GetTempPath());
        if (!string.Equals(root, Path.GetFullPath(preview.RootPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A pasta temporária mudou desde a análise. Faça uma nova prévia.");

        int deleted = 0;
        int failed = 0;
        long freed = 0;
        foreach (var item in preview.Files)
        {
            try
            {
                string path = Path.GetFullPath(item.Path);
                if (!IsInsideRoot(root, path))
                {
                    failed++;
                    continue;
                }

                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) != 0)
                {
                    failed++;
                    continue;
                }

                long size = new FileInfo(path).Length;
                File.Delete(path);
                deleted++;
                freed = freed > long.MaxValue - size ? long.MaxValue : freed + size;
            }
            catch { failed++; }
        }

        return new TempCleanupResult(deleted, freed, failed);
    }

    private static bool IsInsideRoot(string root, string path)
    {
        string fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }
}
