using System.Windows;
using PS2Manager.App.ViewModels;
using PS2Manager.App.Views.Pages;
using PS2Manager.Core;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace PS2Manager.App.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainWindowViewModel _vm;

    public MainWindow(MainWindowViewModel vm, IServiceProvider services, ISnackbarService snackbar, IContentDialogService dialogs)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        RootNavigation.SetServiceProvider(services);
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
        RootNavigation.Navigate(typeof(LibraryPage));
        try { await _vm.StartupAsync(); }
        catch (Exception ex) { Log.Error("Startup", ex); }
    }
}
