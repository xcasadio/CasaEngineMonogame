using CasaEngine.EditorUI.DragAndDrop.Handlers;

namespace CasaEngine.EditorUI.DragAndDrop;

/// <summary>
/// Single entry-point for registering all drag-and-drop handlers at application startup.
/// Call <see cref="RegisterAllHandlers"/> once from <c>App.OnStartup</c>.
/// </summary>
public static class DragAndDropConfiguration
{
    /// <summary>
    /// Registers all asset and toolbox drop handlers into their respective registries.
    /// </summary>
    public static void RegisterAllHandlers()
    {
        // --- Asset handlers (from ContentBrowser drag source) ---
        var assetRegistry = AssetDropHandlerRegistry.Instance;
        assetRegistry.Register(new EntityAssetDropHandler());
        assetRegistry.Register(new StaticModelAssetDropHandler());
        assetRegistry.Register(new SpriteAssetDropHandler());
        assetRegistry.Register(new Animation2dAssetDropHandler());

        // --- Toolbox handlers (from editor toolbox panel) ---
        var toolboxRegistry = ToolboxDropHandlerRegistry.Instance;
        toolboxRegistry.Register(new EmptyEntityToolboxHandler());
        toolboxRegistry.Register(new PlayerStartToolboxHandler());
    }
}
