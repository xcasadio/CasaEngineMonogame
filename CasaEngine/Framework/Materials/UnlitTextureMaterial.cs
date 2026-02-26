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
    /// <summary>Runtime Albedo texture (loaded from <see cref="AlbedoAssetId"/>).</summary>
    public Texture2D? Albedo { get; set; }

    /// <summary>Asset ID of the Albedo texture.</summary>
    public Guid AlbedoAssetId { get; set; } = Guid.Empty;

    /// <summary>Multiplicative tint applied to the texture color.</summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>Overall opacity (0 = fully transparent, 1 = opaque).</summary>
    public float Alpha { get; set; } = 1.0f;

    public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
    {
        // -- Transforms --
        var worldViewProj = world * context.Frame.ViewProjection;
        shader.SetParameter(ShaderParameterNames.WorldViewProj, worldViewProj);
        shader.SetParameter(ShaderParameterNames.World, world);

        // -- Material params --
        shader.SetParameter(ShaderParameterNames.AlbedoTexture, Albedo);
        shader.SetParameter(ShaderParameterNames.TintColor, Tint.ToVector4());
        shader.SetParameter(ShaderParameterNames.Alpha, Alpha);
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element["albedo_asset_id"] is { } a)
            AlbedoAssetId = Guid.Parse(a.Value<string>()!);

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

#if EDITOR
    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject["type"]            = nameof(UnlitTextureMaterial);
        jObject["albedo_asset_id"] = AlbedoAssetId.ToString();
        jObject["tint_color"]      = new JObject
        {
            ["r"] = Tint.R,
            ["g"] = Tint.G,
            ["b"] = Tint.B,
            ["a"] = Tint.A,
        };
        jObject["alpha"] = Alpha;
    }
#endif
}
