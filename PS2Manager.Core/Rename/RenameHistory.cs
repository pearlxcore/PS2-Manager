namespace PS2Manager.Core.Rename;

public sealed record RenameMove(string From, string To);

public sealed class RenameBatch
{
    public string Description { get; set; } = "";
    public DateTime When { get; set; }
    public List<RenameMove> Moves { get; set; } = [];
}

/// <summary>Undo log for file operations, persisted so undo works after restarting the app.</summary>
public sealed class RenameHistory(string path)
{
    private const int MaxBatches = 30;
    private readonly List<RenameBatch> _batches = JsonFile.Load<List<RenameBatch>>(path) ?? [];

    public IReadOnlyList<RenameBatch> Batches => _batches;
    public RenameBatch? Last => _batches.Count > 0 ? _batches[^1] : null;

    public void Push(RenameBatch batch)
    {
        _batches.Add(batch);
        if (_batches.Count > MaxBatches) _batches.RemoveRange(0, _batches.Count - MaxBatches);
        Save();
    }

    /// <summary>Reverses the most recent batch. Returns the moves that were undone (To → From).</summary>
    public List<RenameMove> UndoLast(out List<string> errors)
    {
        errors = [];
        var undone = new List<RenameMove>();
        if (Last is not { } batch) return undone;

        foreach (var m in Enumerable.Reverse(batch.Moves))
        {
            try
            {
                if (!File.Exists(m.To)) { errors.Add($"{Path.GetFileName(m.To)}: no longer exists"); continue; }
                if (File.Exists(m.From) && !m.From.Equals(m.To, StringComparison.OrdinalIgnoreCase))
                { errors.Add($"{Path.GetFileName(m.From)}: original name is taken"); continue; }
                FileOperationPlan.MoveFile(m.To, m.From);
                undone.Add(m);
                TryRemoveEmptyDir(Path.GetDirectoryName(m.To));
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(m.To)}: {ex.Message}");
            }
        }

        _batches.RemoveAt(_batches.Count - 1);
        Save();
        return undone;
    }

    private static void TryRemoveEmptyDir(string? dir)
    {
        try
        {
            if (dir is not null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch { }
    }

    private void Save()
    {
        try { JsonFile.Save(path, _batches); }
        catch (Exception ex) { Log.Error("Saving rename history", ex); }
    }
}
