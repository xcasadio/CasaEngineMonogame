namespace CasaEngine.Framework.Audio.Effects;

/// <summary>
/// Feed-forward dynamic range compressor insert, following D. Giannoulis, M. Massberg and J. D. Reiss, "Digital
/// Dynamic Range Compressor Design - A Tutorial and Analysis", J. Audio Eng. Soc., vol. 60, no. 6, June 2012,
/// pp. 399-408 (the design they recommend: feed-forward, level detection in the log domain after the gain computer,
/// smooth branching peak detector).
/// </summary>
/// <remarks>
/// <para>
/// Per sample, with x_G the level of the input in dB, T the threshold, R the ratio, W the knee width and M the
/// make-up gain:
/// </para>
/// <list type="number">
/// <item>Static characteristic with a soft knee, Eq. (4): y_G = x_G if 2(x_G - T) &lt; -W;
/// y_G = x_G + (1/R - 1)(x_G - T + W/2)^2 / (2W) if 2|x_G - T| &lt;= W; y_G = T + (x_G - T)/R if 2(x_G - T) &gt; W
/// (a hard knee when W is 0).</item>
/// <item>Gain reduction, Eq. (23): x_L = x_G - y_G.</item>
/// <item>Smooth branching peak detector, Eq. (16): y_L = a_A y_L + (1 - a_A) x_L when x_L &gt; y_L, else
/// y_L = a_R y_L + (1 - a_R) x_L, with a = exp(-1 / (tau fs)), Eq. (7), tau being the attack or release time constant.</item>
/// <item>Control voltage c_dB = -y_L and output y_dB = x_dB + c_dB + M, Eq. (1), applied as a linear gain to both channels.</item>
/// </list>
/// <para>
/// The level x_G is the larger of the absolute values of the two channels (the channels are linked, so the image does
/// not move), floored at -120 dB. The attack and release are time constants: the gain reduction reaches 1 - 1/e of a
/// step after one time constant. A change of parameters takes effect at the next block, keeping the envelope.
/// </para>
/// </remarks>
public sealed class CompressorEffect : AudioEffect
{
    public const float MinThresholdDb = -120f;
    public const float MinRatio = 1f;
    public const float MaxRatio = 1000f;
    public const float MaxKneeDb = 60f;
    public const float MinTimeSeconds = 0.0001f;
    public const float MaxTimeSeconds = 10f;
    public const float MaxMakeupGainDb = 60f;

    private const double LevelFloorDb = -120.0;
    private const double Ln10Over20 = 0.11512925464970229; // ln(10) / 20

    internal sealed record Parameters(float ThresholdDb, float Ratio, float KneeDb, float AttackSeconds, float ReleaseSeconds, float MakeupGainDb);

    private Parameters _parameters;

    public CompressorEffect(float thresholdDb = -18f, float ratio = 4f, float kneeDb = 6f, float attackSeconds = 0.01f, float releaseSeconds = 0.1f, float makeupGainDb = 0f)
    {
        _parameters = new Parameters(
            Sanitize(thresholdDb, -18f, MinThresholdDb, 0f),
            Sanitize(ratio, 4f, MinRatio, MaxRatio),
            Sanitize(kneeDb, 6f, 0f, MaxKneeDb),
            Sanitize(attackSeconds, 0.01f, MinTimeSeconds, MaxTimeSeconds),
            Sanitize(releaseSeconds, 0.1f, MinTimeSeconds, MaxTimeSeconds),
            Sanitize(makeupGainDb, 0f, -MaxMakeupGainDb, MaxMakeupGainDb));
    }

    /// <summary>Threshold T in dB (full scale), in [-120, 0].</summary>
    public float ThresholdDb
    {
        get => Volatile.Read(ref _parameters).ThresholdDb;
        set => Publish(Volatile.Read(ref _parameters) with { ThresholdDb = Sanitize(value, ThresholdDb, MinThresholdDb, 0f) });
    }

    /// <summary>Ratio R of input to output change above the threshold (1 = no compression), in [1, 1000].</summary>
    public float Ratio
    {
        get => Volatile.Read(ref _parameters).Ratio;
        set => Publish(Volatile.Read(ref _parameters) with { Ratio = Sanitize(value, Ratio, MinRatio, MaxRatio) });
    }

    /// <summary>Knee width W in dB, centred on the threshold; 0 is a hard knee.</summary>
    public float KneeDb
    {
        get => Volatile.Read(ref _parameters).KneeDb;
        set => Publish(Volatile.Read(ref _parameters) with { KneeDb = Sanitize(value, KneeDb, 0f, MaxKneeDb) });
    }

    /// <summary>Attack time constant in seconds.</summary>
    public float AttackSeconds
    {
        get => Volatile.Read(ref _parameters).AttackSeconds;
        set => Publish(Volatile.Read(ref _parameters) with { AttackSeconds = Sanitize(value, AttackSeconds, MinTimeSeconds, MaxTimeSeconds) });
    }

    /// <summary>Release time constant in seconds.</summary>
    public float ReleaseSeconds
    {
        get => Volatile.Read(ref _parameters).ReleaseSeconds;
        set => Publish(Volatile.Read(ref _parameters) with { ReleaseSeconds = Sanitize(value, ReleaseSeconds, MinTimeSeconds, MaxTimeSeconds) });
    }

    /// <summary>Make-up gain M in dB.</summary>
    public float MakeupGainDb
    {
        get => Volatile.Read(ref _parameters).MakeupGainDb;
        set => Publish(Volatile.Read(ref _parameters) with { MakeupGainDb = Sanitize(value, MakeupGainDb, -MaxMakeupGainDb, MaxMakeupGainDb) });
    }

    private static float Sanitize(float value, float fallback, float min, float max)
    {
        return float.IsNaN(value) ? fallback : Math.Clamp(value, min, max);
    }

    private void Publish(Parameters parameters)
    {
        Volatile.Write(ref _parameters, parameters);
    }

    internal override object CaptureParameters()
    {
        return Volatile.Read(ref _parameters);
    }

    internal override void RestoreParameters(object parameters)
    {
        if (parameters is Parameters restored)
        {
            Publish(restored);
        }
    }

    /// <summary>Static characteristic, Eq. (4) of the paper: output level in dB for an input level in dB.</summary>
    internal static double StaticOutputDb(double inputDb, double thresholdDb, double ratio, double kneeDb)
    {
        var over = inputDb - thresholdDb;

        if (2.0 * over < -kneeDb)
        {
            return inputDb;
        }

        if (kneeDb > 0.0 && 2.0 * Math.Abs(over) <= kneeDb)
        {
            var knee = over + (kneeDb / 2.0);
            return inputDb + (((1.0 / ratio) - 1.0) * knee * knee / (2.0 * kneeDb));
        }

        return thresholdDb + (over / ratio);
    }

    internal override void Process(ref EffectDspState state, Span<float> interleavedStereo, int frameCount, int sampleRate)
    {
        var parameters = Volatile.Read(ref _parameters);

        if (!ReferenceEquals(state.AppliedParameters, parameters))
        {
            // Eq. (7): a = exp(-1 / (tau fs)).
            state.AttackCoefficient = Math.Exp(-1.0 / (parameters.AttackSeconds * sampleRate));
            state.ReleaseCoefficient = Math.Exp(-1.0 / (parameters.ReleaseSeconds * sampleRate));
            state.AppliedParameters = parameters;
        }

        double threshold = parameters.ThresholdDb;
        double ratio = parameters.Ratio;
        double knee = parameters.KneeDb;
        double makeup = parameters.MakeupGainDb;
        var attack = state.AttackCoefficient;
        var release = state.ReleaseCoefficient;
        var envelope = state.GainReductionDb;

        for (var i = 0; i < frameCount * 2; i += 2)
        {
            var left = interleavedStereo[i];
            var right = interleavedStereo[i + 1];
            double level = Math.Max(Math.Abs(left), Math.Abs(right));
            var inputDb = level > 1e-6 ? 20.0 * Math.Log10(level) : LevelFloorDb;
            var reduction = inputDb - StaticOutputDb(inputDb, threshold, ratio, knee); // Eq. (23)

            // Eq. (16): the attack coefficient while the reduction grows, the release coefficient otherwise.
            envelope = reduction > envelope
                ? (attack * envelope) + ((1.0 - attack) * reduction)
                : (release * envelope) + ((1.0 - release) * reduction);

            // Denormal guard: the released envelope is flushed to zero long before the subnormal range.
            if (envelope < EffectDspState.DenormalFlush)
            {
                envelope = 0.0;
            }

            var gain = (float)Math.Exp((makeup - envelope) * Ln10Over20); // Eq. (1), dB to linear
            interleavedStereo[i] = left * gain;
            interleavedStereo[i + 1] = right * gain;
        }

        state.GainReductionDb = envelope;
    }
}
