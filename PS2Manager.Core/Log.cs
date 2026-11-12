using System.Diagnostics;

namespace PS2Manager.Core;

public static class Log
{
    private static readonly object _lock = new();
    private const long MaxBytes = 2 * 1024 * 1024;

    public static void Write(string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        Debug.WriteLine(line);
        try
        {
            lock (_lock)
            {
                var path = AppPaths.LogFile;
                var fi = new FileInfo(path);
                if (fi.Exists && fi.Length > MaxBytes)
                    File.Move(path, path + ".old", overwrite: true);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never take the app down.
        }
    }

    public static void Error(string context, Exception ex) => Write($"ERROR {context}: {ex.GetType().Name}: {ex.Message}");
}
