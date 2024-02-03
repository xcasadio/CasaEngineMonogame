using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Maths;
using CasaEngine.Core.Shapes;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.EditorUI.Windows;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using CasaEngine.Framework.SceneManagement;
using CasaEngine.Framework.SceneManagement.Components;
using CasaEngine.WpfControls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Texture = CasaEngine.Framework.Assets.Textures.Texture;

namespace CasaEngine.EditorUI.Controls.EntityControls;

public partial class EntityComponentControl : UserControl
{
    private CasaEngineGame _game;

    public EntityComponentControl()
    {
        DataContextChanged += OnDataContextChanged;

        InitializeComponent();
    }
    public void InitializeFromGameEditor(GameEditor gameEditor)
    {
        gameEditor.GameStarted += OnGameStarted;
    }

    private void OnGameStarted(object? sender, EventArgs e)
    {
        _game = (CasaEngineGame)sender;
        _game.FrameComputed += OnFrameComputed;
    }

    private void OnFrameComputed(object? sender, EventArgs e)
    {
        if (IsVisible && sender is CasaEngineGame { IsRunningInGameEditorMode: true })
        {
            var expression = Vector3ControlPosition.GetBindingExpression(Vector3Editor.ValueProperty);
            expression?.UpdateTarget();

            expression = RotationControl.GetBindingExpression(RotationEditor.ValueProperty);
            expression?.UpdateTarget();

            expression = Vector3ControlScale.GetBindingExpression(Vector3Editor.ValueProperty);
            expression?.UpdateTarget();
        }
    }


    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is EntityViewModel oldEntity && oldEntity.Entity.RootComponent != null)
        {
            oldEntity.Entity.RootComponent.Coordinates.PositionChanged -= OnEntityPositionChanged;
            oldEntity.Entity.RootComponent.Coordinates.OrientationChanged -= OnEntityOrientationChanged;
            oldEntity.Entity.RootComponent.Coordinates.ScaleChanged -= OnEntityScaleChanged;
        }

        //Attach event because we can modify coordinate with the mouse with the game screenGui
        if (e.NewValue is EntityViewModel entity && entity.Entity.RootComponent != null)
        {
            entity.Entity.RootComponent.Coordinates.PositionChanged += OnEntityPositionChanged;
            entity.Entity.RootComponent.Coordinates.OrientationChanged += OnEntityOrientationChanged;
            entity.Entity.RootComponent.Coordinates.ScaleChanged += OnEntityScaleChanged;
        }
    }

    private void OnEntityOrientationChanged(object? sender, EventArgs e)
    {
        var coordinates = sender as Coordinates;
        RotationControl.Value = coordinates.Orientation;
    }

    private void OnEntityPositionChanged(object? sender, EventArgs e)
    {
        var coordinates = sender as Coordinates;
        Vector3ControlPosition.Value = coordinates.Position;
    }

    private void OnEntityScaleChanged(object? sender, EventArgs e)
    {
        var coordinates = sender as Coordinates;
        Vector3ControlScale.Value = coordinates.Scale;
    }

    private void StaticMeshComponent_MeshSelection_OnClick(object sender, RoutedEventArgs e)
    {
        var selectStaticMeshWindow = new SelectStaticMeshWindow();
        if (selectStaticMeshWindow.ShowDialog() == true
            && sender is FrameworkElement { DataContext: StaticMeshComponent staticMeshComponent })
        {
            var graphicsDevice = staticMeshComponent.Owner.RootComponent.World.Game.GraphicsDevice;

            staticMeshComponent.Mesh = CreateGeometricPrimitive(selectStaticMeshWindow.SelectedType, graphicsDevice).CreateMesh();
            staticMeshComponent.Mesh.Initialize(graphicsDevice, staticMeshComponent.Owner.RootComponent.World.Game.AssetContentManager);
            staticMeshComponent.Mesh.Texture = staticMeshComponent.Owner.RootComponent.World.Game.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);
        }
    }

    private static GeometricPrimitive CreateGeometricPrimitive(Type type, GraphicsDevice graphicsDevice)
    {
        return (GeometricPrimitive)Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance |
            BindingFlags.OptionalParamBinding,
            null, new object[] { graphicsDevice }, null, null);
    }

    private static BoundingBox CreateDefaultBoundingBox()
    {
        return new BoundingBox(-Vector3.One / 2f, Vector3.One / 2f);
    }

    private void SetParametersFromBoundingBox(Shape3d shape, BoundingBox boundingBox)
    {
        switch (shape.Type)
        {
            case Shape3dType.Compound:
                throw new ArgumentOutOfRangeException();
                break;
            case Shape3dType.Box:
                var box = shape as Box;
                var x = boundingBox.Max.X - boundingBox.Min.X;
                var y = boundingBox.Max.Y - boundingBox.Min.Y;
                var z = boundingBox.Max.Z - boundingBox.Min.Z;
                box.Size = new Vector3(x, y, z);
                break;
            case Shape3dType.Capsule:
                var capsule = shape as Capsule;
                capsule.Length = boundingBox.Max.X - boundingBox.Min.X;
                capsule.Radius = Math.Min(boundingBox.Max.Y - boundingBox.Min.Y, boundingBox.Max.Z - boundingBox.Min.Z);
                break;
            case Shape3dType.Cylinder:
                var cylinder = shape as Cylinder;
                cylinder.Length = boundingBox.Max.X - boundingBox.Min.X;
                cylinder.Radius = Math.Min(boundingBox.Max.Y - boundingBox.Min.Y, boundingBox.Max.Z - boundingBox.Min.Z);
                break;
            case Shape3dType.Sphere:
                var sphere = shape as Sphere;
                sphere.Radius = Math.Min(boundingBox.Max.X - boundingBox.Min.X,
                    Math.Min(boundingBox.Max.Y - boundingBox.Min.Y, boundingBox.Max.Z - boundingBox.Min.Z));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ComboBoxExternalComponent_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox
            && sender is FrameworkElement { DataContext: AActor actor })
        {
            System.Diagnostics.Debugger.Break();
            //var externalComponent = GameSettings.ScriptLoader.Create(((KeyValuePair<int, Type>)comboBox.SelectedValue).Key);
            //externalComponent.LoadContent(gamePlayComponent.Owner);
            //gamePlayComponent.ExternalComponent = externalComponent;
        }
    }
}