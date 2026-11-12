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

        ApplyDetailsVisibility();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.ShowDetails)) ApplyDetailsVisibility();
        };
        SetUpColumns();
    }

    // ------------------------------------------------------------------ details pane

    private GridLength _detailsWidth = new(340);

    private void ApplyDetailsVisibility()
    {
        bool show = _vm.ShowDetails;
        if (!show && DetailsColumn.ActualWidth > 0) _detailsWidth = DetailsColumn.Width;
        DetailsColumn.MinWidth = show ? 260 : 0;
        DetailsColumn.Width = show ? _detailsWidth : new GridLength(0);
        SplitterColumn.Width = new GridLength(show ? 12 : 0);
        DetailsPane.Visibility = DetailsSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    // ------------------------------------------------------------------ list columns

    /// <summary>
    /// Right-click a column header to show/hide columns. Visibility and widths are remembered.
    /// </summary>
    private void SetUpColumns()
    {
        var settings = _vm.Library.Settings;
        var menu = new ContextMenu();
        var widthDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
        var defaults = GamesGrid.Columns.Where(c => c.Header is string).ToDictionary(c => (string)c.Header, c => c.Width);

        foreach (var column in GamesGrid.Columns)
        {
            if (column.Header is not string name) continue;   // cover thumbnail column stays fixed

            if (settings.ColumnWidths.TryGetValue(name, out var w) && w > 20)
                column.Width = new DataGridLength(w);
            widthDescriptor.AddValueChanged(column, (_, _) =>
            {
                // Only remember widths the user set by dragging (absolute pixels), not the star defaults.
                if (column.Width.IsAbsolute) settings.ColumnWidths[name] = column.Width.Value;
            });

            if (name == "Title") continue;   // always visible
            column.Visibility = settings.HiddenColumns.Contains(name) ? Visibility.Collapsed : Visibility.Visible;
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = column.Visibility == Visibility.Visible, StaysOpenOnClick = true };
            item.Click += (_, _) =>
            {
                column.Visibility = item.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                settings.HiddenColumns.Remove(name);
                if (!item.IsChecked) settings.HiddenColumns.Add(name);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var reset = new MenuItem { Header = "Reset column widths" };
        reset.Click += (_, _) =>
        {
            foreach (var column in GamesGrid.Columns)
                if (column.Header is string name && defaults.TryGetValue(name, out var width)) column.Width = width;
            settings.ColumnWidths.Clear();
        };
        menu.Items.Add(reset);

        var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader),
            GamesGrid.ColumnHeaderStyle ?? TryFindResource(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)) as Style);
        headerStyle.Setters.Add(new Setter(ContextMenuProperty, menu));
        headerStyle.Setters.Add(new Setter(ToolTipProperty, "Right-click to choose columns"));
        GamesGrid.ColumnHeaderStyle = headerStyle;
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
