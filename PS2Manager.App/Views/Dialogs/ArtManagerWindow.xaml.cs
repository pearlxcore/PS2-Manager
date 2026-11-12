using System.Windows;
using System.Windows.Input;
using PS2Manager.App.ViewModels;

namespace PS2Manager.App.Views.Dialogs;

public partial class ArtManagerWindow : DialogWindow
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp"];
    private readonly ArtManagerViewModel _vm;

    public ArtManagerWindow(ArtManagerViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private void OnSlotDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedImage(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnSlotDrop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ArtSlot slot } && DroppedImage(e) is { } file)
            _vm.SetFromFile(slot, file);
    }

    private static string? DroppedImage(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files.FirstOrDefault(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            : null;

    private void OnPreviewClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ArtSlot { HasImage: true } slot }) _vm.OpenCommand.Execute(slot);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
