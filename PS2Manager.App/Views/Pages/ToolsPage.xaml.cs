using System.Windows.Controls;
using PS2Manager.App.ViewModels;

namespace PS2Manager.App.Views.Pages;

public partial class ToolsPage : UserControl
{
    public ToolsPage(ToolsViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        // Pages stay loaded; refresh each time this one is shown.
        IsVisibleChanged += (_, e) => { if (e.NewValue is true) vm.Refresh(); };
    }
}
