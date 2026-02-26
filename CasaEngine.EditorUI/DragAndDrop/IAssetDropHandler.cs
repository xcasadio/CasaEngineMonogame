using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Entities;
using CasaEngine.Framework.Game;
using System.Collections.Generic;

namespace CasaEngine.EditorUI.DragAndDrop;

/// <summary>
/// Defines a handler that knows how to create a configured <see cref="Entity"/>
/// from a drag-and-dropped <see cref="AssetInfo"/> of a specific type.
/// </summary>
public interface IAssetDropHandler
{
    /// <summary>
    /// File extensions supported by this handler (e.g. ".staticModel", ".entity").
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Returns true if this handler can process the given asset.
    /// Called after the extension matches, for fine-grained validation.
    /// </summary>
    bool CanHandle(AssetInfo assetInfo);

    /// <summary>
    /// Creates and returns a fully configured <see cref="Entity"/> from the asset.
    /// The entity is NOT added to the world — the calling control is responsible for that.
    /// </summary>
    Entity CreateEntity(AssetInfo assetInfo, CasaEngineGame game);
}
