namespace CasaEngine.Framework.Audio;

/// <summary>
/// Optional capability of an <see cref="IAudioBackend"/> (plan decision P32): the backend mixes a per-voice gain
/// factor, spatial pan and speed ratio on the audio thread, on top of the volume of the voice, its ramps and the gain
/// of its bus. <see cref="AudioService"/> detects it with a type test; <see cref="IAudioBackend"/> is unchanged and
/// only <c>SoftwareAudioBackend</c> implements it. A backend without it keeps the 2D behaviour: the caller folds the
/// factors into the volume, pan and pitch it sends.
/// </summary>
/// <remarks>
/// <para>
/// Values are domains clamped by the backend: gain in [0, 1] (NaN is 1); pan in [-1, 1] (NaN is the own pan of the
/// voice, the one given in <see cref="AudioVoiceParameters"/>); rate in [1/16, <see cref="AudioVoiceParameters.MaxRateMultiplier"/>]
/// (NaN or not positive is 1). With (1, NaN, 1) the output of a voice is the one it has without this capability.
/// </para>
/// <para>
/// The values are published as last values, never queued: a change is never lost and never waits for a full command
/// ring. They apply from the next audio block (the gain is ramped across that block) and are independent of
/// <see cref="IAudioBackend.SetVolume"/>, <see cref="IAudioBackend.SetParameters"/> and volume ramps. Every member must
/// be called from the game thread.
/// </para>
/// </remarks>
public interface IAudioVoiceModulationBackend
{
    /// <summary>
    /// Sets the values of the next voice this backend starts (<see cref="IAudioBackend.Play"/>,
    /// <see cref="IAudioBackend.CreateStreamingVoice"/> or <see cref="IStereoVoiceBackend.PlayStereo"/>): they travel
    /// with the start, so the voice never sounds first at full gain or at the wrong speed. They apply to that one voice
    /// only, even when its start is refused; the next one starts from (1, NaN, 1) unless this is called again.
    /// </summary>
    void SetNextVoiceModulation(float gain, float pan, float rate);

    /// <summary>
    /// Sets the last values of a live voice. Ignored for a stale or unknown handle. Never queued and never waits.
    /// A value set on a streaming voice that is created but not started is its starting value.
    /// </summary>
    void SetVoiceModulation(AudioVoiceHandle voice, float gain, float pan, float rate);
}
