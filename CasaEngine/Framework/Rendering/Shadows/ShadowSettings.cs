using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Shadows;

/// <summary>
/// Authoring/runtime settings that control forward shadow-map generation.
/// V1 keeps the feature disabled by default so existing scenes remain unchanged.
/// </summary>
public sealed class ShadowSettings
{
    private int _resolution = 1024;
    private float _depthBias = 0.001f;
    private float _normalBias;
    private float _maxDistance = 100.0f;

    public bool Enabled { get; set; } = false;

    public int Resolution
    {
        get => _resolution;
        set => _resolution = NormalizeResolution(value);
    }

    public float DepthBias
    {
        get => _depthBias;
        set => _depthBias = NormalizeNonNegative(value);
    }

    public float NormalBias
    {
        get => _normalBias;
        set => _normalBias = NormalizeNonNegative(value);
    }

    public float MaxDistance
    {
        get => _maxDistance;
        set => _maxDistance = NormalizeNonNegative(value);
    }

    public void ResetToDefaults()
    {
        Enabled = false;
        Resolution = 1024;
        DepthBias = 0.001f;
        NormalBias = 0.0f;
        MaxDistance = 100.0f;
    }

    public void Load(JObject? element)
    {
        ResetToDefaults();

        if (element is null)
        {
            return;
        }

        if (element.TryGetValue("enabled", StringComparison.OrdinalIgnoreCase, out var enabledNode))
        {
            Enabled = enabledNode.Value<bool>();
        }

        if (element.TryGetValue("resolution", StringComparison.OrdinalIgnoreCase, out var resolutionNode))
        {
            Resolution = resolutionNode.Value<int>();
        }

        if (element.TryGetValue("depth_bias", StringComparison.OrdinalIgnoreCase, out var depthBiasNode))
        {
            DepthBias = depthBiasNode.Value<float>();
        }

        if (element.TryGetValue("normal_bias", StringComparison.OrdinalIgnoreCase, out var normalBiasNode))
        {
            NormalBias = normalBiasNode.Value<float>();
        }

        if (element.TryGetValue("max_distance", StringComparison.OrdinalIgnoreCase, out var maxDistanceNode))
        {
            MaxDistance = maxDistanceNode.Value<float>();
        }
    }

    public void Save(JObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        element["enabled"] = Enabled;
        element["resolution"] = Resolution;
        element["depth_bias"] = DepthBias;
        element["normal_bias"] = NormalBias;
        element["max_distance"] = MaxDistance;
    }

    public void CopyFrom(ShadowSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Enabled = other.Enabled;
        Resolution = other.Resolution;
        DepthBias = other.DepthBias;
        NormalBias = other.NormalBias;
        MaxDistance = other.MaxDistance;
    }

    private static int NormalizeResolution(int value)
        => Math.Max(1, value);

    private static float NormalizeNonNegative(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 0.0f;
        }

        return MathF.Max(0.0f, value);
    }
}