using PS2Manager.App.ViewModels;

namespace PS2Manager.App.Views.Dialogs;

public partial class FileOperationWindow : DialogWindow
{
    public FileOperationWindow(FileOperationViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        BindClose(h => vm.CloseRequested += h);
    }
}
