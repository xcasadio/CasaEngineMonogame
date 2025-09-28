using CasaEngine.Engine.Primitives3D;
using CasaEngine.Framework.Assets.Textures;
using CasaEngine.Framework.Entities.Components;
using System;
using System.Reflection;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class StaticMeshComponentViewModel : SceneComponentViewModel
{
    private readonly StaticMeshComponent _staticMeshComponent;

    public int VertexCount => _staticMeshComponent?.Mesh?.VertexBuffer?.VertexCount ?? 0;

    public Guid? TextureAssetId
    {
        get => _staticMeshComponent.Mesh?.TextureAssetId;
        set
        {
            if (value.HasValue && _staticMeshComponent.Mesh?.TextureAssetId != value)
            {
                _staticMeshComponent.Mesh?.LoadTexture(value.Value, _staticMeshComponent.Owner?.World.Game.AssetContentManager);
            }
        }
    }

    public Guid MeshAssetId
    {
        get => _staticMeshComponent.Mesh?.AssetId ?? Guid.Empty;
        set
        {
            if (_staticMeshComponent.Mesh != null && _staticMeshComponent.Mesh?.AssetId != value)
            {
                _staticMeshComponent.AssetId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VertexCount));
            }
        }
    }

    public StaticMeshComponentViewModel(EntityComponent entityComponent): base(entityComponent)
    {
        _staticMeshComponent = (StaticMeshComponent)entityComponent;
    }

    public void CreateMesh(Type meshType)
    {
        _staticMeshComponent.Mesh = CreateGeometricPrimitive(meshType).CreateMesh();
        _staticMeshComponent.Mesh.Initialize(_staticMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager);
        _staticMeshComponent.Mesh.Texture = _staticMeshComponent.Owner.RootComponent.Owner.World.Game.AssetContentManager.GetAsset<Texture>(Texture.DefaultTextureName);
        
        OnPropertyChanged(nameof(MeshAssetId));
        OnPropertyChanged(nameof(TextureAssetId));
        OnPropertyChanged(nameof(VertexCount));
    }

    private static GeometricPrimitive CreateGeometricPrimitive(Type type)
    {
        return (GeometricPrimitive)Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance | BindingFlags.OptionalParamBinding,
            null, null, null, null);
    }
}