using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace PS2Manager.Core.SaveStates;

/// <summary>One original save state kept before an in-place conversion replaced it.</summary>
/// <param name="Original">Full path of the save state that was replaced.</param>
/// <param name="Backup">The verified copy, relative to the backup set folder.</param>
/// <param name="OriginalSha256">Hash of the original (and of the copy).</param>
/// <param name="ConvertedSha256">Hash of the converted file that replaced the original.</param>
public sealed record BackupEntry(string Original, string Backup, string OriginalSha256, string ConvertedSha256,
    string SourceFormat, string TargetFormat);

public sealed class BackupManifest
{
    public int Version { get; set; } = 1;
    public DateTime CreatedUtc { get; set; }
    public string SourceFolder { get; set; } = "";
    public string TargetRelease { get; set; } = "";
    public string Tool { get; set; } = "";
    public List<BackupEntry> Entries { get; set; } = [];
}

public enum RestoreOutcome
{
    Restored,
    /// <summary>The file at the original location already is the original.</summary>
    AlreadyOriginal,
    /// <summary>Left alone, for example because the file changed after the conversion.</summary>
    Skipped,
    Failed,
}

public sealed record RestoreResult(string Original, string? Destination, RestoreOutcome Outcome, string Message);

/// <summary>
/// The originals replaced by one in-place conversion, plus a manifest.json with what's needed to put
/// them back.
/// </summary>
public sealed class BackupSet
{
    public const string ManifestName = "manifest.json";
    private const string FilesFolder = "files";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _root;

    private BackupSet(string root, string? folder, BackupManifest manifest)
    {
        _root = root;
        Folder = folder;
        Manifest = manifest;
    }

    /// <summary>Documents\PCSX2 SaveState Converter\backups</summary>
    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCSX2 SaveState Converter", "backups");

    /// <summary>The set's folder; null until the first original is stored.</summary>
    public string? Folder { get; private set; }

    public BackupManifest Manifest { get; }

    public DateTime Created => Manifest.CreatedUtc.ToLocalTime();

    public string Description =>
        $"{Created:yyyy-MM-dd HH:mm:ss} - {Manifest.Entries.Count} save state(s) from {Manifest.SourceFolder} (converted to {Manifest.TargetRelease})";

    public override string ToString() => Description;

    /// <summary>Prepares a new set under <paramref name="root"/> for originals from <paramref name="sourceFolder"/>.</summary>
    public static BackupSet Create(string root, string sourceFolder, string targetRelease)
    {
        CheckRoot(root, sourceFolder);
        var version = typeof(BackupSet).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        return new BackupSet(Path.GetFullPath(root), null, new BackupManifest
        {
            CreatedUtc = DateTime.UtcNow,
            SourceFolder = Path.GetFullPath(sourceFolder),
            TargetRelease = targetRelease,
            Tool = $"PS2 Manager {version.Split('+')[0]}".Trim(),
        });
    }

    /// <summary>Backups must not land among the save states PCSX2 (and the next scan) reads.</summary>
    public static void CheckRoot(string root, string sourceFolder)
    {
        if (FileHash.IsSameOrInside(root, sourceFolder))
            throw new InvalidOperationException("The backup folder must be outside the save-state folder.");
    }

    public static BackupSet Open(string folder)
    {
        var full = Path.GetFullPath(folder);
        var manifestPath = Path.Combine(full, ManifestName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"No {ManifestName} in {full}; not a backup set.", manifestPath);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath), JsonOptions)
                       ?? throw new InvalidDataException("Empty backup manifest.");
        if (manifest.Version != 1)
            throw new InvalidDataException($"Unsupported backup manifest version {manifest.Version}.");
        return new BackupSet(Path.GetDirectoryName(full) ?? full, full, manifest);
    }

    /// <summary>Backup sets under <paramref name="root"/>, newest first.</summary>
    public static IReadOnlyList<BackupSet> List(string root)
    {
        if (!Directory.Exists(root)) return [];
        var sets = new List<BackupSet>();
        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            if (!File.Exists(Path.Combine(folder, ManifestName))) continue;
            try
            {
                sets.Add(Open(folder));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
            {
                // Skip sets that can't be read.
            }
        }
        return sets.OrderByDescending(x => x.Manifest.CreatedUtc).ToArray();
    }

    /// <summary>Copies <paramref name="original"/> into the set and verifies the copy against <paramref name="sha256"/>.</summary>
    /// <returns>Full path of the copy.</returns>
    public string Store(string original, string sha256)
    {
        var folder = EnsureFolder();
        var relative = Path.GetRelativePath(Manifest.SourceFolder, Path.GetFullPath(original));
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            relative = Path.GetFileName(original);
        var target = Path.Combine(folder, FilesFolder, relative);
        if (File.Exists(target))
            throw new IOException($"The backup set already holds {relative}.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var partial = target + ".partial";
        try
        {
            File.Copy(original, partial, overwrite: false);
            File.SetAttributes(partial, FileAttributes.Normal);
            if (FileHash.Sha256(partial) != sha256)
                throw new IOException("the backup copy does not match the original");
            File.Move(partial, target);
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
        return target;
    }

    public string RelativePath(string backupPath) => Path.GetRelativePath(Folder!, backupPath);

    public void Record(BackupEntry entry)
    {
        Manifest.Entries.Add(entry);
        Save();
    }

    /// <summary>Drops an entry whose original was not replaced after all, and its copy.</summary>
    public void Forget(BackupEntry entry)
    {
        Manifest.Entries.Remove(entry);
        var copy = Path.Combine(Folder!, entry.Backup);
        if (File.Exists(copy)) File.Delete(copy);
        if (Manifest.Entries.Count > 0)
        {
            Save();
            return;
        }
        // Nothing left, so don't leave an empty set behind.
        File.Delete(Path.Combine(Folder!, ManifestName));
        DeleteEmptyFolders(Path.Combine(Folder!, FilesFolder));
        if (Directory.Exists(Folder!) && !Directory.EnumerateFileSystemEntries(Folder!).Any())
            Directory.Delete(Folder!);
        Folder = null;
    }

    /// <summary>
    /// Puts every original back. In place, a file is replaced only while it still is the converted
    /// file this set recorded; anything saved over it since is left alone. With
    /// <paramref name="outputDirectory"/>, the originals are copied there instead (never overwriting).
    /// </summary>
    public IReadOnlyList<RestoreResult> Restore(string? outputDirectory = null)
    {
        var results = new List<RestoreResult>();
        foreach (var entry in Manifest.Entries)
        {
            try
            {
                results.Add(RestoreOne(entry, outputDirectory));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                results.Add(new(entry.Original, null, RestoreOutcome.Failed, ex.Message));
            }
        }
        return results;
    }

    private RestoreResult RestoreOne(BackupEntry entry, string? outputDirectory)
    {
        var copy = Path.Combine(Folder!, entry.Backup);
        if (!File.Exists(copy) || FileHash.Sha256(copy) != entry.OriginalSha256)
            return new(entry.Original, null, RestoreOutcome.Failed, "the backup copy is missing or damaged.");

        if (outputDirectory is not null)
        {
            var prefix = FilesFolder + Path.DirectorySeparatorChar;
            var relative = entry.Backup.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? entry.Backup[prefix.Length..] : Path.GetFileName(entry.Backup);
            var destination = Path.Combine(Path.GetFullPath(outputDirectory), relative);
            if (File.Exists(destination))
                return new(entry.Original, destination, RestoreOutcome.Skipped, "a file with this name already exists in the output folder.");
            CopyVerified(copy, destination, entry.OriginalSha256, overwrite: false);
            return new(entry.Original, destination, RestoreOutcome.Restored, "original copied to the output folder.");
        }

        if (File.Exists(entry.Original))
        {
            var current = FileHash.Sha256(entry.Original);
            if (current == entry.OriginalSha256)
                return new(entry.Original, entry.Original, RestoreOutcome.AlreadyOriginal, "already the original.");
            if (current != entry.ConvertedSha256)
                return new(entry.Original, null, RestoreOutcome.Skipped,
                    "changed since the conversion (for example a newer save); left as is. Restore to another folder to keep both.");
        }
        CopyVerified(copy, entry.Original, entry.OriginalSha256, overwrite: true);
        return new(entry.Original, entry.Original, RestoreOutcome.Restored, "original restored.");
    }

    private static void CopyVerified(string from, string to, string sha256, bool overwrite)
    {
        var directory = Path.GetDirectoryName(to)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(to)}.{Guid.NewGuid():N}.restore");
        try
        {
            File.Copy(from, temporary);
            File.SetAttributes(temporary, FileAttributes.Normal);
            if (FileHash.Sha256(temporary) != sha256)
                throw new IOException("the restored copy does not match the backup");
            File.Move(temporary, to, overwrite);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private string EnsureFolder()
    {
        if (Folder is not null) return Folder;
        var stamp = Manifest.CreatedUtc.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var folder = Path.Combine(_root, stamp);
        for (var n = 2; Directory.Exists(folder); n++)
            folder = Path.Combine(_root, $"{stamp}-{n}");
        Directory.CreateDirectory(folder);
        Folder = folder;
        return folder;
    }

    private void Save()
    {
        var path = Path.Combine(Folder!, ManifestName);
        var temporary = path + ".partial";
        File.WriteAllText(temporary, JsonSerializer.Serialize(Manifest, JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }

    private static void DeleteEmptyFolders(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var child in Directory.EnumerateDirectories(folder))
            DeleteEmptyFolders(child);
        if (!Directory.EnumerateFileSystemEntries(folder).Any())
            Directory.Delete(folder);
    }
}
