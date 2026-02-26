using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// A simple directional light with direction, diffuse color, specular color and intensity.
/// </summary>
public struct DirectionalLight
{
    public Vector3 Direction;
    public Vector3 DiffuseColor;
    public Vector3 SpecularColor;
    public float Intensity;

    public DirectionalLight(Vector3 direction, Vector3 diffuseColor, Vector3 specularColor, float intensity = 1.0f)
    {
        Direction = Vector3.Normalize(direction);
        DiffuseColor = diffuseColor;
        SpecularColor = specularColor;
        Intensity = intensity;
    }
}
