using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using PS2Manager.Core.Art;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Pcsx2;

/// <summary>PCSX2's compatibility rating (GameIndex.yaml "compat").</summary>
public enum Pcsx2Compat
{
    [Description("—")] Unknown = 0,
    [Description("Nothing")] Nothing = 1,
    [Description("Intro")] Intro = 2,
    [Description("Menus")] Menus = 3,
    [Description("In-game")] InGame = 4,
    [Description("Playable")] Playable = 5,
    [Description("Perfect")] Perfect = 6,
}

public enum Pcsx2Window { [Description("PCSX2 setting")] Default, [Description("Fullscreen")] Fullscreen, [Description("Windowed")] Windowed }
public enum Pcsx2Boot { [Description("PCSX2 setting")] Default, [Description("Fast boot (skip BIOS)")] Fast, [Description("Full boot (show BIOS)")] Full }

/// <summary>How a game is launched. Everything else comes from PCSX2's own settings.</summary>
public sealed record Pcsx2LaunchOptions(Pcsx2Window Window = Pcsx2Window.Default, Pcsx2Boot Boot = Pcsx2Boot.Default,
    bool NoGui = false, bool BigPicture = false)
{
    public IReadOnlyList<string> BuildArguments(string discPath)
    {
        var args = new List<string>();
        if (Window == Pcsx2Window.Fullscreen) args.Add("-fullscreen");
        if (Window == Pcsx2Window.Windowed) args.Add("-nofullscreen");
        if (Boot == Pcsx2Boot.Fast) args.Add("-fastboot");
        if (Boot == Pcsx2Boot.Full) args.Add("-slowboot");
        if (NoGui) args.Add("-nogui");
        if (BigPicture) args.Add("-bigpicture");
        args.Add("--");          // everything after this is the file name
        args.Add(discPath);
        return args;
    }
}

/// <summary>A PCSX2 (Qt) installation: where the program is and where it keeps its data.</summary>
public sealed class Pcsx2Install
{
    public const string ExeName = "pcsx2-qt.exe";

    public Pcsx2Install(string exePath, string dataDir)
    {
        ExePath = exePath;
        DataDir = dataDir;
    }

    public string ExePath { get; }
    public string DataDir { get; }
    public string ExeDir => Path.GetDirectoryName(ExePath)!;
    public string CoversDir => Path.Combine(DataDir, "covers");
    public string IniPath => Path.Combine(DataDir, "inis", "PCSX2.ini");
    public string GameIndexPath => Path.Combine(ExeDir, "resources", "GameIndex.yaml");
    public bool IsPortable => DataDir.Equals(ExeDir, StringComparison.OrdinalIgnoreCase);

    public string? Version
    {
        get { try { return FileVersionInfo.GetVersionInfo(ExePath).ProductVersion; } catch { return null; } }
    }

    public static bool IsRunning => Process.GetProcessesByName("pcsx2-qt").Length > 0;

    /// <summary>
    /// Finds PCSX2. <paramref name="exeOverride"/> wins when it exists; otherwise common install locations are tried.
    /// Data lives next to the exe for portable installs (portable.ini / portable.txt), else in Documents\PCSX2.
    /// </summary>
    public static Pcsx2Install? Detect(string? exeOverride, string? documentsDir = null)
    {
        documentsDir ??= Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var candidates = new List<string?>
        {
            exeOverride,
            Env(Environment.SpecialFolder.ProgramFiles, "PCSX2"),
            Env(Environment.SpecialFolder.ProgramFilesX86, "PCSX2"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PCSX2", ExeName),
            Env(Environment.SpecialFolder.DesktopDirectory, "PCSX2"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "PCSX2", ExeName),
            @"C:\PCSX2\" + ExeName,
            @"C:\Games\PCSX2\" + ExeName,
        };
        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            string exe = Directory.Exists(c) ? Path.Combine(c, ExeName) : c;
            if (File.Exists(exe)) return FromExe(exe, documentsDir);
        }
        return null;

        static string Env(Environment.SpecialFolder f, string sub) => Path.Combine(Environment.GetFolderPath(f), sub, ExeName);
    }

    public static Pcsx2Install FromExe(string exe, string? documentsDir = null)
    {
        documentsDir ??= Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string dir = Path.GetDirectoryName(Path.GetFullPath(exe))!;
        bool portable = File.Exists(Path.Combine(dir, "portable.ini")) || File.Exists(Path.Combine(dir, "portable.txt"));
        return new Pcsx2Install(Path.GetFullPath(exe), portable ? dir : Path.Combine(documentsDir, "PCSX2"));
    }

    public Process? Launch(string discPath, Pcsx2LaunchOptions options)
    {
        var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false, WorkingDirectory = ExeDir };
        foreach (var a in options.BuildArguments(discPath)) psi.ArgumentList.Add(a);
        Log.Write($"PCSX2 launch: {ExePath} {string.Join(' ', psi.ArgumentList)}");
        return Process.Start(psi);
    }

    // ------------------------------------------------------------------ covers

    private static readonly string[] CoverExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>PCSX2 looks up covers by serial (e.g. SLUS-20062.jpg) in its covers folder.</summary>
    public string? FindCover(string gameId) =>
        string.IsNullOrEmpty(gameId) ? null
            : CoverExtensions.Select(e => Path.Combine(CoversDir, gameId + e)).FirstOrDefault(File.Exists);

    /// <summary>Copies the app's front covers into PCSX2's covers folder. Returns (copied, skipped existing, no cover).</summary>
    public (int Copied, int Existing, int NoCover) ExportCovers(IEnumerable<GameEntry> games, ArtStore art, bool overwrite)
    {
        Directory.CreateDirectory(CoversDir);
        int copied = 0, existing = 0, none = 0;
        foreach (var id in games.Where(g => g.HasGameId).Select(g => g.GameId).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var src = art.Get(id, ArtType.Cover);
            if (src is null) { none++; continue; }
            var current = FindCover(id);
            if (current is not null && !overwrite) { existing++; continue; }
            if (current is not null) File.Delete(current);
            File.Copy(src, Path.Combine(CoversDir, id + Path.GetExtension(src).ToLowerInvariant()), overwrite: true);
            copied++;
        }
        return (copied, existing, none);
    }

    // ------------------------------------------------------------------ game list folders

    /// <summary>Folders PCSX2 already scans (both recursive and non-recursive entries).</summary>
    public IReadOnlyList<(string Path, bool Recursive)> ReadGameListPaths() =>
        File.Exists(IniPath) ? Pcsx2Ini.ReadGameListPaths(File.ReadAllText(IniPath)) : [];

    /// <summary>
    /// Adds library folders PCSX2 doesn't scan yet as recursive game-list paths. PCSX2 rewrites its ini on exit,
    /// so this refuses while PCSX2 is running. A backup is written first. Returns the folders added.
    /// </summary>
    public IReadOnlyList<string> AddGameListPaths(IEnumerable<string> folders)
    {
        if (IsRunning) throw new InvalidOperationException("Close PCSX2 first; it overwrites its settings when it exits.");
        if (!File.Exists(IniPath)) throw new FileNotFoundException("PCSX2.ini not found. Start PCSX2 once to create it.", IniPath);

        string text = File.ReadAllText(IniPath);
        var (updated, added) = Pcsx2Ini.AddRecursivePaths(text, folders);
        if (added.Count == 0) return added;

        File.Copy(IniPath, IniPath + ".ps2manager.bak", overwrite: true);
        File.WriteAllText(IniPath, updated, new UTF8Encoding(false));
        Log.Write($"PCSX2 game list: added {string.Join(", ", added)}");
        return added;
    }
}

/// <summary>Minimal, format-preserving edits to PCSX2.ini.</summary>
public static class Pcsx2Ini
{
    public static IReadOnlyList<(string Path, bool Recursive)> ReadGameListPaths(string ini)
    {
        var result = new List<(string, bool)>();
        bool inSection = false;
        foreach (var raw in ini.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('[')) { inSection = line.Equals("[GameList]", StringComparison.OrdinalIgnoreCase); continue; }
            if (!inSection) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (value.Length == 0) continue;
            if (key.Equals("RecursivePaths", StringComparison.OrdinalIgnoreCase)) result.Add((value, true));
            else if (key.Equals("Paths", StringComparison.OrdinalIgnoreCase)) result.Add((value, false));
        }
        return result;
    }

    /// <summary>True when <paramref name="folder"/> is already scanned by one of the existing entries.</summary>
    public static bool IsCovered(string folder, IEnumerable<(string Path, bool Recursive)> existing)
    {
        string f = Normalize(folder);
        return existing.Any(e =>
        {
            string p = Normalize(e.Path);
            return f.Equals(p, StringComparison.OrdinalIgnoreCase)
                   || (e.Recursive && f.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase));
        });
    }

    public static (string Text, IReadOnlyList<string> Added) AddRecursivePaths(string ini, IEnumerable<string> folders)
    {
        var existing = ReadGameListPaths(ini).ToList();
        var added = new List<string>();
        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder) || IsCovered(folder, existing)) continue;
            existing.Add((folder, true));
            added.Add(folder);
        }
        if (added.Count == 0) return (ini, added);

        string nl = ini.Contains("\r\n") ? "\r\n" : "\n";
        var lines = ini.Replace("\r\n", "\n").Split('\n').ToList();
        int header = lines.FindIndex(l => l.Trim().Equals("[GameList]", StringComparison.OrdinalIgnoreCase));
        var newLines = added.Select(a => "RecursivePaths = " + a).ToList();

        if (header < 0)
        {
            while (lines.Count > 0 && lines[^1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
            lines.Add("");
            lines.Add("[GameList]");
            lines.AddRange(newLines);
            lines.Add("");
        }
        else
        {
            // Insert after the last non-blank line of the section.
            int insertAt = header + 1;
            for (int i = header + 1; i < lines.Count && !lines[i].TrimStart().StartsWith('['); i++)
                if (lines[i].Trim().Length > 0) insertAt = i + 1;
            lines.InsertRange(insertAt, newLines);
        }
        return (string.Join(nl, lines), added);
    }

    private static string Normalize(string p) => p.Trim().TrimEnd('\\', '/').Replace('/', '\\');
}

/// <summary>Reads compatibility ratings from PCSX2's GameIndex.yaml (line-based; no YAML library needed).</summary>
public static class Pcsx2GameIndex
{
    private static readonly Regex Serial = new(@"^([A-Z]{4}-\d{5}):\s*$", RegexOptions.Compiled);
    private static readonly Regex Compat = new(@"^\s+compat:\s*(\d)", RegexOptions.Compiled);

    public static Dictionary<string, Pcsx2Compat> Load(string path)
    {
        var map = new Dictionary<string, Pcsx2Compat>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return map;
        string? current = null;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0) continue;
            if (!char.IsWhiteSpace(line[0]))
            {
                var m = Serial.Match(line);
                current = m.Success ? m.Groups[1].Value : null;
                continue;
            }
            if (current is null) continue;
            var c = Compat.Match(line);
            if (c.Success && int.TryParse(c.Groups[1].Value, out int v) && v is >= 0 and <= 6)
            {
                map[current] = (Pcsx2Compat)v;
                current = null;   // only the top-level compat of this entry
            }
        }
        return map;
    }
}
