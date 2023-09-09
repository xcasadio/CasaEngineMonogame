using System;
using System.Windows;
using CasaEngine.Core.Shapes;

namespace CasaEngine.Editor.Windows
{
    /// <summary>
    /// Interaction logic for SelectPhysicsShapeWindow.xaml
    /// </summary>
    public partial class SelectPhysicsShapeWindow : Window
    {
        public Type? SelectedType { get; set; }

        public SelectPhysicsShapeWindow()
        {
            InitializeComponent();
        }

        private void ButtonBox_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(Box));
        }

        private void ButtonSphere_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(Sphere));
        }

        private void ButtonCylinder_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(Cylinder));
        }

        private void ButtonCapsule_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(Capsule));
        }

        private void ButtonCompound_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(Shape3dCompound));
        }

        private void SetType(Type typeSelected)
        {
            SelectedType = typeSelected;
            DialogResult = true;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedType = null;
            DialogResult = false;
            Close();
        }
    }
}
