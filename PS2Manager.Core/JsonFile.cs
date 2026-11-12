using System.Text.Json;
using System.Text.Json.Serialization;

namespace PS2Manager.Core;

/// <summary>Small helper for crash-safe JSON persistence (write temp, then replace).</summary>
public static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T? Load<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(fs, Options);
        }
        catch (Exception ex)
        {
            Log.Error($"Loading {path}", ex);
            return null;
        }
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
            JsonSerializer.Serialize(fs, value, Options);
        File.Move(tmp, path, overwrite: true);
    }
}
