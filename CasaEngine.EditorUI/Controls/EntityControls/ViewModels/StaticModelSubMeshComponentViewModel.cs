using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class StaticModelSubMeshComponentViewModel : SceneComponentViewModel
{
    private readonly StaticModelSubMeshComponent _subMesh;

    public string MeshName => _subMesh.ModelMesh?.Name ?? string.Empty;
    public int VertexCount => _subMesh.ModelMesh?.VertexBuffer?.VertexCount ?? 0;
    public int IndexCount => _subMesh.ModelMesh?.IndexBuffer?.IndexCount ?? 0;

    public StaticModelSubMeshComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _subMesh = (StaticModelSubMeshComponent)entityComponent;
    }
}
