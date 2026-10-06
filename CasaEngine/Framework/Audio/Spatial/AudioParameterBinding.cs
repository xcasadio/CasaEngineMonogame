namespace CasaEngine.Framework.Audio.Spatial;

/// <summary>
/// Linear mapping from a game parameter to a voice property. Immutable; validation of loaded data is the
/// caller's job, <see cref="Evaluate"/> nevertheless never returns a non-finite value.
/// </summary>
public sealed class AudioParameterBinding
{
    /// <summary>Creates a binding; the values are stored as given.</summary>
    public AudioParameterBinding(
        string parameterName,
        AudioParameterTarget target,
        float inputMin,
        float inputMax,
        float outputMin,
        float outputMax)
    {
        ParameterName = parameterName;
        Target = target;
        InputMin = inputMin;
        InputMax = inputMax;
        OutputMin = outputMin;
        OutputMax = outputMax;
    }

    /// <summary>The name of the game parameter that drives the binding.</summary>
    public string ParameterName { get; }

    /// <summary>The voice property that receives the result.</summary>
    public AudioParameterTarget Target { get; }

    /// <summary>The input value mapped to <see cref="OutputMin"/>.</summary>
    public float InputMin { get; }

    /// <summary>The input value mapped to <see cref="OutputMax"/>.</summary>
    public float InputMax { get; }

    /// <summary>The output for an input at <see cref="InputMin"/>.</summary>
    public float OutputMin { get; }

    /// <summary>The output for an input at <see cref="InputMax"/>.</summary>
    public float OutputMax { get; }

    /// <summary>
    /// Maps <paramref name="input"/>: <c>t = (input - InputMin) / (InputMax - InputMin)</c> clamped to [0, 1], output
    /// <c>lerp(OutputMin, OutputMax, t)</c>, clamped to [0, 1] for <see cref="AudioParameterTarget.Volume"/> and to
    /// [-1, 1] octave for <see cref="AudioParameterTarget.Pitch"/>. A degenerate input range is a step (t = 1 when
    /// input is at least <see cref="InputMax"/>, else 0); inverted ranges are accepted. A NaN input behaves as t = 0
    /// (the clamped <see cref="OutputMin"/>); the caller treats a never-written parameter as neutral before calling.
    /// A non-finite result (non-finite stored outputs) is replaced by the neutral value (1 for volume, 0 for pitch).
    /// </summary>
    public float Evaluate(float input)
    {
        var range = (double)InputMax - InputMin;
        double t;

        if (double.IsNaN(input))
        {
            t = 0d;
        }
        else if (!(range > 0d) && !(range < 0d))
        {
            t = input >= InputMax ? 1d : 0d;
        }
        else
        {
            t = ((double)input - InputMin) / range;
            t = double.IsNaN(t) ? 0d : Math.Clamp(t, 0d, 1d);
        }

        var value = t >= 1d
            ? OutputMax
            : OutputMin + ((double)OutputMax - OutputMin) * t;

        if (double.IsNaN(value))
        {
            return Target == AudioParameterTarget.Pitch ? 0f : 1f;
        }

        var lower = Target == AudioParameterTarget.Pitch ? -1d : 0d;

        return (float)Math.Clamp(value, lower, 1d);
    }
}
