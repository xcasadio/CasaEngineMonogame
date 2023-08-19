using System;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Shapes;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using EditorWpf.Windows;
using Microsoft.Xna.Framework;

namespace EditorWpf.Controls.WorldControls
{
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
                var graphicsDevice = staticMeshComponent.Game.GraphicsDevice;

                staticMeshComponent.Mesh = ((GeometricPrimitive)Activator.CreateInstance(selectStaticMeshWindow.SelectedType, graphicsDevice)).CreateMesh();
                staticMeshComponent.Mesh.Initialize(graphicsDevice);
                staticMeshComponent.Mesh.Texture = staticMeshComponent.Game.GameManager.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);
            }
        }

        private void PhysicComponent_ShapeSelection_OnClick(object sender, RoutedEventArgs e)
        {
            var selectPhysicsShapeWindow = new SelectPhysicsShapeWindow();
            if (selectPhysicsShapeWindow.ShowDialog() == true && sender is FrameworkElement button)
            {
                var physicsComponent = button.DataContext as PhysicsComponent;

                var shape = (Shape3d)Activator.CreateInstance(selectPhysicsShapeWindow.SelectedType);
                SetParametersFromBoundingBox(shape, physicsComponent.Owner);
                shape.Position = physicsComponent.Owner.Position;
                shape.Orientation = physicsComponent.Owner.Orientation;

                physicsComponent.Shape = shape;
            }
        }

        private void SetParametersFromBoundingBox(Shape3d shape, Entity entity)
        {
            switch (shape.Type)
            {
                case Shape3dType.Compound:
                    throw new ArgumentOutOfRangeException();
                    break;
                case Shape3dType.Box:
                    var box = shape as Box;
                    var x = entity.BoundingBox.Max.X - entity.BoundingBox.Min.X;
                    var y = entity.BoundingBox.Max.Y - entity.BoundingBox.Min.Y;
                    var z = entity.BoundingBox.Max.Z - entity.BoundingBox.Min.Z;
                    box.Size = new Vector3(x, y, z);

                    break;
                case Shape3dType.Capsule:
                    var capsule = shape as Capsule;
                    capsule.Length = entity.BoundingBox.Max.X - entity.BoundingBox.Min.X;
                    capsule.Radius = Math.Min(entity.BoundingBox.Max.Y - entity.BoundingBox.Min.Y, entity.BoundingBox.Max.Z - entity.BoundingBox.Min.Z);
                    break;
                case Shape3dType.Cylinder:
                    var cylinder = shape as Cylinder;
                    cylinder.Length = entity.BoundingBox.Max.X - entity.BoundingBox.Min.X;
                    cylinder.Radius = Math.Min(entity.BoundingBox.Max.Y - entity.BoundingBox.Min.Y, entity.BoundingBox.Max.Z - entity.BoundingBox.Min.Z);
                    break;
                case Shape3dType.Sphere:
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
