using System.Buffers.Binary;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace PS2Manager.Core.SaveStates;

/// <summary>A save state written by an older PCSX2 that can be converted for the installed one.</summary>
public sealed record OutdatedState(string Path, StableFormat From);

/// <summary>A game's save states that the installed PCSX2 can't load as they are, but could after conversion.</summary>
public sealed record UpgradePlan(StableFormat Target, IReadOnlyList<OutdatedState> States)
{
    /// <summary>"2.4.0" or "2.0.0-2.0.3 and 2.4.0" for the message shown to the user.</summary>
    public string FromLabel => string.Join(" and ", States.Select(s => s.From.Label).Distinct());
}

/// <summary>
/// Finds a game's save states made by older PCSX2 releases and converts them in place (originals are
/// backed up first, into the same folder PCSX2 SaveState Converter uses, so its Backups window can restore them).
/// </summary>
public static class StateUpgrader
{
    private static readonly Lazy<Catalog?> SharedCatalog = new(() =>
    {
        try { return Catalog.Load(); } catch (Exception ex) { Log.Error("Save-state catalog", ex); return null; }
    });

    public static string BackupRoot => BackupSet.DefaultRoot;

    /// <summary>"2.8.2.0" → the catalog's release for 2.8.2, or null for versions it doesn't know (nightlies, 1.x).</summary>
    public static StableFormat? FindTarget(string? pcsx2Version, Catalog catalog)
    {
        if (string.IsNullOrWhiteSpace(pcsx2Version)) return null;
        var v = pcsx2Version.Trim().TrimStart('v', 'V');
        int cut = v.IndexOfAny(['-', '+', ' ']);
        if (cut >= 0) v = v[..cut];
        if (!Version.TryParse(v.Contains('.') ? v : v + ".0", out var parsed)) return null;
        return catalog.FindRelease($"{parsed.Major}.{Math.Max(parsed.Minor, 0)}.{Math.Max(parsed.Build, 0)}");
    }

    /// <summary>A game's save states: slots, the resume state and PCSX2's backups of overwritten slots.</summary>
    public static IReadOnlyList<string> GameStates(string saveStatesDir, string gameId, uint? crc)
    {
        if (string.IsNullOrEmpty(gameId) || !Directory.Exists(saveStatesDir)) return [];
        string prefix = crc is { } c ? $"{gameId} ({c:X8})." : $"{gameId} (*).";
        return ConversionService.StatePatterns
            .SelectMany(p => Directory.EnumerateFiles(saveStatesDir, prefix + p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The states among <paramref name="states"/> that need converting for <paramref name="pcsx2Version"/>, or null
    /// when there are none (or the version isn't one the converter knows). Only reads each file's small version entry.
    /// </summary>
    public static UpgradePlan? Plan(IEnumerable<string> states, string? pcsx2Version, Catalog? catalog = null)
    {
        catalog ??= SharedCatalog.Value;
        if (catalog is null || FindTarget(pcsx2Version, catalog) is not { } target) return null;

        var outdated = new List<OutdatedState>();
        foreach (var path in states)
        {
            if (ReadFormat(path) is not { } format || format == target.Format) continue;
            if (catalog.FindRoute(format, target.Format) is null) continue;          // newer, or no tested route
            if (catalog.FindFormat(format) is { } from) outdated.Add(new OutdatedState(path, from));
        }
        return outdated.Count == 0 ? null : new UpgradePlan(target, outdated);
    }

    /// <summary>Converts the planned states in place. Each original is backed up and verified before it's replaced.</summary>
    public static IReadOnlyList<ConversionResult> Upgrade(UpgradePlan plan, string saveStatesDir, string? backupRoot = null,
        IStateConverter? converter = null, Catalog? catalog = null)
    {
        catalog ??= SharedCatalog.Value ?? throw new InvalidOperationException("The save-state catalog couldn't be loaded.");
        converter ??= NativeConverter.Locate() ?? throw new FileNotFoundException("The save-state converter (pcsx2-stateconvert.exe) is missing.");
        var service = new ConversionService(catalog, converter);
        var backups = BackupSet.Create(backupRoot ?? BackupRoot, saveStatesDir, plan.Target.Version);
        return plan.States.Select(s => service.ConvertInPlace(s.Path, plan.Target.Version, backups)).ToList();
    }

    /// <summary>The save-state format code from "PCSX2 Savestate Version.id", or null if the file isn't a readable state.</summary>
    public static ushort? ReadFormat(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            using var archive = ArchiveFactory.OpenArchive(file, new ReaderOptions { LeaveStreamOpen = true });
            var entry = archive.Entries.FirstOrDefault(e => e.Key == "PCSX2 Savestate Version.id");
            if (entry is null || entry.Size < 4) return null;
            using var stream = entry.OpenEntryStream();
            Span<byte> head = stackalloc byte[4];
            stream.ReadExactly(head);
            return (ushort)(BinaryPrimitives.ReadUInt32LittleEndian(head) >> 16);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }
}
