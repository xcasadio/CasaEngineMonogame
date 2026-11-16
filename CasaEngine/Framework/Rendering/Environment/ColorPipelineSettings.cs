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
    private float _ssaoRadius = DefaultSsaoRadius;
    private float _ssaoIntensity = DefaultSsaoIntensity;
    private float _ssaoBias = DefaultSsaoBias;

    public const float DefaultExposure = 1.0f;

    public const float DefaultBloomThreshold = 1.0f;

    public const float DefaultBloomIntensity = 0.5f;

    public const float DefaultBloomRadius = 1.0f;

    public const float DefaultSsaoRadius = 0.5f;

    public const float DefaultSsaoIntensity = 1.0f;

    public const float DefaultSsaoBias = 0.02f;

    public ColorPipelineMode Mode { get; set; } = ColorPipelineMode.Legacy;

    public TonemapOperator Tonemapper { get; set; } = TonemapOperator.AcesApprox;

    /// <summary>
    /// Screen-space anti-aliasing applied after the tonemapping (ADR-0112). <see cref="AntiAliasingMode.None"/> by default,
    /// and without any effect in <see cref="ColorPipelineMode.Legacy"/>.
    /// </summary>
    public AntiAliasingMode AntiAliasing { get; set; } = AntiAliasingMode.None;

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
    /// Adds a blurred copy of the bright part of the HDR scene before the tonemapping (ADR-0107). Off by default, and
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

    /// <summary>
    /// Screen-space ambient occlusion (ADR-0115): the view draws a scene depth pre-pass that the SSAO pass reads. Off by
    /// default, and without any effect in <see cref="ColorPipelineMode.Legacy"/>, in a view with a non-perspective projection and in
    /// a view that renders its scene and its interface in one go.
    /// </summary>
    public bool SsaoEnabled { get; set; }

    /// <summary>Radius of the occlusion hemisphere, in world units. A negative or non-finite value becomes 0.</summary>
    public float SsaoRadius
    {
        get => _ssaoRadius;
        set => _ssaoRadius = NormalizeNonNegative(value);
    }

    /// <summary>Strength of the occlusion: 0 leaves the image untouched, 1 applies the full occlusion. A negative or non-finite value becomes 0.</summary>
    public float SsaoIntensity
    {
        get => _ssaoIntensity;
        set => _ssaoIntensity = NormalizeNonNegative(value);
    }

    /// <summary>Depth bias, in world units, that keeps a flat surface from occluding itself. A negative or non-finite value becomes 0.</summary>
    public float SsaoBias
    {
        get => _ssaoBias;
        set => _ssaoBias = NormalizeNonNegative(value);
    }

    /// <summary>True when every field has its default value.</summary>
    public bool IsDefault => Mode == ColorPipelineMode.Legacy
        && Tonemapper == TonemapOperator.AcesApprox
        && AntiAliasing == AntiAliasingMode.None
        && _exposure == DefaultExposure
        && !BloomEnabled
        && _bloomThreshold == DefaultBloomThreshold
        && _bloomIntensity == DefaultBloomIntensity
        && _bloomRadius == DefaultBloomRadius
        && !SsaoEnabled
        && _ssaoRadius == DefaultSsaoRadius
        && _ssaoIntensity == DefaultSsaoIntensity
        && _ssaoBias == DefaultSsaoBias;

    public void ResetToDefaults()
    {
        Mode = ColorPipelineMode.Legacy;
        Tonemapper = TonemapOperator.AcesApprox;
        AntiAliasing = AntiAliasingMode.None;
        Exposure = DefaultExposure;
        BloomEnabled = false;
        BloomThreshold = DefaultBloomThreshold;
        BloomIntensity = DefaultBloomIntensity;
        BloomRadius = DefaultBloomRadius;
        SsaoEnabled = false;
        SsaoRadius = DefaultSsaoRadius;
        SsaoIntensity = DefaultSsaoIntensity;
        SsaoBias = DefaultSsaoBias;
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

        if (element.TryGetValue("anti_aliasing", StringComparison.OrdinalIgnoreCase, out var antiAliasingNode))
        {
            AntiAliasing = antiAliasingNode.GetEnum<AntiAliasingMode>();
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

        if (element.TryGetValue("ssao_enabled", StringComparison.OrdinalIgnoreCase, out var ssaoEnabledNode))
        {
            SsaoEnabled = ssaoEnabledNode.Value<bool>();
        }

        if (element.TryGetValue("ssao_radius", StringComparison.OrdinalIgnoreCase, out var ssaoRadiusNode))
        {
            SsaoRadius = ssaoRadiusNode.Value<float>();
        }

        if (element.TryGetValue("ssao_intensity", StringComparison.OrdinalIgnoreCase, out var ssaoIntensityNode))
        {
            SsaoIntensity = ssaoIntensityNode.Value<float>();
        }

        if (element.TryGetValue("ssao_bias", StringComparison.OrdinalIgnoreCase, out var ssaoBiasNode))
        {
            SsaoBias = ssaoBiasNode.Value<float>();
        }
    }

    public void Save(JObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        element["mode"] = Mode.ToString();
        element["tonemapper"] = Tonemapper.ToString();
        element["anti_aliasing"] = AntiAliasing.ToString();
        element["exposure"] = Exposure;
        element["bloom_enabled"] = BloomEnabled;
        element["bloom_threshold"] = BloomThreshold;
        element["bloom_intensity"] = BloomIntensity;
        element["bloom_radius"] = BloomRadius;
        element["ssao_enabled"] = SsaoEnabled;
        element["ssao_radius"] = SsaoRadius;
        element["ssao_intensity"] = SsaoIntensity;
        element["ssao_bias"] = SsaoBias;
    }

    public void CopyFrom(ColorPipelineSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Mode = other.Mode;
        Tonemapper = other.Tonemapper;
        AntiAliasing = other.AntiAliasing;
        Exposure = other.Exposure;
        BloomEnabled = other.BloomEnabled;
        BloomThreshold = other.BloomThreshold;
        BloomIntensity = other.BloomIntensity;
        BloomRadius = other.BloomRadius;
        SsaoEnabled = other.SsaoEnabled;
        SsaoRadius = other.SsaoRadius;
        SsaoIntensity = other.SsaoIntensity;
        SsaoBias = other.SsaoBias;
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
