using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS2Manager.App.Services;
using PS2Manager.Core;
using PS2Manager.Core.Models;
using PS2Manager.Core.Rename;

namespace PS2Manager.App.ViewModels;

public enum FileOperationKind { Rename, MoveToFolders, Flatten, FixExtensions }

/// <summary>Preview-then-apply dialog for every bulk file operation.</summary>
public sealed partial class FileOperationViewModel : ObservableObject
{
    private readonly IReadOnlyList<GameEntry> _games;
    private readonly LibraryService _library;
    private readonly ActivityService _activity;
    private readonly DialogService _dialogs;
    private readonly NotifyService _notify;
    private readonly DispatcherTimer _debounce;

    public FileOperationViewModel(FileOperationKind kind, IReadOnlyList<GameEntry> games, LibraryService library,
        ActivityService activity, DialogService dialogs, NotifyService notify)
    {
        Kind = kind;
        _games = games;
        _library = library;
        _activity = activity;
        _dialogs = dialogs;
        _notify = notify;

        Presets = kind == FileOperationKind.MoveToFolders ? RenameFormats.FolderPresets : RenameFormats.FilePresets;
        _template = kind == FileOperationKind.MoveToFolders ? library.Settings.FolderTemplate : library.Settings.RenameTemplate;
        _conflictMode = library.Settings.ConflictMode;
        _selectedPreset = Presets.FirstOrDefault(p => p.Template == _template);

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); BuildPlan(); };
        BuildPlan();
    }

    public FileOperationKind Kind { get; }
    public IReadOnlyList<RenameFormat> Presets { get; }
    public IReadOnlyList<RenameToken> Tokens => RenameTokens.All;
    public ObservableCollection<PlanItem> Items { get; } = [];

    public bool UsesTemplate => Kind is FileOperationKind.Rename or FileOperationKind.MoveToFolders;
    public bool UsesTargetFolder => Kind is FileOperationKind.Flatten or FileOperationKind.MoveToFolders;

    public string WindowTitle => Kind switch
    {
        FileOperationKind.Rename => "Batch rename",
        FileOperationKind.MoveToFolders => "Move into folders",
        FileOperationKind.Flatten => "Flatten into one folder",
        _ => "Fix file extensions",
    };

    public string Explanation => Kind switch
    {
        FileOperationKind.Rename => "Rename files from game info. The extension is kept. Nothing changes until you press Apply.",
        FileOperationKind.MoveToFolders => "Move each game into its own folder named from game info. Leave the base folder empty to create folders next to each file.",
        FileOperationKind.Flatten => "Move every selected file directly into one folder.",
        _ => "Raw CD images (2352-byte sectors) get .bin; normal 2048-byte images get .iso.",
    };

    public string TargetFolderLabel => Kind == FileOperationKind.Flatten ? "Target folder" : "Base folder (optional)";

    [ObservableProperty] private string _template;
    [ObservableProperty] private RenameFormat? _selectedPreset;
    [ObservableProperty] private string? _targetFolder;
    [ObservableProperty] private ConflictMode _conflictMode;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _templateError = "";
    [ObservableProperty] private bool _isApplied;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private int _readyCount;

    public event Action<bool>? CloseRequested;

    partial void OnTemplateChanged(string value) => Schedule();
    partial void OnTargetFolderChanged(string? value) => Schedule();
    partial void OnConflictModeChanged(ConflictMode value) => BuildPlan();
    partial void OnSelectedPresetChanged(RenameFormat? value) { if (value is not null) Template = value.Template; }

    private void Schedule() { _debounce.Stop(); _debounce.Start(); }

    [RelayCommand]
    private void InsertToken(RenameToken token) => Template = (Template ?? "") + token.Token;

    [RelayCommand]
    private void BrowseTarget()
    {
        var folder = _dialogs.PickFolder(TargetFolderLabel, TargetFolder);
        if (folder is not null) TargetFolder = folder;
    }

    [RelayCommand]
    private void SelectAll(string? include)
    {
        bool on = include != "false";
        foreach (var i in Items.Where(i => i.Status == PlanStatus.Ready)) i.Include = on;
        UpdateSummary();
    }

    public void BuildPlan()
    {
        if (IsApplied) return;
        var unknown = UsesTemplate ? RenameTokens.UnknownTokens(Template ?? "").ToList() : [];
        TemplateError = unknown.Count > 0 ? $"Unknown token(s): {string.Join(" ", unknown)}" : "";

        List<PlanItem> plan = Kind switch
        {
            FileOperationKind.Rename => FileOperationPlan.Rename(_games, Template ?? "", ConflictMode),
            FileOperationKind.MoveToFolders => FileOperationPlan.MoveToFolders(_games, Template ?? "",
                string.IsNullOrWhiteSpace(TargetFolder) ? null : TargetFolder, ConflictMode),
            FileOperationKind.Flatten => string.IsNullOrWhiteSpace(TargetFolder) ? [] : FileOperationPlan.Flatten(_games, TargetFolder!, ConflictMode),
            _ => FileOperationPlan.FixExtensions(_games, ConflictMode),
        };

        foreach (var old in Items) old.PropertyChanged -= ItemChanged;
        Items.Clear();
        foreach (var p in plan.OrderBy(p => p.Status == PlanStatus.Ready ? 0 : p.Status == PlanStatus.Conflict ? 1 : 2))
        {
            p.PropertyChanged += ItemChanged;
            Items.Add(p);
        }
        UpdateSummary();
    }

    private void ItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlanItem.Include)) UpdateSummary();
    }

    private void UpdateSummary()
    {
        ReadyCount = Items.Count(i => i.CanApply);
        if (Kind == FileOperationKind.Flatten && string.IsNullOrWhiteSpace(TargetFolder))
        {
            Summary = "Choose a target folder.";
            return;
        }
        int unchanged = Items.Count(i => i.Status == PlanStatus.Unchanged);
        int conflicts = Items.Count(i => i.Status == PlanStatus.Conflict);
        int problems = Items.Count(i => i.Status is PlanStatus.Invalid or PlanStatus.SourceMissing);
        Summary = $"{ReadyCount} to change · {unchanged} already correct" +
                  (conflicts > 0 ? $" · {conflicts} conflicts" : "") +
                  (problems > 0 ? $" · {problems} problems" : "");
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        using var scope = _activity.TryBegin($"{WindowTitle}…");
        if (scope is null) { _notify.Warning("Busy", "Another task is running."); return; }

        try
        {
            var progress = new Progress<(int Done, int Total)>(p => scope.Report($"{WindowTitle}: {p.Done}/{p.Total}", (double)p.Done / p.Total));
            var result = await FileOperationPlan.ApplyAsync(Items.ToList(), WindowTitle, progress, scope.Token);

            if (result.Batch is not null)
            {
                _library.History.Push(result.Batch);
                _library.AfterFilesMoved(result.Batch.Moves);
            }

            // Remember the template the user settled on.
            if (Kind == FileOperationKind.Rename) _library.Settings.RenameTemplate = Template;
            if (Kind == FileOperationKind.MoveToFolders) _library.Settings.FolderTemplate = Template;
            _library.Settings.ConflictMode = ConflictMode;
            _library.Settings.Save();

            IsApplied = true;
            ReadyCount = 0;
            Summary = $"{result.Succeeded} done" + (result.Failed > 0 ? $" · {result.Failed} failed (see list)" : "") +
                      (result.Cancelled ? " · cancelled" : "") + " · Undo is available on the Tools page.";
            if (result.Cancelled)
                _notify.Info("Cancelled", $"{result.Succeeded} file(s) were changed before cancelling. Undo is on the Tools page.");
            else if (result.Failed == 0)
            {
                _notify.Success(WindowTitle, $"{result.Succeeded} file(s) updated.");
                CloseRequested?.Invoke(true);
            }
        }
        catch (OperationCanceledException) { _notify.Info("Cancelled", "Files already moved stay moved; use Undo to revert."); }
        catch (Exception ex) { Log.Error(WindowTitle, ex); _notify.Error($"{WindowTitle} failed", ex.Message); }
    }

    private bool CanApply() => ReadyCount > 0 && !IsApplied;

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(IsApplied);
}
