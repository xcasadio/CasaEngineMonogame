using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls
{
    /// <summary>
    /// Interaction logic for Vector3Control.xaml
    /// </summary>
    public partial class Vector3Control : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(Vector3), typeof(Vector3Control), new PropertyMetadata(Vector3.Zero, OnValueChanged));

        public event PropertyChangedEventHandler? PropertyChanged;

        [Bindable(true)]
        public Vector3 Value
        {
            get => (Vector3)GetValue(ValueProperty);
            set
            {
                SetValue(ValueProperty, value);
                OnPropertyChanged("X");
            }
        }

        private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {


            if (sender is Vector3Control ctrl)
            {
                if (ctrl.PropertyChanged == null)
                {
                    return;
                }

                ctrl.PropertyChanged(sender, new PropertyChangedEventArgs("X"));
                ctrl.PropertyChanged(sender, new PropertyChangedEventArgs("Y"));
                ctrl.PropertyChanged(sender, new PropertyChangedEventArgs("Z"));
            }
        }

        public float X
        {
            get => Value.X;
            set
            {
                var temp = Value;
                if (value == temp.X)
                {
                    return;
                }
                Value = new Vector3(value, temp.Y, temp.Z);
                //OnPropertyChanged("Value");
                OnPropertyChanged("X");
            }
        }

        public float Y
        {
            get => Value.Y;
            set
            {
                var temp = Value;
                if (value == temp.Y)
                {
                    return;
                }
                Value = new Vector3(temp.X, value, temp.Z);
                OnPropertyChanged("Y");
            }
        }

        public float Z
        {
            get => Value.Z;
            set
            {
                var temp = Value;
                if (value == temp.Z)
                {
                    return;
                }

                Value = new Vector3(temp.X, temp.Y, value);
                OnPropertyChanged("Z");
            }
        }

        public Vector3Control()
        {
            InitializeComponent();
            //(Content as FrameworkElement).DataContext = this;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
