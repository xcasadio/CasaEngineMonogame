using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using EditorWpf.Windows;
using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Engine.Physics.Shapes;

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
                var graphicsDevice = GameEditor.Game.GraphicsDevice;

                staticMeshComponent.Mesh = ((GeometricPrimitive)Activator.CreateInstance(selectStaticMeshWindow.SelectedType, graphicsDevice)).CreateMesh();
                staticMeshComponent.Mesh.Initialize(graphicsDevice);
                staticMeshComponent.Mesh.Texture = new CasaEngine.Framework.Assets.Textures.Texture(
                    graphicsDevice, "Content\\checkboard.png", GameEditor.Game.GameManager.AssetContentManager);
                //GameEditor.Game.Content.Load<Texture2D>("checkboard"));
            }
        }

        private void PhysicComponent_ShapeSelection_OnClick(object sender, RoutedEventArgs e)
        {
            var selectPhysicsShapeWindow = new SelectPhysicsShapeWindow();
            if (selectPhysicsShapeWindow.ShowDialog() == true && sender is FrameworkElement button)
            {
                var physicsComponent = button.DataContext as PhysicsComponent;

                var shape = (Shape)Activator.CreateInstance(selectPhysicsShapeWindow.SelectedType);
                SetParametersFromBoundingBox(shape, physicsComponent.Owner);
                shape.Location = physicsComponent.Owner.Position;
                shape.Orientation = physicsComponent.Owner.Orientation;

                physicsComponent.Shape = shape;
            }
        }

        private void SetParametersFromBoundingBox(Shape shape, Entity entity)
        {
            switch (shape.Type)
            {
                case ShapeType.Compound:
                    throw new ArgumentOutOfRangeException();
                    break;
                case ShapeType.Box:
                    var box = shape as Box;
                    box.Width = entity.BoundingBox.Max.X - entity.BoundingBox.Min.X;
                    box.Height = entity.BoundingBox.Max.Y - entity.BoundingBox.Min.Y;
                    box.Length = entity.BoundingBox.Max.Z - entity.BoundingBox.Min.Z;
                    break;
                case ShapeType.Capsule:
                    var capsule = shape as Capsule;
                    capsule.Length = entity.BoundingBox.Max.X - entity.BoundingBox.Min.X;
                    capsule.Radius = Math.Min(entity.BoundingBox.Max.Y - entity.BoundingBox.Min.Y, entity.BoundingBox.Max.Z - entity.BoundingBox.Min.Z);
                    break;
                case ShapeType.Cylinder:
                    var cylinder = shape as Cylinder;
                    cylinder.Length = entity.BoundingBox.Max.X - entity.BoundingBox.Min.X;
                    cylinder.Radius = Math.Min(entity.BoundingBox.Max.Y - entity.BoundingBox.Min.Y, entity.BoundingBox.Max.Z - entity.BoundingBox.Min.Z);
                    break;
                case ShapeType.Sphere:
                    var sphere = shape as Sphere;
                    sphere.Radius = Math.Min(entity.BoundingBox.Max.X - entity.BoundingBox.Min.X,
                        Math.Min(entity.BoundingBox.Max.Y - entity.BoundingBox.Min.Y, entity.BoundingBox.Max.Z - entity.BoundingBox.Min.Z));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
