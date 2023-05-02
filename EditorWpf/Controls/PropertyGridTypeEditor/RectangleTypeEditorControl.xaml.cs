using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xna.Framework;
using Xceed.Wpf.Toolkit.PropertyGrid;
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;

namespace EditorWpf.Controls.PropertyGridTypeEditor
{
    public partial class RectangleTypeEditorControl : UserControl, ITypeEditor
    {
        public static readonly DependencyProperty XProperty = DependencyProperty.Register(nameof(X), typeof(int), typeof(RectangleTypeEditorControl));
        public static readonly DependencyProperty YProperty = DependencyProperty.Register(nameof(Y), typeof(int), typeof(RectangleTypeEditorControl));
        public static readonly DependencyProperty WidthDependencyProperty = DependencyProperty.Register(nameof(RectangleWidth), typeof(int), typeof(RectangleTypeEditorControl));
        public static readonly DependencyProperty HeightDependencyProperty = DependencyProperty.Register(nameof(RectangleHeight), typeof(int), typeof(RectangleTypeEditorControl));

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
                    var rectangle = GetValue();
                    if (rectangle.X != value)
                    {
                        rectangle.X = value;
                        SetValue(rectangle);
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
                    var rectangle = GetValue();
                    if (rectangle.Y != value)
                    {
                        rectangle.Y = value;
                        SetValue(rectangle);
                    }
                }
            }
        }

        public int RectangleWidth
        {
            get => (int)GetValue(WidthDependencyProperty);
            set
            {
                SetValue(WidthDependencyProperty, value);

                if (_instance != null && _propertyInfo != null)
                {
                    var rectangle = GetValue();
                    if (rectangle.Width != value)
                    {
                        rectangle.Width = value;
                        SetValue(rectangle);
                    }
                }
            }
        }

        public int RectangleHeight
        {
            get => (int)GetValue(HeightDependencyProperty);
            set
            {
                SetValue(HeightDependencyProperty, value);

                if (_instance != null && _propertyInfo != null)
                {
                    var rectangle = GetValue();
                    if (rectangle.Height != value)
                    {
                        rectangle.Height = value;
                        SetValue(rectangle);
                    }
                }
            }
        }

        private void SetValue(Rectangle rectangle)
        {
            _propertyInfo.SetMethod.Invoke(_instance, new object?[] { rectangle });
        }

        private Rectangle GetValue()
        {
            return (Rectangle)_propertyInfo.GetMethod.Invoke(_instance, null);
        }

        public RectangleTypeEditorControl()
        {
            InitializeComponent();
        }

        public FrameworkElement ResolveEditor(PropertyItem propertyItem)
        {
            _instance = propertyItem.Instance;
            _propertyInfo = _instance.GetType().GetProperty(propertyItem.PropertyName, BindingFlags.Instance | BindingFlags.Public);

            var rectangle = (Rectangle)propertyItem.Value;
            X = rectangle.X;
            Y = rectangle.Y;
            RectangleWidth = rectangle.Width;
            RectangleHeight = rectangle.Height;

            return this;
        }
        //TODO: why binding doesn't work?

        private void OnHeightChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            RectangleHeight = (int)e.NewValue;
        }

        private void OnWidthChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            RectangleWidth = (int)e.NewValue;
        }

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
