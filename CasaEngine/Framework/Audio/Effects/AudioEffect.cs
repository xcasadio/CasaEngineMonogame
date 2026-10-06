using CasaEngine.Framework.Audio.Mixing;

namespace CasaEngine.Framework.Audio.Effects;

/// <summary>
/// An insert effect of an <see cref="AudioBus"/> (plan decisions P18 and P20): it processes the interleaved stereo
/// buffer of the bus, after the voices were mixed into it and before the bus gain is applied. Up to
/// <see cref="AudioBus.MaxEffects"/> effects run on a bus, in insertion order.
/// </summary>
/// <remarks>
/// <para>
/// The effect object lives on the game thread: build it there and change its properties there. Every property change
/// publishes a new immutable parameter snapshot as a last value (never queued, so never lost when the command ring is
/// full); the audio thread reads the latest snapshot at the start of each block. The audio-side state (filter memory,
/// compressor envelope) is not in this object: it lives in the software mixer, preallocated, one record per bus slot.
/// </para>
/// <para>
/// Effects only run on a backend with <see cref="IAudioBusBackend"/> (the software backend). Elsewhere they are
/// accepted by <see cref="AudioBus.AddEffect"/> but have no audible effect, and <see cref="AudioService"/> logs that once.
/// </para>
/// <para>
/// The constructor is internal: the DSP is dispatched by the mixer, so the engine provides the effects
/// (<see cref="BiquadFilterEffect"/>, <see cref="CompressorEffect"/>, <see cref="LimiterEffect"/>, <see cref="ReverbEffect"/>,
/// <see cref="DuckingEffect"/>).
/// </para>
/// </remarks>
public abstract class AudioEffect
{
    internal AudioEffect()
    {
    }

    /// <summary>The bus this effect is inserted on, or null while it is not on a bus.</summary>
    public AudioBus Bus { get; internal set; }

    /// <summary>
    /// Game thread. Builds the audio-side memory this effect needs beyond <see cref="EffectDspState"/> (delay lines sized for
    /// <paramref name="sampleRate"/>), carried by the add command so the audio thread allocates nothing. Null when none.
    /// </summary>
    internal virtual object CreateAudioState(int sampleRate)
    {
        return null;
    }

    /// <summary>
    /// Audio thread. Processes <paramref name="frameCount"/> interleaved stereo frames in place, with the state that
    /// belongs to this effect on this bus. Must not allocate, lock or log.
    /// </summary>
    internal abstract void Process(ref EffectDspState state, Span<float> interleavedStereo, int frameCount, int sampleRate);

    /// <summary>
    /// Game thread. The current parameter snapshot, an immutable object (see <see cref="AudioMixerSnapshot"/>), or null
    /// when the effect has none. Capturing allocates nothing: the snapshot already exists, it is the one the audio thread reads.
    /// </summary>
    internal abstract object CaptureParameters();

    /// <summary>Game thread. Publishes a snapshot returned by <see cref="CaptureParameters"/> of the same effect as a last value; any other object is ignored.</summary>
    internal abstract void RestoreParameters(object parameters);
}
