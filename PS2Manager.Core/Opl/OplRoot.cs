using PS2Manager.Core.Art;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Opl;

/// <summary>
/// An OPL device root (USB drive, SMB share or HDD folder) with the standard layout:
/// CD\, DVD\, ART\, CFG\, VMC\, THM\, POPS\, APPS\.
/// </summary>
public sealed class OplRoot(string path)
{
    public static readonly string[] StandardFolders = ["CD", "DVD", "ART", "CFG", "VMC", "THM", "POPS", "APPS", "CHT", "LNG"];

    public string Path { get; } = System.IO.Path.GetFullPath(path);

    public string CdDir => Combine("CD");
    public string DvdDir => Combine("DVD");
    public string ArtDir => Combine("ART");
    public string CfgDir => Combine("CFG");

    public bool Exists => Directory.Exists(Path);

    /// <summary>True when at least the game folders exist.</summary>
    public bool LooksLikeOplRoot => Directory.Exists(CdDir) || Directory.Exists(DvdDir);

    public IEnumerable<string> MissingFolders => StandardFolders.Where(f => !Directory.Exists(Combine(f)));

    public void CreateLayout()
    {
        foreach (var f in StandardFolders) Directory.CreateDirectory(Combine(f));
    }

    public string GameDirFor(DiscType type) => type == DiscType.CD ? CdDir : DvdDir;

    public string CfgPath(string gameId) => System.IO.Path.Combine(CfgDir, GameIds.ToOplId(gameId) + ".cfg");

    public string ArtPath(string gameId, ArtType type, string extension) =>
        System.IO.Path.Combine(ArtDir, $"{GameIds.ToOplId(gameId)}_{type.Suffix()}{extension}");

    public IEnumerable<string> GameFolders => new[] { CdDir, DvdDir }.Where(Directory.Exists);

    public DriveDetails? Drive
    {
        get
        {
            try
            {
                var root = System.IO.Path.GetPathRoot(Path);
                if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\")) return null;
                var d = new DriveInfo(root);
                return d.IsReady ? new DriveDetails(d.Name, d.VolumeLabel, d.DriveFormat, d.TotalSize, d.AvailableFreeSpace, d.DriveType) : null;
            }
            catch { return null; }
        }
    }

    private string Combine(string folder) => System.IO.Path.Combine(Path, folder);
}

public sealed record DriveDetails(string Name, string Label, string Format, long TotalBytes, long FreeBytes, DriveType Type)
{
    /// <summary>FAT32 can't hold files of 4 GiB or more.</summary>
    public bool IsFat32 => Format.Equals("FAT32", StringComparison.OrdinalIgnoreCase);
    public const long Fat32MaxFile = 4L * 1024 * 1024 * 1024 - 1;
}
