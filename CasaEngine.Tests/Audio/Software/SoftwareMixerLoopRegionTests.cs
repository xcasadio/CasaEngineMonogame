using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Software;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>Loop region and rate multiplier of resident voices (ADR-0056, P11/P12).</summary>
public class SoftwareMixerLoopRegionTests
{
    private const int OutputRate = 48000;

    private static float[] Render(SoftwareMixer mixer, int frames)
    {
        var buffer = new float[frames * 2];
        mixer.Render(buffer, frames);
        return buffer;
    }

    private static PcmAudioClip Noise(int frames, int rate = OutputRate)
    {
        var samples = new short[frames];
        var random = new Random(1234);

        for (var i = 0; i < frames; i++)
        {
            samples[i] = (short)random.Next(-20000, 20000);
        }

        return new PcmAudioClip(samples, rate, 1);
    }

    private static float Hermite(float y0, float y1, float y2, float y3, float t)
    {
        var c1 = 0.5f * (y2 - y0);
        var c2 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
        var c3 = 0.5f * (y3 - y0) + 1.5f * (y1 - y2);
        return ((c3 * t + c2) * t + c1) * t + y1;
    }

    private static int Wrap(int index, int start, int end)
    {
        var length = end - start;
        var wrapped = (index - start) % length;
        return start + (wrapped < 0 ? wrapped + length : wrapped);
    }

    private static AudioVoiceParameters Looped(int start, int end)
    {
        return AudioVoiceParameters.Default.WithLooping(true).WithLoopRegion(start, end);
    }

    [Fact]
    public void LoopedRegion_ReplaysExactlyTheRegionFramesAtStepOne()
    {
        var clip = Noise(3000);
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, clip, Looped(1000, 2000), 1f, 1f));

        var output = Render(mixer, 1000 + 3 * 1000 + 17);

        // Frames 0..1999 play once (intro then region), then the region 1000..1999 repeats.
        for (var i = 0; i < 2000; i++)
        {
            Assert.Equal(clip.SampleArray[i] / 32768f, output[i * 2]);
        }

        for (var i = 2000; i < output.Length / 2; i++)
        {
            var expected = clip.SampleArray[1000 + (i - 2000) % 1000] / 32768f;
            Assert.Equal(expected, output[i * 2]);
            Assert.Equal(expected, output[i * 2 + 1]);
        }

        Assert.Empty(DrainEvents(mixer));
    }

    [Fact]
    public void LoopedRegion_AtAFractionalStep_MatchesTheRegionWrappedHermiteReference()
    {
        const int start = 1000;
        const int end = 2000;
        const int sourceRate = 44100;
        var clip = Noise(3000, sourceRate);
        var data = clip.SampleArray;
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, clip, Looped(start, end), 1f, 1f));

        const int frames = 6000;
        var output = Render(mixer, frames);

        var step = (double)sourceRate / OutputRate;
        var position = 0.0;
        var seamFrames = 0;

        for (var i = 0; i < frames; i++)
        {
            if (position >= end)
            {
                position -= end;
                while (position >= end - start)
                {
                    position -= end - start;
                }

                position += start;
            }

            var index = (int)position;
            var t = (float)(position - index);
            float Sample(int n)
            {
                if (n >= end || (n < start && index >= start))
                {
                    n = Wrap(n, start, end);
                }

                return data[Math.Clamp(n, 0, data.Length - 1)] / 32768f;
            }

            var expected = Hermite(Sample(index - 1), Sample(index), Sample(index + 1), Sample(index + 2), t);
            Assert.Equal(expected, output[i * 2], 1e-6f);

            if (index >= end - 3 || (index >= start && index < start + 3))
            {
                seamFrames++;
            }

            position += step;
        }

        // The run crosses the seam several times: the reference really exercised the wrapped neighbours.
        Assert.True(seamFrames > 20);
    }

    [Fact]
    public void LoopedRegion_OnASmoothSignal_HasNoClickAtTheSeam()
    {
        // One region = exactly 5 periods of a sine, so a correct wrap is continuous.
        var samples = new short[3000];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)(Math.Sin(i * 2.0 * Math.PI / 200.0) * 10000.0);
        }

        var clip = new PcmAudioClip(samples, 44100, 1);
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, clip, Looped(1000, 2000), 1f, 1f));

        var output = Render(mixer, 8000);
        var maxDelta = 0f;

        for (var i = 3000; i < 8000; i++)
        {
            maxDelta = Math.Max(maxDelta, Math.Abs(output[i * 2] - output[(i - 1) * 2]));
        }

        // Maximum slope of the signal is 10000/32768 * 2 pi / 200 * 0.92 = 0.0088 per output frame.
        Assert.True(maxDelta < 0.01f, $"largest sample to sample jump was {maxDelta}");
    }

    [Fact]
    public void RegionInvalidAgainstTheClip_LoopsTheWholeClip()
    {
        var clip = Noise(1500);

        foreach (var (start, end) in new[] { (-1, 500), (500, 2000), (700, 700), (900, 300) })
        {
            var mixer = new SoftwareMixer(OutputRate);
            Assert.True(mixer.TryStartResidentStereoVoice(0, 1, clip, Looped(start, end), 1f, 1f));

            var output = Render(mixer, 3200);

            for (var i = 0; i < 3200; i++)
            {
                Assert.Equal(clip.SampleArray[i % 1500] / 32768f, output[i * 2]);
            }
        }
    }

    [Fact]
    public void NonLoopedVoice_IgnoresTheRegion()
    {
        var clip = Noise(3000);
        var mixer = new SoftwareMixer(OutputRate);
        var parameters = AudioVoiceParameters.Default.WithLoopRegion(1000, 2000);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, clip, parameters, 1f, 1f));

        var output = Render(mixer, 3100);

        for (var i = 0; i < 3000; i++)
        {
            Assert.Equal(clip.SampleArray[i] / 32768f, output[i * 2]);
        }

        Assert.Equal(0f, output[3050 * 2]);
        Assert.Single(DrainEvents(mixer));
    }

    [Fact]
    public void SetParameters_CanChangeTheRegionOfARunningVoice()
    {
        var clip = Noise(3000);
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryStartResidentVoice(0, 1, clip, AudioVoiceParameters.Default.WithLooping(true)));
        Render(mixer, 100);

        Assert.True(mixer.TrySetParameters(0, 1, Looped(1000, 2000)));
        var output = Render(mixer, 3000);

        // From frame 100 on the voice runs through 100..1999, then loops 1000..1999.
        var checkedFrames = 0;
        for (var i = 2000 - 100; i < 2900; i++)
        {
            var source = i + 100 < 2000 ? i + 100 : 1000 + (i + 100 - 2000) % 1000;
            Assert.Equal(clip.SampleArray[source] / 32768f * (float)Math.Cos(Math.PI / 4.0), output[i * 2], 1e-4f);
            checkedFrames++;
        }

        Assert.True(checkedFrames > 500);
    }

    [Fact]
    public void RateMultiplierFour_EndsANonLoopedClipAfterAQuarterOfTheFrames()
    {
        var clip = Noise(4000);
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TryStartResidentVoice(0, 1, clip, AudioVoiceParameters.Default.WithRateMultiplier(4f)));

        Render(mixer, 999);
        Assert.Empty(DrainEvents(mixer));

        Render(mixer, 2);
        var events = DrainEvents(mixer);
        Assert.Single(events);
        Assert.Equal(MixerEventKind.VoiceEnded, events[0].Kind);
    }

    [Fact]
    public void RateMultiplier_ComposesWithPitchAndOnStereoVoices()
    {
        var clip = Noise(4000);
        var mixer = new SoftwareMixer(OutputRate);
        // pitch +1 (x2) times multiplier 2 = step 4.
        var parameters = AudioVoiceParameters.Default.WithPitch(1f).WithRateMultiplier(2f);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, clip, parameters, 1f, 1f));

        var output = Render(mixer, 100);

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(clip.SampleArray[i * 4] / 32768f, output[i * 2]);
        }
    }

    [Fact]
    public void StreamingVoice_HonoursTheMultiplierAndIgnoresTheRegion()
    {
        float[] RenderStream(AudioVoiceParameters parameters)
        {
            var mixer = new SoftwareMixer(OutputRate);
            Assert.True(mixer.TryCreateStreamingVoice(0, 1, 1, OutputRate, parameters));
            Assert.True(mixer.TryStartStreamingVoice(0, 1));

            var bytes = new byte[2000 * 2];
            for (var i = 0; i < 2000; i++)
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(i * 2, 2), (short)(i * 10));
            }

            Assert.True(mixer.TrySubmitStreamingBuffer(0, 1, bytes, 0));
            return Render(mixer, 400);
        }

        var normal = RenderStream(AudioVoiceParameters.Default);
        var doubled = RenderStream(AudioVoiceParameters.Default.WithLooping(true).WithLoopRegion(5, 9).WithRateMultiplier(2f));

        for (var i = 10; i < 150; i++)
        {
            Assert.Equal(normal[i * 4], doubled[i * 2], 1e-6f);
        }
    }

    [Fact]
    public void Render_WithRegionsAndMultipliers_DoesNotAllocate()
    {
        const int block = 480;
        var mixer = new SoftwareMixer(OutputRate);
        var clip = Noise(3000, 44100);

        for (var slot = 0; slot < 16; slot++)
        {
            var parameters = Looped(1000 + slot, 2000 - slot).WithRateMultiplier(1f + slot * 0.25f);
            Assert.True(slot % 2 == 0
                ? mixer.TryStartResidentVoice(slot, 1, clip, parameters)
                : mixer.TryStartResidentStereoVoice(slot, 1, clip, parameters, 0.5f, 0.25f));
        }

        var output = new float[block * 2];

        void Block(int iteration)
        {
            for (var slot = 0; slot < 16; slot++)
            {
                var parameters = Looped(1000 + slot, 2000 - slot).WithRateMultiplier(iteration % 2 == 0 ? 2f : 3f);
                Assert.True(mixer.TrySetParameters(slot, 1, parameters));
            }

            mixer.Render(output, block);
        }

        for (var i = 0; i < 20; i++)
        {
            Block(i);
        }

        var before = AllocationWindow.Start();

        for (var i = 0; i < 1000; i++)
        {
            Block(i);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(before, after);
        Assert.Equal(16, mixer.ActiveVoiceCount);
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
}
