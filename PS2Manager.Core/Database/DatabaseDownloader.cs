namespace PS2Manager.Core.Database;

public static class DatabaseDownloader
{
    public const string Url = "https://github.com/niemasd/GameDB-PS2/releases/latest/download/PS2.data.json";

    /// <summary>Downloads the latest GameDB to <paramref name="destPath"/>. Returns false on failure; the old file is kept.</summary>
    public static async Task<bool> DownloadAsync(string destPath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string tmp = destPath + ".download";
        try
        {
            using var response = await Http.Client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                Log.Write($"Database download failed: HTTP {(int)response.StatusCode}");
                return false;
            }

            long total = response.Content.Headers.ContentLength ?? -1;
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            await using (var src = await response.Content.ReadAsStreamAsync(ct))
            await using (var dst = File.Create(tmp))
            {
                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                    done += read;
                    if (total > 0) progress?.Report((double)done / total);
                }
            }

            // Validate before replacing the working copy.
            await using (var check = File.OpenRead(tmp))
            {
                if (GameDatabase.Parse(check).Count == 0) throw new InvalidDataException("Downloaded database is empty.");
            }

            File.Move(tmp, destPath, overwrite: true);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error("Database download", ex);
            return false;
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }
}
