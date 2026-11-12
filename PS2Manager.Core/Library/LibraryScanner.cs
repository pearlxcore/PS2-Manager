using System.Collections.Concurrent;
using PS2Manager.Core.Iso;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Library;

public readonly record struct ScanProgress(int Done, int Total, string CurrentFile);

public sealed record ScanResult(List<GameEntry> Games, int NewCount, int ReusedCount, int RemovedCount, TimeSpan Elapsed);

public static class LibraryScanner
{
    private static readonly EnumerationOptions Enumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
        AttributesToSkip = FileAttributes.System,
    };

    public static List<string> FindDiscFiles(IEnumerable<string> folders)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders.Where(Directory.Exists))
        {
            foreach (var f in Directory.EnumerateFiles(folder, "*", Enumeration))
                if (DiscReader.IsSupported(f)) files.Add(f);
        }
        return files.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Scans <paramref name="folders"/>. Files whose size and timestamp match an entry in
    /// <paramref name="existing"/> are reused without reopening, unless <paramref name="fullRescan"/>.
    /// Entries in <paramref name="existing"/> outside the scanned folders are kept untouched.
    /// </summary>
    public static async Task<ScanResult> ScanAsync(
        IReadOnlyList<string> folders,
        IEnumerable<GameEntry> existing,
        bool fullRescan,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default,
        int parallelism = 3)
    {
        var started = DateTime.UtcNow;
        var known = existing.GroupBy(e => e.IsoPath, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var files = await Task.Run(() => FindDiscFiles(folders), ct);
        var results = new ConcurrentDictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        int done = 0, reused = 0;

        await Parallel.ForEachAsync(files,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallelism), CancellationToken = ct },
            (path, token) =>
            {
                GameEntry entry;
                var fi = new FileInfo(path);
                if (!fullRescan && known.TryGetValue(path, out var prev)
                    && prev.SizeBytes == fi.Length && prev.LastWriteUtc == fi.LastWriteTimeUtc
                    && prev.Validity != DiscValidity.ReadError)
                {
                    entry = prev;
                    Interlocked.Increment(ref reused);
                }
                else
                {
                    entry = DiscReader.Read(path);
                    // Keep a computed full CRC if the file did not change.
                    if (known.TryGetValue(path, out var old) && old.SizeBytes == entry.SizeBytes && old.LastWriteUtc == entry.LastWriteUtc)
                        entry.Crc32 = old.Crc32;
                }
                entry.FileMissing = false;
                results[path] = entry;
                int n = Interlocked.Increment(ref done);
                progress?.Report(new ScanProgress(n, files.Count, Path.GetFileName(path)));
                return ValueTask.CompletedTask;
            });

        // Keep entries from other folders; drop entries under scanned folders that no longer exist.
        int removed = 0;
        var output = new List<GameEntry>(results.Values);
        foreach (var e in known.Values)
        {
            if (results.ContainsKey(e.IsoPath)) continue;
            if (IsUnder(e.IsoPath, folders)) { removed++; continue; }
            output.Add(e);
        }

        output.Sort((a, b) => string.Compare(a.IsoPath, b.IsoPath, StringComparison.OrdinalIgnoreCase));
        int newCount = results.Count - reused;
        return new ScanResult(output, newCount, reused, removed, DateTime.UtcNow - started);
    }

    public static bool IsUnder(string path, IEnumerable<string> folders) =>
        folders.Any(f => path.StartsWith(Path.TrimEndingDirectorySeparator(f) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
}
