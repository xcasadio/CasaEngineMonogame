using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using CasaEngine.Core.Maths;
using CasaEngine.EditorUI.Controls.EntityControls.ViewModels;
using CasaEngine.EditorUI.Windows;
using CasaEngine.Engine;
using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using CasaEngine.WpfControls;
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
            staticMeshComponent.Mesh = CreateGeometricPrimitive(selectStaticMeshWindow.SelectedType).CreateMesh();
            staticMeshComponent.Mesh.Initialize(staticMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager);
            staticMeshComponent.Mesh.Texture = staticMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);
        }
    }

    private static GeometricPrimitive CreateGeometricPrimitive(Type type)
    {
        return (GeometricPrimitive)Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.OptionalParamBinding,
            null, null, null, null);
    }

    public bool ValidateStaticMeshAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is StaticMeshComponent staticMeshComponent
            && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.Texture)
        {
            if (staticMeshComponent.Mesh != null)
            {
                var assetContentManager = staticMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager;
                staticMeshComponent.Mesh.Texture = assetContentManager.Load<Texture>(assetId);

                if (staticMeshComponent.Mesh.Texture?.Resource == null)
                {
                    staticMeshComponent.Mesh.Texture.Load(assetContentManager);
                }
            }

            return true;
        }

        return false;
    }

    public bool ValidateTileMapAsset(object owner, Guid assetId, string assetFullName)
    {
        if (owner is TileMapComponent tileMapComponent
            && System.IO.Path.GetExtension(assetFullName) == Constants.FileNameExtensions.TileMap)
        {
            tileMapComponent.TileMapDataAssetId = assetId;

            return true;
        }

        return false;
    }
}