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

    public const float DefaultExposure = 1.0f;

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

    /// <summary>True when every field has its default value.</summary>
    public bool IsDefault => Mode == ColorPipelineMode.Legacy
        && Tonemapper == TonemapOperator.AcesApprox
        && _exposure == DefaultExposure;

    public void ResetToDefaults()
    {
        Mode = ColorPipelineMode.Legacy;
        Tonemapper = TonemapOperator.AcesApprox;
        Exposure = DefaultExposure;
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
    }

    public void Save(JObject element)
    {
        ArgumentNullException.ThrowIfNull(element);

        element["mode"] = Mode.ToString();
        element["tonemapper"] = Tonemapper.ToString();
        element["exposure"] = Exposure;
    }

    public void CopyFrom(ColorPipelineSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Mode = other.Mode;
        Tonemapper = other.Tonemapper;
        Exposure = other.Exposure;
    }

    private static float NormalizeExposure(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 0.0f;
        }

        return MathF.Max(0.0f, value);
    }
}
