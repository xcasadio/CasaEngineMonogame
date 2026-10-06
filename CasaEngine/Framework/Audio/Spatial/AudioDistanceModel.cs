namespace CasaEngine.Framework.Audio.Spatial;

/// <summary>
/// Distance attenuation models of the OpenAL 1.1 specification, section 3.4
/// (https://www.openal.org/documentation/openal-1.1-specification.pdf).
/// </summary>
public enum AudioDistanceModel
{
    /// <summary>No distance attenuation.</summary>
    None = 0,

    /// <summary>Inverse distance rolloff (section 3.4.1).</summary>
    InverseDistance = 1,

    /// <summary>Inverse distance rolloff, distance clamped to the reference and maximum distances (section 3.4.2).</summary>
    InverseDistanceClamped = 2,

    /// <summary>Linear distance rolloff (section 3.4.3).</summary>
    LinearDistance = 3,

    /// <summary>Linear distance rolloff, distance clamped to the reference and maximum distances (section 3.4.4).</summary>
    LinearDistanceClamped = 4,

    /// <summary>Exponential distance rolloff (section 3.4.5).</summary>
    ExponentDistance = 5,

    /// <summary>Exponential distance rolloff, distance clamped to the reference and maximum distances (section 3.4.6).</summary>
    ExponentDistanceClamped = 6
}
