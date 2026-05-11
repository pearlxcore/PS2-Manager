using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PS2_ISO_Manager.Helper.Manifest;

public static class ManifestManager
{
    private static string ManifestDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Manifests");
    private static string ManifestPath => Path.Combine(ManifestDir, "ps2_manifest.json");

    public static string Save(List<Ps2IsoInfo> list)
    {
        if (!Directory.Exists(ManifestDir))
            Directory.CreateDirectory(ManifestDir);

        var manifest = new Ps2Manifest { Games = list, SavedAt = DateTime.Now };

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, options));

        return ManifestPath;
    }

    public static Ps2Manifest LoadLatest()
    {
        if (!File.Exists(ManifestPath)) return null;
        return JsonSerializer.Deserialize<Ps2Manifest>(File.ReadAllText(ManifestPath));
    }
}