using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Application;

/// <summary>
/// Configures the render views that should exist for a newly loaded runtime world.
/// </summary>
public interface IRuntimeViewBootstrapper
{
    void BootstrapViews(CasaEngineGame game, Scene.World.World world, ViewManager viewManager);
}