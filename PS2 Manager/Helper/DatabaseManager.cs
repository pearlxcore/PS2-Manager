namespace PS2_ISO_Manager.Helper;

public static class DatabaseManager
{
    private const string DbUrl = "https://github.com/niemasd/GameDB-PS2/releases/latest/download/PS2.data.json";

    public static string DefaultPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PS2.data.json");

    public static bool Exists(string path) => File.Exists(path);

    public static async Task<string?> DownloadLatestAsync(string? destPath = null, IProgress<int>? progress = null)
    {
        string dest = destPath ?? DefaultPath;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

            using var response = await client.GetAsync(DbUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return null;

            long total = response.Content.Headers.ContentLength ?? -1;
            using var stream = await response.Content.ReadAsStreamAsync();

            string dir = Path.GetDirectoryName(dest)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using var fileStream = File.Create(dest);
            byte[] buffer = new byte[8192];
            int read;
            long downloaded = 0;

            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                if (total > 0)
                    progress?.Report((int)(downloaded * 100 / total));
            }

            return dest;
        }
        catch
        {
            return null;
        }
    }
}
