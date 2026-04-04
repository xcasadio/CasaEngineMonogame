using CasaEngine.Framework.Rendering.Draw;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Resolves the concrete shader wrapper to use for a render item in the regular draw path.
/// Built-in shaders are registered explicitly, while asset-backed shaders can flow through the
/// variant library and the shader manager.
/// </summary>
public sealed class RenderShaderSelector
{
    private readonly ShaderWrapper _fallbackShader;
    private readonly ShaderManager? _shaderManager;
    private readonly ShaderVariantLibrary? _variantLibrary;
    private readonly Dictionary<Guid, ShaderWrapper> _registeredShaders = new();

    public RenderShaderSelector(
        ShaderWrapper fallbackShader,
        ShaderManager? shaderManager = null,
        ShaderVariantLibrary? variantLibrary = null)
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

    public ShaderWrapper Resolve(in RenderItem item)
    {
        if (item.EffectiveShaderId == Guid.Empty)
        {
            return _fallbackShader;
        }

        if (_registeredShaders.TryGetValue(item.EffectiveShaderId, out var registeredShader))
        {
            return registeredShader;
        }

        if (_variantLibrary is not null)
        {
            var variantShader = _variantLibrary.Get(new ShaderVariantKey(item.EffectiveShaderId, item.Features));
            if (variantShader is not null)
            {
                return variantShader;
            }
        }

        if (_shaderManager is not null)
        {
            var shader = _shaderManager.GetShader(item.EffectiveShaderId);
            if (shader is not null)
            {
                return shader;
            }
        }

        return _fallbackShader;
    }
}