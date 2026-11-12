using PS2Manager.Core.Models;

namespace PS2Manager.Core.Art;

public readonly record struct ArtDownloadProgress(int Done, int Total, int Downloaded, int NotFound, string GameId);

public sealed record ArtDownloadResult(int Downloaded, int NotFound, int Skipped, int Failed);

public sealed class ArtDownloader(ArtStore store, ArtMissCache? misses = null)
{
    public ArtStore Store => store;

    /// <summary>
    /// Tries each source in order until one returns an image. Saves it and returns the path.
    /// Returns null only if every source answered "not found"; throws if a source couldn't be reached
    /// and none had the image.
    /// </summary>
    public async Task<string?> DownloadAsync(string gameId, ArtType type, IReadOnlyList<IArtSource> sources, CancellationToken ct = default)
    {
        Exception? unreachable = null;
        foreach (var source in sources.Where(s => s.Supports(type)))
        {
            try
            {
                var img = await source.FetchAsync(gameId, type, ct);
                if (img is not null)
                {
                    misses?.Clear(gameId, type);
                    return store.Save(gameId, type, img.Data, img.Extension);
                }
            }
            catch (HttpRequestException ex) { unreachable = ex; }   // try the next source
        }
        if (unreachable is not null) throw unreachable;
        misses?.RecordMiss(gameId, type);
        return null;
    }

    /// <summary>Downloads every requested art type for many games, a few in parallel.</summary>
    public async Task<ArtDownloadResult> DownloadManyAsync(
        IEnumerable<string> gameIds,
        IReadOnlyCollection<ArtType> types,
        IReadOnlyList<IArtSource> sources,
        bool overwrite,
        IProgress<ArtDownloadProgress>? progress = null,
        CancellationToken ct = default,
        int parallelism = 6,
        bool skipRecentMisses = false)
    {
        var ids = gameIds.Where(GameIds.IsKnown).Select(GameIds.Normalize)
                         .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        int done = 0, downloaded = 0, notFound = 0, skipped = 0, failed = 0;

        try
        {
        await Parallel.ForEachAsync(ids,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
            async (id, token) =>
            {
                foreach (var type in types)
                {
                    if (!overwrite && store.Has(id, type)) { Interlocked.Increment(ref skipped); continue; }
                    if (skipRecentMisses && misses?.IsRecentMiss(id, type) == true) { Interlocked.Increment(ref skipped); continue; }
                    try
                    {
                        if (await DownloadAsync(id, type, sources, token) is not null) Interlocked.Increment(ref downloaded);
                        else Interlocked.Increment(ref notFound);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        Log.Error($"Art download {id} {type}", ex);
                    }
                }
                int n = Interlocked.Increment(ref done);
                progress?.Report(new ArtDownloadProgress(n, ids.Count, downloaded, notFound, id));
            });
        }
        finally
        {
            misses?.Save();
        }

        return new ArtDownloadResult(downloaded, notFound, skipped, failed);
    }
}
