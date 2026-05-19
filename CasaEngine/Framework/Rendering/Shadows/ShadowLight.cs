using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering.Shadows;

public enum ShadowLightType
{
    Directional,
    Spot,
    Point,
}

/// <summary>
/// Runtime description of one visible light that owns a shadow projection in the forward pipeline.
/// </summary>
public readonly struct ShadowLight
{
    public ShadowLight(
        ShadowLightType type,
        int lightIndex,
        Matrix lightViewProjection,
        Rectangle atlasViewport,
        float depthBias,
        float normalBias)
    {
        Type = type;
        LightIndex = lightIndex;
        LightViewProjection = lightViewProjection;
        AtlasViewport = atlasViewport;
        DepthBias = depthBias;
        NormalBias = normalBias;
    }

    public ShadowLightType Type { get; }

    public int LightIndex { get; }

    public Matrix LightViewProjection { get; }

    public Rectangle AtlasViewport { get; }

    public float DepthBias { get; }

    public float NormalBias { get; }
}