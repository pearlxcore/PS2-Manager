using PS2Manager.Core.Models;

namespace PS2Manager.Core.Database;

/// <summary>
/// User edits to game info, kept separately from GameDB so they survive database updates.
/// Keyed by <see cref="GameEntry.OverrideKey"/>.
/// </summary>
public sealed class OverrideStore
{
    private readonly string _path;
    private readonly Dictionary<string, GameInfo> _items;
    private readonly object _lock = new();

    public OverrideStore(string path)
    {
        _path = path;
        _items = new Dictionary<string, GameInfo>(
            JsonFile.Load<Dictionary<string, GameInfo>>(path) ?? [], StringComparer.OrdinalIgnoreCase);
    }

    public int Count { get { lock (_lock) return _items.Count; } }

    public GameInfo? Get(string key)
    {
        lock (_lock) return _items.TryGetValue(key, out var v) ? v.Clone() : null;
    }

    public void Set(string key, GameInfo? info)
    {
        lock (_lock)
        {
            if (info is null || info.IsEmpty) _items.Remove(key);
            else _items[key] = info.Clone();
            Save();
        }
    }

    public void Remove(string key) => Set(key, null);

    /// <summary>Moves a path-keyed override when the file was renamed/moved.</summary>
    public void Rekey(string oldKey, string newKey)
    {
        if (oldKey.Equals(newKey, StringComparison.OrdinalIgnoreCase)) return;
        lock (_lock)
        {
            if (!_items.Remove(oldKey, out var v)) return;
            _items[newKey] = v;
            Save();
        }
    }

    private void Save()
    {
        try { JsonFile.Save(_path, _items); }
        catch (Exception ex) { Log.Error("Saving overrides", ex); }
    }
}
