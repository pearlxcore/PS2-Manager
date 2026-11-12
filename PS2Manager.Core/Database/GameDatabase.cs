using System.Text.Json;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Database;

/// <summary>
/// GameDB-PS2 (github.com/niemasd/GameDB-PS2) loaded once into a dictionary keyed by canonical serial.
/// </summary>
public sealed class GameDatabase
{
    private readonly Dictionary<string, GameInfo> _games = new(StringComparer.OrdinalIgnoreCase);

    public static GameDatabase Empty { get; } = new();

    public int Count => _games.Count;
    public DateTime? FileDate { get; private init; }

    private GameDatabase() { }

    public static GameDatabase Load(string path)
    {
        if (!File.Exists(path)) return Empty;
        try
        {
            using var fs = File.OpenRead(path);
            var db = Parse(fs);
            return new GameDatabase(db._games) { FileDate = File.GetLastWriteTime(path) };
        }
        catch (Exception ex)
        {
            Log.Error($"Loading database {path}", ex);
            return Empty;
        }
    }

    private GameDatabase(Dictionary<string, GameInfo> games) => _games = games;

    internal static GameDatabase Parse(Stream json)
    {
        var db = new GameDatabase();
        using var doc = JsonDocument.Parse(json);
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object) continue;
            var obj = entry.Value;

            var info = new GameInfo
            {
                Title = Read(obj, "title"),
                Region = Read(obj, "region"),
                Release = Read(obj, "release_date"),
                Genre = Read(obj, "genre"),
                Developer = Read(obj, "developer"),
                Publisher = Read(obj, "publisher"),
                Language = Read(obj, "language"),
            };

            // Index under the object key and every serial it lists.
            var keys = new List<string> { entry.Name };
            if (obj.TryGetProperty("serial", out var serial))
            {
                if (serial.ValueKind == JsonValueKind.String) keys.Add(serial.GetString()!);
                else if (serial.ValueKind == JsonValueKind.Array)
                    keys.AddRange(serial.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.String).Select(s => s.GetString()!));
            }

            foreach (var k in keys)
            {
                string id = GameIds.Normalize(k);
                if (GameIds.IsKnown(id)) db._games.TryAdd(id, info);
            }
        }
        return db;
    }

    public GameInfo? Lookup(string? gameId) =>
        GameIds.IsKnown(gameId) && _games.TryGetValue(GameIds.Normalize(gameId), out var info) ? info : null;

    /// <summary>Title search used for "identify this disc" lookups.</summary>
    public IEnumerable<(string GameId, GameInfo Info)> Search(string text, int max = 50)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return _games
            .Where(kv => kv.Key.Contains(text, StringComparison.OrdinalIgnoreCase)
                      || (kv.Value.Title?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(max)
            .Select(kv => (kv.Key, kv.Value));
    }

    private static string? Read(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p)) return null;
        string? s = p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            JsonValueKind.Array => string.Join(", ", p.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
