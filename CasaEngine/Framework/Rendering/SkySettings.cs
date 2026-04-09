using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Immutable parameters describing a simple procedural sky used for background rendering
/// and scene-wide environment reflections.
/// </summary>
public sealed class SkySettings
{
    private static readonly Vector3 DefaultSunDirection = Vector3.Normalize(new Vector3(-0.5f, -0.8f, -0.3f));

    public Color ZenithColor { get; init; } = new(61, 109, 173);

    public Color HorizonColor { get; init; } = new(236, 205, 156);

    public Color GroundColor { get; init; } = new(103, 91, 78);

    public Color SunColor { get; init; } = new(255, 245, 214);

    /// <summary>
    /// Direction pointing from the sun towards the scene, matching <see cref="DirectionalLight.Direction"/>.
    /// The visible sun position is the opposite of this vector.
    /// </summary>
    public Vector3 SunDirection { get; init; } = DefaultSunDirection;

    /// <summary>
    /// Approximate angular radius of the sun disc in directional space.
    /// </summary>
    public float SunSize { get; init; } = 0.04f;

    public int ReflectionCubeSize { get; init; } = 64;

    internal Vector3 GetNormalizedSunDirection()
        => SunDirection.LengthSquared() > 0.0001f
            ? Vector3.Normalize(SunDirection)
            : DefaultSunDirection;
}