using System;
using System.Windows;
using CasaEngine.Engine.Primitives3D;

namespace EditorWpf.Windows
{
    /// <summary>
    /// Interaction logic for SelectStaticMeshWindow.xaml
    /// </summary>
    public partial class SelectStaticMeshWindow : Window
    {
        public Type? SelectedType { get; set; }

        public SelectStaticMeshWindow()
        {
            InitializeComponent();
        }

        private void ButtonCube_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(BoxPrimitive));
        }

        private void ButtonSphere_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(SpherePrimitive));
        }

        private void ButtonCylinder_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(CylinderPrimitive));
        }

        private void ButtonPlane_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(PlanePrimitive));
        }

        private void ButtonTeapot_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(TeapotPrimitive));
        }

        private void ButtonTorus_Click(object sender, RoutedEventArgs e)
        {
            SetType(typeof(TorusPrimitive));
        }

        private void SetType(Type typeSelected)
        {
            SelectedType = typeSelected;
            DialogResult = true;
            Close();
        }
    }
}
