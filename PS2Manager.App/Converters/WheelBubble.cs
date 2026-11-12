using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PS2Manager.App.Converters;

/// <summary>
/// Lets a nested list pass the mouse wheel to the page around it when the list itself has
/// nothing (more) to scroll. WPF lists otherwise swallow the wheel, so the page "won't scroll"
/// whenever the cursor happens to be over one.
/// </summary>
public static class WheelBubble
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(WheelBubble), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el) return;
        el.PreviewMouseWheel -= OnPreviewMouseWheel;
        if (e.NewValue is true) el.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not UIElement el) return;
        var sv = FindChild<ScrollViewer>(el);
        if (sv is { ScrollableHeight: > 0 })
        {
            var canScroll = e.Delta < 0 ? sv.VerticalOffset < sv.ScrollableHeight - 0.5 : sv.VerticalOffset > 0.5;
            if (canScroll) return;   // let the list scroll itself
        }

        e.Handled = true;
        if (VisualTreeHelper.GetParent(el) is UIElement parent)
            parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender,
            });
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            if (FindChild<T>(child) is { } found) return found;
        }
        return null;
    }
}
