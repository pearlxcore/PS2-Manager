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
    NotifyService notify) : ObservableObject
{
    public LibraryService Library => library;
    public LibraryViewModel LibraryVm => libraryVm;
    public ActivityService Activity => activity;

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

        if (library.Settings.AutoDownloadCovers)
            await libraryVm.DownloadMissingCoversAsync(quiet: true);
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
