using CasaEngine.Framework.Audio.Mixing;

namespace CasaEngine.Framework.Audio.Effects;

/// <summary>
/// Ducking insert (plan decisions P18 and P20): it attenuates the bus it is inserted on, the target, by
/// <see cref="DepthDb"/> while the level of another bus, the <see cref="Source"/>, is above <see cref="ThresholdDb"/>
/// (typically dialogue ducking the music), with an attack and a release.
/// </summary>
/// <remarks>
/// <para>
/// The detector is the one of <see cref="CompressorEffect"/>, D. Giannoulis, M. Massberg and J. D. Reiss, "Digital Dynamic
/// Range Compressor Design - A Tutorial and Analysis", J. Audio Eng. Soc., vol. 60, no. 6, June 2012: a gain computer
/// followed by the smooth branching peak detector. The gain computer is the hard-knee characteristic of Eq. (4) turned into
/// a gain reduction as in Eq. (23): the reduction x_L is <see cref="DepthDb"/> while the level of the source is above the
/// threshold and 0 otherwise (a ducker has a fixed depth, not a ratio). The branching detector of Eq. (16), with the
/// coefficients of Eq. (7) a = exp(-1 / (tau fs)), smooths x_L (attack coefficient while it grows, release coefficient
/// otherwise), and the result is applied as a linear gain, Eq. (1), to both channels of the target buffer.
/// </para>
/// <para>
/// Two choices the paper does not make. The source level is the larger absolute value of its two channels (linked, like the
/// compressor), scaled by the own gain of the source bus at the end of the block (so a muted or faded source ducks nothing),
/// and it goes through a linear peak follower that decays with a time constant of <see cref="PeakHoldSeconds"/> before it is
/// compared with the threshold: without it the level of a periodic signal falls below the threshold at each zero crossing
/// and the reduction would flutter at the pitch of the voice. The release therefore starts when the follower falls under the
/// threshold, a few tens of milliseconds after the source stops, then the release time constant applies.
/// </para>
/// <para>
/// The source is read in the same block: the software mixer mixes the source before the target (the ducking relation is an
/// edge of the order, like a child to its parent or a send to its return bus) and the relation cannot make a cycle (see
/// <see cref="AudioBus.AddEffect"/>). The source buffer read is the one after the insert effects of the source and before
/// its gain. The target is attenuated at the position of this effect among its insert effects, so what the target's children
/// and its earlier effects produced is ducked, and its sends carry the ducked signal.
/// </para>
/// <para>
/// Only on a backend with <see cref="IAudioBusBackend"/>; elsewhere the effect is accepted but absent, and one line is logged.
/// </para>
/// </remarks>
public sealed class DuckingEffect : AudioEffect
{
    public const float MaxDepthDb = 80f;
    public const float MinThresholdDb = -120f;
    public const float MinTimeSeconds = 0.0001f;
    public const float MaxTimeSeconds = 10f;

    /// <summary>Time constant of the peak follower that smooths the source level before the threshold test.</summary>
    public const float PeakHoldSeconds = 0.05f;

    internal sealed record Parameters(float DepthDb, float ThresholdDb, float AttackSeconds, float ReleaseSeconds);

    private Parameters _parameters;

    /// <param name="source">The bus whose level drives the ducking. Must not be null.</param>
    /// <param name="depthDb">Attenuation of the target, in dB, while the source is above the threshold, in [0, 80].</param>
    /// <param name="thresholdDb">Level of the source (dBFS) above which it ducks, in [-120, 0].</param>
    /// <param name="attackSeconds">Time constant of the attenuation growing.</param>
    /// <param name="releaseSeconds">Time constant of the attenuation going away.</param>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    public DuckingEffect(AudioBus source, float depthDb = 12f, float thresholdDb = -40f, float attackSeconds = 0.02f, float releaseSeconds = 0.4f)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        _parameters = new Parameters(
            Sanitize(depthDb, 12f, 0f, MaxDepthDb),
            Sanitize(thresholdDb, -40f, MinThresholdDb, 0f),
            Sanitize(attackSeconds, 0.02f, MinTimeSeconds, MaxTimeSeconds),
            Sanitize(releaseSeconds, 0.4f, MinTimeSeconds, MaxTimeSeconds));
    }

    /// <summary>The bus whose level drives the ducking; fixed at construction.</summary>
    public AudioBus Source { get; }

    /// <summary>Attenuation of the target in dB while the source is above the threshold, in [0, 80].</summary>
    public float DepthDb
    {
        get => Volatile.Read(ref _parameters).DepthDb;
        set => Publish(Volatile.Read(ref _parameters) with { DepthDb = Sanitize(value, DepthDb, 0f, MaxDepthDb) });
    }

    /// <summary>Level of the source in dBFS above which it ducks the target, in [-120, 0].</summary>
    public float ThresholdDb
    {
        get => Volatile.Read(ref _parameters).ThresholdDb;
        set => Publish(Volatile.Read(ref _parameters) with { ThresholdDb = Sanitize(value, ThresholdDb, MinThresholdDb, 0f) });
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

    /// <summary>Not used: a ducking needs the buffer of its source, see <see cref="ProcessDucking"/>; the mixer calls that one.</summary>
    internal override void Process(ref EffectDspState state, Span<float> interleavedStereo, int frameCount, int sampleRate)
    {
    }

    /// <summary>
    /// Audio thread. Attenuates <paramref name="target"/> in place by the gain the level of <paramref name="source"/> asks for
    /// (<paramref name="sourceGain"/> being the own gain of the source bus). Must not allocate, lock or log.
    /// </summary>
    internal void ProcessDucking(ref EffectDspState state, Span<float> target, ReadOnlySpan<float> source, float sourceGain, int frameCount, int sampleRate)
    {
        var parameters = Volatile.Read(ref _parameters);

        if (!ReferenceEquals(state.AppliedParameters, parameters))
        {
            // Eq. (7): a = exp(-1 / (tau fs)).
            state.AttackCoefficient = Math.Exp(-1.0 / (parameters.AttackSeconds * sampleRate));
            state.ReleaseCoefficient = Math.Exp(-1.0 / (parameters.ReleaseSeconds * sampleRate));
            state.HoldCoefficient = Math.Exp(-1.0 / (PeakHoldSeconds * sampleRate));
            state.AppliedParameters = parameters;
        }

        // Comparing the level with the threshold in the linear domain is the same test as in dB, without a logarithm per sample.
        var thresholdLinear = Math.Pow(10.0, parameters.ThresholdDb / 20.0);
        double depth = parameters.DepthDb;
        var attack = state.AttackCoefficient;
        var release = state.ReleaseCoefficient;
        var hold = state.HoldCoefficient;
        var envelope = state.GainReductionDb;
        var peak = state.PeakLevel;

        for (var i = 0; i < frameCount * 2; i += 2)
        {
            double level = Math.Max(Math.Abs(source[i]), Math.Abs(source[i + 1])) * sourceGain;
            peak = Math.Max(level, peak * hold);

            if (peak < EffectDspState.DenormalFlush)
            {
                peak = 0.0;
            }

            // Eq. (4) with a fixed depth, then Eq. (23): x_L is the depth above the threshold, 0 below.
            var reduction = peak > thresholdLinear ? depth : 0.0;

            // Eq. (16).
            envelope = reduction > envelope
                ? (attack * envelope) + ((1.0 - attack) * reduction)
                : (release * envelope) + ((1.0 - release) * reduction);

            if (envelope < EffectDspState.DenormalFlush)
            {
                envelope = 0.0;
            }

            var gain = (float)Math.Exp(-envelope * 0.11512925464970229); // Eq. (1), dB to linear: ln(10) / 20
            target[i] *= gain;
            target[i + 1] *= gain;
        }

        state.GainReductionDb = envelope;
        state.PeakLevel = peak;
    }
}
