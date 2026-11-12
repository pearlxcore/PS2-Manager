using System.Text.Json;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Library;

public sealed class LibraryFile
{
    public int Version { get; set; } = 2;
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public List<string> Folders { get; set; } = [];
    public List<GameEntry> Games { get; set; } = [];
}

/// <summary>Persists the scanned library (the "manifest") so startup doesn't rescan.</summary>
public static class LibraryStore
{
    public static LibraryFile? Load(string? path = null) => JsonFile.Load<LibraryFile>(path ?? AppPaths.LibraryFile);

    public static void Save(IEnumerable<string> folders, IEnumerable<GameEntry> games, string? path = null)
    {
        var file = new LibraryFile
        {
            SavedAt = DateTime.Now,
            Folders = folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Games = games.ToList(),
        };
        JsonFile.Save(path ?? AppPaths.LibraryFile, file);
    }

    /// <summary>
    /// Imports a manifest written by the old WinForms version (ps2_manifest.json).
    /// Only file/disc fields are taken; metadata is re-resolved from the database.
    /// </summary>
    public static List<GameEntry> ImportLegacyManifest(string path)
    {
        var result = new List<GameEntry>();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("Games", out var games) || games.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Not a PS2 Manager manifest (no \"Games\" array).");

        foreach (var g in games.EnumerateArray())
        {
            string iso = Str(g, "IsoPath");
            if (string.IsNullOrEmpty(iso)) continue;

            var e = new GameEntry
            {
                IsoPath = iso,
                SizeBytes = Num(g, "SizeBytes") is { } l ? (long)l : 0,
                GameId = GameIds.Normalize(Str(g, "GameId")),
                BootElf = Str(g, "BootElf"),
                Version = Str(g, "Version"),
                DiscNumber = Num(g, "DiscNumber") is { } d and > 0 and < 10 ? (int)d : 1,
                Pcsx2Crc = Num(g, "Pcsx2Crc") is { } p and >= 0 and <= uint.MaxValue ? (uint)p : null,
                Crc32 = Num(g, "Crc32") is { } c and >= 0 and <= uint.MaxValue ? (uint)c : null,
            };

            // Old titles that came from the volume label are kept as the label fallback.
            if (Str(g, "TitleSource") is "VolumeLabel" or "2") e.VolumeLabel = Str(g, "Title");

            e.Validity = Str(g, "Validity") switch
            {
                "NoSystemCnf" or "1" => DiscValidity.NoSystemCnf,
                "NoGameId" or "2" => DiscValidity.NoGameId,
                "ReadError" or "3" => DiscValidity.ReadError,
                _ => DiscValidity.Valid,
            };

            if (File.Exists(iso))
            {
                var fi = new FileInfo(iso);
                e.SizeBytes = fi.Length;
                e.LastWriteUtc = fi.LastWriteTimeUtc;
                e.IsRawCdImage = Iso.DiscReader.LooksLikeRawCd(fi.Length);
                e.DetectedDiscType = e.IsRawCdImage || fi.Length <= 360_000L * 2048 ? DiscType.CD : DiscType.DVD;
            }
            result.Add(e);
        }
        return result;
    }

    /// <summary>A numeric property as long; null when missing, null-valued or not a number.</summary>
    private static long? Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out long v) ? v : null;

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) ? p.ValueKind switch
        {
            JsonValueKind.String => p.GetString() ?? "",
            JsonValueKind.Number => p.GetRawText(),
            _ => "",
        } : "";
}
