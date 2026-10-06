using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Effects;
using CasaEngine.Framework.Audio.Software;
using Xunit;

namespace CasaEngine.Tests.Audio.Software;

/// <summary>
/// Level metering on <see cref="SoftwareMixer"/> (plan T6.1, decision P22): peak and RMS of each bus after its own gain and of
/// the output, overs counted before the limiter, lock-free publication read by several readers, no allocation.
/// One render is one audio block (480 frames, 10 ms, ten periods of a 1 kHz sine at 48 kHz).
/// </summary>
public class SoftwareMixerMeteringTests
{
    private const int OutputRate = 48000;
    private const int Block = 480;
    private const double CeilingLinear = 0.89125093813374556; // -1 dBFS

    private static void Render(SoftwareMixer mixer, int blocks = 1)
    {
        var buffer = new float[Block * 2];

        for (var i = 0; i < blocks; i++)
        {
            mixer.Render(buffer, Block);
        }
    }

    private static int CreateBus(SoftwareMixer mixer, int parent = SoftwareMixer.MasterBus)
    {
        Assert.True(mixer.TryCreateBus(parent, out var bus));
        return bus;
    }

    private static PcmAudioClip SineClip(double amplitude)
    {
        var samples = new short[OutputRate];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Round(amplitude * 32767.0 * Math.Sin(2.0 * Math.PI * 1000.0 * i / OutputRate));
        }

        return new PcmAudioClip(samples, OutputRate, 1);
    }

    private static void StartSine(SoftwareMixer mixer, int slot, int bus, double amplitude)
    {
        Assert.True(mixer.TryStartResidentStereoVoice(slot, 1, SineClip(amplitude), AudioVoiceParameters.Default.WithLooping(true), 1f, 1f, bus));
    }

    private static void StartConstant(SoftwareMixer mixer, int slot, short value)
    {
        var samples = new short[400000];
        Array.Fill(samples, value);
        Assert.True(mixer.TryStartResidentStereoVoice(slot, 1, new PcmAudioClip(samples, OutputRate, 1), AudioVoiceParameters.Default, 1f, 1f));
    }

    [Fact]
    public void ASineOnABus_IsMeasuredAfterTheBusGain_OnTheBusMasterAndOutput()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var bus = CreateBus(mixer);
        mixer.SetBusGain(bus, 0.5f);
        StartSine(mixer, 0, bus, 0.8);
        Render(mixer, 5);

        var cursor = new AudioMeterCursor();
        var levels = new AudioLevel[SoftwareMixer.BusCapacity];
        mixer.ReadLevels(ref cursor, levels, out _);
        Render(mixer);
        var read = mixer.ReadLevels(ref cursor, levels, out var output);

        Assert.Equal(1, read.BlockCount);
        Assert.Equal(0, read.MissedBlockCount);
        Assert.Equal(Block, read.FrameCount);

        // 0.8 through a 0.5 gain: 0.4 peak, 0.4 / sqrt(2) RMS, on both channels.
        Assert.Equal(0.4f, levels[bus].PeakLeft, 1e-4f);
        Assert.Equal(0.4f, levels[bus].PeakRight, 1e-4f);
        Assert.Equal(0.4f, levels[bus].Peak, 1e-4f);
        Assert.Equal(0.4 / Math.Sqrt(2.0), levels[bus].Rms, 1e-4);
        Assert.Equal(0.4f, levels[SoftwareMixer.MasterBus].Peak, 1e-4f);
        Assert.Equal(0.4f, output.Peak, 1e-4f);
        Assert.Equal(0.4 / Math.Sqrt(2.0), output.Rms, 1e-4);
        Assert.Equal(0, output.Overs);
    }

    [Fact]
    public void AVoiceOnAChildBus_IsMeasuredOnTheChildAndOnItsParentAfterEachGain()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var parent = CreateBus(mixer);
        var child = CreateBus(mixer, parent);
        var sibling = CreateBus(mixer);
        mixer.SetBusGain(child, 0.5f);
        mixer.SetBusGain(parent, 0.5f);
        StartSine(mixer, 0, child, 0.8);
        Render(mixer, 5);

        var cursor = new AudioMeterCursor();
        var levels = new AudioLevel[SoftwareMixer.BusCapacity];
        mixer.ReadLevels(ref cursor, levels, out _);
        Render(mixer);
        mixer.ReadLevels(ref cursor, levels, out var output);

        Assert.Equal(0.4f, levels[child].Peak, 1e-4f);
        Assert.Equal(0.2f, levels[parent].Peak, 1e-4f);
        Assert.Equal(0.2 / Math.Sqrt(2.0), levels[parent].Rms, 1e-4);
        Assert.Equal(0.2f, levels[SoftwareMixer.MasterBus].Peak, 1e-4f);
        Assert.Equal(0.2f, output.Peak, 1e-4f);
        Assert.Equal(0f, levels[sibling].Peak);
        Assert.Equal(0f, levels[sibling].Rms);
    }

    [Fact]
    public void TheOvers_AreCountedBeforeTheLimiter_WhileTheOutputStaysUnderTheCeiling()
    {
        var mixer = new SoftwareMixer(OutputRate);
        Assert.True(mixer.TrySetMasterLimiter(new LimiterEffect()));
        StartConstant(mixer, 0, short.MaxValue);
        StartConstant(mixer, 1, short.MaxValue);
        Render(mixer, 20);

        var cursor = new AudioMeterCursor();
        var levels = new AudioLevel[SoftwareMixer.BusCapacity];
        mixer.ReadLevels(ref cursor, levels, out _);
        Render(mixer);
        mixer.ReadLevels(ref cursor, levels, out var output);

        // 2 x 0.99997 on Master: every one of the 960 samples of the block is beyond full scale.
        Assert.Equal(Block * 2, output.Overs);
        Assert.True(levels[SoftwareMixer.MasterBus].Peak > 1.99f);
        Assert.True(output.Peak <= CeilingLinear + 1e-4, $"output peak {output.Peak} over the -1 dBFS ceiling");
        Assert.True(output.Peak > 0.85f);
    }

    [Fact]
    public void TheOvers_AreCountedWithoutALimiter_AndTheOutputIsHardClipped()
    {
        var mixer = new SoftwareMixer(OutputRate);
        StartConstant(mixer, 0, short.MaxValue);
        StartConstant(mixer, 1, short.MaxValue);
        Render(mixer, 3);

        var cursor = new AudioMeterCursor();
        var levels = new AudioLevel[SoftwareMixer.BusCapacity];
        mixer.ReadLevels(ref cursor, levels, out _);
        Render(mixer);
        mixer.ReadLevels(ref cursor, levels, out var output);

        Assert.Equal(Block * 2, output.Overs);
        Assert.Equal(1f, output.Peak);
    }

    [Fact]
    public void ASilentBlock_ReadsZero_AndAReadWithNothingNewReportsNoBlock()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var cursor = new AudioMeterCursor();
        var levels = new AudioLevel[SoftwareMixer.BusCapacity];
        levels[0] = new AudioLevel(1f, 1f, 1f, 1);

        var empty = mixer.ReadLevels(ref cursor, levels, out var output);
        Assert.Equal(0, empty.BlockCount);
        Assert.Equal(0f, levels[0].Peak);
        Assert.Equal(0f, output.Peak);

        Render(mixer, 2);
        var silent = mixer.ReadLevels(ref cursor, levels, out output);
        Assert.Equal(2, silent.BlockCount);
        Assert.Equal(0f, output.Peak);
        Assert.Equal(0f, output.Rms);

        Assert.Equal(0, mixer.ReadLevels(ref cursor, levels, out _).BlockCount);
    }

    [Fact]
    public void TwoReadersAtDifferentRates_BothSeeThePeakOfOneIsolatedBlock()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var bus = CreateBus(mixer);
        var levels = new AudioLevel[SoftwareMixer.BusCapacity];
        var fast = new AudioMeterCursor();
        var slow = new AudioMeterCursor();
        Render(mixer, 3);
        mixer.ReadLevels(ref fast, levels, out _);
        mixer.ReadLevels(ref slow, levels, out _);

        // One block of a 0.5 constant, then silence: the clip lasts exactly one block.
        var samples = new short[Block];
        Array.Fill(samples, (short)16384);
        Assert.True(mixer.TryStartResidentStereoVoice(0, 1, new PcmAudioClip(samples, OutputRate, 1), AudioVoiceParameters.Default, 1f, 1f, bus));

        var fastSightings = 0;
        var slowSightings = 0;
        var missed = 0;

        for (var block = 1; block <= 36; block++)
        {
            Render(mixer);

            if (block % 3 == 0)
            {
                var read = mixer.ReadLevels(ref fast, levels, out _);
                missed += read.MissedBlockCount;
                fastSightings += Math.Abs(levels[bus].Peak - 0.5f) < 1e-4f ? 1 : 0;
            }

            if (block % 12 == 0)
            {
                var read = mixer.ReadLevels(ref slow, levels, out _);
                missed += read.MissedBlockCount;
                slowSightings += Math.Abs(levels[bus].Peak - 0.5f) < 1e-4f ? 1 : 0;
            }
        }

        Assert.Equal(1, fastSightings);
        Assert.Equal(1, slowSightings);
        Assert.Equal(0, missed);
    }

    [Fact]
    public void AReaderThatWaitsMoreThanTheHistory_IsToldHowManyBlocksItMissed()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var cursor = new AudioMeterCursor();
        var levels = new AudioLevel[SoftwareMixer.BusCapacity];
        Render(mixer);
        mixer.ReadLevels(ref cursor, levels, out _);

        Render(mixer, SoftwareMixer.MeterHistoryBlocks + 10);
        var read = mixer.ReadLevels(ref cursor, levels, out _);

        Assert.Equal(SoftwareMixer.MeterHistoryBlocks, read.BlockCount);
        Assert.Equal(10, read.MissedBlockCount);
    }

    [Fact]
    public void ARequestLargerThanTheBusBuffers_PublishesOneEntryPerMixedBlock()
    {
        var mixer = new SoftwareMixer(OutputRate, maxBlockFrames: 100);
        var cursor = new AudioMeterCursor();
        var levels = new AudioLevel[SoftwareMixer.BusCapacity];
        mixer.Render(new float[250 * 2], 250);

        var read = mixer.ReadLevels(ref cursor, levels, out _);

        Assert.Equal(3, read.BlockCount);
        Assert.Equal(250, read.FrameCount);
    }

    [Fact]
    public void ReadingWhileTheAudioThreadRenders_NeverSeesATornRecord()
    {
        var mixer = new SoftwareMixer(OutputRate);
        var bus = CreateBus(mixer);
        StartSine(mixer, 0, bus, 0.5);
        Render(mixer, 2);
        using var stop = new CancellationTokenSource();

        var renderer = Task.Run(() =>
        {
            var buffer = new float[Block * 2];

            while (!stop.IsCancellationRequested)
            {
                mixer.Render(buffer, Block);
            }
        });

        var cursor = new AudioMeterCursor();
        var levels = new AudioLevel[SoftwareMixer.BusCapacity];
        var reads = 0;
        var deadline = Environment.TickCount64 + 300;

        while (Environment.TickCount64 < deadline)
        {
            var read = mixer.ReadLevels(ref cursor, levels, out var output);

            if (read.BlockCount > 0)
            {
                reads++;

                // Every block of the sine is the same: a record torn between two blocks or two writes could not match.
                Assert.Equal(0.5f, levels[bus].Peak, 1e-4f);
                Assert.Equal(levels[bus].Peak, output.Peak, 1e-6f);
                Assert.Equal(0.5 / Math.Sqrt(2.0), levels[bus].Rms, 1e-4);
            }
        }

        stop.Cancel();
        renderer.Wait();
        Assert.True(reads > 0);
    }

    [Fact]
    public void RenderingAndReadingWithFullBuses_AllocateNothing()
    {
        var mixer = new SoftwareMixer(OutputRate);

        for (var i = 1; i < SoftwareMixer.BusCapacity; i++)
        {
            CreateBus(mixer);
        }

        for (var voice = 0; voice < 32; voice++)
        {
            var samples = new short[48000];
            Array.Fill(samples, (short)3000);
            Assert.True(mixer.TryStartResidentStereoVoice(voice, 1, new PcmAudioClip(samples, OutputRate, 1), AudioVoiceParameters.Default.WithLooping(true), 0.3f, 0.3f, 1 + voice));
        }

        Assert.True(mixer.TrySetMasterLimiter(new LimiterEffect()));
        var buffer = new float[Block * 2];
        var levels = new AudioLevel[SoftwareMixer.BusCapacity];
        var cursor = new AudioMeterCursor();
        mixer.Render(buffer, Block);
        mixer.ReadLevels(ref cursor, levels, out _);

        var before = AllocationWindow.Start();

        for (var round = 0; round < 50; round++)
        {
            mixer.Render(buffer, Block);
            mixer.ReadLevels(ref cursor, levels, out _);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(levels[1].Peak > 0f);
    }
}
