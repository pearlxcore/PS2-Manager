namespace PS2Manager.Core;

/// <summary>
/// All on-disk locations used by the app. Everything lives under
/// %LocalAppData%\PS2Manager so the app works from any install location.
/// </summary>
public static class AppPaths
{
    /// <summary>Set PS2MANAGER_DATA to keep data elsewhere (portable installs, testing).</summary>
    private static string _root =
        Environment.GetEnvironmentVariable("PS2MANAGER_DATA") is { Length: > 0 } custom
            ? Path.GetFullPath(custom)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PS2Manager");

    public static string Root => Ensure(_root);

    /// <summary>Redirects all storage (used by tests).</summary>
    public static void SetRoot(string root) => _root = root;

    public static string DataDir => Ensure(Path.Combine(Root, "Data"));
    public static string ArtDir => Ensure(Path.Combine(Root, "Art"));
    public static string LogDir => Ensure(Path.Combine(Root, "Logs"));

    public static string DatabaseFile => Path.Combine(DataDir, "PS2.data.json");
    public static string LibraryFile => Path.Combine(Root, "library.json");
    public static string OverridesFile => Path.Combine(Root, "overrides.json");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string RenameHistoryFile => Path.Combine(Root, "rename-history.json");
    public static string ArtMissesFile => Path.Combine(Root, "art-misses.json");
    public static string LogFile => Path.Combine(LogDir, "debug.log");

    private static string Ensure(string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }
}
