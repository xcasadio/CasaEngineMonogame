using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using CasaEngine.Core.Serialization;

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
    private Vector3 _shadowColor = Vector3.Zero;
    private float _filterRadius;
    private float _fadeDistance;
    private float _depthRange;

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

    /// <summary>
    /// How the shadow factor is applied to lit surfaces. <see cref="ShadowMode.OccludeDirectLight"/> (default) is the
    /// historical behaviour.
    /// </summary>
    public ShadowMode Mode { get; set; } = ShadowMode.OccludeDirectLight;

    /// <summary>
    /// Colour a fully shadowed pixel is multiplied by in <see cref="ShadowMode.MultiplyLitColor"/> (default black). Each
    /// component is kept in [0, 1]; a non-finite component becomes 0.
    /// </summary>
    public Vector3 ShadowColor
    {
        get => _shadowColor;
        set => _shadowColor = NormalizeColor(value);
    }

    /// <summary>
    /// Radius, in shadow-map texels, of the Poisson-disc filter that softens shadow edges. 0 (default) keeps the 4-tap filter
    /// of earlier versions; a negative or non-finite value becomes 0.
    /// </summary>
    public float FilterRadius
    {
        get => _filterRadius;
        set => _filterRadius = NormalizeNonNegative(value);
    }

    /// <summary>
    /// Width, in world units, of the band before <see cref="MaxDistance"/> over which the shadow fades linearly to "lit",
    /// measured from the centre of the shadow map as a square. 0 (default) keeps the sharp cut at the edge of the map. A
    /// negative or non-finite value becomes 0 and a value above <see cref="MaxDistance"/> reads back as <see cref="MaxDistance"/>.
    /// </summary>
    public float FadeDistance
    {
        get => MathF.Min(_fadeDistance, _maxDistance);
        set => _fadeDistance = NormalizeNonNegative(value);
    }

    /// <summary>
    /// Depth extent of the shadow map, in world units, kept separate from its lateral extent (<see cref="MaxDistance"/>). 0
    /// (default) makes the depth follow <see cref="MaxDistance"/> as in earlier versions; a positive value places the light that
    /// far upstream of the camera and the far plane at twice that distance (at least 1). A negative or non-finite value becomes 0.
    /// </summary>
    public float DepthRange
    {
        get => _depthRange;
        set => _depthRange = NormalizeNonNegative(value);
    }

    public void ResetToDefaults()
    {
        Enabled = false;
        Resolution = 1024;
        DepthBias = 0.001f;
        NormalBias = 0.0f;
        MaxDistance = 100.0f;
        Mode = ShadowMode.OccludeDirectLight;
        ShadowColor = Vector3.Zero;
        FilterRadius = 0.0f;
        FadeDistance = 0.0f;
        DepthRange = 0.0f;
    }

    public void Load(JObject element)
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

        if (element.TryGetValue("mode", StringComparison.OrdinalIgnoreCase, out var modeNode))
        {
            Mode = modeNode.GetEnum<ShadowMode>();
        }

        if (element.TryGetValue("shadow_color", StringComparison.OrdinalIgnoreCase, out var shadowColorNode))
        {
            ShadowColor = shadowColorNode.GetVector3();
        }

        if (element.TryGetValue("filter_radius", StringComparison.OrdinalIgnoreCase, out var filterRadiusNode))
        {
            FilterRadius = filterRadiusNode.Value<float>();
        }

        if (element.TryGetValue("fade_distance", StringComparison.OrdinalIgnoreCase, out var fadeDistanceNode))
        {
            FadeDistance = fadeDistanceNode.Value<float>();
        }

        if (element.TryGetValue("depth_range", StringComparison.OrdinalIgnoreCase, out var depthRangeNode))
        {
            DepthRange = depthRangeNode.Value<float>();
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

        // The fields below are optional in the file: written only when they differ from the defaults, so existing worlds keep
        // their format, and a stale key is removed.
        if (Mode != ShadowMode.OccludeDirectLight)
        {
            element["mode"] = Mode.ToString();
        }
        else
        {
            element.Remove("mode");
        }

        if (ShadowColor != Vector3.Zero)
        {
            element["shadow_color"] = new JObject
            {
                ["x"] = ShadowColor.X,
                ["y"] = ShadowColor.Y,
                ["z"] = ShadowColor.Z,
            };
        }
        else
        {
            element.Remove("shadow_color");
        }

        if (FilterRadius > 0.0f)
        {
            element["filter_radius"] = FilterRadius;
        }
        else
        {
            element.Remove("filter_radius");
        }

        if (FadeDistance > 0.0f)
        {
            element["fade_distance"] = FadeDistance;
        }
        else
        {
            element.Remove("fade_distance");
        }

        if (DepthRange > 0.0f)
        {
            element["depth_range"] = DepthRange;
        }
        else
        {
            element.Remove("depth_range");
        }
    }

    public void CopyFrom(ShadowSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Enabled = other.Enabled;
        Resolution = other.Resolution;
        DepthBias = other.DepthBias;
        NormalBias = other.NormalBias;
        MaxDistance = other.MaxDistance;
        Mode = other.Mode;
        ShadowColor = other.ShadowColor;
        FilterRadius = other.FilterRadius;
        FadeDistance = other.FadeDistance;
        DepthRange = other.DepthRange;
    }

    private static int NormalizeResolution(int value)
        => Math.Max(1, value);

    private static Vector3 NormalizeColor(Vector3 value)
        => new(NormalizeUnit(value.X), NormalizeUnit(value.Y), NormalizeUnit(value.Z));

    private static float NormalizeUnit(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 0.0f;
        }

        return MathF.Min(1.0f, MathF.Max(0.0f, value));
    }

    private static float NormalizeNonNegative(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 0.0f;
        }

        return MathF.Max(0.0f, value);
    }
}