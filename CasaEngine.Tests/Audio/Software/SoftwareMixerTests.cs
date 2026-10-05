using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Software;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

public class SoftwareMixerTests
{
    private const int OutputRate = 48000;

    private static float[] Render(SoftwareMixer mixer, int frames)
    {
        var buffer = new float[frames * 2];
        mixer.Render(buffer, frames);
        return buffer;
    }

    private static PcmAudioClip ConstantMono(short value, int frames, int rate = OutputRate)
    {
        var samples = new short[frames];
        Array.Fill(samples, value);
        return new PcmAudioClip(samples, rate, 1);
    }

    private static PcmAudioClip ConstantStereo(short left, short right, int frames, int rate = OutputRate)
    {
        var samples = new short[frames * 2];
        for (var i = 0; i < frames; i++)
        {
            samples[i * 2] = left;
            samples[i * 2 + 1] = right;
        }

        return new PcmAudioClip(samples, rate, 2);
    }

    private static byte[] StereoBytes(int firstFrame, int frames)
    {
        var bytes = new byte[frames * 4];
        for (var i = 0; i < frames; i++)
        {
            var value = (short)(((firstFrame + i) % 30000) - 15000);
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 4, 2), value);
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 4 + 2, 2), (short)-value);
        }

        return bytes;
    }

    private static List<MixerEvent> DrainEvents(SoftwareMixer mixer)
    {
        var events = new List<MixerEvent>();
        while (mixer.TryDequeueEvent(out var e))
        {
            events.Add(e);
        }

        return events;
    }

    [Fact]
    public void NoVoice_RendersExactSilence()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var output = new float[960];
        Array.Fill(output, 0.5f);

        mixer.Render(output, 480);

        Assert.All(output, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void MonoCentered_UsesConstantPowerGain()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryStartResidentVoice(0, 1, ConstantMono(16384, 1000), AudioVoiceParameters.Default);

        var output = Render(mixer, 100);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(0.5f * 0.70710678f, output[i * 2], 1e-6f);
            Assert.Equal(0.5f * 0.70710678f, output[i * 2 + 1], 1e-6f);
        }
    }

    [Fact]
    public void MonoHardPan_SendsEverythingToOneSide()
    {
        var left = new SoftwareMixer(OutputRate);
        left.TryStartResidentVoice(0, 1, ConstantMono(16384, 1000), new AudioVoiceParameters(1f, -1f, 0f, false));
        var leftOutput = Render(left, 50);

        var right = new SoftwareMixer(OutputRate);
        right.TryStartResidentVoice(0, 1, ConstantMono(16384, 1000), new AudioVoiceParameters(1f, 1f, 0f, false));
        var rightOutput = Render(right, 50);

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(0.5f, leftOutput[i * 2], 1e-6f);
            Assert.Equal(0f, leftOutput[i * 2 + 1], 1e-6f);
            Assert.Equal(0f, rightOutput[i * 2], 1e-6f);
            Assert.Equal(0.5f, rightOutput[i * 2 + 1], 1e-6f);
        }
    }

    [Fact]
    public void StereoCentered_PreservesChannelsExactly()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryStartResidentVoice(0, 1, ConstantStereo(8192, -4096, 1000), AudioVoiceParameters.Default);

        var output = Render(mixer, 100);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(0.25f, output[i * 2]);
            Assert.Equal(-0.125f, output[i * 2 + 1]);
        }
    }

    [Fact]
    public void StereoPannedRight_ScalesLeftChannel()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryStartResidentVoice(0, 1, ConstantStereo(8192, -4096, 1000), new AudioVoiceParameters(1f, 0.5f, 0f, false));

        var output = Render(mixer, 100);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(0.125f, output[i * 2], 1e-6f);
            Assert.Equal(-0.125f, output[i * 2 + 1], 1e-6f);
        }
    }

    [Fact]
    public void UnitStep_ReproducesSourceSamplesExactly()
    {
        var samples = new short[1000];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(i * 31 - 15000);
        }

        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryStartResidentVoice(0, 1, new PcmAudioClip(samples, OutputRate, 2), new AudioVoiceParameters(1f, 0f, 0f, false));

        var output = Render(mixer, 500);

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(samples[i] / 32768f, output[i]);
        }
    }

    [Fact]
    public void PitchUpOneOctave_HalvesTheDurationAndEndsOnce()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryStartResidentVoice(0, 7, ConstantMono(16384, 1000), new AudioVoiceParameters(1f, 0f, 1f, false));

        var output = Render(mixer, 600);

        for (var i = 0; i < 500; i++)
        {
            Assert.NotEqual(0f, output[i * 2]);
        }

        for (var i = 500; i < 600; i++)
        {
            Assert.Equal(0f, output[i * 2]);
        }

        var events = DrainEvents(mixer);
        var ended = Assert.Single(events);
        Assert.Equal(MixerEventKind.VoiceEnded, ended.Kind);
        Assert.Equal(0, ended.Slot);
        Assert.Equal(7, ended.Generation);

        Render(mixer, 600);
        Assert.Empty(DrainEvents(mixer));
        Assert.Equal(0, mixer.ActiveVoiceCount);
    }

    [Fact]
    public void ResampledSine_KeepsItsFrequency()
    {
        // 1000 Hz sine stored at 22050 Hz, rendered at 48000 Hz: over 0.5 s of output there are
        // 500 periods, i.e. 1000 zero crossings. Tolerance: 2 crossings.
        const int clipRate = 22050;
        var samples = new short[clipRate];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(Math.Sin(2.0 * Math.PI * 1000.0 * i / clipRate) * 20000.0);
        }

        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryStartResidentVoice(0, 1, new PcmAudioClip(samples, clipRate, 1), AudioVoiceParameters.Default);

        const int frames = OutputRate / 2;
        var output = Render(mixer, frames);

        var crossings = 0;
        for (var i = 1; i < frames; i++)
        {
            if (output[(i - 1) * 2] < 0f != output[i * 2] < 0f)
            {
                crossings++;
            }
        }

        Assert.InRange(crossings, 998, 1002);
    }

    [Fact]
    public void LoopedVoice_KeepsPlayingWithoutEndEvent()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryStartResidentVoice(0, 1, ConstantMono(16384, 100), new AudioVoiceParameters(1f, 0f, 0f, true));

        var output = Render(mixer, 1000);

        for (var i = 0; i < 1000; i++)
        {
            Assert.NotEqual(0f, output[i * 2]);
        }

        Assert.Empty(DrainEvents(mixer));
        Assert.Equal(1, mixer.ActiveVoiceCount);
    }

    [Fact]
    public void VolumeChange_RampsOverTheNextBlockThenHolds()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryStartResidentVoice(0, 1, ConstantStereo(20000, 20000, 1000), new AudioVoiceParameters(1f, 0f, 0f, true));
        var first = Render(mixer, 100);
        Assert.Equal(20000 / 32768f, first[198]);

        mixer.TrySetVolume(0, 1, 0f);
        var ramp = Render(mixer, 100);

        Assert.True(ramp[0] < first[198]);
        for (var i = 1; i < 100; i++)
        {
            Assert.True(ramp[i * 2] <= ramp[(i - 1) * 2]);
        }

        Assert.Equal(0f, ramp[198], 1e-6f);

        var after = Render(mixer, 100);
        Assert.All(after, sample => Assert.Equal(0f, sample));
    }

    [Fact]
    public void PauseKeepsPosition_ResumeContinues()
    {
        var samples = new short[2000];
        for (var i = 0; i < 1000; i++)
        {
            samples[i * 2] = (short)(i * 10);
            samples[i * 2 + 1] = (short)(i * 10);
        }

        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryStartResidentVoice(0, 1, new PcmAudioClip(samples, OutputRate, 2), AudioVoiceParameters.Default);

        Render(mixer, 10);
        mixer.TryPause(0, 1);
        var paused = Render(mixer, 10);
        Assert.All(paused, sample => Assert.Equal(0f, sample));

        mixer.TryResume(0, 1);
        var resumed = Render(mixer, 5);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal((10 + i) * 10 / 32768f, resumed[i * 2]);
        }
    }

    [Fact]
    public void Stop_SilencesAtNextRender_AndStaleGenerationIsIgnored()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryStartResidentVoice(3, 1, ConstantMono(16384, 5000), AudioVoiceParameters.Default);
        Assert.NotEqual(0f, Render(mixer, 10)[0]);

        mixer.TryStop(3, 1);
        Assert.All(Render(mixer, 10), sample => Assert.Equal(0f, sample));
        Assert.Equal(0, mixer.ActiveVoiceCount);

        mixer.TryStartResidentVoice(3, 2, ConstantMono(16384, 5000), AudioVoiceParameters.Default);
        mixer.TrySetVolume(3, 1, 0f);
        mixer.TryStop(3, 1);
        var output = Render(mixer, 10);

        Assert.NotEqual(0f, output[0]);
        Assert.Equal(1, mixer.ActiveVoiceCount);
    }

    [Fact]
    public void Streaming_ReportsConsumedBuffersInOrder()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryCreateStreamingVoice(2, 5, 2, OutputRate, AudioVoiceParameters.Default);
        mixer.TryStartStreamingVoice(2, 5);

        for (var sequence = 0; sequence < 3; sequence++)
        {
            Assert.True(mixer.TrySubmitStreamingBuffer(2, 5, StereoBytes(sequence * 100, 100), 10 + sequence));
        }

        var output = Render(mixer, 198);
        var events = DrainEvents(mixer);

        Assert.Equal(2, events.Count);
        Assert.Equal(10, events[0].Sequence);
        Assert.Equal(11, events[1].Sequence);
        Assert.All(events, e =>
        {
            Assert.Equal(MixerEventKind.BufferConsumed, e.Kind);
            Assert.Equal(2, e.Slot);
            Assert.Equal(5, e.Generation);
        });

        for (var i = 0; i < 198; i++)
        {
            var expected = ((i % 30000) - 15000) / 32768f;
            Assert.Equal(expected, output[i * 2]);
            Assert.Equal(-expected, output[i * 2 + 1]);
        }
    }

    [Fact]
    public void Streaming_DryQueueIsSilentAndStaysPlaying()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryCreateStreamingVoice(0, 1, 2, OutputRate, AudioVoiceParameters.Default);
        mixer.TryStartStreamingVoice(0, 1);
        mixer.TrySubmitStreamingBuffer(0, 1, StereoBytes(0, 100), 1);

        Render(mixer, 300);
        var events = DrainEvents(mixer);
        Assert.Single(events);
        Assert.Equal(MixerEventKind.BufferConsumed, events[0].Kind);

        Assert.All(Render(mixer, 300), sample => Assert.Equal(0f, sample));
        Assert.Empty(DrainEvents(mixer));
        Assert.Equal(1, mixer.ActiveVoiceCount);

        // New data resumes playback.
        mixer.TrySubmitStreamingBuffer(0, 1, StereoBytes(500, 100), 2);
        var resumed = Render(mixer, 50);
        Assert.Contains(resumed, sample => sample != 0f);
    }

    [Fact]
    public void Streaming_NotStartedVoiceIsSilentAndKeepsItsData()
    {
        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryCreateStreamingVoice(0, 1, 2, OutputRate, AudioVoiceParameters.Default);
        mixer.TrySubmitStreamingBuffer(0, 1, StereoBytes(0, 100), 1);

        Assert.All(Render(mixer, 50), sample => Assert.Equal(0f, sample));
        Assert.Empty(DrainEvents(mixer));
    }

    [Fact]
    public void Streaming_BufferLargerThanOneChunk_IsPlayedWithoutLoss()
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.Equal(4096, mixer.ChunkSamples);
        mixer.TryCreateStreamingVoice(0, 1, 2, OutputRate, AudioVoiceParameters.Default);
        mixer.TryStartStreamingVoice(0, 1);
        Assert.True(mixer.TrySubmitStreamingBuffer(0, 1, StereoBytes(0, 5000), 42));

        var output = Render(mixer, 5000);

        // The last two frames wait for the interpolation window's look-ahead.
        for (var i = 0; i < 4998; i++)
        {
            var expected = ((i % 30000) - 15000) / 32768f;
            Assert.Equal(expected, output[i * 2]);
            Assert.Equal(-expected, output[i * 2 + 1]);
        }

        var consumed = Assert.Single(DrainEvents(mixer));
        Assert.Equal(42, consumed.Sequence);
    }

    [Fact]
    public void Streaming_MonoAtLowerRate_IsResampled()
    {
        var bytes = new byte[2 * 1000];
        for (var i = 0; i < 1000; i++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 2, 2), (short)(Math.Sin(2.0 * Math.PI * 100.0 * i / 8000.0) * 20000.0));
        }

        var mixer = new SoftwareMixer(OutputRate);
        mixer.TryCreateStreamingVoice(0, 1, 1, 8000, AudioVoiceParameters.Default);
        mixer.TryStartStreamingVoice(0, 1);
        mixer.TrySubmitStreamingBuffer(0, 1, bytes, 1);

        // 1000 source frames at 8 kHz last 0.125 s = 6000 output frames; 100 Hz gives 25 crossings.
        var output = Render(mixer, 5900);
        var crossings = 0;
        for (var i = 1; i < 5900; i++)
        {
            if (output[(i - 1) * 2] < 0f != output[i * 2] < 0f)
            {
                crossings++;
            }
        }

        Assert.InRange(crossings, 23, 25);
    }

    [Fact]
    public void StopStreaming_ReturnsQueuedChunksToThePool()
    {
        var mixer = new SoftwareMixer(OutputRate, initialChunkCount: 2, maxChunkCount: 2);
        mixer.TryCreateStreamingVoice(0, 1, 2, OutputRate, AudioVoiceParameters.Default);
        Assert.True(mixer.TrySubmitStreamingBuffer(0, 1, StereoBytes(0, 100), 1));
        Assert.True(mixer.TrySubmitStreamingBuffer(0, 1, StereoBytes(0, 100), 2));
        Assert.False(mixer.TrySubmitStreamingBuffer(0, 1, StereoBytes(0, 100), 3));

        mixer.TryStop(0, 1);
        Render(mixer, 10);

        Assert.True(mixer.TrySubmitStreamingBuffer(0, 1, StereoBytes(0, 100), 4));
    }

    [Fact]
    public void CommandRing_ReportsFullAndKeepsOrder()
    {
        var mixer = new SoftwareMixer(OutputRate, commandCapacity: 4);
        Assert.Equal(4, mixer.CommandCapacity);

        Assert.True(mixer.TryStop(0, 1));
        Assert.True(mixer.TryStop(0, 1));
        Assert.True(mixer.TryStop(0, 1));
        Assert.True(mixer.TryStop(0, 1));
        Assert.False(mixer.TryStop(0, 1));

        Render(mixer, 1);
        Assert.True(mixer.TryStop(0, 1));
    }

    [Fact]
    public void Rings_PreserveOrderAcrossWrapAround()
    {
        var ring = new SpscRingBuffer<int>(4);
        var next = 0;
        var expected = 0;

        for (var round = 0; round < 50; round++)
        {
            for (var i = 0; i < 3; i++)
            {
                Assert.True(ring.TryEnqueue(next++));
            }

            for (var i = 0; i < 3; i++)
            {
                Assert.True(ring.TryDequeue(out var value));
                Assert.Equal(expected++, value);
            }
        }

        Assert.False(ring.TryDequeue(out _));
    }

    [Fact]
    public void EventRing_FullDoesNotLoseVoiceEnd()
    {
        var mixer = new SoftwareMixer(OutputRate, eventCapacity: 1);
        mixer.TryStartResidentVoice(0, 1, ConstantMono(1000, 4), AudioVoiceParameters.Default);
        mixer.TryStartResidentVoice(1, 1, ConstantMono(1000, 4), AudioVoiceParameters.Default);

        Render(mixer, 20);
        Assert.Single(DrainEvents(mixer));

        Render(mixer, 20);
        var second = Assert.Single(DrainEvents(mixer));
        Assert.Equal(MixerEventKind.VoiceEnded, second.Kind);
    }

    [Fact]
    public void Mix_IsHardClipped()
    {
        var mixer = new SoftwareMixer(OutputRate);
        for (var slot = 0; slot < 4; slot++)
        {
            mixer.TryStartResidentVoice(slot, 1, ConstantStereo(30000, -30000, 100), AudioVoiceParameters.Default);
        }

        var output = Render(mixer, 10);

        Assert.Equal(1f, output[0]);
        Assert.Equal(-1f, output[1]);
    }

    [Fact]
    public void Render_DoesNotAllocateWith64ActiveVoices()
    {
        const int block = 480;
        var mixer = new SoftwareMixer(OutputRate, initialChunkCount: 512);
        var residentClip = new PcmAudioClip(new short[1000], 44100, 1);
        for (var i = 0; i < 1000; i++)
        {
            residentClip.SampleArray[i] = (short)(Math.Sin(i * 0.1) * 12000.0);
        }

        var streamBytes = StereoBytes(0, block);
        var sequence = 0;

        for (var slot = 0; slot < 32; slot++)
        {
            mixer.TryStartResidentVoice(slot, 1, residentClip, new AudioVoiceParameters(0.3f, 0.2f, 0.1f, true));
        }

        for (var slot = 32; slot < 64; slot++)
        {
            mixer.TryCreateStreamingVoice(slot, 1, 2, OutputRate, new AudioVoiceParameters(0.3f, -0.2f, 0f, false));
            mixer.TryStartStreamingVoice(slot, 1);

            for (var i = 0; i < 3; i++)
            {
                Assert.True(mixer.TrySubmitStreamingBuffer(slot, 1, streamBytes, sequence++));
            }
        }

        var output = new float[block * 2];

        void Block(int iteration)
        {
            var volume = (iteration & 1) == 0 ? 0.3f : 0.5f;

            for (var slot = 0; slot < 64; slot++)
            {
                Assert.True(mixer.TrySetVolume(slot, 1, volume));
                Assert.True(mixer.TrySetParameters(slot, 1, new AudioVoiceParameters(volume, volume - 0.5f, slot < 32 ? 0.1f * (slot & 1) : 0f, slot < 32)));
            }

            for (var slot = 32; slot < 64; slot++)
            {
                Assert.True(mixer.TrySubmitStreamingBuffer(slot, 1, streamBytes, sequence++));
            }

            mixer.Render(output, block);

            while (mixer.TryDequeueEvent(out _))
            {
            }
        }

        // Warm up (JIT, first-use paths) before measuring.
        for (var i = 0; i < 20; i++)
        {
            Block(i);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1000; i++)
        {
            Block(i);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(before, after);
        Assert.Equal(64, mixer.ActiveVoiceCount);
        Assert.Equal(0, mixer.DroppedChunkCount);
        Assert.Equal(0, mixer.DroppedEventCount);
    }
}
