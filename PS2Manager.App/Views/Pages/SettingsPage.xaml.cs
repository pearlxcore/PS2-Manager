using System.Windows.Controls;
using PS2Manager.App.ViewModels;

namespace PS2Manager.App.Views.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage(SettingsViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        IsVisibleChanged += (_, e) => { if (e.NewValue is true) vm.RefreshPcsx2(); };
    }
}
