using PS2Manager.Core.Art;
using PS2Manager.Core.Models;
using PS2Manager.Core.Rename;

namespace PS2Manager.Core.Opl;

public enum TransferMode { Copy, Move }

public sealed class TransferOptions
{
    public TransferMode Mode { get; init; } = TransferMode.Copy;
    /// <summary>Optional file-name template applied on the device (null keeps the current name).</summary>
    public string? RenameTemplate { get; init; }
    public bool WriteCfg { get; init; } = true;
    public bool ExportArt { get; init; } = true;
    public bool OverwriteExisting { get; init; }
}

public readonly record struct TransferProgress(int GameIndex, int GameCount, string Game, long FileBytesDone, long FileBytesTotal, long AllBytesDone, long AllBytesTotal);

public sealed record TransferItemResult(GameEntry Game, string? TargetPath, bool Success, bool Skipped, string Message);

/// <summary>Writes games, CFG files and art into an OPL device layout.</summary>
public sealed class OplService(ArtStore art)
{
    // ---------------- CFG ----------------

    public GameInfo? ReadCfg(OplRoot root, GameEntry game)
    {
        if (!game.HasGameId) return null;
        string path = root.CfgPath(game.GameId);
        return File.Exists(path) ? OplCfg.Load(path).ToGameInfo() : null;
    }

    /// <summary>Writes the merged game info into CFG/&lt;OPLID&gt;.cfg, keeping keys OPL added itself.</summary>
    public string? WriteCfg(OplRoot root, GameEntry game)
    {
        if (!game.HasGameId) return null;
        string path = root.CfgPath(game.GameId);
        var cfg = OplCfg.Load(path);
        cfg.Apply(game.Info);
        cfg.Save(path);
        return path;
    }

    // ---------------- Art ----------------

    /// <summary>Copies cached art into ART/ with OPL naming. Returns number of files written.</summary>
    public int ExportArt(OplRoot root, GameEntry game, bool overwrite = true)
    {
        if (!game.HasGameId) return 0;
        int n = 0;
        Directory.CreateDirectory(root.ArtDir);
        foreach (var (type, src) in art.GetAll(game.GameId))
        {
            string ext = Path.GetExtension(src).ToLowerInvariant();
            string dest = root.ArtPath(game.GameId, type, ext);
            if (!overwrite && File.Exists(dest)) continue;
            // Remove the same art type in the other format so OPL doesn't pick a stale one.
            foreach (var other in new[] { ".png", ".jpg" }.Where(e => e != ext))
            {
                string stale = root.ArtPath(game.GameId, type, other);
                if (File.Exists(stale)) File.Delete(stale);
            }
            File.Copy(src, dest, overwrite: true);
            n++;
        }
        return n;
    }

    /// <summary>Imports art already on the device into the local cache (for games added by other tools).</summary>
    public int ImportArt(OplRoot root, GameEntry game)
    {
        if (!game.HasGameId || !Directory.Exists(root.ArtDir)) return 0;
        int n = 0;
        string prefix = game.OplId + "_";
        foreach (var file in Directory.EnumerateFiles(root.ArtDir, prefix + "*"))
        {
            string suffix = Path.GetFileNameWithoutExtension(file)[prefix.Length..];
            if (ArtTypes.TryParseSuffix(suffix, out var type) && !art.Has(game.GameId, type))
            {
                art.Import(game.GameId, type, file);
                n++;
            }
        }
        return n;
    }

    // ---------------- Games ----------------

    /// <summary>Where a game would land on the device.</summary>
    public static string TargetPath(OplRoot root, GameEntry game, string? renameTemplate)
    {
        string name = game.FileName;
        if (!string.IsNullOrWhiteSpace(renameTemplate))
        {
            string expanded = RenameTokens.Expand(renameTemplate, game);
            if (!string.IsNullOrWhiteSpace(expanded)) name = expanded + game.Extension;
        }
        // OPL only loads .iso from CD/DVD; raw .bin images can't be used directly.
        if (!name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
            name = Path.ChangeExtension(name, ".iso");
        return Path.Combine(root.GameDirFor(game.DiscType == DiscType.Unknown ? DiscType.DVD : game.DiscType), name);
    }

    /// <summary>Problems that would stop a game from working on this device.</summary>
    public static string? Validate(OplRoot root, GameEntry game)
    {
        if (!File.Exists(game.IsoPath)) return "Source file is missing";
        if (game.IsRawCdImage) return "Raw CD image (.bin); convert to ISO first, OPL can't load it";
        if (root.Drive is { IsFat32: true } && game.SizeBytes > DriveDetails.Fat32MaxFile)
            return "Larger than 4 GB; FAT32 can't store it (use exFAT or split with USBUtil)";
        return null;
    }

    public async Task<List<TransferItemResult>> TransferAsync(
        OplRoot root, IReadOnlyList<GameEntry> games, TransferOptions options,
        IProgress<TransferProgress>? progress = null, CancellationToken ct = default)
    {
        root.CreateLayout();
        var results = new List<TransferItemResult>();
        long allTotal = games.Sum(g => g.SizeBytes), allDone = 0;

        for (int i = 0; i < games.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var g = games[i];
            string target = TargetPath(root, g, options.RenameTemplate);

            if (Validate(root, g) is { } problem)
            {
                results.Add(new(g, null, false, true, problem));
                allDone += g.SizeBytes;
                continue;
            }

            try
            {
                bool exists = File.Exists(target);
                bool same = exists && new FileInfo(target).Length == g.SizeBytes;
                bool samePath = Path.GetFullPath(target).Equals(Path.GetFullPath(g.IsoPath), StringComparison.OrdinalIgnoreCase);

                if (samePath || (same && !options.OverwriteExisting))
                {
                    allDone += g.SizeBytes;
                    progress?.Report(new(i + 1, games.Count, g.Title, g.SizeBytes, g.SizeBytes, allDone, allTotal));
                }
                else
                {
                    if (exists && !options.OverwriteExisting)
                        throw new IOException("A different file with this name is already on the device");

                    long baseDone = allDone;
                    var p = new Progress<long>(b => progress?.Report(new(i + 1, games.Count, g.Title, b, g.SizeBytes, baseDone + b, allTotal)));

                    if (options.Mode == TransferMode.Move && SameVolume(g.IsoPath, target))
                    {
                        if (exists) File.Delete(target);
                        File.Move(g.IsoPath, target);
                    }
                    else
                    {
                        await CopyFileAsync(g.IsoPath, target, p, ct);
                        if (options.Mode == TransferMode.Move) File.Delete(g.IsoPath);
                    }
                    allDone += g.SizeBytes;
                }

                if (options.WriteCfg) WriteCfg(root, g);
                if (options.ExportArt) ExportArt(root, g);

                if (options.Mode == TransferMode.Move) g.IsoPath = target;
                results.Add(new(g, target, true, samePath || (same && !options.OverwriteExisting), samePath ? "Already on device" : same ? "Already on device (same size)" : "OK"));
            }
            catch (OperationCanceledException)
            {
                TryDelete(target + ".part");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"OPL transfer {g.IsoPath}", ex);
                results.Add(new(g, target, false, false, ex.Message));
                allDone += g.SizeBytes;
            }
        }
        return results;
    }

    private static async Task CopyFileAsync(string src, string dest, IProgress<long> progress, CancellationToken ct)
    {
        string part = dest + ".part";
        const int bufferSize = 4 << 20;
        await using (var input = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
        await using (var output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
        {
            output.SetLength(input.Length);
            var buffer = new byte[bufferSize];
            long done = 0;
            int read;
            var lastReport = DateTime.MinValue;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if ((DateTime.UtcNow - lastReport).TotalMilliseconds > 100) { progress.Report(done); lastReport = DateTime.UtcNow; }
            }
            progress.Report(done);
        }
        File.Move(part, dest, overwrite: true);
    }

    private static bool SameVolume(string a, string b) =>
        string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
}
