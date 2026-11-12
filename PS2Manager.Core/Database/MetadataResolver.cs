using PS2Manager.Core.Iso;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Database;

/// <summary>Builds the merged <see cref="GameEntry.Info"/>: GameDB values overlaid with user overrides.</summary>
public sealed class MetadataResolver(Func<GameDatabase> database, OverrideStore overrides)
{
    public GameDatabase Database => database();
    public OverrideStore Overrides => overrides;

    public GameInfo? DatabaseInfo(GameEntry e) => database().Lookup(e.GameId);

    public void Resolve(GameEntry e)
    {
        var db = database().Lookup(e.GameId);
        var ovr = overrides.Get(e.OverrideKey);

        var baseInfo = db?.Clone() ?? new GameInfo();
        baseInfo.Region ??= NullIfEmpty(GameIds.DetectRegion(e.GameId));

        var merged = GameInfo.Merge(baseInfo, ovr);

        TitleSource source;
        if (ovr?.Title is not null) source = TitleSource.Override;
        else if (db?.Title is not null) source = TitleSource.Database;
        else if (!string.IsNullOrWhiteSpace(e.VolumeLabel))
        {
            merged.Title = DiscReader.HumanizeVolumeLabel(e.VolumeLabel);
            source = TitleSource.VolumeLabel;
        }
        else
        {
            merged.Title = Path.GetFileNameWithoutExtension(e.IsoPath);
            source = TitleSource.Filename;
        }

        e.Info = merged;
        e.TitleSource = source;
        e.HasOverrides = ovr is not null && !ovr.IsEmpty;
        e.InDatabase = db is not null;
    }

    public void ResolveAll(IEnumerable<GameEntry> entries)
    {
        foreach (var e in entries) Resolve(e);
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
}
