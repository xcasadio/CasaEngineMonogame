using CasaEngine.Core.Serialization;
using Newtonsoft.Json.Linq;

namespace CasaEngine.Framework.Rendering.Environment;

/// <summary>
/// Per-world colour pipeline settings. The defaults (<see cref="ColorPipelineMode.Legacy"/>) keep every existing scene
/// unchanged. Serialized by <see cref="WorldEnvironmentSettings"/> under <c>color_pipeline</c>.
/// </summary>
public sealed class ColorPipelineSettings
{
    private float _exposure = DefaultExposure;
    private float _bloomThreshold = DefaultBloomThreshold;
    private float _bloomIntensity = DefaultBloomIntensity;
    private float _bloomRadius = DefaultBloomRadius;

    public const float DefaultExposure = 1.0f;

    public const float DefaultBloomThreshold = 1.0f;

    public const float DefaultBloomIntensity = 0.5f;

    public const float DefaultBloomRadius = 1.0f;

    public ColorPipelineMode Mode { get; set; } = ColorPipelineMode.Legacy;

    public TonemapOperator Tonemapper { get; set; } = TonemapOperator.AcesApprox;

    /// <summary>
    /// Scalar exposure applied to the scene colour before the tonemapping operator. A negative or non-finite value
    /// becomes 0.
    /// </summary>
    public float Exposure
    {
        get => _exposure;
        set => _exposure = NormalizeExposure(value);
    }

    /// <summary>
    /// Adds a blurred copy of the bright part of the HDR scene before the tonemapping (ADR-0105). Off by default, and
    /// without any effect in <see cref="ColorPipelineMode.Legacy"/>.
    /// </summary>
    public bool BloomEnabled { get; set; }

    /// <summary>
    /// Scene luminance, in linear light, above which a pixel feeds the bloom (soft knee). A negative or non-finite value
    /// becomes 0.
    /// </summary>
    public float BloomThreshold
    {
        get => _bloomThreshold;
        set => _bloomThreshold = NormalizeNonNegative(value);
    }

    /// <summary>Scale of the blurred bright part added to the scene. A negative or non-finite value becomes 0.</summary>
    public float BloomIntensity
    {
        get => _bloomIntensity;
        set => _bloomIntensity = NormalizeNonNegative(value);
    }

    /// <summary>
    /// Scale of the blur tap spacing: 1 is the base reach, larger values spread the glow further. A negative or non-finite
    /// value becomes 0 (no blur).
    /// </summary>
    public float BloomRadius
    {
        get => _bloomRadius;
        set => _bloomRadius = NormalizeNonNegative(value);
    }

    /// <summary>True when every field has its default value.</summary>
    public bool IsDefault => Mode == ColorPipelineMode.Legacy
        && Tonemapper == TonemapOperator.AcesApprox
        && _exposure == DefaultExposure
        && !BloomEnabled
        && _bloomThreshold == DefaultBloomThreshold
        && _bloomIntensity == DefaultBloomIntensity
        && _bloomRadius == DefaultBloomRadius;

    public void ResetToDefaults()
    {
        Mode = ColorPipelineMode.Legacy;
        Tonemapper = TonemapOperator.AcesApprox;
        Exposure = DefaultExposure;
        BloomEnabled = false;
        BloomThreshold = DefaultBloomThreshold;
        BloomIntensity = DefaultBloomIntensity;
        BloomRadius = DefaultBloomRadius;
    }

    public void Load(JObject element)
    {
        ResetToDefaults();

        if (element is null)
        {
            return;
        }

        if (element.TryGetValue("mode", StringComparison.OrdinalIgnoreCase, out var modeNode))
        {
            Mode = modeNode.GetEnum<ColorPipelineMode>();
        }

        if (element.TryGetValue("tonemapper", StringComparison.OrdinalIgnoreCase, out var tonemapperNode))
        {
            Tonemapper = tonemapperNode.GetEnum<TonemapOperator>();
        }

        if (element.TryGetValue("exposure", StringComparison.OrdinalIgnoreCase, out var exposureNode))
        {
            Exposure = exposureNode.Value<float>();
        }

        if (element.TryGetValue("bloom_enabled", StringComparison.OrdinalIgnoreCase, out var bloomEnabledNode))
        {
            BloomEnabled = bloomEnabledNode.Value<bool>();
        }

        if (element.TryGetValue("bloom_threshold", StringComparison.OrdinalIgnoreCase, out var bloomThresholdNode))
        {
            BloomThreshold = bloomThresholdNode.Value<float>();
        }

        if (element.TryGetValue("bloom_intensity", StringComparison.OrdinalIgnoreCase, out var bloomIntensityNode))
        {
            BloomIntensity = bloomIntensityNode.Value<float>();
        }

        if (element.TryGetValue("bloom_radius", StringComparison.OrdinalIgnoreCase, out var bloomRadiusNode))
        {
            BloomRadius = bloomRadiusNode.Value<float>();
        }
    }

    public void Save(JObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        element["mode"] = Mode.ToString();
        element["tonemapper"] = Tonemapper.ToString();
        element["exposure"] = Exposure;
        element["bloom_enabled"] = BloomEnabled;
        element["bloom_threshold"] = BloomThreshold;
        element["bloom_intensity"] = BloomIntensity;
        element["bloom_radius"] = BloomRadius;
    }

    public void CopyFrom(ColorPipelineSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Mode = other.Mode;
        Tonemapper = other.Tonemapper;
        Exposure = other.Exposure;
        BloomEnabled = other.BloomEnabled;
        BloomThreshold = other.BloomThreshold;
        BloomIntensity = other.BloomIntensity;
        BloomRadius = other.BloomRadius;
    }

    private static float NormalizeExposure(float value)
    {
        return NormalizeNonNegative(value);
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
