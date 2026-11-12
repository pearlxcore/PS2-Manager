using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PS2Manager.App.Services;
using PS2Manager.App.ViewModels;
using PS2Manager.App.Views;
using PS2Manager.App.Views.Pages;
using PS2Manager.Core;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace PS2Manager.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) => { Log.Error("Unobserved task", args.Exception); args.SetObserved(); };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Write($"FATAL: {args.ExceptionObject}");

        var sc = new ServiceCollection();
        sc.AddSingleton<ISnackbarService, SnackbarService>();
        sc.AddSingleton<IContentDialogService, ContentDialogService>();

        sc.AddSingleton<LibraryService>();
        sc.AddSingleton<ActivityService>();
        sc.AddSingleton<DialogService>();
        sc.AddSingleton<NotifyService>();
        sc.AddSingleton<BackgroundCrcService>();

        sc.AddSingleton<MainWindowViewModel>();
        sc.AddSingleton<LibraryViewModel>();
        sc.AddSingleton<ToolsViewModel>();
        sc.AddSingleton<OplViewModel>();
        sc.AddSingleton<SettingsViewModel>();

        sc.AddSingleton<LibraryPage>();
        sc.AddSingleton<ToolsPage>();
        sc.AddSingleton<OplPage>();
        sc.AddSingleton<SettingsPage>();
        sc.AddSingleton<MainWindow>();

        Services = sc.BuildServiceProvider();

        ApplicationThemeManager.Apply(ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.Mica, true);
        ApplicationAccentColorManager.Apply(Color.FromRgb(0x4C, 0x8D, 0xFF), ApplicationTheme.Dark);

        var window = Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            var lib = Services.GetService<LibraryService>();
            if (lib?.IsLoaded == true) lib.Save();
            Services.GetService<BackgroundCrcService>()?.Stop();
            lib?.Settings.Save();
        }
        catch { }
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled UI exception", e.Exception);
        e.Handled = true;
        try { Services.GetService<NotifyService>()?.Error("Something went wrong", e.Exception.Message); } catch { }
    }
}
