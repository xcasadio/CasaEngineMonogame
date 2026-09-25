namespace CasaEngine.Framework.Audio;

/// <summary>
/// Optional capability of an <see cref="IAudioClip"/>: exposes the decoded mono 16 bit PCM
/// samples behind it, so <see cref="AudioService.PlayClipStereo"/> can feed them itself on a
/// software stereo voice with exact per-channel gains (see
/// docs/decisions/0039-software-stereo-voices.md). A clip that does not implement this
/// interface, or whose <see cref="MonoSamples"/> is empty, cannot be played through that path.
/// </summary>
public interface IAudioClipSamples
{
    /// <summary>Decoded 16 bit PCM samples of the clip, one channel.</summary>
    ReadOnlyMemory<short> MonoSamples { get; }

    /// <summary>Sample rate of <see cref="MonoSamples"/>, in Hz.</summary>
    int SampleRate { get; }
}
