namespace CasaEngine.Framework.Rendering.Environment;

internal sealed class ResolvedEnvironmentCache
{
    private bool _hasValue;
    private bool _usesOverride;
    private int _worldVersion = -1;
    private int _overrideVersion = -1;
    private Color _clearColor;
    private ResolvedEnvironmentSettings _resolvedEnvironment;

    public int CacheVersion { get; private set; }

    public bool TryGet(RenderView view, out ResolvedEnvironmentSettings resolvedEnvironment)
    {
        ArgumentNullException.ThrowIfNull(view);

        var source = view.EnvironmentOverride ?? view.World.EnvironmentSettings;
        bool usesOverride = view.EnvironmentOverride is not null;
        int overrideVersion = view.EnvironmentOverride?.Version ?? -1;

        if (_hasValue
            && !source.IsDirty
            && _usesOverride == usesOverride
            && _worldVersion == view.World.EnvironmentSettings.Version
            && _overrideVersion == overrideVersion
            && _clearColor == view.ClearColor)
        {
            resolvedEnvironment = _resolvedEnvironment;
            return true;
        }

        resolvedEnvironment = default;
        return false;
    }

    public void Store(RenderView view, in ResolvedEnvironmentSettings resolvedEnvironment)
    {
        ArgumentNullException.ThrowIfNull(view);

        _hasValue = true;
        _usesOverride = view.EnvironmentOverride is not null;
        _worldVersion = view.World.EnvironmentSettings.Version;
        _overrideVersion = view.EnvironmentOverride?.Version ?? -1;
        _clearColor = view.ClearColor;
        _resolvedEnvironment = resolvedEnvironment;
        CacheVersion++;
    }
}