using CasaEngine.Framework.Audio.Psx;
using Xunit;

namespace CasaEngine.Tests.Audio.Psx;

// The rules of ADR-0084 at full scale, ahead of the envelope and of the voice volume, on synthetic values.
public partial class PsxSpuTests
{
    // Hands the decoded samples to the test, in the order they enter the interpolation window.
    private sealed class DecodedSampleProbe : IPsxSpuInterpolator
    {
        public readonly List<int> Samples = new();

        public int Interpolate(int oldest, int older, int old, int newest, int index)
        {
            if (Samples.Count == 0)
            {
                Samples.Add(oldest);
                Samples.Add(older);
                Samples.Add(old);
            }

            Samples.Add(newest);
            return newest;
        }
    }

    private static List<int> DecodedSamples(params Block[] blocks)
    {
        var probe = new DecodedSampleProbe();
        var spu = new PsxSpu(Tables(), probe);
        var address = 0x100 * 8;
        foreach (var block in blocks)
        {
            spu.WriteRam(address, ToBytes(block));
            address += 16;
        }

        spu.SetStartAddress(0, 0x100);
        spu.SetPitch(0, 0x1000);
        spu.SetAdsr(0, FullEnvelope);
        spu.KeyOn(1);
        RenderLeft(spu, blocks.Length * 28 - 3);
        return probe.Samples;
    }

    [Theory]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void Adpcm_AShiftAbove12GivesMinus128ForEveryNegativeNibbleAndZeroForTheOthers(int shift)
    {
        var nibbles = new int[28];
        for (var i = 0; i < 16; i++)
        {
            nibbles[i] = i - 8;
        }

        var samples = DecodedSamples(new Block(shift, 0, 0, nibbles));

        for (var i = 0; i < 28; i++)
        {
            Assert.Equal(nibbles[i] < 0 ? -128 : 0, samples[i]);
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Adpcm_FiltersFiveToSevenAreFilterZero(int filter)
    {
        // The first block leaves a non-zero history: filter 0 ignores it, every other filter would not.
        var first = new Block(8, 0, 0, Enumerable.Repeat(7, 28).ToArray());
        var nibbles = new int[28];
        nibbles[0] = 1;
        var samples = DecodedSamples(first, new Block(8, filter, 0, nibbles));

        Assert.Equal(16, samples[28]);
        for (var i = 29; i < 56; i++)
        {
            Assert.Equal(0, samples[i]);
        }
    }

    [Fact]
    public void KeyOn_RestartsAPlayedVoiceLikeAFreshOne()
    {
        // Filters 1 to 3 keep a history, pitch 1100h a fraction, the attack and the release a counter between two steps.
        var word = Adsr(false, 12, 2, 0, 0xF, false, false, 31, 3, false, 13);
        var blocks = new[] { MakeBlock(8, 1, 0, 91), MakeBlock(8, 2, 0, 92), MakeBlock(8, 3, 3, 93) };
        var replayedSamples = new DecodedSampleProbe();
        var freshSamples = new DecodedSampleProbe();

        PsxSpu Build(DecodedSampleProbe probe)
        {
            var spu = new PsxSpu(Tables(), probe);
            for (var i = 0; i < blocks.Length; i++)
            {
                spu.WriteRam((0x100 + i * 2) * 8, ToBytes(blocks[i]));
            }

            spu.SetStartAddress(0, 0x100);
            spu.SetPitch(0, 0x1100);
            spu.SetAdsr(0, word);
            spu.SetVolume(0, false, MaxVolumeRegister);
            return spu;
        }

        var replayed = Build(replayedSamples);
        var fresh = Build(freshSamples);

        replayed.KeyOn(1);
        RenderLeft(replayed, 41);
        replayed.KeyOff(1);
        RenderLeft(replayed, 3);
        replayedSamples.Samples.Clear();

        replayed.KeyOn(1);
        fresh.KeyOn(1);

        for (var n = 0; n < 200; n++)
        {
            Assert.Equal(RenderLeft(fresh, 1), RenderLeft(replayed, 1));
            Assert.Equal(fresh.GetEnvx(0), replayed.GetEnvx(0));
            Assert.Equal(fresh.GetCurrentAddress(0), replayed.GetCurrentAddress(0));
        }

        Assert.Equal(freshSamples.Samples, replayedSamples.Samples);
    }

    [Fact]
    public void Gaussian_RoundsEachTermDownAndReadsTheTableAtTheIndex()
    {
        // Index 40h. By hand, with the negative products rounded down: 2001h * -3 = -24579 gives -2, 1001h * -5 = -20485
        // gives -2, 1001h * -7 = -28679 gives -2, 1801h * -11 = -67595 gives -5. The sum -11 is odd, so -6 and not -5.
        var table = new short[512];
        table[0xFF - 0x40] = 0x2001;
        table[0x1FF - 0x40] = 0x1001;
        table[0x100 + 0x40] = 0x1001;
        table[0x40] = 0x1801;
        var interpolator = new PsxGaussianInterpolator(new PsxSpuHardwareTables(Positive, Negative, gaussian: table));

        Assert.Equal(-6, interpolator.Interpolate(-3, -5, -7, -11, 0x40));
    }

    [Fact]
    public void Gaussian_SaturatesTheSum()
    {
        var table = new short[512];
        table[0xFF] = table[0x1FF] = table[0x100] = table[0] = 0x7FFF;
        var interpolator = new PsxGaussianInterpolator(new PsxSpuHardwareTables(Positive, Negative, gaussian: table));

        Assert.Equal(short.MaxValue, interpolator.Interpolate(0x7FFF, 0x7FFF, 0x7FFF, 0x7FFF, 0));
        Assert.Equal(short.MinValue, interpolator.Interpolate(-0x8000, -0x8000, -0x8000, -0x8000, 0));
    }

    [Fact]
    public void Adsr_AnAttackLandingOn7FFEhTakesOneMoreAttackStep()
    {
        // Attack shift 11, step 1: +6 per frame, and 7FFEh is 5461 * 6.
        var word = Adsr(false, 11, 1, 0, 0xF, false, false, 31, 3, false, 0);
        var spu = LoopingVoiceSpu(word, out _, 100);
        spu.KeyOn(1);

        RenderLeft(spu, 5462);
        Assert.Equal(0x7FFE, spu.GetEnvx(0));

        RenderLeft(spu, 1);
        Assert.Equal(0x7FFF, spu.GetEnvx(0));
    }
}
