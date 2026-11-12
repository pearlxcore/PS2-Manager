using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS2Manager.App.Services;
using PS2Manager.Core.Models;
using PS2Manager.Core.Organize;

namespace PS2Manager.App.ViewModels;

public sealed partial class DuplicateRow(GameEntry game, string groupTitle, bool isKeeper) : ObservableObject
{
    public GameEntry Game { get; } = game;
    public string Group { get; } = groupTitle;
    public bool IsKeeper { get; } = isKeeper;
    [ObservableProperty] private bool _delete = !isKeeper;
}

public sealed partial class DuplicatesViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly DialogService _dialogs;
    private readonly NotifyService _notify;

    public DuplicatesViewModel(LibraryService library, DialogService dialogs, NotifyService notify)
    {
        _library = library;
        _dialogs = dialogs;
        _notify = notify;
        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(DuplicateRow.Group)));
        Detect();
    }

    public ObservableCollection<DuplicateRow> Rows { get; } = [];
    public ICollectionView RowsView { get; }
    public DuplicateMatch[] MatchOptions { get; } = Enum.GetValues<DuplicateMatch>();

    [ObservableProperty] private DuplicateMatch _match = DuplicateMatch.Exact;
    [ObservableProperty] private string _summary = "";

    public string MatchHint => Match == DuplicateMatch.Exact
        ? "Same game ID, size, version and disc: identical copies."
        : "Same game ID and disc: different dumps or revisions of one game.";

    partial void OnMatchChanged(DuplicateMatch value)
    {
        OnPropertyChanged(nameof(MatchHint));
        Detect();
    }

    private void Detect()
    {
        Rows.Clear();
        var groups = DuplicateFinder.Find(_library.Games, Match);
        foreach (var g in groups)
        {
            string header = $"{g.Title}  ·  {g.Games[0].GameId}  ·  {g.Games.Count} copies";
            for (int i = 0; i < g.Games.Count; i++)
                Rows.Add(new DuplicateRow(g.Games[i], header, i == 0));
        }
        Summary = groups.Count == 0 ? "No duplicates found." : $"{Rows.Count} files in {groups.Count} groups. The first file in each group is kept by default.";
    }

    [RelayCommand]
    private async Task DeleteMarkedAsync()
    {
        var marked = Rows.Where(r => r.Delete).ToList();
        if (marked.Count == 0) { _notify.Info("Nothing marked for deletion"); return; }

        var orphaned = marked.GroupBy(r => r.Group).Where(g => g.Count() == Rows.Count(r => r.Group == g.Key)).Select(g => g.Key).ToList();
        string warn = orphaned.Count > 0 ? $"\n\nWarning: every copy is marked in {orphaned.Count} group(s); those games would be gone completely." : "";
        if (!await _dialogs.ConfirmAsync("Delete duplicates", $"Move {marked.Count} file(s) to the Recycle Bin?{warn}", "Delete"))
            return;

        var removed = new List<GameEntry>();
        var failed = new List<string>();
        await Task.Run(() =>
        {
            foreach (var r in marked)
            {
                if (ShellService.RecycleFile(r.Game.IsoPath)) removed.Add(r.Game);
                else failed.Add(r.Game.IsoPath);
            }
        });

        _library.Remove(removed);
        Detect();
        if (failed.Count > 0) await _dialogs.AlertAsync("Some files could not be deleted", string.Join("\n", failed));
        else _notify.Success("Duplicates removed", $"{removed.Count} file(s) moved to the Recycle Bin.");
    }

    [RelayCommand]
    private static void ShowInExplorer(DuplicateRow row) => ShellService.ShowInExplorer(row.Game.IsoPath);
}
