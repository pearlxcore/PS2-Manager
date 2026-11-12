using PS2Manager.App.ViewModels;

namespace PS2Manager.App.Views.Dialogs;

public partial class ExportWindow : DialogWindow
{
    public ExportWindow(ExportViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        BindClose(h => vm.CloseRequested += h);
    }
}
