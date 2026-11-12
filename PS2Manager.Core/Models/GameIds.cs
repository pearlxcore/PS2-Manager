using System.Text.RegularExpressions;

namespace PS2Manager.Core.Models;

/// <summary>PS2 serial helpers. Canonical form is "SLUS-20062"; OPL form is "SLUS_200.62".</summary>
public static partial class GameIds
{
    public const string Unknown = "Unknown";

    public static bool IsKnown(string? id) =>
        !string.IsNullOrWhiteSpace(id) && !id.Equals(Unknown, StringComparison.OrdinalIgnoreCase);

    /// <summary>SLUS_200.03 / slus-20003 / SLUS20003 → SLUS-20003.</summary>
    public static string Normalize(string? id)
    {
        if (!IsKnown(id)) return Unknown;
        string s = id!.Trim().ToUpperInvariant().Replace("_", "").Replace("-", "").Replace(".", "").Replace(" ", "");
        if (s.Length > 4 && char.IsLetter(s[0]) && char.IsDigit(s[4]))
            s = s.Insert(4, "-");
        return s;
    }

    /// <summary>SLUS-20062 → SLUS_200.62 (the name OPL uses for ART/CFG files).</summary>
    public static string ToOplId(string? id)
    {
        string n = Normalize(id);
        var m = CanonicalRegex().Match(n);
        if (!m.Success) return n;
        string digits = m.Groups[2].Value;
        return $"{m.Groups[1].Value}_{digits[..3]}.{digits[3..]}";
    }

    /// <summary>Finds the first serial in arbitrary text (SYSTEM.CNF, file names…).</summary>
    public static string Extract(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Unknown;
        var m = DottedRegex().Match(text);
        if (!m.Success) m = PlainRegex().Match(text);
        return m.Success ? Normalize(m.Value) : Unknown;
    }

    public static string DetectRegion(string? gameId)
    {
        string n = Normalize(gameId);
        if (n.Length < 4) return "";
        return n[..4] switch
        {
            "SLUS" or "SCUS" or "PBPX" or "PUPX" => "NTSC-U",
            "SLES" or "SCES" or "SLED" or "SCED" => "PAL",
            "SLPM" or "SLPS" or "SCPS" or "SCPM" or "PAPX" or "PCPX" => "NTSC-J",
            "SLKA" or "SCKA" => "NTSC-K",
            "SLAJ" or "SCAJ" or "TCPS" or "TLES" => "NTSC-J",
            _ => "",
        };
    }

    [GeneratedRegex(@"^([A-Z]{4})-(\d{5})$")]
    private static partial Regex CanonicalRegex();

    [GeneratedRegex(@"[A-Z]{4}[-_ ]\d{3}\.\d{2}", RegexOptions.IgnoreCase)]
    private static partial Regex DottedRegex();

    [GeneratedRegex(@"[A-Z]{4}[-_ ]?\d{5}", RegexOptions.IgnoreCase)]
    private static partial Regex PlainRegex();
}
