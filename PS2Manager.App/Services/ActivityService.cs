using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PS2Manager.App.Services;

/// <summary>
/// The one long-running task at a time (scan, download, transfer…), shown in the status bar
/// with progress and a cancel button.
/// </summary>
public sealed partial class ActivityService : ObservableObject
{
    private CancellationTokenSource? _cts;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private double _progress;          // 0..1
    [ObservableProperty] private bool _isIndeterminate = true;

    public sealed class Scope : IDisposable
    {
        private readonly ActivityService _owner;
        internal Scope(ActivityService owner, CancellationToken token) { _owner = owner; Token = token; }
        public CancellationToken Token { get; }

        public void Report(string text, double? fraction = null)
        {
            void Apply()
            {
                _owner.Text = text;
                if (fraction is { } f) { _owner.IsIndeterminate = false; _owner.Progress = Math.Clamp(f, 0, 1); }
                else _owner.IsIndeterminate = true;
            }
            var d = System.Windows.Application.Current?.Dispatcher;
            if (d is null || d.CheckAccess()) Apply(); else d.BeginInvoke(Apply);
        }

        public void Dispose() => _owner.End();
    }

    /// <summary>Starts an activity, or returns null when another one is already running.</summary>
    public Scope? TryBegin(string text)
    {
        if (IsBusy) return null;
        _cts = new CancellationTokenSource();
        IsBusy = true;
        Text = text;
        Progress = 0;
        IsIndeterminate = true;
        CancelCommand.NotifyCanExecuteChanged();
        return new Scope(this, _cts.Token);
    }

    private void End()
    {
        IsBusy = false;
        Text = "";
        Progress = 0;
        _cts?.Dispose();
        _cts = null;
        CancelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        _cts?.Cancel();
        Text = "Cancelling…";
    }
}
