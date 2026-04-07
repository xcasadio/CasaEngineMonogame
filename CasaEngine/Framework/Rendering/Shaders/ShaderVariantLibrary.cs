using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering.Shaders;

/// <summary>
/// Manages a collection of compiled shader variants keyed by (<see cref="ShaderVariantKey"/>).
///
/// Technique name conventions (Phase 8):
/// Opaque, Opaque_Textured, AlphaTest, AlphaTest_Textured, Transparent,
/// Transparent_Textured, Skinned, Skinned_Textured, with optional draw-path
/// suffixes _VertexColor and _Instanced.
///
/// Alias maps translate these canonical names to the actual technique names defined
/// in each .fx file (e.g. LitForward_PixelLighting_Texture_VertexColor).
/// NormalMap, Reflection, and light-count specialisation stay material-specific.
/// </summary>
public sealed class ShaderVariantLibrary
{
    private static readonly ShaderFeature[] CanonicalBaseFeatureSets =
    {
        ShaderFeature.None,
        ShaderFeature.BasColorTexture,
        ShaderFeature.AlphaTest,
        ShaderFeature.AlphaTest | ShaderFeature.BasColorTexture,
        ShaderFeature.Transparent,
        ShaderFeature.Transparent | ShaderFeature.BasColorTexture,
        ShaderFeature.Skinned,
        ShaderFeature.Skinned | ShaderFeature.BasColorTexture,
    };

    private static readonly ShaderFeature[] CanonicalDrawPathFeatureSets =
    {
        ShaderFeature.None,
        ShaderFeature.VertexColor,
        ShaderFeature.Instanced,
        ShaderFeature.VertexColor | ShaderFeature.Instanced,
    };

    private static readonly ShaderFeature[] TechniqueFallbackFeatureOrder =
    {
        ShaderFeature.Instanced,
        ShaderFeature.VertexColor,
    };

    private const ShaderFeature CanonicalTechniqueFeatureMask =
        ShaderFeature.BasColorTexture |
        ShaderFeature.VertexColor |
        ShaderFeature.AlphaTest |
        ShaderFeature.Skinned |
        ShaderFeature.Instanced |
        ShaderFeature.Transparent;

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
    public static Dictionary<string, string> BuildLitForwardAliases()
        => BuildCanonicalAliasMap(ResolveLitForwardTechnique);

    /// <summary>Returns alias map for mapping canonical technique names to UnlitTexture.fx ones.</summary>
    public static Dictionary<string, string> BuildUnlitTextureAliases()
        => BuildCanonicalAliasMap(ResolveUnlitTechnique);

    /// <summary>Returns alias map for mapping canonical technique names to skinEffect.fx ones.</summary>
    public static Dictionary<string, string> BuildSkinnedEffectAliases()
        => BuildCanonicalAliasMap(_ => "RiggedModelDraw");

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

    private static Dictionary<string, string> BuildCanonicalAliasMap(Func<ShaderFeature, string> actualTechniqueResolver)
    {
        var aliases = new Dictionary<string, string>(
            CanonicalBaseFeatureSets.Length * CanonicalDrawPathFeatureSets.Length,
            StringComparer.OrdinalIgnoreCase);

        for (int baseIndex = 0; baseIndex < CanonicalBaseFeatureSets.Length; baseIndex++)
        {
            for (int drawPathIndex = 0; drawPathIndex < CanonicalDrawPathFeatureSets.Length; drawPathIndex++)
            {
                var features = CanonicalBaseFeatureSets[baseIndex] | CanonicalDrawPathFeatureSets[drawPathIndex];
                var canonicalTechnique = BuildTechniqueName(features);
                if (canonicalTechnique is null)
                {
                    continue;
                }

                aliases[canonicalTechnique] = actualTechniqueResolver(features);
            }
        }

        return aliases;
    }

    private static string ResolveLitForwardTechnique(ShaderFeature features)
    {
        bool textured = (features & ShaderFeature.BasColorTexture) != 0;
        bool vertexColor = (features & ShaderFeature.VertexColor) != 0;

        if (textured)
        {
            return vertexColor
                ? "LitForward_PixelLighting_Texture_VertexColor"
                : "LitForward_PixelLighting_Texture";
        }

        return vertexColor
            ? "LitForward_PixelLighting_VertexColor"
            : "LitForward_PixelLighting";
    }

    private static string ResolveUnlitTechnique(ShaderFeature features)
        => (features & ShaderFeature.BasColorTexture) != 0 ? "Unlit_Textured" : "Unlit_Colored";

    private void ApplyTechnique(ShaderWrapper shader, Guid shaderBaseId, ShaderFeature features)
    {
        Span<ShaderFeature> fallbackCandidates = stackalloc ShaderFeature[5];
        int fallbackCandidateCount = BuildTechniqueFallbackCandidates(features, fallbackCandidates);

        for (int i = 0; i < fallbackCandidateCount; i++)
        {
            string? candidate = BuildTechniqueName(fallbackCandidates[i]);
            if (candidate is null)
            {
                continue;
            }

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

    private static int BuildTechniqueFallbackCandidates(ShaderFeature features, Span<ShaderFeature> destination)
    {
        int count = 0;
        ShaderFeature current = features & CanonicalTechniqueFeatureMask;
        AddTechniqueCandidate(destination, ref count, current);

        for (int i = 0; i < TechniqueFallbackFeatureOrder.Length; i++)
        {
            var optionalFeature = TechniqueFallbackFeatureOrder[i];
            if ((current & optionalFeature) == 0)
            {
                continue;
            }

            current &= ~optionalFeature;
            AddTechniqueCandidate(destination, ref count, current);
        }

        ShaderFeature texturedFallback = (current & ShaderFeature.BasColorTexture) != 0
            ? ShaderFeature.BasColorTexture
            : ShaderFeature.None;
        AddTechniqueCandidate(destination, ref count, texturedFallback);

        if (texturedFallback != ShaderFeature.None)
        {
            AddTechniqueCandidate(destination, ref count, ShaderFeature.None);
        }

        return count;
    }

    private static void AddTechniqueCandidate(Span<ShaderFeature> destination, ref int count, ShaderFeature candidate)
    {
        if (count > 0 && destination[count - 1] == candidate)
        {
            return;
        }

        destination[count++] = candidate;
    }

    /// <summary>
    /// Maps ShaderFeature flags to a canonical technique name (Phase 8 convention).
    /// NormalMap, Reflection, and other material-specialised features are intentionally
    /// excluded so they remain under explicit material control.
    /// </summary>
    public static string? BuildTechniqueName(ShaderFeature features)
    {
        features &= CanonicalTechniqueFeatureMask;

        bool textured  = (features & ShaderFeature.BasColorTexture) != 0;
        bool alphaTest = (features & ShaderFeature.AlphaTest)     != 0;
        bool skinned   = (features & ShaderFeature.Skinned)       != 0;
        bool transparent = (features & ShaderFeature.Transparent) != 0;
        bool vertexColor = (features & ShaderFeature.VertexColor) != 0;
        bool instanced = (features & ShaderFeature.Instanced) != 0;

        string techniqueName;

        if (skinned)
        {
            techniqueName = textured ? "Skinned_Textured" : "Skinned";
        }
        else if (transparent)
        {
            techniqueName = textured ? "Transparent_Textured" : "Transparent";
        }
        else if (alphaTest)
        {
            techniqueName = textured ? "AlphaTest_Textured" : "AlphaTest";
        }
        else
        {
            techniqueName = textured ? "Opaque_Textured" : "Opaque";
        }

        if (vertexColor)
        {
            techniqueName += "_VertexColor";
        }

        if (instanced)
        {
            techniqueName += "_Instanced";
        }

        return techniqueName;
    }
}
