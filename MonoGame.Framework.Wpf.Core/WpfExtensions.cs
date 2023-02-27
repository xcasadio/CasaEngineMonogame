using System.Windows;
using System.Windows.Media;

namespace Microsoft.Xna.Framework
{
    /// <summary>
    /// WpfExtensions
    /// </summary>
    public static class WpfExtensions
    {
        public static T FindParent<T>(this DependencyObject child)
            where T : DependencyObject
        {
            while (true)
            {
                var parentObject = LogicalTreeHelper.GetParent(child);
                var parentObject2 = VisualTreeHelper.GetParent(child);
                if (parentObject == null && parentObject2 == null)
                {
                    return null;
                }

                var parent = parentObject as T ?? parentObject2 as T;
                if (parent != null)
                {
                    return parent;
                }

                child = parentObject ?? parentObject2;
            }
        }

        public static Func<IInputElement, Window> FindWindow = null;

        public static bool IsControlOnActiveWindow(this IInputElement element) => Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive) == GetWindowFrom(element);

        static Window GetWindowFrom(IInputElement focusElement)
        {
            var finder = FindWindow;
            if (finder != null)
            {
                return finder(focusElement);
            }

            if (!(focusElement is FrameworkElement el))
            {
                throw new NotSupportedException("Only FrameworkElement is currently supported."); // we use D3D11Host which derives from image, so the _focusElement should always be castable to FrameworkElement for us
            }

            return Window.GetWindow(el);
        }
    }
}