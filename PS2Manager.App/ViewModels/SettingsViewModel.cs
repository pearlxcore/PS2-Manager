using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS2Manager.App.Services;
using PS2Manager.Core;
using PS2Manager.Core.Art;
using PS2Manager.Core.Pcsx2;
using PS2Manager.Core.Rename;

namespace PS2Manager.App.ViewModels;

public sealed partial class ArtSourceOption(IArtSource source, bool enabled) : ObservableObject
{
    public IArtSource Source { get; } = source;
    public string Name => Source.Name;
    public string Description => Source.Description;
    [ObservableProperty] private bool _isEnabled = enabled;
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly LibraryViewModel _libraryVm;
    private readonly MainWindowViewModel _main;
    private readonly DialogService _dialogs;
    private readonly NotifyService _notify;

    public SettingsViewModel(LibraryService library, LibraryViewModel libraryVm, MainWindowViewModel main, DialogService dialogs, NotifyService notify, Pcsx2Service pcsx2)
    {
        Pcsx2 = pcsx2;
        _library = library;
        _libraryVm = libraryVm;
        _main = main;
        _dialogs = dialogs;
        _notify = notify;

        var s = library.Settings;
        _autoScanOnStartup = s.AutoScanOnStartup;
        _autoDownloadCovers = s.AutoDownloadCovers;
        _scanParallelism = s.ScanParallelism;
        _renameTemplate = s.RenameTemplate;
        _folderTemplate = s.FolderTemplate;
        _conflictMode = s.ConflictMode;
        _pcsx2Window = s.Pcsx2Window;
        _pcsx2Boot = s.Pcsx2Boot;
        _pcsx2NoGui = s.Pcsx2NoGui;
        _pcsx2BigPicture = s.Pcsx2BigPicture;

        // Enabled sources in priority order, then the disabled ones.
        var ordered = s.ArtSourceOrder.Select(ArtSourceCatalog.Find).OfType<IArtSource>().ToList();
        foreach (var src in ordered) ArtSources.Add(new ArtSourceOption(src, true));
        foreach (var src in ArtSourceCatalog.All.Except(ordered)) ArtSources.Add(new ArtSourceOption(src, false));
        foreach (var o in ArtSources) o.PropertyChanged += (_, _) => SaveSources();

        library.LibraryChanged += (_, _) => RefreshFolders();
        RefreshFolders();
    }

    public ObservableCollection<string> Folders { get; } = [];
    public ObservableCollection<ArtSourceOption> ArtSources { get; } = [];
    public IReadOnlyList<string> RenamePresets { get; } = RenameFormats.FilePresets.Select(p => p.Template).ToList();
    public IReadOnlyList<string> FolderPresets { get; } = RenameFormats.FolderPresets.Select(p => p.Template).ToList();
    public ConflictMode[] ConflictModes { get; } = Enum.GetValues<ConflictMode>();
    public int[] ParallelismOptions { get; } = [1, 2, 3, 4, 6, 8];

    public string DataFolder => AppPaths.Root;
    public string DatabaseStatus => _library.DatabaseStatus;
    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0";

    [ObservableProperty] private bool _autoScanOnStartup;
    [ObservableProperty] private bool _autoDownloadCovers;
    [ObservableProperty] private int _scanParallelism;
    [ObservableProperty] private string _renameTemplate;
    [ObservableProperty] private string _folderTemplate;
    [ObservableProperty] private ConflictMode _conflictMode;
    [ObservableProperty] private string? _selectedFolder;
    [ObservableProperty] private ArtSourceOption? _selectedSource;

    partial void OnAutoScanOnStartupChanged(bool value) => Save(s => s.AutoScanOnStartup = value);
    partial void OnAutoDownloadCoversChanged(bool value) => Save(s => s.AutoDownloadCovers = value);
    partial void OnScanParallelismChanged(int value) => Save(s => s.ScanParallelism = value);
    partial void OnRenameTemplateChanged(string value) => Save(s => s.RenameTemplate = value);
    partial void OnFolderTemplateChanged(string value) => Save(s => s.FolderTemplate = value);
    partial void OnConflictModeChanged(ConflictMode value) => Save(s => s.ConflictMode = value);

    // ------------------------------------------------------------------ PCSX2

    public Pcsx2Service Pcsx2 { get; }
    public Pcsx2Window[] Pcsx2WindowOptions { get; } = Enum.GetValues<Pcsx2Window>();
    public Pcsx2Boot[] Pcsx2BootOptions { get; } = Enum.GetValues<Pcsx2Boot>();

    [ObservableProperty] private Pcsx2Window _pcsx2Window;
    [ObservableProperty] private Pcsx2Boot _pcsx2Boot;
    [ObservableProperty] private bool _pcsx2NoGui;
    [ObservableProperty] private bool _pcsx2BigPicture;
    [ObservableProperty] private string _pcsx2FolderStatus = "";

    partial void OnPcsx2WindowChanged(Pcsx2Window value) => Save(s => s.Pcsx2Window = value);
    partial void OnPcsx2BootChanged(Pcsx2Boot value) => Save(s => s.Pcsx2Boot = value);
    partial void OnPcsx2NoGuiChanged(bool value) => Save(s => s.Pcsx2NoGui = value);
    partial void OnPcsx2BigPictureChanged(bool value) => Save(s => s.Pcsx2BigPicture = value);

    /// <summary>Called when the Settings page is shown, so the folder status reflects PCSX2's current ini.</summary>
    public void RefreshPcsx2()
    {
        if (!Pcsx2.IsAvailable) { Pcsx2FolderStatus = ""; return; }
        var missing = Pcsx2.MissingGameListFolders();
        Pcsx2FolderStatus = missing.Count == 0
            ? "PCSX2 already scans all your library folders."
            : $"Not in PCSX2's game list yet: {string.Join(", ", missing)}";
    }

    [RelayCommand]
    private void BrowsePcsx2()
    {
        var exe = _dialogs.PickFile("Choose pcsx2-qt.exe", "PCSX2|pcsx2-qt.exe;pcsx2*.exe|Programs|*.exe");
        if (exe is null) return;
        if (Pcsx2.SetExe(exe)) _notify.Success("PCSX2 found", Pcsx2.Status);
        RefreshPcsx2();
    }

    [RelayCommand] private void DetectPcsx2() { Pcsx2.Detect(); RefreshPcsx2(); }
    [RelayCommand] private void OpenPcsx2Data() { if (Pcsx2.Install is { } i) ShellService.ShowInExplorer(i.DataDir); }
    [RelayCommand] private Task ExportPcsx2Covers() => Pcsx2.ExportCoversAsync(overwrite: false);

    [RelayCommand]
    private async Task AddFoldersToPcsx2()
    {
        var missing = Pcsx2.MissingGameListFolders();
        if (missing.Count == 0) { _notify.Info("Nothing to add", "PCSX2 already scans all your library folders."); return; }
        if (!await _dialogs.ConfirmAsync("Add folders to PCSX2",
                "Add these folders to PCSX2's game list?\n\n" + string.Join("\n", missing) +
                "\n\nPCSX2 must be closed. A backup of PCSX2.ini is saved next to it.", "Add"))
            return;
        Pcsx2.AddFoldersToGameList();
        RefreshPcsx2();
    }

    private void Save(Action<Core.Settings.AppSettings> change)
    {
        change(_library.Settings);
        _library.Settings.Save();
    }

    private void RefreshFolders()
    {
        Folders.Clear();
        foreach (var f in _library.Folders) Folders.Add(f);
        OnPropertyChanged(nameof(DatabaseStatus));
    }

    private void SaveSources() =>
        Save(s => s.ArtSourceOrder = ArtSources.Where(a => a.IsEnabled).Select(a => a.Source.Id).ToList());

    [RelayCommand]
    private void MoveSource(string direction)
    {
        if (SelectedSource is not { } sel) return;
        int i = ArtSources.IndexOf(sel);
        int j = direction == "up" ? i - 1 : i + 1;
        if (j < 0 || j >= ArtSources.Count) return;
        ArtSources.Move(i, j);
        SaveSources();
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folders = _dialogs.PickFolders("Add PS2 game folders");
        if (folders.Length == 0) return;
        await _libraryVm.ScanFoldersAsync(folders, full: false, quiet: false);
        RefreshFolders();
    }

    [RelayCommand]
    private async Task RemoveFolderAsync()
    {
        if (SelectedFolder is not { } folder) return;
        int choice = await _dialogs.ChooseAsync("Remove folder",
            $"Stop scanning \"{folder}\"?\n\nYou can also remove its games from the library. Files on disk are never touched.",
            "Remove folder and its games", "Remove folder only");
        if (choice < 0) return;
        _library.RemoveFolder(folder, removeGames: choice == 0);
        RefreshFolders();
    }

    [RelayCommand]
    private async Task UpdateDatabaseAsync()
    {
        await _main.DownloadDatabaseAsync();
        OnPropertyChanged(nameof(DatabaseStatus));
    }

    [RelayCommand] private void OpenDataFolder() => ShellService.ShowInExplorer(AppPaths.Root);
    [RelayCommand] private static void OpenUrl(string url) => ShellService.Open(url);
}
