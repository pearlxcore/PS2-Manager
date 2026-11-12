using CommunityToolkit.Mvvm.ComponentModel;
using PS2Manager.Core;
using PS2Manager.Core.Art;
using PS2Manager.Core.Database;
using PS2Manager.Core.Library;
using PS2Manager.Core.Models;
using PS2Manager.Core.Opl;
using PS2Manager.Core.Rename;
using PS2Manager.Core.Settings;

namespace PS2Manager.App.Services;

/// <summary>
/// Shared application state: the game list, the database, overrides, art cache and settings.
/// All mutations of <see cref="Games"/> happen on the UI thread.
/// </summary>
public sealed partial class LibraryService : ObservableObject
{
    private GameDatabase _database = GameDatabase.Empty;

    public LibraryService()
    {
        Settings = AppSettings.Load();
        Overrides = new OverrideStore(AppPaths.OverridesFile);
        Resolver = new MetadataResolver(() => _database, Overrides);
        Art = new ArtStore(AppPaths.ArtDir);
        Downloader = new ArtDownloader(Art);
        History = new RenameHistory(AppPaths.RenameHistoryFile);
        Opl = new OplService(Art);
    }

    public AppSettings Settings { get; }
    public OverrideStore Overrides { get; }
    public MetadataResolver Resolver { get; }
    public ArtStore Art { get; }
    public ArtDownloader Downloader { get; }
    public RenameHistory History { get; }
    public OplService Opl { get; }

    public BulkObservableCollection<GameEntry> Games { get; } = [];
    public List<string> Folders { get; private set; } = [];

    public GameDatabase Database => _database;

    [ObservableProperty] private string _databaseStatus = "Database not loaded";
    [ObservableProperty] private bool _isLoaded;

    /// <summary>Raised after bulk changes (scan, removal) so views can refresh filters.</summary>
    public event EventHandler? LibraryChanged;

    public OplRoot? OplRoot =>
        string.IsNullOrWhiteSpace(Settings.OplRootPath) ? null : new OplRoot(Settings.OplRootPath);

    // ------------------------------------------------------------------ load / save

    public async Task LoadAsync()
    {
        var (db, file) = await Task.Run(() => (GameDatabase.Load(AppPaths.DatabaseFile), LibraryStore.Load()));
        SetDatabase(db);

        var games = file?.Games ?? [];
        Folders = file?.Folders ?? [];
        await Task.Run(() =>
        {
            foreach (var g in games)
            {
                g.FileMissing = !File.Exists(g.IsoPath);
                Resolver.Resolve(g);
                g.CoverPath = Art.Get(g.GameId, ArtType.Cover);
            }
        });
        Games.ReplaceAll(games);
        IsLoaded = true;
        LibraryChanged?.Invoke(this, EventArgs.Empty);
        Log.Write($"Loaded library: {games.Count} games, {Folders.Count} folders, database {db.Count} entries");
    }

    public void Save()
    {
        try { LibraryStore.Save(Folders, Games); }
        catch (Exception ex) { Log.Error("Saving library", ex); }
    }

    public void SetDatabase(GameDatabase db)
    {
        _database = db;
        DatabaseStatus = db.Count == 0
            ? "No game database"
            : $"GameDB: {db.Count:N0} titles" + (db.FileDate is { } d ? $" ({d:yyyy-MM-dd})" : "");
    }

    public async Task<bool> UpdateDatabaseAsync(IProgress<double>? progress, CancellationToken ct)
    {
        if (!await DatabaseDownloader.DownloadAsync(AppPaths.DatabaseFile, progress, ct)) return false;
        var db = await Task.Run(() => GameDatabase.Load(AppPaths.DatabaseFile), ct);
        SetDatabase(db);
        RefreshAll();
        return true;
    }

    // ------------------------------------------------------------------ scanning

    public async Task<ScanResult> ScanAsync(IReadOnlyList<string> folders, bool fullRescan, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        foreach (var f in folders)
            if (!Folders.Contains(f, StringComparer.OrdinalIgnoreCase)) Folders.Add(f);

        var result = await LibraryScanner.ScanAsync(folders, Games.ToList(), fullRescan, progress, ct, Settings.ScanParallelism);
        await Task.Run(() =>
        {
            foreach (var g in result.Games)
            {
                if (!LibraryScanner.IsUnder(g.IsoPath, folders)) continue;   // untouched entries keep their state
                Resolver.Resolve(g);
                g.CoverPath = Art.Get(g.GameId, ArtType.Cover);
            }
        }, ct);

        Games.ReplaceAll(result.Games);
        Save();
        LibraryChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public void RemoveFolder(string folder, bool removeGames)
    {
        Folders.RemoveAll(f => f.Equals(folder, StringComparison.OrdinalIgnoreCase));
        if (removeGames) Games.RemoveMany(Games.Where(g => LibraryScanner.IsUnder(g.IsoPath, [folder])).ToList());
        Save();
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ImportEntries(IEnumerable<GameEntry> entries)
    {
        var existing = Games.Select(g => g.IsoPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = entries.Where(e => existing.Add(e.IsoPath)).ToList();
        foreach (var g in added)
        {
            g.FileMissing = !File.Exists(g.IsoPath);
            Resolver.Resolve(g);
            g.CoverPath = Art.Get(g.GameId, ArtType.Cover);
            string? dir = Path.GetDirectoryName(g.IsoPath);
            if (dir is not null && !Folders.Any(f => LibraryScanner.IsUnder(g.IsoPath, [f]) || f.Equals(dir, StringComparison.OrdinalIgnoreCase)))
                Folders.Add(dir);
        }
        Games.ReplaceAll(Games.Concat(added).OrderBy(g => g.IsoPath, StringComparer.OrdinalIgnoreCase).ToList());
        Save();
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(IEnumerable<GameEntry> games)
    {
        Games.RemoveMany(games.ToList());
        Save();
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    // ------------------------------------------------------------------ per-game refresh

    public void Refresh(GameEntry g)
    {
        Resolver.Resolve(g);
        RefreshCover(g);
    }

    public void RefreshCover(GameEntry g)
    {
        g.CoverPath = null;   // force rebinding even if the path string is the same
        g.CoverPath = Art.Get(g.GameId, ArtType.Cover);
    }

    public void RefreshAll()
    {
        foreach (var g in Games)
        {
            g.FileMissing = !File.Exists(g.IsoPath);
            Resolver.Resolve(g);
            g.CoverPath = Art.Get(g.GameId, ArtType.Cover);
        }
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Call after file operations: moves path-keyed overrides, persists, and refreshes views.</summary>
    public void AfterFilesMoved(IEnumerable<RenameMove> moves, bool undo = false)
    {
        var byPath = Games.ToDictionary(g => g.IsoPath, StringComparer.OrdinalIgnoreCase);
        foreach (var m in moves)
        {
            string from = undo ? m.To : m.From, to = undo ? m.From : m.To;
            if (undo && byPath.TryGetValue(from, out var g)) g.IsoPath = to;
            Overrides.Rekey("path:" + from.ToLowerInvariant(), "path:" + to.ToLowerInvariant());
        }
        foreach (var g in Games) g.FileMissing = !File.Exists(g.IsoPath);
        Save();
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyChanged() => LibraryChanged?.Invoke(this, EventArgs.Empty);
}
