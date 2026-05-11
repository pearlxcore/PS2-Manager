using System.Net.Http;

namespace PS2_ISO_Manager.Helper;

public static class CoverManager
{
    private const int MaxConcurrency = 6;
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private static readonly HttpClient _http = new();

    private static string _coverDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Covers");

    public static string CoverDirectory
    {
        get => _coverDir;
        set
        {
            _coverDir = value;
            if (!Directory.Exists(_coverDir))
                Directory.CreateDirectory(_coverDir);
        }
    }

    static CoverManager()
    {
        if (!Directory.Exists(_coverDir))
            Directory.CreateDirectory(_coverDir);
    }

    /// <summary>
    /// Returns the local cover path for a game ID if it exists, otherwise null.
    /// </summary>
    public static string? GetCoverPath(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return null;

        // Try .jpg first (2D covers), then .png (3D covers)
        string jpg = Path.Combine(_coverDir, $"{gameId}.jpg");
        if (File.Exists(jpg)) return jpg;

        string png = Path.Combine(_coverDir, $"{gameId}.png");
        if (File.Exists(png)) return png;

        return null;
    }

    /// <summary>
    /// Downloads a cover for a single game. Returns true if successful.
    /// </summary>
    public static async Task<bool> DownloadCoverAsync(string gameId, CancellationToken token = default)
    {
        string url = $"https://raw.githubusercontent.com/xlenore/ps2-covers/main/covers/default/{Uri.EscapeDataString(gameId)}.jpg";
        string dest = Path.Combine(_coverDir, $"{gameId}.jpg");

        if (File.Exists(dest))
            return true;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var resp = await _http.GetAsync(url, token);
                if (resp.IsSuccessStatusCode)
                {
                    var bytes = await resp.Content.ReadAsByteArrayAsync(token);
                    await File.WriteAllBytesAsync(dest, bytes, token);
                    return true;
                }
                else if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return false;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // transient error — retry
            }

            await Task.Delay(RetryDelay, token);
        }

        return false;
    }

    /// <summary>
    /// Downloads covers for multiple games in parallel with progress reporting.
    /// </summary>
    public static async Task DownloadAllCoversAsync(
        IEnumerable<string> gameIds,
        IProgress<CoverDownloadProgress>? progress = null,
        CancellationToken token = default)
    {
        var serials = gameIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (serials.Count == 0)
            return;

        var sem = new SemaphoreSlim(MaxConcurrency);
        var tasks = new List<Task>();
        int completed = 0;

        foreach (var serial in serials)
        {
            token.ThrowIfCancellationRequested();
            await sem.WaitAsync(token);

            var s = serial;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    bool ok = await DownloadCoverAsync(s, token);
                    int done = Interlocked.Increment(ref completed);
                    progress?.Report(new CoverDownloadProgress
                    {
                        Completed = done,
                        Total = serials.Count,
                        Serial = s,
                        Success = ok
                    });
                }
                finally
                {
                    sem.Release();
                }
            }, token));
        }

        await Task.WhenAll(tasks);
    }
}

public class CoverDownloadProgress
{
    public int Completed { get; set; }
    public int Total { get; set; }
    public string Serial { get; set; } = "";
    public bool Success { get; set; }
}
