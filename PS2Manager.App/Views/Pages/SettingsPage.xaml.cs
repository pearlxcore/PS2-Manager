using System.Windows.Controls;
using PS2Manager.App.ViewModels;

namespace PS2Manager.App.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}
