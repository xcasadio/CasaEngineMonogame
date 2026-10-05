namespace CasaEngine.Framework.Audio.Output;

/// <summary>Output that is never available: no device, no thread.</summary>
internal sealed class NullAudioOutput : IAudioOutput
{
    public bool IsAvailable => false;

    public int SampleRate => 0;

    public int BufferFrames => 0;

    public int BufferCount => 0;

    public int UnderrunCount => 0;

    public bool TryOpen()
    {
        return false;
    }

    public void Start(AudioRenderCallback callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
    }

    public void Dispose()
    {
    }
}
