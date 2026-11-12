using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PS2Manager.App.Views.Dialogs;

/// <summary>Common look and behaviour for tool windows: solid dark background, custom title bar, Esc closes.</summary>
public class DialogWindow : FluentWindow
{
    public DialogWindow()
    {
        ExtendsContentIntoTitleBar = true;
        // Solid background: Mica made every dialog open/close recompose the whole desktop area.
        WindowBackdropType = WindowBackdropType.None;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x1C, 0x20));
        WindowCornerPreference = WindowCornerPreference.Round;
        ShowInTaskbar = false;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ContentRendered += (_, _) => PS2Manager.Core.Log.Write($"{GetType().Name} shown in {sw.ElapsedMilliseconds} ms");
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
