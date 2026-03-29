using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Manages a collection of compiled shader variants keyed by (<see cref="ShaderVariantKey"/>).
///
/// Technique name conventions (Phase 8):
/// Opaque, Opaque_Textured, AlphaTest, AlphaTest_Textured, Transparent, Skinned, Skinned_Textured.
///
/// Alias maps translate these canonical names to the actual technique names defined
/// in each .fx file (e.g. BasicEffect_PixelLighting_Texture).
/// </summary>
public sealed class ShaderVariantLibrary
{
    private readonly ShaderManager _shaderManager;

    // Explicit variant registrations: key -> asset Guid of the compiled effect
    private readonly Dictionary<ShaderVariantKey, Guid> _variantAssets = new();

    // Resolved cache: key -> ready ShaderWrapper
    private readonly Dictionary<ShaderVariantKey, ShaderWrapper?> _resolved = new();

    // Per-shader alias maps: shaderBaseId -> (canonicalName -> actualTechniqueName)
    private readonly Dictionary<Guid, Dictionary<string, string>> _aliasMap = new();

    public ShaderVariantLibrary(ShaderManager shaderManager)
    {
        _shaderManager = shaderManager ?? throw new ArgumentNullException(nameof(shaderManager));
    }

    // Registration -------------------------------------------------------

    public void RegisterVariant(ShaderVariantKey key, Guid effectAssetId)
    {
        _variantAssets[key] = effectAssetId;
        _resolved.Remove(key);
    }

    /// <summary>
    /// Registers technique name aliases for shaderBaseId.
    /// Keys are canonical names (e.g. "Opaque_Textured"); values are actual .fx technique names.
    /// </summary>
    public void RegisterTechniqueAliases(Guid shaderBaseId, Dictionary<string, string> aliases)
    {
        if (!_aliasMap.TryGetValue(shaderBaseId, out var map))
        {
            map = new Dictionary<string, string>(aliases.Count, StringComparer.OrdinalIgnoreCase);
            _aliasMap[shaderBaseId] = map;
        }
        foreach (var (canonical, actual) in aliases)
            map[canonical] = actual;
        _resolved.Clear();
    }

    /// <summary>Returns alias map for mapping canonical technique names to basicEffect.fx ones.</summary>
    public static Dictionary<string, string> BuildBasicEffectAliases() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Opaque"]             = "BasicEffect_PixelLighting",
            ["Opaque_Textured"]    = "BasicEffect_PixelLighting_Texture",
            ["AlphaTest"]          = "BasicEffect_PixelLighting",
            ["AlphaTest_Textured"] = "BasicEffect_PixelLighting_Texture",
            ["Transparent"]        = "BasicEffect_PixelLighting_Texture",
            ["Skinned"]            = "BasicEffect_PixelLighting",
            ["Skinned_Textured"]   = "BasicEffect_PixelLighting_Texture",
        };

    // Lookup -------------------------------------------------------------

    /// <summary>
    /// Returns the best matching ShaderWrapper for key.
    /// Falls back to the base shader when no specific variant is registered.
    /// Returns null when the base shader itself is unavailable.
    /// </summary>
    public ShaderWrapper? Get(ShaderVariantKey key)
    {
        if (_resolved.TryGetValue(key, out var cached))
        {
            return cached;
        }

        ShaderWrapper? result = null;

        // 1. Try explicit variant asset
        if (_variantAssets.TryGetValue(key, out var variantId))
        {
            result = _shaderManager.GetShader(variantId);
            if (result is not null)
            {
                ApplyTechnique(result, key.ShaderBaseId, key.Features);
            }
        }

        // 2. Try base shader + technique selection
        if (result is null && key.ShaderBaseId != Guid.Empty)
        {
            result = _shaderManager.GetShader(key.ShaderBaseId);
            if (result is not null)
            {
                ApplyTechnique(result, key.ShaderBaseId, key.Features);
            }
        }

        if (result is null)
        {
            Core.Log.Logs.WriteWarning(
                $"ShaderVariantLibrary: no shader found for variant {key}.");
        }

        _resolved[key] = result;
        return result;
    }

    public void InvalidateAll() => _resolved.Clear();

    // Technique helpers --------------------------------------------------

    private void ApplyTechnique(ShaderWrapper shader, Guid shaderBaseId, ShaderFeature features)
    {
        var canonical = BuildTechniqueName(features);
        if (canonical is null)
        {
            return;
        }

        string techniqueName = canonical;
        if (_aliasMap.TryGetValue(shaderBaseId, out var aliases) &&
            aliases.TryGetValue(canonical, out var aliased))
        {
            techniqueName = aliased;
        }

        shader.SelectTechnique(techniqueName);
    }

    /// <summary>
    /// Maps ShaderFeature flags to a canonical technique name (Phase 8 convention).
    /// Returns null to skip technique selection.
    /// </summary>
    public static string? BuildTechniqueName(ShaderFeature features)
    {
        bool textured  = (features & ShaderFeature.BasColorTexture) != 0;
        bool alphaTest = (features & ShaderFeature.AlphaTest)     != 0;
        bool skinned   = (features & ShaderFeature.Skinned)       != 0;

        if (skinned)
        {
            return textured ? "Skinned_Textured"   : "Skinned";
        }

        if (alphaTest)
        {
            return textured ? "AlphaTest_Textured" : "AlphaTest";
        }

        return textured ? "Opaque_Textured" : "Opaque";
    }
}
