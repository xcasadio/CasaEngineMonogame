using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using System.Collections.Generic;

namespace CasaEngine.EditorUI.DragAndDrop.Handlers;

/// <summary>
/// Handles drop of <c>.entity</c> assets: loads the entity from the asset catalog
/// using <see cref="EntityReference"/> and returns a clone ready for placement.
/// </summary>
public class EntityAssetDropHandler : IAssetDropHandler
{
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { Constants.FileNameExtensions.Entity };

    public bool CanHandle(AssetInfo assetInfo) => true;

    public Entity CreateEntity(AssetInfo assetInfo, CasaEngineGame game)
    {
        var entityReference = EntityReference.CreateFromAssetInfo(assetInfo, game.AssetContentManager);
        return entityReference.Entity;
    }
}
