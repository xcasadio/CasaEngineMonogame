using System;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Materials;

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

    /// <summary>
    /// Asset ID of the material assigned to this sub-mesh.
    /// Setting this property loads the new material from the content manager and
    /// rebuilds <see cref="MaterialVM"/>.
    /// </summary>
    public Guid MaterialAssetId
    {
        get => _subMesh.ModelMesh?.MaterialAssetId ?? Guid.Empty;
        set
        {
            if (_subMesh.ModelMesh == null || _subMesh.ModelMesh.MaterialAssetId == value) return;

            _subMesh.ModelMesh.MaterialAssetId = value;

            var contentManager = _subMesh.Owner?.World?.Game?.AssetContentManager;
            if (contentManager != null && value != Guid.Empty)
            {
                var newMaterial = contentManager.Load<MaterialBase>(value);
                _subMesh.ModelMesh.Material = newMaterial;
            }
            else
            {
                _subMesh.ModelMesh.Material = null;
            }

            // Rebuild the material ViewModel
            var mat = _subMesh.ModelMesh.Material;
            var newVm = MaterialViewModelFactory.Create(mat);
            if (newVm != null)
            {
                newVm.ContentManager = _subMesh.Owner?.World?.Game?.AssetContentManager;
            }

            MaterialVM = newVm;
            OnPropertyChanged();
        }
    }

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
