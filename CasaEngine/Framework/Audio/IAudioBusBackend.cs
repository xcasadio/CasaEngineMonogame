using CasaEngine.Framework.Audio.Psx;

namespace CasaEngine.Framework.Audio;

/// <summary>
/// Optional capability of an <see cref="IAudioBackend"/> (plan decision P18): the backend mixes a real graph of
/// buses itself, so each voice is routed to its bus and every bus applies its own gain, smoothed per block, on the
/// audio thread. <see cref="AudioService"/> detects it with a type test; <see cref="IAudioBackend"/> is unchanged
/// and only <c>SoftwareAudioBackend</c> implements it. A backend without it keeps the previous behaviour: the bus
/// gain is multiplied into the volume of each voice by <see cref="AudioService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Buses are identified by an index handed out in creation order. Index 0 always exists: it is Master, the root
/// every bus ends in and the bus a voice goes to when none was chosen. A parent exists before its children, so the
/// parent index is always lower than the child index. The backend holds <see cref="BusCapacity"/> buses at most.
/// </para>
/// <para>
/// A bus gain is its own gain, in [0, 1] (zero for a muted bus); the product along the chain up to Master is
/// computed by the mix itself. It is published as a last value, never queued, so a change is never lost.
/// Every member must be called from the game thread.
/// </para>
/// </remarks>
public interface IAudioBusBackend
{
    /// <summary>Number of buses the backend can hold, Master included.</summary>
    int BusCapacity { get; }

    /// <summary>
    /// Creates a bus as a child of <paramref name="parentBus"/> and returns its index. Returns false, with
    /// <paramref name="busIndex"/> at -1, when the backend is unavailable, when the parent does not exist or when
    /// <see cref="BusCapacity"/> buses already exist (one warning is logged; the caller then routes to Master).
    /// </summary>
    bool TryCreateBus(int parentBus, out int busIndex);

    /// <summary>Sets the own gain of a bus (a last value, ramped across the next audio block). Ignored for an unknown bus.</summary>
    void SetBusGain(int busIndex, float gain);

    /// <summary>
    /// Chooses the bus of the next voice this backend starts (<see cref="IAudioBackend.Play"/>,
    /// <see cref="IAudioBackend.CreateStreamingVoice"/> or <see cref="IStereoVoiceBackend.PlayStereo"/>): the bus is
    /// part of the start order, so the voice never sounds on another bus first. It applies to that one voice only;
    /// the next one goes to Master again unless this is called again. An unknown bus means Master.
    /// </summary>
    void SetNextVoiceBus(int busIndex);

    /// <summary>Routes the output of a hosted SPU (<see cref="IPsxSpuHost"/>) to a bus. Returns false when it cannot be sent.</summary>
    bool TrySetPsxSpuBus(PsxSpuPort port, int busIndex);

    /// <summary>
    /// Ramps the volume of a voice to <paramref name="targetVolume"/> over <paramref name="durationSeconds"/> (plan
    /// decision P21): the audio thread interpolates it linearly, sample by sample, from the value the voice has at that
    /// moment. The ramp starts at the start of the next audio block (at most one block, about 10 ms, after the call: no
    /// command carries a timestamp). The command is not lost: a full command ring is waited for like a voice start.
    /// Returns false when it could not be sent (unavailable backend, stale handle, ring still full), in which case the
    /// caller steps the volume itself. While the ramp runs, <see cref="IAudioBackend.SetVolume"/> ends it and the volume
    /// of <see cref="IAudioBackend.SetParameters"/> is ignored. A ramp advances while the voice is paused.
    /// </summary>
    bool TryRampVoiceVolume(AudioVoiceHandle voice, float targetVolume, float durationSeconds);

    /// <summary>Stops the volume ramp of a voice at the value it has reached. Ignored when no ramp runs.</summary>
    void FreezeVoiceVolume(AudioVoiceHandle voice);

    /// <summary>
    /// Ramps the own gain of a bus to <paramref name="targetGain"/> over <paramref name="durationSeconds"/>, like
    /// <see cref="TryRampVoiceVolume"/>. The ramp owns the gain of the bus until it ends; the gain then stays at the target
    /// until a different value is published with <see cref="SetBusGain"/>, which also ends a running ramp. Returns false
    /// when it could not be sent.
    /// </summary>
    bool TryRampBusGain(int busIndex, float targetGain, float durationSeconds);

    /// <summary>Stops the gain ramp of a bus at the value it has reached. Ignored when no ramp runs.</summary>
    void FreezeBusGain(int busIndex);
}
