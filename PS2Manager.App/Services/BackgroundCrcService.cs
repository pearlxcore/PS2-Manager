using CommunityToolkit.Mvvm.ComponentModel;
using PS2Manager.Core;
using PS2Manager.Core.Iso;
using PS2Manager.Core.Models;

namespace PS2Manager.App.Services;

/// <summary>
/// Fills in the PCSX2 CRC (which needs the whole boot ELF, several MB per disc) after scans,
/// one disc at a time so a hard drive reads sequentially. Waits while a foreground task runs.
/// </summary>
public sealed partial class BackgroundCrcService : ObservableObject
{
    private readonly LibraryService _library;
    private readonly ActivityService _activity;
    private CancellationTokenSource? _cts;
    private Task? _task;
    private bool _pending;

    public BackgroundCrcService(LibraryService library, ActivityService activity)
    {
        _library = library;
        _activity = activity;
        library.LibraryChanged += (_, _) => Kick();
    }

    /// <summary>Short status for the status bar; empty when idle.</summary>
    [ObservableProperty] private string _status = "";

    /// <summary>Starts a pass, or queues another one if a pass is already running. Call on the UI thread.</summary>
    public void Kick()
    {
        if (!_library.IsLoaded) return;
        if (_task is { IsCompleted: false }) { _pending = true; return; }
        _cts = new CancellationTokenSource();
        _task = RunAsync(_cts.Token);
    }

    public void Stop() => _cts?.Cancel();

    private static bool Needs(GameEntry g) =>
        g.Pcsx2Crc is null && !g.ElfCrcFailed && g.IsValid && g.HasGameId && !g.FileMissing && g.BootElf.Length > 0;

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            do
            {
                _pending = false;
                var todo = _library.Games.Where(Needs).ToList();   // snapshot on the UI thread
                int done = 0;
                foreach (var g in todo)
                {
                    // Give the disk to scans, transfers and other foreground work.
                    while (_activity.IsBusy) { Status = ""; await Task.Delay(1000, ct); }
                    if (!Needs(g)) continue;   // removed, renamed or already done meanwhile

                    Status = $"Calculating game CRCs {done + 1}/{todo.Count}";
                    string path = g.IsoPath, elf = g.BootElf;
                    uint? crc = await Task.Run(() => DiscReader.ComputeElfCrc(path, elf), ct);

                    if (!g.IsoPath.Equals(path, StringComparison.OrdinalIgnoreCase)) continue;   // file moved while reading
                    if (crc is null) g.ElfCrcFailed = true;
                    else g.Pcsx2Crc = crc;

                    if (++done % 25 == 0) _library.Save();
                }
                if (done > 0) { _library.Save(); Log.Write($"Background CRC pass: {done} discs"); }
            }
            while (_pending && !ct.IsCancellationRequested);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Error("Background CRC pass", ex); }
        finally { Status = ""; }
    }
}
