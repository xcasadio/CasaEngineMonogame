using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Manages a collection of compiled shader variants keyed by (<see cref="ShaderVariantKey"/>).
///
/// Technique name conventions (Phase 8):
/// Opaque, Opaque_Textured, AlphaTest, AlphaTest_Textured, Transparent,
/// Transparent_Textured, Skinned, Skinned_Textured.
///
/// Alias maps translate these canonical names to the actual technique names defined
/// in each .fx file (e.g. LitForward_PixelLighting_Texture).
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

    /// <summary>Returns alias map for mapping canonical technique names to LitForward.fx ones.</summary>
    public static Dictionary<string, string> BuildLitForwardAliases() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Opaque"]             = "LitForward_PixelLighting",
            ["Opaque_Textured"]    = "LitForward_PixelLighting_Texture",
            ["AlphaTest"]          = "LitForward_PixelLighting",
            ["AlphaTest_Textured"] = "LitForward_PixelLighting_Texture",
            ["Transparent"]        = "LitForward_PixelLighting",
            ["Transparent_Textured"] = "LitForward_PixelLighting_Texture",
            ["Skinned"]            = "LitForward_PixelLighting",
            ["Skinned_Textured"]   = "LitForward_PixelLighting_Texture",
        };

    /// <summary>Returns alias map for mapping canonical technique names to UnlitTexture.fx ones.</summary>
    public static Dictionary<string, string> BuildUnlitTextureAliases() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Opaque"]             = "Unlit_Colored",
            ["Opaque_Textured"]    = "Unlit_Textured",
            ["AlphaTest"]          = "Unlit_Colored",
            ["AlphaTest_Textured"] = "Unlit_Textured",
            ["Transparent"]        = "Unlit_Colored",
            ["Transparent_Textured"] = "Unlit_Textured",
            ["Skinned"]            = "Unlit_Colored",
            ["Skinned_Textured"]   = "Unlit_Textured",
        };

    /// <summary>Returns alias map for mapping canonical technique names to skinEffect.fx ones.</summary>
    public static Dictionary<string, string> BuildSkinnedEffectAliases() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Opaque"]               = "RiggedModelDraw",
            ["Opaque_Textured"]      = "RiggedModelDraw",
            ["AlphaTest"]            = "RiggedModelDraw",
            ["AlphaTest_Textured"]   = "RiggedModelDraw",
            ["Transparent"]          = "RiggedModelDraw",
            ["Transparent_Textured"] = "RiggedModelDraw",
            ["Skinned"]              = "RiggedModelDraw",
            ["Skinned_Textured"]     = "RiggedModelDraw",
        };

    // Lookup -------------------------------------------------------------

    /// <summary>
    /// Returns the best matching ShaderWrapper for key.
    /// Falls back to the base shader when no specific variant is registered.
    /// Returns null when the base shader itself is unavailable.
    /// </summary>
    public ShaderWrapper? Get(ShaderVariantKey key)
        => GetOrResolve(
            key,
            _resolved,
            ResolveShader,
            (shader, resolvedKey) => ApplyTechnique(shader, resolvedKey.ShaderBaseId, resolvedKey.Features));

    internal static TShader? GetOrResolve<TShader>(
        ShaderVariantKey key,
        IDictionary<ShaderVariantKey, TShader?> resolved,
        Func<ShaderVariantKey, TShader?> resolveShader,
        Action<TShader, ShaderVariantKey> applySelection)
        where TShader : class
    {
        if (resolved.TryGetValue(key, out var cached))
        {
            if (cached is not null)
            {
                applySelection(cached, key);
            }

            return cached;
        }

        var result = resolveShader(key);
        if (result is not null)
        {
            applySelection(result, key);
        }

        resolved[key] = result;
        return result;
    }

    private ShaderWrapper? ResolveShader(ShaderVariantKey key)
    {
        ShaderWrapper? result = null;

        // 1. Try explicit variant asset
        if (_variantAssets.TryGetValue(key, out var variantId))
        {
            result = _shaderManager.GetShader(variantId);
        }

        // 2. Try base shader + technique selection
        if (result is null && key.ShaderBaseId != Guid.Empty)
        {
            result = _shaderManager.GetShader(key.ShaderBaseId);
        }

        if (result is null)
        {
            Core.Log.Logs.WriteWarning(
                $"ShaderVariantLibrary: no shader found for variant {key}.");
        }

        return result;
    }

    public void InvalidateAll() => _resolved.Clear();

    // Technique helpers --------------------------------------------------

    private void ApplyTechnique(ShaderWrapper shader, Guid shaderBaseId, ShaderFeature features)
    {
        foreach (var candidate in BuildTechniqueFallbackChain(features))
        {
            string techniqueName = candidate;
            if (_aliasMap.TryGetValue(shaderBaseId, out var aliases) &&
                aliases.TryGetValue(candidate, out var aliased))
            {
                techniqueName = aliased;
            }

            if (shader.HasTechnique(techniqueName))
            {
                shader.SelectTechnique(techniqueName);
                return;
            }
        }

        var requestedTechnique = BuildTechniqueName(features) ?? "<none>";
        Core.Log.Logs.WriteWarning(
            $"ShaderVariantLibrary: no compatible technique found for shader '{shaderBaseId}' and canonical technique '{requestedTechnique}'.");
    }

    private static IEnumerable<string> BuildTechniqueFallbackChain(ShaderFeature features)
    {
        var canonical = BuildTechniqueName(features);
        if (canonical is null)
        {
            yield break;
        }

        yield return canonical;

        bool textured = (features & ShaderFeature.BasColorTexture) != 0;
        var texturedFallback = textured ? "Opaque_Textured" : "Opaque";

        if (!string.Equals(canonical, texturedFallback, StringComparison.OrdinalIgnoreCase))
        {
            yield return texturedFallback;
        }

        if (!string.Equals(texturedFallback, "Opaque", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Opaque";
        }
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
        bool transparent = (features & ShaderFeature.Transparent) != 0;

        if (skinned)
        {
            return textured ? "Skinned_Textured"   : "Skinned";
        }

        if (transparent)
        {
            return textured ? "Transparent_Textured" : "Transparent";
        }

        if (alphaTest)
        {
            return textured ? "AlphaTest_Textured" : "AlphaTest";
        }

        return textured ? "Opaque_Textured" : "Opaque";
    }
}
