namespace PS2Manager.Core.SaveStates;

/// <summary>
/// Catalog files that ship with the app. They're read from the app folder when present, otherwise
/// from the copies embedded in this assembly (the single-file build has nothing next to the exe).
/// </summary>
internal static class ShippedData
{
    /// <summary>Reads the file from <paramref name="directory"/>, the app folder or the embedded copy, in that order.</summary>
    public static string? ReadText(string name, string? directory = null)
    {
        foreach (var folder in new[] { directory, AppContext.BaseDirectory })
        {
            if (folder is null) continue;
            var path = Path.Combine(folder, name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        return ReadEmbedded(name);
    }

    /// <summary>The copy embedded at build time (resource "catalog/NAME"), or null.</summary>
    internal static string? ReadEmbedded(string name)
    {
        using var stream = typeof(ShippedData).Assembly.GetManifestResourceStream("catalog/" + name);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
