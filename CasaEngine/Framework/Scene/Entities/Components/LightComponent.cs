using System.ComponentModel;
using CasaEngine.Core.Serialization;
using CasaEngine.Framework.Rendering;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Scene.Entities.Components;

public enum LightType
{
    Directional,
    Point,
    Spot,
}

[DisplayName("Light")]
public class LightComponent : SceneComponent, IRenderLightSource
{
    private float _intensity = 1.0f;
    private float _range = 10.0f;
    private float _innerConeAngleDegrees = 20.0f;
    private float _outerConeAngleDegrees = 35.0f;

    public LightType Type { get; set; } = LightType.Directional;

    public Color Color { get; set; } = Color.White;

    public Color SpecularColor { get; set; } = Color.White;

    public float Intensity
    {
        get => _intensity;
        set => _intensity = MathF.Max(0.0f, value);
    }

    public float Range
    {
        get => _range;
        set => _range = MathF.Max(0.0f, value);
    }

    public float InnerConeAngleDegrees
    {
        get => _innerConeAngleDegrees;
        set => _innerConeAngleDegrees = Math.Clamp(value, 0.0f, OuterConeAngleDegrees);
    }

    public float OuterConeAngleDegrees
    {
        get => _outerConeAngleDegrees;
        set
        {
            _outerConeAngleDegrees = Math.Clamp(value, 0.0f, 89.0f);
            _innerConeAngleDegrees = Math.Clamp(_innerConeAngleDegrees, 0.0f, _outerConeAngleDegrees);
        }
    }

    [Browsable(false)]
    public Vector3 Direction => Forward;

    [Browsable(false)]
    public Vector3 DiffuseColorVector => Color.ToVector3();

    [Browsable(false)]
    public Vector3 SpecularColorVector => SpecularColor.ToVector3();

    [Browsable(false)]
    public float InnerConeAngleRadians => MathHelper.ToRadians(InnerConeAngleDegrees);

    [Browsable(false)]
    public float OuterConeAngleRadians => MathHelper.ToRadians(OuterConeAngleDegrees);

    public LightComponent()
    {
    }

    protected LightComponent(LightComponent other)
        : base(other)
    {
        Type = other.Type;
        Color = other.Color;
        SpecularColor = other.SpecularColor;
        _intensity = other._intensity;
        _range = other._range;
        _innerConeAngleDegrees = other._innerConeAngleDegrees;
        _outerConeAngleDegrees = other._outerConeAngleDegrees;
    }

    public override LightComponent Clone() => new(this);

    public void AppendLights(LightingContext lightingContext)
    {
        ArgumentNullException.ThrowIfNull(lightingContext);

        if (Intensity <= 0.0f)
        {
            return;
        }

        switch (Type)
        {
            case LightType.Directional:
                AppendDirectionalLight(lightingContext);
                break;

            case LightType.Point:
                AppendPointLight(lightingContext);
                break;

            case LightType.Spot:
                AppendSpotLight(lightingContext);
                break;
        }
    }

    public override void Load(JObject element)
    {
        base.Load(element);

        if (element["light_type"] is JToken lightTypeNode)
        {
            Type = lightTypeNode.GetEnum<LightType>();
        }

        if (element["color"] is JToken colorNode)
        {
            Color = colorNode.GetColor();
        }

        if (element["specular_color"] is JToken specularColorNode)
        {
            SpecularColor = specularColorNode.GetColor();
        }

        if (element["intensity"] is JToken intensityNode)
        {
            Intensity = intensityNode.GetSingle();
        }

        if (element["range"] is JToken rangeNode)
        {
            Range = rangeNode.GetSingle();
        }

        if (element["outer_cone_angle_degrees"] is JToken outerConeAngleNode)
        {
            OuterConeAngleDegrees = outerConeAngleNode.GetSingle();
        }

        if (element["inner_cone_angle_degrees"] is JToken innerConeAngleNode)
        {
            InnerConeAngleDegrees = innerConeAngleNode.GetSingle();
        }
    }

    private void AppendDirectionalLight(LightingContext lightingContext)
    {
        int index = lightingContext.ActiveDirectionalLightCount;
        if (index >= LightingContext.MaxDirectionalLights)
        {
            return;
        }

        lightingContext.DirectionalLights[index] = new DirectionalLight(
            Direction,
            DiffuseColorVector,
            SpecularColorVector,
            Intensity);
        lightingContext.ActiveDirectionalLightCount = index + 1;
    }

    private void AppendPointLight(LightingContext lightingContext)
    {
        if (Range <= 0.0f)
        {
            return;
        }

        int index = lightingContext.ActivePointLightCount;
        if (index >= LightingContext.MaxPointLights)
        {
            return;
        }

        lightingContext.PointLights[index] = new PointLight(
            Position,
            DiffuseColorVector,
            SpecularColorVector,
            Range,
            Intensity);
        lightingContext.ActivePointLightCount = index + 1;
    }

    private void AppendSpotLight(LightingContext lightingContext)
    {
        if (Range <= 0.0f || OuterConeAngleRadians <= 0.0f)
        {
            return;
        }

        int index = lightingContext.ActiveSpotLightCount;
        if (index >= LightingContext.MaxSpotLights)
        {
            return;
        }

        lightingContext.SpotLights[index] = new SpotLight(
            Position,
            Direction,
            DiffuseColorVector,
            SpecularColorVector,
            Range,
            InnerConeAngleRadians,
            OuterConeAngleRadians,
            Intensity);
        lightingContext.ActiveSpotLightCount = index + 1;
    }
}