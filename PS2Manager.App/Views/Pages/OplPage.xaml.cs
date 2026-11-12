using System.Windows.Controls;
using System.Windows.Input;
using PS2Manager.App.ViewModels;

namespace PS2Manager.App.Views.Pages;

public partial class OplPage : Page
{
    private readonly OplViewModel _vm;
    private bool _loadedOnce;

    public OplPage(OplViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (_loadedOnce || !vm.IsRootValid) return;
            _loadedOnce = true;
            await vm.RefreshDeviceCommand.ExecuteAsync(null);
        };
    }

    private void OnDeviceDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DeviceGrid.SelectedItem is DeviceGame d) _vm.EditDeviceGameCommand.Execute(d);
    }
}
