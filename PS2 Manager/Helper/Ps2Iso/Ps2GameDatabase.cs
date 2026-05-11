using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public sealed class Ps2GameDatabase
{
    private readonly Dictionary<string, string> _titles =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Ps2GameMetadata> _games =
        new(StringComparer.OrdinalIgnoreCase);

    // ===============================
    // JSON helper (string OR array)
    // ===============================
    private static string ReadStringOrArray(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
            return "Unknown";

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString()!,
            JsonValueKind.Array => string.Join(", ",
                prop.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString())),
            _ => "Unknown"
        };
    }

    private static Ps2GameMetadata ParseMetadata(JsonElement obj)
    {
        return new Ps2GameMetadata
        {
            Title       = ReadStringOrArray(obj, "title"),
            Serial      = ReadStringOrArray(obj, "serial"),
            Region      = ReadStringOrArray(obj, "region"),
            ReleaseDate = ReadStringOrArray(obj, "release_date"),
            Genre       = ReadStringOrArray(obj, "genre"),
            Developer   = ReadStringOrArray(obj, "developer"),
            Publisher   = ReadStringOrArray(obj, "publisher"),
            Language    = ReadStringOrArray(obj, "language")
        };
    }

    // ===============================
    // Constructor
    // ===============================
    public Ps2GameDatabase(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            return;

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));

        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object)
                continue;

            var obj = entry.Value;

            if (!obj.TryGetProperty("serial", out var serialProp))
                continue;

            string serial = Normalize(serialProp.GetString()!);

            // Title
            string title = ReadStringOrArray(obj, "title");
            _titles[serial] = title;

            // Full metadata
            _games[serial] = ParseMetadata(obj);
        }
    }

    // ===============================
    // Public API
    // ===============================
    public string GetTitle(string isoGameId)
    {
        string key = Normalize(isoGameId);
        return _titles.TryGetValue(key, out var title)
            ? title
            : "Unknown";
    }

    public Ps2GameMetadata? GetMetadata(string isoGameId)
    {
        string key = Normalize(isoGameId);
        return _games.TryGetValue(key, out var meta)
            ? meta
            : null;
    }

    // ===============================
    // Normalization
    // ===============================
    private static string Normalize(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        id = id.Trim().ToUpperInvariant();
        id = id.Replace('_', '-');
        id = id.Replace(".", "");
        return id;
    }
}
