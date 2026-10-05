using CasaEngine.Framework.Audio;
using Xunit;

namespace CasaEngine.Tests.Audio;

/// <summary>
/// The observable <see cref="IAudioBackend"/> contract, run against every backend through the
/// derived classes. A derived class says how to build a backend, a clip, and how to make a voice
/// end or a queued buffer get consumed, which is backend specific.
/// </summary>
public abstract class AudioBackendConformanceTests : IDisposable
{
    private const int StreamSampleRate = 24000;
    private readonly List<IAudioBackend> _created = new();

    protected abstract IAudioBackend CreateBackend(int capacity);

    protected abstract IAudioBackend CreateUnavailableBackend();

    protected abstract IAudioClip CreateClip(int frames, int channels);

    /// <summary>Makes a playing, non-looped resident voice reach the end of its clip.</summary>
    protected abstract void EndResidentVoice(IAudioBackend backend, AudioVoiceHandle voice);

    /// <summary>Makes the backend consume <paramref name="count"/> buffers queued on a started streaming voice.</summary>
    protected abstract void ConsumeBuffers(IAudioBackend backend, AudioVoiceHandle voice, int count);

    public void Dispose()
    {
        foreach (var backend in _created)
        {
            backend.Dispose();
        }
    }

    private IAudioBackend Create(int capacity = 4)
    {
        var backend = CreateBackend(capacity);
        _created.Add(backend);
        return backend;
    }

    private IAudioBackend CreateUnavailable()
    {
        var backend = CreateUnavailableBackend();
        _created.Add(backend);
        return backend;
    }

    private AudioVoiceHandle PlayOne(IAudioBackend backend, bool looped = false)
    {
        return backend.Play(CreateClip(4800, 1), new AudioVoiceParameters(1f, 0f, 0f, looped));
    }

    private static byte[] Buffer(int frames = 480) => new byte[frames * 4];

    private static AudioVoiceHandle StartedStream(IAudioBackend backend, out byte[] buffer)
    {
        var voice = backend.CreateStreamingVoice(StreamSampleRate, 2, AudioVoiceParameters.Default);
        Assert.True(voice.IsValid);
        buffer = Buffer();
        backend.Start(voice);
        return voice;
    }

    // ---- capacity and refusal ------------------------------------------------

    [Fact]
    public void Backend_ReportsItsCapacity()
    {
        var backend = Create(3);

        Assert.True(backend.IsAvailable);
        Assert.Equal(3, backend.VoiceCapacity);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void Play_BeyondCapacity_ReturnsNoneWithoutThrowing()
    {
        var backend = Create(2);

        var first = PlayOne(backend, looped: true);
        var second = PlayOne(backend, looped: true);
        var refused = PlayOne(backend, looped: true);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.False(refused.IsValid);
        Assert.Equal(2, backend.ActiveVoiceCount);
    }

    [Fact]
    public void Release_FreesAVoiceForANewPlay()
    {
        var backend = Create(1);
        var first = PlayOne(backend, looped: true);

        backend.Release(first);
        var second = PlayOne(backend, looped: true);

        Assert.True(second.IsValid);
        Assert.Equal(1, backend.ActiveVoiceCount);
    }

    [Fact]
    public void Play_WithNullClip_Throws()
    {
        var backend = Create();

        Assert.Throws<ArgumentNullException>(() => backend.Play(null, AudioVoiceParameters.Default));
    }

    // ---- handles --------------------------------------------------------------

    [Fact]
    public void Handle_AfterRelease_IsStale()
    {
        var backend = Create();
        var voice = PlayOne(backend, looped: true);

        backend.Release(voice);

        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void Handle_OfAReusedSlot_DoesNotControlTheNewVoice()
    {
        var backend = Create(1);
        var old = PlayOne(backend, looped: true);
        backend.Release(old);
        var current = PlayOne(backend, looped: true);

        backend.Stop(old);
        backend.Pause(old);
        backend.Release(old);

        Assert.NotEqual(old, current);
        Assert.Equal(AudioVoiceState.Playing, backend.GetState(current));
        Assert.Equal(1, backend.ActiveVoiceCount);
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(old));
    }

    [Fact]
    public void NoneHandle_IsIgnoredByEveryCall()
    {
        var backend = Create();
        var none = AudioVoiceHandle.None;

        backend.SetParameters(none, AudioVoiceParameters.Default);
        backend.SetVolume(none, 0.5f);
        backend.Pause(none);
        backend.Resume(none);
        backend.Stop(none);
        backend.Release(none);
        backend.Start(none);
        backend.SubmitBuffer(none, Buffer(), 0, 4);

        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(none));
        Assert.Equal(0, backend.GetPendingBufferCount(none));
    }

    [Fact]
    public void SetVolumeAndSetParameters_OnAStaleHandle_AreIgnored()
    {
        var backend = Create(1);
        var old = PlayOne(backend, looped: true);
        backend.Release(old);
        var current = PlayOne(backend, looped: true);

        backend.SetVolume(old, 0f);
        backend.SetParameters(old, new AudioVoiceParameters(0f, 1f, 1f, false));
        backend.SetVolume(current, float.NaN);

        Assert.Equal(AudioVoiceState.Playing, backend.GetState(current));
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(old));
    }

    // ---- states ----------------------------------------------------------------

    [Fact]
    public void GetState_FollowsPauseResumeAndStop_AndKeepsTheSlotUntilRelease()
    {
        var backend = Create();
        var voice = PlayOne(backend, looped: true);
        Assert.Equal(AudioVoiceState.Playing, backend.GetState(voice));

        backend.Pause(voice);
        Assert.Equal(AudioVoiceState.Paused, backend.GetState(voice));

        backend.Resume(voice);
        Assert.Equal(AudioVoiceState.Playing, backend.GetState(voice));

        backend.Stop(voice);
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));
        Assert.Equal(1, backend.ActiveVoiceCount);

        backend.Release(voice);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void Resume_OnAPlayingVoice_AndPauseResume_OnAStoppedVoice_AreNoOps()
    {
        var backend = Create();
        var voice = PlayOne(backend, looped: true);

        backend.Resume(voice);
        Assert.Equal(AudioVoiceState.Playing, backend.GetState(voice));

        backend.Stop(voice);
        backend.Pause(voice);
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));
        backend.Resume(voice);
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));
    }

    [Fact]
    public void ResidentVoice_ReachingItsEnd_BecomesStoppedAndKeepsItsSlot()
    {
        var backend = Create();
        var voice = PlayOne(backend);

        EndResidentVoice(backend, voice);

        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));
        Assert.Equal(1, backend.ActiveVoiceCount);

        backend.Release(voice);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void StopAll_StopsAndReleasesEveryVoice()
    {
        var backend = Create();
        var first = PlayOne(backend, looped: true);
        var second = PlayOne(backend, looped: true);
        var stream = backend.CreateStreamingVoice(StreamSampleRate, 2, AudioVoiceParameters.Default);

        backend.StopAll();

        Assert.Equal(0, backend.ActiveVoiceCount);
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(first));
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(second));
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(stream));
        Assert.True(PlayOne(backend, looped: true).IsValid);
    }

    // ---- streaming -------------------------------------------------------------

    [Fact]
    public void CreateStreamingVoice_IsStoppedUntilStarted()
    {
        var backend = Create();
        Assert.True(backend.SupportsStreaming);

        var voice = backend.CreateStreamingVoice(StreamSampleRate, 2, AudioVoiceParameters.Default);

        Assert.True(voice.IsValid);
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));
        Assert.Equal(1, backend.ActiveVoiceCount);

        backend.Start(voice);
        Assert.Equal(AudioVoiceState.Playing, backend.GetState(voice));
    }

    [Fact]
    public void SubmitBuffer_CountsPendingBuffers_AndConsumptionLowersThem()
    {
        var backend = Create();
        var voice = StartedStream(backend, out var buffer);

        backend.SubmitBuffer(voice, buffer, 0, buffer.Length);
        backend.SubmitBuffer(voice, buffer, 0, buffer.Length);
        backend.SubmitBuffer(voice, buffer, 0, buffer.Length);
        Assert.Equal(3, backend.GetPendingBufferCount(voice));

        ConsumeBuffers(backend, voice, 2);

        Assert.Equal(1, backend.GetPendingBufferCount(voice));
    }

    [Fact]
    public void SubmitBuffer_WithAnEmptyRange_IsIgnored_AndANullBufferThrows()
    {
        var backend = Create();
        var voice = StartedStream(backend, out var buffer);

        backend.SubmitBuffer(voice, buffer, 0, 0);

        Assert.Equal(0, backend.GetPendingBufferCount(voice));
        Assert.Throws<ArgumentNullException>(() => backend.SubmitBuffer(voice, null, 0, 4));
    }

    [Fact]
    public void StreamingVoice_RunningDry_StaysPlaying()
    {
        var backend = Create();
        var voice = StartedStream(backend, out var buffer);
        backend.SubmitBuffer(voice, buffer, 0, buffer.Length);

        ConsumeBuffers(backend, voice, 1);

        Assert.Equal(0, backend.GetPendingBufferCount(voice));
        Assert.Equal(AudioVoiceState.Playing, backend.GetState(voice));
    }

    [Fact]
    public void GetPendingBufferCount_OnAResidentVoiceOrAStaleHandle_IsZero()
    {
        var backend = Create();
        var resident = PlayOne(backend, looped: true);
        var stream = StartedStream(backend, out var buffer);
        backend.SubmitBuffer(stream, buffer, 0, buffer.Length);
        backend.Release(stream);

        Assert.Equal(0, backend.GetPendingBufferCount(resident));
        Assert.Equal(0, backend.GetPendingBufferCount(stream));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void CreateStreamingVoice_WithAnInvalidChannelCount_Throws(int channelCount)
    {
        var backend = Create();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => backend.CreateStreamingVoice(StreamSampleRate, channelCount, AudioVoiceParameters.Default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-44100)]
    public void CreateStreamingVoice_WithANonPositiveRate_ReturnsNone(int sampleRate)
    {
        var backend = Create();

        var voice = backend.CreateStreamingVoice(sampleRate, 2, AudioVoiceParameters.Default);

        Assert.False(voice.IsValid);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void CreateStreamingVoice_BeyondCapacity_ReturnsNone()
    {
        var backend = Create(1);
        PlayOne(backend, looped: true);

        var voice = backend.CreateStreamingVoice(StreamSampleRate, 2, AudioVoiceParameters.Default);

        Assert.False(voice.IsValid);
    }

    // ---- unavailable and disposed ---------------------------------------------

    [Fact]
    public void UnavailableBackend_EveryCallIsASilentNoOp()
    {
        var backend = CreateUnavailable();

        var voice = PlayOne(backend);
        var stream = backend.CreateStreamingVoice(StreamSampleRate, 2, AudioVoiceParameters.Default);

        Assert.False(backend.IsAvailable);
        Assert.False(voice.IsValid);
        Assert.False(stream.IsValid);
        Assert.Equal(0, backend.ActiveVoiceCount);

        var none = AudioVoiceHandle.None;
        backend.SetParameters(none, AudioVoiceParameters.Default);
        backend.SetVolume(none, 1f);
        backend.Pause(none);
        backend.Resume(none);
        backend.Stop(none);
        backend.Release(none);
        backend.Start(none);
        backend.SubmitBuffer(none, Buffer(), 0, 4);
        backend.StopAll();
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(none));
        Assert.Equal(0, backend.GetPendingBufferCount(none));
    }

    [Fact]
    public void DisposedBackend_RefusesNewVoices()
    {
        var backend = Create();
        PlayOne(backend, looped: true);

        backend.Dispose();

        Assert.False(PlayOne(backend).IsValid);
        Assert.False(backend.CreateStreamingVoice(StreamSampleRate, 2, AudioVoiceParameters.Default).IsValid);
    }
}
