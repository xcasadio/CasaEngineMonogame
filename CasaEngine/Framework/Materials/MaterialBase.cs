using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

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
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    // --- Render states (null = use renderer defaults) ---

    public BlendState? BlendState { get; set; }
    public DepthStencilState? DepthStencilState { get; set; }
    public RasterizerState? RasterizerState { get; set; }
    public SamplerState? SamplerState { get; set; }

    // --- Sorting ---

    public bool IsTransparent { get; set; }
    public RenderQueue Queue { get; set; } = RenderQueue.Opaque;

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
    /// Pushes all material-specific shader parameters (WVP, textures, scalars…).
    /// Called once per draw item after render states are applied.
    /// </summary>
    public abstract void Bind(ShaderWrapper shader, in RenderContext context, Matrix world);

    /// <summary>
    /// Returns the <see cref="ShaderFeature"/> flags active for this material,
    /// optionally considering the <paramref name="mesh"/> (Phase 7).
    /// The renderer uses these flags to select the correct compiled shader variant.
    /// </summary>
    public virtual Rendering.Shaders.ShaderFeature GetFeatures(Graphics.StaticModelMesh? mesh = null)
        => Rendering.Shaders.ShaderFeature.None;

    // -------------------------------------------------------------------------
    // Serialisation
    // -------------------------------------------------------------------------

    public virtual void Load(JObject element)
    {
        if (element["id"] is { } idToken)
            Id = Guid.Parse(idToken.Value<string>()!);

        Name = element["name"]?.Value<string>() ?? string.Empty;
        IsTransparent = element["is_transparent"]?.Value<bool>() ?? false;

        if (element["queue"] is { } queueToken &&
            Enum.TryParse<RenderQueue>(queueToken.Value<string>(), out var queue))
        {
            Queue = queue;
        }

        if (element["shader_asset_id"] is { } shaderToken)
            ShaderAssetId = Guid.Parse(shaderToken.Value<string>()!);

        CastShadows    = element["cast_shadows"]?.Value<bool>() ?? true;
        ReceiveShadows = element["receive_shadows"]?.Value<bool>() ?? true;

        LoadRenderStates(element);
    }

#if EDITOR
    public virtual void Save(JObject jObject)
    {
        jObject["id"]             = Id.ToString();
        jObject["name"]           = Name;
        jObject["is_transparent"] = IsTransparent;
        jObject["queue"]          = Queue.ToString();
        jObject["shader_asset_id"]  = ShaderAssetId.ToString();
        jObject["cast_shadows"]   = CastShadows;
        jObject["receive_shadows"] = ReceiveShadows;

        SaveRenderStates(jObject);
    }
#endif

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

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

    private void LoadRenderStates(JObject element)
    {
        if (element["blend_state"] is { } b && BlendStateMap.TryGetValue(b.Value<string>()!, out var blend))
            BlendState = blend;
        if (element["depth_stencil_state"] is { } d && DepthStateMap.TryGetValue(d.Value<string>()!, out var depth))
            DepthStencilState = depth;
        if (element["rasterizer_state"] is { } r && RasterizerMap.TryGetValue(r.Value<string>()!, out var rasterizer))
            RasterizerState = rasterizer;
        if (element["sampler_state"] is { } s && SamplerMap.TryGetValue(s.Value<string>()!, out var sampler))
            SamplerState = sampler;
    }

#if EDITOR
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
                return kv.Key;
        return defaultKey;
    }
#endif
}
