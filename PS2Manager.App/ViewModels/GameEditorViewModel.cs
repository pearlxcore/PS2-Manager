using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS2Manager.App.Services;
using PS2Manager.App.Views.Dialogs;
using PS2Manager.Core;
using PS2Manager.Core.Models;
using PS2Manager.Core.Opl;

namespace PS2Manager.App.ViewModels;

/// <summary>One editable field: shows the database value as the baseline and flags changes.</summary>
public sealed partial class EditableField : ObservableObject
{
    public EditableField(string key, string label, string? baseValue, string? value, IReadOnlyList<string>? options = null)
    {
        Key = key;
        Label = label;
        BaseValue = baseValue;
        Options = options;
        _value = value ?? baseValue ?? "";
    }

    public string Key { get; }
    public string Label { get; }
    public string? BaseValue { get; }
    public IReadOnlyList<string>? Options { get; }
    public string BaseText => string.IsNullOrWhiteSpace(BaseValue) ? "Database: (empty)" : $"Database: {BaseValue}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    private string _value;

    public bool IsChanged => Norm(Value) != Norm(BaseValue);

    [RelayCommand]
    private void Reset() => Value = BaseValue ?? "";

    private static string Norm(string? s) => (s ?? "").Trim();
}

public sealed partial class CompatibilityOption(int bit, string name, string hint, bool isChecked) : ObservableObject
{
    public int Bit { get; } = bit;
    public string Name { get; } = name;
    public string Hint { get; } = hint;
    [ObservableProperty] private bool _isChecked = isChecked;
}

public sealed partial class GameEditorViewModel : ObservableObject
{
    private readonly LibraryService _library;
    private readonly DialogService _dialogs;
    private readonly NotifyService _notify;
    private Dictionary<string, string>? _extra;

    public GameEditorViewModel(GameEntry game, LibraryService library, DialogService dialogs, NotifyService notify)
    {
        Game = game;
        _library = library;
        _dialogs = dialogs;
        _notify = notify;

        var dbInfo = library.Resolver.DatabaseInfo(game);
        var baseInfo = dbInfo?.Clone() ?? new GameInfo();
        baseInfo.Region ??= NullIfEmpty(GameIds.DetectRegion(game.GameId));
        // Without a DB entry, the visible title (label/filename) is the baseline.
        baseInfo.Title ??= !string.IsNullOrWhiteSpace(game.VolumeLabel)
            ? Core.Iso.DiscReader.HumanizeVolumeLabel(game.VolumeLabel)
            : Path.GetFileNameWithoutExtension(game.IsoPath);

        var ovr = library.Overrides.Get(game.OverrideKey);
        _extra = ovr?.Extra;

        Fields = new Dictionary<string, EditableField>
        {
            ["Title"] = F("Title", "Title"),
            ["Genre"] = F("Genre", "Genre"),
            ["Developer"] = F("Developer", "Developer"),
            ["Publisher"] = F("Publisher", "Publisher"),
            ["Release"] = F("Release", "Release date"),
            ["Region"] = F("Region", "Region"),
            ["Language"] = F("Language", "Languages"),
            ["Description"] = F("Description", "Description"),
            ["Players"] = F("Players", "Players", OplCfgValues.Players),
            ["Rating"] = F("Rating", "Rating", OplCfgValues.Rating),
            ["Aspect"] = F("Aspect", "Aspect ratio", OplCfgValues.Aspect),
            ["Scan"] = F("Scan", "Scan mode", OplCfgValues.Scan),
            ["Vmode"] = F("Vmode", "Video mode", OplCfgValues.Vmode),
            ["Parental"] = F("Parental", "Parental rating", OplCfgValues.Parental),
        };

        EditableField F(string key, string label, IReadOnlyList<string>? options = null) =>
            new(key, label, baseInfo.Get(key), ovr?.Get(key), options);

        int compat = ovr?.Compatibility ?? 0;
        Compatibility = OplCfgValues.CompatibilityModes
            .Select(m => new CompatibilityOption(m.Bit, m.Name, m.Hint, (compat & (1 << m.Bit)) != 0)).ToList();

        MediaOptions = [$"Auto (detected: {game.DetectedDiscType})", "CD", "DVD"];
        _media = ovr?.Media switch { DiscType.CD => "CD", DiscType.DVD => "DVD", _ => MediaOptions[0] };

        var root = library.OplRoot;
        HasOplRoot = root is { Exists: true } && game.HasGameId;
        OplCfgPath = HasOplRoot ? root!.CfgPath(game.GameId) : "";
        HasOplCfg = HasOplRoot && File.Exists(OplCfgPath);
        _writeToOpl = HasOplRoot && library.Settings.AutoWriteCfgOnSave;
    }

    public GameEntry Game { get; }
    public Dictionary<string, EditableField> Fields { get; }
    public IReadOnlyList<CompatibilityOption> Compatibility { get; }
    public IReadOnlyList<string> MediaOptions { get; }
    public bool HasOplRoot { get; }
    public bool HasOplCfg { get; }
    public string OplCfgPath { get; }

    public string Subtitle => $"{(Game.HasGameId ? Game.GameId : "No game ID")}  ·  {Game.FileName}";
    public string TitleSourceText => Game.TitleSource switch
    {
        TitleSource.Database => "Title from GameDB",
        TitleSource.Override => "Title edited by you",
        TitleSource.VolumeLabel => "Not in GameDB: title from disc label",
        TitleSource.Filename => "Not in GameDB: title from file name",
        _ => "",
    };

    [ObservableProperty] private string _media;
    [ObservableProperty] private bool _writeToOpl;

    public event Action<bool>? CloseRequested;

    [RelayCommand]
    private void ResetAll()
    {
        foreach (var f in Fields.Values) f.Value = f.BaseValue ?? "";
        foreach (var c in Compatibility) c.IsChecked = false;
        Media = MediaOptions[0];
        _extra = null;
    }

    [RelayCommand]
    private void ImportFromOpl()
    {
        var root = _library.OplRoot;
        if (root is null) return;
        var info = _library.Opl.ReadCfg(root, Game);
        if (info is null) { _notify.Info("No CFG on device", OplCfgPath); return; }

        foreach (var f in Fields.Values)
            if (info.Get(f.Key) is { } v) f.Value = v;
        if (info.Compatibility is { } compat)
            foreach (var c in Compatibility) c.IsChecked = (compat & (1 << c.Bit)) != 0;
        _extra = info.Extra;
        _notify.Info("Loaded from OPL CFG", "Review and press Save to keep these values.");
    }

    [RelayCommand]
    private void ManageArt()
    {
        if (!Game.HasGameId) { _notify.Warning("No game ID", "Art is stored by game ID."); return; }
        _dialogs.ShowDialog(new ArtManagerWindow(new ArtManagerViewModel(Game, _library, _dialogs, _notify)));
        _library.RefreshCover(Game);
    }

    [RelayCommand]
    private void Save()
    {
        var ovr = new GameInfo();
        foreach (var f in Fields.Values)
            ovr.Set(f.Key, f.IsChanged ? f.Value : null);

        int mask = Compatibility.Where(c => c.IsChecked).Aggregate(0, (m, c) => m | (1 << c.Bit));
        ovr.Compatibility = mask > 0 ? mask : null;
        ovr.Media = Media switch { "CD" => DiscType.CD, "DVD" => DiscType.DVD, _ => null };
        ovr.Extra = _extra is { Count: > 0 } ? new Dictionary<string, string>(_extra, StringComparer.OrdinalIgnoreCase) : null;

        _library.Overrides.Set(Game.OverrideKey, ovr);
        _library.Refresh(Game);
        _library.NotifyChanged();

        if (WriteToOpl && _library.OplRoot is { } root)
        {
            try
            {
                _library.Opl.WriteCfg(root, Game);
                _notify.Success("Saved", $"Also written to {Path.GetFileName(OplCfgPath)} on the OPL device.");
            }
            catch (Exception ex)
            {
                Log.Error("Write CFG", ex);
                _notify.Error("Saved locally, but writing the OPL CFG failed", ex.Message);
            }
        }
        else
        {
            _notify.Success("Game info saved", Game.Title);
        }
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
}
