using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PS2Manager.App.Services;
using PS2Manager.Core;
using PS2Manager.Core.Export;
using PS2Manager.Core.Models;

namespace PS2Manager.App.ViewModels;

public sealed partial class ExportColumnOption(ExportColumn column) : ObservableObject
{
    public ExportColumn Column { get; } = column;
    public string Header => Column.Header;
    [ObservableProperty] private bool _isChecked = column.DefaultOn;
}

public sealed partial class ExportViewModel : ObservableObject
{
    private readonly IReadOnlyList<GameEntry> _visible, _all, _selected;
    private readonly DialogService _dialogs;
    private readonly NotifyService _notify;

    public ExportViewModel(IReadOnlyList<GameEntry> visible, IReadOnlyList<GameEntry> all, IReadOnlyList<GameEntry> selected,
        DialogService dialogs, NotifyService notify)
    {
        _visible = visible;
        _all = all;
        _selected = selected;
        _dialogs = dialogs;
        _notify = notify;
        Columns = LibraryExporter.Columns.Select(c => new ExportColumnOption(c)).ToList();
        _scope = visible.Count < all.Count ? "Filtered" : "All";
    }

    public IReadOnlyList<ExportColumnOption> Columns { get; }
    public string AllText => $"All games ({_all.Count})";
    public string FilteredText => $"Current filter ({_visible.Count})";
    public string SelectedText => $"Selected ({_selected.Count})";
    public bool HasSelection => _selected.Count > 0;

    [ObservableProperty] private string _scope;

    public event Action<bool>? CloseRequested;

    [RelayCommand]
    private void Export()
    {
        var cols = Columns.Where(c => c.IsChecked).Select(c => c.Column).ToList();
        if (cols.Count == 0) { _notify.Warning("Pick at least one column"); return; }

        var path = _dialogs.SaveFile("Export library", "Excel workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv|Text (*.txt)|*.txt",
            $"PS2 Library {DateTime.Now:yyyy-MM-dd}.xlsx");
        if (path is null) return;

        var games = (Scope switch { "Selected" => _selected, "Filtered" => _visible, _ => _all })
            .OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        try
        {
            LibraryExporter.Export(games, cols, path);
            _notify.Success("Exported", $"{games.Count} games → {Path.GetFileName(path)}");
            ShellService.ShowInExplorer(path);
            CloseRequested?.Invoke(true);
        }
        catch (Exception ex)
        {
            Log.Error("Export", ex);
            _notify.Error("Export failed", ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
