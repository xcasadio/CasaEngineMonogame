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

    public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
    {
        // Select the unlit technique (BasicEffect = no texture, BasicEffect_Texture = textured)
        shader.SelectTechnique(BasColor != null ? "BasicEffect_Texture" : "BasicEffect");

        // -- Transforms --
        var worldViewProj = world * context.Frame.ViewProjection;
        shader.SetParameter(ShaderParameterNames.WorldViewProj, worldViewProj);
        shader.SetParameter(ShaderParameterNames.World, world);

        // -- Material params --
        // basicEffect.fx uses DiffuseColor (float4) — pack Tint + Alpha into it.
        shader.SetParameter(ShaderParameterNames.DiffuseColor, new Vector4(Tint.ToVector3(), Alpha));
        shader.SetParameter(ShaderParameterNames.EmissiveColor, Vector3.Zero);
        shader.SetParameter(ShaderParameterNames.BasColorTexture, BasColor);
    }

    public override Rendering.Shaders.ShaderFeature GetFeatures(Graphics.StaticModelMesh? mesh = null)
        => BasColor is not null
            ? Rendering.Shaders.ShaderFeature.BasColorTexture
            : Rendering.Shaders.ShaderFeature.None;

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element["BasColor_asset_id"] is { } a)
            BasColorAssetId = Guid.Parse(a.Value<string>()!);

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
