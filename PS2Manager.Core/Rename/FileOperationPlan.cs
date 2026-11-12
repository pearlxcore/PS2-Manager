using CommunityToolkit.Mvvm.ComponentModel;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Rename;

public enum PlanStatus { Ready, Unchanged, Conflict, Invalid, SourceMissing, Done, Failed }

public enum ConflictMode { Skip, AppendNumber }

/// <summary>One proposed file move/rename, shown in the preview grid before anything touches the disk.</summary>
public sealed partial class PlanItem : ObservableObject
{
    public required GameEntry Game { get; init; }
    public required string SourcePath { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetName), nameof(TargetDirectory))]
    private string _targetPath = "";

    [ObservableProperty] private PlanStatus _status;
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _include = true;

    public string SourceName => Path.GetFileName(SourcePath);
    public string TargetName => Path.GetFileName(TargetPath);
    public string TargetDirectory => Path.GetDirectoryName(TargetPath) ?? "";
    public bool CanApply => Include && Status == PlanStatus.Ready;
}

public sealed record PlanResult(int Succeeded, int Failed, int Skipped, RenameBatch? Batch, bool Cancelled = false);

/// <summary>Builds and executes previewable file operations (rename, move to folder, flatten, fix extension).</summary>
public static class FileOperationPlan
{
    public const int MaxNameLength = 240;

    /// <summary>Rename each file in place using a token template (extension preserved).</summary>
    public static List<PlanItem> Rename(IEnumerable<GameEntry> games, string template, ConflictMode mode)
        => Build(games, g =>
        {
            string name = RenameTokens.Expand(template, g);
            if (string.IsNullOrWhiteSpace(name)) return null;
            return Path.Combine(g.Directory, Truncate(name) + g.Extension);
        }, mode);

    /// <summary>Move each file into a subfolder (named by template) of a base directory, or its current directory.</summary>
    public static List<PlanItem> MoveToFolders(IEnumerable<GameEntry> games, string folderTemplate, string? baseDir, ConflictMode mode)
        => Build(games, g =>
        {
            string folder = RenameTokens.Expand(folderTemplate, g);
            if (string.IsNullOrWhiteSpace(folder)) return null;
            string root = baseDir ?? g.Directory;
            // Already inside a folder with that name? Don't nest again.
            if (baseDir is null && Path.GetFileName(g.Directory).Equals(folder, StringComparison.OrdinalIgnoreCase))
                return g.IsoPath;
            return Path.Combine(root, Truncate(folder), g.FileName);
        }, mode);

    /// <summary>Move every file directly into one target folder.</summary>
    public static List<PlanItem> Flatten(IEnumerable<GameEntry> games, string targetDir, ConflictMode mode)
        => Build(games, g => Path.Combine(targetDir, g.FileName), mode);

    /// <summary>.iso that is really a raw CD image → .bin, and vice versa.</summary>
    public static List<PlanItem> FixExtensions(IEnumerable<GameEntry> games, ConflictMode mode)
        => Build(games, g =>
        {
            string ext = g.Extension.ToLowerInvariant();
            if (ext is not (".iso" or ".bin" or ".img")) return g.IsoPath;
            bool raw = g.IsRawCdImage || Iso.DiscReader.LooksLikeRawCd(g.SizeBytes);
            string correct = raw ? ".bin" : ".iso";
            if (ext == correct || (ext == ".img" && !raw)) return g.IsoPath;
            return Path.ChangeExtension(g.IsoPath, correct);
        }, mode);

    private static List<PlanItem> Build(IEnumerable<GameEntry> games, Func<GameEntry, string?> target, ConflictMode mode)
    {
        var items = new List<PlanItem>();
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = games.ToList();

        // Pass 1: compute targets; unchanged files keep their name claimed.
        var pending = new List<(GameEntry g, string? t)>();
        foreach (var g in list)
        {
            string? t = null;
            try { t = target(g); } catch { /* invalid */ }
            pending.Add((g, t));
            if (t is not null && t.Equals(g.IsoPath, StringComparison.Ordinal)) claimed.Add(t);
        }

        foreach (var (g, t) in pending)
        {
            var item = new PlanItem { Game = g, SourcePath = g.IsoPath, TargetPath = t ?? g.IsoPath };

            if (!File.Exists(g.IsoPath)) { item.Status = PlanStatus.SourceMissing; item.Message = "File not found"; item.Include = false; }
            else if (t is null || !IsValidPath(t)) { item.Status = PlanStatus.Invalid; item.Message = "Template produced an empty or invalid name"; item.Include = false; }
            else if (t.Equals(g.IsoPath, StringComparison.Ordinal)) { item.Status = PlanStatus.Unchanged; item.Include = false; }
            else
            {
                string final = t;
                bool caseOnly = t.Equals(g.IsoPath, StringComparison.OrdinalIgnoreCase);
                bool Taken(string p) => claimed.Contains(p) || File.Exists(p);

                if (!caseOnly && Taken(final))
                {
                    if (mode == ConflictMode.AppendNumber)
                    {
                        string dir = Path.GetDirectoryName(t)!, stem = Path.GetFileNameWithoutExtension(t), ext = Path.GetExtension(t);
                        for (int n = 2; Taken(final); n++) final = Path.Combine(dir, $"{stem} ({n}){ext}");
                    }
                    else
                    {
                        item.Status = PlanStatus.Conflict;
                        item.Message = File.Exists(t) ? "A file with this name already exists" : "Another file in this batch gets the same name";
                        item.Include = false;
                        items.Add(item);
                        continue;
                    }
                }

                claimed.Add(final);
                item.TargetPath = final;
                item.Status = PlanStatus.Ready;
            }
            items.Add(item);
        }
        return items;
    }

    /// <summary>Executes the included, ready items. Updates each GameEntry's path on success.</summary>
    public static async Task<PlanResult> ApplyAsync(IEnumerable<PlanItem> items, string description,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        var todo = items.Where(i => i.CanApply).ToList();
        var batch = new RenameBatch { Description = description, When = DateTime.Now };
        int ok = 0, failed = 0, done = 0;

        await Task.Run(() =>
        {
            foreach (var item in todo)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    MoveFile(item.SourcePath, item.TargetPath);
                    batch.Moves.Add(new RenameMove(item.SourcePath, item.TargetPath));
                    item.Status = PlanStatus.Done;
                    ok++;
                }
                catch (Exception ex)
                {
                    item.Status = PlanStatus.Failed;
                    item.Message = ex.Message;
                    failed++;
                    Log.Error($"Move {item.SourcePath} -> {item.TargetPath}", ex);
                }
                progress?.Report((++done, todo.Count));
            }
        }, CancellationToken.None);

        // Update entries on the caller's thread (UI-bound objects).
        foreach (var item in todo.Where(i => i.Status == PlanStatus.Done))
            item.Game.IsoPath = item.TargetPath;

        return new PlanResult(ok, failed, items.Count() - todo.Count, batch.Moves.Count > 0 ? batch : null, ct.IsCancellationRequested);
    }

    /// <summary>Move that also handles case-only renames and cross-volume moves.</summary>
    public static void MoveFile(string from, string to)
    {
        string? dir = Path.GetDirectoryName(to);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (from.Equals(to, StringComparison.OrdinalIgnoreCase) && !from.Equals(to, StringComparison.Ordinal))
        {
            string tmp = from + ".renaming-" + Guid.NewGuid().ToString("N")[..8];
            File.Move(from, tmp);
            File.Move(tmp, to);
            return;
        }
        if (File.Exists(to)) throw new IOException($"Target already exists: {to}");
        File.Move(from, to);
    }

    private static bool IsValidPath(string path)
    {
        try
        {
            string name = Path.GetFileName(path);
            return !string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(name))
                && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && Path.IsPathFullyQualified(path);
        }
        catch { return false; }
    }

    private static string Truncate(string s) => s.Length <= MaxNameLength ? s : s[..MaxNameLength].TrimEnd();
}
