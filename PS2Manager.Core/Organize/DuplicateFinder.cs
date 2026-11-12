using PS2Manager.Core.Models;

namespace PS2Manager.Core.Organize;

public enum DuplicateMatch
{
    /// <summary>Same serial, same size, same version: almost certainly identical dumps.</summary>
    Exact,
    /// <summary>Same serial regardless of size/version: different dumps or revisions of one game.</summary>
    SameGameId,
}

public sealed record DuplicateGroup(string Key, string Title, IReadOnlyList<GameEntry> Games);

public static class DuplicateFinder
{
    public static List<DuplicateGroup> Find(IEnumerable<GameEntry> games, DuplicateMatch match)
    {
        return games
            .Where(g => g.HasGameId && !g.FileMissing)
            .GroupBy(g => match == DuplicateMatch.Exact
                ? $"{g.GameId}|{g.SizeBytes}|{g.Version}|{g.DiscNumber}"
                : $"{g.GameId}|{g.DiscNumber}")
            .Where(grp => grp.Count() > 1)
            .Select(grp =>
            {
                // Keep-candidate first: the one whose name already looks tidy (shortest path), then oldest scan.
                var ordered = grp.OrderBy(g => g.IsoPath.Length).ThenBy(g => g.IsoPath, StringComparer.OrdinalIgnoreCase).ToList();
                return new DuplicateGroup(grp.Key, ordered[0].Title, ordered);
            })
            .OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
