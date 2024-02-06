using System;
using System.Collections.ObjectModel;
using CasaEngine.Framework.SceneManagement.Components;

namespace CasaEngine.EditorUI.Controls.EntityControls.ViewModels;

public class Animation2dSelectedListModelView
{
    private readonly AnimatedSpriteComponent _animatedSpriteComponent;

    public ObservableCollection<AssetInfoViewModel> Animations { get; } = new();

    public Animation2dSelectedListModelView(AnimatedSpriteComponent animatedSpriteComponent)
    {
        _animatedSpriteComponent = animatedSpriteComponent;

        foreach (var animationAssetId in _animatedSpriteComponent.AnimationAssetIds)
        {
            Animations.Add(new AssetInfoViewModel(animationAssetId));
        }
    }

    public void Add(Guid id)
    {
        Animations.Add(new AssetInfoViewModel(id));
        _animatedSpriteComponent.AnimationAssetIds.Add(id);
    }

    public void Remove(AssetInfoViewModel assetInfoViewModel)
    {
        Animations.Remove(assetInfoViewModel);
        _animatedSpriteComponent.AnimationAssetIds.Remove(assetInfoViewModel.Id);
    }
}