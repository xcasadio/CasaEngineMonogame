using CasaEngine.Framework.Rendering.Draw;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Resolves the concrete shader wrapper to use for a render item in the regular draw path.
/// Built-in shaders are registered explicitly, while asset-backed shaders can flow through the
/// variant library and the shader manager.
/// </summary>
public readonly struct ResolvedShader
{
    public ResolvedShader(ShaderWrapper shader, bool techniqueSelectedBySelector)
    {
        Shader = shader;
        TechniqueSelectedBySelector = techniqueSelectedBySelector;
    }

    public ShaderWrapper Shader { get; }

    public bool TechniqueSelectedBySelector { get; }
}

public sealed class RenderShaderSelector
{
    private readonly ShaderWrapper _fallbackShader;
    private readonly ShaderManager _shaderManager;
    private readonly ShaderVariantLibrary _variantLibrary;
    private readonly Dictionary<Guid, ShaderWrapper> _registeredShaders = new();

    public RenderShaderSelector(
        ShaderWrapper fallbackShader,
        ShaderManager shaderManager = null,
        ShaderVariantLibrary variantLibrary = null)
    {
        _fallbackShader = fallbackShader ?? throw new ArgumentNullException(nameof(fallbackShader));
        _shaderManager = shaderManager;
        _variantLibrary = variantLibrary;
    }

    public void RegisterShader(Guid shaderId, ShaderWrapper shader)
    {
        ArgumentNullException.ThrowIfNull(shader);

        if (shaderId == Guid.Empty)
        {
            throw new ArgumentException("A stable shader id is required.", nameof(shaderId));
        }

        _registeredShaders[shaderId] = shader;
    }

    public ResolvedShader Resolve(in RenderItem item)
        => Resolve(item.EffectiveShaderId, item.Features);

    public ResolvedShader Resolve(Guid effectiveShaderId, ShaderFeature features)
    {
        if (effectiveShaderId == Guid.Empty)
        {
            return new ResolvedShader(_fallbackShader, techniqueSelectedBySelector: false);
        }

        if (_variantLibrary is not null)
        {
            var variantShader = _variantLibrary.Get(new ShaderVariantKey(effectiveShaderId, features));
            if (variantShader is not null)
            {
                return new ResolvedShader(variantShader, techniqueSelectedBySelector: true);
            }
        }

        if (_registeredShaders.TryGetValue(effectiveShaderId, out var registeredShader))
        {
            return new ResolvedShader(registeredShader, techniqueSelectedBySelector: false);
        }

        if (_shaderManager is not null)
        {
            var shader = _shaderManager.GetShader(effectiveShaderId);
            if (shader is not null)
            {
                return new ResolvedShader(shader, techniqueSelectedBySelector: false);
            }
        }

        return new ResolvedShader(_fallbackShader, techniqueSelectedBySelector: false);
    }
}