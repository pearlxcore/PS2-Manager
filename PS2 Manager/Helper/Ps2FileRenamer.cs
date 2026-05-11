using System.Text.RegularExpressions;

namespace PS2_ISO_Manager.Helper;

public static class Ps2FileRenamer
{
    private static readonly Dictionary<string, Func<Ps2IsoInfo, string>> _parameters = new()
    {
        ["{title}"]     = g => Sanitize(g.Title),
        ["{gameid}"]    = g => g.GameId != "Unknown" ? g.GameId : "",
        ["{region}"]    = g => g.Region != "Unknown" ? g.Region : "",
        ["{disc}"]      = g => g.DiscNumber > 1 ? $"Disc {g.DiscNumber}" : "",
        ["{version}"]   = g => !string.IsNullOrEmpty(g.Version) ? $"v{g.Version}" : "",
    };

    public static IReadOnlyCollection<string> ParameterNames => _parameters.Keys;

    public static string BuildFileName(Ps2IsoInfo game, string template)
    {
        string result = template;
        foreach (var kv in _parameters)
            result = result.Replace(kv.Key, kv.Value(game));

        // Clean up
        result = Regex.Replace(result, @"\(\s*\)", ""); // remove empty parens
        result = Regex.Replace(result, @"\[\s*\]", ""); // remove empty brackets
        result = Regex.Replace(result, @"\{[^}]+\}", ""); // remove unreplaced tokens
        result = Regex.Replace(result, @"\s*-\s*-", " -");
        result = Regex.Replace(result, @"\s+", " "); // collapse whitespace
        result = result.Trim();

        // Preserve original extension
        string ext = Path.GetExtension(game.IsoPath);
        if (string.IsNullOrEmpty(ext))
            ext = ".iso";
        if (!result.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            result += ext;

        return result;
    }

    public static string Rename(Ps2IsoInfo game, string template)
    {
        string newName = BuildFileName(game, template);
        string? dir = Path.GetDirectoryName(game.IsoPath);
        if (string.IsNullOrEmpty(dir)) return "";

        string newPath = Path.Combine(dir, newName);

        if (game.IsoPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
            return newPath; // already named correctly

        if (File.Exists(newPath))
            return ""; // target exists, skip

        File.Move(game.IsoPath, newPath);
        game.IsoPath = newPath;
        return newPath;
    }

    private static string Sanitize(string name)
    {
        // Remove characters invalid in Windows file names
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in invalid)
            name = name.Replace(c.ToString(), "");
        return name.Trim();
    }

    public static string BuildFolderName(Ps2IsoInfo game, string template)
    {
        string result = template;
        foreach (var kv in _parameters)
            result = result.Replace(kv.Key, kv.Value(game));

        result = Regex.Replace(result, @"\(\s*\)", "");
        result = Regex.Replace(result, @"\[\s*\]", "");
        result = Regex.Replace(result, @"\{[^}]+\}", "");
        result = Regex.Replace(result, @"\s+", " ");
        result = result.Trim();

        return string.IsNullOrWhiteSpace(result) ? Sanitize(game.Title) : Sanitize(result);
    }

    public static string MoveToFolder(Ps2IsoInfo game, string folderTemplate)
    {
        string folderName = BuildFolderName(game, folderTemplate);
        string? parentDir = Path.GetDirectoryName(game.IsoPath);
        if (string.IsNullOrEmpty(parentDir)) return "";

        string newDir = Path.Combine(parentDir, folderName);
        string newPath = Path.Combine(newDir, Path.GetFileName(game.IsoPath));

        if (game.IsoPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
            return newPath;

        if (!File.Exists(game.IsoPath))
            return ""; // source missing (stale manifest path)

        if (File.Exists(newPath))
            return ""; // target already exists

        if (!Directory.Exists(newDir))
            Directory.CreateDirectory(newDir);

        try
        {
            File.Move(game.IsoPath, newPath);
            game.IsoPath = newPath;
            return newPath;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"MoveToFolder failed: {game.IsoPath} -> {newPath} — {ex.Message}");
            return "";
        }
    }

    public static string Preview(string template)
    {
        // Show a sample with placeholder values
        var dummy = new Ps2IsoInfo
        {
            Title = "Sample Game",
            GameId = "SLUS-20999",
            Region = "NTSC-U",
            DiscNumber = 2,
            Version = "1.01"
        };
        return BuildFileName(dummy, template);
    }
}
