namespace CasaEngine.Framework.Scene.Entities;

public sealed class EntityPolicyState
{
    private EntityPolicySourceMode _policySourceMode = EntityPolicySourceMode.EngineDefault;
    private Mobility _mobility = Mobility.Movable;
    private TickPolicy _tickPolicy = TickPolicy.EveryFrame;
    private SpatialPolicy _spatialPolicy = SpatialPolicy.DynamicIndex;
    private RenderDynamicPolicy _renderDynamicPolicy = RenderDynamicPolicy.Static;
    private bool? _legacyUpdatesEnabledOverride;
    private bool _conditionalUpdateRequested;
    private bool _configuredPolicySetDirty = true;
    private EntityPolicySet _configuredPolicySet = EntityPolicySet.DynamicDefault;

    public EntityPolicySourceMode PolicySourceMode
    {
        get => _policySourceMode;
        set
        {
            if (_policySourceMode == value)
            {
                return;
            }

            _policySourceMode = value;
            InvalidateConfiguredPolicySet();
        }
    }

    public Mobility Mobility
    {
        get => _mobility;
        set
        {
            if (_mobility == value)
            {
                return;
            }

            _mobility = value;
            InvalidateConfiguredPolicySet();
        }
    }

    public TickPolicy TickPolicy
    {
        get => _tickPolicy;
        set
        {
            if (_tickPolicy == value)
            {
                return;
            }

            _tickPolicy = value;
            InvalidateConfiguredPolicySet();
        }
    }

    public SpatialPolicy SpatialPolicy
    {
        get => _spatialPolicy;
        set
        {
            if (_spatialPolicy == value)
            {
                return;
            }

            _spatialPolicy = value;
            InvalidateConfiguredPolicySet();
        }
    }

    public RenderDynamicPolicy RenderDynamicPolicy
    {
        get => _renderDynamicPolicy;
        set
        {
            if (_renderDynamicPolicy == value)
            {
                return;
            }

            _renderDynamicPolicy = value;
            InvalidateConfiguredPolicySet();
        }
    }

    internal bool? LegacyUpdatesEnabledOverride => _legacyUpdatesEnabledOverride;

    internal bool HasPendingConditionalUpdateRequest => _conditionalUpdateRequested;

    internal EntityPolicySet GetAuthoringPolicySet()
    {
        return new EntityPolicySet(_mobility, _tickPolicy, _spatialPolicy, _renderDynamicPolicy);
    }

    public void ApplyExplicitPolicies(EntityPolicySet policySet)
    {
        bool changed = _policySourceMode != EntityPolicySourceMode.Explicit
            || _mobility != policySet.Mobility
            || _tickPolicy != policySet.TickPolicy
            || _spatialPolicy != policySet.SpatialPolicy
            || _renderDynamicPolicy != policySet.RenderDynamicPolicy;

        _policySourceMode = EntityPolicySourceMode.Explicit;
        _mobility = policySet.Mobility;
        _tickPolicy = policySet.TickPolicy;
        _spatialPolicy = policySet.SpatialPolicy;
        _renderDynamicPolicy = policySet.RenderDynamicPolicy;

        if (changed)
        {
            InvalidateConfiguredPolicySet();
        }
    }

    public void UseEnginePolicyDefaults()
    {
        PolicySourceMode = EntityPolicySourceMode.EngineDefault;
    }

    public void RequestConditionalUpdate()
    {
        _conditionalUpdateRequested = true;
    }

    internal void ClearConditionalUpdateRequest()
    {
        _conditionalUpdateRequested = false;
    }

    internal void SetLegacyUpdatesEnabledOverride(bool value)
    {
        if (_legacyUpdatesEnabledOverride == value)
        {
            return;
        }

        _legacyUpdatesEnabledOverride = value;
        InvalidateConfiguredPolicySet();
    }

    internal void InvalidateConfiguredPolicySet()
    {
        _configuredPolicySetDirty = true;
    }

    internal EntityPolicySet GetConfiguredPolicySet(Entity owner)
    {
        if (_configuredPolicySetDirty)
        {
            _configuredPolicySet = EntityPolicyResolver.ResolveConfiguredPolicySet(owner);
            _configuredPolicySetDirty = false;
        }

        return _configuredPolicySet;
    }

    internal EntityPolicyState Clone()
    {
        return new EntityPolicyState
        {
            _policySourceMode = _policySourceMode,
            _mobility = _mobility,
            _tickPolicy = _tickPolicy,
            _spatialPolicy = _spatialPolicy,
            _renderDynamicPolicy = _renderDynamicPolicy,
            _legacyUpdatesEnabledOverride = _legacyUpdatesEnabledOverride,
            _configuredPolicySetDirty = true,
            _configuredPolicySet = EntityPolicySet.DynamicDefault,
        };
    }
}