using PS2Manager.Core.Models;

namespace PS2Manager.Core.Art;

/// <summary>
/// Local art cache: Art\&lt;GAMEID&gt;\&lt;SUFFIX&gt;.png|jpg. One file per (game, art type).
/// </summary>
public sealed class ArtStore(string rootDir)
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".bmp"];

    public string RootDir { get; } = rootDir;

    public string GameDir(string gameId) => Path.Combine(RootDir, Sanitize(GameIds.Normalize(gameId)));

    public string? Get(string gameId, ArtType type)
    {
        if (!GameIds.IsKnown(gameId)) return null;
        string dir = GameDir(gameId);
        foreach (var ext in Extensions)
        {
            string p = Path.Combine(dir, type.Suffix() + ext);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public bool Has(string gameId, ArtType type) => Get(gameId, type) is not null;

    public Dictionary<ArtType, string> GetAll(string gameId)
    {
        var d = new Dictionary<ArtType, string>();
        foreach (var t in ArtTypes.All)
            if (Get(gameId, t) is { } p) d[t] = p;
        return d;
    }

    /// <summary>Stores image bytes, replacing any existing file of that type. Returns the stored path.</summary>
    public string Save(string gameId, ArtType type, ReadOnlySpan<byte> data, string extension)
    {
        if (!GameIds.IsKnown(gameId)) throw new InvalidOperationException("Art needs a game ID.");
        extension = NormalizeExt(extension, data);
        string dir = GameDir(gameId);
        Directory.CreateDirectory(dir);
        Delete(gameId, type);
        string path = Path.Combine(dir, type.Suffix() + extension);
        string tmp = path + ".tmp";
        File.WriteAllBytes(tmp, data.ToArray());
        File.Move(tmp, path, overwrite: true);
        return path;
    }

    public string Import(string gameId, ArtType type, string sourceFile) =>
        Save(gameId, type, File.ReadAllBytes(sourceFile), Path.GetExtension(sourceFile));

    public void Delete(string gameId, ArtType type)
    {
        string dir = GameDir(gameId);
        foreach (var ext in Extensions)
        {
            string p = Path.Combine(dir, type.Suffix() + ext);
            if (File.Exists(p)) File.Delete(p);
        }
    }

    /// <summary>Picks the extension from the file signature, so a PNG served as .jpg is stored as .png.</summary>
    internal static string NormalizeExt(string ext, ReadOnlySpan<byte> data)
    {
        if (data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return ".png";
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ".jpg";
        if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D) return ".bmp";
        ext = ext.ToLowerInvariant();
        return ext == ".jpeg" ? ".jpg" : Extensions.Contains(ext) ? ext : ".png";
    }

    internal static bool IsImage(ReadOnlySpan<byte> data) =>
        data.Length > 64 &&
        ((data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) ||
         (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) ||
         (data[0] == 0x42 && data[1] == 0x4D));

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}
