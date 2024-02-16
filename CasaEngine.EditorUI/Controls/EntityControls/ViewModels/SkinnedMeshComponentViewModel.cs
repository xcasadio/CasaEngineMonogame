using System;
using System.Collections.ObjectModel;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class SkinnedMeshComponentViewModel : NotifyPropertyChangeBase
{
    private readonly SkinnedMeshComponent _skinnedMeshComponent;

    public ObservableCollection<SkeletonAnimationViewModel> Animations { get; } = new();

    public int NumberOfBones => _skinnedMeshComponent?.SkinnedMesh?.RiggedModel?.FlatListToBoneNodes.Count ?? 0;

    public Guid RiggedModelAssetId
    {
        get => _skinnedMeshComponent.SkinnedMesh?.RiggedModelAssetId ?? Guid.Empty;
        set
        {
            if (_skinnedMeshComponent.SkinnedMesh != null && _skinnedMeshComponent.SkinnedMesh?.RiggedModelAssetId != value)
            {
                _skinnedMeshComponent.SkinnedMeshAssetId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NumberOfBones));
                UpdateAnimations();
            }
        }
    }

    public SkinnedMeshComponentViewModel(SkinnedMeshComponent skinnedMeshComponent)
    {
        _skinnedMeshComponent = skinnedMeshComponent;
        UpdateAnimations();
    }

    public void UpdateAnimations()
    {
        if (_skinnedMeshComponent.SkinnedMesh?.RiggedModel != null)
        {
            foreach (var riggedAnimation in _skinnedMeshComponent.SkinnedMesh?.RiggedModel?.OriginalAnimations)
            {
                Animations.Add(new SkeletonAnimationViewModel(riggedAnimation));
            }
        }
    }
}