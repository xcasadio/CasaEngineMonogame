using CasaEngine.Framework.Rendering;

namespace CasaEngine.Framework.Game;

/// <summary>
/// Configures the render views that should exist for a newly loaded runtime world.
/// </summary>
public interface IRuntimeViewBootstrapper
{
    void BootstrapViews(CasaEngineGame game, World.World world, ViewManager viewManager);
}