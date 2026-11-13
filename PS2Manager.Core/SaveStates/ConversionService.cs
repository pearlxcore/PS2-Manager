namespace PS2Manager.Core.SaveStates;

public enum ConversionOutcome
{
    Converted,
    AlreadyCompatible,
    Unsupported,
    Skipped,
    Failed,
}

public sealed record ConversionResult(
    string Source,
    string? Output,
    ConversionOutcome Outcome,
    ushort? SourceFormat,
    ushort? TargetFormat,
    string Message,
    ConversionRoute? Route,
    IReadOnlyList<string> Report)
{
    public bool Succeeded => Outcome is ConversionOutcome.Converted or ConversionOutcome.AlreadyCompatible;

    /// <summary>In-place conversions: the verified copy of the original that was replaced.</summary>
    public string? Backup { get; init; }
}

/// <summary>Results of converting a folder in place, and the backup set holding the replaced originals.</summary>
public sealed record InPlaceRun(IReadOnlyList<ConversionResult> Results, string? BackupFolder);

/// <summary>
/// Converts save states to another release format, either into a new file (the source is never
/// written) or in place after a verified backup of the original.
/// </summary>
public sealed class ConversionService(Catalog catalog, IStateConverter? converter)
{
    /// <summary>The files PCSX2 reads as save states: slots, and the backups it keeps of overwritten slots.</summary>
    public static readonly string[] StatePatterns = ["*.p2s", "*.p2s.backup"];

    public Catalog Catalog { get; } = catalog;
    public IStateConverter? Converter { get; } = converter;

    /// <summary>Save states in a folder, ignoring this tool's own temporary files.</summary>
    public static IReadOnlyList<string> EnumerateStates(string directory, bool recursive) =>
        StatePatterns
            .SelectMany(pattern => Directory.EnumerateFiles(directory, pattern,
                recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
            .Where(file => !Path.GetFileName(file).StartsWith('.'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public ConversionResult Convert(string input, string targetVersion, string output)
    {
        var source = Path.GetFullPath(input);
        var destination = Path.GetFullPath(output);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            return Result(source, ConversionOutcome.Failed,
                "output path must differ from the source; convert in place to replace originals (they are backed up first).");
        if (File.Exists(destination))
            return Result(source, ConversionOutcome.Skipped, "output already exists; refusing to overwrite it.");
        var directory = Path.GetDirectoryName(destination) ?? throw new InvalidOperationException("Output path has no directory.");

        var stopped = Prepare(source, targetVersion, directory, out var prepared);
        if (prepared is null) return stopped!;
        try
        {
            File.Move(prepared.Temporary, destination, overwrite: false);
            return prepared.Converted(destination,
                $"converted 0x{prepared.SourceFormat:X4} -> 0x{prepared.Release.Format:X4} ({prepared.RouteStatus} route).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return prepared.Failed($"output could not be written: {ex.Message}");
        }
        finally
        {
            prepared.Cleanup();
        }
    }

    /// <summary>
    /// Converts a save state and replaces it with the result. The original is first copied into
    /// <paramref name="backups"/> and the copy verified; the replacement happens only if the original
    /// did not change meanwhile. On any failure the original stays exactly as it was.
    /// </summary>
    public ConversionResult ConvertInPlace(string input, string targetVersion, BackupSet backups)
    {
        var source = Path.GetFullPath(input);
        var stopped = Prepare(source, targetVersion, Path.GetDirectoryName(source)!, out var prepared);
        if (prepared is null) return stopped!;

        BackupEntry? entry = null;
        string? backup = null;
        var replaced = false;
        try
        {
            backup = backups.Store(source, prepared.SourceHash);
            entry = new BackupEntry(source, backups.RelativePath(backup), prepared.SourceHash, FileHash.Sha256(prepared.Temporary),
                $"0x{prepared.SourceFormat:X4}", $"0x{prepared.Release.Format:X4}");
            backups.Record(entry);
            if (FileHash.Sha256(source) != prepared.SourceHash)
                throw new IOException("the save state changed during the conversion (is PCSX2 running?)");
            File.Move(prepared.Temporary, source, overwrite: true);
            replaced = true;
            return prepared.Converted(source,
                $"converted 0x{prepared.SourceFormat:X4} -> 0x{prepared.Release.Format:X4} in place ({prepared.RouteStatus} route); original backed up.")
                with { Backup = backup };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!replaced)
            {
                if (entry is not null) backups.Forget(entry);
                else if (backup is not null && File.Exists(backup)) File.Delete(backup);
            }
            return prepared.Failed($"original left unchanged: {ex.Message}");
        }
        finally
        {
            prepared.Cleanup();
        }
    }

    /// <summary>
    /// Converts every save state under inputDirectory into outputDirectory, keeping relative paths
    /// and file names (PCSX2 locates states by "SERIAL (CRC).slot.p2s").
    /// </summary>
    public IReadOnlyList<ConversionResult> ConvertDirectory(string inputDirectory, string targetVersion, string outputDirectory,
        bool recursive, IProgress<ConversionResult>? progress = null, CancellationToken cancellation = default)
    {
        var root = Path.GetFullPath(inputDirectory);
        var outputRoot = Path.GetFullPath(outputDirectory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        if (string.Equals(Path.TrimEndingDirectorySeparator(root), Path.TrimEndingDirectorySeparator(outputRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Output directory must differ from the input directory; convert in place to replace originals.");

        var results = new List<ConversionResult>();
        foreach (var file in EnumerateStates(root, recursive).Where(file => !FileHash.IsSameOrInside(file, outputRoot)))
        {
            cancellation.ThrowIfCancellationRequested();
            var result = Convert(file, targetVersion, Path.Combine(outputRoot, Path.GetRelativePath(root, file)));
            results.Add(result);
            progress?.Report(result);
        }
        return results;
    }

    /// <summary>Converts every save state under inputDirectory in place, backing the originals up into one new set under backupRoot.</summary>
    public InPlaceRun ConvertDirectoryInPlace(string inputDirectory, string targetVersion, bool recursive, string backupRoot,
        IProgress<ConversionResult>? progress = null, CancellationToken cancellation = default)
    {
        var root = Path.GetFullPath(inputDirectory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var backups = BackupSet.Create(backupRoot, root, targetVersion);

        var results = new List<ConversionResult>();
        foreach (var file in EnumerateStates(root, recursive))
        {
            cancellation.ThrowIfCancellationRequested();
            var result = ConvertInPlace(file, targetVersion, backups);
            results.Add(result);
            progress?.Report(result);
        }
        return new InPlaceRun(results, backups.Folder);
    }

    /// <summary>
    /// Inspects and assesses the source and converts it into a verified temporary file in
    /// workDirectory. Returns the result to report when there is nothing to write (already
    /// compatible, unsupported, failed); otherwise null and the prepared conversion.
    /// </summary>
    private ConversionResult? Prepare(string source, string targetVersion, string workDirectory, out Prepared? prepared)
    {
        prepared = null;
        var release = Catalog.FindRelease(targetVersion);
        if (release is null)
            return Result(source, ConversionOutcome.Failed, $"'{targetVersion}' is not an official stable release in the catalog.");

        var inspection = SaveStateInspector.Inspect(source, Catalog);
        if (!inspection.IsValid || inspection.Format is null)
            return Result(source, ConversionOutcome.Failed,
                $"source archive did not pass inspection: {inspection.Error ?? "missing " + string.Join(", ", inspection.MissingRequired)}");

        var format = inspection.Format.Value;
        var assessment = ConversionEngine.Assess(format, release.Format, Catalog);
        switch (assessment.Status)
        {
            case ConversionStatus.Compatible:
                return Result(source, ConversionOutcome.AlreadyCompatible,
                    $"already in the {release.Label} format (0x{release.Format:X4}); no conversion needed.", format, release.Format);
            case ConversionStatus.Unsupported:
                return Result(source, ConversionOutcome.Unsupported, string.Join(" ", assessment.Blockers), format, release.Format);
        }

        var route = assessment.Route!;
        if (Converter is null)
            return Result(source, ConversionOutcome.Failed,
                "the converter (pcsx2-stateconvert) was not found; set SAVECONVERT_NATIVE or install it next to the application.",
                format, release.Format, route);

        Directory.CreateDirectory(workDirectory);
        var temporary = Path.Combine(workDirectory, $".{Path.GetFileName(source)}.{Guid.NewGuid():N}.tmp.p2s");
        var sourceHash = FileHash.Sha256(source);
        var candidate = new Prepared(source, format, release, route, temporary, sourceHash, []);
        try
        {
            var result = Converter.Convert(source, temporary, route.Target);
            if (!result.Ok)
                return Result(source, ConversionOutcome.Failed, result.Error ?? "the converter failed.", format, release.Format, route, result.Report);

            var produced = SaveStateInspector.Inspect(temporary, Catalog);
            if (!produced.IsValid || produced.Format != release.Format)
                return Result(source, ConversionOutcome.Failed,
                    $"converted archive failed validation: {produced.Error ?? $"format {produced.FormatText}"}", format, release.Format, route, result.Report);
            if (FileHash.Sha256(source) != sourceHash)
                return Result(source, ConversionOutcome.Failed, "source file changed during conversion; result discarded.",
                    format, release.Format, route, result.Report);

            prepared = candidate with { Report = result.Report };
            return null;
        }
        finally
        {
            if (prepared is null) candidate.Cleanup();
        }
    }

    private static ConversionResult Result(string source, ConversionOutcome outcome, string message, ushort? format = null,
        ushort? target = null, ConversionRoute? route = null, IReadOnlyList<string>? report = null) =>
        new(source, null, outcome, format, target, message, route, report ?? []);

    private sealed record Prepared(string Source, ushort SourceFormat, StableFormat Release, ConversionRoute Route,
        string Temporary, string SourceHash, IReadOnlyList<string> Report)
    {
        public string RouteStatus => Route.Status.ToString().ToLowerInvariant();

        public ConversionResult Converted(string output, string message) =>
            new(Source, output, ConversionOutcome.Converted, SourceFormat, Release.Format, message, Route, Report);

        public ConversionResult Failed(string message) =>
            new(Source, null, ConversionOutcome.Failed, SourceFormat, Release.Format, message, Route, Report);

        /// <summary>The converter writes "&lt;path&gt;.partial" and renames it; a timeout can leave either behind.</summary>
        public void Cleanup()
        {
            foreach (var leftover in new[] { Temporary, Temporary + ".partial" })
                if (File.Exists(leftover)) File.Delete(leftover);
        }
    }
}
