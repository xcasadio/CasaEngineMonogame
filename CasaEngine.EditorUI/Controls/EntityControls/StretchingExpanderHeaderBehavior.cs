using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public class StretchingExpanderHeaderBehavior : Behavior<Expander>
{
    protected override void OnAttached()
    {
        AssociatedObject.Loaded += StretchHeader;
    }

    private void StretchHeader(object sender, RoutedEventArgs e)
    {
        if (AssociatedObject.Header is DependencyObject header
            && VisualTreeHelper.GetParent(header) is ContentPresenter contentPresenter)
        {
            contentPresenter.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }
}