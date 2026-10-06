using System.Numerics;

namespace CasaEngine.Framework.Audio.Spatial;

/// <summary>Doppler pitch ratio. Pure and allocation-free.</summary>
internal static class AudioDoppler
{
    /// <summary>Default speed of sound of the OpenAL 1.1 specification, section 3.5.2, in world units per second.</summary>
    public const float DefaultSpeedOfSound = 343.3f;

    /// <summary>Lowest ratio returned (engine addition).</summary>
    public const float MinRatio = 0.25f;

    /// <summary>Highest ratio returned (engine addition).</summary>
    public const float MaxRatio = 4f;

    /// <summary>
    /// Frequency ratio f'/f. Formula: OpenAL 1.1 specification, section 3.5.2
    /// (https://www.openal.org/documentation/openal-1.1-specification.pdf).
    /// Engine additions: a denominator at or below 1e-6 gives <see cref="MaxRatio"/>; the result is clamped to
    /// [<see cref="MinRatio"/>, <see cref="MaxRatio"/>]; a non-positive Doppler factor or speed of sound, a zero
    /// distance, no spatialization or any non-finite input gives 1. In 2D, positions and velocities are projected on X/Y.
    /// </summary>
    public static float Ratio(
        AudioSpatialMode mode,
        Vector3 listenerPosition,
        Vector3 listenerVelocity,
        Vector3 sourcePosition,
        Vector3 sourceVelocity,
        float speedOfSound,
        float dopplerFactor)
    {
        if ((mode != AudioSpatialMode.Spatial2D && mode != AudioSpatialMode.Spatial3D)
            || !(dopplerFactor > 0f) || !(speedOfSound > 0f)
            || !float.IsFinite(dopplerFactor) || !float.IsFinite(speedOfSound))
        {
            return 1f;
        }

        // SL = source to listener vector.
        var sl = listenerPosition - sourcePosition;

        if (mode == AudioSpatialMode.Spatial2D)
        {
            sl.Z = 0f;
            listenerVelocity.Z = 0f;
            sourceVelocity.Z = 0f;
        }

        var magnitude = sl.Length();

        if (!float.IsFinite(magnitude) || magnitude < 1e-9f
            || !float.IsFinite(listenerVelocity.LengthSquared()) || !float.IsFinite(sourceVelocity.LengthSquared()))
        {
            return 1f;
        }

        // Section 3.5.2: vls = DotProduct(SL, LV) / Mag(SL); vss = DotProduct(SL, SV) / Mag(SL)
        var vls = Vector3.Dot(sl, listenerVelocity) / magnitude;
        var vss = Vector3.Dot(sl, sourceVelocity) / magnitude;

        // Section 3.5.2: vss = min(vss, SS/DF); vls = min(vls, SS/DF)
        var bound = speedOfSound / dopplerFactor;
        vss = Math.Min(vss, bound);
        vls = Math.Min(vls, bound);

        // Section 3.5.2: f' = f * (SS - DF*vls) / (SS - DF*vss)
        var denominator = speedOfSound - dopplerFactor * vss;

        if (!(denominator > 1e-6f))
        {
            return MaxRatio;
        }

        var ratio = (speedOfSound - dopplerFactor * vls) / denominator;

        return float.IsFinite(ratio) ? Math.Clamp(ratio, MinRatio, MaxRatio) : 1f;
    }
}
