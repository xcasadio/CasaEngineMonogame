using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CasaEngine.Tests.Scene;

public class EntityPolicyResolverTests
{
    [Fact]
    public void GetEffectivePolicySet_StaticModelDefaultsToStaticNeverStaticIndex()
    {
        var entity = new Entity
        {
            RootComponent = new StaticModelComponent(),
        };

        EntityPolicySet policySet = entity.GetEffectivePolicySet();

        Assert.Equal(Mobility.Static, policySet.Mobility);
        Assert.Equal(TickPolicy.Never, policySet.TickPolicy);
        Assert.Equal(SpatialPolicy.StaticIndex, policySet.SpatialPolicy);
        Assert.Equal(RenderDynamicPolicy.Static, policySet.RenderDynamicPolicy);
    }

    [Fact]
    public void GetResolvedPolicies_ConditionalComponentSignalsRuntimeUpdatesExplicitly()
    {
        var entity = new Entity();
        var component = new ConditionalTestComponent();
        entity.AddComponent(component);

        ResolvedEntityPolicies inactivePolicies = entity.GetResolvedPolicies();
        Assert.Equal(TickPolicy.Conditional, inactivePolicies.PolicySet.TickPolicy);
        Assert.False(inactivePolicies.ShouldUpdateThisFrame);

        component.IsActive = true;

        ResolvedEntityPolicies activePolicies = entity.GetResolvedPolicies();
        Assert.True(activePolicies.ShouldUpdateThisFrame);
        Assert.Equal(RenderDynamicPolicy.MaterialAnimated, activePolicies.PolicySet.RenderDynamicPolicy);
    }

    [Fact]
    public void GetEffectivePolicySet_StaticModelWithCharacterControllerDefaultsToDynamicEveryFrame()
    {
        var entity = new Entity
        {
            RootComponent = new StaticModelComponent(),
        };
        entity.AddComponent(new CharacterControllerComponent());

        EntityPolicySet policySet = entity.GetEffectivePolicySet();

        Assert.Equal(Mobility.Movable, policySet.Mobility);
        Assert.Equal(TickPolicy.EveryFrame, policySet.TickPolicy);
        Assert.Equal(SpatialPolicy.DynamicIndex, policySet.SpatialPolicy);
        Assert.Equal(RenderDynamicPolicy.GeometryAnimated, policySet.RenderDynamicPolicy);
    }

    [Fact]
    public void GetResolvedPolicies_CharacterControllerRequiresOnDemandViewInvalidation()
    {
        var entity = new Entity
        {
            RootComponent = new StaticModelComponent(),
        };
        entity.AddComponent(new CharacterControllerComponent());

        ResolvedEntityPolicies resolvedPolicies = entity.GetResolvedPolicies();

        Assert.True(resolvedPolicies.ShouldUpdateThisFrame);
        Assert.True(resolvedPolicies.RequiresOnDemandViewInvalidation);
        Assert.Equal(RenderDynamicPolicy.GeometryAnimated, resolvedPolicies.PolicySet.RenderDynamicPolicy);
    }

    [Fact]
    public void GetResolvedPolicies_LegacyUpdatesEnabledShimOverridesTickDecision()
    {
        var entity = new Entity
        {
            RootComponent = new StaticModelComponent(),
        };
        entity.ApplyExplicitPolicies(EntityPolicySet.DynamicDefault);

#pragma warning disable CS0618
        entity.UpdatesEnabled = false;
#pragma warning restore CS0618

        ResolvedEntityPolicies resolvedPolicies = entity.GetResolvedPolicies();

        Assert.Equal(TickPolicy.Never, resolvedPolicies.PolicySet.TickPolicy);
        Assert.False(resolvedPolicies.ShouldUpdateThisFrame);
        Assert.False(entity.Policies.LegacyUpdatesEnabledOverride.GetValueOrDefault(true));
    }

    [Fact]
    public void Load_WithPolicyFields_RestoresEntityAuthoringPolicies()
    {
        var entity = new Entity();
        var document = new JObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Policy Entity",
            ["policy_source"] = EntityPolicySourceMode.Explicit.ToString(),
            ["mobility"] = Mobility.Static.ToString(),
            ["tick_policy"] = TickPolicy.Never.ToString(),
            ["spatial_policy"] = SpatialPolicy.StaticIndex.ToString(),
            ["render_dynamic_policy"] = RenderDynamicPolicy.MaterialAnimated.ToString(),
            ["script_class_name"] = string.Empty,
            ["root_component"] = "null",
            ["components"] = new JArray(),
        };

        entity.Load(document);

        Assert.Equal(EntityPolicySourceMode.Explicit, entity.PolicySourceMode);
        Assert.Equal(Mobility.Static, entity.Mobility);
        Assert.Equal(TickPolicy.Never, entity.TickPolicy);
        Assert.Equal(SpatialPolicy.StaticIndex, entity.SpatialPolicy);
        Assert.Equal(RenderDynamicPolicy.MaterialAnimated, entity.RenderDynamicPolicy);
        Assert.Equal(EntityPolicySourceMode.Explicit, entity.Policies.PolicySourceMode);
        Assert.Equal(Mobility.Static, entity.Policies.Mobility);
        Assert.Equal(TickPolicy.Never, entity.Policies.TickPolicy);
        Assert.Equal(SpatialPolicy.StaticIndex, entity.Policies.SpatialPolicy);
        Assert.Equal(RenderDynamicPolicy.MaterialAnimated, entity.Policies.RenderDynamicPolicy);
    }

    [Fact]
    public void EntityPolicyState_CloneCopiesAuthoringButNotTransientConditionalState()
    {
        var state = new EntityPolicyState
        {
            PolicySourceMode = EntityPolicySourceMode.Explicit,
            Mobility = Mobility.Static,
            TickPolicy = TickPolicy.Conditional,
            SpatialPolicy = SpatialPolicy.StaticIndex,
            RenderDynamicPolicy = RenderDynamicPolicy.MaterialAnimated,
        };
        state.SetLegacyUpdatesEnabledOverride(false);
        state.RequestConditionalUpdate();

        EntityPolicyState clone = state.Clone();
        clone.Mobility = Mobility.Movable;

        Assert.Equal(EntityPolicySourceMode.Explicit, clone.PolicySourceMode);
        Assert.Equal(TickPolicy.Conditional, clone.TickPolicy);
        Assert.Equal(SpatialPolicy.StaticIndex, clone.SpatialPolicy);
        Assert.Equal(RenderDynamicPolicy.MaterialAnimated, clone.RenderDynamicPolicy);
        Assert.False(clone.HasPendingConditionalUpdateRequest);
        Assert.False(clone.LegacyUpdatesEnabledOverride.GetValueOrDefault(true));
        Assert.Equal(Mobility.Static, state.Mobility);
    }

    [Fact]
    public void EntityPolicyState_ClearConditionalRequestStopsConditionalUpdate()
    {
        var entity = new Entity();
        entity.ApplyExplicitPolicies(EntityPolicySet.StaticMaterialAnimated);
        entity.RequestConditionalUpdate();

        Assert.True(entity.GetResolvedPolicies().ShouldUpdateThisFrame);

        entity.Policies.ClearConditionalUpdateRequest();

        Assert.False(entity.GetResolvedPolicies().ShouldUpdateThisFrame);
    }

    private sealed class ConditionalTestComponent : EntityComponent, IConditionalEntityUpdateSource, IEntityPolicyDefaultsProvider
    {
        public bool IsActive { get; set; }

        public override EntityComponent Clone()
        {
            return new ConditionalTestComponent
            {
                IsActive = IsActive,
            };
        }

        public bool ShouldUpdateWhenConditional(Entity owner)
        {
            return IsActive;
        }

        public void ApplyEntityPolicyDefaults(Entity owner, ref EntityPolicyDefaultsBuilder defaults)
        {
            defaults.Apply(EntityPolicySet.StaticMaterialAnimated);
        }
    }
}