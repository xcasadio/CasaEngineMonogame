using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using System.Collections.Generic;
using System.IO;

namespace CasaEngine.EditorUI.DragAndDrop.Handlers;

/// <summary>
/// Handles drop of <c>.anim2d</c> assets: creates an <see cref="Entity"/>
/// with a root <see cref="AnimatedSpriteComponent"/> pre-loaded with the animation.
/// The animation is loaded from the asset ID during <c>InitializeWithWorld</c>.
/// </summary>
public class Animation2dAssetDropHandler : IAssetDropHandler
{
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { Constants.FileNameExtensions.Animation2d };

    public bool CanHandle(AssetInfo assetInfo) => true;

    public Entity CreateEntity(AssetInfo assetInfo, CasaEngineGame game)
    {
        var entity = new Entity
        {
            Name = Path.GetFileNameWithoutExtension(assetInfo.FileName)
        };
        var animComponent = new AnimatedSpriteComponent();
        animComponent.AnimationAssetIds.Add(assetInfo.Id);
        entity.RootComponent = animComponent;
        return entity;
    }
}
