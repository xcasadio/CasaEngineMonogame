namespace CasaEngine.Framework.Audio.Effects;

/// <summary>
/// Feed-forward peak limiter, built on the compressor design of <see cref="CompressorEffect"/> (D. Giannoulis,
/// M. Massberg and J. D. Reiss, "Digital Dynamic Range Compressor Design - A Tutorial and Analysis", J. Audio Eng. Soc.,
/// vol. 60, no. 6, June 2012): the same level detector, gain computer and smooth branching peak detector, with the
/// ratio taken to infinity and a hard knee.
/// </summary>
/// <remarks>
/// <para>
/// Per sample, with x_G the level of the input in dB and C the ceiling: the static characteristic of Eq. (4) with
/// R = infinity and W = 0 is y_G = min(x_G, C), so the gain reduction of Eq. (23) is x_L = max(0, x_G - C). It is
/// smoothed by the branching detector of Eq. (16) (attack coefficient while it grows, release coefficient otherwise,
/// a = exp(-1 / (tau fs)), Eq. (7)) and applied as a linear gain to both channels (linked: the stereo image does not move).
/// </para>
/// <para>
/// There is no look-ahead, so the smoothed gain lags a sudden overshoot by a few attack time constants, and between the
/// peaks of a periodic signal it relaxes a little (release). The samples that the gain leaves above the ceiling are cut at
/// the ceiling (a clamp, the one addition to the design of the paper), so the output never exceeds the ceiling; the clamp
/// only acts in those gaps and is the sound of a hard clip at the ceiling, which the gain stage then removes from the
/// steady state. The mixer keeps its own hard clip at +-1 after the limiter as the last resort.
/// </para>
/// <para>
/// It is the default protection of the Master bus under the software backend (<see cref="AudioService.MasterLimiter"/>,
/// ceiling -1 dBFS): it runs after the Master gain and before the hard clip. It can also be inserted on any bus.
/// </para>
/// </remarks>
public sealed class LimiterEffect : AudioEffect
{
    public const float DefaultCeilingDb = -1f;
    public const float MinCeilingDb = -60f;
    public const float MinTimeSeconds = 0.00005f;
    public const float MaxTimeSeconds = 10f;

    private const double LevelFloorDb = -120.0;
    private const double Ln10Over20 = 0.11512925464970229; // ln(10) / 20

    internal sealed record Parameters(bool IsEnabled, float CeilingDb, float AttackSeconds, float ReleaseSeconds);

    private Parameters _parameters;

    public LimiterEffect(float ceilingDb = DefaultCeilingDb, float attackSeconds = 0.0002f, float releaseSeconds = 0.1f)
    {
        _parameters = new Parameters(
            true,
            Sanitize(ceilingDb, DefaultCeilingDb, MinCeilingDb, 0f),
            Sanitize(attackSeconds, 0.0002f, MinTimeSeconds, MaxTimeSeconds),
            Sanitize(releaseSeconds, 0.1f, MinTimeSeconds, MaxTimeSeconds));
    }

    /// <summary>When false the limiter passes the signal untouched (its envelope is kept at rest).</summary>
    public bool IsEnabled
    {
        get => Volatile.Read(ref _parameters).IsEnabled;
        set => Publish(Volatile.Read(ref _parameters) with { IsEnabled = value });
    }

    /// <summary>Ceiling C in dBFS, in [-60, 0].</summary>
    public float CeilingDb
    {
        get => Volatile.Read(ref _parameters).CeilingDb;
        set => Publish(Volatile.Read(ref _parameters) with { CeilingDb = Sanitize(value, CeilingDb, MinCeilingDb, 0f) });
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

    private static float Sanitize(float value, float fallback, float min, float max)
    {
        return float.IsNaN(value) ? fallback : Math.Clamp(value, min, max);
    }

    private void Publish(Parameters parameters)
    {
        Volatile.Write(ref _parameters, parameters);
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

        if (!parameters.IsEnabled)
        {
            state.GainReductionDb = 0.0;
            return;
        }

        double ceiling = parameters.CeilingDb;
        var ceilingLinear = (float)Math.Exp(ceiling * Ln10Over20);
        var attack = state.AttackCoefficient;
        var release = state.ReleaseCoefficient;
        var envelope = state.GainReductionDb;

        for (var i = 0; i < frameCount * 2; i += 2)
        {
            var left = interleavedStereo[i];
            var right = interleavedStereo[i + 1];
            double level = Math.Max(Math.Abs(left), Math.Abs(right));
            var inputDb = level > 1e-6 ? 20.0 * Math.Log10(level) : LevelFloorDb;

            // Eq. (4) with R = infinity and W = 0, then Eq. (23): x_L = x_G - min(x_G, C).
            var reduction = inputDb > ceiling ? inputDb - ceiling : 0.0;

            // Eq. (16).
            envelope = reduction > envelope
                ? (attack * envelope) + ((1.0 - attack) * reduction)
                : (release * envelope) + ((1.0 - release) * reduction);

            if (envelope < EffectDspState.DenormalFlush)
            {
                envelope = 0.0;
            }

            var gain = (float)Math.Exp(-envelope * Ln10Over20);
            interleavedStereo[i] = Math.Clamp(left * gain, -ceilingLinear, ceilingLinear);
            interleavedStereo[i + 1] = Math.Clamp(right * gain, -ceilingLinear, ceilingLinear);
        }

        state.GainReductionDb = envelope;
    }
}
