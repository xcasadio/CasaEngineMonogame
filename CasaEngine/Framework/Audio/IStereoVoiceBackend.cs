namespace CasaEngine.Framework.Audio;

/// <summary>
/// Optional capability of an <see cref="IAudioBackend"/>: plays a mono clip with an exact left and
/// right gain per output frame, mixed by the backend itself. <see cref="AudioService"/> detects it
/// with a type test and, when present, routes <see cref="AudioService.PlayClipStereo"/> through it
/// instead of feeding a streaming voice from the game thread (ADR-0039); <see cref="IAudioBackend"/>
/// is unchanged and a backend that does not implement this keeps the previous behaviour.
/// </summary>
/// <remarks>
/// The returned handle is an ordinary voice of the backend: Pause, Resume, Stop, Release, GetState,
/// SetVolume and SetParameters apply to it, and a non looped voice becomes
/// <see cref="AudioVoiceState.Stopped"/> at the end of its clip. The voice output is
/// <c>volume * leftGain</c> on the left and <c>volume * rightGain</c> on the right, with no pan law;
/// the pan of the parameters is ignored while the voice is in this mode. Gains are expected in
/// [0, 1], already sanitized by the caller.
/// </remarks>
public interface IStereoVoiceBackend
{
    /// <summary>
    /// Starts a voice for a mono <paramref name="clip"/> with explicit gains. Returns
    /// <see cref="AudioVoiceHandle.None"/> when no voice is available, when the backend is
    /// unavailable or when the clip is not a usable mono clip; must not throw for those cases.
    /// </summary>
    AudioVoiceHandle PlayStereo(IAudioClip clip, in AudioVoiceParameters parameters, float leftGain, float rightGain);

    /// <summary>Changes the gains of a live voice started by <see cref="PlayStereo"/>. Ignored for a stale handle.</summary>
    void SetStereoGains(AudioVoiceHandle voice, float leftGain, float rightGain);
}
