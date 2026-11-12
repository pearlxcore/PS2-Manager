using System.Globalization;
using System.Text;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Opl;

/// <summary>
/// OPL per-game config (CFG/&lt;OPLID&gt;.cfg): one Key=Value per line. Key order and keys we
/// don't understand are preserved, so files written by OPL or OPL Manager survive a round trip.
/// </summary>
public sealed class OplCfg
{
    private readonly List<KeyValuePair<string, string>> _entries = [];

    /// <summary>CFG keys mapped onto <see cref="GameInfo"/> fields.</summary>
    internal static readonly (string Key, string Field)[] InfoKeys =
    [
        ("Title", nameof(GameInfo.Title)),
        ("Description", nameof(GameInfo.Description)),
        ("Developer", nameof(GameInfo.Developer)),
        ("Publisher", nameof(GameInfo.Publisher)),
        ("Genre", nameof(GameInfo.Genre)),
        ("Release", nameof(GameInfo.Release)),
        ("Players", nameof(GameInfo.Players)),
        ("Rating", nameof(GameInfo.Rating)),
        ("Aspect", nameof(GameInfo.Aspect)),
        ("Scan", nameof(GameInfo.Scan)),
        ("Vmode", nameof(GameInfo.Vmode)),
        ("Parental", nameof(GameInfo.Parental)),
    ];

    public const string CompatibilityKey = "$Compatibility";

    public IReadOnlyList<KeyValuePair<string, string>> Entries => _entries;

    public string? this[string key]
    {
        get => _entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
        set
        {
            int i = _entries.FindIndex(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(value)) { if (i >= 0) _entries.RemoveAt(i); return; }
            value = value.Replace("\r", " ").Replace("\n", " ");
            if (i >= 0) _entries[i] = new(_entries[i].Key, value);
            else _entries.Add(new(key, value));
        }
    }

    public static OplCfg Parse(string text)
    {
        var cfg = new OplCfg();
        foreach (var raw in text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            cfg._entries.Add(new(line[..eq].Trim(), line[(eq + 1)..]));
        }
        return cfg;
    }

    public static OplCfg Load(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : new OplCfg();

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var (k, v) in _entries) sb.Append(k).Append('=').Append(v).Append("\r\n");
        return sb.ToString();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ToString(), new UTF8Encoding(false));
    }

    /// <summary>Reads the info fields (and unknown keys as Extra) into a GameInfo.</summary>
    public GameInfo ToGameInfo()
    {
        var info = new GameInfo();
        var known = new HashSet<string>(InfoKeys.Select(k => k.Key), StringComparer.OrdinalIgnoreCase) { CompatibilityKey };
        foreach (var (key, field) in InfoKeys) info.Set(field, this[key]);
        if (int.TryParse(this[CompatibilityKey], NumberStyles.Integer, CultureInfo.InvariantCulture, out int compat))
            info.Compatibility = compat;

        foreach (var (k, v) in _entries)
        {
            if (known.Contains(k)) continue;
            (info.Extra ??= new(StringComparer.OrdinalIgnoreCase))[k] = v;
        }
        return info;
    }

    /// <summary>Writes info fields into this cfg; other keys are left as they are.</summary>
    public void Apply(GameInfo info)
    {
        foreach (var (key, field) in InfoKeys) this[key] = info.Get(field);
        this[CompatibilityKey] = info.Compatibility is > 0 ? info.Compatibility.Value.ToString(CultureInfo.InvariantCulture) : null;
        foreach (var (k, v) in info.Extra ?? []) this[k] = v;
    }
}

/// <summary>Allowed values for OPL's info-page attributes (used by the editor's dropdowns).</summary>
public static class OplCfgValues
{
    public static readonly string[] Players = ["players/1", "players/2", "players/3", "players/4", "players/5", "players/6", "players/7", "players/8"];
    public static readonly string[] Rating = ["rating/0", "rating/1", "rating/2", "rating/3", "rating/4", "rating/5"];
    public static readonly string[] Aspect = ["aspect/s", "aspect/w", "aspect/w1", "aspect/w2"];
    public static readonly string[] Scan = ["scan/240p", "scan/240p1", "scan/480i", "scan/480p", "scan/480p1", "scan/480p2", "scan/480p3", "scan/480p4", "scan/480p5", "scan/576i", "scan/576p", "scan/720p", "scan/1080i", "scan/1080i2", "scan/1080p"];
    public static readonly string[] Vmode = ["vmode/ntsc", "vmode/pal", "vmode/multi"];
    public static readonly string[] Parental = ["parental/3", "parental/7", "parental/12", "parental/16", "parental/18"];

    /// <summary>OPL compatibility modes ($Compatibility bitmask; bit 0 = Mode 1).</summary>
    public static readonly (int Bit, string Name, string Hint)[] CompatibilityModes =
    [
        (0, "Mode 1", "Accurate reads"),
        (1, "Mode 2", "Synchronous mode"),
        (2, "Mode 3", "Unhook syscalls"),
        (3, "Mode 4", "Skip videos (PSS)"),
        (4, "Mode 5", "Emulate DVD-DL"),
        (5, "Mode 6", "Disable IGR"),
        (6, "Mode 7", ""),
        (7, "Mode 8", ""),
    ];
}
