namespace CasaEngine.Framework.Audio.Backends;

/// <summary>
/// Backend that plays nothing. Used when no audio device could be opened, so the rest of the
/// engine keeps calling the audio API without having to test for its absence everywhere.
/// </summary>
public sealed class NullAudioBackend : IAudioBackend
{
    public bool IsAvailable => false;

    public int VoiceCapacity => 0;

    public int ActiveVoiceCount => 0;

    public AudioVoiceHandle Play(IAudioClip clip, in AudioVoiceParameters parameters) => AudioVoiceHandle.None;

    public void SetParameters(AudioVoiceHandle voice, in AudioVoiceParameters parameters)
    {
    }

    public void SetVolume(AudioVoiceHandle voice, float volume)
    {
    }

    public AudioVoiceState GetState(AudioVoiceHandle voice) => AudioVoiceState.Stopped;

    public void Pause(AudioVoiceHandle voice)
    {
    }

    public void Resume(AudioVoiceHandle voice)
    {
    }

    public void Stop(AudioVoiceHandle voice)
    {
    }

    public void Release(AudioVoiceHandle voice)
    {
    }

    public void StopAll()
    {
    }

    public bool SupportsStreaming => false;

    public AudioVoiceHandle CreateStreamingVoice(int sampleRate, int channelCount, in AudioVoiceParameters parameters)
        => AudioVoiceHandle.None;

    public void SubmitBuffer(AudioVoiceHandle voice, byte[] buffer, int offset, int count)
    {
    }

    public int GetPendingBufferCount(AudioVoiceHandle voice) => 0;

    public void Start(AudioVoiceHandle voice)
    {
    }

    public void Dispose()
    {
    }
}
