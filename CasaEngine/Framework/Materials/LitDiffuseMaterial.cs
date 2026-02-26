using CasaEngine.Framework.Rendering;
using CasaEngine.Framework.Rendering.Shaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Materials;

/// <summary>
/// Diffuse-lit material using Lambert shading + specular highlight.
/// Uses directional lights from <see cref="LightingContext"/>.
/// Implemented fully in Phase 5; stub created here for use by MaterialLoader.
/// </summary>
public class LitDiffuseMaterial : MaterialBase
{
    public Texture2D? Albedo { get; set; }
    public Guid AlbedoAssetId { get; set; } = Guid.Empty;
    public Color DiffuseColor { get; set; } = Color.White;
    public Vector3 EmissiveColor { get; set; } = Vector3.Zero;
    public Vector3 SpecularColor { get; set; } = new Vector3(0.5f);
    public float SpecularPower { get; set; } = 16.0f;

    public override void Bind(ShaderWrapper shader, in RenderContext context, Matrix world)
    {
        var worldViewProj = world * context.Frame.ViewProjection;
        shader.SetParameter(ShaderParameterNames.WorldViewProj, worldViewProj);
        shader.SetParameter(ShaderParameterNames.World, world);
        var wit = Matrix.Transpose(Matrix.Invert(world));
        shader.SetParameter(ShaderParameterNames.WorldInverseTranspose, wit);
        shader.SetParameter(ShaderParameterNames.EyePosition, context.Frame.CameraPosition);

        shader.SetParameter(ShaderParameterNames.DiffuseColor, DiffuseColor.ToVector4());
        shader.SetParameter(ShaderParameterNames.EmissiveColor, EmissiveColor);
        shader.SetParameter(ShaderParameterNames.SpecularColor, SpecularColor);
        shader.SetParameter(ShaderParameterNames.SpecularPower, SpecularPower);
        shader.SetParameter(ShaderParameterNames.AlbedoTexture, Albedo);

        context.Lighting?.Bind(shader);
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element["albedo_asset_id"] is { } a)
            AlbedoAssetId = Guid.Parse(a.Value<string>()!);

        if (element["diffuse_color"] is JObject dc)
            DiffuseColor = new Color(
                dc["r"]?.Value<int>() ?? 255, dc["g"]?.Value<int>() ?? 255,
                dc["b"]?.Value<int>() ?? 255, dc["a"]?.Value<int>() ?? 255);

        if (element["emissive_color"] is JObject ec)
            EmissiveColor = new Vector3(
                ec["r"]?.Value<float>() ?? 0, ec["g"]?.Value<float>() ?? 0, ec["b"]?.Value<float>() ?? 0);

        if (element["specular_color"] is JObject sc)
            SpecularColor = new Vector3(
                sc["r"]?.Value<float>() ?? 0.5f, sc["g"]?.Value<float>() ?? 0.5f, sc["b"]?.Value<float>() ?? 0.5f);

        SpecularPower = element["specular_power"]?.Value<float>() ?? 16.0f;
    }

#if EDITOR
    public override void Save(JObject jObject)
    {
        base.Save(jObject);
        jObject["type"]            = nameof(LitDiffuseMaterial);
        jObject["albedo_asset_id"] = AlbedoAssetId.ToString();
        jObject["diffuse_color"]   = new JObject { ["r"] = DiffuseColor.R, ["g"] = DiffuseColor.G, ["b"] = DiffuseColor.B, ["a"] = DiffuseColor.A };
        jObject["emissive_color"]  = new JObject { ["r"] = EmissiveColor.X, ["g"] = EmissiveColor.Y, ["b"] = EmissiveColor.Z };
        jObject["specular_color"]  = new JObject { ["r"] = SpecularColor.X, ["g"] = SpecularColor.Y, ["b"] = SpecularColor.Z };
        jObject["specular_power"]  = SpecularPower;
    }
#endif
}
