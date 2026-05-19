using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// A simple spot light with position, direction, range, cone angles and intensity.
/// </summary>
public struct SpotLight
{
    public Vector3 Position;
    public Vector3 Direction;
    public Vector3 DiffuseColor;
    public Vector3 SpecularColor;
    public float Range;
    public float Intensity;
    public float InnerConeAngle;
    public float OuterConeAngle;
    public bool CastShadows;

    public SpotLight(
        Vector3 position,
        Vector3 direction,
        Vector3 diffuseColor,
        Vector3 specularColor,
        float range,
        float innerConeAngle,
        float outerConeAngle,
        float intensity = 1.0f,
        bool castShadows = false)
    {
        Position = position;
        Direction = Vector3.Normalize(direction);
        DiffuseColor = diffuseColor;
        SpecularColor = specularColor;
        Range = MathF.Max(0.0f, range);
        Intensity = intensity;

        float clampedOuterConeAngle = Math.Clamp(outerConeAngle, 0.0f, MathHelper.PiOver2);
        InnerConeAngle = Math.Clamp(innerConeAngle, 0.0f, clampedOuterConeAngle);
        OuterConeAngle = clampedOuterConeAngle;
        CastShadows = castShadows;
    }
}