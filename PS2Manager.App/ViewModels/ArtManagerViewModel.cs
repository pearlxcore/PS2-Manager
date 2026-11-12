using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS2Manager.App.Services;
using PS2Manager.Core;
using PS2Manager.Core.Art;
using PS2Manager.Core.Models;

namespace PS2Manager.App.ViewModels;

public sealed partial class ArtSlot : ObservableObject
{
    public ArtSlot(ArtType type) => Type = type;

    public ArtType Type { get; }
    public string Name => Type.DisplayName();
    public string Suffix => Type.Suffix();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string? _path;

    [ObservableProperty] private string _info = "";
    [ObservableProperty] private bool _isBusy;

    public bool HasImage => Path is not null;
}

public sealed partial class ArtManagerViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly DialogService _dialogs;
    private readonly NotifyService _notify;

    public ArtManagerViewModel(GameEntry game, LibraryService library, DialogService dialogs, NotifyService notify)
    {
        Game = game;
        _library = library;
        _dialogs = dialogs;
        _notify = notify;
        Slots = ArtTypes.All.Select(t => new ArtSlot(t)).ToList();
        SourceOptions = ["Auto (priority order)", .. ArtSourceCatalog.All.Select(s => s.Name)];
        _selectedSource = SourceOptions[0];
        RefreshSlots();
    }

    public GameEntry Game { get; }
    public IReadOnlyList<ArtSlot> Slots { get; }
    public IReadOnlyList<string> SourceOptions { get; }
    public string Subtitle => $"{Game.GameId}  ·  OPL name: {Game.OplId}_*.png";

    [ObservableProperty] private string _selectedSource;
    [ObservableProperty] private bool _isDownloadingAll;

    private IReadOnlyList<IArtSource> Sources =>
        SelectedSource == SourceOptions[0]
            ? _library.Settings.ResolveArtSources()
            : ArtSourceCatalog.All.Where(s => s.Name == SelectedSource).ToList();

    private void RefreshSlots()
    {
        foreach (var s in Slots) UpdateSlot(s);
    }

    private void UpdateSlot(ArtSlot slot)
    {
        slot.Path = null;
        var path = _library.Art.Get(Game.GameId, slot.Type);
        slot.Path = path;
        slot.Info = path is null ? "None" : Describe(path);
    }

    private static string Describe(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var frame = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            return $"{frame.PixelWidth}×{frame.PixelHeight} · {System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant()} · {new FileInfo(path).Length / 1024:N0} KB";
        }
        catch { return System.IO.Path.GetFileName(path); }
    }

    [RelayCommand]
    private async Task DownloadAsync(ArtSlot slot)
    {
        slot.IsBusy = true;
        try
        {
            var path = await _library.Downloader.DownloadAsync(Game.GameId, slot.Type, Sources);
            UpdateSlot(slot);
            if (path is null) _notify.Info($"No {slot.Name.ToLowerInvariant()} found", $"Tried: {string.Join(", ", Sources.Where(s => s.Supports(slot.Type)).Select(s => s.Name))}");
        }
        catch (Exception ex) { _notify.Error("Download failed", ex.Message); }
        finally { slot.IsBusy = false; }
    }

    [RelayCommand]
    private async Task DownloadMissingAsync()
    {
        IsDownloadingAll = true;
        int found = 0;
        try
        {
            foreach (var slot in Slots.Where(s => !s.HasImage))
            {
                slot.IsBusy = true;
                try
                {
                    if (await _library.Downloader.DownloadAsync(Game.GameId, slot.Type, Sources) is not null) found++;
                    UpdateSlot(slot);
                }
                finally { slot.IsBusy = false; }
            }
            _notify.Success("Done", $"{found} image(s) downloaded.");
        }
        catch (Exception ex) { _notify.Error("Download failed", ex.Message); }
        finally { IsDownloadingAll = false; }
    }

    [RelayCommand]
    private void Browse(ArtSlot slot)
    {
        var file = _dialogs.PickFile($"Choose {slot.Name.ToLowerInvariant()}", "Images|*.png;*.jpg;*.jpeg;*.bmp");
        if (file is not null) SetFromFile(slot, file);
    }

    public void SetFromFile(ArtSlot slot, string file)
    {
        try
        {
            _library.Art.Import(Game.GameId, slot.Type, file);
            UpdateSlot(slot);
        }
        catch (Exception ex) { _notify.Error("Couldn't use that image", ex.Message); }
    }

    [RelayCommand]
    private void Paste(ArtSlot slot)
    {
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var file = Clipboard.GetFileDropList().Cast<string>().FirstOrDefault();
                if (file is not null) { SetFromFile(slot, file); return; }
            }
            if (Clipboard.GetImage() is { } img)
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(img));
                using var ms = new MemoryStream();
                enc.Save(ms);
                _library.Art.Save(Game.GameId, slot.Type, ms.ToArray(), ".png");
                UpdateSlot(slot);
                return;
            }
            _notify.Info("Clipboard has no image");
        }
        catch (Exception ex) { _notify.Error("Paste failed", ex.Message); }
    }

    [RelayCommand]
    private void Remove(ArtSlot slot)
    {
        try
        {
            _library.Art.Delete(Game.GameId, slot.Type);
            UpdateSlot(slot);
        }
        catch (Exception ex) { _notify.Error("Delete failed", ex.Message); }
    }

    [RelayCommand]
    private void Open(ArtSlot slot)
    {
        if (slot.Path is not null) ShellService.Open(slot.Path);
    }

    [RelayCommand]
    private void OpenFolder() => ShellService.ShowInExplorer(_library.Art.GameDir(Game.GameId));

    [RelayCommand]
    private void ExportToOpl()
    {
        var root = _library.OplRoot;
        if (root is not { Exists: true }) { _notify.Warning("No OPL device set", "Choose the device on the OPL Device page first."); return; }
        try
        {
            int n = _library.Opl.ExportArt(root, Game);
            _notify.Success("Exported to OPL", $"{n} file(s) written to {root.ArtDir}");
        }
        catch (Exception ex) { Log.Error("Export art", ex); _notify.Error("Export failed", ex.Message); }
    }
}
