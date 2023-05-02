using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace EditorWpf.Controls
{
    /// <summary>
    /// Interaction logic for Vector3Control.xaml
    /// </summary>
    public partial class Vector3Control : UserControl
    {
        public static readonly DependencyProperty XProperty = DependencyProperty.Register(nameof(X), typeof(float), typeof(Vector3Control), new PropertyMetadata(0f, OnValueChanged));
        public static readonly DependencyProperty YProperty = DependencyProperty.Register(nameof(Y), typeof(float), typeof(Vector3Control), new PropertyMetadata(0f, OnValueChanged));
        public static readonly DependencyProperty ZProperty = DependencyProperty.Register(nameof(Z), typeof(float), typeof(Vector3Control), new PropertyMetadata(0f, OnValueChanged));

        public event PropertyChangedEventHandler? PropertyChanged;

        public float X
        {
            get => (float)GetValue(XProperty);
            set => SetValue(XProperty, value);
        }

        public float Y
        {
            get => (float)GetValue(YProperty);
            set => SetValue(YProperty, value);
        }

        public float Z
        {
            get => (float)GetValue(ZProperty);
            set => SetValue(ZProperty, value);
        }

        private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Vector3Control ctrl)
            {
                if (ctrl.PropertyChanged == null)
                {
                    return;
                }

                ctrl.PropertyChanged(sender, new PropertyChangedEventArgs(e.Property.Name));
            }
        }

        public Vector3Control()
        {
            InitializeComponent();
        }
    }
}
