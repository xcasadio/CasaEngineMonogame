using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Application;

/// <summary>
/// Creates the default full-screen backbuffer view used by the runtime when no custom views were registered.
/// </summary>
public sealed class DefaultRuntimeViewBootstrapper : IRuntimeViewBootstrapper
{
    public static DefaultRuntimeViewBootstrapper Instance { get; } = new();

    private DefaultRuntimeViewBootstrapper()
    {
    }

    public void BootstrapViews(CasaEngineGame game, CasaEngine.Framework.Scene.World.World world, ViewManager viewManager)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(viewManager);

        if (viewManager.Views.Count > 0)
        {
            return;
        }

        var camera = world.Entities.Select(static entity => entity.GetComponent<CameraComponent>())
            .FirstOrDefault(static cameraComponent => cameraComponent != null);

        if (camera == null)
        {
            camera = world.CreateDefaultCamera();
        }

        var fullScreen = new Rectangle(0, 0, game.ScreenSizeWidth, game.ScreenSizeHeight);
        viewManager.CreateView(new ViewDefinition
        {
            World = world,
            Camera = camera,
            Surface = new BackBufferSurface(fullScreen),
            Name = "Default view",
            ClearColor = Color.CornflowerBlue,
        });
    }
}