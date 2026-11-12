using System.Windows.Controls;
using PS2Manager.App.ViewModels;

namespace PS2Manager.App.Views.Pages;

public partial class ToolsPage : Page
{
    public ToolsPage(ToolsViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        Loaded += (_, _) => vm.Refresh();
    }
}
