namespace CasaEngine.Framework.Audio.Effects;

/// <summary>
/// Freeverb reverberator, the public domain C++ program by "Jezar at Dreampoint", implemented from its description in
/// J. O. Smith III, "Physical Audio Signal Processing", W3K Publishing, 2010, section "Freeverb"
/// (https://ccrma.stanford.edu/~jos/pasp/Freeverb.html and its subsections "Freeverb Main Loop", "Lowpass-Feedback Comb
/// Filter" and "Freeverb Allpass Approximation"). No source code was read or copied.
/// </summary>
/// <remarks>
/// <para>
/// Structure, per the page and its Fig. 3.8 (default settings of the left channel): the stereo input is summed to mono
/// and scaled by a fixed input gain; eight lowpass-feedback comb filters run in parallel, their outputs are summed and
/// go through four allpass sections in series. The delay lengths at 44.1 kHz are, for the left channel, combs 1557,
/// 1617, 1491, 1422, 1277, 1356, 1188, 1116 and allpasses 225, 556, 441, 341; the right channel adds the stereo spread
/// of 23 samples to each of the twelve lengths. Here every length is scaled to the output rate and rounded to the
/// nearest sample (at least 1): <see cref="ScaleDelay"/>.
/// </para>
/// <para>
/// Comb filter: H(z) = z^-N / (1 - f (1 - d) / (1 - d z^-1) z^-N), a delay line whose output is filtered by the unity-gain
/// one-pole lowpass (1 - d) / (1 - d z^-1) and fed back with gain f. Allpass section: H(z) = (-1 + (1 + g) z^-N) / (1 - g z^-N)
/// with g = 0.5 (the page notes that this is an approximation of a true allpass). The page gives the mapping of the
/// user parameters to the coefficients: f = roomsize * 0.28 + 0.7 and d = damp * 0.4 (defaults 0.5 and 0.5, f = 0.84,
/// d = 0.2). Output, per the main loop: outputL = outL wet1 + outR wet2 + inputL dry (and symmetrically for the right
/// channel); the page defaults are wet1 = 1, wet2 = 0, dry = 0.
/// </para>
/// <para>
/// Choices that are not on the page: the input gain is 1/32 (the page does not give it); the wet gain scales wet1 and
/// wet2; the separation s in [0, 1] sets wet1 = (1 + s) / 2 and wet2 = (1 - s) / 2, so s = 1 is the page default
/// (wet1 = 1, wet2 = 0, maximal separation) and s = 0 gives the same signal on both channels (wet1 = wet2). The
/// recursive memories are flushed to zero under 1e-30 so a long tail never reaches the subnormal range.
/// </para>
/// <para>
/// Meant to be inserted on a return bus fed by <see cref="Mixing.AudioBus.SetSend"/>, fully wet (the defaults:
/// <see cref="Dry"/> 0, <see cref="Wet"/> 1). The delay lines are built on the game thread when the effect is added to a
/// bus, so the audio thread allocates nothing.
/// </para>
/// </remarks>
public sealed class ReverbEffect : AudioEffect
{
    public const int CombCount = 8;
    public const int AllpassCount = 4;

    /// <summary>Sample rate the tunings of the page are given at.</summary>
    public const int ReferenceSampleRate = 44100;

    /// <summary>Stereo spread of the page: samples added to each delay length of the right channel (at 44.1 kHz).</summary>
    public const int StereoSpread = 23;

    private const float InputGain = 1f / 32f;
    private const float AllpassFeedback = 0.5f;
    private const float ScaleRoom = 0.28f;
    private const float OffsetRoom = 0.7f;
    private const float ScaleDamp = 0.4f;
    private const float Flush = (float)EffectDspState.DenormalFlush;

    // Fig. 3.8 of the page, left channel, 44.1 kHz.
    private static readonly int[] CombLengths44100 = { 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1116 };
    private static readonly int[] AllpassLengths44100 = { 225, 556, 441, 341 };

    internal sealed record Parameters(float RoomSize, float Damping, float Wet, float Dry, float StereoSeparation);

    private Parameters _parameters;

    public ReverbEffect(float roomSize = 0.5f, float damping = 0.5f, float wet = 1f, float dry = 0f, float stereoSeparation = 1f)
    {
        _parameters = new Parameters(
            Sanitize(roomSize, 0.5f, 0f, 1f),
            Sanitize(damping, 0.5f, 0f, 1f),
            Sanitize(wet, 1f, 0f, 1f),
            Sanitize(dry, 0f, 0f, 1f),
            Sanitize(stereoSeparation, 1f, 0f, 1f));
    }

    /// <summary>Room size in [0, 1] (the page's roomsize): the comb feedback is f = roomsize * 0.28 + 0.7, so 1 is f = 0.98.</summary>
    public float RoomSize
    {
        get => Volatile.Read(ref _parameters).RoomSize;
        set => Publish(Volatile.Read(ref _parameters) with { RoomSize = Sanitize(value, RoomSize, 0f, 1f) });
    }

    /// <summary>Damping in [0, 1] (the page's damp): the lowpass coefficient is d = damp * 0.4.</summary>
    public float Damping
    {
        get => Volatile.Read(ref _parameters).Damping;
        set => Publish(Volatile.Read(ref _parameters) with { Damping = Sanitize(value, Damping, 0f, 1f) });
    }

    /// <summary>Level of the reverberated signal, in [0, 1].</summary>
    public float Wet
    {
        get => Volatile.Read(ref _parameters).Wet;
        set => Publish(Volatile.Read(ref _parameters) with { Wet = Sanitize(value, Wet, 0f, 1f) });
    }

    /// <summary>Level of the input signal passed through next to the reverberation, in [0, 1] (the page's dry; 0 on a return bus).</summary>
    public float Dry
    {
        get => Volatile.Read(ref _parameters).Dry;
        set => Publish(Volatile.Read(ref _parameters) with { Dry = Sanitize(value, Dry, 0f, 1f) });
    }

    /// <summary>Stereo separation in [0, 1]: 1 gives wet1 = 1, wet2 = 0 (page default), 0 the same signal on both channels.</summary>
    public float StereoSeparation
    {
        get => Volatile.Read(ref _parameters).StereoSeparation;
        set => Publish(Volatile.Read(ref _parameters) with { StereoSeparation = Sanitize(value, StereoSeparation, 0f, 1f) });
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

    /// <summary>A delay length of the page (given at 44.1 kHz) scaled to <paramref name="sampleRate"/>, rounded, at least 1.</summary>
    public static int ScaleDelay(int samplesAt44100, int sampleRate)
    {
        return Math.Max(1, (int)Math.Round((double)samplesAt44100 * sampleRate / ReferenceSampleRate, MidpointRounding.AwayFromZero));
    }

    /// <summary>Delay length in samples of comb filter <paramref name="index"/> (0 to 7) of a channel at <paramref name="sampleRate"/>.</summary>
    public static int GetCombLength(int index, bool rightChannel, int sampleRate)
    {
        return ScaleDelay(CombLengths44100[index] + (rightChannel ? StereoSpread : 0), sampleRate);
    }

    /// <summary>Delay length in samples of allpass section <paramref name="index"/> (0 to 3) of a channel at <paramref name="sampleRate"/>.</summary>
    public static int GetAllpassLength(int index, bool rightChannel, int sampleRate)
    {
        return ScaleDelay(AllpassLengths44100[index] + (rightChannel ? StereoSpread : 0), sampleRate);
    }

    internal override object CreateAudioState(int sampleRate)
    {
        return new ReverbState(sampleRate);
    }

    /// <summary>Audio-side memory: the delay lines of both channels and the comb lowpass memories.</summary>
    internal sealed class ReverbState
    {
        public readonly float[][] CombBuffers = new float[CombCount * 2][];
        public readonly int[] CombIndex = new int[CombCount * 2];
        public readonly float[] CombStore = new float[CombCount * 2];
        public readonly float[][] AllpassBuffers = new float[AllpassCount * 2][];
        public readonly int[] AllpassIndex = new int[AllpassCount * 2];
        public object AppliedParameters;
        public float Feedback;
        public float Damp;
        public float Wet1;
        public float Wet2;
        public float Dry;

        public ReverbState(int sampleRate)
        {
            for (var c = 0; c < CombCount; c++)
            {
                CombBuffers[c] = new float[GetCombLength(c, false, sampleRate)];
                CombBuffers[CombCount + c] = new float[GetCombLength(c, true, sampleRate)];
            }

            for (var a = 0; a < AllpassCount; a++)
            {
                AllpassBuffers[a] = new float[GetAllpassLength(a, false, sampleRate)];
                AllpassBuffers[AllpassCount + a] = new float[GetAllpassLength(a, true, sampleRate)];
            }
        }
    }

    internal override void Process(ref EffectDspState state, Span<float> interleavedStereo, int frameCount, int sampleRate)
    {
        if (state.Extra is not ReverbState memory)
        {
            return;
        }

        var parameters = Volatile.Read(ref _parameters);

        if (!ReferenceEquals(memory.AppliedParameters, parameters))
        {
            memory.Feedback = (parameters.RoomSize * ScaleRoom) + OffsetRoom;
            memory.Damp = parameters.Damping * ScaleDamp;
            memory.Wet1 = parameters.Wet * (1f + parameters.StereoSeparation) * 0.5f;
            memory.Wet2 = parameters.Wet * (1f - parameters.StereoSeparation) * 0.5f;
            memory.Dry = parameters.Dry;
            memory.AppliedParameters = parameters;
        }

        var feedback = memory.Feedback;
        var damp = memory.Damp;
        var oneMinusDamp = 1f - damp;
        var wet1 = memory.Wet1;
        var wet2 = memory.Wet2;
        var dry = memory.Dry;

        for (var i = 0; i < frameCount * 2; i += 2)
        {
            var inputLeft = interleavedStereo[i];
            var inputRight = interleavedStereo[i + 1];
            var input = (inputLeft + inputRight) * InputGain;
            var outLeft = 0f;
            var outRight = 0f;

            // Eight lowpass-feedback combs in parallel per channel.
            for (var c = 0; c < CombCount; c++)
            {
                outLeft += Comb(memory, c, input, feedback, damp, oneMinusDamp);
                outRight += Comb(memory, CombCount + c, input, feedback, damp, oneMinusDamp);
            }

            // Four allpass sections in series.
            for (var a = 0; a < AllpassCount; a++)
            {
                outLeft = Allpass(memory, a, outLeft);
                outRight = Allpass(memory, AllpassCount + a, outRight);
            }

            interleavedStereo[i] = (outLeft * wet1) + (outRight * wet2) + (inputLeft * dry);
            interleavedStereo[i + 1] = (outRight * wet1) + (outLeft * wet2) + (inputRight * dry);
        }
    }

    // H(z) = z^-N / (1 - f (1 - d) / (1 - d z^-1) z^-N): the delayed value is lowpassed into the memory and fed back.
    private static float Comb(ReverbState memory, int index, float input, float feedback, float damp, float oneMinusDamp)
    {
        var buffer = memory.CombBuffers[index];
        var position = memory.CombIndex[index];
        var delayed = buffer[position];
        var store = (delayed * oneMinusDamp) + (memory.CombStore[index] * damp);

        if (store < Flush && store > -Flush)
        {
            store = 0f;
        }

        memory.CombStore[index] = store;
        var written = input + (store * feedback);

        if (written < Flush && written > -Flush)
        {
            written = 0f;
        }

        buffer[position] = written;
        position++;
        memory.CombIndex[index] = position == buffer.Length ? 0 : position;
        return delayed;
    }

    // H(z) = (-1 + (1 + g) z^-N) / (1 - g z^-N) as a feedback comb (w = x + g w[n-N]) followed by the feedforward comb
    // y = -w + (1 + g) w[n-N].
    private static float Allpass(ReverbState memory, int index, float input)
    {
        var buffer = memory.AllpassBuffers[index];
        var position = memory.AllpassIndex[index];
        var delayed = buffer[position];
        var w = input + (delayed * AllpassFeedback);

        if (w < Flush && w > -Flush)
        {
            w = 0f;
        }

        buffer[position] = w;
        position++;
        memory.AllpassIndex[index] = position == buffer.Length ? 0 : position;
        return (-w) + ((1f + AllpassFeedback) * delayed);
    }
}
