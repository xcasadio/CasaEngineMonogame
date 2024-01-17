using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Shapes;
using CasaEngine.EditorUI.Windows;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class EntityComponentControl : UserControl
{
    public EntityComponentControl()
    {
        InitializeComponent();
    }

    private void ButtonDeleteComponentOnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Component component })
        {
            component.Owner.ComponentManager.Remove(component);
        }
    }

    private void StaticMeshComponent_MeshSelection_OnClick(object sender, RoutedEventArgs e)
    {
        var selectStaticMeshWindow = new SelectStaticMeshWindow();
        if (selectStaticMeshWindow.ShowDialog() == true
            && sender is FrameworkElement { DataContext: StaticMeshComponent staticMeshComponent })
        {
            var graphicsDevice = staticMeshComponent.Owner.Game.GraphicsDevice;

            staticMeshComponent.Mesh = CreateGeometricPrimitive(selectStaticMeshWindow.SelectedType, graphicsDevice).CreateMesh();
            staticMeshComponent.Mesh.Initialize(graphicsDevice, staticMeshComponent.Owner.Game.GameManager.AssetContentManager);
            staticMeshComponent.Mesh.Texture = staticMeshComponent.Owner.Game.GameManager.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);
        }
    }

    private static GeometricPrimitive CreateGeometricPrimitive(Type type, GraphicsDevice graphicsDevice)
    {
        return (GeometricPrimitive)Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance |
            BindingFlags.OptionalParamBinding,
            null, new object[] { graphicsDevice }, null, null);
    }

    private void PhysicComponent_ShapeSelection_OnClick(object sender, RoutedEventArgs e)
    {
        var selectPhysicsShapeWindow = new SelectPhysicsShapeWindow();
        if (selectPhysicsShapeWindow.ShowDialog() == true
            && sender is FrameworkElement { DataContext: PhysicsComponent physicsComponent })
        {
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

    private void ComboBoxExternalComponent_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox
            && sender is FrameworkElement { DataContext: GamePlayComponent gamePlayComponent })
        {
            var externalComponent = GameSettings.ScriptLoader.Create(((KeyValuePair<int, Type>)comboBox.SelectedValue).Key);
            externalComponent.Initialize(gamePlayComponent.Owner);
            gamePlayComponent.ExternalComponent = externalComponent;
        }
    }
}