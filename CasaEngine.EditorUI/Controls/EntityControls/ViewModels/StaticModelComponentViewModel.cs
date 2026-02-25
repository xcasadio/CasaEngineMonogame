using System;
using CasaEngine.Framework.Entities.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class StaticModelComponentViewModel : SceneComponentViewModel
{
    private readonly StaticModelComponent _staticModelComponent;

    public int MeshCount => _staticModelComponent.StaticModel?.Meshes.Count ?? 0;

    public Guid StaticModelAssetId
    {
        get => _staticModelComponent.StaticModelAssetId;
        set
        {
            if (_staticModelComponent.StaticModelAssetId != value)
            {
                _staticModelComponent.StaticModelAssetId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MeshCount));
            }
        }
    }

    public StaticModelComponentViewModel(EntityComponent entityComponent) : base(entityComponent)
    {
        _staticModelComponent = (StaticModelComponent)entityComponent;
    }
}
