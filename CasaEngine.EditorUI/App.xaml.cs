using System.Windows;
using CasaEngine.EditorUI.DragAndDrop;
using CasaEngine.EditorUI.DragAndDrop.Handlers;

namespace CasaEngine.EditorUI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Must be set before the first D3D11Host (GameEditor) control is created.
        // All editor viewports will share a single GraphicsDevice, which is the
        // prerequisite for sharing GPU resources (textures, models, render targets)
        // across editor tabs — the foundation for PR3+ of the multi-view migration.
        D3D11Host.UseASingleSharedGraphicsDevice = true;

        // Register drag & drop asset handlers
        var registry = AssetDropHandlerRegistry.Instance;
        registry.Register(new EntityAssetDropHandler());
        registry.Register(new StaticModelAssetDropHandler());
        registry.Register(new SpriteAssetDropHandler());
        registry.Register(new Animation2dAssetDropHandler());

        // Register toolbox drop handlers
        var toolboxRegistry = ToolboxDropHandlerRegistry.Instance;
        toolboxRegistry.Register(new EmptyEntityToolboxHandler());
        toolboxRegistry.Register(new PlayerStartToolboxHandler());

        base.OnStartup(e);
    }
}