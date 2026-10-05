using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Backends;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;
using Xunit.Abstractions;

namespace CasaEngine.Tests.Audio;

/// <summary>Behaviour specific to <see cref="SoftwareAudioBackend"/>, driven through an offline output.</summary>
public class SoftwareAudioBackendTests
{
    private const int Rate = 48000;

    private readonly ITestOutputHelper _log;

    public SoftwareAudioBackendTests(ITestOutputHelper log)
    {
        _log = log;
    }

    private static PcmAudioClip Constant(short value, int frames, int rate = Rate, int channels = 1)
    {
        var samples = new short[frames * channels];
        Array.Fill(samples, value);
        return new PcmAudioClip(samples, rate, channels);
    }

    private static SoftwareAudioBackend Create(out OfflineAudioOutput output, int capacity = 16)
    {
        output = new OfflineAudioOutput();
        return new SoftwareAudioBackend(output, capacity);
    }

    [Fact]
    public void Construction_StartsTheOutputAndExposesDiagnostics()
    {
        using var backend = Create(out var output);

        Assert.True(backend.IsAvailable);
        Assert.True(output.IsStarted);
        Assert.Equal(Rate, backend.OutputSampleRate);
        Assert.Equal(4 * 480 * 1000 / Rate, backend.LeadMilliseconds);
        Assert.Equal(0, backend.UnderrunCount);
        Assert.Equal(0, backend.DroppedChunkCount);
        Assert.Equal(0, backend.DroppedEventCount);
    }

    [Fact]
    public void UnavailableOutput_DoesNotThrowAndReportsNoDiagnostics()
    {
        using var backend = new SoftwareAudioBackend(new OfflineAudioOutput(canOpen: false));

        Assert.False(backend.IsAvailable);
        Assert.False(backend.SupportsStreaming);
        Assert.Equal(0, backend.OutputSampleRate);
        Assert.Equal(0, backend.LeadMilliseconds);
        Assert.False(backend.Play(Constant(1000, 100), AudioVoiceParameters.Default).IsValid);
    }

    [Fact]
    public void Dispose_DisposesTheOutput_AndIsIdempotent()
    {
        var backend = Create(out var output);

        backend.Dispose();
        backend.Dispose();

        Assert.True(output.IsDisposed);
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void Play_WithAClipThatIsNotPcm_Throws()
    {
        using var backend = Create(out _);

        Assert.Throws<ArgumentException>(() => backend.Play(new FakeAudioClip(), AudioVoiceParameters.Default));
    }

    [Fact]
    public void Play_WithADisposedClip_ReturnsNone()
    {
        using var backend = Create(out _);
        var clip = Constant(1000, 100);
        clip.Dispose();

        Assert.False(backend.Play(clip, AudioVoiceParameters.Default).IsValid);
    }

    [Fact]
    public void PlayingClip_RendersSound_AndStopSilencesIt()
    {
        using var backend = Create(out var output);
        var voice = backend.Play(Constant(8000, Rate), new AudioVoiceParameters(1f, 0f, 0f, true));

        var playing = output.Pump(512);
        backend.Stop(voice);
        output.Pump(512);
        var stopped = output.Pump(512);

        Assert.True(playing > 0.05f);
        Assert.Equal(0f, stopped);
    }

    [Fact]
    public void PausedVoice_IsSilent_AndResumeRestoresIt()
    {
        using var backend = Create(out var output);
        var voice = backend.Play(Constant(8000, Rate), new AudioVoiceParameters(1f, 0f, 0f, true));
        output.Pump(256);

        backend.Pause(voice);
        output.Pump(256);
        var paused = output.Pump(256);
        backend.Resume(voice);
        var resumed = output.Pump(512);

        Assert.Equal(0f, paused);
        Assert.True(resumed > 0.05f);
    }

    [Fact]
    public void Clip_OfAnotherSampleRate_IsPlayed()
    {
        using var backend = Create(out var output);

        var voice = backend.Play(Constant(8000, 22050, rate: 22050), new AudioVoiceParameters(1f, 0f, 0f, true));

        Assert.True(voice.IsValid);
        Assert.True(output.Pump(512) > 0.05f);
    }

    [Fact]
    public void Stream_OfAnyPositiveRate_IsAccepted()
    {
        using var backend = Create(out _);

        var low = backend.CreateStreamingVoice(4000, 1, AudioVoiceParameters.Default);
        var high = backend.CreateStreamingVoice(96000, 2, AudioVoiceParameters.Default);

        Assert.True(low.IsValid);
        Assert.True(high.IsValid);
    }

    [Fact]
    public void SubmittedBuffers_AreRendered()
    {
        using var backend = Create(out var output);
        var voice = backend.CreateStreamingVoice(Rate, 2, AudioVoiceParameters.Default);
        var buffer = new byte[480 * 4];
        for (var i = 0; i < buffer.Length; i += 2)
        {
            BitConverter.TryWriteBytes(buffer.AsSpan(i, 2), (short)8000);
        }

        backend.SubmitBuffer(voice, buffer, 0, buffer.Length);
        backend.Start(voice);

        Assert.True(output.Pump(256) > 0.05f);
    }

    [Fact]
    public void StartedThenStoppedStream_CannotBeRestarted()
    {
        using var backend = Create(out _);
        var voice = backend.CreateStreamingVoice(Rate, 2, AudioVoiceParameters.Default);
        backend.Start(voice);
        backend.Stop(voice);

        backend.Start(voice);

        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));
    }

    [Fact]
    public void FullCommandRing_WithoutAnAudioThread_DropsAfterTheBoundedWait()
    {
        // Nothing pumps the output, so the 4096 slot command ring can never drain.
        using var backend = Create(out _, capacity: 4);
        var voice = backend.Play(Constant(1000, Rate), new AudioVoiceParameters(1f, 0f, 0f, true));

        var started = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 5000; i++)
        {
            backend.SetVolume(voice, 0.5f);
        }

        // SetVolume is never retried, so filling the ring is fast; Pause is retried then dropped.
        Assert.True(started.ElapsedMilliseconds < SoftwareAudioBackend.CommandRetryMilliseconds * 20);
        started.Restart();
        backend.Pause(voice);
        started.Stop();

        Assert.Equal(AudioVoiceState.Playing, backend.GetState(voice));
        Assert.InRange(started.ElapsedMilliseconds, SoftwareAudioBackend.CommandRetryMilliseconds - 20, SoftwareAudioBackend.CommandRetryMilliseconds * 20);
    }

    [Fact]
    public void DeadOutput_MutesTheBackend_AndNeverWaitsOnAFullRing()
    {
        using var backend = Create(out var output, capacity: 4);
        var voice = backend.Play(Constant(1000, Rate), new AudioVoiceParameters(1f, 0f, 0f, true));
        var stream = backend.CreateStreamingVoice(Rate, 2, AudioVoiceParameters.Default);

        // Fill the 4096 slot command ring (nothing pumps it), then kill the output.
        for (var i = 0; i < 5000; i++)
        {
            backend.SetVolume(voice, 0.5f);
        }

        output.Die();

        Assert.False(backend.IsAvailable);
        Assert.False(backend.SupportsStreaming);
        Assert.Equal(AudioVoiceState.Stopped, backend.GetState(voice));
        Assert.Equal(0, backend.GetPendingBufferCount(stream));

        var started = System.Diagnostics.Stopwatch.StartNew();
        var worst = 0.0;
        var clip = Constant(1000, 100);
        for (var i = 0; i < 10; i++)
        {
            var before = started.Elapsed.TotalMilliseconds;
            Assert.False(backend.Play(clip, AudioVoiceParameters.Default).IsValid);
            Assert.False(backend.CreateStreamingVoice(Rate, 2, AudioVoiceParameters.Default).IsValid);
            backend.Pause(voice);
            backend.SetParameters(voice, AudioVoiceParameters.Default);
            backend.Stop(voice);
            backend.StopAll();
            worst = Math.Max(worst, started.Elapsed.TotalMilliseconds - before);
        }

        _log.WriteLine($"worst dead-output call batch: {worst} ms");
        Assert.True(worst < 5, $"a call on a dead output took {worst} ms");
    }

    [Fact]
    public void OutputDyingWhileACommandWaitsOnAFullRing_ReturnsWithoutTheFullWait()
    {
        using var backend = Create(out var output, capacity: 4);
        var voice = backend.Play(Constant(1000, Rate), new AudioVoiceParameters(1f, 0f, 0f, true));
        for (var i = 0; i < 5000; i++)
        {
            backend.SetVolume(voice, 0.5f);
        }

        // The output is already dead when the backend is first asked to retry on the full ring.
        output.Die();
        var started = System.Diagnostics.Stopwatch.StartNew();
        backend.Pause(voice);
        started.Stop();

        _log.WriteLine($"Pause on a full ring, dead output: {started.Elapsed.TotalMilliseconds} ms");
        // Half the retry wait: far below the full wait, and above a GC pause or a first-call JIT caused by the other
        // tests running in parallel (a 5 ms bound failed that way now and then).
        var bound = SoftwareAudioBackend.CommandRetryMilliseconds / 2.0;
        Assert.True(started.Elapsed.TotalMilliseconds < bound, $"Pause took {started.Elapsed.TotalMilliseconds} ms");
    }

    [Fact]
    public void SteadyFrame_DoesNotAllocate()
    {
        using var backend = Create(out var output);
        var residents = new AudioVoiceHandle[8];
        var clip = Constant(4000, Rate);

        for (var i = 0; i < residents.Length; i++)
        {
            residents[i] = backend.Play(clip, new AudioVoiceParameters(1f, 0f, 0f, true));
        }

        var streams = new AudioVoiceHandle[2];
        for (var i = 0; i < streams.Length; i++)
        {
            streams[i] = backend.CreateStreamingVoice(Rate, 2, AudioVoiceParameters.Default);
            backend.Start(streams[i]);
        }

        var buffer = new byte[480 * 4];

        void Frame()
        {
            output.Pump(480);

            foreach (var voice in residents)
            {
                backend.SetVolume(voice, 0.5f);
                backend.GetState(voice);
            }

            foreach (var voice in streams)
            {
                backend.SetVolume(voice, 0.5f);
                backend.GetState(voice);
                backend.SubmitBuffer(voice, buffer, 0, buffer.Length);
                backend.GetPendingBufferCount(voice);
            }
        }

        for (var i = 0; i < 100; i++)
        {
            Frame();
        }

        var before = AllocationWindow.Start();
        for (var i = 0; i < 300; i++)
        {
            Frame();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.Equal(0, backend.DroppedChunkCount);
    }

    [Fact]
    public void AudioService_PlaysAClipAndRecyclesItsVoiceAfterTheEnd()
    {
        using var backend = Create(out var output);
        using var service = new AudioService(backend);
        var clip = Constant(8000, 2400);

        var voice = service.PlayClip(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default);

        Assert.True(voice.IsValid);
        Assert.True(output.Pump(512) > 0.01f);
        Assert.True(service.IsAlive(voice));

        output.Pump(4800);
        service.Update(0.016f);

        Assert.False(service.IsAlive(voice));
        Assert.Equal(0, backend.ActiveVoiceCount);
    }

    [Fact]
    public void AudioService_PlaysAStereoVoiceThroughStreamingAndRecyclesIt()
    {
        using var backend = Create(out var output);
        using var service = new AudioService(backend);
        var clip = Constant(8000, 4800, rate: 24000);

        var voice = service.PlayClipStereo(clip, AudioBusNames.Sfx, AudioVoiceParameters.Default, 1f, 0.5f);

        Assert.True(voice.IsValid);

        var peak = 0f;
        for (var i = 0; i < 400 && service.IsAlive(voice); i++)
        {
            service.Update(0.016f);
            peak = Math.Max(peak, output.Pump(768));
        }

        service.Update(0.016f);

        Assert.True(peak > 0.01f);
        Assert.False(service.IsAlive(voice));
        Assert.Equal(0, backend.ActiveVoiceCount);
    }
}
