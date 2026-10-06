namespace CasaEngine.Framework.Audio.Spatial;

/// <summary>
/// Distance attenuation gain. Formulas: OpenAL 1.1 specification, section 3.4
/// (https://www.openal.org/documentation/openal-1.1-specification.pdf). Pure and allocation-free.
/// </summary>
internal static class AudioDistanceAttenuation
{
    /// <summary>
    /// Gain in [0, 1] for a source at <paramref name="distance"/> from the listener. Engine addition (not in the
    /// specification): a formula that cannot be evaluated (division by zero, non-positive inverse denominator,
    /// non-finite result, NaN input, linear model with reference distance equal to maximum distance) gives 1
    /// (no attenuation), and the result is clamped to [0, 1].
    /// </summary>
    public static float Evaluate(
        AudioDistanceModel model,
        float distance,
        float referenceDistance,
        float maxDistance,
        float rolloffFactor)
    {
        if (model == AudioDistanceModel.None
            || float.IsNaN(distance) || float.IsNaN(referenceDistance)
            || float.IsNaN(maxDistance) || float.IsNaN(rolloffFactor))
        {
            return 1f;
        }

        double gain;

        switch (model)
        {
            case AudioDistanceModel.InverseDistance:
                // https://www.openal.org/documentation/openal-1.1-specification.pdf section 3.4.1:
                // gain = REF / (REF + ROLLOFF * (distance - REF))
                gain = Inverse(distance, referenceDistance, rolloffFactor);
                break;

            case AudioDistanceModel.InverseDistanceClamped:
                // Same document, section 3.4.2: distance = max(distance, REF); distance = min(distance, MAX);
                // then the inverse formula.
                gain = Inverse(ClampDistance(distance, referenceDistance, maxDistance), referenceDistance, rolloffFactor);
                break;

            case AudioDistanceModel.LinearDistance:
                // Same document, section 3.4.3: distance = min(distance, MAX);
                // gain = 1 - ROLLOFF * (distance - REF) / (MAX - REF)
                gain = Linear(Math.Min(distance, maxDistance), referenceDistance, maxDistance, rolloffFactor);
                break;

            case AudioDistanceModel.LinearDistanceClamped:
                // Same document, section 3.4.4: distance = max(distance, REF); distance = min(distance, MAX);
                // then the linear formula.
                gain = Linear(ClampDistance(distance, referenceDistance, maxDistance), referenceDistance, maxDistance, rolloffFactor);
                break;

            case AudioDistanceModel.ExponentDistance:
                // Same document, section 3.4.5: gain = (distance / REF) ^ (-ROLLOFF)
                gain = Exponent(distance, referenceDistance, rolloffFactor);
                break;

            case AudioDistanceModel.ExponentDistanceClamped:
                // Same document, section 3.4.6: distance = max(distance, REF); distance = min(distance, MAX);
                // then the exponent formula.
                gain = Exponent(ClampDistance(distance, referenceDistance, maxDistance), referenceDistance, rolloffFactor);
                break;

            default:
                return 1f;
        }

        if (double.IsNaN(gain) || double.IsInfinity(gain))
        {
            return 1f;
        }

        return (float)Math.Clamp(gain, 0d, 1d);
    }

    private static double ClampDistance(double distance, double reference, double max)
    {
        distance = Math.Max(distance, reference);
        return Math.Min(distance, max);
    }

    private static double Inverse(double distance, double reference, double rolloff)
    {
        var denominator = reference + rolloff * (distance - reference);

        // Engine addition: a non-positive denominator is a non-evaluable formula (no attenuation).
        return denominator <= 0d ? double.NaN : reference / denominator;
    }

    private static double Linear(double distance, double reference, double max, double rolloff)
    {
        var range = max - reference;

        // Engine addition: reference == max divides by zero, the formula is not evaluable.
        return range == 0d ? double.NaN : 1d - rolloff * (distance - reference) / range;
    }

    private static double Exponent(double distance, double reference, double rolloff)
    {
        return reference == 0d ? double.NaN : Math.Pow(distance / reference, -rolloff);
    }
}
