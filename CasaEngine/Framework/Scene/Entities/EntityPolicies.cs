namespace CasaEngine.Framework.Scene.Entities;

public enum Mobility
{
    Static = 0,
    Movable = 1,
}

public enum TickPolicy
{
    Never = 0,
    Conditional = 1,
    EveryFrame = 2,
}

public enum SpatialPolicy
{
    StaticIndex = 0,
    DynamicIndex = 1,
}

public enum RenderDynamicPolicy
{
    Static = 0,
    MaterialAnimated = 1,
    GeometryAnimated = 2,
}

public enum EntityPolicySourceMode
{
    EngineDefault = 0,
    Explicit = 1,
}

public readonly record struct EntityPolicySet(
    Mobility Mobility,
    TickPolicy TickPolicy,
    SpatialPolicy SpatialPolicy,
    RenderDynamicPolicy RenderDynamicPolicy)
{
    public static EntityPolicySet DynamicDefault => new(
        Mobility.Movable,
        TickPolicy.EveryFrame,
        SpatialPolicy.DynamicIndex,
        RenderDynamicPolicy.Static);

    public static EntityPolicySet StaticDecoration => new(
        Mobility.Static,
        TickPolicy.Never,
        SpatialPolicy.StaticIndex,
        RenderDynamicPolicy.Static);

    public static EntityPolicySet StaticMaterialAnimated => new(
        Mobility.Static,
        TickPolicy.Conditional,
        SpatialPolicy.StaticIndex,
        RenderDynamicPolicy.MaterialAnimated);

    public static EntityPolicySet DynamicGeometryAnimated => new(
        Mobility.Movable,
        TickPolicy.EveryFrame,
        SpatialPolicy.DynamicIndex,
        RenderDynamicPolicy.GeometryAnimated);

    public static EntityPolicySet DynamicTransformAnimated => new(
        Mobility.Movable,
        TickPolicy.EveryFrame,
        SpatialPolicy.DynamicIndex,
        RenderDynamicPolicy.GeometryAnimated);
}

public readonly record struct ResolvedEntityPolicies(
    EntityPolicySet PolicySet,
    EntityPolicySourceMode SourceMode,
    bool ShouldUpdateThisFrame,
    bool UsesDynamicSpatialMaintenance,
    bool RequiresOnDemandViewInvalidation);

public readonly record struct EntityPolicyDiagnosticsSnapshot(
    int TotalEntities,
    int ExplicitPolicyEntities,
    int DefaultPolicyEntities,
    int UpdatedEntities,
    int MobilityStaticEntities,
    int MobilityMovableEntities,
    int TickNeverEntities,
    int TickConditionalEntities,
    int TickEveryFrameEntities,
    int SpatialStaticEntities,
    int SpatialDynamicEntities,
    int RenderStaticEntities,
    int RenderMaterialAnimatedEntities,
    int RenderGeometryAnimatedEntities,
    int DynamicRenderEntities,
    int SuspectCombinationCount)
{
    public static EntityPolicyDiagnosticsSnapshot Empty => default;
}

public struct EntityPolicyDefaultsBuilder
{
    private bool _hasValues;
    private Mobility _mobility;
    private TickPolicy _tickPolicy;
    private SpatialPolicy _spatialPolicy;
    private RenderDynamicPolicy _renderDynamicPolicy;

    public bool HasValues => _hasValues;

    public void Apply(EntityPolicySet policySet)
    {
        if (!_hasValues)
        {
            _mobility = policySet.Mobility;
            _tickPolicy = policySet.TickPolicy;
            _spatialPolicy = policySet.SpatialPolicy;
            _renderDynamicPolicy = policySet.RenderDynamicPolicy;
            _hasValues = true;
            return;
        }

        _mobility = _mobility == Mobility.Movable || policySet.Mobility == Mobility.Movable
            ? Mobility.Movable
            : Mobility.Static;
        _tickPolicy = (TickPolicy)Math.Max((int)_tickPolicy, (int)policySet.TickPolicy);
        _spatialPolicy = _spatialPolicy == SpatialPolicy.DynamicIndex || policySet.SpatialPolicy == SpatialPolicy.DynamicIndex
            ? SpatialPolicy.DynamicIndex
            : SpatialPolicy.StaticIndex;
        _renderDynamicPolicy = (RenderDynamicPolicy)Math.Max((int)_renderDynamicPolicy, (int)policySet.RenderDynamicPolicy);
    }

    public EntityPolicySet Build(EntityPolicySet fallback)
    {
        return _hasValues
            ? new EntityPolicySet(_mobility, _tickPolicy, _spatialPolicy, _renderDynamicPolicy)
            : fallback;
    }
}

public interface IConditionalEntityUpdateSource
{
    bool ShouldUpdateWhenConditional(Entity owner);
}

public interface IEntityPolicyDefaultsProvider
{
    void ApplyEntityPolicyDefaults(Entity owner, ref EntityPolicyDefaultsBuilder defaults);
}