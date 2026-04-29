using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Rendering.Environment;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Collects runtime light data from the current world without exposing concrete authoring components to the render pipeline.
/// </summary>
public static class WorldLightCollector
{
    /// <summary>Collects lights with a neutral local-light priority origin.</summary>
    public static void Collect(Scene.World.World world, LightingContext lightingContext, in ResolvedEnvironmentSettings environment)
        => Collect(world, lightingContext, in environment, Vector3.Zero);

    /// <summary>Collects lights and ranks local lights relative to the current view position.</summary>
    public static void Collect(Scene.World.World world, LightingContext lightingContext, in ResolvedEnvironmentSettings environment, Vector3 priorityPosition)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(lightingContext);

        lightingContext.BeginCollection(priorityPosition, environment.EffectiveAmbientColor);

        var entities = world.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            CollectFromEntity(entities[i], lightingContext);
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

        var components = entity.ComponentList;
        for (int i = 0; i < components.Count; i++)
        {
            var component = components[i];
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

        var children = entity.ChildList;
        for (int i = 0; i < children.Count; i++)
        {
            CollectFromEntity(children[i], lightingContext);
        }
    }

    private static void CollectFromSceneComponent(SceneComponent sceneComponent, LightingContext lightingContext)
    {
        if (sceneComponent is IRenderLightSource lightSource)
        {
            lightSource.AppendLights(lightingContext);
        }

        var children = sceneComponent.Children;
        for (int i = 0; i < children.Count; i++)
        {
            CollectFromSceneComponent(children[i], lightingContext);
        }
    }
}