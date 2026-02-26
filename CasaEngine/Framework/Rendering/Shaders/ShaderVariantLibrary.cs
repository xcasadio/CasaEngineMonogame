using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Manages a collection of compiled shader variants keyed by (<see cref="ShaderVariantKey"/>).
/// Strategy: one variant = one compiled <c>.fx</c> asset registered by name pattern, or
/// a technique inside a shared <c>.fx</c> (the library resolves by technique name first).
///
/// Fallback chain for a <see cref="ShaderVariantKey"/> lookup:
/// <list type="number">
///   <item>Exact variant match cached in <c>_variants</c></item>
///   <item>Exact variant asset found via <see cref="ShaderManager"/></item>
///   <item>Base shader with a matching technique auto-selected by <see cref="BuildTechniqueName"/></item>
///   <item>Base shader with no technique narrowing (log warning)</item>
/// </list>
/// </summary>
public sealed class ShaderVariantLibrary
{
    private readonly ShaderManager _shaderManager;

    // Explicit variant registrations: key → asset Guid of the compiled effect
    private readonly Dictionary<ShaderVariantKey, Guid> _variantAssets = new();

    // Resolved cache: key → ready ShaderWrapper
    private readonly Dictionary<ShaderVariantKey, ShaderWrapper?> _resolved = new();

    // -----------------------------------------------------------------------
    //  Constructor
    // -----------------------------------------------------------------------

    public ShaderVariantLibrary(ShaderManager shaderManager)
    {
        _shaderManager = shaderManager ?? throw new ArgumentNullException(nameof(shaderManager));
    }

    // -----------------------------------------------------------------------
    //  Registration
    // -----------------------------------------------------------------------

    /// <summary>
    /// Registers an explicit compiled-effect asset for a variant key.
    /// Takes priority over automatic technique resolution.
    /// </summary>
    public void RegisterVariant(ShaderVariantKey key, Guid effectAssetId)
    {
        _variantAssets[key] = effectAssetId;
        _resolved.Remove(key); // invalidate cache
    }

    // -----------------------------------------------------------------------
    //  Lookup
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the best matching <see cref="ShaderWrapper"/> for <paramref name="key"/>.
    /// Falls back to the base shader when no specific variant is registered.
    /// Returns <c>null</c> when the base shader itself is unavailable.
    /// </summary>
    public ShaderWrapper? Get(ShaderVariantKey key)
    {
        if (_resolved.TryGetValue(key, out var cached))
            return cached;

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
            if (result is not null)
            {
                var techniqueName = BuildTechniqueName(key.Features);
                if (techniqueName is not null)
                    result.SelectTechnique(techniqueName); // logs warning internally if missing
            }
        }

        if (result is null)
        {
            Core.Log.Logs.WriteWarning(
                $"ShaderVariantLibrary: no shader found for variant {key}. " +
                "Check that the shader asset Guid is registered.");
        }

        _resolved[key] = result;
        return result;
    }

    /// <summary>Evicts cached entries so they are re-resolved on next access.</summary>
    public void InvalidateAll() => _resolved.Clear();

    // -----------------------------------------------------------------------
    //  Technique name convention
    // -----------------------------------------------------------------------

    /// <summary>
    /// Maps a set of <see cref="ShaderFeature"/> flags to a technique name
    /// following the engine's naming convention.
    ///
    /// Naming scheme (subset rules, most-specific wins):
    /// <list type="bullet">
    ///   <item>Skinned → "Skinned[_Textured]"</item>
    ///   <item>AlphaTest + Textured → "AlphaTest_Textured"</item>
    ///   <item>AlphaTest → "AlphaTest"</item>
    ///   <item>Textured → "Opaque_Textured" / "BasicEffect_PixelLighting_Texture"</item>
    ///   <item>None → "Opaque" / "BasicEffect_PixelLighting"</item>
    /// </list>
    ///
    /// Returns <c>null</c> to skip technique selection (use whatever is current).
    /// </summary>
    public static string? BuildTechniqueName(ShaderFeature features)
    {
        bool textured  = (features & ShaderFeature.AlbedoTexture) != 0;
        bool alphaTest = (features & ShaderFeature.AlphaTest)     != 0;
        bool skinned   = (features & ShaderFeature.Skinned)       != 0;

        if (skinned)
            return textured ? "Skinned_Textured" : "Skinned";

        if (alphaTest)
            return textured ? "AlphaTest_Textured" : "AlphaTest";

        // Default lit-diffuse mapping — matches basicEffect.fx technique names
        return textured
            ? "BasicEffect_PixelLighting_Texture"
            : "BasicEffect_PixelLighting";
    }
}
