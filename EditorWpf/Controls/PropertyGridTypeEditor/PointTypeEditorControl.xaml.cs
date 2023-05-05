using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit.PropertyGrid;
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;
using Point = Microsoft.Xna.Framework.Point;

namespace EditorWpf.Controls.PropertyGridTypeEditor
{
    public partial class PointTypeEditorControl : UserControl, ITypeEditor
    {
        public static readonly DependencyProperty XProperty = DependencyProperty.Register(nameof(X), typeof(int), typeof(PointTypeEditorControl));
        public static readonly DependencyProperty YProperty = DependencyProperty.Register(nameof(Y), typeof(int), typeof(PointTypeEditorControl));

        private object? _instance;
        private PropertyInfo? _propertyInfo;

        public int X
        {
            get => (int)GetValue(XProperty);
            set
            {
                SetValue(XProperty, value);

                if (_instance != null && _propertyInfo != null)
                {
                    var Point = GetValue();
                    if (Point.X != value)
                    {
                        Point.X = value;
                        SetValue(Point);
                    }
                }
            }
        }

        public int Y
        {
            get => (int)GetValue(YProperty);
            set
            {
                SetValue(YProperty, value);

                if (_instance != null && _propertyInfo != null)
                {
                    var Point = GetValue();
                    if (Point.Y != value)
                    {
                        Point.Y = value;
                        SetValue(Point);
                    }
                }
            }
        }

        private void SetValue(Point Point)
        {
            _propertyInfo.SetMethod.Invoke(_instance, new object?[] { Point });
        }

        private Point GetValue()
        {
            return (Point)_propertyInfo.GetMethod.Invoke(_instance, null);
        }

        public PointTypeEditorControl()
        {
            InitializeComponent();
        }

        public FrameworkElement ResolveEditor(PropertyItem propertyItem)
        {
            _instance = propertyItem.Instance;
            _propertyInfo = _instance.GetType().GetProperty(propertyItem.PropertyName, BindingFlags.Instance | BindingFlags.Public);

            var Point = (Point)propertyItem.Value;
            X = Point.X;
            Y = Point.Y;

            return this;
        }
        //TODO: why binding doesn't work?
        private void OnYChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Y = (int)e.NewValue;
        }

        private void OnXChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            X = (int)e.NewValue;
        }
    }
}
