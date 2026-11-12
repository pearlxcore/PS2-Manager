namespace PS2Manager.Core.Models;

public enum DiscType { Unknown, CD, DVD }

/// <summary>
/// Editable game information. Used in three roles:
///  - database values (from GameDB-PS2),
///  - user overrides (only the fields the user changed are non-null),
///  - the merged result shown in the UI.
/// Field set mirrors what OPL stores in CFG/&lt;GAMEID&gt;.cfg.
/// </summary>
public sealed class GameInfo
{
    public string? Title { get; set; }
    public string? Genre { get; set; }
    public string? Developer { get; set; }
    public string? Publisher { get; set; }
    public string? Release { get; set; }
    public string? Region { get; set; }
    public string? Language { get; set; }
    public string? Description { get; set; }

    // OPL info-page attributes (stored as e.g. "players/2", "aspect/w")
    public string? Players { get; set; }
    public string? Rating { get; set; }
    public string? Aspect { get; set; }
    public string? Scan { get; set; }
    public string? Vmode { get; set; }
    public string? Parental { get; set; }

    // OPL launch settings
    public int? Compatibility { get; set; }   // $Compatibility bitmask, bit n = mode n+1
    public DiscType? Media { get; set; }       // CD/DVD placement override

    /// <summary>CFG keys we don't model, preserved verbatim on write.</summary>
    public Dictionary<string, string>? Extra { get; set; }

    public static readonly string[] TextFields =
    [
        nameof(Title), nameof(Genre), nameof(Developer), nameof(Publisher), nameof(Release),
        nameof(Region), nameof(Language), nameof(Description), nameof(Players), nameof(Rating),
        nameof(Aspect), nameof(Scan), nameof(Vmode), nameof(Parental),
    ];

    public string? Get(string field) => field switch
    {
        nameof(Title) => Title,
        nameof(Genre) => Genre,
        nameof(Developer) => Developer,
        nameof(Publisher) => Publisher,
        nameof(Release) => Release,
        nameof(Region) => Region,
        nameof(Language) => Language,
        nameof(Description) => Description,
        nameof(Players) => Players,
        nameof(Rating) => Rating,
        nameof(Aspect) => Aspect,
        nameof(Scan) => Scan,
        nameof(Vmode) => Vmode,
        nameof(Parental) => Parental,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };

    public void Set(string field, string? value)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        switch (field)
        {
            case nameof(Title): Title = value; break;
            case nameof(Genre): Genre = value; break;
            case nameof(Developer): Developer = value; break;
            case nameof(Publisher): Publisher = value; break;
            case nameof(Release): Release = value; break;
            case nameof(Region): Region = value; break;
            case nameof(Language): Language = value; break;
            case nameof(Description): Description = value; break;
            case nameof(Players): Players = value; break;
            case nameof(Rating): Rating = value; break;
            case nameof(Aspect): Aspect = value; break;
            case nameof(Scan): Scan = value; break;
            case nameof(Vmode): Vmode = value; break;
            case nameof(Parental): Parental = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty =>
        TextFields.All(f => Get(f) is null) && Compatibility is null && Media is null && (Extra is null || Extra.Count == 0);

    /// <summary>Returns a new GameInfo where every non-null field of <paramref name="top"/> wins.</summary>
    public static GameInfo Merge(GameInfo? bottom, GameInfo? top)
    {
        var result = new GameInfo();
        foreach (var f in TextFields)
            result.Set(f, top?.Get(f) ?? bottom?.Get(f));
        result.Compatibility = top?.Compatibility ?? bottom?.Compatibility;
        result.Media = top?.Media ?? bottom?.Media;
        if (bottom?.Extra is { Count: > 0 } || top?.Extra is { Count: > 0 })
        {
            result.Extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in bottom?.Extra ?? []) result.Extra[kv.Key] = kv.Value;
            foreach (var kv in top?.Extra ?? []) result.Extra[kv.Key] = kv.Value;
        }
        return result;
    }

    public GameInfo Clone() => Merge(null, this);
}
