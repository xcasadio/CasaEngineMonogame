using System;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class StaticModelSubMeshComponentViewModel : SceneComponentViewModel
{
    private readonly StaticModelSubMeshComponent _subMesh;
    private MaterialViewModel? _materialVM;

    public string MeshName  => _subMesh.ModelMesh?.Name ?? string.Empty;
    public int VertexCount  => _subMesh.ModelMesh?.VertexBuffer?.VertexCount ?? 0;
    public int IndexCount   => _subMesh.ModelMesh?.IndexBuffer?.IndexCount ?? 0;

    /// <summary>Display name of the concrete material type, or "None".</summary>
    public string MaterialTypeName => _subMesh.ModelMesh?.Material?.GetType().Name ?? "None";

    /// <summary>Asset ID of the material assigned to this sub-mesh.</summary>
    public Guid MaterialAssetId => _subMesh.ModelMesh?.MaterialAssetId ?? Guid.Empty;

    /// <summary>
    /// Type-specific ViewModel for the material assigned to this sub-mesh.
    /// Null when no material is assigned.
    /// </summary>
    public MaterialViewModel? MaterialVM
    {
        get => _materialVM;
        private set { _materialVM = value; OnPropertyChanged(); OnPropertyChanged(nameof(MaterialTypeName)); }
    }

    public StaticModelSubMeshComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _subMesh = (StaticModelSubMeshComponent)entityComponent;

        var mat = _subMesh.ModelMesh?.Material;
        if (mat != null)
        {
            _materialVM = MaterialViewModelFactory.Create(mat);
            if (_materialVM != null)
            {
                // Provide the content manager so the VM can reload textures after saving.
                _materialVM.ContentManager = _subMesh.Owner?.World?.Game?.AssetContentManager;
            }
        }
    }
}
