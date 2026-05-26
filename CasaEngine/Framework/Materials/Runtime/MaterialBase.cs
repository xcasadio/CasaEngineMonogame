using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Models;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials.Runtime;

/// <summary>Rendering queue ordering. Lower values = rendered first.</summary>
public enum RenderQueue
{
    Opaque      = 2000,
    AlphaTest   = 2500,
    Transparent = 3000,
    Overlay     = 4000,
}

/// <summary>
/// Base class for all materials. Holds render states and the shader reference.
/// Concrete sub-classes (UnlitTextureMaterial, LitDiffuseMaterial…) implement
/// <see cref="Bind"/> to push their specific parameters to the shader.
/// </summary>
public abstract class MaterialBase : ISerializable
{
    public virtual bool SupportsVariantTechniqueSelection => true;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    // --- Render states (null = use renderer defaults) ---

    public BlendState BlendState { get; set; }
    public DepthStencilState DepthStencilState { get; set; }
    public RasterizerState RasterizerState { get; set; }
    public SamplerState SamplerState { get; set; }

    // --- Sorting ---

    public bool IsTransparent { get; set; }
    public RenderQueue Queue { get; set; } = RenderQueue.Opaque;
    public float AlphaCutoff { get; set; } = 0.5f;

    // --- Shadow casting/receiving (placeholder — used from Phase 10) ---

    public bool CastShadows { get; set; } = true;
    public bool ReceiveShadows { get; set; } = true;

    // --- Shader reference ---

    /// <summary>Asset ID of the Effect used to render this material. Resolved at load time.</summary>
    public Guid ShaderAssetId { get; set; } = Guid.Empty;

    // -------------------------------------------------------------------------
    // Abstract surface area
    // -------------------------------------------------------------------------

    /// <summary>
    /// Selects the technique to use for this material on the already resolved shader.
    /// Called by the renderer before <see cref="Bind"/> so shader routing and technique selection
    /// are no longer hidden inside material parameter uploads.
    /// </summary>
    public virtual void SelectTechnique(ShaderWrapper shader, in RenderContext context, ShaderFeature features)
    {
    }

    /// <summary>
    /// Returns true when the material must choose or override the technique after shader resolution.
    /// Materials that only rely on canonical shader variants can keep the selector decision.
    /// </summary>
    public virtual bool RequiresMaterialTechniqueSelection(
        bool techniqueSelectedBySelector,
        in RenderContext context,
        ShaderFeature features)
        => !techniqueSelectedBySelector || !SupportsVariantTechniqueSelection;

    /// <summary>
    /// Pushes all material-specific shader parameters (WVP, textures, scalars…).
    /// Called once per draw item after render states are applied.
    /// </summary>
    public abstract void Bind(ShaderWrapper shader, in RenderContext context, Matrix world);

    public virtual MaterialShaderCapabilities GetShaderCapabilities()
        => CreateShaderCapabilities(MaterialShaderFamily.Lit);

    /// <summary>
    /// Returns the <see cref="ShaderFeature"/> flags active for this material,
    /// optionally considering the <paramref name="mesh"/> (Phase 7).
    /// This is a compatibility wrapper over <see cref="RenderFeatureResolver"/>.
    /// Asset-backed draw paths should prefer <see cref="CompiledMaterial.Features"/>, while
    /// runtime-only materials can still query this helper when no compiled snapshot exists.
    /// </summary>
    public virtual ShaderFeature GetFeatures(StaticModelMesh mesh = null)
        => mesh is null
            ? RenderFeatureResolver.ResolveMaterialFeatures(this)
            : RenderFeatureResolver.Resolve(this, mesh);

    // -------------------------------------------------------------------------
    // Serialisation
    // -------------------------------------------------------------------------

    public virtual void Load(JObject element)
    {
        if (element["id"] is { } idToken)
        {
            Id = Guid.Parse(idToken.Value<string>()!);
        }

        Name = element["name"]?.Value<string>() ?? string.Empty;
        IsTransparent = element["is_transparent"]?.Value<bool>() ?? false;
        AlphaCutoff = element["alpha_cutoff"]?.Value<float>() ?? AlphaCutoff;

        if (element["queue"] is { } queueToken &&
            Enum.TryParse<RenderQueue>(queueToken.Value<string>(), out var queue))
        {
            Queue = queue;
        }

        if (element["shader_asset_id"] is { } shaderToken)
        {
            ShaderAssetId = Guid.Parse(shaderToken.Value<string>()!);
        }

        CastShadows    = element["cast_shadows"]?.Value<bool>() ?? true;
        ReceiveShadows = element["receive_shadows"]?.Value<bool>() ?? true;

        LoadRenderStates(element);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    protected MaterialShaderCapabilities CreateShaderCapabilities(
        MaterialShaderFamily shaderFamily,
        bool hasBasColorTexture = false,
        bool hasNormalMap = false,
        bool hasEmissive = false,
        bool hasReflection = false,
        bool isTransparent = false)
        => new(
            shaderFamily,
            hasBasColorTexture,
            hasNormalMap,
            hasEmissive,
            hasReflection,
            Queue == RenderQueue.AlphaTest,
            IsEffectivelyTransparent() || isTransparent);

    protected bool IsEffectivelyTransparent()
    {
        if (Queue == RenderQueue.AlphaTest)
        {
            return false;
        }

        if (IsTransparent || Queue >= RenderQueue.Transparent)
        {
            return true;
        }

        return ReferenceEquals(BlendState, BlendState.AlphaBlend) ||
               ReferenceEquals(BlendState, BlendState.NonPremultiplied) ||
               ReferenceEquals(BlendState, BlendState.Additive);
    }

    private static readonly Dictionary<string, BlendState> BlendStateMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Opaque"]              = BlendState.Opaque,
        ["AlphaBlend"]          = BlendState.AlphaBlend,
        ["Additive"]            = BlendState.Additive,
        ["NonPremultiplied"]    = BlendState.NonPremultiplied,
    };

    private static readonly Dictionary<string, DepthStencilState> DepthStateMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Default"]  = DepthStencilState.Default,
        ["None"]     = DepthStencilState.None,
        ["Read"]     = DepthStencilState.DepthRead,
    };

    private static readonly Dictionary<string, RasterizerState> RasterizerMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CullNone"]                = RasterizerState.CullNone,
        ["CullClockwise"]           = RasterizerState.CullClockwise,
        ["CullCounterClockwise"]    = RasterizerState.CullCounterClockwise,
    };

    private static readonly Dictionary<string, SamplerState> SamplerMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LinearClamp"]         = SamplerState.LinearClamp,
        ["LinearWrap"]          = SamplerState.LinearWrap,
        ["PointClamp"]          = SamplerState.PointClamp,
        ["PointWrap"]           = SamplerState.PointWrap,
        ["AnisotropicClamp"]    = SamplerState.AnisotropicClamp,
        ["AnisotropicWrap"]     = SamplerState.AnisotropicWrap,
    };

    // -------------------------------------------------------------------------
    // Editor-facing render-state helpers
    // -------------------------------------------------------------------------

    public static IReadOnlyList<string> BlendStateNames         => new List<string>(BlendStateMap.Keys);
    public static IReadOnlyList<string> DepthStencilStateNames  => new List<string>(DepthStateMap.Keys);
    public static IReadOnlyList<string> RasterizerStateNames    => new List<string>(RasterizerMap.Keys);
    public static IReadOnlyList<string> SamplerStateNames       => new List<string>(SamplerMap.Keys);

    public string GetBlendStateName()       => BlendState    == null ? "Opaque"                : GetKeyOrDefault(BlendStateMap, BlendState!,    "Opaque");
    public string GetDepthStateName()       => DepthStencilState == null ? "Default"           : GetKeyOrDefault(DepthStateMap, DepthStencilState!, "Default");
    public string GetRasterizerStateName()  => RasterizerState == null ? "CullCounterClockwise": GetKeyOrDefault(RasterizerMap, RasterizerState!, "CullCounterClockwise");
    public string GetSamplerStateName()     => SamplerState  == null ? "AnisotropicClamp"      : GetKeyOrDefault(SamplerMap,   SamplerState!,   "AnisotropicClamp");

    public void SetBlendStateByName(string name)      { if (BlendStateMap.TryGetValue(name, out var v))
        {
            BlendState = v;
        }
    }
    public void SetDepthStateByName(string name)      { if (DepthStateMap.TryGetValue(name, out var v))
        {
            DepthStencilState = v;
        }
    }
    public void SetRasterizerStateByName(string name) { if (RasterizerMap.TryGetValue(name, out var v))
        {
            RasterizerState = v;
        }
    }
    public void SetSamplerStateByName(string name)    { if (SamplerMap.TryGetValue(name, out var v))
        {
            SamplerState = v;
        }
    }

    private void LoadRenderStates(JObject element)
    {
        if (element["blend_state"] is { } b && BlendStateMap.TryGetValue(b.Value<string>()!, out var blend))
        {
            BlendState = blend;
        }

        if (element["depth_stencil_state"] is { } d && DepthStateMap.TryGetValue(d.Value<string>()!, out var depth))
        {
            DepthStencilState = depth;
        }

        if (element["rasterizer_state"] is { } r && RasterizerMap.TryGetValue(r.Value<string>()!, out var rasterizer))
        {
            RasterizerState = rasterizer;
        }

        if (element["sampler_state"] is { } s && SamplerMap.TryGetValue(s.Value<string>()!, out var sampler))
        {
            SamplerState = sampler;
        }
    }

    private void SaveRenderStates(JObject jObject)
    {
        jObject["blend_state"]          = BlendState == null ? "Opaque" : GetKeyOrDefault(BlendStateMap, BlendState!, "Opaque");
        jObject["depth_stencil_state"]  = DepthStencilState == null ? "Default" : GetKeyOrDefault(DepthStateMap, DepthStencilState!, "Default");
        jObject["rasterizer_state"]     = RasterizerState == null ? "CullCounterClockwise" : GetKeyOrDefault(RasterizerMap, RasterizerState!, "CullCounterClockwise");
        jObject["sampler_state"]        = SamplerState == null ? "AnisotropicClamp" : GetKeyOrDefault(SamplerMap, SamplerState!, "AnisotropicClamp");
    }

    private static string GetKeyOrDefault<T>(Dictionary<string, T> map, T value, string defaultKey)
    {
        foreach (var kv in map)
            if (ReferenceEquals(kv.Value, value))
            {
                return kv.Key;
            }

        return defaultKey;
    }
}
