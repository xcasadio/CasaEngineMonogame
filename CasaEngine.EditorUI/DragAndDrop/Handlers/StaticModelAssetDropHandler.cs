using CasaEngine.Framework;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Entities.Components;
using CasaEngine.Framework.Game;
using System.Collections.Generic;
using System.IO;

namespace CasaEngine.EditorUI.DragAndDrop.Handlers;

/// <summary>
/// Handles drop of <c>.staticModel</c> assets: creates an <see cref="Entity"/>
/// with a root <see cref="StaticModelComponent"/> referencing the asset.
/// </summary>
public class StaticModelAssetDropHandler : IAssetDropHandler
{
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { Constants.FileNameExtensions.StaticModel };

    public bool CanHandle(AssetInfo assetInfo) => true;

    public Entity CreateEntity(AssetInfo assetInfo, CasaEngineGame game)
    {
        var entity = new Entity
        {
            Name = Path.GetFileNameWithoutExtension(assetInfo.FileName)
        };
        var staticModelComponent = new StaticModelComponent
        {
            StaticModelAssetId = assetInfo.Id
        };
        entity.RootComponent = staticModelComponent;
        return entity;
    }
}
