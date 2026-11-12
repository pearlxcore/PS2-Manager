namespace PS2Manager.Core.Art;

/// <summary>OPL art types. The suffix is what OPL expects in ART/&lt;OPLID&gt;_&lt;SUFFIX&gt;.png.</summary>
public enum ArtType
{
    Cover,       // COV  – front cover
    BackCover,   // COV2 – back cover
    Spine,       // LAB  – spine / label
    Icon,        // ICO  – disc icon
    Logo,        // LGO  – title logo
    Background,  // BG   – background
    Screenshot,  // SCR  – screenshot
    Screenshot2, // SCR2 – second screenshot
}

public static class ArtTypes
{
    public static readonly ArtType[] All = Enum.GetValues<ArtType>();

    public static string Suffix(this ArtType t) => t switch
    {
        ArtType.Cover => "COV",
        ArtType.BackCover => "COV2",
        ArtType.Spine => "LAB",
        ArtType.Icon => "ICO",
        ArtType.Logo => "LGO",
        ArtType.Background => "BG",
        ArtType.Screenshot => "SCR",
        ArtType.Screenshot2 => "SCR2",
        _ => throw new ArgumentOutOfRangeException(nameof(t)),
    };

    public static string DisplayName(this ArtType t) => t switch
    {
        ArtType.Cover => "Front cover",
        ArtType.BackCover => "Back cover",
        ArtType.Spine => "Spine",
        ArtType.Icon => "Disc icon",
        ArtType.Logo => "Logo",
        ArtType.Background => "Background",
        ArtType.Screenshot => "Screenshot 1",
        ArtType.Screenshot2 => "Screenshot 2",
        _ => t.ToString(),
    };

    public static bool TryParseSuffix(string suffix, out ArtType type)
    {
        foreach (var t in All)
            if (t.Suffix().Equals(suffix, StringComparison.OrdinalIgnoreCase)) { type = t; return true; }
        type = default;
        return false;
    }
}
