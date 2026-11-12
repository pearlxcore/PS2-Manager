using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PS2Manager.App.ViewModels;
using PS2Manager.Core.Iso;
using PS2Manager.Core.Models;

namespace PS2Manager.App.Views.Pages;

public partial class LibraryPage : UserControl
{
    private readonly LibraryViewModel _vm;

    public LibraryPage(LibraryViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        Drop += OnDrop;
        DragOver += OnDragOver;
        PreviewKeyDown += OnKeyDown;
    }

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _vm.SelectedGames = GamesGrid.SelectedItems.OfType<GameEntry>().ToList();

    private void OnCoverSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        _vm.SelectedGames = CoverWall.SelectedItems.OfType<GameEntry>().ToList();

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Ignore double-clicks on headers/scrollbars.
        if (e.OriginalSource is DependencyObject d && FindParent<System.Windows.Controls.Primitives.ScrollBar>(d) is null
            && (FindParent<DataGridRow>(d) is not null || FindParent<ListViewItem>(d) is not null))
            _vm.EditInfoCommand.Execute(null);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is TextBox) return;   // don't steal keys from text input

        switch (e.Key)
        {
            case Key.Enter when _vm.EditInfoCommand.CanExecute(null):
                _vm.EditInfoCommand.Execute(null); e.Handled = true; break;
            case Key.F2 when _vm.RenameCommand.CanExecute(null):
                _vm.RenameCommand.Execute(null); e.Handled = true; break;
            case Key.Delete when _vm.DeleteFilesCommand.CanExecute(null):
                _vm.DeleteFilesCommand.Execute(null); e.Handled = true; break;
            case Key.F5:
                _vm.RescanCommand.Execute(null); e.Handled = true; break;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        // Folders are scanned as-is; dropped disc files add their containing folder.
        var folders = paths.Select(p => Directory.Exists(p) ? p : DiscReader.IsSupported(p) ? Path.GetDirectoryName(p) : null)
                           .OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (folders.Count > 0) await _vm.ScanFoldersAsync(folders, full: false, quiet: false);
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        for (var d = child; d is not null; d = d is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                 ? System.Windows.Media.VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is T t) return t;
        return null;
    }
}
