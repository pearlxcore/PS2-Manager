using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PS2Manager.Core;
using PS2Manager.Core.Iso;
using PS2Manager.Core.Models;
using PS2Manager.Core.Pcsx2;

namespace PS2Manager.App.Services;

/// <summary>
/// Works alongside an existing PCSX2 install: finds it, launches games with PCSX2's own settings,
/// shares covers and library folders, and shows PCSX2's compatibility rating per game.
/// </summary>
public sealed partial class Pcsx2Service : ObservableObject
{
    private readonly LibraryService _library;
    private readonly NotifyService _notify;
    private readonly DialogService _dialogs;
    private Dictionary<string, Pcsx2Compat> _compat = new(StringComparer.OrdinalIgnoreCase);

    public Pcsx2Service(LibraryService library, NotifyService notify, DialogService dialogs)
    {
        _library = library;
        _notify = notify;
        _dialogs = dialogs;
        library.LibraryChanged += (_, _) => ApplyCompat();
        Detect();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAvailable), nameof(Status))]
    private Pcsx2Install? _install;

    public bool IsAvailable => Install is not null;

    public string Status => Install is { } i
        ? $"PCSX2 {i.Version ?? ""} · data in {i.DataDir}{(i.IsPortable ? " (portable)" : "")}"
        : "PCSX2 not found. Choose pcsx2-qt.exe to enable Play and the PCSX2 tools.";

    /// <summary>Finds PCSX2 (using the saved path first) and loads its compatibility list in the background.</summary>
    public void Detect()
    {
        Install = Pcsx2Install.Detect(_library.Settings.Pcsx2ExePath);
        if (Install is { } i && string.IsNullOrWhiteSpace(_library.Settings.Pcsx2ExePath))
            _library.Settings.Pcsx2ExePath = i.ExePath;
        _ = LoadCompatAsync();
    }

    public bool SetExe(string exePath)
    {
        if (!File.Exists(exePath)) return false;
        _library.Settings.Pcsx2ExePath = exePath;
        _library.Settings.Save();
        Detect();
        return IsAvailable;
    }

    private async Task LoadCompatAsync()
    {
        var path = Install?.GameIndexPath;
        try
        {
            _compat = path is null ? new(StringComparer.OrdinalIgnoreCase) : await Task.Run(() => Pcsx2GameIndex.Load(path));
            if (_compat.Count > 0) Log.Write($"PCSX2 GameIndex: {_compat.Count} compatibility ratings");
        }
        catch (Exception ex) { Log.Error("Reading PCSX2 GameIndex", ex); }
        Application.Current?.Dispatcher.Invoke(ApplyCompat);
    }

    private void ApplyCompat()
    {
        foreach (var g in _library.Games)
            g.Pcsx2Compat = g.HasGameId && _compat.TryGetValue(g.GameId, out var c) ? c : Pcsx2Compat.Unknown;
    }

    // ------------------------------------------------------------------ actions

    public bool CanPlay(GameEntry? g) => IsAvailable && g is { FileMissing: false };

    public async Task PlayAsync(GameEntry game)
    {
        if (Install is not { } i) { _notify.Warning("PCSX2 not found", "Set the PCSX2 location in Settings."); return; }
        if (!File.Exists(game.IsoPath)) { _notify.Error("File not found", game.IsoPath); return; }
        try
        {
            string? state = null;
            if (await FindResumeStateAsync(i, game) is { } resume)
            {
                var saved = File.GetLastWriteTime(resume);
                int choice = await _dialogs.ChooseAsync("Resume game?",
                    $"{game.Title} has a resume point from {saved:d MMM yyyy, HH:mm} ({Ago(saved)}).\n\nContinue where you left off?",
                    "Yes", "No");
                if (choice < 0) return;          // cancelled: don't launch
                if (choice == 0) state = resume;
            }

            i.Launch(game.IsoPath, _library.Settings.Pcsx2Options, state);
            _notify.Info(state is null ? "Starting PCSX2" : "Resuming in PCSX2", game.Title);
        }
        catch (Exception ex)
        {
            Log.Error("PCSX2 launch", ex);
            _notify.Error("Couldn't start PCSX2", ex.Message);
        }
    }

    /// <summary>
    /// Finds this game's resume state. Uses the PCSX2 CRC when known; if it isn't yet and there are several
    /// candidates (different versions of the game), the CRC is computed now (reads the boot ELF, ~0.5 s).
    /// </summary>
    private static async Task<string?> FindResumeStateAsync(Pcsx2Install install, GameEntry game)
    {
        if (!game.HasGameId) return null;
        var states = install.FindResumeStates(game.GameId, game.Pcsx2Crc);
        if (states.Count <= 1 || game.Pcsx2Crc is not null) return states.FirstOrDefault();

        var crc = await Task.Run(() => DiscReader.ComputeElfCrc(game.IsoPath, game.BootElf));
        if (crc is null) return null;
        game.Pcsx2Crc = crc;
        return install.FindResumeStates(game.GameId, crc).FirstOrDefault();
    }

    private static string Ago(DateTime t)
    {
        var d = DateTime.Now - t;
        return d.TotalMinutes < 1 ? "just now"
            : d.TotalHours < 1 ? $"{(int)d.TotalMinutes} min ago"
            : d.TotalDays < 1 ? $"{(int)d.TotalHours} h ago"
            : d.TotalDays < 2 ? "yesterday"
            : $"{(int)d.TotalDays} days ago";
    }

    public async Task ExportCoversAsync(bool overwrite)
    {
        if (Install is not { } i) return;
        try
        {
            var games = _library.Games.ToList();
            var (copied, existing, none) = await Task.Run(() => i.ExportCovers(games, _library.Art, overwrite));
            _notify.Success("Covers sent to PCSX2", $"{copied} copied · {existing} already there · {none} without a cover");
        }
        catch (Exception ex) { Log.Error("PCSX2 covers", ex); _notify.Error("Couldn't copy covers", ex.Message); }
    }

    /// <summary>Library folders PCSX2 doesn't scan yet.</summary>
    public IReadOnlyList<string> MissingGameListFolders()
    {
        if (Install is not { } i) return [];
        var existing = i.ReadGameListPaths();
        return _library.Folders.Where(f => !Pcsx2Ini.IsCovered(f, existing)).ToList();
    }

    public void AddFoldersToGameList()
    {
        if (Install is not { } i) return;
        try
        {
            var added = i.AddGameListPaths(_library.Folders);
            if (added.Count == 0) _notify.Info("Nothing to add", "PCSX2 already scans all your library folders.");
            else _notify.Success("Folders added to PCSX2", string.Join(", ", added) + ". They'll appear next time PCSX2 starts.");
        }
        catch (InvalidOperationException ex) { _notify.Warning("PCSX2 is running", ex.Message); }
        catch (Exception ex) { Log.Error("PCSX2 game list", ex); _notify.Error("Couldn't update PCSX2.ini", ex.Message); }
    }
}
