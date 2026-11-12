using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PS2Manager.App.Views.Dialogs;

/// <summary>Common look and behaviour for tool windows: Mica, custom title bar, Esc closes.</summary>
public class DialogWindow : FluentWindow
{
    public DialogWindow()
    {
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.Mica;
        WindowCornerPreference = WindowCornerPreference.Round;
        ShowInTaskbar = false;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && e.OriginalSource is not System.Windows.Controls.ComboBox) { Close(); e.Handled = true; }
        };
    }

    /// <summary>Hooks a view model's CloseRequested(bool) event.</summary>
    protected void BindClose(Action<Action<bool>> subscribe) =>
        subscribe(result =>
        {
            try { DialogResult = result; } catch (InvalidOperationException) { }
            Close();
        });
}
