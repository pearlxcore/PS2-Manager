using System.Windows;
using PS2Manager.App.ViewModels;
using PS2Manager.App.Views.Pages;
using PS2Manager.Core;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace PS2Manager.App.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _vm;

    private readonly Dictionary<string, FrameworkElement> _pages;

    public MainWindow(MainWindowViewModel vm, IServiceProvider services, ISnackbarService snackbar, IContentDialogService dialogs)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        // All pages live in the tree permanently; switching only flips Visibility, so lists keep
        // their realized items and nothing is unloaded/reloaded (that was the lag on every switch).
        _pages = new()
        {
            ["Library"] = services.GetRequiredService<LibraryPage>(),
            ["Tools"] = services.GetRequiredService<ToolsPage>(),
            ["Opl"] = services.GetRequiredService<OplPage>(),
            ["Settings"] = services.GetRequiredService<SettingsPage>(),
        };
        foreach (var page in _pages.Values)
        {
            page.Visibility = Visibility.Collapsed;
            PageHost.Children.Add(page);
        }
        NavLibrary.IsChecked = true;
        snackbar.SetSnackbarPresenter(SnackbarPresenter);
        dialogs.SetDialogHost(RootContentDialog);

        var s = vm.Library.Settings;
        Width = s.WindowWidth;
        Height = s.WindowHeight;
        if (s.WindowMaximized) WindowState = WindowState.Maximized;

        Loaded += OnLoaded;
        Closing += (_, _) =>
        {
            s.WindowMaximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal) { s.WindowWidth = Width; s.WindowHeight = Height; }
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { await _vm.StartupAsync(); }
        catch (Exception ex) { Log.Error("Startup", ex); }
    }

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string key } || _pages is null) return;
        foreach (var (name, page) in _pages)
            page.Visibility = name == key ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAbout(object sender, RoutedEventArgs e) =>
        new Dialogs.AboutWindow { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();

    private void OnTogglePane(object sender, RoutedEventArgs e)
    {
        var collapse = NavPane.Width > 100;
        NavPane.Width = collapse ? 52 : 210;
        // Hide labels (instead of clipping them mid-word) and show them as tooltips while collapsed.
        foreach (var item in FindNavItems(NavPane))
            if (item.Content is System.Windows.Controls.Panel panel)
                foreach (var label in panel.Children.OfType<System.Windows.Controls.TextBlock>())
                {
                    label.Visibility = collapse ? Visibility.Collapsed : Visibility.Visible;
                    item.ToolTip = collapse ? label.Text : null;
                }
    }

    private static IEnumerable<System.Windows.Controls.ContentControl> FindNavItems(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            // Nav items and nav actions: icon + label panels.
            if (child is System.Windows.Controls.Primitives.ButtonBase { Content: System.Windows.Controls.Panel } item) yield return item;
            else foreach (var nested in FindNavItems(child)) yield return nested;
        }
    }
}
