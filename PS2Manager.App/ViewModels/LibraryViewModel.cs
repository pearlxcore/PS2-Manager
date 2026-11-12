using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PS2Manager.App.Services;
using PS2Manager.App.Views.Dialogs;
using PS2Manager.Core;
using PS2Manager.Core.Art;
using PS2Manager.Core.Iso;
using PS2Manager.Core.Library;
using PS2Manager.Core.Models;

namespace PS2Manager.App.ViewModels;

public enum StatusFilter
{
    [Description("All games")] All,
    [Description("Missing cover")] MissingCover,
    [Description("Edited by me")] Edited,
    [Description("Not in database")] NotInDatabase,
    [Description("Problems (unreadable / missing file)")] Problems,
    [Description("Multi-disc")] MultiDisc,
}

public enum LibrarySort
{
    [Description("Title (A–Z)")] Title,
    [Description("Recently added")] Recent,
    [Description("Release date")] Release,
    [Description("Size (largest)")] Size,
    [Description("Region")] Region,
    [Description("Game ID")] GameId,
}

/// <summary>One line in the library overview shown when nothing is selected.</summary>
public sealed record LibraryStat(string Label, string Value, StatusFilter? Filter = null);

public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly ActivityService _activity;
    private readonly DialogService _dialogs;
    private readonly NotifyService _notify;
    private readonly DispatcherTimer _searchDebounce;

    public LibraryViewModel(LibraryService library, ActivityService activity, DialogService dialogs, NotifyService notify, Pcsx2Service pcsx2)
    {
        Pcsx2 = pcsx2;
        pcsx2.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Pcsx2Service.IsAvailable)) PlayCommand.NotifyCanExecuteChanged(); };
        _library = library;
        _activity = activity;
        _dialogs = dialogs;
        _notify = notify;

        GamesView = CollectionViewSource.GetDefaultView(library.Games);
        GamesView.Filter = Filter;
        _sortBy = Enum.TryParse<LibrarySort>(library.Settings.LibrarySort, out var sort) ? sort : LibrarySort.Title;
        ApplySort();
        _showDetails = library.Settings.ShowDetailsPane;

        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); RefreshView(); };

        library.LibraryChanged += (_, _) => RefreshView();
        library.Games.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));

        _isCoverView = library.Settings.LibraryView == "Covers";
        _coverSize = library.Settings.CoverSize;
    }

    public LibraryService Library => _library;
    public Pcsx2Service Pcsx2 { get; }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play() { if (SelectedGame is { } g) Pcsx2.Play(g); }
    private bool CanPlay() => Pcsx2.CanPlay(SelectedGame);
    public ICollectionView GamesView { get; }

    public IReadOnlyList<string> RegionOptions { get; } = ["All regions", "NTSC-U", "PAL", "NTSC-J", "NTSC-K", "Other"];
    public IReadOnlyList<string> MediaOptions { get; } = ["CD + DVD", "CD", "DVD"];
    public StatusFilter[] StatusOptions { get; } = Enum.GetValues<StatusFilter>();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _regionFilter = "All regions";
    [ObservableProperty] private string _mediaFilter = "CD + DVD";
    [ObservableProperty] private StatusFilter _statusFilter = StatusFilter.All;
    [ObservableProperty] private bool _isCoverView;
    [ObservableProperty] private double _coverSize;
    [ObservableProperty] private string _countText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditInfoCommand), nameof(ManageArtCommand), nameof(ShowInExplorerCommand), nameof(PlayCommand))]
    private GameEntry? _selectedGame;

    private IReadOnlyList<GameEntry> _selectedGames = [];
    public IReadOnlyList<GameEntry> SelectedGames
    {
        get => _selectedGames;
        set
        {
            _selectedGames = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectionText));
            UpdateCountText();
            foreach (var c in new IRelayCommand[] { RenameCommand, MoveToFoldersCommand, DeleteFilesCommand, RemoveFromLibraryCommand,
                         DownloadArtForSelectionCommand, ComputeCrcCommand, SendToOplCommand })
                c.NotifyCanExecuteChanged();
        }
    }

    public bool HasSelection => _selectedGames.Count > 0;
    public string SelectionText => _selectedGames.Count switch { 0 => "", 1 => "1 selected", var n => $"{n} selected" };
    public bool IsEmpty => _library.Games.Count == 0;
    public IEnumerable<GameEntry> VisibleGames => GamesView.Cast<GameEntry>();

    partial void OnSearchTextChanged(string value) { _searchDebounce.Stop(); _searchDebounce.Start(); }
    partial void OnRegionFilterChanged(string value) => RefreshView();
    partial void OnMediaFilterChanged(string value) => RefreshView();
    partial void OnStatusFilterChanged(StatusFilter value) => RefreshView();
    partial void OnIsCoverViewChanged(bool value) { _library.Settings.LibraryView = value ? "Covers" : "Grid"; }
    partial void OnCoverSizeChanged(double value) { _library.Settings.CoverSize = value; }

    // ------------------------------------------------------------------ sorting & overview

    public LibrarySort[] SortOptions { get; } = Enum.GetValues<LibrarySort>();
    [ObservableProperty] private LibrarySort _sortBy;
    [ObservableProperty] private bool _showDetails;
    [ObservableProperty] private IReadOnlyList<LibraryStat> _stats = [];
    private int _shownCount;

    partial void OnSortByChanged(LibrarySort value) { _library.Settings.LibrarySort = value.ToString(); ApplySort(); }
    partial void OnShowDetailsChanged(bool value) => _library.Settings.ShowDetailsPane = value;

    [RelayCommand] private void ToggleDetails() => ShowDetails = !ShowDetails;

    [RelayCommand]
    private void ApplyStatFilter(LibraryStat? stat)
    {
        if (stat?.Filter is { } f) { ClearFilters(); StatusFilter = f; }
    }

    private void ApplySort()
    {
        using (GamesView.DeferRefresh())
        {
            GamesView.SortDescriptions.Clear();
            void By(string p, ListSortDirection d = ListSortDirection.Ascending) => GamesView.SortDescriptions.Add(new SortDescription(p, d));
            switch (SortBy)
            {
                case LibrarySort.Recent: By(nameof(GameEntry.ScannedAt), ListSortDirection.Descending); break;
                case LibrarySort.Release: By(nameof(GameEntry.Release), ListSortDirection.Descending); break;
                case LibrarySort.Size: By(nameof(GameEntry.SizeBytes), ListSortDirection.Descending); break;
                case LibrarySort.Region: By(nameof(GameEntry.Region)); break;
                case LibrarySort.GameId: By(nameof(GameEntry.GameId)); break;
            }
            By(nameof(GameEntry.Title));
        }
    }

    public void RefreshView()
    {
        GamesView.Refresh();
        _shownCount = GamesView.Cast<object>().Count();
        UpdateCountText();
        UpdateStats();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void UpdateCountText()
    {
        int total = _library.Games.Count;
        var text = _shownCount == total ? $"{total:N0} games" : $"{total:N0} games · {_shownCount:N0} shown";
        if (_selectedGames.Count > 0)
            text += $" · {_selectedGames.Count:N0} selected ({GameEntry.FormatSize(_selectedGames.Sum(g => g.SizeBytes))})";
        CountText = text;
    }

    private void UpdateStats()
    {
        var games = _library.Games;
        if (games.Count == 0) { Stats = []; return; }
        int Count(Func<GameEntry, bool> f) => games.Count(f);
        var list = new List<LibraryStat>
        {
            new("Games", games.Count.ToString("N0")),
            new("Total size", GameEntry.FormatSize(games.Sum(g => g.SizeBytes))),
            new("DVD / CD", $"{Count(g => g.DiscType == DiscType.DVD):N0} / {Count(g => g.DiscType == DiscType.CD):N0}"),
        };
        foreach (var r in games.GroupBy(g => string.IsNullOrEmpty(g.Region) ? "Unknown" : g.Region).OrderByDescending(x => x.Count()))
            list.Add(new($"  {r.Key}", r.Count().ToString("N0")));
        void Flag(string label, int n, StatusFilter f) { if (n > 0) list.Add(new(label, n.ToString("N0"), f)); }
        Flag("Missing cover", Count(g => g.CoverPath is null), StatusFilter.MissingCover);
        Flag("Not in database", Count(g => !g.InDatabase), StatusFilter.NotInDatabase);
        Flag("Edited by me", Count(g => g.HasOverrides), StatusFilter.Edited);
        Flag("Problems", Count(g => g.FileMissing || !g.IsValid), StatusFilter.Problems);
        Stats = list;
    }

    private bool Filter(object o)
    {
        if (o is not GameEntry g) return false;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string q = SearchText.Trim();
            if (!(g.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || g.GameId.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || g.OplId.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || g.FileName.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || g.Developer.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || g.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase)
                  || g.Genre.Contains(q, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        if (RegionFilter != "All regions")
        {
            string region = g.Region ?? "";
            bool known = region is "NTSC-U" or "PAL" or "NTSC-J" or "NTSC-K";
            if (RegionFilter == "Other" ? known : !region.Equals(RegionFilter, StringComparison.OrdinalIgnoreCase)) return false;
        }

        if (MediaFilter == "CD" && g.DiscType != DiscType.CD) return false;
        if (MediaFilter == "DVD" && g.DiscType != DiscType.DVD) return false;

        return StatusFilter switch
        {
            StatusFilter.MissingCover => g.CoverPath is null,
            StatusFilter.Edited => g.HasOverrides,
            StatusFilter.NotInDatabase => !g.InDatabase,
            StatusFilter.Problems => g.FileMissing || !g.IsValid,
            StatusFilter.MultiDisc => g.IsMultiDisc,
            _ => true,
        };
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = "";
        RegionFilter = "All regions";
        MediaFilter = "CD + DVD";
        StatusFilter = StatusFilter.All;
    }

    // ------------------------------------------------------------------ scanning

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folders = _dialogs.PickFolders("Add PS2 game folders");
        if (folders.Length == 0) return;
        await ScanFoldersAsync(folders, full: false, quiet: false);
    }

    [RelayCommand]
    private Task RescanAsync() => RescanAsync(full: false, quiet: false);

    [RelayCommand]
    private Task FullRescanAsync() => RescanAsync(full: true, quiet: false);

    public async Task RescanAsync(bool full, bool quiet)
    {
        var folders = _library.Folders.Where(Directory.Exists).ToList();
        if (folders.Count == 0)
        {
            if (!quiet) await AddFolderAsync();
            return;
        }
        await ScanFoldersAsync(folders, full, quiet);
    }

    public async Task ScanFoldersAsync(IReadOnlyList<string> folders, bool full, bool quiet)
    {
        using var scope = _activity.TryBegin("Scanning…");
        if (scope is null) { _notify.Warning("Busy", "Another task is running."); return; }
        try
        {
            var progress = new Progress<ScanProgress>(p =>
                scope.Report($"Scanning {p.Done:N0} / {p.Total:N0}: {p.CurrentFile}", p.Total == 0 ? null : (double)p.Done / p.Total));
            var result = await _library.ScanAsync(folders, full, progress, scope.Token);
            Log.Write($"Scan: {result.Games.Count} games, {result.NewCount} read, {result.ReusedCount} cached, {result.RemovedCount} removed in {result.Elapsed.TotalSeconds:0.0}s");
            if (!quiet || result.NewCount > 0 || result.RemovedCount > 0)
                _notify.Success("Scan complete",
                    $"{result.Games.Count:N0} games · {result.NewCount:N0} read · {result.RemovedCount:N0} removed · {result.Elapsed.TotalSeconds:0.0}s");
        }
        catch (OperationCanceledException) { _notify.Info("Scan cancelled"); }
        catch (Exception ex) { Log.Error("Scan", ex); _notify.Error("Scan failed", ex.Message); }
    }

    [RelayCommand]
    private void ImportLegacyManifest()
    {
        var path = _dialogs.PickFile("Import manifest from the old PS2 Manager", "PS2 Manager manifest (ps2_manifest.json)|*.json");
        if (path is null) return;
        try
        {
            var games = LibraryStore.ImportLegacyManifest(path);
            // The old version kept covers in <exe>\Covers next to <exe>\Manifests.
            string? exeDir = Path.GetDirectoryName(Path.GetDirectoryName(path));
            int covers = exeDir is null ? 0 : _library.Art.ImportLegacyCovers(Path.Combine(exeDir, "Covers"));
            _library.ImportEntries(games);
            _notify.Success("Manifest imported",
                $"{games.Count:N0} entries" + (covers > 0 ? $", {covers:N0} covers reused" : "") + ". Run a rescan to refresh disc details.");
        }
        catch (Exception ex)
        {
            _notify.Error("Import failed", ex.Message);
        }
    }

    // ------------------------------------------------------------------ per-game

    [RelayCommand(CanExecute = nameof(CanSingle))]
    private void EditInfo(GameEntry? game)
    {
        game ??= SelectedGame;
        if (game is null) return;
        var vm = new GameEditorViewModel(game, _library, _dialogs, _notify);
        _dialogs.ShowDialog(new GameEditorWindow(vm));
        RefreshView();
    }

    [RelayCommand(CanExecute = nameof(CanSingle))]
    private void ManageArt(GameEntry? game)
    {
        game ??= SelectedGame;
        if (game is null) return;
        if (!game.HasGameId) { _notify.Warning("No game ID", "Art is stored by game ID, and this disc doesn't have one."); return; }
        var vm = new ArtManagerViewModel(game, _library, _dialogs, _notify);
        _dialogs.ShowDialog(new ArtManagerWindow(vm));
        _library.RefreshCover(game);
        RefreshView();
    }

    private bool CanSingle(GameEntry? game) => (game ?? SelectedGame) is not null;

    [RelayCommand(CanExecute = nameof(CanSingle))]
    private void ShowInExplorer(GameEntry? game)
    {
        game ??= SelectedGame;
        if (game is not null) ShellService.ShowInExplorer(game.IsoPath);
    }

    // ------------------------------------------------------------------ selection actions

    private IReadOnlyList<GameEntry> Targets => _selectedGames.Count > 0 ? _selectedGames : SelectedGame is { } g ? [g] : [];

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Rename() => OpenFileOperation(FileOperationKind.Rename, Targets);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void MoveToFolders() => OpenFileOperation(FileOperationKind.MoveToFolders, Targets);

    public void OpenFileOperation(FileOperationKind kind, IReadOnlyList<GameEntry> games)
    {
        if (games.Count == 0) { _notify.Info("Nothing to do", "Select some games first."); return; }
        var vm = new FileOperationViewModel(kind, games, _library, _activity, _dialogs, _notify);
        _dialogs.ShowDialog(new FileOperationWindow(vm));
        RefreshView();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task DownloadArtForSelectionAsync() =>
        DownloadArtAsync(Targets, _library.Settings.BulkArtTypes, overwrite: false, quiet: false);

    public Task DownloadMissingCoversAsync(bool quiet) =>
        DownloadArtAsync(_library.Games.Where(g => g.HasGameId && g.CoverPath is null).ToList(), [ArtType.Cover], false, quiet);

    public async Task DownloadArtAsync(IReadOnlyList<GameEntry> games, IReadOnlyCollection<ArtType> types, bool overwrite, bool quiet)
    {
        var withIds = games.Where(g => g.HasGameId).ToList();
        if (withIds.Count == 0 || types.Count == 0)
        {
            if (!quiet) _notify.Info("Nothing to download", "No selected games have a game ID, or no art types are chosen.");
            return;
        }

        using var scope = _activity.TryBegin("Downloading art…");
        if (scope is null) { if (!quiet) _notify.Warning("Busy", "Another task is running."); return; }
        try
        {
            var progress = new Progress<ArtDownloadProgress>(p =>
                scope.Report($"Downloading art {p.Done:N0} / {p.Total:N0} · {p.Downloaded:N0} found", (double)p.Done / p.Total));
            var result = await _library.Downloader.DownloadManyAsync(
                withIds.Select(g => g.GameId), types, _library.Settings.ResolveArtSources(), overwrite, progress, scope.Token,
                skipRecentMisses: quiet);   // automatic runs skip art that wasn't found recently

            foreach (var g in withIds) _library.RefreshCover(g);
            RefreshView();

            if (!quiet || result.Downloaded > 0)
                _notify.Success("Art download finished",
                    $"{result.Downloaded:N0} downloaded · {result.NotFound:N0} not found · {result.Skipped:N0} already had" +
                    (result.Failed > 0 ? $" · {result.Failed} failed" : ""));
        }
        catch (OperationCanceledException) { _notify.Info("Download cancelled"); }
        catch (Exception ex) { Log.Error("Art download", ex); _notify.Error("Art download failed", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private Task ComputeCrcAsync() => ComputeCrcForAsync(Targets);

    public async Task ComputeCrcForAsync(IReadOnlyList<GameEntry> targets)
    {
        var games = targets.Where(g => File.Exists(g.IsoPath)).ToList();
        if (games.Count == 0) return;
        using var scope = _activity.TryBegin("Calculating CRC32…");
        if (scope is null) { _notify.Warning("Busy", "Another task is running."); return; }
        try
        {
            for (int i = 0; i < games.Count; i++)
            {
                var g = games[i];
                int idx = i;
                var p = new Progress<double>(f => scope.Report($"CRC32 {idx + 1}/{games.Count}: {g.Title} {f:P0}", (idx + f) / games.Count));
                g.Crc32 = await Task.Run(() => Checksums.FileCrc32Async(g.IsoPath, p, scope.Token));
            }
            _library.Save();
            _notify.Success("CRC32 calculated", games.Count == 1 ? $"{games[0].Title}: {games[0].Crc32:X8}" : $"{games.Count} files");
        }
        catch (OperationCanceledException) { _notify.Info("CRC cancelled"); }
        catch (Exception ex) { _notify.Error("CRC failed", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteFilesAsync()
    {
        var games = Targets.ToList();
        string what = games.Count == 1 ? $"\"{games[0].FileName}\"" : $"{games.Count} files";
        if (!await _dialogs.ConfirmAsync("Delete files", $"Move {what} to the Recycle Bin and remove from the library?", "Delete"))
            return;

        var failed = new List<string>();
        var removed = new List<GameEntry>();
        await Task.Run(() =>
        {
            foreach (var g in games)
            {
                if (ShellService.RecycleFile(g.IsoPath)) removed.Add(g);
                else failed.Add(g.FileName);
            }
        });
        _library.Remove(removed);
        if (failed.Count == 0) _notify.Success("Deleted", $"{removed.Count} file(s) moved to the Recycle Bin.");
        else await _dialogs.AlertAsync("Some files could not be deleted", string.Join("\n", failed));
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RemoveFromLibraryAsync()
    {
        var games = Targets.ToList();
        if (!await _dialogs.ConfirmAsync("Remove from library",
                $"Remove {games.Count} game(s) from the library? Files on disk are not touched; they come back on the next scan of their folder.",
                "Remove"))
            return;
        _library.Remove(games);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SendToOplAsync()
    {
        var opl = App.Services.GetRequiredService<OplViewModel>();
        await opl.TransferAsync(Targets.ToList());
    }

    [RelayCommand]
    private void Export()
    {
        var vm = new ExportViewModel(VisibleGames.ToList(), _library.Games.ToList(), Targets.ToList(), _dialogs, _notify);
        _dialogs.ShowDialog(new ExportWindow(vm));
    }

    [RelayCommand]
    private void ToggleView() => IsCoverView = !IsCoverView;
}
