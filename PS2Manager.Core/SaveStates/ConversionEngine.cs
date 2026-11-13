namespace PS2Manager.Core.SaveStates;

public enum ConversionStatus
{
    /// <summary>Source and target share a format; nothing to convert.</summary>
    Compatible,
    /// <summary>A conversion route exists (see the route's validation status).</summary>
    Convertible,
    Unsupported,
}

public sealed record ConversionAssessment(
    ConversionStatus Status,
    ushort SourceFormat,
    ushort TargetFormat,
    ConversionRoute? Route,
    IReadOnlyList<string> Blockers);

/// <summary>
/// Decides how a source format reaches a target format, using only the routes listed in
/// catalog/routes.json. No route is inferred: unlisted pairs are unsupported.
/// </summary>
public static class ConversionEngine
{
    public static ConversionAssessment Assess(ushort sourceFormat, ushort targetFormat, Catalog catalog)
    {
        if (sourceFormat == targetFormat)
            return new(ConversionStatus.Compatible, sourceFormat, targetFormat, null, []);

        if (catalog.FindRoute(sourceFormat, targetFormat) is { } route)
            return new(ConversionStatus.Convertible, sourceFormat, targetFormat, route, []);

        var reason = catalog.FindUnsupported(sourceFormat, targetFormat)?.Reason
                     ?? (catalog.FindFormat(sourceFormat) is null
                         ? $"0x{sourceFormat:X4} is not a format written by an official stable release in the catalog."
                         : $"No conversion route from 0x{sourceFormat:X4} to 0x{targetFormat:X4} is implemented.");
        return new(ConversionStatus.Unsupported, sourceFormat, targetFormat, null, [reason]);
    }
}
