using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS2Manager.App.Services;
using PS2Manager.Core;
using PS2Manager.Core.Art;
using PS2Manager.Core.Iso;
using PS2Manager.Core.Models;
using PS2Manager.Core.Opl;
using PS2Manager.Core.Rename;

namespace PS2Manager.App.ViewModels;

public sealed class DeviceGame
{
    public required GameEntry Game { get; init; }
    public required string Folder { get; init; }
    public bool HasCfg { get; init; }
    public int ArtCount { get; init; }
    public string Issue { get; init; } = "";
    public bool HasIssue => Issue.Length > 0;
    public string ArtText => ArtCount == 0 ? "No art" : $"{ArtCount} art";
}

public sealed partial class OplViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly LibraryViewModel _libraryVm;
    private readonly ActivityService _activity;
    private readonly DialogService _dialogs;
    private readonly NotifyService _notify;

    public OplViewModel(LibraryService library, LibraryViewModel libraryVm, ActivityService activity, DialogService dialogs, NotifyService notify)
    {
        _library = library;
        _libraryVm = libraryVm;
        _activity = activity;
        _dialogs = dialogs;
        _notify = notify;

        var s = library.Settings;
        _rootPath = s.OplRootPath ?? "";
        _transferMode = s.OplTransferMode;
        _renameTemplate = s.OplRenameTemplate ?? "";
        _writeCfg = s.OplWriteCfg;
        _exportArt = s.OplExportArt;
        _autoWriteCfgOnSave = s.AutoWriteCfgOnSave;

        libraryVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.SelectedGames)) OnPropertyChanged(nameof(SelectionText));
        };
        RefreshRootInfo();
    }

    public ObservableCollection<DeviceGame> DeviceGames { get; } = [];
    public IReadOnlyList<string> TemplateOptions { get; } = ["", .. RenameFormats.FilePresets.Select(p => p.Template)];
    public TransferMode[] TransferModes { get; } = Enum.GetValues<TransferMode>();

    [ObservableProperty] private string _rootPath;
    [ObservableProperty] private string _driveText = "";
    [ObservableProperty] private string _layoutText = "";
    [ObservableProperty] private string _warningText = "";
    [ObservableProperty] private bool _isRootValid;
    [ObservableProperty] private bool _hasMissingFolders;
    [ObservableProperty] private string _deviceSummary = "Not scanned yet.";

    [ObservableProperty] private TransferMode _transferMode;
    [ObservableProperty] private string _renameTemplate;
    [ObservableProperty] private bool _writeCfg;
    [ObservableProperty] private bool _exportArt;
    [ObservableProperty] private bool _overwrite;
    [ObservableProperty] private bool _autoWriteCfgOnSave;
    [ObservableProperty] private bool _downloadMissingArtFirst = true;

    public string SelectionText => $"{_libraryVm.SelectedGames.Count} game(s) selected in Library";

    partial void OnTransferModeChanged(TransferMode value) { _library.Settings.OplTransferMode = value; _library.Settings.Save(); }
    partial void OnRenameTemplateChanged(string value) { _library.Settings.OplRenameTemplate = value; _library.Settings.Save(); }
    partial void OnWriteCfgChanged(bool value) { _library.Settings.OplWriteCfg = value; _library.Settings.Save(); }
    partial void OnExportArtChanged(bool value) { _library.Settings.OplExportArt = value; _library.Settings.Save(); }
    partial void OnAutoWriteCfgOnSaveChanged(bool value) { _library.Settings.AutoWriteCfgOnSave = value; _library.Settings.Save(); }

    private OplRoot? Root => IsRootValid ? new OplRoot(RootPath) : null;

    private void RefreshRootInfo()
    {
        WarningText = "";
        DriveText = "";
        LayoutText = "";
        HasMissingFolders = false;

        if (string.IsNullOrWhiteSpace(RootPath) || !Directory.Exists(RootPath))
        {
            IsRootValid = false;
            LayoutText = string.IsNullOrWhiteSpace(RootPath) ? "Choose your OPL USB drive, network share folder or HDD folder." : "Folder not found. Is the device connected?";
            return;
        }

        IsRootValid = true;
        var root = new OplRoot(RootPath);
        if (root.Drive is { } d)
        {
            DriveText = $"{(string.IsNullOrEmpty(d.Label) ? d.Name : $"{d.Label} ({d.Name.TrimEnd('\\')})")} · {d.Format} · " +
                        $"{FormatBytes(d.FreeBytes)} free of {FormatBytes(d.TotalBytes)}";
            if (d.IsFat32) WarningText = "FAT32: games over 4 GB can't be copied. Format as exFAT (OPL 1.1+) or keep big games on another drive.";
        }
        else
        {
            DriveText = RootPath.StartsWith(@"\\") ? "Network share" : RootPath;
        }

        var missing = root.MissingFolders.ToList();
        HasMissingFolders = missing.Count > 0;
        LayoutText = !root.LooksLikeOplRoot
            ? "No CD/DVD folders here yet. Create the OPL folder layout to use this location."
            : missing.Count == 0 ? "OPL folder layout is complete." : $"Missing folders: {string.Join(", ", missing)}";
    }

    private static string FormatBytes(long b) => b >= 1L << 40 ? $"{b / (double)(1L << 40):0.0} TB" : $"{b / (double)(1L << 30):0.0} GB";

    [RelayCommand]
    private async Task BrowseRootAsync()
    {
        var folder = _dialogs.PickFolder("Choose the OPL device root (the folder that contains CD and DVD)", RootPath);
        if (folder is null) return;
        RootPath = folder;
        _library.Settings.OplRootPath = folder;
        _library.Settings.Save();
        RefreshRootInfo();
        if (IsRootValid && new OplRoot(folder).LooksLikeOplRoot) await RefreshDeviceAsync();
        else DeviceGames.Clear();
    }

    [RelayCommand]
    private void OpenRoot() { if (IsRootValid) ShellService.ShowInExplorer(RootPath); }

    [RelayCommand]
    private void CreateLayout()
    {
        if (Root is not { } root) return;
        try
        {
            root.CreateLayout();
            RefreshRootInfo();
            _notify.Success("OPL folders created", root.Path);
        }
        catch (Exception ex) { _notify.Error("Couldn't create folders", ex.Message); }
    }

    [RelayCommand]
    private async Task RefreshDeviceAsync()
    {
        RefreshRootInfo();
        if (Root is not { } root) return;

        using var scope = _activity.TryBegin("Reading OPL device…");
        if (scope is null) { _notify.Warning("Busy", "Another task is running."); return; }
        try
        {
            var known = _library.Games.ToDictionary(g => g.IsoPath, StringComparer.OrdinalIgnoreCase);
            var rows = await Task.Run(() =>
            {
                var list = new List<DeviceGame>();
                foreach (var folderName in new[] { "CD", "DVD" })
                {
                    string dir = Path.Combine(root.Path, folderName);
                    if (!Directory.Exists(dir)) continue;
                    var files = Directory.EnumerateFiles(dir).Where(DiscReader.IsSupported).ToList();
                    foreach (var file in files)
                    {
                        scope.Token.ThrowIfCancellationRequested();
                        scope.Report($"Reading {folderName}\\{Path.GetFileName(file)}");
                        var g = known.TryGetValue(file, out var k) ? k : DiscReader.Read(file);
                        _library.Resolver.Resolve(g);

                        string issue = "";
                        if (!file.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) issue = "OPL only loads .iso files";
                        else if (!g.HasGameId) issue = "No game ID found on disc";
                        else if (g.DiscType != DiscType.Unknown && !g.DiscType.ToString().Equals(folderName, StringComparison.OrdinalIgnoreCase))
                            issue = $"{g.DiscType} game in the {folderName} folder";

                        int art = g.HasGameId && Directory.Exists(root.ArtDir)
                            ? Directory.EnumerateFiles(root.ArtDir, g.OplId + "_*").Count() : 0;

                        list.Add(new DeviceGame
                        {
                            Game = g,
                            Folder = folderName,
                            HasCfg = g.HasGameId && File.Exists(root.CfgPath(g.GameId)),
                            ArtCount = art,
                            Issue = issue,
                        });
                    }
                }
                return list.OrderBy(r => r.Game.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            }, scope.Token);

            DeviceGames.Clear();
            foreach (var r in rows) DeviceGames.Add(r);
            long total = rows.Sum(r => r.Game.SizeBytes);
            DeviceSummary = $"{rows.Count} games · {FormatBytes(total)} · {rows.Count(r => r.HasCfg)} with CFG · " +
                            $"{rows.Count(r => r.ArtCount > 0)} with art" +
                            (rows.Any(r => r.HasIssue) ? $" · {rows.Count(r => r.HasIssue)} need attention" : "");
        }
        catch (OperationCanceledException) { _notify.Info("Cancelled"); }
        catch (Exception ex) { Log.Error("OPL refresh", ex); _notify.Error("Couldn't read the device", ex.Message); }
    }

    // ------------------------------------------------------------------ transfer

    [RelayCommand]
    private Task TransferSelectedAsync() => TransferAsync(_libraryVm.SelectedGames.ToList());

    public async Task TransferAsync(IReadOnlyList<GameEntry> games)
    {
        RefreshRootInfo();
        if (Root is not { } root)
        {
            _notify.Warning("No OPL device", "Choose the device root on the OPL Device page first.");
            return;
        }
        if (games.Count == 0) { _notify.Info("Nothing selected", "Select games in the Library first."); return; }

        var problems = games.Select(g => (g, p: OplService.Validate(root, g))).Where(x => x.p is not null).ToList();
        var ok = games.Except(problems.Select(p => p.g)).ToList();
        long bytes = ok.Sum(g => g.SizeBytes);

        string msg = $"{TransferMode} {ok.Count} game(s) ({FormatBytes(bytes)}) to {root.Path}";
        if (root.Drive is { } d && TransferMode == TransferMode.Copy && bytes > d.FreeBytes)
            msg += $"\n\nNot enough free space: {FormatBytes(d.FreeBytes)} available.";
        if (problems.Count > 0)
            msg += $"\n\n{problems.Count} game(s) will be skipped:\n" + string.Join("\n", problems.Take(8).Select(p => $"• {p.g.Title}: {p.p}"));
        if (!string.IsNullOrWhiteSpace(RenameTemplate)) msg += $"\n\nNames on device: {RenameTemplate}";

        if (ok.Count == 0) { await _dialogs.AlertAsync("Nothing can be transferred", msg); return; }
        if (!await _dialogs.ConfirmAsync("Send to OPL device", msg, TransferMode == TransferMode.Move ? "Move" : "Copy")) return;

        if (ExportArt && DownloadMissingArtFirst)
            await _libraryVm.DownloadArtAsync(ok, _library.Settings.BulkArtTypes, overwrite: false, quiet: true);

        if (await RunTransferAsync(root, ok)) await RefreshDeviceAsync();
    }

    private async Task<bool> RunTransferAsync(OplRoot root, List<GameEntry> ok)
    {
        using var scope = _activity.TryBegin("Sending to OPL…");
        if (scope is null) { _notify.Warning("Busy", "Another task is running."); return false; }
        try
        {
            var options = new TransferOptions
            {
                Mode = TransferMode,
                RenameTemplate = string.IsNullOrWhiteSpace(RenameTemplate) ? null : RenameTemplate,
                WriteCfg = WriteCfg,
                ExportArt = ExportArt,
                OverwriteExisting = Overwrite,
            };
            var progress = new Progress<TransferProgress>(p =>
                scope.Report($"{p.GameIndex}/{p.GameCount} {p.Game}: {FormatBytes(p.FileBytesDone)} / {FormatBytes(p.FileBytesTotal)}",
                    p.AllBytesTotal == 0 ? null : (double)p.AllBytesDone / p.AllBytesTotal));

            var results = await Task.Run(() => _library.Opl.TransferAsync(root, ok, options, progress, scope.Token));

            if (TransferMode == TransferMode.Move)
            {
                var moves = results.Where(r => r.Success && r.TargetPath is not null).Select(r => (r.Game, r.TargetPath!)).ToList();
                // TransferAsync updated IsoPath on a worker thread; re-raise on the UI thread.
                foreach (var (g, _) in moves) g.NotifyAllChanged();
                _library.Save();
                _library.NotifyChanged();
            }

            var failed = results.Where(r => !r.Success).ToList();
            int copied = results.Count(r => r.Success && !r.Skipped);
            int already = results.Count(r => r.Success && r.Skipped);
            if (failed.Count == 0)
                _notify.Success("Transfer complete", $"{copied} sent · {already} already on device");
            else
                await _dialogs.AlertAsync("Transfer finished with problems",
                    $"{copied} sent, {already} already on device, {failed.Count} failed:\n\n" +
                    string.Join("\n", failed.Select(f => $"• {f.Game.Title}: {f.Message}")));
        }
        catch (OperationCanceledException) { _notify.Info("Transfer cancelled", "The partial file was removed."); }
        catch (Exception ex) { Log.Error("OPL transfer", ex); _notify.Error("Transfer failed", ex.Message); }
        return true;
    }

    // ------------------------------------------------------------------ device maintenance

    [RelayCommand]
    private async Task WriteAllCfgAsync()
    {
        if (Root is not { } root || DeviceGames.Count == 0) { _notify.Info("Refresh the device list first"); return; }
        int n = 0, failed = 0;
        await Task.Run(() =>
        {
            foreach (var d in DeviceGames.Where(d => d.Game.HasGameId))
            {
                try { _library.Opl.WriteCfg(root, d.Game); n++; }
                catch (Exception ex) { failed++; Log.Error("WriteCfg", ex); }
            }
        });
        _notify.Success("CFG files written", $"{n} written" + (failed > 0 ? $", {failed} failed" : ""));
        await RefreshDeviceAsync();
    }

    [RelayCommand]
    private async Task ExportAllArtAsync()
    {
        if (Root is not { } root || DeviceGames.Count == 0) { _notify.Info("Refresh the device list first"); return; }
        var games = DeviceGames.Select(d => d.Game).Where(g => g.HasGameId).ToList();
        if (DownloadMissingArtFirst)
            await _libraryVm.DownloadArtAsync(games, _library.Settings.BulkArtTypes, overwrite: false, quiet: true);

        int files = 0;
        await Task.Run(() => { foreach (var g in games) files += _library.Opl.ExportArt(root, g, Overwrite); });
        _notify.Success("Art exported", $"{files} file(s) written to ART");
        await RefreshDeviceAsync();
    }

    [RelayCommand]
    private async Task ImportFromDeviceAsync()
    {
        if (Root is not { } root || DeviceGames.Count == 0) { _notify.Info("Refresh the device list first"); return; }
        if (!await _dialogs.ConfirmAsync("Import from device",
                "Copy game info from the device's CFG files into your edits, and copy device art you don't have into the local cache?\n\n" +
                "Existing edits for a game are only filled in, never overwritten.", "Import"))
            return;

        int infos = 0, arts = 0;
        await Task.Run(() =>
        {
            foreach (var d in DeviceGames.Where(d => d.Game.HasGameId))
            {
                arts += _library.Opl.ImportArt(root, d.Game);
                if (_library.Opl.ReadCfg(root, d.Game) is not { } cfg) continue;

                // Only keep CFG values that differ from the database, and never overwrite the user's own edits.
                var db = _library.Resolver.DatabaseInfo(d.Game);
                var existing = _library.Overrides.Get(d.Game.OverrideKey) ?? new GameInfo();
                var merged = existing.Clone();
                foreach (var f in GameInfo.TextFields)
                {
                    var v = cfg.Get(f);
                    if (v is null || existing.Get(f) is not null || string.Equals(v, db?.Get(f), StringComparison.Ordinal)) continue;
                    merged.Set(f, v);
                }
                merged.Compatibility ??= cfg.Compatibility;
                if (cfg.Extra is { Count: > 0 })
                {
                    merged.Extra ??= new(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in cfg.Extra) merged.Extra.TryAdd(kv.Key, kv.Value);
                }
                _library.Overrides.Set(d.Game.OverrideKey, merged);
                infos++;
            }
        });
        _library.RefreshAll();
        _notify.Success("Imported from device", $"{infos} CFG file(s) read · {arts} art file(s) copied");
    }

    [RelayCommand]
    private async Task FixFoldersAsync()
    {
        if (Root is not { } root) return;
        var wrong = DeviceGames.Where(d => d.Issue.Contains(" game in the ")).ToList();
        if (wrong.Count == 0) { _notify.Info("All games are in the right folder"); return; }
        if (!await _dialogs.ConfirmAsync("Fix CD/DVD folders", $"Move {wrong.Count} game(s) into the folder matching their media type?", "Move"))
            return;

        var errors = new List<string>();
        foreach (var d in wrong)
        {
            string target = Path.Combine(root.GameDirFor(d.Game.DiscType), d.Game.FileName);
            try { FileOperationPlan.MoveFile(d.Game.IsoPath, target); d.Game.IsoPath = target; }
            catch (Exception ex) { errors.Add($"{d.Game.FileName}: {ex.Message}"); }
        }
        if (errors.Count > 0) await _dialogs.AlertAsync("Some games couldn't be moved", string.Join("\n", errors));
        _library.Save();
        await RefreshDeviceAsync();
    }

    [RelayCommand]
    private async Task AddDeviceToLibraryAsync()
    {
        if (Root is not { } root) return;
        var folders = root.GameFolders.ToList();
        if (folders.Count == 0) { _notify.Info("No CD/DVD folders on the device"); return; }
        await _libraryVm.ScanFoldersAsync(folders, full: false, quiet: false);
    }

    [RelayCommand]
    private void EditDeviceGame(DeviceGame? d)
    {
        if (d is null) return;
        _libraryVm.EditInfoCommand.Execute(d.Game);
    }

    [RelayCommand]
    private void ManageDeviceArt(DeviceGame? d)
    {
        if (d is null) return;
        _libraryVm.ManageArtCommand.Execute(d.Game);
    }
}
