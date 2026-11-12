using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace PS2Manager.App.Services;

public sealed class DialogService
{
    public static Window? ActiveWindow =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
    {
        var box = new MessageBox
        {
            Title = title,
            Content = new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 480 },
            PrimaryButtonText = confirmText,
            CloseButtonText = cancelText,
            Owner = ActiveWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        return await box.ShowDialogAsync() == MessageBoxResult.Primary;
    }

    /// <summary>Three-way choice. Returns 0 = primary, 1 = secondary, -1 = cancel.</summary>
    public async Task<int> ChooseAsync(string title, string message, string primary, string secondary, string cancel = "Cancel")
    {
        var box = new MessageBox
        {
            Title = title,
            Content = new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 480 },
            PrimaryButtonText = primary,
            SecondaryButtonText = secondary,
            IsSecondaryButtonEnabled = true,
            CloseButtonText = cancel,
            Owner = ActiveWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        return await box.ShowDialogAsync() switch
        {
            MessageBoxResult.Primary => 0,
            MessageBoxResult.Secondary => 1,
            _ => -1,
        };
    }

    public async Task AlertAsync(string title, string message)
    {
        var box = new MessageBox
        {
            Title = title,
            Content = new System.Windows.Controls.TextBox
            {
                Text = message, TextWrapping = TextWrapping.Wrap, IsReadOnly = true, MaxWidth = 560, MaxHeight = 400,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto, BorderThickness = new Thickness(0),
            },
            CloseButtonText = "OK",
            Owner = ActiveWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        await box.ShowDialogAsync();
    }

    public string? PickFolder(string title, string? initial = null)
    {
        var dlg = new OpenFolderDialog { Title = title, Multiselect = false };
        if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial)) dlg.InitialDirectory = initial;
        return dlg.ShowDialog(ActiveWindow) == true ? dlg.FolderName : null;
    }

    public string[] PickFolders(string title)
    {
        var dlg = new OpenFolderDialog { Title = title, Multiselect = true };
        return dlg.ShowDialog(ActiveWindow) == true ? dlg.FolderNames : [];
    }

    public string? PickFile(string title, string filter)
    {
        var dlg = new OpenFileDialog { Title = title, Filter = filter };
        return dlg.ShowDialog(ActiveWindow) == true ? dlg.FileName : null;
    }

    public string? SaveFile(string title, string filter, string fileName)
    {
        var dlg = new SaveFileDialog { Title = title, Filter = filter, FileName = fileName, AddExtension = true };
        return dlg.ShowDialog(ActiveWindow) == true ? dlg.FileName : null;
    }

    /// <summary>Shows a modal tool window owned by the active window.</summary>
    public bool? ShowDialog(Window window)
    {
        window.Owner = ActiveWindow;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        return window.ShowDialog();
    }
}
