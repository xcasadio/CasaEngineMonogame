using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

/// <summary>
/// A simple unlit material that displays a texture multiplied by a tint color and alpha.
/// No lighting calculations — uses the UnlitTexture shader.
/// </summary>
public class UnlitTextureMaterial : MaterialBase
{
    /// <summary>Runtime BasColor texture (loaded from <see cref="BasColorAssetId"/>).</summary>
    public Texture2D? BasColor { get; set; }

    /// <summary>Asset ID of the BasColor texture.</summary>
    public Guid BasColorAssetId { get; set; } = Guid.Empty;

    /// <summary>Multiplicative tint applied to the texture color.</summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>Overall opacity (0 = fully transparent, 1 = opaque).</summary>
    public float Alpha { get; set; } = 1.0f;

    internal static string GetPrimaryTechniqueName(bool hasBasColor)
        => hasBasColor ? "Unlit_Textured" : "Unlit_Colored";

    internal static string GetFallbackTechniqueName(bool hasBasColor)
        => hasBasColor ? "LitForward_Texture" : "LitForward";

    public override void SelectTechnique(ShaderWrapper shader, in RenderContext context, ShaderFeature features)
    {
        bool hasBasColor = BasColor != null;
        string techniqueName = GetPrimaryTechniqueName(hasBasColor);

        if (shader.HasTechnique(techniqueName))
        {
            shader.SelectTechnique(techniqueName);
            return;
        }

        shader.SelectTechnique(GetFallbackTechniqueName(hasBasColor));
    }

    public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
    {

        // -- Transforms --
        var worldViewProj = world * context.Frame.ViewProjection;
        shader.SetParameter(ShaderParameterNames.WorldViewProj, worldViewProj);
        shader.SetParameter(ShaderParameterNames.World, world);

        // -- Material params --
        shader.SetParameter(ShaderParameterNames.TintColor, Tint.ToVector4());
        shader.SetParameter(ShaderParameterNames.Alpha, Alpha);
        shader.SetParameter(ShaderParameterNames.AlphaCutoff, Queue == RenderQueue.AlphaTest ? AlphaCutoff : 0.0f);
        shader.SetParameter(ShaderParameterNames.EmissiveColor, Vector3.Zero);
        shader.SetTextureParameter(ShaderParameterNames.BasColorTexture, BasColor, context.Stats);
    }

    public override MaterialShaderCapabilities GetShaderCapabilities()
        => CreateShaderCapabilities(
            MaterialShaderFamily.Unlit,
            hasBasColorTexture: BasColor is not null || BasColorAssetId != Guid.Empty,
            isTransparent: Alpha < 0.999f || Tint.A < byte.MaxValue);

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element["BasColor_asset_id"] is { } a)
        {
            BasColorAssetId = Guid.Parse(a.Value<string>()!);
        }

        if (element["tint_color"] is JObject tc)
        {
            Tint = new Color(
                tc["r"]?.Value<int>() ?? 255,
                tc["g"]?.Value<int>() ?? 255,
                tc["b"]?.Value<int>() ?? 255,
                tc["a"]?.Value<int>() ?? 255);
        }

        Alpha = element["alpha"]?.Value<float>() ?? 1.0f;
    }

}
