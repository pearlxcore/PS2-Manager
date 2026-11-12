namespace PS2Manager.Core.Art;

/// <summary>
/// Remembers (game, art type) pairs no source had, so automatic downloads (e.g. at startup)
/// don't ask every server again each launch. Manual downloads ignore it.
/// </summary>
public sealed class ArtMissCache(string path)
{
    public static readonly TimeSpan RetryAfter = TimeSpan.FromDays(7);

    private readonly Dictionary<string, DateTime> _misses = JsonFile.Load<Dictionary<string, DateTime>>(path) ?? [];
    private readonly object _lock = new();

    private static string Key(string gameId, ArtType type) => $"{gameId.ToUpperInvariant()}|{type}";

    public bool IsRecentMiss(string gameId, ArtType type)
    {
        lock (_lock)
            return _misses.TryGetValue(Key(gameId, type), out var when) && DateTime.UtcNow - when < RetryAfter;
    }

    public void RecordMiss(string gameId, ArtType type) { lock (_lock) _misses[Key(gameId, type)] = DateTime.UtcNow; }

    public void Clear(string gameId, ArtType type) { lock (_lock) _misses.Remove(Key(gameId, type)); }

    public void Save()
    {
        lock (_lock)
        {
            // Drop expired entries so the file doesn't grow forever.
            foreach (var k in _misses.Where(kv => DateTime.UtcNow - kv.Value >= RetryAfter).Select(kv => kv.Key).ToList())
                _misses.Remove(k);
            try { JsonFile.Save(path, _misses); }
            catch (Exception ex) { Log.Error("Saving art miss cache", ex); }
        }
    }
}
