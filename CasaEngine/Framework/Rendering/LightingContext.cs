using CasaEngine.Framework.Rendering.Environment;
using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.Rendering;

/// <summary>
/// Holds lighting data for a render pass: directional, point and spot lights plus ambient color.
/// Populated and attached to <see cref="RenderContext"/> before rendering.
/// Fully implemented in Phase 5.
/// </summary>
public class LightingContext
{
    public const int MaxDirectionalLights = 8;
    public const int MaxPointLights = 8;
    public const int MaxSpotLights = 8;

    private readonly float[] _pointLightScores = new float[MaxPointLights];
    private readonly float[] _spotLightScores = new float[MaxSpotLights];
    private Vector3 _priorityPosition;

    public DirectionalLight[] DirectionalLights { get; } = new DirectionalLight[MaxDirectionalLights];
    public int ActiveDirectionalLightCount { get; set; }
    public PointLight[] PointLights { get; } = new PointLight[MaxPointLights];
    public int ActivePointLightCount { get; set; }
    public SpotLight[] SpotLights { get; } = new SpotLight[MaxSpotLights];
    public int ActiveSpotLightCount { get; set; }
    public Vector3 AmbientColor { get; set; } = EnvironmentResolver.LegacyAmbientColor;

    internal static int ClampActiveDirectionalLightCount(int activeDirectionalLightCount)
        => Math.Clamp(activeDirectionalLightCount, 0, MaxDirectionalLights);

    internal static int ClampActivePointLightCount(int activePointLightCount)
        => Math.Clamp(activePointLightCount, 0, MaxPointLights);

    internal static int ClampActiveSpotLightCount(int activeSpotLightCount)
        => Math.Clamp(activeSpotLightCount, 0, MaxSpotLights);

    public void ClearLights()
    {
        ActiveDirectionalLightCount = 0;
        ActivePointLightCount = 0;
        ActiveSpotLightCount = 0;

        for (int i = 0; i < MaxDirectionalLights; i++)
        {
            DirectionalLights[i] = default;
        }

        for (int i = 0; i < MaxPointLights; i++)
        {
            PointLights[i] = default;
            _pointLightScores[i] = 0.0f;
        }

        for (int i = 0; i < MaxSpotLights; i++)
        {
            SpotLights[i] = default;
            _spotLightScores[i] = 0.0f;
        }
    }

    /// <summary>Clears previous lights and sets per-view data used to rank local lights.</summary>
    public void BeginCollection(Vector3 priorityPosition, Vector3 ambientColor)
    {
        ClearLights();
        _priorityPosition = priorityPosition;
        AmbientColor = ambientColor;
    }

    /// <summary>Adds a directional light while respecting the shader-supported cap.</summary>
    public void AddDirectionalLight(in DirectionalLight light)
    {
        int index = ActiveDirectionalLightCount;
        if (index >= MaxDirectionalLights
            || light.Intensity <= 0.0f
            || GetLightColorWeight(light.DiffuseColor, light.SpecularColor) <= 0.0f)
        {
            return;
        }

        DirectionalLights[index] = light;
        ActiveDirectionalLightCount = index + 1;
    }

    /// <summary>Adds a point light, keeping only the highest-priority candidates when the cap is reached.</summary>
    public void AddPointLight(in PointLight light)
    {
        if (light.Range <= 0.0f || light.Intensity <= 0.0f)
        {
            return;
        }

        float score = ComputeLocalLightScore(light.Position, light.Range, light.DiffuseColor, light.SpecularColor, light.Intensity);
        if (score <= 0.0f)
        {
            return;
        }

        InsertPointLight(light, score);
    }

    /// <summary>Adds a spot light, keeping only the highest-priority candidates when the cap is reached.</summary>
    public void AddSpotLight(in SpotLight light)
    {
        if (light.Range <= 0.0f || light.Intensity <= 0.0f || light.OuterConeAngle <= 0.0f)
        {
            return;
        }

        float score = ComputeLocalLightScore(light.Position, light.Range, light.DiffuseColor, light.SpecularColor, light.Intensity);
        if (score <= 0.0f)
        {
            return;
        }

        InsertSpotLight(light, score);
    }

    public void CopyFrom(LightingContext other)
    {
        ArgumentNullException.ThrowIfNull(other);

        ActiveDirectionalLightCount = ClampActiveDirectionalLightCount(other.ActiveDirectionalLightCount);
        ActivePointLightCount = ClampActivePointLightCount(other.ActivePointLightCount);
        ActiveSpotLightCount = ClampActiveSpotLightCount(other.ActiveSpotLightCount);
        AmbientColor = other.AmbientColor;
        _priorityPosition = other._priorityPosition;

        for (int i = 0; i < MaxDirectionalLights; i++)
        {
            DirectionalLights[i] = other.DirectionalLights[i];
        }

        for (int i = 0; i < MaxPointLights; i++)
        {
            PointLights[i] = other.PointLights[i];
            _pointLightScores[i] = other._pointLightScores[i];
        }

        for (int i = 0; i < MaxSpotLights; i++)
        {
            SpotLights[i] = other.SpotLights[i];
            _spotLightScores[i] = other._spotLightScores[i];
        }
    }

    private void InsertPointLight(in PointLight light, float score)
    {
        int count = ActivePointLightCount;
        if (count >= MaxPointLights)
        {
            if (score <= _pointLightScores[MaxPointLights - 1])
            {
                return;
            }

            count = MaxPointLights - 1;
        }
        else
        {
            ActivePointLightCount = count + 1;
        }

        int insertIndex = count;
        while (insertIndex > 0 && score > _pointLightScores[insertIndex - 1])
        {
            PointLights[insertIndex] = PointLights[insertIndex - 1];
            _pointLightScores[insertIndex] = _pointLightScores[insertIndex - 1];
            insertIndex--;
        }

        PointLights[insertIndex] = light;
        _pointLightScores[insertIndex] = score;
    }

    private void InsertSpotLight(in SpotLight light, float score)
    {
        int count = ActiveSpotLightCount;
        if (count >= MaxSpotLights)
        {
            if (score <= _spotLightScores[MaxSpotLights - 1])
            {
                return;
            }

            count = MaxSpotLights - 1;
        }
        else
        {
            ActiveSpotLightCount = count + 1;
        }

        int insertIndex = count;
        while (insertIndex > 0 && score > _spotLightScores[insertIndex - 1])
        {
            SpotLights[insertIndex] = SpotLights[insertIndex - 1];
            _spotLightScores[insertIndex] = _spotLightScores[insertIndex - 1];
            insertIndex--;
        }

        SpotLights[insertIndex] = light;
        _spotLightScores[insertIndex] = score;
    }

    private float ComputeLocalLightScore(Vector3 position, float range, Vector3 diffuseColor, Vector3 specularColor, float intensity)
    {
        float rangeSquared = MathF.Max(range * range, 0.0001f);
        float distanceSquared = Vector3.DistanceSquared(position, _priorityPosition);
        float relativeDistance = distanceSquared / rangeSquared;
        float distanceScore = 1.0f / (1.0f + relativeDistance);
        return MathF.Max(0.0f, intensity) * GetLightColorWeight(diffuseColor, specularColor) * distanceScore;
    }

    private static float GetLightColorWeight(Vector3 diffuseColor, Vector3 specularColor)
    {
        float diffuseWeight = GetLuminance(diffuseColor);
        float specularWeight = GetLuminance(specularColor);
        return MathF.Max(diffuseWeight, specularWeight);
    }

    private static float GetLuminance(Vector3 color)
        => MathF.Max(0.0f, color.X * 0.2126f + color.Y * 0.7152f + color.Z * 0.0722f);
}
