using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering.Environment;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Collects runtime light data from the current world without exposing concrete authoring components to the render pipeline.
/// </summary>
public static class WorldLightCollector
{
    public static void Collect(Scene.World.World world, LightingContext lightingContext, in ResolvedEnvironmentSettings environment)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(lightingContext);

        lightingContext.ClearLights();
        lightingContext.AmbientColor = environment.EffectiveAmbientColor;

        foreach (var entity in world.Entities)
        {
            CollectFromEntity(entity, lightingContext);
        }
    }

    private static void CollectFromEntity(Entity entity, LightingContext lightingContext)
    {
        if (!entity.IsEnabled || !entity.IsVisible)
        {
            return;
        }

        if (entity.RootComponent is SceneComponent rootComponent)
        {
            CollectFromSceneComponent(rootComponent, lightingContext);
        }

        foreach (var component in entity.Components)
        {
            if (component is SceneComponent sceneComponent)
            {
                CollectFromSceneComponent(sceneComponent, lightingContext);
                continue;
            }

            if (component is IRenderLightSource lightSource)
            {
                lightSource.AppendLights(lightingContext);
            }
        }

        foreach (var child in entity.Children)
        {
            CollectFromEntity(child, lightingContext);
        }
    }

    private static void CollectFromSceneComponent(SceneComponent sceneComponent, LightingContext lightingContext)
    {
        if (sceneComponent is IRenderLightSource lightSource)
        {
            lightSource.AppendLights(lightingContext);
        }

        foreach (var child in sceneComponent.Children)
        {
            CollectFromSceneComponent(child, lightingContext);
        }
    }
}