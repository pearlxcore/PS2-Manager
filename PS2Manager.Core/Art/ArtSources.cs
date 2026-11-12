using System.Net;
using PS2Manager.Core.Models;

namespace PS2Manager.Core.Art;

public sealed record ArtImage(byte[] Data, string Extension, string SourceName);

public interface IArtSource
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool Supports(ArtType type);
    /// <summary>Returns null when the source has no image for this game/type.</summary>
    Task<ArtImage?> FetchAsync(string gameId, ArtType type, CancellationToken ct);
}

/// <summary>Base for sources that map (game, type) to a list of candidate URLs.</summary>
public abstract class UrlArtSource : IArtSource
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract bool Supports(ArtType type);
    protected abstract IEnumerable<string> Urls(string gameId, ArtType type);

    public async Task<ArtImage?> FetchAsync(string gameId, ArtType type, CancellationToken ct)
    {
        if (!GameIds.IsKnown(gameId) || !Supports(type)) return null;

        Exception? transient = null;
        foreach (var url in Urls(gameId, type))
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var resp = await Http.Client.GetAsync(url, ct);
                    if (resp.StatusCode == HttpStatusCode.NotFound) break;            // definitive: try next URL
                    if (resp.IsSuccessStatusCode)
                    {
                        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                        if (!ArtStore.IsImage(bytes)) break;
                        return new ArtImage(bytes, ArtStore.NormalizeExt(Path.GetExtension(url), bytes), Name);
                    }
                    if ((int)resp.StatusCode is >= 400 and < 500 and not 429) break;  // permanent
                    if (attempt == 3) transient = new HttpRequestException($"HTTP {(int)resp.StatusCode} from {url}");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) when (attempt == 3) { transient = ex; Log.Error($"Art fetch {url}", ex); }
                catch { /* transient: retry */ }

                if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(attempt * 1.5), ct);
            }
        }
        // Network trouble is not the same as "this art doesn't exist": callers must not cache it as a miss.
        if (transient is not null) throw new HttpRequestException($"{Name}: {transient.Message}", transient);
        return null;
    }
}

/// <summary>
/// Archive of the OPL Manager GameArt database (github.com/Luden02/psx-ps2-opl-art-database).
/// Has every OPL art type, named PS2/&lt;OPLID&gt;/&lt;OPLID&gt;_&lt;SUFFIX&gt;.png.
/// </summary>
public sealed class OplArtDatabaseSource : UrlArtSource
{
    private const string Base = "https://raw.githubusercontent.com/Luden02/psx-ps2-opl-art-database/main/PS2/";
    public override string Id => "opl-artdb";
    public override string Name => "OPL Art Database";
    public override string Description => "All OPL art types (covers, spine, icon, logo, backgrounds, screenshots).";
    public override bool Supports(ArtType type) => true;

    protected override IEnumerable<string> Urls(string gameId, ArtType type)
    {
        string id = GameIds.ToOplId(gameId);
        string folder = Base + Uri.EscapeDataString(id) + "/" + Uri.EscapeDataString(id);
        switch (type)
        {
            case ArtType.Background: yield return $"{folder}_BG_00.png"; yield return $"{folder}_BG.png"; break;
            case ArtType.Screenshot: yield return $"{folder}_SCR_00.png"; yield return $"{folder}_SCR.png"; break;
            case ArtType.Screenshot2: yield return $"{folder}_SCR_01.png"; yield return $"{folder}_SCR2.png"; break;
            default: yield return $"{folder}_{type.Suffix()}.png"; break;
        }
    }
}

/// <summary>xlenore/ps2-covers — front covers, flat 2D.</summary>
public sealed class XlenoreCoverSource : UrlArtSource
{
    public override string Id => "xlenore-2d";
    public override string Name => "xlenore covers (2D)";
    public override string Description => "Large collection of flat front covers.";
    public override bool Supports(ArtType type) => type == ArtType.Cover;
    protected override IEnumerable<string> Urls(string gameId, ArtType type)
    {
        yield return $"https://raw.githubusercontent.com/xlenore/ps2-covers/main/covers/default/{Uri.EscapeDataString(GameIds.Normalize(gameId))}.jpg";
    }
}

/// <summary>xlenore/ps2-covers — 3D box renders (transparent PNG).</summary>
public sealed class Xlenore3DCoverSource : UrlArtSource
{
    public override string Id => "xlenore-3d";
    public override string Name => "xlenore covers (3D)";
    public override string Description => "3D box renders with transparent background.";
    public override bool Supports(ArtType type) => type == ArtType.Cover;
    protected override IEnumerable<string> Urls(string gameId, ArtType type)
    {
        yield return $"https://raw.githubusercontent.com/xlenore/ps2-covers/main/covers/3d/{Uri.EscapeDataString(GameIds.Normalize(gameId))}.png";
    }
}

public static class ArtSourceCatalog
{
    public static IReadOnlyList<IArtSource> All { get; } =
        [new OplArtDatabaseSource(), new XlenoreCoverSource(), new Xlenore3DCoverSource()];

    public static IArtSource? Find(string id) => All.FirstOrDefault(s => s.Id == id);

    /// <summary>Default priority: OPL DB first (matches OPL's expectations), then xlenore 2D.</summary>
    public static readonly string[] DefaultOrder = ["opl-artdb", "xlenore-2d"];
}
