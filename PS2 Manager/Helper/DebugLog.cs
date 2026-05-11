using System.Diagnostics;

namespace PS2_ISO_Manager.Helper;

public static class DebugLog
{
    private static readonly string _path =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");

    private static readonly object _lock = new();

    public static void Write(string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        Debug.WriteLine(line);
        lock (_lock)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }

    public static string LogPath => _path;
}
