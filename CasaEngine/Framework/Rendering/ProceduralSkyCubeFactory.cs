using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Builds cubemap resources from a <see cref="SkySettings"/> profile.
/// </summary>
public static class ProceduralSkyCubeFactory
{
    public static TextureCube CreateReflectionCube(GraphicsDevice graphicsDevice, SkySettings settings, int? size = null)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(settings);

        int cubeSize = Math.Max(1, size ?? settings.ReflectionCubeSize);
        var textureCube = new TextureCube(graphicsDevice, cubeSize, mipMap: false, SurfaceFormat.Color);

        foreach (CubeMapFace face in Enum.GetValues<CubeMapFace>())
        {
            var data = new Color[cubeSize * cubeSize];

            for (int y = 0; y < cubeSize; y++)
            {
                for (int x = 0; x < cubeSize; x++)
                {
                    float u = cubeSize == 1 ? 0.5f : x / (float)(cubeSize - 1);
                    float v = cubeSize == 1 ? 0.5f : y / (float)(cubeSize - 1);
                    Vector3 direction = GetDirection(face, u, v);
                    data[y * cubeSize + x] = EvaluateColor(settings, direction, includeSun: true);
                }
            }

            textureCube.SetData(face, data);
        }

        textureCube.Name = "ProceduralSkyCube";
        return textureCube;
    }

    internal static Color EvaluateColor(SkySettings settings, Vector3 direction, bool includeSun)
    {
        Vector3 color = EvaluateColorVector(settings, direction, includeSun);
        return new Color(new Vector4(Vector3.Clamp(color, Vector3.Zero, Vector3.One), 1.0f));
    }

    internal static Vector3 EvaluateColorVector(SkySettings settings, Vector3 direction, bool includeSun)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Vector3 normalizedDirection = direction.LengthSquared() > 0.0001f
            ? Vector3.Normalize(direction)
            : Vector3.Up;

        Vector3 horizon = settings.HorizonColor.ToVector3();
        Vector3 sky = settings.ZenithColor.ToVector3();
        Vector3 ground = settings.GroundColor.ToVector3();

        Vector3 color;
        if (normalizedDirection.Y >= 0.0f)
        {
            float t = MathF.Pow(Math.Clamp(normalizedDirection.Y, 0.0f, 1.0f), 0.55f);
            color = Vector3.Lerp(horizon, sky, MathHelper.SmoothStep(0.0f, 1.0f, t));
        }
        else
        {
            float t = MathF.Pow(Math.Clamp(-normalizedDirection.Y, 0.0f, 1.0f), 0.65f);
            color = Vector3.Lerp(horizon, ground, MathHelper.SmoothStep(0.0f, 1.0f, t));
        }

        float horizonGlow = MathF.Exp(-MathF.Abs(normalizedDirection.Y) * 7.0f);
        color = Vector3.Lerp(color, horizon, horizonGlow * 0.12f);

        if (includeSun)
        {
            Vector3 visibleSunDirection = -settings.GetNormalizedSunDirection();
            float sunDot = Math.Clamp(Vector3.Dot(normalizedDirection, visibleSunDirection), 0.0f, 1.0f);
            float sunSize = Math.Clamp(settings.SunSize, 0.0025f, 0.2f);
            float glowStart = Math.Clamp(1.0f - sunSize * 6.0f, 0.0f, 1.0f);
            float discStart = Math.Clamp(1.0f - sunSize, 0.0f, 1.0f);
            float sunGlow = MathHelper.SmoothStep(glowStart, 1.0f, sunDot);
            float sunDisc = MathHelper.SmoothStep(discStart, 1.0f, sunDot);
            color += settings.SunColor.ToVector3() * (sunGlow * 0.32f + sunDisc * 0.88f);
        }

        return Vector3.Clamp(color, Vector3.Zero, Vector3.One);
    }

    private static Vector3 GetDirection(CubeMapFace face, float u, float v)
    {
        float x = u * 2.0f - 1.0f;
        float y = 1.0f - v * 2.0f;

        Vector3 direction = face switch
        {
            CubeMapFace.PositiveX => new Vector3(1.0f, y, -x),
            CubeMapFace.NegativeX => new Vector3(-1.0f, y, x),
            CubeMapFace.PositiveY => new Vector3(x, 1.0f, -y),
            CubeMapFace.NegativeY => new Vector3(x, -1.0f, y),
            CubeMapFace.PositiveZ => new Vector3(x, y, 1.0f),
            CubeMapFace.NegativeZ => new Vector3(-x, y, -1.0f),
            _ => Vector3.Up,
        };

        return Vector3.Normalize(direction);
    }
}