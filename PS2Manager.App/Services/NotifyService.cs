using Wpf.Ui;
using Wpf.Ui.Controls;

namespace PS2Manager.App.Services;

/// <summary>Toast-style messages via the WPF-UI snackbar in the main window.</summary>
public sealed class NotifyService(ISnackbarService snackbar)
{
    public void Success(string title, string message = "") =>
        Show(title, message, ControlAppearance.Success, SymbolRegular.CheckmarkCircle24);

    public void Info(string title, string message = "") =>
        Show(title, message, ControlAppearance.Info, SymbolRegular.Info24);

    public void Warning(string title, string message = "") =>
        Show(title, message, ControlAppearance.Caution, SymbolRegular.Warning24, 6);

    public void Error(string title, string message = "") =>
        Show(title, message, ControlAppearance.Danger, SymbolRegular.ErrorCircle24, 8);

    private void Show(string title, string message, ControlAppearance appearance, SymbolRegular icon, int seconds = 4)
    {
        void Do()
        {
            try { snackbar.Show(title, message, appearance, new SymbolIcon(icon), TimeSpan.FromSeconds(seconds)); }
            catch { /* presenter not ready yet */ }
        }
        var d = System.Windows.Application.Current.Dispatcher;
        if (d.CheckAccess()) Do(); else d.BeginInvoke(Do);
    }
}
