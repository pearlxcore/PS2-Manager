using System.Text.RegularExpressions;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Rename;

public sealed record RenameToken(string Token, string Description, Func<GameEntry, string> Value);

/// <summary>
/// Token engine for file and folder names. Tokens are case-insensitive, so templates from the
/// old version ({title}, {gameid}, {region}, {disc}, {version}) keep working.
/// </summary>
public static partial class RenameTokens
{
    public static IReadOnlyList<RenameToken> All { get; } =
    [
        new("{TITLE}", "Game title (database or your edit)", g => g.Title),
        new("{GAME_ID}", "Serial, e.g. SLUS-20062", g => g.HasGameId ? g.GameId : ""),
        new("{GAME_ID_OPL}", "OPL serial, e.g. SLUS_200.62", g => g.HasGameId ? g.OplId : ""),
        new("{REGION}", "NTSC-U / PAL / NTSC-J", g => g.Region),
        new("{VERSION}", "Disc version from SYSTEM.CNF, e.g. 1.01", g => g.Version),
        new("{DISC}", "\"Disc 2\" for multi-disc games, empty otherwise", g => g.IsMultiDisc ? $"Disc {g.DiscNumber}" : ""),
        new("{DISC_NUM}", "Disc number (always)", g => g.DiscNumber.ToString()),
        new("{MEDIA}", "CD or DVD", g => g.DiscType == DiscType.Unknown ? "" : g.DiscType.ToString()),
        new("{DEVELOPER}", "Developer", g => g.Developer),
        new("{PUBLISHER}", "Publisher", g => g.Publisher),
        new("{GENRE}", "Genre", g => g.Genre),
        new("{YEAR}", "Release year", g => g.Year),
        new("{LANGUAGE}", "Languages", g => g.Info.Language ?? ""),
        new("{CRC}", "PCSX2 game CRC (8 hex digits)", g => g.Pcsx2Crc?.ToString("X8") ?? ""),
    ];

    /// <summary>Old lowercase names that differ from the new ones.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["{gameid}"] = "{GAME_ID}",
        ["{id}"] = "{GAME_ID}",
        ["{serial}"] = "{GAME_ID}",
        ["{oplid}"] = "{GAME_ID_OPL}",
    };

    public static string Expand(string template, GameEntry game)
    {
        string result = TokenRegex().Replace(template, m =>
        {
            string tok = Aliases.TryGetValue(m.Value, out var a) ? a : m.Value;
            var def = All.FirstOrDefault(t => t.Token.Equals(tok, StringComparison.OrdinalIgnoreCase));
            return def is null ? "" : SanitizePart(def.Value(game));
        });
        return Tidy(result);
    }

    /// <summary>Unknown tokens in a template (for validation in the UI).</summary>
    public static IEnumerable<string> UnknownTokens(string template) =>
        TokenRegex().Matches(template).Select(m => m.Value)
            .Where(t => !Aliases.ContainsKey(t) && !All.Any(d => d.Token.Equals(t, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>Removes characters Windows forbids in file names.</summary>
    public static string SanitizePart(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace(": ", " - ").Replace(':', '-').Replace('/', '-').Replace('\\', '-').Replace('"', '\'');
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c.ToString(), "");
        return s.Trim();
    }

    /// <summary>Cleans up separators left behind by empty tokens.</summary>
    internal static string Tidy(string s)
    {
        s = EmptyBracketsRegex().Replace(s, "");
        s = RepeatedDashRegex().Replace(s, " - ");
        s = WhitespaceRegex().Replace(s, " ");
        s = s.Trim(' ', '-', '_', '.', ',');
        return s;
    }

    [GeneratedRegex(@"\{[A-Za-z_0-9]+\}")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\(\s*\)|\[\s*\]|\{\s*\}")]
    private static partial Regex EmptyBracketsRegex();

    [GeneratedRegex(@"\s*-\s*(-\s*)+")]
    private static partial Regex RepeatedDashRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
