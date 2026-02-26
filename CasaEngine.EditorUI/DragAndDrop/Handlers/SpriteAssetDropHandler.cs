using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using System.Collections.Generic;
using System.IO;

namespace CasaEngine.EditorUI.DragAndDrop.Handlers;

/// <summary>
/// Handles drop of <c>.sprite</c> assets: creates an <see cref="Entity"/>
/// with a root <see cref="StaticSpriteComponent"/> referencing the asset.
/// The sprite data is loaded automatically when the entity is added to the world.
/// </summary>
public class SpriteAssetDropHandler : IAssetDropHandler
{
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { Constants.FileNameExtensions.Sprite };

    public bool CanHandle(AssetInfo assetInfo) => true;

    public Entity CreateEntity(AssetInfo assetInfo, CasaEngineGame game)
    {
        var entity = new Entity
        {
            Name = Path.GetFileNameWithoutExtension(assetInfo.FileName)
        };
        var spriteComponent = new StaticSpriteComponent
        {
            SpriteAssetId = assetInfo.Id
        };
        entity.RootComponent = spriteComponent;
        return entity;
    }
}
