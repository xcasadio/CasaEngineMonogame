namespace CasaEngine.Framework.Audio;

/// <summary>
/// A decoded, fully resident sound ready to be played by an <see cref="IAudioBackend"/>.
/// Implementations are backend specific; the engine only manipulates this contract so the
/// mixing and voice logic stays testable without an audio device.
/// Loaded through <c>AssetContentManager.Load&lt;IAudioClip&gt;</c> and disposed by its Unload.
/// </summary>
public interface IAudioClip : IDisposable
{
    int SampleRate { get; }

    int ChannelCount { get; }

    TimeSpan Duration { get; }

    bool IsDisposed { get; }
}
