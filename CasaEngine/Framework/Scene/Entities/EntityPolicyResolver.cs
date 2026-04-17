using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities.Components;

namespace CasaEngine.Framework.Scene.Entities;

public static class EntityPolicyResolver
{
    public static EntityPolicySet ResolveConfiguredPolicySet(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        EntityPolicyState policies = entity.Policies;
        EntityPolicySet policySet = policies.PolicySourceMode == EntityPolicySourceMode.Explicit
            ? policies.GetAuthoringPolicySet()
            : ResolveEngineDefaultPolicySet(entity);

        if (policies.LegacyUpdatesEnabledOverride.HasValue)
        {
            policySet = policySet with
            {
                TickPolicy = policies.LegacyUpdatesEnabledOverride.Value
                    ? TickPolicy.EveryFrame
                    : TickPolicy.Never,
            };
        }

        return policySet;
    }

    public static ResolvedEntityPolicies ResolveRuntimePolicies(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        EntityPolicyState policies = entity.Policies;
        EntityPolicySet policySet = policies.GetConfiguredPolicySet(entity);
        bool shouldUpdateThisFrame = policySet.TickPolicy switch
        {
            TickPolicy.Never => false,
            TickPolicy.EveryFrame => true,
            TickPolicy.Conditional => HasConditionalUpdateRequest(entity),
            _ => true,
        };

        return new ResolvedEntityPolicies(
            policySet,
            policies.PolicySourceMode,
            shouldUpdateThisFrame,
            policySet.SpatialPolicy == SpatialPolicy.DynamicIndex,
            policySet.RenderDynamicPolicy != RenderDynamicPolicy.Static);
    }

    public static string? GetSuspectCombinationReason(EntityPolicySet policySet)
    {
        if (policySet.Mobility == Mobility.Static && policySet.SpatialPolicy == SpatialPolicy.DynamicIndex)
        {
            return "Static mobility with DynamicIndex spatial maintenance.";
        }

        if (policySet.TickPolicy == TickPolicy.Never && policySet.SpatialPolicy == SpatialPolicy.DynamicIndex)
        {
            return "TickPolicy.Never with DynamicIndex spatial maintenance.";
        }

        if (policySet.TickPolicy == TickPolicy.Never && policySet.RenderDynamicPolicy == RenderDynamicPolicy.GeometryAnimated)
        {
            return "TickPolicy.Never with GeometryAnimated render policy.";
        }

        return null;
    }

    private static EntityPolicySet ResolveEngineDefaultPolicySet(Entity entity)
    {
        var defaults = new EntityPolicyDefaultsBuilder();

        if (entity.RootComponent != null)
        {
            AccumulateDefaults(entity, entity.RootComponent, ref defaults);
        }

        for (int index = 0; index < entity.AttachedComponents.Count; index++)
        {
            AccumulateDefaults(entity, entity.AttachedComponents[index], ref defaults);
        }

        return defaults.Build(EntityPolicySet.DynamicDefault);
    }

    private static bool HasConditionalUpdateRequest(Entity entity)
    {
        if (entity.Policies.HasPendingConditionalUpdateRequest)
        {
            return true;
        }

        if (entity.RootComponent != null && ComponentRequestsConditionalUpdate(entity.RootComponent, entity))
        {
            return true;
        }

        for (int index = 0; index < entity.AttachedComponents.Count; index++)
        {
            if (ComponentRequestsConditionalUpdate(entity.AttachedComponents[index], entity))
            {
                return true;
            }
        }

        return entity.GameplayProxy is IConditionalEntityUpdateSource gameplayProxySource
            && gameplayProxySource.ShouldUpdateWhenConditional(entity);
    }

    private static void AccumulateDefaults(Entity owner, EntityComponent component, ref EntityPolicyDefaultsBuilder defaults)
    {
        switch (component)
        {
            case StaticModelComponent:
            case PlayerStartComponent:
                defaults.Apply(EntityPolicySet.StaticDecoration);
                break;

            case AnimatedSpriteComponent:
            case TileMapComponent:
                defaults.Apply(EntityPolicySet.StaticMaterialAnimated);
                break;

            case SkinnedMeshComponent:
                defaults.Apply(EntityPolicySet.DynamicGeometryAnimated);
                break;

            case PhysicsBaseComponent:
            case CameraLookAtComponent:
            case CameraTargeted2dComponent:
            case SteeringAgentComponent:
            case SteeringPhysicsBridgeComponent:
            case StaticSpriteComponent:
                defaults.Apply(EntityPolicySet.DynamicDefault);
                break;
        }

        if (component is IEntityPolicyDefaultsProvider defaultsProvider)
        {
            defaultsProvider.ApplyEntityPolicyDefaults(owner, ref defaults);
        }

        if (component is not SceneComponent sceneComponent)
        {
            return;
        }

        for (int index = 0; index < sceneComponent.Children.Count; index++)
        {
            AccumulateDefaults(owner, sceneComponent.Children[index], ref defaults);
        }
    }

    private static bool ComponentRequestsConditionalUpdate(EntityComponent component, Entity owner)
    {
        if (component is IConditionalEntityUpdateSource conditionalUpdateSource
            && conditionalUpdateSource.ShouldUpdateWhenConditional(owner))
        {
            return true;
        }

        if (component is not SceneComponent sceneComponent)
        {
            return false;
        }

        for (int index = 0; index < sceneComponent.Children.Count; index++)
        {
            if (ComponentRequestsConditionalUpdate(sceneComponent.Children[index], owner))
            {
                return true;
            }
        }

        return false;
    }
}