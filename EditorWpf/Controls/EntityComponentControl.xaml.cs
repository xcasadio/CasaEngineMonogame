using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using EditorWpf.Windows;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CasaEngine.Engine.Physics.Shapes;
using Microsoft.Xna.Framework.Graphics;

namespace EditorWpf.Controls
{
    /// <summary>
    /// Interaction logic for EntityComponentControl.xaml
    /// </summary>
    public partial class EntityComponentControl : UserControl
    {
        public event EventHandler? ComponentsChanged;

        public EntityComponentControl()
        {
            InitializeComponent();
        }

        private void ButtonDeleteComponentOnClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: Component component })
            {
                component.Owner.ComponentManager.Components.Remove(component);
                ComponentsChanged?.Invoke(component.Owner, EventArgs.Empty);
            }
        }

        private void StaticMeshComponent_MeshSelection_OnClick(object sender, RoutedEventArgs e)
        {
            var selectStaticMeshWindow = new SelectStaticMeshWindow();
            if (selectStaticMeshWindow.ShowDialog() == true && sender is FrameworkElement button)
            {
                var staticMeshComponent = button.DataContext as StaticMeshComponent;
                var graphicsDevice = EngineComponents.Game.GraphicsDevice;

                staticMeshComponent.Mesh = ((GeometricPrimitive)Activator.CreateInstance(selectStaticMeshWindow.SelectedType, graphicsDevice)).CreateMesh();
                staticMeshComponent.Mesh.Texture = EngineComponents.Game.Content.Load<Texture2D>("checkboard");
            }
        }

        private void PhysicComponent_ShapeSelection_OnClick(object sender, RoutedEventArgs e)
        {
            var selectPhysicsShapeWindow = new SelectPhysicsShapeWindow();
            if (selectPhysicsShapeWindow.ShowDialog() == true && sender is FrameworkElement button)
            {
                var physicsComponent = button.DataContext as PhysicsComponent;

                var shape = (Shape)Activator.CreateInstance(selectPhysicsShapeWindow.SelectedType);
                shape.Location = physicsComponent.Owner.Position;
                shape.Orientation = physicsComponent.Owner.Orientation;

                physicsComponent.Shape = shape;
            }
        }
    }
}
