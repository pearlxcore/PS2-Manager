using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS2Manager.App.Services;
using PS2Manager.App.Views.Dialogs;
using PS2Manager.Core;
using PS2Manager.Core.Art;
using PS2Manager.Core.Models;

namespace PS2Manager.App.ViewModels;

public sealed partial class ArtTypeOption(ArtType type, bool isChecked) : ObservableObject
{
    public ArtType Type { get; } = type;
    public string Name => Type.DisplayName();
    [ObservableProperty] private bool _isChecked = isChecked;
}

public sealed partial class ToolsViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly LibraryViewModel _libraryVm;
    private readonly MainWindowViewModel _main;
    private readonly DialogService _dialogs;
    private readonly NotifyService _notify;

    public ToolsViewModel(LibraryService library, LibraryViewModel libraryVm, MainWindowViewModel main, DialogService dialogs, NotifyService notify)
    {
        _library = library;
        _libraryVm = libraryVm;
        _main = main;
        _dialogs = dialogs;
        _notify = notify;

        ArtTypes = Core.Art.ArtTypes.All.Select(t => new ArtTypeOption(t, library.Settings.BulkArtTypes.Contains(t))).ToList();
        foreach (var o in ArtTypes) o.PropertyChanged += (_, _) => SaveArtTypes();

        libraryVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.SelectedGames)) Refresh();
        };
        library.LibraryChanged += (_, _) => Refresh();
        Refresh();
    }

    public IReadOnlyList<ArtTypeOption> ArtTypes { get; }

    [ObservableProperty] private bool _useSelection;
    [ObservableProperty] private string _selectedText = "";
    [ObservableProperty] private string _allText = "";
    [ObservableProperty] private bool _overwriteArt;
    [ObservableProperty] private string _undoText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand))]
    private bool _canUndo;

    partial void OnUseSelectionChanged(bool value) => Refresh();

    public void Refresh()
    {
        int sel = _libraryVm.SelectedGames.Count;
        SelectedText = $"Selected in Library ({sel})";
        AllText = $"Whole library ({_library.Games.Count})";
        if (sel == 0 && UseSelection) UseSelection = false;

        var last = _library.History.Last;
        CanUndo = last is not null;
        UndoText = last is null
            ? "Nothing to undo."
            : $"{last.Description}: {last.Moves.Count} file(s), {last.When:g}";
    }

    private IReadOnlyList<GameEntry> ScopeGames =>
        UseSelection && _libraryVm.SelectedGames.Count > 0 ? _libraryVm.SelectedGames : _library.Games.ToList();

    private void SaveArtTypes()
    {
        _library.Settings.BulkArtTypes = ArtTypes.Where(a => a.IsChecked).Select(a => a.Type).ToList();
        _library.Settings.Save();
    }

    [RelayCommand] private void Rename() => _libraryVm.OpenFileOperation(FileOperationKind.Rename, ScopeGames);
    [RelayCommand] private void MoveToFolders() => _libraryVm.OpenFileOperation(FileOperationKind.MoveToFolders, ScopeGames);
    [RelayCommand] private void Flatten() => _libraryVm.OpenFileOperation(FileOperationKind.Flatten, ScopeGames);
    [RelayCommand] private void FixExtensions() => _libraryVm.OpenFileOperation(FileOperationKind.FixExtensions, ScopeGames);

    [RelayCommand]
    private void FindDuplicates()
    {
        _dialogs.ShowDialog(new DuplicatesWindow(new DuplicatesViewModel(_library, _dialogs, _notify)));
        _libraryVm.RefreshView();
    }

    [RelayCommand]
    private Task DownloadArtAsync()
    {
        var types = ArtTypes.Where(a => a.IsChecked).Select(a => a.Type).ToList();
        return _libraryVm.DownloadArtAsync(ScopeGames, types, OverwriteArt, quiet: false);
    }

    [RelayCommand]
    private async Task ComputeCrcAsync()
    {
        var games = ScopeGames;
        if (games.Count > 20 && !await _dialogs.ConfirmAsync("Calculate CRC32",
                $"This reads every byte of {games.Count} files and can take a long time. Continue?", "Start"))
            return;
        await _libraryVm.ComputeCrcForAsync(games);
    }

    [RelayCommand] private void Export() => _libraryVm.ExportCommand.Execute(null);
    [RelayCommand] private void ImportLegacy() => _libraryVm.ImportLegacyManifestCommand.Execute(null);
    [RelayCommand] private Task UpdateDatabaseAsync() => _main.DownloadDatabaseAsync();

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAsync()
    {
        var last = _library.History.Last;
        if (last is null) return;
        if (!await _dialogs.ConfirmAsync("Undo", $"Undo \"{last.Description}\" ({last.Moves.Count} files)?", "Undo"))
            return;

        var undone = await Task.Run(() => (Moves: _library.History.UndoLast(out var errors), Errors: errors));
        _library.AfterFilesMoved(undone.Moves, undo: true);
        Refresh();
        if (undone.Errors.Count == 0) _notify.Success("Undone", $"{undone.Moves.Count} file(s) restored.");
        else await _dialogs.AlertAsync("Undo finished with problems", string.Join("\n", undone.Errors));
    }

    [RelayCommand] private static void OpenDataFolder() => ShellService.ShowInExplorer(AppPaths.Root);
    [RelayCommand] private static void OpenLog() => ShellService.Open(AppPaths.LogFile);
}
