using System.Windows;
using PS2Manager.App.ViewModels;

namespace PS2Manager.App.Views.Dialogs;

public partial class DuplicatesWindow : DialogWindow
{
    public DuplicatesWindow(DuplicatesViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
