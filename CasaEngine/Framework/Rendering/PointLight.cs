using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// A simple point light with position, diffuse/specular colors, range and intensity.
/// </summary>
public struct PointLight
{
    public Vector3 Position;
    public Vector3 DiffuseColor;
    public Vector3 SpecularColor;
    public float Range;
    public float Intensity;

    public PointLight(Vector3 position, Vector3 diffuseColor, Vector3 specularColor, float range, float intensity = 1.0f)
    {
        Position = position;
        DiffuseColor = diffuseColor;
        SpecularColor = specularColor;
        Range = MathF.Max(0.0f, range);
        Intensity = intensity;
    }
}