using CasaEngine.Framework.Audio.Output;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// <see cref="IAudioOutput"/> with no device and no thread: <see cref="Start"/> only stores the
/// render callback and <see cref="Pump"/> invokes it synchronously on the calling (test) thread, so
/// a test drives the audio thread frame by frame.
/// </summary>
internal sealed class OfflineAudioOutput : IAudioOutput
{
    private readonly bool _canOpen;
    private AudioRenderCallback _callback;
    private float[] _buffer = new float[2 * 4096];

    public OfflineAudioOutput(bool canOpen = true, int sampleRate = 48000)
    {
        _canOpen = canOpen;
        SampleRate = sampleRate;
    }

    public bool IsAvailable { get; private set; }

    public int SampleRate { get; }

    public int BufferFrames => 480;

    public int BufferCount => 4;

    public int UnderrunCount => 0;

    public bool IsDisposed { get; private set; }

    public bool IsStarted => _callback != null;

    public bool TryOpen()
    {
        IsAvailable = _canOpen;
        return _canOpen;
    }

    public void Start(AudioRenderCallback callback)
    {
        _callback = callback;
    }

    /// <summary>Renders <paramref name="frames"/> stereo frames and returns the peak absolute sample.</summary>
    public float Pump(int frames)
    {
        if (_buffer.Length < frames * 2)
        {
            _buffer = new float[frames * 2];
        }

        _callback(_buffer, frames);

        var peak = 0f;
        for (var i = 0; i < frames * 2; i++)
        {
            peak = Math.Max(peak, Math.Abs(_buffer[i]));
        }

        return peak;
    }

    public void Dispose()
    {
        IsDisposed = true;
        IsAvailable = false;
    }
}
