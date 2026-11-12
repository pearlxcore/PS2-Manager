using CommunityToolkit.Mvvm.ComponentModel;
using PS2Manager.App.Services;
using PS2Manager.Core;
using PS2Manager.Core.Database;

namespace PS2Manager.App.ViewModels;

public sealed partial class MainWindowViewModel(
    LibraryService library,
    LibraryViewModel libraryVm,
    ActivityService activity,
    DialogService dialogs,
    NotifyService notify,
    BackgroundCrcService backgroundCrc) : ObservableObject
{
    public LibraryService Library => library;
    public LibraryViewModel LibraryVm => libraryVm;
    public ActivityService Activity => activity;
    public BackgroundCrcService BackgroundCrc => backgroundCrc;

    /// <summary>Startup: load cached library, offer database download, then refresh in the background.</summary>
    public async Task StartupAsync()
    {
        await library.LoadAsync();

        if (library.Database.Count == 0)
        {
            bool download = await dialogs.ConfirmAsync("Game database",
                "The PS2 game database isn't downloaded yet. It provides titles, genres, developers and release dates.\n\n" +
                "Download it now from GameDB-PS2 (about 10 MB)?", "Download", "Not now");
            if (download) await DownloadDatabaseAsync();
        }

        if (library.Settings.AutoScanOnStartup && library.Folders.Count > 0)
            await libraryVm.RescanAsync(full: false, quiet: true);

        if (library.Settings.CheckForUpdates)
            await CheckForUpdatesAsync(manual: false);

        if (library.Settings.AutoDownloadCovers)
            await libraryVm.DownloadMissingCoversAsync(quiet: true);
    }

    /// <summary>
    /// Looks for a newer release on GitHub. On startup it's silent unless there is one (and the user hasn't
    /// skipped that version); from Settings it also reports "up to date" and errors.
    /// </summary>
    public async Task CheckForUpdatesAsync(bool manual)
    {
        var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        try
        {
            var update = await PS2Manager.Core.Updates.UpdateChecker.CheckAsync(current);
            if (update is null)
            {
                if (manual) notify.Success("You're up to date", $"PS2 Manager {current.ToString(3)} is the latest version.");
                return;
            }

            string latest = update.Version.ToString(3);
            if (!manual && library.Settings.SkippedUpdateVersion == latest) return;

            string message = $"PS2 Manager {latest} is available. You have {current.ToString(3)}.";
            if (manual)
            {
                if (await dialogs.ConfirmAsync("Update available", message, "Download", "Later")) Services.ShellService.Open(update.PageUrl);
                return;
            }

            switch (await dialogs.ChooseAsync("Update available", message, "Download", "Skip this version", "Later"))
            {
                case 0: Services.ShellService.Open(update.PageUrl); break;
                case 1: library.Settings.SkippedUpdateVersion = latest; library.Settings.Save(); break;
            }
        }
        catch (Exception ex)
        {
            PS2Manager.Core.Log.Write($"Update check failed: {ex.Message}");
            if (manual) notify.Error("Couldn't check for updates", ex.Message);
        }
    }

    public async Task DownloadDatabaseAsync()
    {
        using var scope = activity.TryBegin("Downloading game database…");
        if (scope is null) { notify.Warning("Busy", "Another task is running."); return; }
        try
        {
            var progress = new Progress<double>(p => scope.Report($"Downloading game database… {p:P0}", p));
            if (await library.UpdateDatabaseAsync(progress, scope.Token))
                notify.Success("Database updated", library.DatabaseStatus);
            else
                notify.Error("Database download failed", "Check your connection. Titles fall back to disc labels for now.");
        }
        catch (OperationCanceledException) { notify.Info("Download cancelled"); }
        catch (Exception ex) { Log.Error("DB download", ex); notify.Error("Database download failed", ex.Message); }
    }
}
