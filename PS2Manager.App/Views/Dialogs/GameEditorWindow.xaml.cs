using PS2Manager.App.ViewModels;

namespace PS2Manager.App.Views.Dialogs;

public partial class GameEditorWindow : DialogWindow
{
    public GameEditorWindow(GameEditorViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        BindClose(h => vm.CloseRequested += h);
    }
}
