using System.Text.Json;

namespace PS2Manager.Core.SaveStates;

/// <param name="Version">Release whose tag and commit are recorded (the latest release writing this format).</param>
/// <param name="Releases">Every official release that writes this format with the same save-state code;
/// defaults to <paramref name="Version"/> alone.</param>
public sealed record StableFormat(string Version, string Tag, string Commit, ushort Format, bool Stable, IReadOnlyList<string>? Releases = null)
{
    public IReadOnlyList<string> AllReleases => Releases is { Count: > 0 } releases ? releases : [Version];

    /// <summary>Display name: "2.4.0", or a range such as "2.0.0-2.0.3" for formats shared by several releases.</summary>
    public string Label => AllReleases.Count > 1 ? $"{AllReleases[0]}-{AllReleases[^1]}" : Version;

    public bool Covers(string version) => Version == version || AllReleases.Contains(version);
}

public enum RouteStatus
{
    /// <summary>Converted states resumed in the unmodified target release.</summary>
    Validated,
    /// <summary>Conversion implemented and structurally checked; gameplay not yet verified.</summary>
    Implemented,
}

public sealed record ConversionRoute(ushort Source, ushort Target, string Method, RouteStatus Status,
    IReadOnlyList<string> SourceReleases, string TargetRelease, string Evidence);

public sealed record UnsupportedRoute(ushort Source, ushort Target, string Reason);

/// <summary>Stable release formats and the conversion routes between them (catalog/*.json).</summary>
public sealed class Catalog
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<StableFormat> Formats { get; }
    public IReadOnlyList<ConversionRoute> Routes { get; }
    public IReadOnlyList<UnsupportedRoute> Unsupported { get; }

    private Catalog(IReadOnlyList<StableFormat> formats, IReadOnlyList<ConversionRoute> routes, IReadOnlyList<UnsupportedRoute> unsupported)
    {
        Formats = formats;
        Routes = routes;
        Unsupported = unsupported;
    }

    public StableFormat? FindRelease(string version) => Formats.FirstOrDefault(x => x.Stable && x.Covers(version));
    public StableFormat? FindFormat(ushort format) => Formats.FirstOrDefault(x => x.Stable && x.Format == format);
    public ConversionRoute? FindRoute(ushort source, ushort target) => Routes.FirstOrDefault(x => x.Source == source && x.Target == target);
    public UnsupportedRoute? FindUnsupported(ushort source, ushort target) => Unsupported.FirstOrDefault(x => x.Source == source && x.Target == target);

    public static Catalog Load(string? directory = null)
    {
        var formats = JsonSerializer.Deserialize<StableFormat[]>(Read("stable-formats.json", directory), Options) ?? [];

        using var document = JsonDocument.Parse(Read("routes.json", directory));
        var routes = document.RootElement.GetProperty("routes").EnumerateArray().Select(x => new ConversionRoute(
            ParseFormat(x.GetProperty("source").GetString()),
            ParseFormat(x.GetProperty("target").GetString()),
            x.GetProperty("method").GetString() ?? "direct",
            Enum.Parse<RouteStatus>(x.GetProperty("status").GetString() ?? "Implemented", ignoreCase: true),
            x.GetProperty("sourceReleases").EnumerateArray().Select(r => r.GetString() ?? "").ToArray(),
            x.GetProperty("targetRelease").GetString() ?? "",
            x.GetProperty("evidence").GetString() ?? "")).ToArray();
        var unsupported = document.RootElement.GetProperty("unsupported").EnumerateArray().Select(x => new UnsupportedRoute(
            ParseFormat(x.GetProperty("source").GetString()),
            ParseFormat(x.GetProperty("target").GetString()),
            x.GetProperty("reason").GetString() ?? "")).ToArray();
        return new Catalog(formats, routes, unsupported);
    }

    private static ushort ParseFormat(string? text)
    {
        if (text is null || !text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Invalid format identifier '{text}' in routes.json.");
        return Convert.ToUInt16(text[2..], 16);
    }

    private static string Read(string name, string? directory) =>
        ShippedData.ReadText(name, directory) ?? throw new FileNotFoundException($"{name} not found.");
}
